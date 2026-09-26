using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Sokoban.Core.Data;
using Sokoban.Core.Rules;
using Sokoban.Domain.Gameplay;
using Sokoban.Runtime.Fixtures;
using Sokoban.Runtime.Persistence;
using Sokoban.Runtime.Presentation.App;
using Sokoban.Runtime.Presentation.Style;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
namespace Sokoban.PlayModeTests
{
    public sealed class GameFlowTests
    {
        private GameObject root;
        private string directory;
        private GameApplicationCoordinator coordinator;
        private ControlledFiles files;
#if UNITY_EDITOR
        private uint previousWidth, previousHeight;
#endif
        [UnitySetUp] public IEnumerator Setup()
        {
#if UNITY_EDITOR
            UnityEditor.PlayModeWindow.GetRenderingResolution(out previousWidth, out previousHeight);
            UnityEditor.PlayModeWindow.SetViewType(UnityEditor.PlayModeWindow.PlayModeViewTypes.GameView);
            UnityEditor.PlayModeWindow.SetCustomRenderingResolution(1280, 720, "U7 test fixture");
            yield return null;
#endif
            directory = Path.Combine(Path.GetTempPath(), "sokoban-u7-play-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(directory, "builtins"));
            File.WriteAllBytes(Path.Combine(directory, "builtins", "fixture.sokopack.json"), StrictPackJson.Serialize(PlayableFlowFixture.Create()));
            root = new GameObject("U7 isolated flow");
            var app = root.AddComponent<ApplicationController>();
#if UNITY_EDITOR
            app.theme = UnityEditor.AssetDatabase.LoadAssetAtPath<UiTheme>("Assets/Project/Settings/SokobanTheme.asset");
#endif
            Assert.That(app.theme, Is.Not.Null);
            app.Initialize();
            coordinator = root.AddComponent<GameApplicationCoordinator>();
            files = new ControlledFiles();
            coordinator.Initialize(app, Path.Combine(directory, "data"), Path.Combine(directory, "builtins"), files);
            yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            files?.ReleaseFlush();
            if (coordinator != null) while (!coordinator.LastSave.IsCompleted) yield return null;
            UnityEngine.Object.Destroy(root);
            yield return null;
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
#if UNITY_EDITOR
            if (previousWidth > 0 && previousHeight > 0) UnityEditor.PlayModeWindow.SetCustomRenderingResolution(previousWidth, previousHeight, "Previous");
#endif
        }
#if UNITY_EDITOR
        [UnityTest] public IEnumerator CapturePlayablePages()
        {
            string output = Environment.GetEnvironmentVariable("SOKOBAN_U7_EVIDENCE_DIR");
            if (string.IsNullOrEmpty(output)) Assert.Ignore("Opt-in graphical evidence needs an explicit output directory.");
            UnityEditor.PlayModeWindow.GetRenderingResolution(out uint oldWidth, out uint oldHeight);
            uint width = uint.TryParse(Environment.GetEnvironmentVariable("SOKOBAN_UI_WIDTH"), out var w) ? w : 1280;
            uint height = uint.TryParse(Environment.GetEnvironmentVariable("SOKOBAN_UI_HEIGHT"), out var h) ? h : 720;
            UnityEditor.PlayModeWindow.SetViewType(UnityEditor.PlayModeWindow.PlayModeViewTypes.GameView);
            UnityEditor.PlayModeWindow.SetCustomRenderingResolution(width, height, "U7 pages");
            Directory.CreateDirectory(output);
            try
            {
                yield return null;
                var title = coordinator.App.pageRoot.GetComponentsInChildren<TMPro.TextMeshProUGUI>().Single(t => t.name == "Title");
                title.ForceMeshUpdate();
                Assert.That(title.textInfo.characterInfo.Take(title.textInfo.characterCount).Count(c => c.isVisible), Is.GreaterThan(0), "Home title must actually render.");
                yield return Capture("home");
                coordinator.App.Navigate("PackLibrary");
                yield return Capture("library");
                coordinator.SelectPack(coordinator.Catalog.Entries[0].Pack);
                yield return Capture("select");
                coordinator.StartLevel("A");
                yield return Capture("game");
                coordinator.Gameplay.RequestMove(Direction.Right);
                yield return new WaitForSecondsRealtime(.15f);
                while (!coordinator.LastSave.IsCompleted) yield return null;
                yield return Capture("result");
            }
            finally
            {
                if (oldWidth > 0 && oldHeight > 0) UnityEditor.PlayModeWindow.SetCustomRenderingResolution(oldWidth, oldHeight, "Previous");
            }
            IEnumerator Capture(string state)
            {
                Canvas.ForceUpdateCanvases();
                var type = typeof(UnityEditor.EditorWindow).Assembly.GetType("UnityEditor.PlayModeView");
                var flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
                var method = type.GetMethod("GetMainPlayModeView", flags | System.Reflection.BindingFlags.Static);
                var field = type.GetField("m_TargetTexture", flags | System.Reflection.BindingFlags.Instance);
                RenderTexture source = null;
                for (int frame = 0; frame < 8; frame++)
                {
                    var view = method.Invoke(null, null);
                    ((UnityEditor.EditorWindow)view).Repaint();
                    yield return null;
                    source = field.GetValue(view) as RenderTexture;
                }
                Assert.That(source, Is.Not.Null);
                Assert.That(source.width, Is.EqualTo((int)width));
                Assert.That(source.height, Is.EqualTo((int)height));
                var target = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
                var previous = RenderTexture.active;
                var texture = new Texture2D(source.width, source.height, TextureFormat.RGB24, false);
                try
                {
                    if (SystemInfo.graphicsUVStartsAtTop) Graphics.Blit(source, target, new Vector2(1, -1), new Vector2(0, 1));
                    else Graphics.Blit(source, target);
                    RenderTexture.active = target;
                    texture.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
                    texture.Apply();
                    var colors = new System.Collections.Generic.HashSet<Color32>();
                    var pixels = texture.GetPixels32();
                    for (int y = 0; y < source.height; y += 11)
                        for (int x = 0; x < source.width; x += 11) colors.Add(pixels[y * source.width + x]);
                    Assert.That(colors.Count, Is.GreaterThan(8));
                    File.WriteAllBytes(Path.Combine(output, state + "-" + width + ".png"), texture.EncodeToPNG());
                }
                finally
                {
                    RenderTexture.active = previous;
                    RenderTexture.ReleaseTemporary(target);
                    UnityEngine.Object.DestroyImmediate(texture);
                }
            }
        }
#endif
        [UnityTest] public IEnumerator InitialWinSettlesOnceAndPersistsZeroMoveRecord()
        {
            var pack = PlayableFlowFixture.Create();
            var level = AsciiLevelFactory.Create("######", "#    #", "# @* #", "######");
            level.levelId = "initial-win";
            var session = new GameSession(level, SessionMode.Formal);
            pack.levels.Clear();
            pack.levelOrder.Clear();
            pack.solutionWitnesses.Clear();
            pack.levels.Add(level);
            pack.levelOrder.Add(level.levelId);
            pack.solutionWitnesses.Add(session.CreateWitness());
            coordinator.SelectPack(pack);
            while (!coordinator.LastSave.IsCompleted) yield return null;
            coordinator.StartLevel(level.levelId);
            yield return null;
            while (!coordinator.LastSave.IsCompleted) yield return null;
            Assert.That(coordinator.App.Modal.IsOpen, Is.True);
            Assert.That(coordinator.Gameplay.Session.Completions.Count, Is.EqualTo(1));
            Assert.That(coordinator.Progress.Best(pack.packId, level).moves, Is.Zero);
            coordinator.App.Modal.Close();
            yield return null;
            yield return null;
            Assert.That(coordinator.App.Modal.IsOpen, Is.False);
            Assert.That(coordinator.Gameplay.Session.Completions.Count, Is.EqualTo(1));
        }
        [UnityTest] public IEnumerator SaveFailureHasOneFittingRetryAndPreservesCompletion()
        {
            coordinator.SelectPack(coordinator.Catalog.Entries[0].Pack);
            while (!coordinator.LastSave.IsCompleted) yield return null;
            coordinator.StartLevel("A");
            files.FailFlush = true;
            coordinator.Gameplay.RequestMove(Direction.Right);
            yield return new WaitForSecondsRealtime(.15f);
            while (!coordinator.LastSave.IsCompleted) yield return null;
            Assert.That(coordinator.ProgressSaved, Is.False);
            Assert.That(coordinator.Progress.Best(coordinator.ActivePack.packId, coordinator.ActivePack.levels[0]), Is.Not.Null);
            Assert.That(coordinator.App.Modal.Body.text, Does.Contain("成绩保存失败"));
            for (int attempt = 0; attempt < 2; attempt++)
            {
                var retries = coordinator.App.Modal.Actions.GetComponentsInChildren<Button>().Where(b => b.name == "重试保存").ToArray();
                Assert.That(retries.Length, Is.EqualTo(1));
                retries[0].onClick.Invoke();
                while (!coordinator.LastSave.IsCompleted) yield return null;
            }
            Canvas.ForceUpdateCanvases();
            var actions = coordinator.App.Modal.Actions;
            float total = actions.GetComponentsInChildren<Button>().Sum(b => b.GetComponent<LayoutElement>().preferredWidth);
            total += (actions.GetComponentsInChildren<Button>().Length - 1) * 8;
            Assert.That(total, Is.LessThanOrEqualTo(actions.rect.width + .1f));
            files.FailFlush = false;
            actions.GetComponentsInChildren<Button>().Single(b => b.name == "重试保存").onClick.Invoke();
            while (!coordinator.LastSave.IsCompleted) yield return null;
            Assert.That(coordinator.ProgressSaved, Is.True, coordinator.ProgressError);
            Assert.That(coordinator.App.Modal.Body.text, Does.Contain("正式成绩已保存"));
            Assert.That(actions.GetComponentsInChildren<Button>().Count(b => b.name == "重试保存"), Is.Zero);
        }
        [UnityTest] public IEnumerator LateSaveCannotRewriteAnotherModal()
        {
            coordinator.SelectPack(coordinator.Catalog.Entries[0].Pack);
            while (!coordinator.LastSave.IsCompleted) yield return null;
            coordinator.StartLevel("A");
            files.HoldFlush();
            coordinator.Gameplay.RequestMove(Direction.Right);
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(coordinator.LastSave.IsCompleted, Is.False);
            coordinator.App.Modal.Show("另一个窗口", "保留这段内容");
            files.ReleaseFlush();
            while (!coordinator.LastSave.IsCompleted) yield return null;
            Assert.That(coordinator.App.Modal.Body.text, Is.EqualTo("保留这段内容"));
        }
        [UnityTest] public IEnumerator LateFailedSaveWaitsForUnrelatedModalToClose()
        {
            coordinator.SelectPack(coordinator.Catalog.Entries[0].Pack);
            while (!coordinator.LastSave.IsCompleted) yield return null;
            coordinator.StartLevel("A");
            files.HoldFlush();
            files.FailFlush = true;
            coordinator.Gameplay.RequestMove(Direction.Right);
            yield return new WaitForSecondsRealtime(.15f);
            coordinator.App.Modal.Show("未保存修改", "请先处理当前文档");
            files.ReleaseFlush();
            while (!coordinator.LastSave.IsCompleted) yield return null;
            yield return null;
            Assert.That(coordinator.App.Modal.Body.text, Is.EqualTo("请先处理当前文档"));
            coordinator.App.Modal.Close();
            yield return null;
            Assert.That(coordinator.App.Modal.Body.text, Does.Contain("当前完成记录仍保留"));
            Assert.That(coordinator.App.Modal.Actions.GetComponentsInChildren<Button>().Count(b => b.name == "重试保存"), Is.EqualTo(1));
        }
        [UnityTest] public IEnumerator WinningAnimationCannotBeUndoneBeforeSettlementOrLoseProgressOnExit()
        {
            coordinator.SelectPack(coordinator.Catalog.Entries[0].Pack);
            while (!coordinator.LastSave.IsCompleted) yield return null;
            coordinator.StartLevel("A");
            var view = coordinator.Gameplay;
            view.RequestMove(Direction.Right);
            Assert.That(view.HasPendingCompletion, Is.True);
            Assert.That(view.Undo(), Is.False);
            view.Restart();
            Assert.That(view.Session.IsCompleted, Is.True);
            Assert.That(view.PendingDirectionCount, Is.Zero);
            Assert.That(coordinator.Progress.Best(coordinator.ActivePack.packId, coordinator.ActivePack.levels[0]), Is.Not.Null);
            coordinator.App.Navigate("MainMenu");
            while (!coordinator.LastSave.IsCompleted) yield return null;
            Assert.That(coordinator.ProgressSaved, Is.True);
            Assert.That(coordinator.App.CurrentPage, Is.EqualTo("MainMenu"));
            Assert.That(coordinator.App.Modal.IsOpen, Is.False);
        }
        [UnityTest] public IEnumerator ThreeLevelOrderSettlementsNextButtonsAndRestartReadback()
        {
            coordinator.SelectPack(coordinator.Catalog.Entries[0].Pack);
            coordinator.StartLevel("A");
            string[] order = { "A", "C", "B" };
            for (int i = 0; i < order.Length; i++)
            {
                Assert.That(coordinator.ActiveLevelId, Is.EqualTo(order[i]));
                var proof = coordinator.ActivePack.solutionWitnesses.Find(w => w.levelId == order[i]);
                foreach (char step in proof.moves)
                {
                    SokobanRules.TryParseDirection(step, out var direction);
                    coordinator.Gameplay.RequestMove(direction);
                    yield return new WaitForSecondsRealtime(.12f);
                }
                while (!coordinator.LastSave.IsCompleted) yield return null;
                Assert.That(coordinator.Gameplay.Session.IsCompleted, Is.True);
                Assert.That(coordinator.ProgressSaved, Is.True, coordinator.ProgressError);
                Assert.That(coordinator.App.Modal.IsOpen, Is.True);
                Assert.That(coordinator.App.Input.CanMove, Is.False);
                Assert.That(coordinator.App.Modal.Body.text, Does.Contain("正式成绩已保存"));
                if (i < 2)
                {
                    var next = coordinator.App.Modal.Actions.GetComponentsInChildren<Button>().Single(b => b.name == "下一关 →");
                    next.onClick.Invoke();
                    yield return null;
                    Assert.That(coordinator.Gameplay.Session.Moves, Is.Zero);
                }
            }
            Assert.That(UnlockPolicyEvaluator.AllComplete(coordinator.ActivePack, coordinator.Progress), Is.True);
            var disk = new ProgressRepository(new UserDataPaths(Path.Combine(directory, "data"))).Load();
            Assert.That(disk.Succeeded, Is.True, disk.Error);
            var restored = new ProgressService(disk.Snapshot);
            Assert.That(UnlockPolicyEvaluator.AllComplete(coordinator.ActivePack, restored), Is.True);
            Assert.That(restored.RecentPackId, Is.EqualTo(coordinator.ActivePack.packId));
        }
        [UnityTest] public IEnumerator StartingAtLastOpenLevelDoesNotClaimWholePack()
        {
            var pack = coordinator.Catalog.Entries[0].Pack;
            pack.unlockPolicy = UnlockPolicy.AllOpen;
            coordinator.SelectPack(pack);
            coordinator.StartLevel("B");
            for (int i = 0; i < 3; i++)
            {
                coordinator.Gameplay.RequestMove(Direction.Right);
                yield return new WaitForSecondsRealtime(.12f);
            }
            Assert.That(coordinator.App.Modal.IsOpen, Is.True);
            Assert.That(UnlockPolicyEvaluator.AllComplete(pack, coordinator.Progress), Is.False);
            var title = coordinator.App.Modal.GetComponentsInChildren<TMPro.TextMeshProUGUI>().Single(t => t.name == "Title");
            Assert.That(title.text, Is.EqualTo("关卡完成"));
        }
        [UnityTest] public IEnumerator AnimationCommitsLogicFirstBoundsQueueAndUndoCancelsBoth()
        {
            var pack = coordinator.Catalog.Entries[0].Pack;
            pack.unlockPolicy = UnlockPolicy.AllOpen;
            coordinator.SelectPack(pack);
            coordinator.StartLevel("B");
            var view = coordinator.Gameplay;
            var before = view.Session.State.Player;
            view.RequestMove(Direction.Right);
            Assert.That(view.Session.State.Player.x, Is.EqualTo(before.x + 1));
            Assert.That(view.IsAnimating, Is.True);
            var player = (RectTransform)view.Board.entityLayer.Find(view.Session.InitialState.ToLevelData().entities.Find(e => e.type == EntityType.Player).id);
            Assert.That(player.anchorMin.x, Is.EqualTo((float)before.x / view.Board.Width).Within(.001f));
            view.RequestMove(Direction.Right);
            view.RequestMove(Direction.Up);
            Assert.That(view.PendingDirectionCount, Is.EqualTo(1));
            view.Undo();
            Assert.That(view.PendingDirectionCount, Is.Zero);
            Assert.That(view.IsAnimating, Is.False);
            Assert.That(view.Session.Moves, Is.Zero);
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(view.Session.Moves, Is.Zero);
            view.RequestMove(Direction.Right);
            view.RequestMove(Direction.Right);
            coordinator.App.Input.ClearPending();
            Assert.That(view.PendingDirectionCount, Is.Zero);
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(view.Session.Moves, Is.EqualTo(1));
            view.Restart();
            Assert.That(view.Session.Moves, Is.Zero);
            Assert.That(view.Session.State.Player, Is.EqualTo(before));
        }
        private sealed class ControlledFiles : IFileSystem
        {
            private readonly PhysicalFileSystem inner = new PhysicalFileSystem();
            private readonly System.Threading.ManualResetEventSlim gate = new System.Threading.ManualResetEventSlim(true);
            public volatile bool FailFlush;
            public void HoldFlush() => gate.Reset();
            public void ReleaseFlush() => gate.Set();
            public void CreateDirectory(string path) => inner.CreateDirectory(path);
            public bool FileExists(string path) => inner.FileExists(path);
            public Stream CreateNew(string path) => inner.CreateNew(path);
            public void Flush(Stream stream)
            {
                if (!gate.Wait(TimeSpan.FromSeconds(10))) throw new IOException("Test flush hold timed out.");
                if (FailFlush) throw new IOException("Injected progress write failure");
                inner.Flush(stream);
            }
            public byte[] ReadAllBytes(string path, int maxBytes) => inner.ReadAllBytes(path, maxBytes);
            public string[] GetFiles(string directory, string pattern) => inner.GetFiles(directory, pattern);
            public void CopyNew(string source, string destination) => inner.CopyNew(source, destination);
            public void Move(string source, string destination) => inner.Move(source, destination);
            public void Replace(string source, string destination) => inner.Replace(source, destination);
            public void Delete(string path) => inner.Delete(path);
        }
    }
}
