using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Sokoban.Core.Data;
using Sokoban.Runtime.Persistence;
using Sokoban.Runtime.Presentation.App;
using Sokoban.Runtime.Presentation.Controls;
using Sokoban.Runtime.Presentation.Style;
using Sokoban.Runtime.Presentation.Workshop;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
namespace Sokoban.PlayModeTests
{
 public sealed class ContentExchangeTests
 {
  GameObject root;ApplicationController app;GameApplicationCoordinator game;WorkshopApplicationCoordinator workshop;ImportPreviewModal exchange;string directory;uint oldW,oldH;
  IEnumerator Create()
  {
#if UNITY_EDITOR
   UnityEditor.PlayModeWindow.GetRenderingResolution(out oldW,out oldH);UnityEditor.PlayModeWindow.SetViewType(UnityEditor.PlayModeWindow.PlayModeViewTypes.GameView);UnityEditor.PlayModeWindow.SetCustomRenderingResolution(1280,720,"Exchange tests");
#endif
   directory=Path.Combine(Path.GetTempPath(),"sokoban-exchange-ui-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);root=new GameObject("Exchange tests");app=root.AddComponent<ApplicationController>();
#if UNITY_EDITOR
   app.theme=UnityEditor.AssetDatabase.LoadAssetAtPath<UiTheme>("Assets/Project/Settings/SokobanTheme.asset");
#endif
   app.Initialize();game=root.AddComponent<GameApplicationCoordinator>();game.Initialize(app,Path.Combine(directory,"data"),Path.Combine(directory,"empty"));workshop=root.GetComponent<WorkshopApplicationCoordinator>();exchange=root.GetComponent<ImportPreviewModal>();exchange.ConfigureDialogs(new Sokoban.Runtime.Platform.FileDialogs.InGameFileDialogService(app));yield return null;
  }
  [UnityTearDown] public IEnumerator Cleanup()
  {
   var task=exchange?.LastTask;if(root!=null)UnityEngine.Object.Destroy(root);yield return null;if(task!=null)while(!task.IsCompleted)yield return null;if(directory!=null&&Directory.Exists(directory))Directory.Delete(directory,true);
#if UNITY_EDITOR
   if(oldW>0)UnityEditor.PlayModeWindow.SetCustomRenderingResolution(oldW,oldH,"Previous");
#endif
  }
  TMP_InputField Field(string name)=>app.modalLayer.GetComponentsInChildren<TMP_InputField>().Single(f=>f.name==name);
  Button Button(string name)=>app.modalLayer.GetComponentsInChildren<Button>().Single(b=>b.name==name);
  IEnumerator Done(){float end=Time.realtimeSinceStartup+10;while(!exchange.LastTask.IsCompleted){Assert.That(Time.realtimeSinceStartup,Is.LessThan(end));yield return null;}Assert.That(exchange.LastTask.IsFaulted,Is.False);yield return null;}
  [UnityTest] public IEnumerator WorkshopExportAndLibraryImportUseActualPickerCallbacksWithoutLeavingBlankPage()
  {
   yield return Create();var p=new PackData{name="完整交换"};var l=AsciiLevelFactory.Create("#####","#@$.#","#   #","#####");p.levels.Add(l);p.levelOrder.Add(l.levelId);workshop.Open(p);
   string file=Path.Combine(directory,"真实 中文文件.sokopack.json");workshop.Operation("ExportPlayable");yield return null;Field("FileName").text=file;Button("保存到此处").onClick.Invoke();yield return Done();Assert.That(File.Exists(file),Is.True);ContentCatalog.ValidatePlayable(StrictPackJson.Parse(File.ReadAllBytes(file)));
   app.Modal.Close();app.Navigate("PackLibrary");app.Command("ImportPack");Assert.That(app.CurrentPage,Is.EqualTo("PackLibrary"));yield return null;Field("FileName").text=file;Button("选择文件").onClick.Invoke();yield return Done();Button("导入副本").onClick.Invoke();yield return Done();
   Assert.That(game.Catalog.Entries.Count(e=>e.Source==ContentSource.Installed),Is.EqualTo(1));Assert.That(app.modalLayer.GetComponentsInChildren<TMP_Text>().Any(t=>t.text=="导入完成"),Is.True);
   app.Modal.Close();app.Command("ImportPack");app.Modal.Close();yield return Done();Assert.That(app.CurrentPage,Is.EqualTo("PackLibrary"));Assert.That(app.pageRoot.GetComponentsInChildren<Button>().Any(b=>b.name=="Import"),Is.True);
  }
  [UnityTest] public IEnumerator CaptureRuntimeFilePickerAndImportPreviewAtBothTargetSizes()
  {
   string folder=Environment.GetEnvironmentVariable("SOKOBAN_U16_EVIDENCE_DIR");if(string.IsNullOrEmpty(folder))Assert.Ignore("Opt-in visual evidence.");yield return Create();
   Directory.CreateDirectory(Path.Combine(directory,"我的关卡集"));File.WriteAllBytes(Path.Combine(directory,"待编辑的草稿.sokopack.json"),StrictPackJson.Serialize(new PackData{name="草稿"}));
#if UNITY_EDITOR
   foreach(int width in new[]{1280,1920})
   {
    int height=width==1280?720:1080;UnityEditor.PlayModeWindow.SetCustomRenderingResolution((uint)width,(uint)height,"Exchange evidence");yield return null;app.Command("ImportPack");yield return null;
    Field("DirectoryPath").text=directory;Field("DirectoryPath").onSubmit.Invoke(directory);float deadline=Time.realtimeSinceStartup+5;while(app.modalLayer.GetComponentsInChildren<TMP_Text>().Any(t=>t.name=="PickerStatus"&&t.text=="正在读取目录…")){Assert.That(Time.realtimeSinceStartup,Is.LessThan(deadline));yield return null;}
    yield return GameViewEvidence.Capture(Path.Combine(folder,"picker-"+width+".png"),width,height);app.Modal.Close();yield return Done();
    var p=new PackData{name="导入一套新的推箱子关卡",description="由本机验证，再进入游戏"};string file=Path.Combine(directory,"preview.sokopack.json");File.WriteAllBytes(file,StrictPackJson.Serialize(p));app.Command("ImportPack");yield return null;Field("FileName").text=file;Button("选择文件").onClick.Invoke();yield return Done();
    yield return GameViewEvidence.Capture(Path.Combine(folder,"preview-"+width+".png"),width,height);app.Modal.Close();
   }
#endif
  }
 }
}
