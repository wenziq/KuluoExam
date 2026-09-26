using Sokoban.Domain.Workshop;
using Sokoban.Runtime.Presentation.Style;
using Sokoban.Runtime.Presentation.Views;
using UnityEngine;
using UnityEngine.UI;
namespace Sokoban.Runtime.Presentation.Workshop
{
    public sealed class PaintPaletteView
    {
        static readonly PaintTool[] tools={PaintTool.Floor,PaintTool.Wall,PaintTool.Player,PaintTool.Box,PaintTool.Goal,PaintTool.EraseEntity};
        readonly Button[] buttons=new Button[7];readonly WorkshopView view;
        public PaintPaletteView(RectTransform root,WorkshopView view)
        {
            this.view=view;string[] labels={"地板","墙","玩家","箱子","目标","橡皮","选择"};
            for(int i=0;i<7;i++)
            {
                int index=i;var b=UiFactory.Button("Tool"+i,root,labels[i],view.Owner.App.theme,()=>Choose(index));buttons[i]=b;UiFactory.Place((RectTransform)b.transform,6+i*48,5,45,53);
                var label=b.GetComponentInChildren<TMPro.TextMeshProUGUI>();label.fontSize=11;UiFactory.Place(label.rectTransform,2,29,41,22);
                var icon=UiFactory.Rect("ToolIcon",b.transform);UiFactory.Place(icon,12,4,21,24);
                if(i<5){var graphic=icon.gameObject.AddComponent<Board.BoardTileGraphic>();graphic.raycastTarget=false;Board.BoardSymbol[] symbols={Board.BoardSymbol.Floor,Board.BoardSymbol.Wall,Board.BoardSymbol.Player,Board.BoardSymbol.Box,Board.BoardSymbol.Goal};graphic.SetSymbol(symbols[i]);}
                else {var symbol=icon.gameObject.AddComponent<UiIcon>();symbol.kind=i==5?IconKind.Eraser:IconKind.Select;symbol.color=view.Owner.App.theme.text;symbol.raycastTarget=false;}
                view.Owner.App.Tooltip(b,labels[i]+" · "+(i<6?(i+1).ToString():"V"));
            }
            view.Owner.App.Menu(buttons[5],new Controls.MenuOption("实体橡皮","只删除玩家或箱子，保留地板和目标",()=>Set(PaintTool.EraseEntity)),new Controls.MenuOption("目标橡皮","只删除目标，保留地板和实体",()=>Set(PaintTool.EraseGoal)),new Controls.MenuOption("地形橡皮","墙恢复地板，保留其他层",()=>Set(PaintTool.EraseTerrain)));
        }
        public void Choose(int index)
        {
            view.CommitGesture();view.State.IsSelectionTool=index==6;
            if(index<6)view.State.Tool=tools[index];
            Refresh();view.RememberViewport();view.RefreshStatus();
        }
        void Set(PaintTool tool){view.CommitGesture();view.State.Tool=tool;view.State.IsSelectionTool=false;Refresh();view.RememberViewport();view.RefreshStatus();}
        public void Refresh()
        {
            for(int i=0;i<buttons.Length;i++)
            {
                bool selected;
                if(view.State.IsSelectionTool) selected=i==6;
                else selected=i<6&&tools[i]==view.State.Tool||i==5&&(int)view.State.Tool>=5;
                buttons[i].GetComponent<Image>().color=selected?UiTheme.Hex("415734"):UiTheme.Hex("252c22");
            }
        }
    }
}
