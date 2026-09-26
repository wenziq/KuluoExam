using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Sokoban.Core.Data;
using Sokoban.Core.Identity;
using Sokoban.Core.Rules;
namespace Sokoban.Domain.Analysis
{
    public readonly struct PushStep
    {
        public readonly int BoxFrom;
        public readonly Direction Direction;
        public PushStep(int boxFrom, Direction direction) { BoxFrom = boxFrom; Direction = direction; }
    }
    public static class SolutionReconstructor
    {
        public static WitnessData Reconstruct(LevelData root, IReadOnlyList<PushStep> pushes, CancellationToken token = default, Action checkpoint = null)
        {
            var state = new BoardState(root); var path = new StringBuilder();
            foreach (var step in pushes)
            {
                checkpoint?.Invoke(); token.ThrowIfCancellationRequested();
                int support = Reachability.Neighbor(step.BoxFrom, Reachability.Opposite(step.Direction), state.Width, state.Height);
                string walk = Reachability.WalkPath(state, support, token, checkpoint);
                foreach (char c in walk)
                {
                    checkpoint?.Invoke();
                    SokobanRules.TryParseDirection(c, out var direction); var move = SokobanRules.TryMove(state, direction);
                    if (!move.Succeeded || move.Pushed) throw new InvalidOperationException("步行重建与共同规则不一致。");
                    state = move.State; path.Append(c);
                }
                var push = SokobanRules.TryMove(state, step.Direction);
                if (!push.Succeeded || !push.Pushed) throw new InvalidOperationException("推动重建与共同规则不一致。");
                state = push.State; path.Append(SokobanRules.ToCharacter(step.Direction));
                if (path.Length > ContentLimits.MaxWitnessMoves) throw new InvalidOperationException("重建路径超过证据长度限制。");
            }
            var witness = new WitnessData { levelId = root.levelId, levelFingerprint = LevelFingerprint.Compute(root), moves = path.ToString(), source = WitnessSource.Solver };
            checkpoint?.Invoke();
            var verified = WitnessVerifier.Verify(root, witness, cancellationToken: token);
            checkpoint?.Invoke();
            if (!verified.IsValid || verified.Pushes != pushes.Count) throw new InvalidOperationException("解法重放失败：" + verified.Reason);
            return witness;
        }
    }
}
