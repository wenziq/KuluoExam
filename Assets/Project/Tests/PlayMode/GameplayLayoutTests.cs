using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Sokoban.Core.Data;
using Sokoban.Core.Rules;
using Sokoban.Domain.Gameplay;
using Sokoban.Runtime.Presentation.App;
using Sokoban.Runtime.Presentation.Style;
using Sokoban.Runtime.Presentation.Views;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Sokoban.PlayModeTests
{
    public sealed class GameplayLayoutTests
    {
        GameObject root;
        ApplicationController app;
        GameplayView view;
        uint oldWidth, oldHeight;

        [UnitySetUp] public IEnumerator Setup()
        {
            UnityEditor.PlayModeWindow.GetRenderingResolution(out oldWidth, out oldHeight);
            UnityEditor.PlayModeWindow.SetViewType(UnityEditor.PlayModeWindow.PlayModeViewTypes.GameView);
            UnityEditor.PlayModeWindow.SetCustomRenderingResolution(1280, 720, "Gameplay layout");
            yield return null;
            root = new GameObject("Isolated gameplay layout");
            app = root.AddComponent<ApplicationController>();
            app.theme = UnityEditor.AssetDatabase.LoadAssetAtPath<UiTheme>("Assets/Project/Settings/SokobanTheme.asset");
            app.Initialize();
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            UnityEngine.Object.Destroy(root);
            yield return null;
            if (oldWidth > 0 && oldHeight > 0)
                UnityEditor.PlayModeWindow.SetCustomRenderingResolution(oldWidth, oldHeight, "Previous");
        }

        void Show(int width, int height)
        {
            app.ClearPage();
            var rows = Enumerable.Range(0, height).Select(y => y == 0 || y == height - 1
                ? new string('#', width) : "#" + new string(' ', width - 2) + "#").ToArray();
            rows[height - 3] = "# @ $ ." + new string(' ', width - 8) + "#";
            var level = AsciiLevelFactory.Create(rows);
            level.name = width + " × " + height + " · 大地图游玩检查";
            level.designNotes = "先观察箱子和目标的位置，再选择推动方向。";
            var pack = new PackData { name = "布局检查" };
            pack.levels.Add(level); pack.levelOrder.Add(level.levelId);
            app.Navigate("Gameplay");
            view = UiFactory.Rect("Gameplay", app.pageRoot).gameObject.AddComponent<GameplayView>();
            view.Build(app, pack, level.levelId, new GameSession(level, SessionMode.Formal), () => app.Navigate("LevelSelect"));
        }

        Button Button(string name) => view.GetComponentsInChildren<Button>().Single(b => b.name == name);

        [UnityTest] public IEnumerator LargeBoardUsesTheMainAreaAndControlsStayVisible()
        {
            foreach (var size in new[] { new Vector2Int(1280, 720), new Vector2Int(1920, 1080), new Vector2Int(960, 560) })
            {
                UnityEditor.PlayModeWindow.SetCustomRenderingResolution((uint)size.x, (uint)size.y, "Gameplay layout");
                Show(20, 20); yield return null; yield return null; Canvas.ForceUpdateCanvases();
                Assert.That(view.Board.viewport.rect.width, Is.GreaterThan(Screen.width * .9f));
                Assert.That(view.Board.viewport.rect.height, Is.GreaterThan(Screen.height * .64f));
                Assert.That(view.Board.boardRoot.rect.height / 20, Is.GreaterThanOrEqualTo(Screen.height <= 560 ? 17 : 24));
                var title = view.GetComponentsInChildren<TMPro.TextMeshProUGUI>().Single(t => t.name == "LevelName");
                title.ForceMeshUpdate();
                Assert.That(title.textInfo.characterInfo.Take(title.textInfo.characterCount).Any(c => c.isVisible), Is.True, "Level name must render.");
                foreach (var button in view.GetComponentsInChildren<Button>())
                {
                    var corners = new Vector3[4]; ((RectTransform)button.transform).GetWorldCorners(corners);
                    Assert.That(corners.All(c => c.x >= 0 && c.x <= Screen.width + 1 && c.y >= 0 && c.y <= Screen.height + 1), Is.True, button.name + " " + size);
                }
            }
        }

        [UnityTest] public IEnumerator ZoomFitPlayerFocusAndNotesPreserveGameState()
        {
            Show(20, 20); yield return null; Canvas.ForceUpdateCanvases();
            Button("ZoomIn").onClick.Invoke();
            Assert.That(view.Board.Zoom, Is.GreaterThan(1));
            Button("FocusPlayer").onClick.Invoke();
            var player = view.Session.State.Player;
            var local = new Vector2(((player.x + .5f) / 20 - .5f) * view.Board.boardRoot.rect.width,
                ((player.y + .5f) / 20 - .5f) * view.Board.boardRoot.rect.height);
            Assert.That((local * view.Board.Zoom + view.Board.Pan).magnitude, Is.LessThan(.1f));
            Button("FitBoard").onClick.Invoke();
            Assert.That(view.Board.Zoom, Is.EqualTo(1)); Assert.That(view.Board.Pan, Is.EqualTo(Vector2.zero));
            view.Board.OnScroll(new PointerEventData(EventSystem.current) { scrollDelta = Vector2.up });
            Assert.That(view.Board.Zoom, Is.GreaterThan(1));
            var drag = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Middle, delta = new Vector2(80, 40) };
            view.Board.OnPointerDown(drag); view.Board.OnDrag(drag); view.Board.OnPointerUp(drag);
            Assert.That(view.Board.Pan.magnitude, Is.GreaterThan(0));
            Button("FitBoard").onClick.Invoke();
            Button("LevelNotes").onClick.Invoke();
            Assert.That(app.Modal.IsOpen, Is.True);
            Assert.That(app.Modal.Body.text, Does.Contain("先观察箱子"));
            view.RequestMove(Direction.Right);
            view.Board.OnScroll(new PointerEventData(EventSystem.current) { scrollDelta = Vector2.up });
            Assert.That(view.Session.Moves, Is.Zero); Assert.That(view.Board.Zoom, Is.EqualTo(1));
            app.Modal.Close(); view.RequestMove(Direction.Right);
            Assert.That(view.Session.Moves, Is.EqualTo(1));
            yield return new WaitForSecondsRealtime(.12f);
            Assert.That(view.Undo(), Is.True); Assert.That(view.Session.Moves, Is.Zero);
        }

        [UnityTest] public IEnumerator CaptureLargeBoards()
        {
            var output = Environment.GetEnvironmentVariable("SOKOBAN_GAMEPLAY_EVIDENCE_DIR");
            if (string.IsNullOrEmpty(output)) Assert.Ignore("Opt-in gameplay screenshots.");
            foreach (var size in new[] { new Vector2Int(1280, 720), new Vector2Int(1920, 1080), new Vector2Int(960, 560) })
            {
                UnityEditor.PlayModeWindow.SetCustomRenderingResolution((uint)size.x, (uint)size.y, "Gameplay evidence");
                foreach (var map in new[] { new Vector2Int(9, 7), new Vector2Int(20, 20), new Vector2Int(20, 8) })
                {
                    Show(map.x, map.y); yield return null;
                    yield return GameViewEvidence.Capture(Path.Combine(output, map.x + "x" + map.y + "-" + size.x + ".png"), size.x, size.y);
                }
            }
        }
    }
}
