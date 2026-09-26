using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using Sokoban.Core.Data;
using Sokoban.Core.Identity;
using Sokoban.Core.Rules;
using Sokoban.Domain.Analysis;
namespace Sokoban.Tests.EditMode.Analysis
{
    public sealed class SolverOracleTests
    {
        static readonly AnalysisBudget Limit = new AnalysisBudget(10000, 200000, 128 * 1024 * 1024);
        internal static readonly string[] GoalBox = { "########", "##    ##", "##    ##", "#@* $.##", "##    ##", "########" };
        [Test] public void SimpleRouteAndInitialWinAreReplayed()
        {
            Check(AsciiLevelFactory.Create("#####", "#@$.#", "#   #", "#####"), 1);
            Check(AsciiLevelFactory.Create("#####", "#@* #", "#   #", "#####"), 0);
        }
        [Test] public void GoalBoxCanMoveAndSymmetriesPreserveMinimum()
        {
            var map = GoalBox;
            for (int rotation = 0; rotation < 4; rotation++)
            {
                Check(AsciiLevelFactory.Create(map), 5);
                Check(AsciiLevelFactory.Create(map.Select(row => new string(row.Reverse().ToArray())).ToArray()), 5);
                map = Enumerable.Range(0, map[0].Length).Select(x => new string(map.Reverse().Select(row => row[x]).ToArray())).ToArray();
            }
        }
        [Test] public void AllSingleBoxPlacementsMatchIndependentWalkStateOracle()
        {
            for (int player = 0; player < 9; player++)
            for (int box = 0; box < 9; box++)
            for (int goal = 0; goal < 9; goal++)
            {
                if (player == box) continue;
                var rows = new[] { "#####".ToCharArray(), "#   #".ToCharArray(), "#   #".ToCharArray(), "#   #".ToCharArray(), "#####".ToCharArray() };
                rows[1 + goal / 3][1 + goal % 3] = '.';
                rows[1 + player / 3][1 + player % 3] = player == goal ? '+' : '@';
                rows[1 + box / 3][1 + box % 3] = box == goal ? '*' : '$';
                var level = AsciiLevelFactory.Create(rows.Select(row => new string(row)).ToArray());
                int expected = WalkOracle(level); Check(level, expected);
                var reverse = new ReversePushDistances(level); int cell = (3 - box / 3) * 5 + 1 + box % 3;
                if (reverse.IsDead(cell)) Assert.That(expected, Is.EqualTo(-1));
                int lower = reverse.MatchingLowerBound(new[] { cell });
                if (expected >= 0) Assert.That(lower, Is.LessThanOrEqualTo(expected));
            }
        }
        [Test] public void PlayerRegionAndHashCollisionsNeverCollapseDifferentStates()
        {
            var a = new SearchStateKey(new[] { 1 }, 32); var b = new SearchStateKey(new[] { 2 }, 1);
            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()), "fixture must collide in the actual hash");
            Assert.That(a.Equals(b), Is.False);
            Assert.That(new HashSet<SearchStateKey> { a, b }.Count, Is.EqualTo(2));
            Assert.That(new SearchStateKey(new[] { 3, 1 }, 4), Is.EqualTo(new SearchStateKey(new[] { 1, 3 }, 4)));
            Assert.That(new SearchStateKey(new[] { 1, 3 }, 4), Is.Not.EqualTo(new SearchStateKey(new[] { 1, 3 }, 8)));
            int[] owned = { 1, 3 }; var isolated = new SearchStateKey(owned, 4); owned[0] = 7;
            Assert.That(isolated, Is.EqualTo(new SearchStateKey(new[] { 1, 3 }, 4)));
        }
        [Test] public void InvalidBudgetAndCancellationDoNotProveUnsolvable()
        {
            var root = AsciiLevelFactory.Create(GoalBox);
            Assert.That(new PushBfsSolver().Solve(root, new AnalysisBudget(0,0,0)).Outcome, Is.EqualTo(AnalysisOutcome.Unknown));
            var cancelled = new PushBfsSolver().Solve(root, Limit, new CancellationToken(true));
            Assert.That(cancelled.StopReason, Is.EqualTo(AnalysisStopReason.Cancelled));
            Assert.That(cancelled.Outcome, Is.EqualTo(AnalysisOutcome.Unknown));
            root.entities.RemoveAll(e => e.type == EntityType.Player);
            Assert.That(new PushBfsSolver().Solve(root, Limit).Outcome, Is.EqualTo(AnalysisOutcome.Invalid));
        }
        [Test] public void FrozenBoxesExhaustAndDifferentPlayerRegionsRemainDistinct()
        {
            Check(AsciiLevelFactory.Create("#####", "#$@.#", "#####", "#####"), -1);
            foreach (var rows in new[] {
                new[] { "#######", "# .   #", "###$###", "# @   #", "#######" },
                new[] { "#######", "# .@  #", "###$###", "#     #", "#######" },
                new[] { "#######", "# . . #", "# $$@ #", "#     #", "#######" } })
            {
                var root = AsciiLevelFactory.Create(rows); Check(root, WalkOracle(root));
            }
        }
        [Test] public void RandomTwoBoxMicroMapsMatchWalkOracle()
        {
            var random = new Random(314159);
            for (int trial = 0; trial < 80; trial++)
            {
                var order = Enumerable.Range(0, 12).OrderBy(_ => random.Next()).ToArray();
                var rows = new[] { "######".ToCharArray(), "#    #".ToCharArray(), "#    #".ToCharArray(), "#    #".ToCharArray(), "######".ToCharArray() };
                var marks = new[] { '@', '$', '$', '.', '.', '#' };
                for (int i=0;i<marks.Length;i++) rows[1+order[i]/4][1+order[i]%4]=marks[i];
                var root=AsciiLevelFactory.Create(rows.Select(row=>new string(row)).ToArray());
                int expected=WalkOracle(root);Check(root,expected);
                var bound=new ReversePushDistances(root).MatchingLowerBound(Reachability.Boxes(new BoardState(root)));
                if(expected>=0)Assert.That(bound,Is.LessThanOrEqualTo(expected));
            }
        }
        [Test] public void WalkingReconstructionUsesActualPlayerAndRejectsBadParents()
        {
            var root=AsciiLevelFactory.Create("#######","#@    #","#  $. #","#     #","#######");
            var witness=SolutionReconstructor.Reconstruct(root,new[]{new PushStep(3+2*7,Direction.Right)});
            var replay=WitnessVerifier.Verify(root,witness);
            Assert.That(replay.IsValid,Is.True);Assert.That(replay.Pushes,Is.EqualTo(1));Assert.That(replay.Moves,Is.GreaterThan(1));
            Assert.Throws<InvalidOperationException>(()=>SolutionReconstructor.Reconstruct(root,new[]{new PushStep(1,Direction.Up)}));
            Assert.Throws<OperationCanceledException>(()=>SolutionReconstructor.Reconstruct(root,new[]{new PushStep(17,Direction.Right)},new CancellationToken(true)));
        }
        [Test] public void EachBudgetHasItsOwnUnknownStopReason()
        {
            var root=AsciiLevelFactory.Create(GoalBox);
            foreach(var pair in new[]{
                (new AnalysisBudget(0,100000,128*1024*1024),AnalysisStopReason.TimeBudget),
                (new AnalysisBudget(10000,0,128*1024*1024),AnalysisStopReason.NodeBudget),
                (new AnalysisBudget(10000,100000,0),AnalysisStopReason.MemoryBudget)})
            {
                var result=new PushBfsSolver().Solve(root,pair.Item1);
                Assert.That(result.Outcome,Is.EqualTo(AnalysisOutcome.Unknown));Assert.That(result.StopReason,Is.EqualTo(pair.Item2));
                Assert.That(result.Witness,Is.Null);Assert.That(result.Optimality,Is.EqualTo(AnalysisOptimality.NotApplicable));
            }
        }
        [Test] public void FloodFillDistinguishesPlayerSidesOfABlockingBox()
        {
            var level=AsciiLevelFactory.Create("#######","# .   #","###$###","# @   #","#######");
            int box=3+2*7;
            int low=Reachability.Representative(Reachability.Find(level,new[]{box},2+7));
            int high=Reachability.Representative(Reachability.Find(level,new[]{box},2+3*7));
            Assert.That(low,Is.Not.EqualTo(high));
            Assert.That(new SearchStateKey(new[]{box},low),Is.Not.EqualTo(new SearchStateKey(new[]{box},high)));
        }
        static void Check(LevelData root, int expected)
        {
            var result = new PushBfsSolver().Solve(root, Limit);
            Assert.That(result.Outcome, Is.EqualTo(expected < 0 ? AnalysisOutcome.Unsolvable : AnalysisOutcome.Solvable), result.Explanation);
            if (expected < 0) { Assert.That(result.StopReason, Is.EqualTo(AnalysisStopReason.Exhausted)); return; }
            Assert.That(result.Optimality, Is.EqualTo(AnalysisOptimality.PushOptimal));
            Assert.That(result.LevelFingerprint, Is.EqualTo(LevelFingerprint.Compute(root)));
            var replay = WitnessVerifier.Verify(root, result.Witness);
            Assert.That(replay.IsValid, Is.True, replay.Reason);
            Assert.That(replay.Pushes, Is.EqualTo(expected));
        }
        // Independent Dijkstra over every legal player step; no push-region merging or heuristic.
        internal static int WalkOracle(LevelData root)
        {
            var initial = new BoardState(root); var costs = new Dictionary<string,int> { [Key(initial)] = 0 };
            var todo = new List<(BoardState state,int cost)> { (initial, 0) };
            while (todo.Count > 0)
            {
                int best = 0; for (int i=1;i<todo.Count;i++) if(todo[i].cost<todo[best].cost) best=i;
                var current=todo[best];todo.RemoveAt(best);
                if(costs[Key(current.state)] != current.cost) continue;
                if(current.state.IsWon) return current.cost;
                Assert.That(costs.Count, Is.LessThan(100000), "micro oracle unexpectedly too large");
                foreach(Direction direction in Enum.GetValues(typeof(Direction)))
                {
                    var next=SokobanRules.TryMove(current.state,direction);if(!next.Succeeded)continue;
                    int cost=current.cost+(next.Pushed?1:0);string key=Key(next.State);
                    if(costs.TryGetValue(key,out int prior)&&prior<=cost)continue;
                    costs[key]=cost;todo.Add((next.State,cost));
                }
            }
            return -1;
        }
        static string Key(BoardState state) => state.Player.ToIndex(state.Width)+":"+string.Join(",",Enumerable.Range(0,state.BoxCount).Select(i=>state.GetBoxPosition(i).ToIndex(state.Width)).OrderBy(i=>i));
    }
}
