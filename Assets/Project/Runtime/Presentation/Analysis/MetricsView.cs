using System;
using System.Linq;
using Sokoban.Domain.Analysis;
using Sokoban.Runtime.Presentation.Style;
using UnityEngine;
namespace Sokoban.Runtime.Presentation.Analysis
{
    public static class MetricsView
    {
        private static readonly string[] Labels={"参考解推动次数 P","人物移动总步数 M","不推箱子的步数 W","换一个箱子推 S","把箱子推离目标 G"};
        private static readonly string[] Explanations={
            "箱子每被推动一格，计 1 次。只有已证明推动次数最少，才标为“最少推动”；否则显示当前参考解的推动次数。",
            "人物每移动一格，计 1 步，包括推箱子的步数。例如走 3 步、再推箱子 2 次，M = 5。",
            "人物没有推动箱子时走的步数，即 W = M − P。上面的例子中，W = 3。",
            "这次推的箱子与上一次推的不是同一个，就计 1 次。例如依次推 A、A、B、A，S = 2。中间走路不影响判断，第一次推箱子不计。",
            "箱子原本在目标点上，被推到非目标格，计 1 次。从一个目标点直接推到另一个目标点不计。这可能是在为其他箱子让路。"};
        private static readonly string[] Hints={"箱子每被推动一格，计 1 次","人物走路和推箱子的步数之和","只计算走路，没有推箱子的步数","相邻两次推动换了箱子，计 1 次；点击看步骤","把目标上的箱子推到非目标格；点击看步骤"};
        public static int Count=>Labels.Length;
        public static string Label(int index,bool optimal=false)=>index==0&&optimal?"最少推动次数 P":Labels[index];
        public static string Explanation(int index)=>Explanations[index];
        public static string Hint(int index)=>Hints[index];
        public static void ShowGuide(AnalysisMenuController controller)
        {
            string body="基于当前参考解；其他解可能不同。\n\n";
            for(int i=0;i<Labels.Length;i++)body+=Label(i)+"\n"+Explanation(i)+"\n\n";
            controller.Owner.App.Modal.Show("这些指标是什么意思？",body);
        }
        public static void ShowEvents(AnalysisMenuController controller,AnalysisReport report,string kind)
        {
            if(kind!="S"&&kind!="G")throw new ArgumentOutOfRangeException(nameof(kind));
            var app=controller.Owner.App;var metrics=report?.Metrics;
            if(metrics==null){app.Modal.Show("指标暂不可用","请先分析当前参考解的可玩性。");return;}
            Func<PushObservation,bool> match=kind=="G"?(Func<PushObservation,bool>)(e=>e.LeftGoal):e=>e.SwitchedBox;
            var events=metrics.Events.Where(match).ToArray();
            int index=kind=="G"?4:3;
            app.Modal.Show(Label(index),Explanation(index)+"\n\n"+(events.Length==0?"这条参考解未出现此事件；不能推断其他解也没有。":"点击下面的步骤，查看这条参考解中的具体推动。其他解可能不同。"));
            foreach(var observation in events)
            {
                var captured=observation;
                var button=UiFactory.Button("Event"+observation.PushIndex,app.Modal.Body.transform.parent,"第 "+observation.PushIndex+" 推 · 第 "+observation.MoveIndex+" 步 · ("+observation.From.x+","+observation.From.y+") → ("+observation.To.x+","+observation.To.y+")",app.theme,()=>controller.Production.OpenPlayback(report,captured.PushIndex));UiFactory.Preferred(button.gameObject,40);
            }
        }
    }
}
