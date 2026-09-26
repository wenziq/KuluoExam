using System;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using Sokoban.Core.Data;
using Sokoban.Core.Rules;
using Sokoban.Domain.Analysis;
namespace Sokoban.Tests.EditMode.Analysis
{
    public sealed class AStarParityTests
    {
        static readonly AnalysisBudget Limit=new AnalysisBudget(10000,200000,128*1024*1024);
        [Test] public void SingleBoxPlacementsAndTwoBoxMapsMatchUnprunedBfs()
        {
            for(int player=0;player<9;player++)for(int box=0;box<9;box++)for(int goal=0;goal<9;goal++)
            {
                if(player==box)continue;
                var rows=new[]{"#####".ToCharArray(),"#   #".ToCharArray(),"#   #".ToCharArray(),"#   #".ToCharArray(),"#####".ToCharArray()};
                rows[1+goal/3][1+goal%3]='.';rows[1+player/3][1+player%3]=player==goal?'+':'@';rows[1+box/3][1+box%3]=box==goal?'*':'$';
                Compare(AsciiLevelFactory.Create(rows.Select(row=>new string(row)).ToArray()));
            }
            var random=new Random(161803);
            for(int trial=0;trial<120;trial++)
            {
                var cells=Enumerable.Range(0,12).OrderBy(_=>random.Next()).ToArray();
                var rows=new[]{"######".ToCharArray(),"#    #".ToCharArray(),"#    #".ToCharArray(),"#    #".ToCharArray(),"######".ToCharArray()};
                var marks=new[]{'@','$','$','.','.','#'};for(int i=0;i<marks.Length;i++)rows[1+cells[i]/4][1+cells[i]%4]=marks[i];
                Compare(AsciiLevelFactory.Create(rows.Select(row=>new string(row)).ToArray()));
            }
        }
        [Test] public void GoalBoxMovementSymmetriesAndInitialWinRemainCorrect()
        {
            var map=SolverOracleTests.GoalBox;
            for(int i=0;i<4;i++)
            {
                Compare(AsciiLevelFactory.Create(map));Compare(AsciiLevelFactory.Create(map.Select(row=>new string(row.Reverse().ToArray())).ToArray()));
                map=Enumerable.Range(0,map[0].Length).Select(x=>new string(map.Reverse().Select(row=>row[x]).ToArray())).ToArray();
            }
            Compare(AsciiLevelFactory.Create("#####","#@* #","#   #","#####"));
        }
        [Test] public void BudgetAndCancellationNeverBecomeUnsolvable()
        {
            var root=AsciiLevelFactory.Create(SolverOracleTests.GoalBox);
            foreach(var budget in new[]{new AnalysisBudget(0,100000,128*1024*1024),new AnalysisBudget(10000,0,128*1024*1024),new AnalysisBudget(10000,100000,0)})
            {
                var result=new PushAStarSolver().Solve(root,budget);Assert.That(result.Outcome,Is.EqualTo(AnalysisOutcome.Unknown));Assert.That(result.Witness,Is.Null);
            }
            Assert.That(new PushAStarSolver().Solve(root,Limit,new CancellationToken(true)).StopReason,Is.EqualTo(AnalysisStopReason.Cancelled));
            root.entities.RemoveAll(e=>e.type==EntityType.Player);
            Assert.That(new PushAStarSolver().Solve(root,Limit).Outcome,Is.EqualTo(AnalysisOutcome.Invalid));
        }
        [Test] public void EachUnsolvableProofHasReliableConditions()
        {
            var staticRoot=AsciiLevelFactory.Create("#######","# $   #","# .@  #","#     #","#######");
            Assert.That(new PushAStarSolver().Solve(staticRoot,Limit).StopReason,Is.EqualTo(AnalysisStopReason.StaticDeadlock));
            var matching=AsciiLevelFactory.Create("##########","# .  # . #","# $$ #   #","# @  #   #","##########");
            Assert.That(new PushAStarSolver().Solve(matching,Limit).StopReason,Is.EqualTo(AnalysisStopReason.MatchingImpossible));
            var frozen=AsciiLevelFactory.Create("########","# .  . #","# $$   #","# $$@  #","# .  . #","########");
            var result=new PushAStarSolver().Solve(frozen,Limit);
            Assert.That(result.Outcome,Is.EqualTo(AnalysisOutcome.Unsolvable));Assert.That(result.StopReason,Is.EqualTo(AnalysisStopReason.Exhausted));
            Assert.That(new PushBfsSolver().Solve(frozen,Limit).Outcome,Is.EqualTo(result.Outcome));
        }
        [Test] public void NonzeroSearchBudgetsStopDuringActualWork()
        {
            var root=AsciiLevelFactory.Create("########","# . . .#","#      #","# $$$  #","#  @   #","#      #","########");
            var nodes=new PushAStarSolver().Solve(root,new AnalysisBudget(10000,1,128*1024*1024));
            Assert.That(nodes.StopReason,Is.EqualTo(AnalysisStopReason.NodeBudget));Assert.That(nodes.ExpandedNodes,Is.EqualTo(1));
            long initial=2*1024*1024+root.width*root.height*16*4+384+3*8;
            var memory=new PushAStarSolver().Solve(root,new AnalysisBudget(10000,100000,initial));
            Assert.That(memory.StopReason,Is.EqualTo(AnalysisStopReason.MemoryBudget));Assert.That(memory.EstimatedPeakBytes,Is.LessThanOrEqualTo(initial));
            var time=new PushAStarSolver().Solve(root,new AnalysisBudget(10,100000,128*1024*1024),progress:_=>Thread.Sleep(15));
            Assert.That(time.StopReason,Is.EqualTo(AnalysisStopReason.TimeBudget));Assert.That(time.Outcome,Is.EqualTo(AnalysisOutcome.Unknown));
        }
        static void Compare(LevelData root)
        {
            var bfs=new PushBfsSolver().Solve(root,Limit);var actual=new PushAStarSolver().Solve(root,Limit);
            Assert.That(actual.Outcome,Is.EqualTo(bfs.Outcome),actual.Explanation);
            if(actual.Outcome!=AnalysisOutcome.Solvable)return;
            var expected=WitnessVerifier.Verify(root,bfs.Witness);var replay=WitnessVerifier.Verify(root,actual.Witness);
            Assert.That(replay.IsValid,Is.True,replay.Reason);Assert.That(replay.Pushes,Is.EqualTo(expected.Pushes));
            Assert.That(actual.Optimality,Is.EqualTo(AnalysisOptimality.PushOptimal));
            Assert.That(actual.EstimatedPeakBytes,Is.LessThanOrEqualTo(Limit.MaxEstimatedBytes));
        }
    }
}
