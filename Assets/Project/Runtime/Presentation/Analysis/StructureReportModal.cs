using System.Linq;
using Sokoban.Core.Data;
using Sokoban.Core.Validation;
using Sokoban.Runtime.Presentation.Style;
using TMPro;
namespace Sokoban.Runtime.Presentation.Analysis
{
    public static class StructureReportModal
    {
        public static void Show(AnalysisMenuController controller,LevelData root,ValidationReport report)
        {
            var owner=controller.Owner;var app=owner.App;var document=owner.Document;string hash=document.CurrentHash;
            int errors=report.Issues.Count(i=>i.Severity==IssueSeverity.Error),warnings=report.Issues.Count-errors;
            app.Modal.Show("遗漏项检查结果",root.name+"\n草稿结构快照 · "+root.width+" × "+root.height+"\n\n"+(errors>0?"发现 "+errors+" 项需要修复的问题。当前仍可保存草稿。":"必要内容已齐全；结构完整不等于有解。")+"\n设计提醒 "+warnings+" 项");
            foreach(var issue in report.Issues)
            {
                var captured=issue;
                var button=UiFactory.Button("定位_"+issue.Code,app.Modal.Body.transform.parent,(issue.Severity==IssueSeverity.Error?"需修复 · ":"提醒 · ")+issue.Message+"\n"+issue.Code+(issue.Position.HasValue?" · ("+issue.Position.Value.x+", "+issue.Position.Value.y+")":" · 点击返回问题面板"),app.theme,()=>
                {
                    if(!ReferenceEquals(owner.Document,document)||document.CurrentHash!=hash){app.Modal.Show("内容已改变","请重新检查当前草稿，再定位问题。");return;}
                    controller.Locate(captured);
                });
                UiFactory.Preferred(button.gameObject,78);var text=button.GetComponentInChildren<TextMeshProUGUI>();text.fontSize=12;text.textWrappingMode=TextWrappingModes.Normal;
            }
            var solve=app.Modal.AddAction("分析有解性",()=>controller.Analyze());solve.interactable=errors==0;
            if(errors>0)app.Tooltip(solve,"先修复必要结构问题；当前仍可保存草稿");
            var done=app.Modal.Actions.Find("Done");if(done!=null){done.name="关闭结果";done.GetComponentInChildren<TextMeshProUGUI>().text="关闭结果";}
        }
    }
}
