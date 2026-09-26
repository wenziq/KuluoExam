using System;
using Sokoban.Core.Data;
using Sokoban.Domain.Gameplay;
using Sokoban.Runtime.Presentation.App;
using Sokoban.Runtime.Presentation.Board;
using Sokoban.Runtime.Presentation.Style;
using UnityEngine;
namespace Sokoban.Runtime.Presentation.Views
{
    public sealed class LevelSelectView : PageView
    {
        public void Build(ApplicationController app, PackData pack, ProgressService progress, Action<string> play)
        {
            Begin(app);
            PageHeading("", pack.name, pack.description);
            Action("Back", "← 关卡集", 40, 12, 135, () => app.Navigate("PackLibrary"));

            var content = ScrollBody(166,24,304);
            for (int i = 0; i < pack.levelOrder.Count; i++)
            {
                string id = pack.levelOrder[i];
                var level = pack.levels.Find(x => x.levelId == id);
                var best = progress.Best(pack.packId, level);
                bool open = UnlockPolicyEvaluator.CanEnter(pack, id, progress);
                var card = UiFactory.Button("Level_" + id, content, "", Theme, () => { if (open) play(id); });
                card.interactable=open;
                var number=UiFactory.Text("Number",card.transform,(i+1).ToString("00"),Theme,11,Theme.secondary);UiFactory.Place(number.rectTransform,18,14,70,28);
                var board = BoardView.Create(card.transform);
                var br=(RectTransform)board.transform;UiFactory.Fill(br,18,86,18,52);
                board.fitPadding = new Vector2(28,28);board.maxCellSize=20;
                board.Show(level);
                var name = UiFactory.Text("Name", card.transform, level.name, Theme, 18);
                UiFactory.Fill(name.rectTransform,18,43,18,230);
                var state = UiFactory.Text("Status", card.transform, best != null ? "✓ 已完成   " + best.pushes + " 推动 · " + best.moves + " 步" : open ? "可挑战  →" : "尚未解锁 · 完成前面的关卡后解锁", Theme, 14, best != null ? Theme.accent : Theme.secondary);
                UiFactory.Fill(state.rectTransform,18,12,18,269);
                app.Tooltip(card, level.name + (open ? "" : "：完成前面的关卡后解锁"));
            }
        }
    }
}
