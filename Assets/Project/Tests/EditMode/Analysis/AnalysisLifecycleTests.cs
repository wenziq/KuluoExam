using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Sokoban.Core.Data;
using Sokoban.Core.Identity;
using Sokoban.Domain.Analysis;
using Sokoban.Runtime.Platform.Tasks;
namespace Sokoban.Tests.EditMode.Analysis
{
    public sealed class AnalysisLifecycleTests
    {
        static LevelData Root()=>AsciiLevelFactory.Create("#####","#@$.#","#   #","#####");
        static AnalysisRequestIdentity Identity(LevelData root,string request="one",string document="doc",string purpose="solve",long lifecycle=1)=>new AnalysisRequestIdentity(request,document,LevelFingerprint.Compute(root),purpose,"initial",lifecycle);
        [Test] public void CancelledJobKeepsVerifiedEvidenceAndCacheRebindsCopiedIdentity()
        {
            var root=Root();var cache=new AnalysisCache();var solved=new PushBfsSolver().Solve(root);cache.Record(root,solved,PushAStarSolver.AlgorithmVersion);
            cache.Record(root,new AnalysisResult{Outcome=AnalysisOutcome.Unknown,StopReason=AnalysisStopReason.Cancelled,LevelFingerprint=LevelFingerprint.Compute(root)},PushAStarSolver.AlgorithmVersion);
            Assert.That(cache.Evidence(root,PushAStarSolver.AlgorithmVersion).Outcome,Is.EqualTo(AnalysisOutcome.Solvable));
            Assert.That(cache.Latest(root,PushAStarSolver.AlgorithmVersion).StopReason,Is.EqualTo(AnalysisStopReason.Cancelled));
            var copy=root.DeepCopy();copy.levelId="copy";copy.name="新名称";var evidence=cache.Evidence(copy,PushAStarSolver.AlgorithmVersion);
            Assert.That(evidence.Witness.levelId,Is.EqualTo("copy"));Assert.That(Sokoban.Core.Rules.WitnessVerifier.Verify(copy,evidence.Witness).IsValid,Is.True);
            evidence.Witness.moves="bad";Assert.That(cache.Evidence(copy,PushAStarSolver.AlgorithmVersion).Witness.moves,Is.EqualTo("R"));
            Assert.That(cache.Evidence(copy,"new-algorithm").Optimality,Is.EqualTo(AnalysisOptimality.NotProven));Assert.That(cache.Latest(copy,"new-algorithm"),Is.Null);
            copy.terrain[2*5+2]=1;Assert.That(cache.Evidence(copy,PushAStarSolver.AlgorithmVersion),Is.Null);
        }
        [Test] public void CacheHasCountAndByteLimitsAndRejectsInvalidEvidence()
        {
            var cache=new AnalysisCache(1,1000000);var root=Root();var first=new PushBfsSolver().Solve(root);cache.Record(root,first,PushAStarSolver.AlgorithmVersion);
            var other=AsciiLevelFactory.Create("######","#@ $.#","#    #","######");cache.Record(other,new PushBfsSolver().Solve(other),PushAStarSolver.AlgorithmVersion);
            Assert.That(cache.Count,Is.EqualTo(1));Assert.That(cache.Evidence(root,PushAStarSolver.AlgorithmVersion),Is.Null);
            first.Witness.moves="L";Assert.Throws<ArgumentException>(()=>cache.Record(root,first,PushAStarSolver.AlgorithmVersion));
            var tiny=new AnalysisCache(64,1);tiny.Record(other,new PushBfsSolver().Solve(other),PushAStarSolver.AlgorithmVersion);Assert.That(tiny.Count,Is.Zero);
        }
        [Test] public void OldFailureCannotReplaceNewRequestAndOnlyOneWorkerRuns()
        {
            var dispatcher=new MainThreadDispatcher();var solver=new GateSolver();var root=Root();
            using(var coordinator=new AnalysisCoordinator(dispatcher,solver:solver))
            {
                coordinator.Start(root,Identity(root));Assert.That(solver.Entered.Wait(3000),Is.True);
                coordinator.Start(root,Identity(root,"two","other","playability",2));solver.Release.Set();
                Assert.That(coordinator.LastTask.Wait(5000),Is.True);dispatcher.Drain();
                Assert.That(solver.Peak,Is.EqualTo(1));Assert.That(coordinator.CurrentIdentity.RequestId,Is.EqualTo("two"));
                Assert.That(coordinator.State,Is.EqualTo(AnalysisJobState.Completed));Assert.That(coordinator.LastResult.Outcome,Is.EqualTo(AnalysisOutcome.Solvable));
                Assert.That(coordinator.LastResult.Explanation,Is.Not.EqualTo("old failure"));
            }
            solver.Dispose();
        }
        [Test] public void DetachedOrDisposedTaskCannotPublishAndCancelKeepsEvidence()
        {
            var root=Root();var dispatcher=new MainThreadDispatcher();var cache=new AnalysisCache();cache.Record(root,new PushBfsSolver().Solve(root),PushAStarSolver.AlgorithmVersion);
            var solver=new GateSolver();var coordinator=new AnalysisCoordinator(dispatcher,cache,solver);
            coordinator.Start(root,Identity(root));Assert.That(solver.Entered.Wait(3000),Is.True);coordinator.Cancel();
            Assert.That(coordinator.Evidence.Witness.moves,Is.EqualTo("R"));Assert.That(coordinator.State,Is.EqualTo(AnalysisJobState.Cancelled));
            coordinator.Detach();coordinator.Dispose();solver.Release.Set();Assert.That(coordinator.LastTask.Wait(5000),Is.True);dispatcher.Drain();
            Assert.That(dispatcher.PendingCount,Is.Zero);Assert.That(coordinator.CurrentIdentity,Is.Null);solver.Dispose();
        }
        [Test] public void DispatcherCoalescesProgressAndRunsOnlyOnOwningThread()
        {
            var dispatcher=new MainThreadDispatcher();object owner=new object();int value=0;
            Task.Run(()=>{for(int i=0;i<10000;i++){int captured=i;dispatcher.PostLatest(owner,()=>value=captured);}}).Wait();
            Assert.That(dispatcher.PendingCount,Is.EqualTo(1));Assert.That(value,Is.Zero);dispatcher.Drain();Assert.That(value,Is.EqualTo(9999));
            Assert.Throws<AggregateException>(()=>Task.Run(()=>dispatcher.Drain()).Wait());
        }
        [Test] public void ZeroBudgetCompletionKeepsUnknownAndCorrectContentIdentity()
        {
            var root=Root();var dispatcher=new MainThreadDispatcher();
            using(var coordinator=new AnalysisCoordinator(dispatcher))
            {
                coordinator.Start(root,Identity(root),new AnalysisBudget(0,0,0));Assert.That(coordinator.LastTask.Wait(3000),Is.True);dispatcher.Drain();
                Assert.That(coordinator.State,Is.EqualTo(AnalysisJobState.Completed));
                Assert.That(coordinator.LastResult.StopReason,Is.EqualTo(AnalysisStopReason.TimeBudget));
                Assert.That(coordinator.LastResult.LevelFingerprint,Is.EqualTo(LevelFingerprint.Compute(root)));
                Assert.That(coordinator.LastResult.Outcome,Is.EqualTo(AnalysisOutcome.Unknown));
            }
        }
        [Test] public void CurrentWorkerFailureKeepsVerifiedEvidenceAndExplicitFailedState()
        {
            var root=Root();var cache=new AnalysisCache();cache.Record(root,new PushBfsSolver().Solve(root),PushAStarSolver.AlgorithmVersion);
            var dispatcher=new MainThreadDispatcher();var solver=new GateSolver();
            using(var coordinator=new AnalysisCoordinator(dispatcher,cache,solver))
            {
                coordinator.Start(root,Identity(root));Assert.That(solver.Entered.Wait(3000),Is.True);solver.Release.Set();
                Assert.That(coordinator.LastTask.Wait(5000),Is.True);dispatcher.Drain();
                Assert.That(coordinator.State,Is.EqualTo(AnalysisJobState.Failed));Assert.That(coordinator.LastResult.Outcome,Is.EqualTo(AnalysisOutcome.Unknown));
                Assert.That(coordinator.Evidence.Witness.moves,Is.EqualTo("R"));
            }
            solver.Dispose();
        }
        [Test] public void SeparateCoordinatorsShareOneWorkerAndOwnImmutableSnapshots()
        {
            var root=Root();string fingerprint=LevelFingerprint.Compute(root);var dispatcher=new MainThreadDispatcher();var solver=new GateSolver(false);
            using(var first=new AnalysisCoordinator(dispatcher,solver:solver))
            using(var second=new AnalysisCoordinator(dispatcher,solver:solver))
            {
                first.Start(root,Identity(root));Assert.That(solver.Entered.Wait(3000),Is.True);
                second.Start(root,Identity(root,"second"));root.terrain[2*5+2]=1;root.levelId="changed-by-caller";solver.Release.Set();
                Assert.That(Task.WaitAll(new[]{first.LastTask,second.LastTask},5000),Is.True);dispatcher.Drain();
                Assert.That(solver.Peak,Is.EqualTo(1));Assert.That(first.LastResult.Outcome,Is.EqualTo(AnalysisOutcome.Solvable));Assert.That(second.LastResult.Outcome,Is.EqualTo(AnalysisOutcome.Solvable));
                Assert.That(first.LastResult.LevelFingerprint,Is.EqualTo(fingerprint));Assert.That(first.LastResult.Witness.levelId,Is.Not.EqualTo(root.levelId));
            }
            solver.Dispose();
        }
        sealed class GateSolver:IAnalysisSolver,IDisposable
        {
            readonly bool failFirst;public GateSolver(bool failFirst=true){this.failFirst=failFirst;}
            public readonly ManualResetEventSlim Entered=new ManualResetEventSlim(),Release=new ManualResetEventSlim();int calls,active;public int Peak;
            public AnalysisResult Solve(LevelData root,AnalysisBudget budget,CancellationToken token,Action<AnalysisProgress> progress)
            {
                int concurrent=Interlocked.Increment(ref active);Peak=Math.Max(Peak,concurrent);
                try{if(Interlocked.Increment(ref calls)==1){Entered.Set();Release.Wait(5000);if(failFirst)throw new InvalidOperationException("old failure");}return new PushAStarSolver().Solve(root,budget,token,progress);}
                finally{Interlocked.Decrement(ref active);}
            }
            public void Dispose(){Entered.Dispose();Release.Dispose();}
        }
    }
}
