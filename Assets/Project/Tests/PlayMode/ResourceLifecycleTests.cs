using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Sokoban.Core.Data;
using Sokoban.Core.Identity;
using Sokoban.Domain.Gameplay;
using Sokoban.Domain.Workshop;
using Sokoban.Runtime.Presentation.Analysis;
using Sokoban.Runtime.Presentation.App;
using Sokoban.Runtime.Presentation.Style;
using Sokoban.Runtime.Presentation.Workshop;
using Sokoban.Runtime.Presentation.Views;
using UnityEngine;
using UnityEngine.TestTools;
namespace Sokoban.PlayModeTests
{
 public sealed class ResourceLifecycleTests
 {
  [UnityTest] public IEnumerator HundredEditTrialAndPlaybackCyclesKeepObjectsTasksAndCacheBounded()
  {
   string directory=Path.Combine(Path.GetTempPath(),"sokoban-lifecycle-"+Guid.NewGuid().ToString("N"));var root=new GameObject("Lifecycle qualification");
   var app=root.AddComponent<ApplicationController>();
#if UNITY_EDITOR
   app.theme=UnityEditor.AssetDatabase.LoadAssetAtPath<UiTheme>("Assets/Project/Settings/SokobanTheme.asset");
#endif
   app.Initialize();var game=root.AddComponent<GameApplicationCoordinator>();game.Initialize(app,directory,Path.Combine(Application.streamingAssetsPath,"BuiltInPacks"));
   var workshop=root.GetComponent<WorkshopApplicationCoordinator>();var analysis=root.GetComponent<AnalysisMenuController>();
   var draft=ContentIdentity.CreateIndependentCopy(game.Catalog.Entries[0].Pack);draft.documentKind=DocumentKind.DraftPack;workshop.Open(draft);yield return null;
   int baseline=0,peak=0;long baselineMemory=0;var timer=System.Diagnostics.Stopwatch.StartNew();
   try
   {
    for(int i=0;i<100;i++)
    {
     workshop.Run(()=>LevelOperations.Rename(workshop.Document,workshop.Document.SelectedLevelId,"生命周期 "+i));workshop.Undo();
     workshop.Operation("Solve");if(i%2==0)analysis.Job.Cancel();while(!analysis.Job.LastTask.IsCompleted)yield return null;app.Modal.Close();
     workshop.StartTrial(false);yield return null;Assert.That(workshop.Trial,Is.Not.Null);workshop.ReturnToEditor();yield return null;
     var level=draft.levels.Single(l=>l.levelId==draft.levelOrder[0]);var witness=draft.solutionWitnesses.Single(w=>w.levelId==level.levelId);
     var playback=workshop.ShowPlayback(level,new SolutionPlaybackSession(level,witness));playback.Seek(playback.Session.TotalPushes);playback.Seek(0);yield return null;workshop.ReturnToEditor();yield return null;yield return null;
     int count=root.GetComponentsInChildren<Transform>(true).Length;peak=Math.Max(peak,count);
     if(i==9){baseline=count;GC.Collect();baselineMemory=GC.GetTotalMemory(true);}
     if(i>9)Assert.That(count,Is.LessThanOrEqualTo(baseline+4),"Live hierarchy grew at cycle "+i);
     Assert.That(analysis.Cache.Count,Is.LessThanOrEqualTo(64));Assert.That(analysis.Cache.EstimatedBytes,Is.LessThanOrEqualTo(8*1024*1024));
     Assert.That(analysis.Job.LastTask.IsCompleted,Is.True);Assert.That(root.GetComponentsInChildren<PlaybackView>(true),Is.Empty);Assert.That(root.GetComponentsInChildren<GameplayView>(true),Is.Empty);
    }
    GC.Collect();long memory=GC.GetTotalMemory(true);Assert.That(memory-baselineMemory,Is.LessThan(8*1024*1024),"Managed retained heap grew more than 8 MiB after warmup");
    Assert.That(game.Progress.Snapshot().records,Is.Empty);Assert.That(workshop.Document.UndoCount,Is.Zero);
    string result="cycles=100; liveTransformsBaseline="+baseline+"; peak="+peak+"; retainedManagedDeltaBytes="+(memory-baselineMemory)+"; elapsedMs="+timer.ElapsedMilliseconds+"; cached="+analysis.Cache.Count;
    Debug.Log("U19 lifecycle: "+result);string output=Environment.GetEnvironmentVariable("SOKOBAN_U19_EVIDENCE_DIR");if(!string.IsNullOrEmpty(output)){Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"lifecycle.txt"),result);}
   }
   finally{UnityEngine.Object.Destroy(root);}
   yield return null;if(Directory.Exists(directory))Directory.Delete(directory,true);
  }
 }
}
