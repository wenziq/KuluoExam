using System.Threading;
using NUnit.Framework;
using Sokoban.Domain.Analysis;
namespace Sokoban.Tests.EditMode.Analysis
{
    public sealed class SearchBudgetTests
    {
        [Test] public void ClockCancellationExpansionAndAllAllocationsAreIndependentlyBounded()
        {
            long clock=0;var budget=new SearchBudget(new AnalysisBudget(10,2,100),elapsed:()=>clock);
            Assert.That(budget.TryReserve(40),Is.True);Assert.That(budget.TryReserve(60),Is.True);Assert.That(budget.EstimatedBytes,Is.EqualTo(100));
            Assert.That(budget.TryReserve(1),Is.False);Assert.That(budget.StopReason,Is.EqualTo(AnalysisStopReason.MemoryBudget));Assert.That(budget.PeakBytes,Is.EqualTo(100));
            budget=new SearchBudget(new AnalysisBudget(10,2,100),elapsed:()=>clock);Assert.That(budget.TryExpand(),Is.True);Assert.That(budget.TryExpand(),Is.True);Assert.That(budget.TryExpand(),Is.False);Assert.That(budget.StopReason,Is.EqualTo(AnalysisStopReason.NodeBudget));
            budget=new SearchBudget(new AnalysisBudget(10,2,100),elapsed:()=>clock);clock=10;Assert.That(budget.Check(),Is.False);Assert.That(budget.StopReason,Is.EqualTo(AnalysisStopReason.TimeBudget));
            var source=new CancellationTokenSource();clock=0;budget=new SearchBudget(new AnalysisBudget(10,2,100),source.Token,()=>clock);source.Cancel();Assert.That(budget.Check(),Is.False);Assert.That(budget.StopReason,Is.EqualTo(AnalysisStopReason.Cancelled));source.Dispose();
        }
        [Test] public void CountersCannotOverflowAndStoppingIsSticky()
        {
            var budget=new SearchBudget(new AnalysisBudget(long.MaxValue,long.MaxValue,long.MaxValue),elapsed:()=>0);
            Assert.That(budget.TryReserve(long.MaxValue-1),Is.True);Assert.That(budget.TryReserve(100),Is.False);
            Assert.That(budget.Check(),Is.False);Assert.That(budget.EstimatedBytes,Is.EqualTo(long.MaxValue-1));
        }
    }
}
