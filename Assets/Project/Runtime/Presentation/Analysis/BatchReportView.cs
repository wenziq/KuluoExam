using System.Linq;
using System.Collections.Generic;
using Sokoban.Domain.Analysis;
namespace Sokoban.Runtime.Presentation.Analysis
{
    public static class BatchReportView
    {
        public static string Describe(BatchAnalysisQueue queue,IReadOnlyDictionary<string,AnalysisReport> reports=null)
        {
            int completed=queue.Entries.Count(e=>e.State==AnalysisJobState.Completed||e.State==AnalysisJobState.Failed||e.State==AnalysisJobState.Cancelled);
            return "已处理 "+completed+" / "+queue.Entries.Count+" 关 · 按当前顺序逐关检查\n取消会保留已完成结果。未判定不等于无解。\n\n"+string.Join("\n",queue.Entries.Select((e,i)=>(i+1)+". "+e.Name+" — "+Status(e)+Metrics(e,reports)));
        }
        static string Metrics(BatchAnalysisEntry entry,IReadOnlyDictionary<string,AnalysisReport> reports)
        {
            if(reports==null||entry.State!=AnalysisJobState.Completed||!reports.TryGetValue(entry.LevelId,out var report))return "";
            var m=report.Metrics;if(m==null)return " · 指标不可用";
            return "\n    参考解 P/M/W/S/G："+m.Pushes+" / "+m.Moves+" / "+m.Walks+" / "+m.BoxSwitches+" / "+m.GoalsLeft;
        }
        static string Status(BatchAnalysisEntry entry)
        {
            if(entry.State==AnalysisJobState.Running)return "正在分析";if(entry.State==AnalysisJobState.Queued)return "等待中";if(entry.State==AnalysisJobState.Cancelled)return "已取消";if(entry.State==AnalysisJobState.Failed)return "失败 · 未判定";
            return entry.Result?.Outcome==AnalysisOutcome.Solvable?"有解"+(entry.Result.FromCache?" · 复用当前证据":""):entry.Result?.Outcome==AnalysisOutcome.Unsolvable?"无解 · 可靠证明":entry.Result?.Outcome==AnalysisOutcome.Invalid?"结构待修复":"暂未判定";
        }
    }
}
