#if SOKOBAN_VERIFICATION_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Sokoban.Core.Rules;
using Sokoban.Runtime.Presentation.App;
using Sokoban.Runtime.Presentation.Workshop;
using UnityEngine;
using UnityEngine.UI;
namespace Sokoban.Runtime.Development
{
 public sealed class U19PlayerProbe:MonoBehaviour
 {
  [Serializable]sealed class Receipt
  {
   public bool passed,headless;public string phase,unity,os,cpu,gpu,error;public int systemMemoryMb,records,levels,runtimeErrors;public long retainedManagedBytes,totalUnityAllocatedBytes;public double maxMoveDispatchMs,maxAnimationMs,maxCancelDispatchMs;
  }
  Receipt receipt;float deadline;
  int runtimeErrors;string firstRuntimeError;
  void OnEnable(){Application.logMessageReceived+=CaptureError;}
  void OnDisable(){Application.logMessageReceived-=CaptureError;}
  void CaptureError(string message,string stack,LogType type)
  {
   if(type!=LogType.Error&&type!=LogType.Exception&&type!=LogType.Assert)return;
   runtimeErrors++;if(firstRuntimeError==null)firstRuntimeError=message+"\n"+stack;
  }
  public static string DataRootOverride
  {get{
#if SOKOBAN_VERIFICATION_BUILD && !UNITY_EDITOR
   var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"--sokoban-u19-probe");if(index>=0&&index+1<args.Length&&Path.IsPathRooted(args[index+1]))return Path.GetFullPath(args[index+1]);
#endif
   return null;
  }}
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Begin(){if(DataRootOverride!=null)new GameObject("Final Player qualification").AddComponent<U19PlayerProbe>();}
  IEnumerator Start()
  {
   Application.runInBackground=true;var args=Environment.GetCommandLineArgs();receipt=new Receipt{phase=args.Contains("--sokoban-u19-reload")?"reload":"play",headless=args.Contains("-nographics"),unity=Application.unityVersion,os=SystemInfo.operatingSystem,cpu=SystemInfo.processorType,gpu=SystemInfo.graphicsDeviceName,systemMemoryMb=SystemInfo.systemMemorySize};Directory.CreateDirectory(DataRootOverride);deadline=Time.realtimeSinceStartup+90;var run=Run();
   while(true){object next;try{if(!run.MoveNext())break;next=run.Current;}catch(Exception e){receipt.error=e.ToString();Finish(false);yield break;}yield return next;}Finish(true);
  }
  IEnumerator Run()
  {
   GameApplicationCoordinator game;while((game=FindAnyObjectByType<GameApplicationCoordinator>())==null||!game.Initialized){Check();yield return null;}
   var pack=game.Catalog.Entries.Single().Pack;receipt.levels=pack.levels.Count;Require(receipt.levels==6,"Expected final six-level tutorial.");
   if(receipt.phase=="play")
   {
    game.GetComponentsInChildren<Button>().Single(b=>b.name=="StartGame").onClick.Invoke();yield return null;
    for(int i=0;i<6;i++)
    {
     Require(game.ActiveLevelId==pack.levelOrder[i],"Formal level order changed.");var path=pack.solutionWitnesses.Single(w=>w.levelId==game.ActiveLevelId).moves;
     foreach(char c in path)
     {
      SokobanRules.TryParseDirection(c,out var direction);var timer=System.Diagnostics.Stopwatch.StartNew();int previous=game.Gameplay.Session.Moves;game.Gameplay.RequestMove(direction);receipt.maxMoveDispatchMs=Math.Max(receipt.maxMoveDispatchMs,timer.Elapsed.TotalMilliseconds);Require(game.Gameplay.Session.Moves==previous+1,"Input did not update logical state immediately.");
      while(game.Gameplay.IsAnimating){Check();yield return null;}receipt.maxAnimationMs=Math.Max(receipt.maxAnimationMs,timer.Elapsed.TotalMilliseconds);
     }
     Require(game.Gameplay.Session.IsCompleted,"Level did not complete.");while(!game.LastSave.IsCompleted||!game.App.Modal.IsOpen){Check();yield return null;}
     if(i<5)game.GetComponentsInChildren<Button>().Single(b=>b.name=="下一关 →").onClick.Invoke();yield return null;
    }
    game.App.Modal.Close();var workshop=game.GetComponent<WorkshopApplicationCoordinator>();var draft=Sokoban.Core.Identity.ContentIdentity.CreateIndependentCopy(pack);draft.documentKind=Sokoban.Core.Data.DocumentKind.DraftPack;workshop.Open(draft);yield return null;
    workshop.Operation("Solve");var analysis=game.GetComponent<Sokoban.Runtime.Presentation.Analysis.AnalysisMenuController>();var cancel=System.Diagnostics.Stopwatch.StartNew();analysis.Job.Cancel();receipt.maxCancelDispatchMs=cancel.Elapsed.TotalMilliseconds;
    while(!analysis.Job.LastTask.IsCompleted){Check();yield return null;}game.App.Modal.Close();workshop.Resize(20,20);yield return null;
    Require(workshop.View.Board.Width==20&&workshop.View.Board.Height==20,"Maximum map resize failed.");workshop.Document.MarkSaved(workshop.Document.CurrentHash);
   }
   receipt.records=game.Progress.Snapshot().records.Count;Require(receipt.records==6,"Six records did not persist.");receipt.retainedManagedBytes=GC.GetTotalMemory(true);receipt.totalUnityAllocatedBytes=UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
  }
  void Check(){if(Time.realtimeSinceStartup>deadline)throw new TimeoutException("Final Player qualification timed out.");}
  static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
  void Finish(bool passed){receipt.runtimeErrors=runtimeErrors;if(runtimeErrors>0){passed=false;receipt.error=firstRuntimeError;}receipt.passed=passed;File.WriteAllText(Path.Combine(DataRootOverride,"u19-"+receipt.phase+"-receipt.json"),JsonUtility.ToJson(receipt,true));Debug.Log("U19 Player "+(passed?"PASS":receipt.error));Application.Quit(passed?0:2);}
 }
}
#endif
