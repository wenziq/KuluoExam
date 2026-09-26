using System;
using System.Collections.Generic;
using System.Threading;
using Sokoban.Core.Data;
using Sokoban.Core.Identity;
using Sokoban.Core.Rules;
namespace Sokoban.Domain.Analysis
{
    public sealed class PushObservation
    {
        public int PushIndex { get; }
        public int MoveIndex { get; }
        public string BoxId { get; }
        public Coordinate From { get; }
        public Coordinate To { get; }
        public bool SwitchedBox { get; }
        public bool LeftGoal { get; }
        internal PushObservation(int push, int move, string box, Coordinate from, Coordinate to, bool switched, bool left)
        { PushIndex=push; MoveIndex=move; BoxId=box; From=from; To=to; SwitchedBox=switched; LeftGoal=left; }
    }
    /// <summary>Observations of one verified path, not global minima or a player difficulty score.</summary>
    public sealed class ReferenceSolutionMetrics
    {
        public const string Version = "reference-metrics-v2";
        public string LevelFingerprint { get; }
        public int Pushes { get; }
        public int Moves { get; }
        public int Walks => Moves-Pushes;
        public int BoxSwitches { get; }
        public int GoalsLeft { get; }
        public IReadOnlyList<PushObservation> Events { get; }
        private ReferenceSolutionMetrics(string fingerprint,int moves,List<PushObservation> events)
        {
            LevelFingerprint=fingerprint; Moves=moves; Pushes=events.Count; Events=events.AsReadOnly();
            foreach(var observation in events)
            {
                if(observation.SwitchedBox)BoxSwitches++;
                if(observation.LeftGoal)GoalsLeft++;
            }
        }
        public static ReferenceSolutionMetrics Calculate(LevelData root,WitnessData witness,CancellationToken token=default,Action checkpoint=null)
        {
            token.ThrowIfCancellationRequested();checkpoint?.Invoke();
            var verified=WitnessVerifier.Verify(root,witness,cancellationToken:token);
            token.ThrowIfCancellationRequested();checkpoint?.Invoke();
            if(!verified.IsValid)throw new ArgumentException("指标需要当前有效参考解："+verified.Reason,nameof(witness));
            var state=new BoardState(root);
            var events=new List<PushObservation>();string previousBox=null;
            for(int index=0;index<witness.moves.Length;index++)
            {
                token.ThrowIfCancellationRequested();checkpoint?.Invoke();
                SokobanRules.TryParseDirection(witness.moves[index],out var direction);
                var moved=SokobanRules.TryMove(state,direction);
                if(!moved.Succeeded)throw new InvalidOperationException("已验证路线在统计时出现非法移动。");
                if(moved.Pushed)
                {
                    int fromIndex=Reachability.Neighbor(state.Player.ToIndex(state.Width),direction,state.Width,state.Height);
                    var from=Coordinate.FromIndex(fromIndex,state.Width);
                    var to=Coordinate.FromIndex(Reachability.Neighbor(fromIndex,direction,state.Width,state.Height),state.Width);
                    events.Add(new PushObservation(events.Count+1,index+1,moved.BoxId,from,to,previousBox!=null&&previousBox!=moved.BoxId,state.IsGoal(from)&&!state.IsGoal(to)));
                    previousBox=moved.BoxId;
                }
                state=moved.State;
            }
            checkpoint?.Invoke();
            if(!state.IsWon||events.Count!=verified.Pushes)throw new InvalidOperationException("指标统计与证据重放不一致。");
            return new ReferenceSolutionMetrics(Sokoban.Core.Identity.LevelFingerprint.Compute(root),verified.Moves,events);
        }
    }
}
