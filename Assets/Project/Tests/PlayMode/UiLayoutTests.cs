using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Sokoban.Core.Data;
using Sokoban.Domain.Workshop;
using Sokoban.Runtime.Persistence;
using Sokoban.Runtime.Presentation.App;
using Sokoban.Runtime.Presentation.Style;
using Sokoban.Runtime.Presentation.Workshop;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
namespace Sokoban.PlayModeTests
{
 public sealed class UiLayoutTests
 {
  GameObject root; string directory; WorkshopApplicationCoordinator workshop; uint oldW,oldH;
  [UnitySetUp] public IEnumerator Setup()
  {
#if UNITY_EDITOR
   UnityEditor.PlayModeWindow.GetRenderingResolution(out oldW,out oldH);
   UnityEditor.PlayModeWindow.SetViewType(UnityEditor.PlayModeWindow.PlayModeViewTypes.GameView);
   UnityEditor.PlayModeWindow.SetCustomRenderingResolution(1280,720,"UI qualification");
#endif
   directory=Path.Combine(Path.GetTempPath(),"sokoban-u19-"+Guid.NewGuid().ToString("N"));
   root=new GameObject("UI qualification");var app=root.AddComponent<ApplicationController>();
#if UNITY_EDITOR
   app.theme=UnityEditor.AssetDatabase.LoadAssetAtPath<UiTheme>("Assets/Project/Settings/SokobanTheme.asset");
#endif
   app.Initialize();var game=root.AddComponent<GameApplicationCoordinator>();game.Initialize(app,directory,Path.Combine(Application.streamingAssetsPath,"BuiltInPacks"));workshop=root.GetComponent<WorkshopApplicationCoordinator>();workshop.NewPack();yield return null;
  }
  [UnityTearDown] public IEnumerator Cleanup()
  {
   var p=root.GetComponent<WorkshopPersistenceController>();var save=p.LastSave;var recovery=p.LastRecovery;UnityEngine.Object.Destroy(root);yield return null;
   while(!save.IsCompleted||!recovery.IsCompleted)yield return null;if(Directory.Exists(directory))Directory.Delete(directory,true);
#if UNITY_EDITOR
   if(oldW>0)UnityEditor.PlayModeWindow.SetCustomRenderingResolution(oldW,oldH,"Previous");
#endif
  }
  [UnityTest] public IEnumerator SelectedRowStaysVisibleInThirtyLevelListWithoutSnappingOnEdits()
  {
   for(int i=0;i<30;i++)workshop.Run(()=>LevelOperations.Add(workshop.Document));yield return null;Canvas.ForceUpdateCanvases();
   var scroll=root.GetComponentsInChildren<ScrollRect>().Single(s=>s.name=="Levels");
   var row=(RectTransform)scroll.content.Find("Level_"+workshop.Document.SelectedLevelId);
   var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport,row);
   Assert.That(bounds.min.y,Is.GreaterThanOrEqualTo(scroll.viewport.rect.yMin-1),"Selected last row is below viewport");
   Assert.That(bounds.max.y,Is.LessThanOrEqualTo(scroll.viewport.rect.yMax+1));
   scroll.verticalNormalizedPosition=1;yield return null;float before=scroll.content.anchoredPosition.y;
   workshop.Run(()=>LevelOperations.Rename(workshop.Document,workshop.Document.SelectedLevelId,"保持用户滚动位置"));yield return null;
   Assert.That(scroll.content.anchoredPosition.y,Is.EqualTo(before).Within(1));
   workshop.Select(workshop.Document.Snapshot().levelOrder[0]);yield return null;Canvas.ForceUpdateCanvases();
   row=(RectTransform)scroll.content.Find("Level_"+workshop.Document.SelectedLevelId);bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport,row);
   Assert.That(bounds.max.y,Is.LessThanOrEqualTo(scroll.viewport.rect.yMax+1));
  }
  [UnityTest] public IEnumerator PaletteToolsHaveVisibleGeometryAndSavedStatusIsNotRepeated()
  {
   workshop.Run(()=>LevelOperations.Add(workshop.Document));yield return null;
   foreach(string name in new[]{"Tool5","Tool6"})Assert.That(root.GetComponentsInChildren<Button>().Single(b=>b.name==name).GetComponentInChildren<UiIcon>(),Is.Not.Null,name);
   var rect=UiFactory.Rect("Status test",root.transform);var status=new WorkshopStatusBar(rect,workshop.App.theme);workshop.Document.MarkSaved(workshop.Document.CurrentHash);
   status.Refresh(workshop.Document,workshop.Document.Snapshot().levels[0],workshop.View.State,"","草稿已保存");
   string text=rect.GetComponentInChildren<TMP_Text>().text;Assert.That(text.Split(new[]{"草稿已保存"},StringSplitOptions.None).Length-1,Is.EqualTo(1));
  }
  [UnityTest] public IEnumerator EmptyWorkshopKeepsCompactButtonsAfterResizeAndCanCreateDraft()
  {
   workshop.Document.MarkSaved(workshop.Document.CurrentHash);
   workshop.ForgetDocument(workshop.Document);
   workshop.App.Navigate("WorkshopLibrary");
   foreach(var size in new[]{new Vector2Int(1280,720),new Vector2Int(1920,1080),new Vector2Int(1280,960)})
   {
#if UNITY_EDITOR
    UnityEditor.PlayModeWindow.SetCustomRenderingResolution((uint)size.x,(uint)size.y,"Empty workshop");
#endif
    yield return null;yield return null;Canvas.ForceUpdateCanvases();
    var buttons=new[]{"EmptyCreate","EmptyCopy"}.Select(name=>root.GetComponentsInChildren<Button>().Single(b=>b.name==name)).ToArray();
    foreach(var button in buttons)
    {
     var rect=(RectTransform)button.transform;
     Assert.That(rect.rect.width,Is.EqualTo(180).Within(.1f),button.name);
     Assert.That(rect.rect.height,Is.EqualTo(44).Within(.1f),button.name);
     var corners=new Vector3[4];rect.GetWorldCorners(corners);
     Assert.That(corners.All(c=>c.x>=0&&c.x<=Screen.width&&c.y>=0&&c.y<=Screen.height),Is.True,button.name+" "+size);
    }
    var first=(RectTransform)buttons[0].transform;var second=(RectTransform)buttons[1].transform;
    Assert.That(second.anchoredPosition.x,Is.GreaterThan(first.anchoredPosition.x+first.rect.width));
   }
   root.GetComponentsInChildren<Button>().Single(b=>b.name=="EmptyCreate").onClick.Invoke();yield return null;
   Assert.That(workshop.Document,Is.Not.Null);Assert.That(workshop.App.CurrentPage,Is.EqualTo("Workshop"));
  }
  [UnityTest] public IEnumerator EmptyWorkshopCanCopyExample()
  {
   workshop.Document.MarkSaved(workshop.Document.CurrentHash);workshop.ForgetDocument(workshop.Document);
   workshop.App.Navigate("WorkshopLibrary");yield return null;
   root.GetComponentsInChildren<Button>().Single(b=>b.name=="EmptyCopy").onClick.Invoke();yield return null;
   Assert.That(workshop.Document,Is.Not.Null);Assert.That(workshop.Document.Snapshot().levels.Count,Is.GreaterThan(0));
   Assert.That(workshop.App.CurrentPage,Is.EqualTo("Workshop"));
  }
  [UnityTest] public IEnumerator MaxMapLongNamesAndAspectChangesKeepCellsSquareAndActionsAccessible()
  {
   for(int i=0;i<30;i++)workshop.Run(()=>LevelOperations.Add(workshop.Document));workshop.Resize(20,20);
   workshop.Run(()=>LevelOperations.Rename(workshop.Document,workshop.Document.SelectedLevelId,new string('长',80)));
   foreach(var size in new[]{new Vector2Int(1280,720),new Vector2Int(1920,1080),new Vector2Int(1280,960),new Vector2Int(1600,720)})
   {
#if UNITY_EDITOR
    UnityEditor.PlayModeWindow.SetCustomRenderingResolution((uint)size.x,(uint)size.y,"Responsive qualification");
#endif
    yield return null;yield return null;Canvas.ForceUpdateCanvases();var board=workshop.View.Board;
    Assert.That(board.boardRoot.rect.width/20,Is.EqualTo(board.boardRoot.rect.height/20).Within(.01f));
    foreach(var cell in new[]{new Vector2Int(0,0),new Vector2Int(19,19),new Vector2Int(10,9)})
    {
     var r=board.boardRoot.rect;var world=board.boardRoot.TransformPoint(new Vector3(r.xMin+(cell.x+.5f)*r.width/20,r.yMin+(cell.y+.5f)*r.height/20));
     Assert.That(board.TryCell(RectTransformUtility.WorldToScreenPoint(null,world),null,out var actual),Is.True);Assert.That(actual,Is.EqualTo(cell));
    }
    var listScroll=root.GetComponentsInChildren<ScrollRect>().Single(s=>s.name=="Levels");
    var selected=(RectTransform)listScroll.content.Find("Level_"+workshop.Document.SelectedLevelId);
    var selectedBounds=RectTransformUtility.CalculateRelativeRectTransformBounds(listScroll.viewport,selected);
    Assert.That(selectedBounds.min.y,Is.GreaterThanOrEqualTo(listScroll.viewport.rect.yMin-1),"Selected row after resize "+size);
    foreach(string name in new[]{"TrialCurrent","TrialFirst","Analysis","PackOperations","SaveDraft","AddLevel","Tool6"})
    {
     var button=root.GetComponentsInChildren<Button>().Single(b=>b.name==name);var corners=new Vector3[4];((RectTransform)button.transform).GetWorldCorners(corners);
     Assert.That(corners.All(c=>c.x>=-1&&c.x<=Screen.width+1&&c.y>=-1&&c.y<=Screen.height+1),Is.True,name+" "+size);
    }
#if UNITY_EDITOR
    string output=Environment.GetEnvironmentVariable("SOKOBAN_U19_EVIDENCE_DIR");if(!string.IsNullOrEmpty(output))yield return GameViewEvidence.Capture(Path.Combine(output,"max-map-"+size.x+"x"+size.y+".png"),size.x,size.y);
#endif
   }
  }
