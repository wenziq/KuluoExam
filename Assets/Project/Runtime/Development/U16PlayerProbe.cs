#if SOKOBAN_VERIFICATION_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Sokoban.Core.Data;
using Sokoban.Core.Rules;
using Sokoban.Runtime.Persistence;
using Sokoban.Runtime.Presentation.App;
using Sokoban.Runtime.Presentation.Controls;
using Sokoban.Runtime.Presentation.Workshop;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace Sokoban.Runtime.Development
{
 public sealed class U16PlayerProbe:MonoBehaviour
 {
  [Serializable] sealed class Receipt{public bool passed;public string phase,unity,dataRoot,error,utc;}
  Receipt receipt;float deadline;
  static string Arg(string key){var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,key);return index>=0&&index+1<args.Length?args[index+1]:null;}
  public static string DataRootOverride
  {
   get{
#if SOKOBAN_VERIFICATION_BUILD && !UNITY_EDITOR
    var root=Arg("--sokoban-u16-probe");if(root!=null&&Path.IsPathRooted(root))return Path.GetFullPath(root);
#endif
    return null;
   }
  }
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Begin(){if(DataRootOverride!=null)new GameObject("U16 development verification").AddComponent<U16PlayerProbe>();}
  IEnumerator Start()
  {
   Application.runInBackground=true;receipt=new Receipt{phase=Arg("--sokoban-u16-phase"),unity=Application.unityVersion,dataRoot=DataRootOverride,utc=DateTime.UtcNow.ToString("O")};Directory.CreateDirectory(receipt.dataRoot);deadline=Time.realtimeSinceStartup+(Arg("--sokoban-native-picker")=="true"?240:60);var run=Run();
   while(true){object next;try{if(!run.MoveNext())break;next=run.Current;}catch(Exception e){receipt.error=e.ToString();Finish(false);yield break;}yield return next;}Finish(true);
  }
  IEnumerator Run()
  {
   GameApplicationCoordinator game;while((game=FindAnyObjectByType<GameApplicationCoordinator>())==null||!game.Initialized){Check();yield return null;}
   var workshop=game.GetComponent<WorkshopApplicationCoordinator>();var exchange=game.GetComponent<ImportPreviewModal>();bool native=Arg("--sokoban-native-picker")=="true";if(!native)exchange.ConfigureDialogs(new Sokoban.Runtime.Platform.FileDialogs.InGameFileDialogService(game.App));string file=Arg("--sokoban-u16-file");
   if(receipt.phase=="export")
   {
    var p=new PackData{name="独立 Player 内容交换"};var l=AsciiLevelFactory.Create("#####","#@$.#","#   #","#####");p.levels.Add(l);p.levelOrder.Add(l.levelId);workshop.Open(p);yield return null;
    workshop.Operation("ExportPlayable");yield return null;if(!native){Field(game,"FileName").text=file;Click(game,"保存到此处");}while(!exchange.LastTask.IsCompleted){Check();yield return null;}
    Require(!exchange.LastTask.IsFaulted&&File.Exists(file),"Export through actual picker failed.");ContentCatalog.ValidatePlayable(StrictPackJson.Parse(File.ReadAllBytes(file)));Require(game.Catalog.Entries.All(e=>e.Source!=ContentSource.Installed),"Export unexpectedly applied content.");game.App.Modal.Close();yield break;
   }
   if(receipt.phase=="import")
   {
    game.App.Command("ImportPack");yield return null;if(!native){Field(game,"FileName").text=file;Click(game,"选择文件");}while(!exchange.LastTask.IsCompleted){Check();yield return null;}
    Require(game.App.Modal.IsOpen,"Import preview missing.");Click(game,"导入副本");while(!exchange.LastTask.IsCompleted){Check();yield return null;}
    Require(!exchange.LastTask.IsFaulted,"Import task failed.");game.App.Modal.Close();game.ReloadCatalog();
   }
   var entry=game.Catalog.Entries.Single(e=>e.Source==ContentSource.Installed);ContentCatalog.ValidatePlayable(entry.Pack);
   if(receipt.phase=="reload"){Require(game.Progress.Snapshot().records.Any(r=>r.packId==entry.PackId),"Formal completion failed to survive restart.");yield break;}
   game.SelectPack(entry.Pack);game.StartLevel(entry.Pack.levelOrder[0]);game.Gameplay.RequestMove(Direction.Right);yield return new WaitForSecondsRealtime(.2f);while(!game.LastSave.IsCompleted){Check();yield return null;}Require(game.Gameplay.Session.IsCompleted&&game.ProgressSaved,"Imported level could not complete and save.");game.App.Modal.Close();
  }
  static TMP_InputField Field(GameApplicationCoordinator game,string name)=>game.GetComponentsInChildren<TMP_InputField>().Single(f=>f.name==name);
  static void Click(GameApplicationCoordinator game,string name){var b=game.GetComponentsInChildren<Button>().Single(x=>x.name==name);Require(b.IsInteractable(),"Blocked "+name);b.onClick.Invoke();}
  static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
  void Check(){if(Time.realtimeSinceStartup>deadline)throw new TimeoutException("U16 Player timed out.");}
  void Finish(bool passed){receipt.passed=passed;File.WriteAllText(Path.Combine(receipt.dataRoot,"u16-"+receipt.phase+"-receipt.json"),JsonUtility.ToJson(receipt,true));Debug.Log("U16 Player "+(passed?"PASS":receipt.error));if(passed)Application.Quit(0);}
 }
}
#endif
