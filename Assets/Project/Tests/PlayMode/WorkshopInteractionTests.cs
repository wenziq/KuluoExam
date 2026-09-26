using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Sokoban.Core.Data;
using Sokoban.Core.Identity;
using Sokoban.Domain.Gameplay;
using Sokoban.Domain.Workshop;
using Sokoban.Runtime.Fixtures;
using Sokoban.Runtime.Persistence;
using Sokoban.Runtime.Presentation.App;
using Sokoban.Runtime.Presentation.Controls;
using Sokoban.Runtime.Presentation.Style;
using Sokoban.Runtime.Presentation.Workshop;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
namespace Sokoban.PlayModeTests
{
    public sealed class WorkshopInteractionTests
    {
        uint previousWidth,previousHeight;
        GameObject root; string directory; GameApplicationCoordinator game; WorkshopApplicationCoordinator workshop;
        [UnitySetUp] public IEnumerator Setup()
        {
#if UNITY_EDITOR
            UnityEditor.PlayModeWindow.GetRenderingResolution(out previousWidth,out previousHeight);
            UnityEditor.PlayModeWindow.SetViewType(UnityEditor.PlayModeWindow.PlayModeViewTypes.GameView);
            UnityEditor.PlayModeWindow.SetCustomRenderingResolution(1280,720,"Workshop test");
#endif
            directory=Path.Combine(Path.GetTempPath(),"sokoban-u9-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(directory,"builtins"));
            File.WriteAllBytes(Path.Combine(directory,"builtins","example.sokopack.json"),StrictPackJson.Serialize(PlayableFlowFixture.Create()));
            root=new GameObject("Workshop fixture"); var app=root.AddComponent<ApplicationController>();
#if UNITY_EDITOR
            app.theme=UnityEditor.AssetDatabase.LoadAssetAtPath<UiTheme>("Assets/Project/Settings/SokobanTheme.asset");
#endif
            app.Initialize(); game=root.AddComponent<GameApplicationCoordinator>();
            game.Initialize(app,Path.Combine(directory,"data"),Path.Combine(directory,"builtins"));
            workshop=root.GetComponent<WorkshopApplicationCoordinator>() ?? root.AddComponent<WorkshopApplicationCoordinator>(); workshop.Initialize(game);
            yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        { var persistence=root.GetComponent<WorkshopPersistenceController>();if(persistence!=null)while(!persistence.LastSave.IsCompleted||!persistence.LastRecovery.IsCompleted)yield return null;
            UnityEngine.Object.Destroy(root); yield return null; if(Directory.Exists(directory))Directory.Delete(directory,true);
#if UNITY_EDITOR
            if(previousWidth>0&&previousHeight>0)UnityEditor.PlayModeWindow.SetCustomRenderingResolution(previousWidth,previousHeight,"Previous");
#endif
        }
        PackData Draft()
        { var pack=ContentIdentity.CreateIndependentCopy(PlayableFlowFixture.Create()); pack.documentKind=DocumentKind.DraftPack;return pack; }
        Button Button(string name)=>root.GetComponentsInChildren<Button>().Single(b=>b.name==name);
        [UnityTest] public IEnumerator BothTrialButtonsPreserveDocumentAndNeverRecordFormalProgress()
        {
            var pack=Draft();workshop.Open(pack,true);yield return null;
            var doc=workshop.Document;doc.SelectLevel(pack.levelOrder[1]);LevelOperations.Rename(doc,doc.SelectedLevelId,"未保存的第二关");workshop.View.Refresh();
            Button("Tool3").onClick.Invoke();Button("Tool6").onClick.Invoke();
            var player=doc.Snapshot().levels.Find(l=>l.levelId==doc.SelectedLevelId).entities.Single(e=>e.type==EntityType.Player);
            workshop.View.SelectCell(player.x,player.y);workshop.View.SetTab(2);
            workshop.View.Board.SetView(1.3f,new Vector2(21,13));
            int history=doc.UndoCount;string hash=doc.CurrentHash;
            Button("TrialCurrent").onClick.Invoke();yield return null;
            Assert.That(workshop.Trial.Session.Mode,Is.EqualTo(SessionMode.Trial));
            Assert.That(workshop.Trial.Session.InitialState.ToLevelData().levelId,Is.EqualTo(pack.levelOrder[1]));
            workshop.ReturnToEditor();yield return null;
            Assert.That(workshop.Document,Is.SameAs(doc));Assert.That(doc.CurrentHash,Is.EqualTo(hash));Assert.That(doc.UndoCount,Is.EqualTo(history));
            Assert.That(workshop.View.State.Tool,Is.EqualTo(PaintTool.Box));Assert.That(workshop.View.State.IsSelectionTool,Is.True);Assert.That(workshop.View.State.SelectedObjectId,Is.EqualTo(player.id));Assert.That(workshop.InspectorTab,Is.EqualTo(2));
            Assert.That(workshop.View.Board.Zoom,Is.EqualTo(1.3f).Within(.001f));Assert.That(workshop.View.Board.Pan,Is.EqualTo(new Vector2(21,13)));
            Button("TrialFirst").onClick.Invoke();yield return null;
            Assert.That(workshop.Trial.Session.InitialState.ToLevelData().levelId,Is.EqualTo(pack.levelOrder[0]));
            workshop.Trial.RequestMove(Sokoban.Core.Rules.Direction.Right);yield return new WaitForSecondsRealtime(.15f);
            Assert.That(game.Progress.Snapshot().records,Is.Empty);
            Assert.That(game.App.Modal.IsOpen,Is.True);
        }
        [UnityTest] public IEnumerator ApprovedMenusAndTrialTooltipsAreDiscoverable()
        {
            workshop.Open(Draft(),true);yield return null;
            var menus=root.GetComponentsInChildren<HoverMenu>();
            Assert.That(menus.Single(m=>m.name=="Analysis").Options.Count,Is.EqualTo(5));
            Assert.That(menus.Single(m=>m.name=="PackOperations").Options.Count,Is.EqualTo(6));
            Assert.That(Button("TrialCurrent").GetComponent<TooltipTarget>().explanation,Is.EqualTo("从当前关卡开始玩"));
            Assert.That(Button("TrialFirst").GetComponent<TooltipTarget>().explanation,Is.EqualTo("从头开始"));
            Assert.That(Button("TrialCurrent").GetComponentInChildren<UiIcon>().kind,Is.EqualTo(IconKind.Play));
            Assert.That(Button("TrialFirst").GetComponentInChildren<UiIcon>().kind,Is.EqualTo(IconKind.PlayFromStart));
        }

#if UNITY_EDITOR
        [UnityTest] public IEnumerator CaptureWorkshopAtBothSizes()
        {
            string output=Environment.GetEnvironmentVariable("SOKOBAN_U9_EVIDENCE_DIR");
            if(string.IsNullOrEmpty(output))Assert.Ignore("Explicit graphical evidence directory required.");
            Directory.CreateDirectory(output);uint width=1280,height=720;
            foreach(uint size in new uint[]{1280,1920})
            {
                width=size;height=size==1280?720u:1080u;
                UnityEditor.PlayModeWindow.SetCustomRenderingResolution(width,height,"U9 workshop evidence");
                workshop.NewPack();
                if(game.App.Modal.IsOpen){Button("不保存并丢弃").onClick.Invoke();while(root.GetComponent<WorkshopPersistenceController>().IsLeaving)yield return null;}
                yield return null;
                for(int i=0;i<3;i++){Button("AddLevel").onClick.Invoke();yield return null;}
                var doc=workshop.Document;var level=doc.Snapshot().levels.Last();
                Canvas.ForceUpdateCanvases();
                int[] tools={2,3,4};int[] xs={3,4,5};
                for(int i=0;i<tools.Length;i++){Button("Tool"+tools[i]).onClick.Invoke();workshop.View.Board.OnPointerDown(At(xs[i],4));workshop.View.Board.OnPointerUp(At(xs[i],4));}
                Button("MoveLevelUp").onClick.Invoke();yield return null;
                Button("TrialCurrent").onClick.Invoke();yield return null;
                Assert.That(workshop.Trial,Is.Not.Null);yield return Capture("trial");workshop.ReturnToEditor();yield return null;
                Assert.That(doc.SelectedLevelId,Is.EqualTo(level.levelId));yield return Capture("workshop");
                Button("Analysis").GetComponent<HoverMenu>().Open();yield return Capture("analysis-menu");Button("Analysis").GetComponent<HoverMenu>().Close();
                Button("PackOperations").GetComponent<HoverMenu>().Open();yield return Capture("pack-menu");Button("PackOperations").GetComponent<HoverMenu>().Close();
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
        PointerEventData At(int x,int y)
        {
            var board=workshop.View.Board;var r=board.boardRoot.rect;
            var world=board.boardRoot.TransformPoint(new Vector3(r.xMin+(x+.5f)*r.width/board.Width,r.yMin+(y+.5f)*r.height/board.Height,0));
            return new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,world),button=PointerEventData.InputButton.Left};
        }
        [UnityTest] public IEnumerator DrawingOutsideBreaksStrokeAndEscapeRestoresWholeGesture()
        {
            workshop.NewPack();yield return null;Button("AddLevel").onClick.Invoke();yield return null;Canvas.ForceUpdateCanvases();
            var doc=workshop.Document;var board=workshop.View.Board;int before=doc.UndoCount;
            Button("Tool1").onClick.Invoke();board.OnPointerDown(At(1,1));board.OnDrag(At(2,1));
            board.OnDrag(new PointerEventData(EventSystem.current){position=new Vector2(-100,-100),button=PointerEventData.InputButton.Left});
            board.OnDrag(At(7,7));board.OnPointerUp(At(7,7));
            Assert.That(doc.UndoCount,Is.EqualTo(before+1));var level=doc.Snapshot().levels[0];
            Assert.That(level.terrain[11],Is.EqualTo(1));Assert.That(level.terrain[12],Is.EqualTo(1));Assert.That(level.terrain[77],Is.EqualTo(1));Assert.That(level.terrain[44],Is.EqualTo(0));
            board.OnPointerDown(At(1,5));var menu=Button("Analysis").GetComponent<HoverMenu>();menu.Open();board.OnDrag(At(7,5));
            Assert.That(doc.Snapshot().levels[0].terrain[57],Is.EqualTo(0));menu.Close();board.OnDrag(At(7,5));board.OnPointerUp(At(7,5));Assert.That(doc.Snapshot().levels[0].terrain[54],Is.EqualTo(0));
            string hash=doc.CurrentHash;board.OnPointerDown(At(3,3));board.OnDrag(At(5,3));game.App.Input.DispatchEscape();
            Assert.That(doc.CurrentHash,Is.EqualTo(hash));Assert.That(doc.HasActiveStroke,Is.False);
            game.App.Modal.Show("覆盖窗口","模态下不能绘制");board.OnPointerDown(At(4,4));board.OnPointerUp(At(4,4));Assert.That(doc.CurrentHash,Is.EqualTo(hash));
        }
        [UnityTest] public IEnumerator TextAndImeKeepMapShortcutsAndSaveCommitsFieldFirst()
        {
            var settings=InputSystem.settings;var oldMode=settings.updateMode;var oldBackground=settings.backgroundBehavior;
#if UNITY_EDITOR
            var oldEditor=settings.editorInputBehaviorInPlayMode;
            settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            settings.updateMode=InputSettings.UpdateMode.ProcessEventsManually;settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            var keyboard=InputSystem.AddDevice<Keyboard>();InputSystem.EnableDevice(keyboard);
            try
            {
                workshop.Open(Draft(),true);yield return null;
                var doc=workshop.Document;string hash=doc.CurrentHash;int history=doc.UndoCount;
                var field=root.GetComponentsInChildren<TMP_InputField>().Single(f=>f.name=="LevelNameInput");
                EventSystem.current.SetSelectedGameObject(field.gameObject);
                Sample(Key.LeftCtrl,Key.Z,Key.Delete,Key.Digit1,Key.RightArrow);
                Assert.That(doc.CurrentHash,Is.EqualTo(hash));Assert.That(doc.UndoCount,Is.EqualTo(history));Assert.That(workshop.View.State.Tool,Is.EqualTo(PaintTool.Wall));
                Sample();field.text="输入后保存";int saves=0;
                workshop.OperationRequested+=id=>{if(id=="SaveDraft"){saves++;Assert.That(doc.Snapshot().levels.Find(l=>l.levelId==doc.SelectedLevelId).name,Is.EqualTo("输入后保存"));}};
                keyboard.OnIMECompositionChanged(new IMECompositionString("中"));Sample(Key.LeftCtrl,Key.S);Assert.That(saves,Is.Zero);
                keyboard.OnIMECompositionChanged(new IMECompositionString(""));Sample();Sample(Key.LeftCtrl,Key.S);Assert.That(saves,Is.EqualTo(1));
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);settings.updateMode=oldMode;settings.backgroundBehavior=oldBackground;
#if UNITY_EDITOR
                settings.editorInputBehaviorInPlayMode=oldEditor;
#endif
            }
            void Sample(params Key[] keys){InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));InputSystem.Update();Assert.That(Keyboard.current,Is.SameAs(keyboard));workshop.View.SendMessage("Update");}
        }
        [UnityTest] public IEnumerator CreateRenameReorderCropCancelAndUndoThroughControls()
        {
            workshop.NewPack();yield return null;
            for(int i=0;i<3;i++){Button("AddLevel").onClick.Invoke();yield return null;}
            var doc=workshop.Document;Assert.That(doc.Snapshot().levels.Count,Is.EqualTo(3));string third=doc.SelectedLevelId;
            var name=root.GetComponentsInChildren<TMP_InputField>().Single(f=>f.name=="LevelNameInput");name.text="第三关·测试";name.onEndEdit.Invoke(name.text);
            Button("MoveLevelUp").onClick.Invoke();Assert.That(doc.Snapshot().levelOrder[1],Is.EqualTo(third));
            Assert.That(doc.Snapshot().levels.Find(l=>l.levelId==third).name,Is.EqualTo("第三关·测试"));
            var width=root.GetComponentsInChildren<TMP_InputField>().Single(f=>f.name=="WidthInput");width.text="8";Button("ResizeLevel").onClick.Invoke();
            Assert.That(game.App.Modal.IsOpen,Is.True);game.App.Modal.Close();Assert.That(doc.Snapshot().levels.Find(l=>l.levelId==third).width,Is.EqualTo(10));
            Button("ResizeLevel").onClick.Invoke();Button("确认裁剪").onClick.Invoke();Assert.That(doc.Snapshot().levels.Find(l=>l.levelId==third).width,Is.EqualTo(8));
            Button("UndoEdit").onClick.Invoke();Assert.That(doc.Snapshot().levels.Find(l=>l.levelId==third).width,Is.EqualTo(10));
            var handle=root.GetComponentsInChildren<LevelDragHandle>().Single(h=>h.Id==third);string order=string.Join(",",doc.Snapshot().levelOrder);
            handle.List.BeginDrag(third);handle.List.CancelDrag();Assert.That(string.Join(",",doc.Snapshot().levelOrder),Is.EqualTo(order));
        }
        [UnityTest] public IEnumerator InvalidDraftTrialShowsPersistentIssueInsteadOfEnteringGame()
        {
            var pack=new PackData{documentKind=DocumentKind.DraftPack};workshop.Open(pack,true);yield return null;
            Button("AddLevel").onClick.Invoke();yield return null;
            Assert.That(workshop.Document.Snapshot().levels.Count,Is.EqualTo(1));
            Button("TrialCurrent").onClick.Invoke();yield return null;
            Assert.That(workshop.Trial,Is.Null);Assert.That(game.App.Modal.IsOpen,Is.True);
            yield return new WaitForSecondsRealtime(.2f);Assert.That(game.App.Modal.IsOpen,Is.True);
        }
    }
}
