using System;
using System.Diagnostics;
using System.Threading;
namespace Sokoban.Domain.Analysis
{
    public sealed class SearchBudget
    {
        readonly AnalysisBudget limits;
        readonly CancellationToken cancellation;
        readonly Func<long> elapsed;
        public long ExpandedNodes { get; private set; }
        public long EstimatedBytes { get; private set; }
        public long PeakBytes { get; private set; }
        public long ElapsedMilliseconds => Math.Max(0, elapsed());
        public AnalysisStopReason StopReason { get; private set; }
        public SearchBudget(AnalysisBudget budget, CancellationToken cancellationToken = default, Func<long> elapsed = null)
        {
            limits = budget ?? throw new ArgumentNullException(nameof(budget)); cancellation = cancellationToken;
            if (elapsed != null) this.elapsed = elapsed;
            else { var clock = Stopwatch.StartNew(); this.elapsed = () => clock.ElapsedMilliseconds; }
        }
        public bool Check()
        {
            if (StopReason != AnalysisStopReason.None) return false;
            if (cancellation.IsCancellationRequested) StopReason = AnalysisStopReason.Cancelled;
            else if (ElapsedMilliseconds >= limits.MaxElapsedMilliseconds) StopReason = AnalysisStopReason.TimeBudget;
            return StopReason == AnalysisStopReason.None;
        }
        public bool TryReserve(long bytes)
        {
            if (bytes < 0) throw new ArgumentOutOfRangeException(nameof(bytes));
            if (!Check()) return false;
            if (bytes > limits.MaxEstimatedBytes - EstimatedBytes) { StopReason = AnalysisStopReason.MemoryBudget; return false; }
            EstimatedBytes += bytes; PeakBytes = Math.Max(PeakBytes, EstimatedBytes); return true;
        }
        public bool TryExpand()
        {
            if (!Check()) return false;
            if (ExpandedNodes >= limits.MaxExpandedNodes) { StopReason = AnalysisStopReason.NodeBudget; return false; }
            ExpandedNodes++; return true;
        }
        public void ThrowIfStopped() { if (!Check()) throw new SearchStoppedException(); }
    }
    internal sealed class SearchStoppedException : Exception { }
}
