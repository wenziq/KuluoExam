using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Sokoban.Core.Data;
using Sokoban.Core.Rules;
using Sokoban.Domain.Gameplay;
using Sokoban.Runtime.Persistence;
using Sokoban.Runtime.Platform.Feedback;
using Sokoban.Runtime.Presentation.App;
using Sokoban.Runtime.Presentation.Style;
using Sokoban.Runtime.Presentation.Views;
using Sokoban.Runtime.Presentation.Workshop;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
namespace Sokoban.PlayModeTests
{
 public sealed class SettingsFeedbackTests
 {
  GameObject root;ApplicationController app;GameApplicationCoordinator game;SettingsController settings;WorkshopApplicationCoordinator workshop;string directory;uint oldW,oldH;
  IEnumerator Create(IFileSystem files=null)
  {
#if UNITY_EDITOR
   UnityEditor.PlayModeWindow.GetRenderingResolution(out oldW,out oldH);UnityEditor.PlayModeWindow.SetViewType(UnityEditor.PlayModeWindow.PlayModeViewTypes.GameView);UnityEditor.PlayModeWindow.SetCustomRenderingResolution(1280,720,"Settings tests");
#endif
   directory=Path.Combine(Path.GetTempPath(),"sokoban-u17-ui-"+Guid.NewGuid().ToString("N"));root=new GameObject("Settings tests");app=root.AddComponent<ApplicationController>();
#if UNITY_EDITOR
   app.theme=UnityEditor.AssetDatabase.LoadAssetAtPath<UiTheme>("Assets/Project/Settings/SokobanTheme.asset");
#endif
   app.Initialize();game=root.AddComponent<GameApplicationCoordinator>();game.Initialize(app,Path.Combine(directory,"data"),Path.Combine(directory,"empty"),files);settings=root.GetComponent<SettingsController>();workshop=root.GetComponent<WorkshopApplicationCoordinator>();yield return null;
  }
  [UnityTearDown]public IEnumerator Cleanup(){var task=settings?.LastSave;if(root!=null)UnityEngine.Object.Destroy(root);yield return null;if(task!=null)while(!task.IsCompleted)yield return null;if(directory!=null&&Directory.Exists(directory))Directory.Delete(directory,true);
#if UNITY_EDITOR
   if(oldW>0)UnityEditor.PlayModeWindow.SetCustomRenderingResolution(oldW,oldH,"Previous");
#endif
  }
  Button Button(string name)=>app.modalLayer.GetComponentsInChildren<Button>().Single(b=>b.name==name);
  [UnityTest]public IEnumerator SettingsHelpAreModalAndKeepDirtyWorkshopContextWhilePersistingControls()
  {
   yield return Create();workshop.NewPack();var doc=workshop.Document;app.Navigate("Settings");Assert.That(app.CurrentPage,Is.EqualTo("Workshop"));Assert.That(app.Modal.IsOpen,Is.True);Assert.That(app.Input.CanMove,Is.False);Assert.That(workshop.Document,Is.SameAs(doc));Assert.That(doc.IsDirty,Is.True);
   Button("SoundToggle").onClick.Invoke();app.modalLayer.GetComponentInChildren<Slider>().value=.72f;Button("FullscreenToggle").onClick.Invoke();while(!settings.LastSave.IsCompleted)yield return null;Assert.That(settings.LastSave.Result.Committed,Is.True);
   var reloaded=new SettingsRepository(game.DataPaths).Load();Assert.That(reloaded.Settings.sound,Is.False);Assert.That(reloaded.Settings.volume,Is.EqualTo(.72f).Within(.001));Assert.That(reloaded.Settings.fullscreen,Is.True);
   Button("SettingsHelp").onClick.Invoke();Assert.That(app.modalLayer.GetComponentsInChildren<TMP_Text>().Any(t=>t.text.Contains("暂未判定不等于无解")),Is.True);app.Modal.Close();Assert.That(workshop.Document,Is.SameAs(doc));Assert.That(workshop.Document.IsDirty,Is.True);
  }
  GameplayView Trial()
  {
   var level=AsciiLevelFactory.Create("#######","#@ $ .#","#     #","#######");var p=new PackData{name="反馈验证"};p.levels.Add(level);p.levelOrder.Add(level.levelId);workshop.Open(p);workshop.StartTrial(false);return workshop.Trial;
  }
  [UnityTest]public IEnumerator ActualMovesProduceSixFeedbackKindsAndInvalidSpamIsLimited()
  {
   yield return Create();var view=Trial();Assert.That(view,Is.Not.Null);var events=new List<FeedbackKind>();root.GetComponent<FeedbackController>().Emitted+=events.Add;
   view.RequestMove(Direction.Up);view.RequestMove(Direction.Up);view.RequestMove(Direction.Up);Assert.That(events.Count(k=>k==FeedbackKind.Invalid),Is.EqualTo(1));Assert.That(view.Session.Moves,Is.Zero);
   view.RequestMove(Direction.Right);yield return new WaitForSecondsRealtime(.1f);view.RequestMove(Direction.Right);yield return new WaitForSecondsRealtime(.1f);Assert.That(view.Undo(),Is.True);
   app.Navigate("Settings");int moves=view.Session.Moves;view.RequestMove(Direction.Right);Assert.That(view.Session.Moves,Is.EqualTo(moves));app.Modal.Close();Assert.That(workshop.Trial,Is.SameAs(view));
   view.RequestMove(Direction.Right);yield return new WaitForSecondsRealtime(.1f);view.RequestMove(Direction.Right);yield return new WaitForSecondsRealtime(.2f);
   foreach(FeedbackKind kind in Enum.GetValues(typeof(FeedbackKind)))Assert.That(events,Does.Contain(kind));Assert.That(view.Session.IsCompleted,Is.True);
  }
  [UnityTest]public IEnumerator CaptureSettingsAndHelpAtBothResolutions()
  {
   string folder=Environment.GetEnvironmentVariable("SOKOBAN_U17_EVIDENCE_DIR");if(string.IsNullOrEmpty(folder))Assert.Ignore("Opt-in screenshots.");yield return Create();
#if UNITY_EDITOR
   foreach(uint width in new uint[]{1280,1920})
   {uint height=width==1280?720u:1080u;UnityEditor.PlayModeWindow.SetCustomRenderingResolution(width,height,"Settings evidence");yield return null;app.Navigate("Settings");yield return GameViewEvidence.Capture(Path.Combine(folder,"settings-"+width+".png"),(int)width,(int)height);app.Modal.Close();app.Navigate("Help");yield return GameViewEvidence.Capture(Path.Combine(folder,"help-"+width+".png"),(int)width,(int)height);app.Modal.Close();}
#endif
  }
  sealed class DenyWrites:IFileSystem
  {
   readonly PhysicalFileSystem real=new PhysicalFileSystem();public void CreateDirectory(string p)=>throw new UnauthorizedAccessException("Read-only settings fixture");public bool FileExists(string p)=>real.FileExists(p);public Stream CreateNew(string p)=>real.CreateNew(p);public void Flush(Stream s)=>real.Flush(s);public byte[] ReadAllBytes(string p,int n)=>real.ReadAllBytes(p,n);public string[] GetFiles(string p,string f)=>real.GetFiles(p,f);public void CopyNew(string a,string b)=>real.CopyNew(a,b);public void Move(string a,string b)=>real.Move(a,b);public void Replace(string a,string b)=>real.Replace(a,b);public void Delete(string p)=>real.Delete(p);
  }
  [UnityTest]public IEnumerator FailedSettingsCannotSilentlyPassTheQuitGuard()
  {
   yield return Create(new DenyWrites());var value=settings.Current;value.volume=.8f;settings.Set(value);while(!settings.LastSave.IsCompleted)yield return null;Assert.That(settings.LastSave.Result.Committed,Is.False);
   bool continued=false;Assert.That(settings.GuardQuit(()=>continued=true),Is.False);Assert.That(continued,Is.False);Assert.That(app.Modal.IsOpen,Is.True);Button("仍然退出").onClick.Invoke();Assert.That(continued,Is.True);Assert.That(settings.GuardQuit(()=>{}),Is.True);
  }

 }
}
