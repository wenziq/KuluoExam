using NUnit.Framework;
using UnityEngine;
using Sokoban.Runtime.Presentation.App;
using Sokoban.Runtime.Presentation.Style;
using Sokoban.Runtime.Presentation.Board;
using Sokoban.Core.Data;
using Sokoban.Core.Rules;
using UnityEngine.EventSystems;
using System.Linq;
namespace Sokoban.PlayModeTests
{
    public class UiShellTests
    {
        [Test]
        public void RepeatedInitializationKeepsOneShellAndModalCapturesInput()
        {
#if UNITY_EDITOR
            var theme=UnityEditor.AssetDatabase.LoadAssetAtPath<UiTheme>("Assets/Project/Settings/SokobanTheme.asset");
            if(theme==null)Assert.Ignore("Run UiPrefabBuilder.EnsureAssets before asset integration checks.");
            var root=new GameObject("Shell test");
            try {
                var app=root.AddComponent<ApplicationController>();app.theme=theme;app.Initialize();app.Initialize();
                Assert.That(root.GetComponentsInChildren<Canvas>().Length,Is.EqualTo(1));
                Assert.That(Object.FindObjectsByType<EventSystem>().Length,Is.EqualTo(1));
                Assert.That(theme.font.fallbackFontAssetTable.Any(f=>f!=null),Is.True);
                var text=UiFactory.Text("User text",app.pageRoot,"<color=red>测试关卡</color>",theme);Assert.That(text.richText,Is.False);
                app.Input.SetContext(InputContext.Gameplay);app.Modal.Show("分析结果","等待关闭");Assert.That(app.Input.CanMove,Is.False);Assert.That(app.Modal.IsOpen,Is.True);
                app.Input.DispatchEscape();Assert.That(app.Modal.IsOpen,Is.False);Assert.That(app.Input.CanMove,Is.True);
            } finally {Object.DestroyImmediate(root);}
#endif
        }
        [Test]
        public void ModalBlocksUnderlyingSelectablesAndRestoresExistingGroupFlags()
        {
#if UNITY_EDITOR
            var theme = UnityEditor.AssetDatabase.LoadAssetAtPath<UiTheme>("Assets/Project/Settings/SokobanTheme.asset");
            if (theme == null) Assert.Ignore("Generate UI assets before asset integration tests.");
            var root = new GameObject("Modal focus ownership test");
            try
            {
                var app = root.AddComponent<ApplicationController>();
                app.theme = theme;
                app.Initialize();
                var pageButton = app.Button(app.pageRoot, "底层操作", () => { });
                var topButton = app.topbar.GetComponentInChildren<UnityEngine.UI.Button>();
                var nested = UiFactory.Rect("Independent group", app.pageRoot);
                var nestedGroup = nested.gameObject.AddComponent<CanvasGroup>();
                nestedGroup.ignoreParentGroups = true;
                var nestedButton = app.Button(nested, "嵌套操作", () => { });
                var pageGroup = app.pageRoot.gameObject.AddComponent<CanvasGroup>();
                pageGroup.alpha = .8f;
                pageGroup.blocksRaycasts = false;
                var menuGroup = app.menuLayer.gameObject.AddComponent<CanvasGroup>();
                menuGroup.interactable = false;
                var menuButton = app.Button(app.menuLayer, "原本禁用的菜单", () => { });
                Assert.That(pageButton.IsInteractable(), Is.True);
                app.Modal.Show("分析结果", "必须关闭窗口才能继续操作底层页面。");
                Assert.That(pageButton.IsInteractable(), Is.False);
                Assert.That(topButton.IsInteractable(), Is.False);
                Assert.That(nestedButton.IsInteractable(), Is.False);
                Assert.That(app.Modal.Actions.GetComponentInChildren<UnityEngine.UI.Button>().IsInteractable(), Is.True);
                app.Modal.Close();
                Assert.That(pageButton.IsInteractable(), Is.True);
                Assert.That(topButton.IsInteractable(), Is.True);
                Assert.That(menuButton.IsInteractable(), Is.False);
                Assert.That(nestedButton.IsInteractable(), Is.True);
                Assert.That(nestedGroup.ignoreParentGroups, Is.True);
                Assert.That(app.pageRoot.GetComponent<CanvasGroup>(), Is.SameAs(pageGroup));
                Assert.That(pageGroup.alpha, Is.EqualTo(.8f));
                Assert.That(pageGroup.blocksRaycasts, Is.False);
                // Disabling the host must also release its input ownership and UI gates.
                app.Modal.Show("第二次打开", "测试生命周期清理");
                app.Modal.gameObject.SetActive(false);
                Assert.That(pageButton.IsInteractable(), Is.True);
                Assert.That(topButton.IsInteractable(), Is.True);
                Assert.That(menuButton.IsInteractable(), Is.False);
            }
            finally { Object.DestroyImmediate(root); }
#endif
        }
        [Test]
        public void EntityRefreshKeepsTerrainObjects()
        {
            var root = new GameObject("Board test", typeof(RectTransform));
            try
            {
                var level = AsciiLevelFactory.Create(new[] { "#####", "#@$.#", "#   #", "#####" });
                var view = BoardView.Create(root.transform);
                view.Show(level);
                var terrainIds = view.terrainLayer.Cast<Transform>().ToArray();
                var state = new BoardState(level);
                var moved = SokobanRules.TryMove(state, Direction.Right);
                Assert.That(moved.Succeeded, Is.True);
                view.RefreshEntities(moved.State);
                Assert.That(view.terrainLayer.Cast<Transform>().ToArray(), Is.EqualTo(terrainIds));
            }
            finally { Object.DestroyImmediate(root); }
        }
        [Test]
        public void NewestHeldDirectionWinsAndClearRequiresRelease()
        {
            var savedSettings = UnityEngine.InputSystem.InputSystem.settings;
            var oldUpdateMode = savedSettings.updateMode;
            var oldBackgroundBehavior = savedSettings.backgroundBehavior;
#if UNITY_EDITOR
            var oldEditorInputBehavior = savedSettings.editorInputBehaviorInPlayMode;
#endif
            var isolatedSettings = savedSettings;
            isolatedSettings.updateMode = UnityEngine.InputSystem.InputSettings.UpdateMode.ProcessEventsManually;
            isolatedSettings.backgroundBehavior = UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            isolatedSettings.editorInputBehaviorInPlayMode = UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            var keyboard = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>();
            UnityEngine.InputSystem.InputSystem.EnableDevice(keyboard);
            var go = new GameObject("Keyboard routing test");
            try
            {
                var router = go.AddComponent<InputRouter>();
                var moves = new System.Collections.Generic.List<Direction>();
                router.MoveRequested += moves.Add;
                router.SetContext(InputContext.Gameplay);
                Sample();
                Sample(UnityEngine.InputSystem.Key.UpArrow);
                Sample(UnityEngine.InputSystem.Key.UpArrow, UnityEngine.InputSystem.Key.RightArrow);
                Assert.That(moves, Is.EqualTo(new[] { Direction.Up, Direction.Right }));
                router.ClearPending();
                Sample(UnityEngine.InputSystem.Key.UpArrow, UnityEngine.InputSystem.Key.RightArrow);
                Assert.That(moves.Count, Is.EqualTo(2));
                Sample();
                Sample(UnityEngine.InputSystem.Key.LeftArrow);
                Assert.That(moves.Last(), Is.EqualTo(Direction.Left));
            }
            finally
            {
                Object.DestroyImmediate(go);
                UnityEngine.InputSystem.InputSystem.RemoveDevice(keyboard);
                savedSettings.updateMode = oldUpdateMode;
                savedSettings.backgroundBehavior = oldBackgroundBehavior;
#if UNITY_EDITOR
                savedSettings.editorInputBehaviorInPlayMode = oldEditorInputBehavior;
#endif
            }
            void Sample(params UnityEngine.InputSystem.Key[] keys)
            {
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState(keys));
                UnityEngine.InputSystem.InputSystem.Update();
                Assert.That(UnityEngine.InputSystem.Keyboard.current, Is.SameAs(keyboard), "Synthetic keyboard must own this sample.");
                foreach (var key in keys) Assert.That(keyboard[key].isPressed, Is.True, "Queued key was not processed by the isolated input settings.");
                go.SendMessage("Update");
            }
        }
        [Test]
        public void HighestOverlayConsumesEscapeAndRestoresPreviousContext()
        {
            var go = new GameObject("Input priority test");
            try
            {
                var router = go.AddComponent<InputRouter>();
                router.SetContext(InputContext.Gameplay);
                var menu = new object();
                var modal = new object();
                int page = 0, menuCalls = 0, modalCalls = 0;
                router.EscapeRequested += () => page++;
                router.Capture(menu, InputContext.MenuOrGesture);
                router.SetEscapeHandler(menu, () => { menuCalls++; router.Release(menu); });
                router.Capture(modal, InputContext.Modal);
                router.SetEscapeHandler(modal, () => { modalCalls++; router.Release(modal); });
                router.DispatchEscape();
                Assert.That(modalCalls, Is.EqualTo(1));
                Assert.That(menuCalls, Is.Zero);
                Assert.That(page, Is.Zero);
                Assert.That(router.Context, Is.EqualTo(InputContext.MenuOrGesture));
                router.DispatchEscape();
                Assert.That(menuCalls, Is.EqualTo(1));
                Assert.That(router.CanMove, Is.True);
                Assert.That(page, Is.Zero);
            }
            finally { Object.DestroyImmediate(go); }
        }
        [Test]
        public void OverlayAndPageTransitionsClearHeldGameplay()
        {
            var go = new GameObject("Input routing test");
            try
            {
                var router = go.AddComponent<InputRouter>();
                int cleared = 0;
                router.Cleared += () => cleared++;
                router.SetContext(InputContext.Gameplay);
                Assert.That(router.CanMove, Is.True);
                router.SetContext(InputContext.Modal);
                Assert.That(router.CanMove, Is.False);
                router.SetContext(InputContext.Text);
                Assert.That(router.CanMove, Is.False);
                Assert.That(cleared, Is.GreaterThanOrEqualTo(2));
                router.ClearPending();
                Assert.That(cleared, Is.GreaterThanOrEqualTo(3));
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
