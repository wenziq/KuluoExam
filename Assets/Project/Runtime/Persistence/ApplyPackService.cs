using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Sokoban.Core.Data;
using Sokoban.Core.Identity;
using Sokoban.Core.Validation;
using Sokoban.Domain.Analysis;
using Sokoban.Runtime.Platform.Tasks;
namespace Sokoban.Runtime.Persistence
{
    public enum ApplyStatus { Applied, Blocked, Cancelled, Failed, Busy }
    public enum ApplyStage { SavingDraft, CheckingStructure, VerifyingLevels, Installing }
    public sealed class ApplyProgress
    {
        public ApplyStage Stage {get; internal set;}
        public int Completed {get; internal set;}
        public int Total {get; internal set;}
        public string LevelName {get; internal set;}
    }
    public sealed class ApplyProblem
    {
        public string LevelId,Name,Explanation;
        public AnalysisOutcome Outcome;
    }
    public sealed class ApplyResult
    {
        public ApplyStatus Status {get; internal set;}=ApplyStatus.Failed;
        public bool Applied=>Status==ApplyStatus.Applied;
        public bool DraftSaved {get; internal set;}
        public string SnapshotHash {get; internal set;}
        public PackData InstalledPack {get; internal set;}
        public string Error {get; internal set;}
        public IReadOnlyList<ApplyProblem> Problems {get; internal set;}=Array.Empty<ApplyProblem>();
    }
    public sealed class ApplyPackService
    {
        readonly UserDataPaths paths;readonly DraftRepository drafts;readonly InstalledPackRepository installed;readonly WitnessRepository witnesses;
        readonly HashSet<string> reserved;readonly Func<PackData,Task<TransactionResult>> saveDraft;int running;
        public ApplyPackService(UserDataPaths paths,IFileSystem files=null,IAnalysisSolver solver=null,IEnumerable<string> reservedIds=null,Func<PackData,Task<TransactionResult>> saveDraft=null)
        {this.paths=paths;drafts=new DraftRepository(paths,files);installed=new InstalledPackRepository(paths,files);witnesses=new WitnessRepository(solver);reserved=new HashSet<string>(reservedIds??Array.Empty<string>(),StringComparer.Ordinal);this.saveDraft=saveDraft??drafts.SaveAsync;}
        public bool IsRunning=>Volatile.Read(ref running)!=0;
        public Task<ApplyResult> ApplyAsync(PackData draft,IEnumerable<WitnessData> evidence=null,CancellationToken token=default,IProgress<ApplyProgress> progress=null)
        {
            if(Interlocked.CompareExchange(ref running,1,0)!=0)return Task.FromResult(new ApplyResult{Status=ApplyStatus.Busy,Error="已有一次应用正在处理，请等待结果。"});
            try
            {
                token.ThrowIfCancellationRequested();var snapshot=draft.DeepCopy();
                if(snapshot.documentKind!=DocumentKind.DraftPack||!PackValidator.Validate(snapshot).IsValid)throw new ArgumentException("应用需要可安全保存的草稿快照。");
                if(reserved.Contains(snapshot.packId))throw new ArgumentException("不能替换内置关卡集身份，请先创建独立副本。");
                string hash=DocumentHash.Compute(snapshot);
                var extra=(evidence??Array.Empty<WitnessData>()).Take(ContentLimits.MaxLevels+1).Select(w=>w?.DeepCopy()).ToList();
                if(extra.Count>ContentLimits.MaxLevels)throw new ArgumentException("补充证据超过关卡数量上限。");
                var candidates=snapshot.solutionWitnesses.Concat(extra).ToArray();
                progress?.Report(new ApplyProgress{Stage=ApplyStage.SavingDraft,Total=snapshot.levels.Count});
                // Join the ordinary draft queue immediately, in invocation order, before waiting on another application.
                var save=saveDraft(snapshot);
                return Complete(AsyncOperationQueue.Run("apply:"+paths.Primary(StorageArea.Installed,snapshot.packId),()=>Run(snapshot,hash,candidates,save,token,progress)));
            }
            catch(Exception error)
            {Interlocked.Exchange(ref running,0);return Task.FromResult(new ApplyResult{Status=error is OperationCanceledException?ApplyStatus.Cancelled:ApplyStatus.Failed,Error=error.Message});}
        }
        async Task<ApplyResult> Complete(Task<ApplyResult> task)
        {try{return await task.ConfigureAwait(false);}finally{Interlocked.Exchange(ref running,0);}}
        async Task<ApplyResult> Run(PackData snapshot,string hash,WitnessData[] candidates,Task<TransactionResult> save,CancellationToken token,IProgress<ApplyProgress> progress)
        {
            var result=new ApplyResult{SnapshotHash=hash};var problems=new List<ApplyProblem>();result.Problems=problems.AsReadOnly();
            try
            {
                var saved=await save.ConfigureAwait(false);result.DraftSaved=saved.Committed;
                if(!saved.Committed){result.Error="草稿保存失败："+saved.Error?.Message;return result;}
                token.ThrowIfCancellationRequested();progress?.Report(new ApplyProgress{Stage=ApplyStage.CheckingStructure,Total=snapshot.levels.Count});
                var structure=StructureValidator.Validate(snapshot);
                foreach(var group in structure.Issues.Where(i=>i.Severity==IssueSeverity.Error).GroupBy(i=>i.LevelId))
                    problems.Add(new ApplyProblem{LevelId=group.Key,Name=snapshot.levels.Find(l=>l.levelId==group.Key)?.name??"关卡集",Outcome=AnalysisOutcome.Invalid,Explanation=string.Join("\n",group.Select(i=>i.Message))});
                if(problems.Count>0){result.Status=ApplyStatus.Blocked;return result;}
                var previous=await AnalysisWorker.RunAsync(t=>installed.Load(snapshot.packId,t),token).ConfigureAwait(false);
                if(previous!=null)candidates=candidates.Concat(previous.solutionWitnesses).ToArray();
                var proofs=new List<WitnessData>();int completed=0;
                foreach(string id in snapshot.levelOrder)
                {
                    token.ThrowIfCancellationRequested();var level=snapshot.levels.Find(l=>l.levelId==id);
                    progress?.Report(new ApplyProgress{Stage=ApplyStage.VerifyingLevels,Completed=completed,Total=snapshot.levels.Count,LevelName=level.name});
                    var analysis=await witnesses.ResolveAsync(level,candidates,token).ConfigureAwait(false);completed++;
                    if(analysis.Outcome==AnalysisOutcome.Solvable)proofs.Add(analysis.Witness.DeepCopy());
                    else problems.Add(new ApplyProblem{LevelId=id,Name=level.name,Outcome=analysis.Outcome,Explanation=analysis.Explanation});
                }
                if(problems.Count>0){result.Status=ApplyStatus.Blocked;return result;}
                token.ThrowIfCancellationRequested();snapshot.documentKind=DocumentKind.PlayablePack;snapshot.solutionWitnesses=proofs;
                progress?.Report(new ApplyProgress{Stage=ApplyStage.Installing,Completed=completed,Total=completed});
                var commit=await installed.InstallAsync(snapshot,true,token).ConfigureAwait(false);
                if(commit.Committed){result.Status=ApplyStatus.Applied;result.InstalledPack=commit.Pack;return result;}
                result.Status=commit.Transaction.Status==TransactionStatus.Cancelled?ApplyStatus.Cancelled:ApplyStatus.Failed;result.Error=commit.Transaction.Error?.Message;return result;
            }
            catch(OperationCanceledException){result.Status=ApplyStatus.Cancelled;result.Error="应用已取消，已完成的草稿保存仍保留。";return result;}
            catch(Exception error){result.Status=ApplyStatus.Failed;result.Error=error.Message;return result;}
        }
    }
}
