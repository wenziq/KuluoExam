using System;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using Sokoban.Core.Data;
using Sokoban.Core.Identity;
using Sokoban.Core.Rules;
using Sokoban.Domain.Analysis;
namespace Sokoban.Tests.EditMode.Analysis
{
    public sealed class ReferenceMetricsTests
    {
        [Test] public void HandCalculatedEventsUseActualBoxIdentityAndOneBasedIndices()
        {
            var root=AsciiLevelFactory.Create(SolverOracleTests.GoalBox);
            var witness=new WitnessData{levelId=root.levelId,levelFingerprint=LevelFingerprint.Compute(root),moves="RDRURULULD",source=WitnessSource.Manual};
            Assert.That(WitnessVerifier.Verify(root,witness).IsValid,Is.True,"hand path must actually win");
            var metrics=ReferenceSolutionMetrics.Calculate(root,witness);
            Assert.That(new[]{metrics.Pushes,metrics.Moves,metrics.Walks,metrics.BoxSwitches,metrics.GoalsLeft},Is.EqualTo(new[]{5,10,5,2,1}));
            Assert.That(metrics.Events.Where(e=>e.LeftGoal).Select(e=>e.PushIndex),Is.EqualTo(new[]{1}));
            Assert.That(metrics.Events.Where(e=>e.SwitchedBox).Select(e=>e.PushIndex),Is.EqualTo(new[]{3,4}));
            Assert.That(metrics.Events.Select(e=>e.MoveIndex),Is.EqualTo(new[]{1,4,5,7,10}));
            string box=root.entities.Single(e=>e.type==EntityType.Box&&e.x==2&&e.y==2).id;
            Assert.That(metrics.Events[0].BoxId,Is.EqualTo(box));root.entities.Reverse();
            var reordered=ReferenceSolutionMetrics.Calculate(root,witness);Assert.That(reordered.Events.Select(e=>e.BoxId),Is.EqualTo(metrics.Events.Select(e=>e.BoxId)));
        }
        [Test] public void InvalidStaleAndCancelledPathsNeverCreatePlausibleMetrics()
        {
            var root=AsciiLevelFactory.Create("#####","#@$.#","#   #","#####");var witness=new PushBfsSolver().Solve(root).Witness;
            var bad=witness.DeepCopy();bad.moves="L";Assert.Throws<ArgumentException>(()=>ReferenceSolutionMetrics.Calculate(root,bad));
            bad=witness.DeepCopy();bad.levelFingerprint=new string('0',64);Assert.Throws<ArgumentException>(()=>ReferenceSolutionMetrics.Calculate(root,bad));
            Assert.Throws<OperationCanceledException>(()=>ReferenceSolutionMetrics.Calculate(root,witness,new CancellationToken(true)));
            int checks=0;Assert.Throws<OperationCanceledException>(()=>ReferenceSolutionMetrics.Calculate(root,witness,checkpoint:()=>{checks++;throw new OperationCanceledException();}));Assert.That(checks,Is.GreaterThan(0));
        }
        [Test] public void EmptyWinningPathHasGenuineZeroMetrics()
        {
            var root=AsciiLevelFactory.Create("#####","#@* #","#   #","#####");var witness=new PushBfsSolver().Solve(root).Witness;
            var metrics=ReferenceSolutionMetrics.Calculate(root,witness);Assert.That(metrics.Moves,Is.Zero);Assert.That(metrics.Pushes,Is.Zero);Assert.That(metrics.Events,Is.Empty);
        }
    }
}
