using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Sokoban.Core.Data;
using Sokoban.Domain.Analysis;
namespace Sokoban.Tests.EditMode.Analysis
{
    public sealed class ProductionAnalysisTests
    {
        static LevelData Level()=>AsciiLevelFactory.Create("######","#@ $.#","#    #","######");
        [Test] public void RecheckRebindsChangedLayoutButFailureNeverProvesUnsolvable()
        {
            var root=Level();var old=new PushBfsSolver().Solve(root).Witness;root.terrain[root.width+1]=1;
            var result=WitnessRecheckService.Recheck(root,old);Assert.That(result.Outcome,Is.EqualTo(AnalysisOutcome.Solvable));Assert.That(result.Optimality,Is.EqualTo(AnalysisOptimality.NotProven));
            root.entities.Single(e=>e.type==EntityType.Player).x=2;
            result=WitnessRecheckService.Recheck(root,old);Assert.That(result.Outcome,Is.EqualTo(AnalysisOutcome.Unknown));
            Assert.That(result.Explanation,Does.Contain("旧路线"));Assert.That(result.Witness,Is.Null);
        }
        [Test] public async Task BatchFailureKeepsItsRootIdentityAndContinuesNextLevel()
        {
            var a=Level();var b=Level();var pack=new PackData();pack.levels.AddRange(new[]{a,b});pack.levelOrder.AddRange(new[]{a.levelId,b.levelId});
            var queue=new BatchAnalysisQueue(pack);
            await queue.RunAsync((level,token)=>level.levelId==a.levelId?Task.FromException<AnalysisResult>(new InvalidOperationException("injected failure")):Task.FromResult(new PushBfsSolver().Solve(level)),CancellationToken.None);
            Assert.That(queue.Entries[0].State,Is.EqualTo(AnalysisJobState.Failed));Assert.That(queue.Entries[0].Result.Outcome,Is.EqualTo(AnalysisOutcome.Unknown));
            Assert.That(queue.Entries[0].Result.LevelFingerprint,Is.EqualTo(Sokoban.Core.Identity.LevelFingerprint.Compute(a)));
            Assert.That(queue.Entries[1].Result.Outcome,Is.EqualTo(AnalysisOutcome.Solvable));
        }
        [Test] public async Task BatchUsesUniqueOrderSerialWorkAndPreservesFinishedRowsOnCancellation()
        {
            var a=Level();var b=Level();var c=Level();var pack=new PackData();pack.levels.AddRange(new[]{a,b,c});pack.levelOrder.AddRange(new[]{c.levelId,a.levelId,b.levelId});
            var queue=new BatchAnalysisQueue(pack);pack.levelOrder.Reverse();var cancellation=new CancellationTokenSource();int calls=0,active=0,max=0;
            await queue.RunAsync(async(level,token)=>{calls++;active++;max=Math.Max(max,active);await Task.Yield();active--;return new PushBfsSolver().Solve(level);},cancellation.Token,row=>{if(row.State==AnalysisJobState.Completed)cancellation.Cancel();});
            Assert.That(calls,Is.EqualTo(1));Assert.That(max,Is.EqualTo(1));Assert.That(queue.Entries.Select(e=>e.Level.levelId),Is.EqualTo(new[]{c.levelId,a.levelId,b.levelId}));
            Assert.That(queue.Entries[0].Result.Outcome,Is.EqualTo(AnalysisOutcome.Solvable));Assert.That(queue.Entries.Skip(1).All(e=>e.State==AnalysisJobState.Cancelled),Is.True);
        }
    }
}
