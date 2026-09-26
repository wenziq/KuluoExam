using System.Linq;
using Sokoban.Core.Data;
using Sokoban.Domain.Workshop;
using Sokoban.Runtime.Presentation.Style;
using TMPro;
using UnityEngine;
namespace Sokoban.Runtime.Presentation.Workshop
{
    public sealed class WorkshopStatusBar
    {
        readonly TextMeshProUGUI text;
        public WorkshopStatusBar(RectTransform root,UiTheme theme){text=UiFactory.Text("Status",root,"",theme,11,theme.secondary);UiFactory.Fill(text.rectTransform,16,0,16,0);}
        public void Refresh(WorkshopDocument document,LevelData level,LevelViewportState state,string hover,string persistenceStatus="",string applicationStatus="")
        {
            string[] names={"地板","墙","目标","玩家","箱子","实体橡皮","目标橡皮","地形橡皮"};
            string tool=state.IsSelectionTool?"选择":names[(int)state.Tool];
            string saved=document.IsDirty?"● 草稿未保存":"草稿已保存";
            text.text=(string.IsNullOrEmpty(persistenceStatus)||persistenceStatus==saved?"":persistenceStatus+"   ·   ")+saved+(string.IsNullOrEmpty(applicationStatus)?"":" · "+applicationStatus)+"    ·    "+tool+"    "+hover+"    |    箱子 "+(level?.entities.Count(e=>e.type==EntityType.Box)??0)+" / 目标 "+(level?.features.Count??0);
        }
    }
}
