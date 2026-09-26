using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Sokoban.Core.Data;
using Sokoban.Core.Identity;
using Sokoban.Core.Validation;
using Sokoban.Domain.Analysis;
using Sokoban.Domain.Workshop;
using Sokoban.Runtime.Persistence;
using Sokoban.Runtime.Presentation.Analysis;
using Sokoban.Runtime.Presentation.Style;
using Sokoban.Runtime.Presentation.Workshop;
using TMPro;
using UnityEngine;
namespace Sokoban.Runtime.Presentation.Controls
{
    public sealed class ApplyPackModal:MonoBehaviour
    {
        WorkshopApplicationCoordinator owner;WorkshopPersistenceController persistence;ApplyPackService service;IAnalysisSolver solver;
        CancellationTokenSource cancellation;bool destroyed;
        WorkshopDocument appliedDocument;string appliedHash;int appliedRevision;
        public Task<ApplyResult> LastApply {get; private set;}=Task.FromResult<ApplyResult>(null);
        public bool IsApplying=>service?.IsRunning==true;
        public bool HasPlayableVersion=>ReferenceEquals(appliedDocument,owner?.Document)&&appliedHash!=null&&owner.Game.Catalog.Entries.Any(e=>e.Source==ContentSource.Installed&&e.PackId==owner.Document.Snapshot().packId);
        public string ActionLabel=>HasPlayableVersion?"更新可玩关卡集":"加入可玩关卡集";
        public string ApplicationStatus=>!HasPlayableVersion?"草稿 · 尚未加入可玩关卡集":owner.Document.CurrentHash==appliedHash?"已加入可玩关卡集 r"+appliedRevision:"修改未同步 · r"+appliedRevision;
        public void Configure(IAnalysisSolver backend)
        {if(owner!=null)throw new InvalidOperationException("应用服务已初始化。");solver=backend;}
        public void Initialize(WorkshopApplicationCoordinator coordinator)
        {
            if(owner!=null)return;owner=coordinator;persistence=GetComponent<WorkshopPersistenceController>();
            service=new ApplyPackService(owner.Game.DataPaths,owner.Game.Files,solver,owner.Game.Catalog.Entries.Where(e=>e.Source==ContentSource.BuiltIn).Select(e=>e.PackId),snapshot=>persistence.SaveSnapshotAsync(owner.Document,snapshot));
            owner.RegisterOperation("ApplyPack",StartApply);owner.DocumentOpened+=Opened;owner.App.NavigationRequested+=Navigated;
        }
        void Opened()
        {
            cancellation?.Cancel();appliedDocument=owner.Document;appliedHash=null;appliedRevision=0;
            if(owner.Document==null)return;string id=owner.Document.Snapshot().packId;
            var installed=owner.Game.Catalog.Entries.FirstOrDefault(e=>e.Source==ContentSource.Installed&&e.PackId==id)?.Pack;
            if(installed!=null){installed.documentKind=DocumentKind.DraftPack;appliedHash=DocumentHash.Compute(installed);appliedRevision=installed.contentRevision;}
            owner.View?.Refresh();
        }
        void Navigated(string page){if(page!="Workshop")cancellation?.Cancel();}
        void StartApply()
        {
            if(owner.Document==null||persistence.IsDeleting)return;
            if(service.IsRunning){owner.App.Modal.Show("上一请求仍在处理","请等待上一请求结束后重试。已经进入文件提交的操作会报告真实结果。");return;}
            owner.View?.CommitFields();owner.View?.CommitGesture();var document=owner.Document;var snapshot=document.Snapshot();
            var extras=new List<WitnessData>();var analysis=GetComponent<AnalysisMenuController>();
            foreach(var level in snapshot.levels)
            {
                if(!StructureValidator.Validate(level).IsValid)continue;string fingerprint=LevelFingerprint.Compute(level);
                if(snapshot.solutionWitnesses.Any(w=>w.levelId==level.levelId&&w.levelFingerprint==fingerprint))continue;
                var cached=analysis.Cache.Evidence(level,PushAStarSolver.AlgorithmVersion);if(cached?.Witness!=null)extras.Add(cached.Witness);
            }
            var source=new CancellationTokenSource();cancellation=source;
            string actionLabel=ActionLabel;
            owner.App.Modal.Show(actionLabel,"正在保存草稿并检查每一关是否有解。全部通过后，即可在关卡集中正式游玩。\n取消会停止本次操作；已经保存的草稿仍保留。",()=>{if(ReferenceEquals(cancellation,source))source.Cancel();});
            var marker=owner.App.Modal.Body;UnsavedChangesModal.RemoveDone(owner.App);owner.App.Modal.AddAction("取消操作",()=>{if(ReferenceEquals(cancellation,source))source.Cancel();},false);
            bool Current()=>!destroyed&&ReferenceEquals(owner.Document,document)&&owner.App.CurrentPage=="Workshop"&&owner.App.Modal.IsOpen&&owner.App.Modal.Body==marker;
            var progress=new Progress<ApplyProgress>(p=>
            {
                if(!Current()||!service.IsRunning)return;
                string message=ProgressMessage(p);
                owner.App.Modal.SetMessage(message+"\n\n本次使用点击时的内容。之后的编辑需要再次更新可玩关卡集。取消不会删除草稿或已有可玩版本。");
            });
            LastApply=Run();
            async Task<ApplyResult> Run()
            {
                try
                {
                    var result=await service.ApplyAsync(snapshot,extras,source.Token,progress);
                    if(destroyed)return result;
                    owner.Game.ReloadCatalog();
                    if(result.Applied&&ReferenceEquals(owner.Document,document))
                    {
                        appliedDocument=document;appliedHash=result.SnapshotHash;appliedRevision=result.InstalledPack.contentRevision;
                        document.Edit("记录已应用证据",document.SelectedLevelId,p=>
                        {
                            p.contentRevision=appliedRevision;
                            foreach(var witness in result.InstalledPack.solutionWitnesses)
                            {
                                var current=p.levels.Find(l=>l.levelId==witness.levelId);
                                if(current==null||!StructureValidator.Validate(current).IsValid||LevelFingerprint.Compute(current)!=witness.levelFingerprint)continue;
                                p.solutionWitnesses.RemoveAll(w=>w.levelId==witness.levelId);p.solutionWitnesses.Add(witness.DeepCopy());
                            }
                        });
                        foreach(var level in result.InstalledPack.levels)
                        {
                            var witness=result.InstalledPack.solutionWitnesses.Find(w=>w.levelId==level.levelId);
                            var proof=new AnalysisResult{Outcome=AnalysisOutcome.Solvable,Optimality=AnalysisOptimality.NotProven,StopReason=AnalysisStopReason.SolutionFound,LevelFingerprint=witness.levelFingerprint,Witness=witness.DeepCopy(),Explanation="应用时已从初始布局完整验证通关路线。"};
                            analysis.Cache.Record(level,proof,PushAStarSolver.AlgorithmVersion);
                            var prior=analysis.PriorReport(level.levelId);
                            if(!analysis.Running&&(prior?.Fingerprint!=proof.LevelFingerprint||prior.Evidence==null))
                                analysis.Publish(new AnalysisReport{Root=level.DeepCopy(),DocumentId=snapshot.packId,Fingerprint=proof.LevelFingerprint,CapturedUtc=DateTime.UtcNow,Result=proof,Evidence=proof,State=AnalysisJobState.Completed});
                        }
                        owner.Changed();
                    }
                    if(Current())Render(result,document,actionLabel);
                    return result;
                }
                finally{if(ReferenceEquals(cancellation,source))cancellation=null;source.Dispose();}
            }
        }
        static string ProgressMessage(ApplyProgress progress)
        {
            switch(progress.Stage)
            {
                case ApplyStage.SavingDraft: return "正在保存草稿快照…";
                case ApplyStage.CheckingStructure: return "正在检查关卡和顺序…";
                case ApplyStage.Installing: return "全部证据通过，正在提交完整可玩包…";
                default: return "正在验证第 "+(progress.Completed+1)+" / "+progress.Total+" 关："+progress.LevelName;
            }
        }
        void Render(ApplyResult result,WorkshopDocument document,string actionLabel)
        {
            WorkshopFields.Clear(owner.App.Modal.Actions);
            if(result.Applied)
            {
                bool changed=document.CurrentHash!=result.SnapshotHash;
                owner.App.Modal.SetMessage(actionLabel+"成功\n"+result.InstalledPack.name+" · "+result.InstalledPack.levels.Count+" 关 · r"+result.InstalledPack.contentRevision+"\n\n"+(changed?"提交的是启动时快照；后续修改尚未同步到可玩关卡集。" : "已可在“关卡集”中选择这套关卡正式游玩。")+"\n草稿与正式成绩分别保存。");
                owner.App.Modal.AddAction("从第一关开始",()=>StartFirst(result.InstalledPack));owner.App.Modal.AddAction("继续编辑",owner.App.Modal.Close,false);
            }
            else
            {
                string heading=result.Status==ApplyStatus.Blocked?"暂时不能"+actionLabel:result.Status==ApplyStatus.Cancelled?"操作已取消":"操作未完成";
                owner.App.Modal.SetMessage(heading+"\n"+(result.DraftSaved?"启动时草稿快照已保存。":"当前编辑仍保留。")+"正式游戏继续使用上一有效版本。\n"+result.Error+"\n"+string.Join("\n\n",result.Problems.Select(p=>p.Name+"："+p.Explanation)));
                owner.App.Modal.AddAction("关闭结果",owner.App.Modal.Close,false);owner.App.Modal.AddAction("重试",StartApply);
                foreach(var problem in result.Problems)
                {
                    if(problem.LevelId==null)continue;var captured=problem;
                    var button=UiFactory.Button("Locate_"+problem.LevelId,owner.App.Modal.Body.transform.parent,"前往："+problem.Name,owner.App.theme,()=>
                    {
                        owner.App.Modal.Close();if(!owner.Document.Snapshot().levelOrder.Contains(captured.LevelId))return;owner.Select(captured.LevelId);owner.View?.SetTab(captured.Outcome==AnalysisOutcome.Invalid?2:1);
                        if(captured.Outcome==AnalysisOutcome.Unknown)
                        {
                            owner.App.Modal.Show("通关证据尚未建立","暂时无法判断是否有解。可以打开有解性分析查看结果，或亲自试玩通关后再加入可玩关卡集。");
                            owner.App.Modal.AddAction("分析有解性",()=>GetComponent<AnalysisMenuController>().Analyze(AnalysisPurpose.Solvability));
                            owner.App.Modal.AddAction("人工试玩",()=>{owner.App.Modal.Close();owner.StartTrial(false);});
                        }
                    });UiFactory.Preferred(button.gameObject,38);
                }
            }
        }
        void StartFirst(PackData pack)
        {
            owner.App.Modal.Close();void Go(){owner.Game.SelectPack(pack);owner.Game.StartLevel(pack.levelOrder[0]);}
            if(persistence.Guard(Go))Go();
        }
        void OnDestroy()
        {
            destroyed=true;cancellation?.Cancel();if(owner==null)return;owner.UnregisterOperation("ApplyPack");owner.DocumentOpened-=Opened;owner.App.NavigationRequested-=Navigated;
        }
    }
}
