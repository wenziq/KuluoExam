using Sokoban.Core.Validation;
using Sokoban.Runtime.Presentation.Style;
using UnityEngine;
namespace Sokoban.Runtime.Presentation.Workshop
{
    public sealed class IssueListView
    {
        readonly RectTransform root;readonly WorkshopApplicationCoordinator owner;
        public IssueListView(RectTransform root,WorkshopApplicationCoordinator owner){this.root=root;this.owner=owner;}
        public void Refresh(ValidationReport report)
        {
            WorkshopFields.Clear(root);
            if(report.Issues.Count==0){var good=UiFactory.Text("StructureClear",root,"结构检查通过\n有解性需要分析或实际通关验证。",owner.App.theme,14);UiFactory.Preferred(good.gameObject,100);return;}
            foreach(var issue in report.Issues)
            {
                var item=issue;var button=UiFactory.Button("Issue",root,(item.Severity==IssueSeverity.Error?"! ":"△ ")+item.Message,owner.App.theme,()=>
                {
                    if(item.LevelId!=null)owner.Select(item.LevelId);
                    if(item.Position.HasValue)owner.View.SelectCell(item.Position.Value.x,item.Position.Value.y);
                });UiFactory.Preferred(button.gameObject,70);
            }
        }
    }
}
