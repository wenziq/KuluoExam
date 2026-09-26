using System;
using System.Threading;
using System.Threading.Tasks;
using Sokoban.Core.Data;
using Sokoban.Core.Identity;
using Sokoban.Core.Validation;
using Sokoban.Domain.Analysis;
namespace Sokoban.Runtime.Platform.Tasks
{
    public sealed class AnalysisCoordinator : IDisposable
    {
        readonly MainThreadDispatcher dispatcher;
        readonly AnalysisCache cache;
        readonly IAnalysisSolver solver;
        readonly object gate = new object();
        CancellationTokenSource cancellation;
        long generation;
        bool disposed;
        LevelData currentRoot;
        AnalysisResult lastResult;
        public AnalysisCoordinator(MainThreadDispatcher dispatcher, AnalysisCache cache = null, IAnalysisSolver solver = null)
        { this.dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher)); this.cache = cache ?? new AnalysisCache(); this.solver = solver ?? new PushAStarSolver(); }
        public AnalysisJobState State { get; private set; }
        public AnalysisRequestIdentity CurrentIdentity { get; private set; }
        public AnalysisResult LastResult => lastResult?.Copy();
        public AnalysisProgress Progress { get; private set; }
        public AnalysisResult Evidence => currentRoot == null ? null : cache.Evidence(currentRoot, PushAStarSolver.AlgorithmVersion);
        public Task LastTask { get; private set; } = Task.CompletedTask;
        public event Action Changed;
        public void Start(LevelData level, AnalysisRequestIdentity identity, AnalysisBudget budget = null, IAnalysisSolver requestSolver = null)
        {
            dispatcher.AssertOwner(); if (disposed) throw new ObjectDisposedException(nameof(AnalysisCoordinator));
            if (level == null || identity == null || string.IsNullOrEmpty(identity.RequestId) || string.IsNullOrEmpty(identity.DocumentId) || string.IsNullOrEmpty(identity.ContentKey)) throw new ArgumentException("分析请求身份不完整。");
            var snapshot = level.DeepCopy();
            if (StructureValidator.Validate(snapshot).IsValid && identity.ContentKey != LevelFingerprint.Compute(snapshot)) throw new ArgumentException("请求身份与分析快照不一致。");
            Invalidate(); var source = new CancellationTokenSource(); long request;
            lock (gate) { request = ++generation; cancellation = source; CurrentIdentity = identity; }
            currentRoot = snapshot; lastResult = null; Progress = null; State = AnalysisJobState.Queued;
            LastTask = RunAsync(snapshot, identity, request, source, budget ?? AnalysisBudget.Normal, requestSolver ?? solver);
            Changed?.Invoke();
        }
        Task RunAsync(LevelData snapshot, AnalysisRequestIdentity identity, long request, CancellationTokenSource source, AnalysisBudget budget, IAnalysisSolver requestSolver)
        {
            return Task.Run(async () =>
            {
                try
                {
                    await AnalysisWorker.RunAsync(token =>
                    {
                    Post(request, identity, () => { State = AnalysisJobState.Running; Changed?.Invoke(); });
                    var result = requestSolver.Solve(snapshot, budget, source.Token, progress =>
                    {
                        var copy = new AnalysisProgress { ExpandedNodes = progress.ExpandedNodes, ElapsedMilliseconds = progress.ElapsedMilliseconds, EstimatedBytes = progress.EstimatedBytes };
                        Post(request, identity, () => { Progress = copy; State = AnalysisJobState.Running; Changed?.Invoke(); });
                    });
                    Post(request, identity, () => Complete(snapshot, result));
                    return true;
                    }, source.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                { Post(request, identity, () => Complete(snapshot, Unknown(identity.ContentKey, AnalysisStopReason.Cancelled, "分析已取消。"))); }
                catch (Exception error)
                { Post(request, identity, () => Complete(snapshot, Unknown(identity.ContentKey, AnalysisStopReason.Error, error.Message))); }
                finally
                {
                    lock (gate) { if (ReferenceEquals(cancellation, source)) cancellation = null; }
                    source.Dispose();
                }
            });
        }
        void Post(long request, AnalysisRequestIdentity identity, Action action)
        {
            lock (gate)
            {
                if (disposed || request != generation || !identity.Matches(CurrentIdentity)) return;
                dispatcher.PostLatest(this, () =>
                {
                    lock (gate) if (disposed || request != generation || !identity.Matches(CurrentIdentity)) return;
                    action();
                });
            }
        }
        void Complete(LevelData snapshot, AnalysisResult result)
        {
            try
            {
                if (result == null) throw new InvalidOperationException("分析没有返回结果。");
                cache.Record(snapshot, result, PushAStarSolver.AlgorithmVersion); lastResult = result.Copy();
            }
            catch (Exception error) { lastResult = Unknown(CurrentIdentity.ContentKey, AnalysisStopReason.Error, "分析结果未通过检查：" + error.Message); }
            State = lastResult.StopReason == AnalysisStopReason.Cancelled ? AnalysisJobState.Cancelled : lastResult.StopReason == AnalysisStopReason.Error ? AnalysisJobState.Failed : AnalysisJobState.Completed;
            Changed?.Invoke();
        }
        static AnalysisResult Unknown(string fingerprint, AnalysisStopReason reason, string explanation) => new AnalysisResult { Outcome = AnalysisOutcome.Unknown, LevelFingerprint = fingerprint, StopReason = reason, Explanation = explanation };
        void Invalidate()
        {
            lock (gate)
            {
                generation++;
                if (cancellation != null) { cancellation.Cancel(); cancellation = null; }
                dispatcher.Remove(this);
            }
        }
        public void Cancel()
        {
            dispatcher.AssertOwner(); if (disposed) return; Invalidate();
            if (CurrentIdentity == null) return;
            lastResult = Unknown(CurrentIdentity.ContentKey, AnalysisStopReason.Cancelled, "分析已取消；已有有效参考解仍保留。");
            cache.Record(currentRoot, lastResult, PushAStarSolver.AlgorithmVersion); State = AnalysisJobState.Cancelled; Changed?.Invoke();
        }
        public void Detach()
        {
            dispatcher.AssertOwner(); Invalidate(); CurrentIdentity = null; currentRoot = null; lastResult = null; Progress = null; State = AnalysisJobState.Idle;
        }
        public void Dispose()
        {
            dispatcher.AssertOwner(); if (disposed) return; Detach(); lock (gate) disposed = true; Changed = null;
        }
    }
}
