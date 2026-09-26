using System;
using System.Threading;
using Sokoban.Core.Data;
using Sokoban.Core.Identity;
using Sokoban.Core.Validation;
namespace Sokoban.Core.Rules
{
    public enum WitnessFailure { None, InvalidStructure, MissingWitness, RulesMismatch, LevelMismatch, FingerprintMismatch, InvalidPath, MoveLimit, Cancelled, IllegalMove, NotWon }
    public sealed class WitnessVerification
    {
        public bool IsValid => Failure == WitnessFailure.None;
        public WitnessFailure Failure { get; internal set; } = WitnessFailure.MissingWitness;
        public string Reason { get; internal set; } = "缺少证据。";
        public int Moves { get; internal set; }
        public int Pushes { get; internal set; }
        public int FailedMoveIndex { get; internal set; } = -1;
        public BoardState FinalState { get; internal set; }
    }
    public static class WitnessVerifier
    {
        /// <summary>Checks a root-bound path, never source trust or optimality. Counters report replayed legal moves.</summary>
        public static WitnessVerification Verify(LevelData root, WitnessData witness, int maxMoves = ContentLimits.MaxWitnessMoves, CancellationToken cancellationToken = default)
        {
            var result = new WitnessVerification();
            if (cancellationToken.IsCancellationRequested)
                return Fail(result, WitnessFailure.Cancelled, "验证已取消。");
            if (!StructureValidator.Validate(root).IsValid)
                return Fail(result, WitnessFailure.InvalidStructure, "根局面的玩法结构无效。");
            if (witness == null)
                return result;
            if (witness.rulesVersion != ContentLimits.RulesVersion)
                return Fail(result, WitnessFailure.RulesMismatch, "证据规则版本不匹配。");
            if (!string.Equals(witness.levelId, root.levelId, StringComparison.Ordinal))
                return Fail(result, WitnessFailure.LevelMismatch, "证据关卡身份不匹配。");
            // Snapshot once so fingerprint and replay always refer to the same root.
            var state = new BoardState(root);
            if (!string.Equals(witness.levelFingerprint, LevelFingerprint.Compute(state.ToLevelData()), StringComparison.Ordinal))
                return Fail(result, WitnessFailure.FingerprintMismatch, "证据指纹与根局面不匹配。");
            string path = witness.moves;
            if (path == null)
                return Fail(result, WitnessFailure.InvalidPath, "证据路径缺失。");
            if (maxMoves < 0 || path.Length > Math.Min(maxMoves, ContentLimits.MaxWitnessMoves))
                return Fail(result, WitnessFailure.MoveLimit, "证据路径超过验证预算。");
            result.FinalState = state;
            for (int index = 0; index < path.Length; index++)
            {
                if (cancellationToken.IsCancellationRequested)
                    return Fail(result, WitnessFailure.Cancelled, "验证已取消。", index);
                if (!SokobanRules.TryParseDirection(path[index], out var direction))
                    return Fail(result, WitnessFailure.InvalidPath, "证据只能包含 U/D/L/R。", index);
                var move = SokobanRules.TryMove(state, direction);
                if (!move.Succeeded)
                    return Fail(result, WitnessFailure.IllegalMove, "证据包含非法移动：" + move.Failure, index);
                state = move.State;
                result.FinalState = state;
                result.Moves++;
                if (move.Pushed)
                    result.Pushes++;
            }
            if (cancellationToken.IsCancellationRequested)
                return Fail(result, WitnessFailure.Cancelled, "验证已取消。");
            if (!state.IsWon)
                return Fail(result, WitnessFailure.NotWon, "证据结束时尚未通关。");
            result.Failure = WitnessFailure.None;
            result.Reason = "每步合法且最终通关。";
            return result;
        }
        private static WitnessVerification Fail(WitnessVerification result, WitnessFailure failure, string reason, int index = -1)
        {
            result.Failure = failure;
            result.Reason = reason;
            result.FailedMoveIndex = index;
            return result;
        }
    }
}
