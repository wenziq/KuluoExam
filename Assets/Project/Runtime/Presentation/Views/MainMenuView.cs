using System;
using Sokoban.Core.Data;
using Sokoban.Domain.Gameplay;
using Sokoban.Runtime.Presentation.App;
using Sokoban.Runtime.Presentation.Board;
using Sokoban.Runtime.Presentation.Style;
using UnityEngine;
namespace Sokoban.Runtime.Presentation.Views
{
    public sealed class MainMenuView : PageView
    {
        public void Build(ApplicationController app, PackData pack, ProgressService progress, Action start)
        {
            Begin(app);
            Label("Eyebrow", "●  为每一次恰到好处的推动", 44, 17, 550, 28, 11, Theme.accent);
            var titleLabel = Label("Title", "推箱子", 44, 63, 540, 70, 48);
            titleLabel.overflowMode = TMPro.TextOverflowModes.Overflow;
            titleLabel.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            Label("Subtitle", "关卡制作与试玩", 44, 125, 570, 70, 44, Theme.accent);
            Label("Description", "从一个箱子开始，发现空间里的可能。\n也可以走进工坊，设计属于你的下一道谜题。", 44, 203, 540, 62, 16, Theme.secondary);
            int complete = 0;
            if (pack != null)
                foreach (var level in pack.levels)
                    if (progress.Best(pack.packId, level) != null) complete++;
            string title = complete == 0 ? "开始闯关  →" : complete == pack?.levels.Count ? "再玩一次  →" : "继续闯关  →";
            Action("StartGame", title, 44, 293, 132, start, true).interactable = pack != null;
            Action("Workshop", "进入关卡工坊  →", 188, 293, 160, () => app.Navigate("Workshop"));
            Label("CurrentPack", pack == null ? "暂无可玩关卡集" : "当前关卡集   " + pack.name + "     " + complete + " / " + pack.levels.Count + " 已完成", 44, 368, 555, 48, 13, Theme.secondary);
            if (pack != null && pack.levels.Count > 0)
            {
                var board = BoardView.Create(Root);
                var rect = (RectTransform)board.transform;
                rect.anchorMin = new Vector2(.55f, .32f);
                rect.anchorMax = new Vector2(.96f, .91f);
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                board.fitPadding = new Vector2(48, 48);
                rect.localRotation=Quaternion.Euler(0,0,5);
                board.Show(pack.levels[0]);
            }
            var row = UiFactory.Rect("Features", Root);
            row.anchorMin = new Vector2(0, 0);
            row.anchorMax = new Vector2(1, 0);
            row.offsetMin = new Vector2(44, 55);
            row.offsetMax = new Vector2(-44, 195);
            var layout = row.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            layout.spacing = 16;
            layout.childControlHeight = layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
            Add("01","发现关卡","从第一次推动到多箱配合","选择关卡集 ↗", () => app.Navigate("PackLibrary"));
            Add("02","把想法画下来","绘制、编排、随时试玩","打开关卡工坊 ↗", () => app.Navigate("Workshop"));
            Add("03","交换你的谜题","导入一份关卡包，开始探索","导入关卡集 ↗", () => app.Command("ImportPack"));
            var exit = Action("Exit", "退出", 0, 0, 88, () => app.Command("Exit"));
            var er = (RectTransform)exit.transform;
            er.anchorMin = er.anchorMax = new Vector2(1, 0);
            er.pivot = new Vector2(1, 0);
            er.anchoredPosition = new Vector2(-52, 12);
            void Add(string number,string heading,string description,string action,Action callback)
            {
                var button=UiFactory.Button("Feature"+number,row,"",Theme,callback);
                button.GetComponent<UnityEngine.UI.Image>().color=Color.clear;
                var outline = button.GetComponent<UnityEngine.UI.Outline>();
                if (Application.isPlaying) UnityEngine.Object.Destroy(outline);
                else UnityEngine.Object.DestroyImmediate(outline);
                UiFactory.Preferred(button.gameObject,140,300);
                var line=UiFactory.Panel("Divider",button.transform,Theme.border).rectTransform;UiFactory.Place(line,0,0,300,1);line.anchorMax=new Vector2(1,1);line.sizeDelta=new Vector2(0,1);
                var title=UiFactory.Text("Heading",button.transform,heading,Theme,16);UiFactory.Place(title.rectTransform,8,35,300,30);
                var note=UiFactory.Text("Description",button.transform,description,Theme,12,Theme.secondary);UiFactory.Place(note.rectTransform,8,68,300,26);
                var link=UiFactory.Text("Action",button.transform,action,Theme,12,Theme.accent);UiFactory.Place(link.rectTransform,8,106,300,26);
                var index=UiFactory.Text("Number",button.transform,number,Theme,10,Theme.secondary);UiFactory.Place(index.rectTransform,8,8,50,20);
            }
        }
    }
}
