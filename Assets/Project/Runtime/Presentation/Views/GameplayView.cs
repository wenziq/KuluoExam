using System;
using Sokoban.Runtime.Platform.Feedback;
using Sokoban.Core.Data;
using Sokoban.Core.Rules;
using Sokoban.Domain.Gameplay;
using Sokoban.Runtime.Presentation.App;
using Sokoban.Runtime.Presentation.Board;
using Sokoban.Runtime.Presentation.Style;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
namespace Sokoban.Runtime.Presentation.Views
{
    public sealed class GameplayView : PageView
    {
        public GameSession Session { get; private set; }
        public BoardView Board { get; private set; }
        public bool IsAnimating => animationRemaining > 0;
        public int PendingDirectionCount => pending.HasValue ? 1 : 0;
        public event Action CompletionCommitted;
        public event Action Completed;
        public event Action PauseRequested;
        public event Action RestartRequested;
        private TextMeshProUGUI counters,feedbackText,zoomText;
        private int shownZoom = -1;
        private const string MovementHint = "方向键 / WASD 移动 · Z 撤销 · R 重开";
        private float feedbackUntil;
        private FeedbackController feedback;
        private Button undo;
        private Direction? pending;
        private float animationRemaining;
        private BoardState animationFrom;
        private int handledCompletions;
        private int recordedCompletions;
        public bool HasPendingCompletion => Session != null && Session.Completions.Count > handledCompletions;
        public void Build(ApplicationController app, PackData pack, string levelId, GameSession session, Action back)
        {
            Begin(app);
            Session = session;
            var level = pack.levels.Find(l => l.levelId == levelId);
            var header = UiFactory.Rect("GameplayHeader", Root);
            header.anchorMin = new Vector2(0, 1); header.anchorMax = Vector2.one;
            header.pivot = new Vector2(.5f, 1); header.sizeDelta = new Vector2(0, 64);
            var row = header.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            row.padding = new RectOffset(16, 16, 4, 4); row.spacing = 16;
            row.childAlignment = TextAnchor.MiddleLeft;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = row.childForceExpandHeight = false;
            var backButton = UiFactory.Button("Back", header, session.Mode == SessionMode.Formal ? "← 返回选关" : "← 返回编辑", Theme, back);
            UiFactory.Preferred(backButton.gameObject, 36, 116);
            var heading = UiFactory.Rect("LevelHeading", header);
            var headingLayout = heading.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            headingLayout.flexibleWidth = 1; headingLayout.minWidth = 140; headingLayout.preferredHeight = 56;
            var mode = UiFactory.Text("Mode", heading,
                (session.Mode == SessionMode.Formal ? "正式闯关" : session.Mode == SessionMode.AssistedReplayTakeover ? "辅助试玩 · 已查看参考解" : "正在试玩草稿")
                + "   ·   " + (pack.levelOrder.IndexOf(levelId) + 1) + " / " + pack.levelOrder.Count, Theme, 12, Theme.accent);
            UiFactory.Fill(mode.rectTransform, 0, 36, 0, 0);
            mode.textWrappingMode = TextWrappingModes.NoWrap;
            var title = UiFactory.Text("LevelName", heading, level.name, Theme, 22);
            UiFactory.Fill(title.rectTransform, 0, 0, 0, 18);
            title.textWrappingMode = TextWrappingModes.NoWrap;
            counters = UiFactory.Text("Counters", header, "", Theme, 16);
            counters.alignment = TextAlignmentOptions.Right;
            counters.textWrappingMode = TextWrappingModes.NoWrap;
            UiFactory.Preferred(counters.gameObject, 36, 280);
            var notesButton = UiFactory.Button("LevelNotes", header, "关卡说明", Theme, () =>
                App.Modal.Show(level.name, (string.IsNullOrWhiteSpace(level.designNotes) ? "本关暂无额外笔记。" : level.designNotes)
                    + "\n\n● 玩家    ▣ 箱子    ◇ 目标\n\n方向键 / WASD 移动。推动也计入步数。\n撤销会恢复上一步的人物、箱子和计数。\n\n滚轮或 − / + 缩放；按住空格拖动，或用鼠标中键拖动平移。\n全图：显示整张地图。定位玩家：将人物移到视野中央。"));
            UiFactory.Preferred(notesButton.gameObject, 36, 92);
            App.Tooltip(notesButton, "查看关卡笔记、图例和操作说明");
            Board = BoardView.Create(Root);
            UiFactory.Fill((RectTransform)Board.transform, 16, 72, 16, 68);
            Board.fitPadding = new Vector2(16, 16);
            Board.maxCellSize = 86;
            Board.CanInteract = () => App.Input.CanMove;
            Board.Show(level);
            undo = Bottom("Undo", "撤销  Z", 16, 96, () => Undo());
            Bottom("Restart", "重开  R", 120, 88, RequestRestart);
            Bottom("Pause", "暂停  Esc", 216, 88, RequestPause);
            var hint = Label("Hint", MovementHint, 0, 0, 470, 22, 12, Theme.secondary);
            hint.rectTransform.anchorMin = hint.rectTransform.anchorMax = Vector2.zero;
            hint.rectTransform.pivot = Vector2.zero;
            hint.rectTransform.anchoredPosition = new Vector2(16, 4);
            feedbackText = hint; feedback = App.GetComponent<FeedbackController>();
            string[] arrows = { "↑", "←", "↓", "→" };
            Direction[] directions = { Direction.Up, Direction.Left, Direction.Down, Direction.Right };
            for (int i = 0; i < 4; i++)
            {
                var direction = directions[i];
                Bottom("Move" + direction, arrows[i], 320 + i * 42, 36, () => RequestMove(direction));
            }
            RightBottom("FitBoard", "全图", 16, 76, () => Board.Fit(), "显示整张地图并恢复居中");
            RightBottom("ZoomIn", "+", 100, 36, () => Board.SetView(Board.Zoom * 1.25f, Board.Pan), "放大地图，也可以使用鼠标滚轮");
            RightBottom("ZoomOut", "−", 144, 36, () => Board.SetView(Board.Zoom / 1.25f, Board.Pan), "缩小地图，也可以使用鼠标滚轮");
            RightBottom("FocusPlayer", "定位玩家", 188, 92, FocusPlayer, "保持当前缩放，将人物移到视野中央");
            zoomText = Label("MapNavigationHint", "", 0, 0, 390, 22, 12, Theme.secondary);
            zoomText.rectTransform.anchorMin = zoomText.rectTransform.anchorMax = new Vector2(1, 0);
            zoomText.rectTransform.pivot = new Vector2(1, 0);
            zoomText.rectTransform.anchoredPosition = new Vector2(-16, 4);
            zoomText.alignment = TextAlignmentOptions.Right;
            App.Input.MoveRequested += RequestMove;
            App.Input.Cleared += CancelAnimation;
            App.Input.EscapeRequested += OnEscape;
            Refresh();
        }
        private Button Bottom(string name, string text, float x, float width, Action action)
        {
            var button = Action(name, text, 0, 0, width, action);
            var r = (RectTransform)button.transform;
            r.anchorMin = r.anchorMax = Vector2.zero;
            r.pivot = Vector2.zero;
            r.anchoredPosition = new Vector2(x, 32);
            r.sizeDelta = new Vector2(width, 36);
            return button;
        }
        private void RightBottom(string name, string text, float right, float width, Action action, string tooltip)
        {
            var button = Bottom(name, text, 0, width, action);
            var rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(1, 0);
            rect.pivot = new Vector2(1, 0);
            rect.anchoredPosition = new Vector2(-right, 32);
            App.Tooltip(button, tooltip);
        }
        private void FocusPlayer()
        {
            var player = Session.State.Player;
            var size = Board.boardRoot.rect.size;
            var offset = new Vector2(((player.x + .5f) / Board.Width - .5f) * size.x,
                ((player.y + .5f) / Board.Height - .5f) * size.y);
            Board.SetView(Board.Zoom, -offset * Board.Zoom);
        }
        public void RequestMove(Direction direction)
        {
            if (Session == null || !App.Input.CanMove || Session.IsCompleted) return;
            if (IsAnimating) { pending = direction; return; }
            var previous = Session.State;
            var move = Session.TryMove(direction);
            if (!move.Succeeded){Respond(FeedbackKind.Invalid,"这一步被挡住了 · 可换个方向或撤销");return;}
            bool onGoal=false;if(move.Pushed)for(int i=0;i<Session.State.BoxCount;i++)if(Session.State.GetBoxId(i)==move.BoxId)onGoal=Session.State.IsGoal(Session.State.GetBoxPosition(i));
            Respond(onGoal?FeedbackKind.Goal:move.Pushed?FeedbackKind.Push:FeedbackKind.Move,onGoal?"箱子已进入目标":move.Pushed?"推动一步":"移动一步");
            animationFrom = previous;
            animationRemaining = .085f;
            Refresh();
            Board.InterpolateEntities(animationFrom, Session.State, 0);
            if (Session.IsCompleted) pending = null;
            RecordCompletionIfNeeded();
        }
        public bool Undo()
        {
            if (HasPendingCompletion) return false;
            App.Input.ClearPending();
            bool changed = Session.Undo();
            if(changed)Respond(FeedbackKind.Undo,"已撤销上一步");
            Refresh();
            return changed;
        }
        public void Restart()
        {
            if (HasPendingCompletion) return;
            App.Input.ClearPending();
            Session.Restart();
            Refresh();
            CheckCompletion();
        }
        public void CancelAnimation()
        {
            animationRemaining = 0;
            pending = null;
            if (Board != null && Session != null) Board.RefreshEntities(Session.State);
        }
        private void Refresh()
        {
            Board.RefreshEntities(Session.State);
            int placed = 0;
            for (int i = 0; i < Session.State.BoxCount; i++)
                if (Session.State.IsGoal(Session.State.GetBoxPosition(i))) placed++;
            counters.text = Session.Moves + " 步数   ·   " + Session.Pushes + " 推动   ·   " + placed + "/" + Session.State.BoxCount + " 到位";
            undo.interactable = Session.HistoryCount > 0;
        }
        private void RecordCompletionIfNeeded()
        {
            if (Session.Completions.Count <= recordedCompletions) return;
            recordedCompletions = Session.Completions.Count;
            CompletionCommitted?.Invoke();
        }
        private void CheckCompletion()
        {
            RecordCompletionIfNeeded();
            if (IsAnimating || App.Modal.IsOpen || Session.Completions.Count <= handledCompletions) return;
            handledCompletions = Session.Completions.Count;
            App.Input.ClearPending();
            Respond(FeedbackKind.Win,"全部箱子到位 · 本关完成");
            Completed?.Invoke();
        }
        private void Respond(FeedbackKind kind,string message)
        {
            if(feedback!=null&&!feedback.Emit(kind))return;
            if(feedbackText!=null){feedbackText.text=message;feedbackText.color=kind==FeedbackKind.Invalid?Theme.warning:Theme.accent;feedbackUntil=Time.unscaledTime+.85f;}
        }
        private void Update()
        {
            if (Session == null) return;
            if(feedbackText!=null&&feedbackUntil>0&&Time.unscaledTime>=feedbackUntil){feedbackText.text=MovementHint;feedbackText.color=Theme.secondary;feedbackUntil=0;}
            int zoom = Mathf.RoundToInt(Board.Zoom * 100);
            if (zoom != shownZoom) { shownZoom = zoom; zoomText.text = zoom + "% · 滚轮缩放 · 空格 + 拖动平移"; }
            CheckCompletion();
            if (App.Input.CanMove && Keyboard.current != null)
            {
                if (Keyboard.current.zKey.wasPressedThisFrame) { Undo(); return; }
                if (Keyboard.current.rKey.wasPressedThisFrame) { RequestRestart(); return; }
            }
            if (!IsAnimating) return;
            animationRemaining -= Time.unscaledDeltaTime;
            Board.InterpolateEntities(animationFrom, Session.State, 1 - animationRemaining / .085f);
            if (animationRemaining <= 0 && pending.HasValue)
            {
                var direction = pending.Value;
                pending = null;
                RequestMove(direction);
            }
        }
        private void RequestRestart() { if (!HasPendingCompletion) RestartRequested?.Invoke(); }
        private void RequestPause() { if (!HasPendingCompletion) PauseRequested?.Invoke(); }
        private void OnEscape() { if (App.Input.Context == InputContext.Gameplay) RequestPause(); }
        private void OnDisable()
        {
            CancelAnimation();
            if (App == null || App.Input == null) return;
            App.Input.MoveRequested -= RequestMove;
            App.Input.Cleared -= CancelAnimation;
            App.Input.EscapeRequested -= OnEscape;
        }
    }
}