#if UNITY_EDITOR
  [UnityTest] public IEnumerator CaptureFinalProductPagesAtBothTargetResolutions()
  {
   string output=Environment.GetEnvironmentVariable("SOKOBAN_U19_EVIDENCE_DIR");if(string.IsNullOrEmpty(output))yield break;
   var game=root.GetComponent<GameApplicationCoordinator>();workshop.Document.MarkSaved(workshop.Document.CurrentHash);
   foreach(int width in new[]{1280,1920})
   {
    int height=width==1280?720:1080;UnityEditor.PlayModeWindow.SetCustomRenderingResolution((uint)width,(uint)height,"Final product");yield return null;
    game.App.Navigate("MainMenu");yield return Capture("main");game.App.Navigate("PackLibrary");yield return Capture("library");
    var pack=game.Catalog.Entries[0].Pack;game.SelectPack(pack);yield return Capture("select");
    foreach(var cardBoard in root.GetComponentsInChildren<Sokoban.Runtime.Presentation.Board.BoardView>())Assert.That(cardBoard.Pan,Is.EqualTo(Vector2.zero),"Card map moved during grid layout");game.StartLevel(pack.levelOrder[0]);yield return Capture("game");
    root.GetComponentsInChildren<Button>().Single(b=>b.name=="Pause").onClick.Invoke();yield return Capture("pause");game.App.Modal.Close();
    game.App.Navigate("Settings");yield return Capture("settings");game.App.Modal.Close();game.App.Navigate("Help");yield return Capture("help");game.App.Modal.Close();
    game.Gameplay.RequestMove(Sokoban.Core.Rules.Direction.Right);yield return new WaitForSecondsRealtime(.12f);yield return Capture("result");game.App.Modal.Close();
    var draft=Sokoban.Core.Identity.ContentIdentity.CreateIndependentCopy(pack);draft.documentKind=DocumentKind.DraftPack;workshop.Open(draft);yield return Capture("workshop");
    workshop.Document.MarkSaved(workshop.Document.CurrentHash);
    IEnumerator Capture(string name){yield return null;yield return GameViewEvidence.Capture(Path.Combine(output,name+"-"+width+".png"),width,height);}
   }
  }
#endif
 }
}
