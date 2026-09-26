#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Sokoban.Core.Data;
using Sokoban.Runtime.Presentation.App;
using Sokoban.Runtime.Presentation.Board;
using Sokoban.Runtime.Presentation.Controls;
using Sokoban.Runtime.Presentation.Style;
using UnityEngine;
using UnityEngine.TestTools;
namespace Sokoban.PlayModeTests
{
    public sealed class UiVisualEvidenceTests
    {
        [UnityTest]
        public IEnumerator CaptureShellAndBoard()
        {
            string path = Environment.GetEnvironmentVariable("SOKOBAN_UI_EVIDENCE_PATH");
            if (string.IsNullOrEmpty(path)) Assert.Ignore("Opt-in visual evidence requires an explicit output path and graphics rendering.");
            uint width = uint.TryParse(Environment.GetEnvironmentVariable("SOKOBAN_UI_WIDTH"), out var requestedWidth) ? requestedWidth : 1280;
            uint height = uint.TryParse(Environment.GetEnvironmentVariable("SOKOBAN_UI_HEIGHT"), out var requestedHeight) ? requestedHeight : 720;
            UnityEditor.PlayModeWindow.GetRenderingResolution(out uint oldWidth, out uint oldHeight);
            UnityEditor.PlayModeWindow.SetViewType(UnityEditor.PlayModeWindow.PlayModeViewTypes.GameView);
            UnityEditor.PlayModeWindow.SetCustomRenderingResolution(width, height, "Sokoban visual evidence");
            yield return null;
            yield return null;
            UnityEditor.PlayModeWindow.GetRenderingResolution(out uint actualWidth, out uint actualHeight);
            Assert.That(actualWidth, Is.EqualTo(width));
            Assert.That(actualHeight, Is.EqualTo(height));
            Assert.That(Screen.width, Is.EqualTo((int)width));
            Assert.That(Screen.height, Is.EqualTo((int)height));
            var theme = UnityEditor.AssetDatabase.LoadAssetAtPath<UiTheme>("Assets/Project/Settings/SokobanTheme.asset");
            Assert.That(theme, Is.Not.Null);
            var root = new GameObject("U6 visual evidence fixture");
            try
            {
                var app = root.AddComponent<ApplicationController>();
                app.theme = theme;
                app.Initialize();
                Canvas.ForceUpdateCanvases();
                float left = theme.LeftWidth(Screen.width), right = theme.RightWidth(Screen.width);
                var sidebar = UiFactory.Panel("LeftColumn", app.pageRoot, theme.panel).rectTransform;
                UiFactory.Fill(sidebar); sidebar.anchorMax = new Vector2(0, 1); sidebar.pivot = new Vector2(0, .5f); sidebar.anchoredPosition = Vector2.zero; sidebar.sizeDelta = new Vector2(left, 0);
                var info = UiFactory.Panel("RightColumn", app.pageRoot, theme.panel).rectTransform;
                UiFactory.Fill(info); info.anchorMin = new Vector2(1, 0); info.sizeDelta = new Vector2(right, 0); info.pivot = new Vector2(1, .5f);
                var heading = UiFactory.Text("Heading", sidebar, "关卡顺序", theme, 14);
                UiFactory.Place(heading.rectTransform, 18, 20, left - 36, 30);
                var title = UiFactory.Text("Level", sidebar, "01  视觉验收地图", theme, 13, theme.accent);
                UiFactory.Place(title.rectTransform, 18, 70, left - 36, 40);
                var details = UiFactory.Text("Details", info, "关卡信息\n\n简体中文，标点：·「」\n\n目标与箱子保留状态标记。\n\n<color=red>用户文本</color>", theme, 14);
                UiFactory.Fill(details.rectTransform, 20, 20, 20, 20);
                details.verticalAlignment = TMPro.VerticalAlignmentOptions.Top;
                var board = BoardView.Create(app.pageRoot);
                UiFactory.Fill(board.viewport, left + 1, 0, right + 1, 0);
                board.Show(AsciiLevelFactory.Create("#######", "#  .  #", "# @$  #", "#     #", "#     #", "#######"));
                string state = Environment.GetEnvironmentVariable("SOKOBAN_UI_STATE");
                if (state == "modal")
                    app.Modal.Show("分析结果", "视觉验收窗口\n\n该窗口保留，直到明确关闭。\n中文名称、说明文字与底部操作必须完整可见。\n\n<用户输入按字面显示>");
                else if (state == "menu")
                {
                    var trigger = app.Button(app.pageRoot, "分析", () => { });
                    UiFactory.Place((RectTransform)trigger.transform, left + 180, 12, 82, 34);
                    var menu = app.Menu(trigger,
                        new MenuOption("检查遗漏项", "检查玩家、箱子、目标与地图结构", null),
                        new MenuOption("分析有解性", "确认当前关卡是否存在通关路线", null),
                        new MenuOption("分析可玩性", "观察参考解的推动、绕行与多箱协调", null),
                        new MenuOption("分析整个关卡集", "按当前顺序，逐关检查有解性", null),
                        new MenuOption("复检旧参考解", "检查旧路线在修改后是否仍然可用", null));
                    menu.Open();
                }
                Canvas.ForceUpdateCanvases();
                yield return null;
                if (File.Exists(path)) File.Delete(path);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                // ScreenCapture can return an unrelated editor backbuffer in batch/unfocused
                // Editors. Read the actual GameView target, including its overlay Canvas.
                RenderTexture source = null;
                float deadline = Time.realtimeSinceStartup + 15;
                for (int frame = 0; frame < 5 || source == null; frame++)
                {
                    source = GameViewTarget(repaint: true);
                    if (Time.realtimeSinceStartup > deadline) break;
                    yield return null;
                }
                Assert.That(source, Is.Not.Null, "No rendered GameView target; graphical Editor validation is required.");
                Assert.That(source.width, Is.EqualTo((int)width));
                Assert.That(source.height, Is.EqualTo((int)height));
                File.WriteAllBytes(path, EncodeGameView(source));
                var captured = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                try
                {
                    Assert.That(captured.LoadImage(File.ReadAllBytes(path)), Is.True, "Screenshot must decode as an image.");
                    Assert.That(captured.width, Is.EqualTo((int)width));
                    Assert.That(captured.height, Is.EqualTo((int)height));
                    var colors = new HashSet<Color32>();
                    var pixels = captured.GetPixels32();
                    for (int y = 0; y < captured.height; y += 11)
                        for (int x = 0; x < captured.width; x += 11)
                            colors.Add(pixels[y * captured.width + x]);
                    Assert.That(colors.Count, Is.GreaterThan(8), "Captured frame is blank or uniform; it does not prove UI rendering.");
                }
                finally { UnityEngine.Object.DestroyImmediate(captured); }

            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                if (oldWidth > 0 && oldHeight > 0)
                    UnityEditor.PlayModeWindow.SetCustomRenderingResolution(oldWidth, oldHeight, "Previous rendering resolution");
            }
        }
        private static RenderTexture GameViewTarget(bool repaint)
        {
            var type = typeof(UnityEditor.EditorWindow).Assembly.GetType("UnityEditor.PlayModeView");
            var method = type?.GetMethod("GetMainPlayModeView", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            var view = method?.Invoke(null, null);
            if (repaint && view is UnityEditor.EditorWindow window) window.Repaint();
            var field = type?.GetField("m_TargetTexture", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            var target = view == null ? null : field?.GetValue(view) as RenderTexture;
            return target != null && target.IsCreated() ? target : null;
        }
        private static byte[] EncodeGameView(RenderTexture source)
        {
            var output = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            var texture = new Texture2D(source.width, source.height, TextureFormat.RGB24, false);
            try
            {
                if (SystemInfo.graphicsUVStartsAtTop)
                    Graphics.Blit(source, output, new Vector2(1, -1), new Vector2(0, 1));
                else
                    Graphics.Blit(source, output);
                RenderTexture.active = output;
                texture.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
                texture.Apply();
                return texture.EncodeToPNG();
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(output);
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }
    }
}
#endif
