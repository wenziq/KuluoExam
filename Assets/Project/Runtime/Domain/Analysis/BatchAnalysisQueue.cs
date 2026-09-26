using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Sokoban.Core.Data;
using Sokoban.Core.Identity;
using Sokoban.Core.Validation;
namespace Sokoban.Domain.Analysis
{
    public sealed class BatchAnalysisEntry
    {
        readonly LevelData level;
        public LevelData Level=>level.DeepCopy();
        public string LevelId=>level.levelId;
        public string Name=>level.name;
        public AnalysisResult Result {get; internal set;}
        public AnalysisJobState State {get; internal set;}=AnalysisJobState.Queued;
        internal BatchAnalysisEntry(LevelData level){this.level=level.DeepCopy();}
    }
    public sealed class BatchAnalysisQueue
    {
        readonly List<BatchAnalysisEntry> entries=new List<BatchAnalysisEntry>();bool started;
        public IReadOnlyList<BatchAnalysisEntry> Entries {get;}
        public BatchAnalysisQueue(PackData snapshot)
        {
            if(snapshot==null||!PackValidator.Validate(snapshot).IsValid)throw new ArgumentException("批量分析需要安全的关卡集快照。");
            foreach(string id in snapshot.levelOrder)entries.Add(new BatchAnalysisEntry(snapshot.levels.Single(l=>l.levelId==id)));
            Entries=entries.AsReadOnly();
        }
        public async Task RunAsync(Func<LevelData,CancellationToken,Task<AnalysisResult>> solve,CancellationToken token,Action<BatchAnalysisEntry> changed=null)
        {
            if(started)throw new InvalidOperationException("批量队列不能重复运行。");if(solve==null)throw new ArgumentNullException(nameof(solve));started=true;
            foreach(var entry in entries)
            {
                if(token.IsCancellationRequested){entry.State=AnalysisJobState.Cancelled;changed?.Invoke(entry);continue;}
                entry.State=AnalysisJobState.Running;changed?.Invoke(entry);
                try
                {
                    var result=await solve(entry.Level,token);token.ThrowIfCancellationRequested();
                    entry.Result=result?.Copy()??throw new InvalidOperationException("没有分析结果。");
                    entry.State=result.StopReason==AnalysisStopReason.Error?AnalysisJobState.Failed:result.StopReason==AnalysisStopReason.Cancelled?AnalysisJobState.Cancelled:AnalysisJobState.Completed;
                }
                catch(OperationCanceledException){entry.State=AnalysisJobState.Cancelled;}
                catch(Exception error)
                {
                    var failedRoot=entry.Level;
                    entry.State=AnalysisJobState.Failed;
                    entry.Result=new AnalysisResult
                    {
                        Outcome=AnalysisOutcome.Unknown,StopReason=AnalysisStopReason.Error,Explanation=error.Message,
                        LevelFingerprint=StructureValidator.Validate(failedRoot).IsValid?LevelFingerprint.Compute(failedRoot):null
                    };
                }
                changed?.Invoke(entry);
            }
        }
    }
}
