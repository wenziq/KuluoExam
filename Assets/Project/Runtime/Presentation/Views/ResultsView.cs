using System;
using Sokoban.Domain.Gameplay;
using Sokoban.Runtime.Presentation.App;
namespace Sokoban.Runtime.Presentation.Views
{
    public sealed class ResultsView
    {
        private readonly ApplicationController app;
        private readonly string summary;
        private readonly TMPro.TextMeshProUGUI body;
        private UnityEngine.UI.Button retryButton;
        public ResultsView(ApplicationController app, GameSession session, bool wholePackComplete, Action next, Action replay, Action back, Action undo)
        {
            this.app = app;
            summary = (session.Moves == 0 ? "初始已完成\n\n" : "每个箱子，都找到了自己的位置。\n\n") + session.Pushes + " 次推动  ·  " + session.Moves + " 步\n\n";
            app.Modal.Show(wholePackComplete ? "关卡集完成" : "关卡完成", summary + "正在保存成绩…");
            body = app.Modal.Body;
            var done = app.Modal.Actions.Find("Done");
            if (done != null) { done.gameObject.SetActive(false); UnityEngine.Object.Destroy(done.gameObject); }
            if (next != null) app.Modal.AddAction("下一关 →", next);
            app.Modal.AddAction("重玩", replay, false);
            app.Modal.AddAction("返回选关", back, false);
            if (session.HistoryCount > 0) app.Modal.AddAction("撤销最后一步", undo, false);
        }
        public bool IsCurrent => app.Modal.IsOpen && app.Modal.Body == body;
        public void SetSaveState(bool saved, string error, Action retry)
        {
            if (!app.Modal.IsOpen || app.Modal.Body != body) return;
            app.Modal.SetMessage(summary + (saved ? "正式成绩已保存。" : "成绩保存失败，当前完成记录仍保留。\n" + error));
            if (!saved && retryButton == null) retryButton = app.Modal.AddAction("重试保存", retry);
            if (retryButton != null) retryButton.gameObject.SetActive(!saved);
            foreach (var button in app.Modal.Actions.GetComponentsInChildren<UnityEngine.UI.Button>())
                Sokoban.Runtime.Presentation.Style.UiFactory.Preferred(button.gameObject, 36, 104);
        }
    }
}
