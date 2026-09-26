using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sokoban.Core.Data;
using Sokoban.Core.Validation;
using Sokoban.Domain.Analysis;
using Sokoban.Domain.Gameplay;
using Sokoban.Domain.Workshop;
using Sokoban.Runtime.Platform.Tasks;
using Sokoban.Runtime.Presentation.Board;
using Sokoban.Runtime.Presentation.Style;
using Sokoban.Runtime.Presentation.Workshop;
using TMPro;
using UnityEngine;
namespace Sokoban.Runtime.Presentation.Analysis
{
    public sealed class ProductionAnalysisController:MonoBehaviour
    {
        AnalysisMenuController analysis;
        CancellationTokenSource cancellation;long generation;
        WorkshopDocument document;string capturedHash;TextMeshProUGUI marker;
        RectTransform deadLayer;
        public bool DeadCellsVisible {get; private set;}
        public BatchAnalysisQueue Batch {get; private set;}
        public PlaybackView Playback {get; private set;}
        public Task LastTask {get; private set;}=Task.CompletedTask;
        WorkshopApplicationCoordinator Owner=>analysis.Owner;
        public void Initialize(AnalysisMenuController controller)
        {
            if(analysis!=null)return;analysis=controller;
            Owner.RegisterOperation("AnalyzePack",AnalyzePack);Owner.RegisterOperation("RecheckWitness",Recheck);
            Owner.DocumentChanged+=Changed;Owner.DocumentOpened+=Opened;Owner.App.NavigationRequested+=Navigated;
        }
        void Opened(){DeadCellsVisible=false;Cancel();}
        void Navigated(string page){if(page!="Workshop")Cancel();else RefreshOverlays();}
        void Changed()
        {
            if(cancellation!=null&&(!ReferenceEquals(document,Owner.Document)||capturedHash!=Owner.Document?.CurrentHash))
            {if(IsWindow)Finish("地图已改变，本次生产分析已取消。已完成的快照结果不会覆盖当前地图。请关闭后重新分析。");Cancel();}
            RefreshOverlays();
        }
        bool IsWindow=>marker!=null&&Owner.App.Modal.IsOpen&&Owner.App.Modal.Body==marker;
        void Cancel(){generation++;cancellation?.Cancel();}
        void StartOperation(string title,Func<CancellationToken,long,Task> operation)
        {
            Owner.View?.CommitFields();Owner.View?.CommitGesture();Cancel();Owner.App.Modal.Close();if(analysis.Running)analysis.Job.Cancel();
            document=Owner.Document;capturedHash=document?.CurrentHash;var source=new CancellationTokenSource();cancellation=source;long request=++generation;
            Owner.App.Modal.Show(title,"正在准备快照…",()=>{if(ReferenceEquals(cancellation,source))source.Cancel();});marker=Owner.App.Modal.Body;
            Owner.App.Modal.AddAction("取消计算",()=>{if(ReferenceEquals(cancellation,source))source.Cancel();});
            LastTask=Run();
            async Task Run()
            {
                try{await operation(source.Token,request);}
                catch(OperationCanceledException){if(Valid(request,false))Finish("本次计算已取消；此前完成的结果保留。");}
                catch(Exception error){if(Valid(request,false))Finish("操作未完成："+error.Message+"\n当前草稿保持不变。");}
                finally{if(ReferenceEquals(cancellation,source))cancellation=null;source.Dispose();}
            }
        }
        bool Valid(long request,bool requireActive=true)=>this!=null&&request==generation&&IsWindow&&ReferenceEquals(document,Owner.Document)&&capturedHash==Owner.Document?.CurrentHash&&Owner.App.CurrentPage=="Workshop"&&(!requireActive||cancellation?.IsCancellationRequested==false);
        void Finish(string message)
        {
            Owner.App.Modal.SetMessage(message);WorkshopFields.Clear(Owner.App.Modal.Actions);Owner.App.Modal.AddAction("关闭结果",Owner.App.Modal.Close,false);
        }
        AnalysisReport Report(LevelData root,AnalysisResult result,AnalysisMenuController.ReportSolver metrics=null)=>new AnalysisReport{Root=root.DeepCopy(),DocumentId=document.Snapshot().packId,Fingerprint=result.LevelFingerprint,CapturedUtc=DateTime.UtcNow,Purpose=AnalysisPurpose.Playability,Result=result,Evidence=result.Outcome==AnalysisOutcome.Solvable?result:null,Metrics=metrics?.Metrics,MetricsMessage=metrics?.MetricsMessage,Moves=metrics?.Moves,Pushes=metrics?.Pushes,State=AnalysisJobState.Completed,IsCurrent=true};
        public void AnalyzePack()
        {
            if(Owner.Document==null)return;
            StartOperation("分析整个关卡集",async(token,request)=>
            {
                Batch=new BatchAnalysisQueue(document.Snapshot());var reports=new Dictionary<string,AnalysisReport>();
                await Batch.RunAsync(async(root,ct)=>
                {
                    AnalysisResult cached=null;
                    if(StructureValidator.Validate(root).IsValid)
                    {cached=analysis.Cache.Evidence(root,PushAStarSolver.AlgorithmVersion);if(cached==null){var latest=analysis.Cache.Latest(root,PushAStarSolver.AlgorithmVersion);if(latest?.Outcome==AnalysisOutcome.Unsolvable)cached=latest;}}
                    var worker=new AnalysisMenuController.ReportSolver(analysis.Backend,cached);
                    var result=await AnalysisWorker.RunAsync(t=>worker.Solve(root,AnalysisBudget.Normal,t,null),ct);
                    reports[root.levelId]=Report(root,result,worker);return result;
                },token,entry=>
                {
                    if(!Valid(request,false))return;
                    if(entry.State==AnalysisJobState.Completed||entry.State==AnalysisJobState.Failed)
                    {
                        if(!reports.TryGetValue(entry.LevelId,out var report))reports[entry.LevelId]=report=Report(entry.Level,entry.Result);
                        analysis.Publish(report);
                    }
                    Owner.App.Modal.SetMessage(BatchReportView.Describe(Batch,reports));
                });
                if(!Valid(request,false))return;Finish(BatchReportView.Describe(Batch,reports));
                foreach(var entry in Batch.Entries)
                {
                    string id=entry.LevelId;var button=UiFactory.Button("BatchLevel_"+id,marker.transform.parent,"查看："+entry.Name,Owner.App.theme,()=>{Owner.App.Modal.Close();Owner.Select(id);Owner.View?.SetTab(1);});UiFactory.Preferred(button.gameObject,36);
                }
            });
        }
        public void Recheck()
        {
            if(Owner.Document==null)return;
            var root=analysis.CurrentLevel();if(root==null)return;
            var old=analysis.PriorReport(root.levelId)?.Evidence?.Witness??Owner.Document.Snapshot().solutionWitnesses.Find(w=>w.levelId==root.levelId);
            if(old==null){Owner.App.Modal.Show("没有旧参考解","先完成一次分析或试玩，再修改地图，便可复检旧路线。");return;}
            StartOperation("复检旧参考解",async(token,request)=>
            {
                root=analysis.CurrentLevel();var existing=analysis.Cache.Evidence(root,PushAStarSolver.AlgorithmVersion);AnalysisMenuController.ReportSolver details=null;
                var result=await AnalysisWorker.RunAsync(t=>
                {
                    var checkedResult=WitnessRecheckService.Recheck(root,old,t);
                    if(checkedResult.Outcome==AnalysisOutcome.Solvable&&old.levelFingerprint==checkedResult.LevelFingerprint&&existing?.Witness?.moves==checkedResult.Witness.moves)
                    {checkedResult.Optimality=existing.Optimality;checkedResult.Explanation="布局指纹没有变化，旧路线重放成功；保留原有最优性结论。";}
                    details=new AnalysisMenuController.ReportSolver(analysis.Backend,checkedResult);return details.Solve(root,AnalysisBudget.Normal,t,null);
                },token);
                if(!Valid(request))return;
                if(result.Outcome==AnalysisOutcome.Solvable)analysis.Publish(Report(root,result,details));
                Finish(result.Explanation);
                if(result.Outcome==AnalysisOutcome.Solvable)Owner.App.Modal.AddAction("查看参考解",()=>OpenPlayback(analysis.SelectedReport));
                else
                {
                    Owner.App.Modal.AddAction("继续搜索",()=>analysis.Analyze());
                    if(result.ProblemCell.HasValue)Owner.App.Modal.AddAction("定位失败步骤",()=>{Owner.App.Modal.Close();Owner.View?.SelectCell(result.ProblemCell.Value.x,result.ProblemCell.Value.y);Owner.View?.SetTab(0);});
                }
            });
        }
        public void OpenPlayback(AnalysisReport report,int push=0)
        {
            if(report?.Evidence?.Witness==null){Owner.App.Modal.Show("参考解不可用","请先分析当前关卡或完成一次试玩。");return;}
            var root=report.Level;var witness=report.Evidence.Witness.DeepCopy();
            StartOperation("准备参考解回放",async(token,request)=>
            {
                var session=await AnalysisWorker.RunAsync(t=>new SolutionPlaybackSession(root,witness,t),token);
                if(!Valid(request))return;session.SeekPush(Math.Max(0,Math.Min(push,session.TotalPushes)));Owner.App.Modal.Close();Playback=Owner.ShowPlayback(root,session);
            });
        }
        public void ToggleDeadCells(){DeadCellsVisible=!DeadCellsVisible;RefreshOverlays();Owner.View?.AnalysisPanel?.Refresh();}
        public void RefreshOverlays()
        {
            if(analysis==null||Owner.View==null)return;
            if(!DeadCellsVisible){if(deadLayer!=null)deadLayer.gameObject.SetActive(false);return;}
            var root=analysis.CurrentLevel();if(root==null)return;
            var distances=new ReversePushDistances(root);var cells=new List<Coordinate>();for(int i=0;i<root.terrain.Length;i++)if(distances.IsDead(i))cells.Add(Coordinate.FromIndex(i,root.width));
            DrawOverlay(ref deadLayer,"StaticDeadCells",cells,BoardSymbol.DeadCell);
        }
        void DrawOverlay(ref RectTransform layer,string name,IReadOnlyList<Coordinate> cells,BoardSymbol symbol)
        {
            var board=Owner.View?.Board;if(board==null)return;if(layer!=null){layer.gameObject.SetActive(false);Destroy(layer.gameObject);}
            layer=UiFactory.Rect(name,board.overlayLayer);UiFactory.Fill(layer);layer.SetAsFirstSibling();
            foreach(var cell in cells)
            {
                if(!cell.IsInside(board.Width,board.Height))continue;
                var rect=UiFactory.Rect("Mark_"+cell.x+"_"+cell.y,layer);rect.anchorMin=new Vector2((float)cell.x/board.Width,(float)cell.y/board.Height);rect.anchorMax=new Vector2((float)(cell.x+1)/board.Width,(float)(cell.y+1)/board.Height);rect.offsetMin=Vector2.one*2;rect.offsetMax=-Vector2.one*2;
                var graphic=rect.gameObject.AddComponent<BoardTileGraphic>();graphic.raycastTarget=false;graphic.color=Owner.App.theme.warning;graphic.SetSymbol(symbol);
            }
        }
        void OnDestroy()
        {
            Cancel();if(analysis==null)return;Owner.DocumentChanged-=Changed;Owner.DocumentOpened-=Opened;Owner.App.NavigationRequested-=Navigated;
            foreach(string id in new[]{"AnalyzePack","RecheckWitness"})Owner.UnregisterOperation(id);
        }
    }
}
