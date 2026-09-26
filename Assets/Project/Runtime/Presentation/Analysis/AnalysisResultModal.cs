using System;
using Sokoban.Domain.Analysis;
using Sokoban.Runtime.Presentation.Style;
using Sokoban.Runtime.Presentation.Controls;
using Sokoban.Runtime.Presentation.Workshop;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace Sokoban.Runtime.Presentation.Analysis
{
    public sealed class AnalysisResultModal:MonoBehaviour
    {
        AnalysisMenuController controller;
        TextMeshProUGUI marker,progressText;
        AnalysisJobState renderedState=AnalysisJobState.Idle;
        bool renderedCurrent;
        public bool IsCurrent=>controller!=null&&controller.Owner.App.Modal.IsOpen&&controller.Owner.App.Modal.Body==marker&&gameObject.activeInHierarchy;
        public static AnalysisResultModal Open(AnalysisMenuController controller,AnalysisReport report,Action closed)
        {
            var app=controller.Owner.App;
            app.Modal.Show(report.Purpose==AnalysisPurpose.Playability?"可玩性分析":"有解性分析","",closed);
            var marker=app.Modal.Body;marker.gameObject.SetActive(false);
            var prefab=Resources.Load<GameObject>("UI/Pages/AnalysisResult");
            var go=prefab!=null?Instantiate(prefab,marker.transform.parent):UiFactory.Rect("AnalysisResult",marker.transform.parent).gameObject;
            var view=go.GetComponent<AnalysisResultModal>()??go.AddComponent<AnalysisResultModal>();
            var layout=go.GetComponent<VerticalLayoutGroup>()??go.AddComponent<VerticalLayoutGroup>();layout.spacing=12;layout.childControlWidth=true;layout.childControlHeight=true;layout.childForceExpandHeight=false;
            view.controller=controller;view.marker=marker;view.Render(report);return view;
        }
        TextMeshProUGUI Text(string name,string value,float size=14,Color? color=null,float minHeight=32)
        {
            var text=UiFactory.Text(name,transform,value,controller.Owner.App.theme,size,color);text.overflowMode=TextOverflowModes.Overflow;
            UiFactory.Preferred(text.gameObject,Mathf.Max(minHeight,text.GetPreferredValues(value,540,0).y+8));return text;
        }
        public void Render(AnalysisReport report)
        {
            if(!IsCurrent)return;
            bool running=report.State==AnalysisJobState.Queued||report.State==AnalysisJobState.Running;
            if(running&&renderedState==report.State&&progressText!=null&&renderedCurrent==report.IsCurrent)
            {progressText.text=Progress(report);return;}
            renderedState=report.State;renderedCurrent=report.IsCurrent;var app=controller.Owner.App;var theme=app.theme;
            WorkshopFields.Clear(transform);WorkshopFields.Clear(app.Modal.Actions);
            Text("Snapshot",report.Root.name+"\n"+report.Root.width+" × "+report.Root.height+" · 草稿快照 · "+report.CapturedUtc.ToLocalTime().ToString("HH:mm:ss"),13,theme.secondary,48);
            if(!report.IsCurrent)Text("Stale","布局或当前关卡已改变。这份报告已过期，只供查看。",14,theme.warning,52);
            if(running)
            {
                Text("Outcome","正在探索通关路线…",23,theme.accent,45);progressText=Text("Progress",Progress(report),15,null,72);
                Text("RunningHint","结果会保留在这个窗口中。关闭窗口会取消本次计算；尚未找到答案不等于无解。",14,theme.secondary,90);
                app.Modal.AddAction("取消分析",()=>controller.Job.Cancel());app.Modal.AddAction("关闭窗口",app.Modal.Close,false);return;
            }
            progressText=null;
            var result=report.Result;bool hasEvidence=report.Evidence!=null;
            bool budgetReached=result?.Outcome==AnalysisOutcome.Unknown&&report.State==AnalysisJobState.Completed
                &&(result.StopReason==AnalysisStopReason.TimeBudget||result.StopReason==AnalysisStopReason.NodeBudget||result.StopReason==AnalysisStopReason.MemoryBudget);
            bool canExtend=report.IsCurrent&&!hasEvidence&&budgetReached&&!report.UsedExtendedSearch;
            string headline=Headline(report);
            Text("Outcome",headline,23,result?.Outcome==AnalysisOutcome.Unsolvable?theme.warning:hasEvidence?theme.accent:theme.text,48);
            Text("Explanation",result?.Explanation??"当前还没有可发布的结论。",14,theme.secondary,62);
            if(!hasEvidence&&budgetReached)
                Text("SearchLimitHint",report.UsedExtendedSearch
                    ?"延长搜索后仍未找到答案，这不代表无解。可以调整关卡，或亲自试玩通关来建立有解证据。"
                    :"本次搜索达到限制，暂时无法判断是否有解。可以延长搜索时间重新尝试，最多搜索 10 秒；仍不保证找到答案。",14,theme.secondary,78);
            if(hasEvidence&&result?.Outcome!=AnalysisOutcome.Solvable)Text("RetainedProof","此前的当前有效参考解仍可使用；本次取消或失败不会抹去有解证据。",14,theme.accent,70);
            if(hasEvidence)
            {
                Text("Optimality",report.Evidence.Optimality==AnalysisOptimality.PushOptimal?"最少推动已证明 · 不代表最少行走":"当前参考解已验证 · 最优性未证明",14,null,38);
                DrawMetrics(report,report.Purpose==AnalysisPurpose.Playability);
                if(report.Metrics!=null)Text("MetricScope","基于当前参考解；其他解可能不同。\n这些数值观察操作负担与多箱配合，不生成趣味性分数或自动难度评级。",13,theme.secondary,84);
                else Text("MetricsUnavailable",report.MetricsMessage??"部分指标尚未计算，可通过分析可玩性查看。",13,theme.secondary,60);
            }
            else Text("MetricsUnavailable","当前没有有效参考解，依赖路径的指标不可用。",15,theme.secondary,70);
            if(report.Purpose==AnalysisPurpose.Playability&&!running)
            {
                Text("MetricHelpHeading","这些指标是什么意思？",18,null,40);
                for(int i=0;i<MetricsView.Count;i++)Text("MetricHelp"+i,"【"+MetricsView.Label(i,report.Evidence?.Optimality==AnalysisOptimality.PushOptimal)+"】\n"+MetricsView.Explanation(i),14,null,50);
                Text("MetricHelpScope","五项指标均基于当前参考解；其他解可能不同。S / G 统计发生次数，点击对应卡片可查看具体步骤。",13,theme.secondary,60);
            }
            if(result?.Outcome==AnalysisOutcome.Unsolvable&&result.StopReason!=AnalysisStopReason.StaticDeadlock)
                Text("NoLocalCause","未生成可定位的局部原因；不能把某一堵墙当成唯一原因。",13,theme.secondary,62);
            if(result!=null)
            {
                var details=UiFactory.Button("TechnicalDetails",transform,"分析技术详情",theme);UiFactory.Preferred(details.gameObject,32);
                var technical=Text("Technical",(result.FromCache?"历史缓存数据":"本次搜索数据")+"\n扩展状态 "+result.ExpandedNodes+" · 耗时 "+result.ElapsedMilliseconds+" ms\n搜索存储估计 "+(result.EstimatedPeakBytes/1048576d).ToString("0.0")+" MiB\n"+PushAStarSolver.AlgorithmVersion+"\n计算量不能直接解释为玩家难度。",12,theme.secondary,104);
                technical.gameObject.SetActive(false);details.onClick.AddListener(()=>technical.gameObject.SetActive(!technical.gameObject.activeSelf));
            }
            app.Modal.AddAction("关闭结果",app.Modal.Close,false);
            if(report.IsCurrent)
            {
                if(report.State==AnalysisJobState.Failed)app.Modal.AddAction("重试分析",()=>controller.Analyze(report.Purpose));
                else if(!hasEvidence)
                {
                    if(result?.StopReason==AnalysisStopReason.StaticDeadlock&&result.ProblemCell.HasValue)
                        app.Modal.AddAction("定位死格",()=>{app.Modal.Close();controller.Owner.Select(report.Root.levelId);controller.Owner.View.Palette.Choose(6);controller.Owner.View.SelectCell(result.ProblemCell.Value.x,result.ProblemCell.Value.y);controller.Owner.View.SetTab(0);});
                    else if(canExtend)app.Modal.AddAction("延长搜索时间，再试一次",()=>controller.Analyze(report.Purpose,true));
                }
                else
                {
                    if(report.Purpose!=AnalysisPurpose.Playability)app.Modal.AddAction("查看可玩性",()=>controller.Analyze(AnalysisPurpose.Playability));
                    app.Modal.AddAction("查看参考解",()=>controller.Production.OpenPlayback(report));
                    app.Modal.AddAction("试玩当前",()=>{app.Modal.Close();controller.Owner.StartTrial(false);});
                }
            }
        }
        static string Headline(AnalysisReport report)
        {
            if(report.State==AnalysisJobState.Failed)return "分析未完成";
            if(report.State==AnalysisJobState.Cancelled)return "本次分析已取消";
            if(report.Result?.Outcome==AnalysisOutcome.Solvable)return "已找到通关路线";
            if(report.Result?.Outcome==AnalysisOutcome.Unsolvable)return "已证明无解";
            return report.Evidence!=null?"有解证据保留 · 本次暂未判定":"暂未判定";
        }
        static string Progress(AnalysisReport report)=>report.Progress==null?"正在准备不可变地图快照…":"已探索 "+report.Progress.ExpandedNodes+" 个状态\n已用时 "+report.Progress.ElapsedMilliseconds+" ms\n搜索总量未知，不显示完成百分比。";
        void DrawMetrics(AnalysisReport report,bool all)
        {
            var m=report.Metrics;int?[] values={m?.Pushes??report.Pushes,m?.Moves??report.Moves,m?.Walks,m?.BoxSwitches,m?.GoalsLeft};
            for(int i=0;i<(all?MetricsView.Count:2);i+=2)
            {
                var row=UiFactory.Rect("MetricsRow",transform);UiFactory.Preferred(row.gameObject,82);var horizontal=row.gameObject.AddComponent<HorizontalLayoutGroup>();horizontal.spacing=10;horizontal.childControlWidth=true;horizontal.childForceExpandWidth=true;horizontal.childControlHeight=true;
                for(int j=i;j<Math.Min(i+2,all?MetricsView.Count:2);j++)
                {
                    var card=UiFactory.Panel("Metric"+j,row,controller.Owner.App.theme.raised);UiFactory.Round(card);
                    card.raycastTarget=true;
                    var tooltip=card.gameObject.AddComponent<TooltipTarget>();tooltip.host=controller.Owner.App.Tooltips;tooltip.explanation=MetricsView.Hint(j);
                    if(j>=3&&m!=null){string kind=j==3?"S":"G";var button=card.gameObject.AddComponent<UnityEngine.UI.Button>();button.targetGraphic=card;button.onClick.AddListener(()=>MetricsView.ShowEvents(controller,report,kind));}
                    var value=UiFactory.Text("Value",card.transform,values[j]?.ToString()??"—",controller.Owner.App.theme,26,controller.Owner.App.theme.accent);UiFactory.Place(value.rectTransform,14,5,205,40);
                    var label=UiFactory.Text("MetricName",card.transform,MetricsView.Label(j,report.Evidence.Optimality==AnalysisOptimality.PushOptimal),controller.Owner.App.theme,12);UiFactory.Place(label.rectTransform,14,46,225,27);
                }
            }
        }
    }
}
