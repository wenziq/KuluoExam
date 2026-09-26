using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Sokoban.Runtime.Persistence;
using Sokoban.Runtime.Platform.FileDialogs;
using Sokoban.Runtime.Presentation.App;
using Sokoban.Runtime.Presentation.Controls;
using Sokoban.Runtime.Presentation.Style;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
namespace Sokoban.PlayModeTests
{
 public sealed class FilePickerTests
 {
  GameObject root;ApplicationController app;string directory;
  IEnumerator Create()
  {
   directory=Path.Combine(Path.GetTempPath(),"sokoban-picker-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);root=new GameObject("File picker tests");app=root.AddComponent<ApplicationController>();
#if UNITY_EDITOR
   app.theme=UnityEditor.AssetDatabase.LoadAssetAtPath<UiTheme>("Assets/Project/Settings/SokobanTheme.asset");
#endif
   app.Initialize();yield return null;
  }
  [UnityTearDown] public IEnumerator Cleanup(){if(root!=null)UnityEngine.Object.Destroy(root);yield return null;if(directory!=null&&Directory.Exists(directory))Directory.Delete(directory,true);}
  TMP_InputField Field(string name)=>app.modalLayer.GetComponentsInChildren<TMP_InputField>().Single(f=>f.name==name);
  Button Button(string name)=>app.modalLayer.GetComponentsInChildren<Button>().Single(b=>b.name==name);
  [UnityTest] public IEnumerator SelectsChinesePathRejectsBadSuffixAndCancelsWithoutChangingFiles()
  {
   yield return Create();string path=Path.Combine(directory,"中文 空格.sokopack.json");File.WriteAllText(path,"original");var dialog=new InGameFileDialogService(app);var selection=dialog.OpenAsync(directory);yield return null;
   Field("FileName").text="missing.txt";Button("选择文件").onClick.Invoke();Assert.That(selection.IsCompleted,Is.False);
   Field("FileName").text=path;Button("选择文件").onClick.Invoke();Assert.That(selection.Result,Is.EqualTo(path));Assert.That(app.Modal.IsOpen,Is.False);
   var cancelled=dialog.OpenAsync(directory);app.Modal.Close();Assert.That(cancelled.Result,Is.Null);Assert.That(File.ReadAllText(path),Is.EqualTo("original"));
  }
  [UnityTest] public IEnumerator ExistingSaveNeedsSecondExplicitClickAndEditingNameResetsConsent()
  {
   yield return Create();string path=Path.Combine(directory,"旧.sokopack.json");File.WriteAllText(path,"old");var selection=new InGameFileDialogService(app).SaveAsync(Path.GetFileName(path),directory);yield return null;
   Button("保存到此处").onClick.Invoke();Assert.That(selection.IsCompleted,Is.False);
   Field("FileName").text="./旧.sokopack.json";Button("保存到此处").onClick.Invoke();Assert.That(selection.IsCompleted,Is.False);
   Button("保存到此处").onClick.Invoke();Assert.That(selection.Result,Is.EqualTo(path));Assert.That(File.ReadAllText(path),Is.EqualTo("old"));
  }
  [UnityTest] public IEnumerator DestroyingApplicationCompletesPendingPickerAsCancelled()
  {
   yield return Create();var pending=new InGameFileDialogService(app).OpenAsync(directory);UnityEngine.Object.Destroy(root);yield return null;Assert.That(pending.IsCompleted,Is.True);Assert.That(pending.Result,Is.Null);
  }

  [UnityTest] public IEnumerator NativeSelectionPreservesExactPathRemembersDirectoryAndTreatsCancelAsNoOp()
  {
   yield return Create();string path=Path.Combine(directory,"系统选择 中文.sokopack.json");File.WriteAllText(path,"existing");
   bool saving=false;string seenDirectory=null,seenName=null;string selected=path;
   var dialog=new RuntimeFileDialogService(app,(save,name,folder)=>{saving=save;seenName=name;seenDirectory=folder;return System.Threading.Tasks.Task.FromResult(selected);});
   var open=dialog.OpenAsync(directory);while(!open.IsCompleted)yield return null;
   Assert.That(open.Result,Is.EqualTo(path));Assert.That(saving,Is.False);Assert.That(app.Modal.IsOpen,Is.False);
   var saveTask=dialog.SaveAsync("导出.sokopack.json");while(!saveTask.IsCompleted)yield return null;
   Assert.That(saving,Is.True);Assert.That(seenDirectory,Is.EqualTo(directory));Assert.That(seenName,Is.EqualTo("导出.sokopack.json"));
   Assert.That(saveTask.Result,Is.EqualTo(path));Assert.That(File.ReadAllText(path),Is.EqualTo("existing"));
   selected="";var cancel=dialog.OpenAsync();while(!cancel.IsCompleted)yield return null;
   Assert.That(cancel.Result,Is.Null);Assert.That(app.Modal.IsOpen,Is.False);
  }
  [UnityTest] public IEnumerator NativeInvalidExtensionAndFailureShowErrorWithoutChangingFiles()
  {
   yield return Create();string path=Path.Combine(directory,"wrong.json");File.WriteAllText(path,"keep");
   var dialog=new RuntimeFileDialogService(app,(save,name,folder)=>System.Threading.Tasks.Task.FromResult(path));
   var task=dialog.SaveAsync("测试.sokopack.json",directory);while(!task.IsCompleted)yield return null;
   Assert.That(task.Result,Is.Null);Assert.That(app.Modal.Body.text,Does.Contain(".sokopack.json"));Assert.That(File.ReadAllText(path),Is.EqualTo("keep"));
   dialog=new RuntimeFileDialogService(app,(save,name,folder)=>throw new IOException("Injected native failure"));
   task=dialog.OpenAsync(directory);while(!task.IsCompleted)yield return null;
   Assert.That(task.IsFaulted,Is.False);Assert.That(app.Modal.Body.text,Does.Contain("Injected native failure"));
  }
  [UnityTest] public IEnumerator NativePendingSelectionCannotOpenTwiceOrUpdateDestroyedApplication()
  {
   yield return Create();var pending=new System.Threading.Tasks.TaskCompletionSource<string>();int calls=0;
   var dialog=new RuntimeFileDialogService(app,(save,name,folder)=>{calls++;return pending.Task;});
   var first=dialog.OpenAsync(directory);yield return null;var duplicate=dialog.OpenAsync(directory);
   Assert.That(duplicate.Result,Is.Null);Assert.That(calls,Is.EqualTo(1));
   UnityEngine.Object.Destroy(root);yield return null;pending.SetResult(Path.Combine(directory,"gone.sokopack.json"));
   while(!first.IsCompleted)yield return null;Assert.That(first.Result,Is.Null);
  }

 }
}
