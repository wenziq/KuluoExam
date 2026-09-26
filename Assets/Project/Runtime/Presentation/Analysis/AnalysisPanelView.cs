using Sokoban.Runtime.Presentation.Style;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace Sokoban.Runtime.Presentation.Analysis
{
    public sealed class AnalysisPanelView
    {
        readonly AnalysisMenuController controller;
        readonly TextMeshProUGUI status,metrics,note;
        readonly Button solve,playability,replay,events,dead;
        public AnalysisPanelView(RectTransform root,AnalysisMenuController controller)
        {
            this.controller=controller;var app=controller.Owner.App;
            status=UiFactory.Text("AnalysisStatus",root,"尚未分析",app.theme,18,app.theme.accent);UiFactory.Preferred(status.gameObject,70);
            solve=UiFactory.Button("AnalyzeCurrent",root,"分析有解性",app.theme,()=>controller.Analyze(),true);UiFactory.Preferred(solve.gameObject,38);
            playability=UiFactory.Button("AnalyzePlayability",root,"分析可玩性",app.theme,()=>controller.Analyze(AnalysisPurpose.Playability));UiFactory.Preferred(playability.gameObject,38);
            replay=UiFactory.Button("ViewReference",root,"查看参考解",app.theme,()=>controller.Production.OpenPlayback(controller.SelectedReport));UiFactory.Preferred(replay.gameObject,36);
            events=UiFactory.Button("ReferenceEvents",root,"查看换箱、离开目标等步骤",app.theme,()=>ShowEvents());UiFactory.Preferred(events.gameObject,34);
            dead=UiFactory.Button("ToggleDeadCells",root,"显示经典静态死格",app.theme,()=>{controller.Production.ToggleDeadCells();Refresh();});UiFactory.Preferred(dead.gameObject,34);
            app.Tooltip(dead,"十字标记表示按当前地形无法把箱子推回任何目标的非目标地格；默认关闭");
            metrics=UiFactory.Text("ReferenceMetrics",root,"",app.theme,15);UiFactory.Preferred(metrics.gameObject,190);
            var guide=UiFactory.Button("MetricGuide",root,"这些指标是什么意思？",app.theme,()=>MetricsView.ShowGuide(controller));UiFactory.Preferred(guide.gameObject,34);
            note=UiFactory.Text("ReferenceCaveat",root,"",app.theme,12,app.theme.secondary);note.overflowMode=TextOverflowModes.Overflow;UiFactory.Preferred(note.gameObject,110);
            app.Tooltip(solve,"选择一关后，搜索从初始布局开始的通关路线");app.Tooltip(playability,"观察当前参考解的五项指标；不自动判断是否好玩");
            Refresh();
        }
        void ShowEvents()
        {
            var app=controller.Owner.App;app.Modal.Show("参考解事件","选择事件类型，查看真实推动序号。其他解可能不同。");
            foreach(string kind in new[]{"S","G"})
            {
                string selected=kind;int index=kind=="S"?3:4;
                var button=UiFactory.Button("Events"+kind,app.Modal.Body.transform.parent,MetricsView.Label(index),app.theme,()=>MetricsView.ShowEvents(controller,controller.SelectedReport,selected));UiFactory.Preferred(button.gameObject,38);
            }
        }
        public void Refresh()
        {
            status.text=controller.StatusText;var report=controller.SelectedReport;var m=report?.Metrics;
            bool selected=controller.Owner.Document?.SelectedLevelId!=null;solve.interactable=playability.interactable=selected;
            replay.interactable=report?.Evidence?.Witness!=null;events.interactable=m!=null;dead.interactable=selected;dead.GetComponentInChildren<TextMeshProUGUI>().text=controller.Production.DeadCellsVisible?"隐藏经典静态死格":"显示经典静态死格";
            string Value(int? value)=>value?.ToString()??"—";
            int?[] values={m?.Pushes??report?.Pushes,m?.Moves??report?.Moves,m?.Walks,m?.BoxSwitches,m?.GoalsLeft};
            metrics.text="";
            for(int i=0;i<MetricsView.Count;i++)metrics.text+=(i==0?"":"\n")+MetricsView.Label(i,report?.Evidence?.Optimality==Sokoban.Domain.Analysis.AnalysisOptimality.PushOptimal)+"    "+Value(values[i]);
            note.text=m==null?"尚无当前参考解指标。选择分析可玩性后查看；不可用不等于零。":"基于当前参考解；其他解可能不同。最少推动不等于最少行走，不生成综合难度分数。";
        }
    }
}
