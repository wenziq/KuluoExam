using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Sokoban.Core.Data;
using Sokoban.Core.Identity;
using Sokoban.Core.Rules;
using Sokoban.Core.Validation;
using Sokoban.Domain.Analysis;
using Sokoban.Domain.Gameplay;
using Sokoban.Domain.Workshop;
using Sokoban.Runtime.Platform.Tasks;
using Sokoban.Runtime.Presentation.Workshop;
using UnityEngine;
namespace Sokoban.Runtime.Presentation.Analysis
{
    public enum AnalysisPurpose { Solvability, Playability }
    public sealed class AnalysisReport
    {
        internal LevelData Root;
        public LevelData Level => Root.DeepCopy();
        public string DocumentId { get; internal set; }
        public string Fingerprint { get; internal set; }
        public DateTime CapturedUtc { get; internal set; }
        public AnalysisPurpose Purpose { get; internal set; }
        public AnalysisResult Result { get; internal set; }
        public AnalysisResult Evidence { get; internal set; }
        public ReferenceSolutionMetrics Metrics { get; internal set; }
        public int? Moves { get; internal set; }
        public int? Pushes { get; internal set; }
        public string MetricsMessage { get; internal set; }
        public AnalysisJobState State { get; internal set; }
        public AnalysisProgress Progress { get; internal set; }
        public bool IsCurrent { get; internal set; }
        public bool UsedExtendedSearch { get; internal set; }
        internal AnalysisRequestIdentity Identity;
    }
    public sealed class AnalysisMenuController : MonoBehaviour
    {
        public AnalysisCoordinator Job { get; private set; }
        public AnalysisCache Cache { get; } = new AnalysisCache();
        public string StatusText { get; private set; } = "尚未分析";
        public AnalysisReport CurrentReport { get; private set; }
        public AnalysisReport SelectedReport { get; private set; }
        public WorkshopApplicationCoordinator Owner { get; private set; }
        public ProductionAnalysisController Production {get; private set;}
        internal IAnalysisSolver Backend=>solver;
        internal AnalysisReport PriorReport(string id)=>lastReports.TryGetValue(id,out var report)?report:null;
        readonly Dictionary<string,AnalysisReport> lastReports = new Dictionary<string,AnalysisReport>();
        MainThreadDispatcher dispatcher;
        IAnalysisSolver solver;
        WorkshopDocument document;
        ReportSolver reportSolver;
        AnalysisResultModal window;
        long lifecycle;
        bool initialized;
        public void Configure(IAnalysisSolver backend)
        { if(initialized)throw new InvalidOperationException("分析服务已经初始化。");solver=backend; }
        public void Initialize(WorkshopApplicationCoordinator owner)
        {
            if(initialized)return; initialized=true; Owner=owner; solver=solver??new PushAStarSolver();
            dispatcher=new MainThreadDispatcher();Job=new AnalysisCoordinator(dispatcher,Cache,solver);Job.Changed+=JobChanged;
            owner.RegisterOperation("CheckStructure",CheckStructure);
            owner.RegisterOperation("Solve",()=>Analyze());
            owner.RegisterOperation("Playability",()=>Analyze(AnalysisPurpose.Playability));
            Production=gameObject.GetComponent<ProductionAnalysisController>()??gameObject.AddComponent<ProductionAnalysisController>();Production.Initialize(this);
            owner.DocumentChanged+=RefreshSelection;owner.DocumentOpened+=RefreshSelection;owner.App.NavigationRequested+=Navigated;owner.TrialCompleted+=ManualCompletion;
        }
        public LevelData CurrentLevel()
        { var pack=Owner?.Document?.Snapshot();return pack?.levels.Find(level=>level.levelId==Owner.Document.SelectedLevelId); }
        public void Analyze(AnalysisPurpose purpose=AnalysisPurpose.Solvability,bool deep=false,AnalysisBudget budget=null)
        {
            Owner.View?.CommitFields();Owner.View?.CommitGesture();RefreshSelection();var root=CurrentLevel();
            if(root==null){Owner.App.Modal.Show("请先选择关卡","新建或选择一关，再检查地图与通关路线。");return;}
            if(!StructureValidator.Validate(root).IsValid){CheckStructure();return;}
            Owner.App.Modal.Close();
            var fingerprint=LevelFingerprint.Compute(root);var evidence=Cache.Evidence(root,PushAStarSolver.AlgorithmVersion);
            var previous=SelectedReport;
            var identity=new AnalysisRequestIdentity(Guid.NewGuid().ToString("N"),Owner.Document.Snapshot().packId,fingerprint,purpose.ToString(),"Initial",lifecycle);
            var report=new AnalysisReport{Root=root.DeepCopy(),DocumentId=identity.DocumentId,Fingerprint=fingerprint,CapturedUtc=DateTime.UtcNow,Purpose=purpose,State=AnalysisJobState.Queued,IsCurrent=true,Identity=identity,Evidence=evidence,UsedExtendedSearch=deep};
            if(previous?.Fingerprint==fingerprint&&previous.Evidence?.Witness?.moves==evidence?.Witness?.moves)
            {report.Metrics=previous.Metrics;report.Moves=previous.Moves;report.Pushes=previous.Pushes;}
            CurrentReport=report;lastReports[root.levelId]=report;
            reportSolver=new ReportSolver(solver,purpose==AnalysisPurpose.Playability&&!deep?evidence:null);
            window=AnalysisResultModal.Open(this,report,()=>{if(report.Identity.Matches(Job.CurrentIdentity)&&Running)Job.Cancel();});
            Job.Start(root,identity,budget??(deep?AnalysisBudget.Deep:AnalysisBudget.Normal),reportSolver);
            Owner.View?.SetTab(1);RefreshSelection();
        }
        public bool Running => Job!=null&&(Job.State==AnalysisJobState.Queued||Job.State==AnalysisJobState.Running);
        void JobChanged()
        {
            var report=CurrentReport;if(report==null||!report.Identity.Matches(Job.CurrentIdentity))return;
            report.State=Job.State;report.Progress=Job.Progress;
            if(!Running)
            {
                report.Result=Job.LastResult;report.Evidence=report.Result?.Outcome==AnalysisOutcome.Solvable?report.Result.Copy():Job.Evidence;
                if(Job.State==AnalysisJobState.Completed&&report.Result?.Outcome==AnalysisOutcome.Solvable)
                {
                    report.Metrics=reportSolver.Metrics;report.MetricsMessage=reportSolver.MetricsMessage;
                    report.Moves=reportSolver.Moves;report.Pushes=reportSolver.Pushes;
                    PersistWitness(report);TrimMetrics();
                }
            }
            RefreshSelection();if(window!=null&&window.IsCurrent)window.Render(report);
        }
        public string SummaryFor(LevelData root)
        {
            if(!StructureValidator.Validate(root).IsValid)return "结构待修复";
            var prior=PriorReport(root.levelId);if(prior==null)return "待分析";
            if(prior.Fingerprint!=LevelFingerprint.Compute(root))return "已过期";
            if(prior.Evidence!=null)return "有解";
            return prior.Result?.Outcome==AnalysisOutcome.Unsolvable?"无解":prior.State==AnalysisJobState.Running||prior.State==AnalysisJobState.Queued?"分析中":"未知";
        }
        public void RefreshSelection()
        {
            if(!initialized)return;
            if(!ReferenceEquals(document,Owner.Document))
            {document=Owner.Document;lifecycle++;Job.Detach();lastReports.Clear();CurrentReport=null;SelectedReport=null;}
            var root=CurrentLevel();
            if(root==null){StatusText="尚未选择关卡";SelectedReport=null;Owner.View?.AnalysisPanel?.Refresh();return;}
            bool valid=StructureValidator.Validate(root).IsValid;string fingerprint=valid?LevelFingerprint.Compute(root):null;
            if(CurrentReport!=null)
            {
                CurrentReport.IsCurrent=CurrentReport.DocumentId==Owner.Document.Snapshot().packId&&CurrentReport.Root.levelId==root.levelId&&fingerprint==CurrentReport.Fingerprint;
                if(!CurrentReport.IsCurrent&&Running)Job.Cancel();
            }
            lastReports.TryGetValue(root.levelId,out var prior);SelectedReport=prior!=null&&prior.Fingerprint==fingerprint?prior:null;
            if(!valid)StatusText="结构待修复";
            else if(Running&&CurrentReport?.IsCurrent==true)StatusText="正在分析…";
            else
            {
                var evidence=Cache.Evidence(root,PushAStarSolver.AlgorithmVersion);var latest=Cache.Latest(root,PushAStarSolver.AlgorithmVersion);
                if(evidence!=null)StatusText=evidence.Optimality==AnalysisOptimality.PushOptimal?"有解 · 最少推动已证明":"有解 · 已验证参考解";
                else if(latest?.Outcome==AnalysisOutcome.Unsolvable)StatusText="无解 · 已有可靠证明";
                else if(latest!=null)StatusText=latest.StopReason==AnalysisStopReason.Cancelled?"已取消 · 暂未判定":"暂未判定";
                else StatusText=prior!=null?"上次分析已过期":"尚未分析";
            }
            Owner.View?.AnalysisPanel?.Refresh();Owner.View?.RefreshAnalysisBadges();Production.RefreshOverlays();
        }
        public void CheckStructure()
        {
            Owner.View?.CommitFields();Owner.View?.CommitGesture();var root=CurrentLevel();
            if(root==null){Owner.App.Modal.Show("请先选择关卡","新建关卡后可以检查地图中的遗漏项。");return;}
            StructureReportModal.Show(this,root,StructureValidator.Validate(root));
        }
        public void Locate(ValidationIssue issue)
        {
            Owner.App.Modal.Close();if(issue.LevelId!=null)Owner.Select(issue.LevelId);Owner.View?.SetTab(2);
            if(issue.Position.HasValue)
            {
                Owner.View?.SelectCell(issue.Position.Value.x,issue.Position.Value.y);
                var root=CurrentLevel();if(issue.ObjectId!=null&&root!=null)
                {
                    if(root.features.Any(f=>f.id==issue.ObjectId))Owner.View.SelectLayer(SelectionLayer.Feature);
                    else if(root.entities.Any(e=>e.id==issue.ObjectId))Owner.View.SelectLayer(SelectionLayer.Entity);
                }
            }
            else if(issue.Code=="PLAYER_MISSING")Owner.View?.Palette.Choose(2);
            else if(issue.Code=="BOX_MISSING")Owner.View?.Palette.Choose(3);
        }
        void Navigated(string page)
        { if(page!="Workshop"){Job.Detach();window=null;}else RefreshSelection(); }
        void ManualCompletion(GameSession session)
        {
            var root=session.InitialState.ToLevelData();var witness=session.CreateWitness();
            var result=new AnalysisResult{Outcome=AnalysisOutcome.Solvable,Optimality=AnalysisOptimality.NotProven,StopReason=AnalysisStopReason.SolutionFound,LevelFingerprint=LevelFingerprint.Compute(root),Witness=witness,Explanation="人工试玩已从初始布局重放验证；未证明最优。"};
            Cache.Record(root,result,PushAStarSolver.AlgorithmVersion);
            if(document!=null)Publish(new AnalysisReport{Root=root,DocumentId=document.Snapshot().packId,Fingerprint=result.LevelFingerprint,CapturedUtc=DateTime.UtcNow,Result=result,Evidence=result,Moves=session.Moves,Pushes=session.Pushes,State=AnalysisJobState.Completed,IsCurrent=true});
        }
        internal void Publish(AnalysisReport report)
        {
            Cache.Record(report.Root,report.Result,PushAStarSolver.AlgorithmVersion);report.Evidence=report.Result.Outcome==AnalysisOutcome.Solvable?report.Result.Copy():Cache.Evidence(report.Root,PushAStarSolver.AlgorithmVersion);
            lastReports[report.Root.levelId]=report;PersistWitness(report);TrimMetrics();RefreshSelection();
        }
        void TrimMetrics()
        {
            var ids=new HashSet<string>(Owner.Document.Snapshot().levelOrder);
            foreach(string id in lastReports.Keys.Where(id=>!ids.Contains(id)).ToArray())lastReports.Remove(id);
            LimitMetricStorage(lastReports.Values);
        }
        internal static void LimitMetricStorage(IEnumerable<AnalysisReport> reports)
        {
            var ordered=reports.OrderByDescending(r=>r.CapturedUtc).ToArray();long bytes=0;
            foreach(var report in ordered)
            {
                long cost=(report.Metrics?.Events.Count??0)*160L;
                if(cost>32L*1024*1024-bytes){report.Metrics=null;report.MetricsMessage="较早的指标已从有限缓存移出，可重新分析；通关证据仍保留。";}
                else bytes+=cost;
            }
        }
        void PersistWitness(AnalysisReport report)
        {
            if(report.Evidence?.Witness==null||document==null)return;
            var pack=document.Snapshot();var current=pack.levels.Find(l=>l.levelId==report.Root.levelId);
            if(current==null||!StructureValidator.Validate(current).IsValid||LevelFingerprint.Compute(current)!=report.Fingerprint)return;
            var witness=report.Evidence.Witness.DeepCopy();
            document.Edit("保留已验证参考解",current.levelId,p=>{p.solutionWitnesses.RemoveAll(w=>w.levelId==current.levelId);p.solutionWitnesses.Add(witness);});
        }
        void Update(){dispatcher?.Drain();}
        void OnDisable(){Job?.Detach();}
        void OnDestroy()
        {
            if(!initialized)return;Owner.DocumentChanged-=RefreshSelection;Owner.DocumentOpened-=RefreshSelection;Owner.App.NavigationRequested-=Navigated;Owner.TrialCompleted-=ManualCompletion;
            foreach(string id in new[]{"CheckStructure","Solve","Playability"})Owner.UnregisterOperation(id);
            Job.Changed-=JobChanged;Job.Dispose();
        }
        // Each request owns one wrapper. It never reads Unity objects or mutable workshop state.
        internal sealed class ReportSolver:IAnalysisSolver
        {
            readonly IAnalysisSolver backend;readonly AnalysisResult cached;
            public ReferenceSolutionMetrics Metrics { get; private set; }
            public string MetricsMessage { get; private set; }
            public int? Moves { get; private set; }
            public int? Pushes { get; private set; }
            public ReportSolver(IAnalysisSolver backend,AnalysisResult cached){this.backend=backend;this.cached=cached?.Copy();}
            public AnalysisResult Solve(LevelData root,AnalysisBudget budget,CancellationToken token,Action<AnalysisProgress> progress)
            {
                var clock=Stopwatch.StartNew();var result=cached?.Copy()??backend.Solve(root,budget,token,progress);
                if(result.Outcome!=AnalysisOutcome.Solvable)return result;
                var verified=WitnessVerifier.Verify(root,result.Witness,cancellationToken:token);
                if(!verified.IsValid)throw new InvalidOperationException("参考解未通过规则验证："+verified.Reason);
                Moves=verified.Moves;Pushes=verified.Pushes;
                using(var timed=CancellationTokenSource.CreateLinkedTokenSource(token))
                {
                    long remaining=Math.Max(0,budget.MaxElapsedMilliseconds-clock.ElapsedMilliseconds);
                    if(remaining<int.MaxValue)timed.CancelAfter((int)remaining);
                    var tracker=new SearchBudget(new AnalysisBudget(remaining,long.MaxValue,budget.MaxEstimatedBytes),timed.Token);
                    if(!tracker.TryReserve(1024*1024L+result.Witness.moves.Length*160L)){MetricsMessage="指标预算不足，通关证据仍然有效。";return result;}
                    try{Metrics=ReferenceSolutionMetrics.Calculate(root,result.Witness,timed.Token,tracker.ThrowIfStopped);}
                    catch(OperationCanceledException){token.ThrowIfCancellationRequested();MetricsMessage="指标计算达到预算，通关证据仍然有效。";}
                    catch(Exception){token.ThrowIfCancellationRequested();MetricsMessage=tracker.StopReason!=AnalysisStopReason.None?"指标计算达到预算，通关证据仍然有效。":"指标暂不可用，可以重新分析。";}
                }
                return result;
            }
        }
    }
}
