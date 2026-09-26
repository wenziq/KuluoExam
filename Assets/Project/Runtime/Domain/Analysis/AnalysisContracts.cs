using Sokoban.Core.Data;
namespace Sokoban.Domain.Analysis
{
    public enum AnalysisOutcome { Solvable, Unsolvable, Unknown, Invalid }
    public enum AnalysisOptimality { PushOptimal, NotProven, NotApplicable }
    public enum AnalysisJobState { Idle, Queued, Running, Completed, Cancelled, Failed }
    public enum AnalysisStopReason { None, SolutionFound, Exhausted, StaticDeadlock, MatchingImpossible, TimeBudget, NodeBudget, MemoryBudget, Cancelled, InvalidStructure, Error }
    public sealed class AnalysisBudget
    {
        public long MaxElapsedMilliseconds { get; }
        public long MaxExpandedNodes { get; }
        public long MaxEstimatedBytes { get; }
        public AnalysisBudget(long maxElapsedMilliseconds, long maxExpandedNodes, long maxEstimatedBytes)
        {
            if(maxElapsedMilliseconds<0 || maxExpandedNodes<0 || maxEstimatedBytes<0) throw new System.ArgumentOutOfRangeException();
            MaxElapsedMilliseconds=maxElapsedMilliseconds;MaxExpandedNodes=maxExpandedNodes;MaxEstimatedBytes=maxEstimatedBytes;
        }
        public static AnalysisBudget Normal => new AnalysisBudget(2000,100000,128L*1024*1024);
        public static AnalysisBudget Deep => new AnalysisBudget(10000,1000000,256L*1024*1024);
    }
    // A completed result is distinct from scheduling state; absence of a result means never analyzed.
    public sealed class AnalysisResult
    {
        public AnalysisOutcome Outcome { get; set; } = AnalysisOutcome.Unknown;
        public AnalysisOptimality Optimality { get; set; } = AnalysisOptimality.NotApplicable;
        public AnalysisStopReason StopReason { get; set; }
        public string LevelFingerprint { get; set; }
        public WitnessData Witness { get; set; }
        public long ExpandedNodes { get; set; }
        public long ElapsedMilliseconds { get; set; }
        public long EstimatedPeakBytes { get; set; }
        public string Explanation { get; set; }
        public bool FromCache { get; set; }
        public Coordinate? ProblemCell { get; set; }
        public AnalysisResult Copy() => new AnalysisResult { Outcome=Outcome, Optimality=Optimality, StopReason=StopReason, LevelFingerprint=LevelFingerprint, Witness=Witness?.DeepCopy(), ExpandedNodes=ExpandedNodes, ElapsedMilliseconds=ElapsedMilliseconds, EstimatedPeakBytes=EstimatedPeakBytes, Explanation=Explanation, FromCache=FromCache, ProblemCell=ProblemCell };
        public bool IsCurrent(string currentFingerprint) => !string.IsNullOrEmpty(LevelFingerprint) && LevelFingerprint == currentFingerprint;
    }
    public sealed class AnalysisRequestIdentity
    {
        public string RequestId { get; }
        public string DocumentId { get; }
        public string ContentKey { get; }
        public string Purpose { get; }
        public string SessionRoot { get; }
        public long Lifecycle { get; }
        public AnalysisRequestIdentity(string requestId,string documentId,string contentKey,string purpose,string sessionRoot,long lifecycle)
        {RequestId=requestId;DocumentId=documentId;ContentKey=contentKey;Purpose=purpose;SessionRoot=sessionRoot;Lifecycle=lifecycle;}
        public bool Matches(AnalysisRequestIdentity other) => other != null && RequestId==other.RequestId && DocumentId==other.DocumentId && ContentKey==other.ContentKey && Purpose==other.Purpose && SessionRoot==other.SessionRoot && Lifecycle==other.Lifecycle;
    }
    public sealed class AnalysisProgress
    {
        public long ExpandedNodes { get; set; }
        public long ElapsedMilliseconds { get; set; }
        public long EstimatedBytes { get; set; }
    }
}
