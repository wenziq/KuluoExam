using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using NUnit.Framework;
using Sokoban.Core.Data;
using Sokoban.Core.Identity;
using Sokoban.Domain.Analysis;
using Sokoban.Runtime.Platform.Tasks;
using Sokoban.Runtime.Presentation.App;
using Sokoban.Runtime.Presentation.Style;
using UnityEngine;
using UnityEngine.TestTools;
namespace Sokoban.PlayModeTests
{
    public sealed class AnalysisResponsivenessTests
    {
        GameObject root;
        AnalysisCoordinator coordinator;
        [UnityTearDown] public IEnumerator Cleanup()
        {
            coordinator?.Dispose();if(coordinator!=null)while(!coordinator.LastTask.IsCompleted)yield return null;
            if(root!=null)UnityEngine.Object.Destroy(root);yield return null;
        }
        [UnityTest] public IEnumerator RealSearchLeavesFramesAndModalButtonResponsiveAndCancelsPromptly()
        {
            root=new GameObject("Analysis responsiveness fixture");var app=root.AddComponent<ApplicationController>();
#if UNITY_EDITOR
            app.theme=UnityEditor.AssetDatabase.LoadAssetAtPath<UiTheme>("Assets/Project/Settings/SokobanTheme.asset");
#endif
            app.Initialize();var dispatcher=new MainThreadDispatcher();coordinator=new AnalysisCoordinator(dispatcher);
            var level=StressLevel();var identity=new AnalysisRequestIdentity("stress","isolated",LevelFingerprint.Compute(level),"solve","initial",1);
            app.Modal.Show("正在分析","后台搜索运行时，窗口仍然响应操作。");
            bool clicked=false;var cancel=app.Modal.AddAction("取消搜索",()=>{clicked=true;coordinator.Cancel();app.Modal.SetMessage("已取消。已有有效证据保留。");});
            var clock=Stopwatch.StartNew();coordinator.Start(level,identity,AnalysisBudget.Deep);
            int frames=0;float deadline=Time.realtimeSinceStartup+3;
            while(coordinator.Progress==null&&!coordinator.LastTask.IsCompleted&&Time.realtimeSinceStartup<deadline)
            {frames++;dispatcher.Drain();yield return null;}
            Assert.That(coordinator.Progress,Is.Not.Null,"Stress fixture must actually expand states.");
            for(int i=0;i<5;i++){dispatcher.Drain();frames++;yield return null;}
            Assert.That(coordinator.LastTask.IsCompleted,Is.False,"Fixture must still search when UI cancellation is clicked.");
            long expanded=coordinator.Progress.ExpandedNodes;var cancellation=Stopwatch.StartNew();cancel.onClick.Invoke();
            Assert.That(clicked,Is.True);Assert.That(app.Modal.Body.text,Does.Contain("已取消"));
            while(!coordinator.LastTask.IsCompleted&&cancellation.ElapsedMilliseconds<2000)yield return null;
            cancellation.Stop();dispatcher.Drain();
            Assert.That(coordinator.LastTask.IsCompleted,Is.True);Assert.That(cancellation.ElapsedMilliseconds,Is.LessThan(250));
            Assert.That(coordinator.State,Is.EqualTo(AnalysisJobState.Cancelled));Assert.That(coordinator.LastResult.Outcome,Is.EqualTo(AnalysisOutcome.Unknown));Assert.That(frames,Is.GreaterThanOrEqualTo(5));
            string output=Environment.GetEnvironmentVariable("SOKOBAN_U12_EVIDENCE_DIR");
            if(!string.IsNullOrEmpty(output))
            {
                Directory.CreateDirectory(output);
                File.WriteAllText(Path.Combine(output,"responsiveness.txt"),"Unity: "+Application.unityVersion+"\nOS: "+SystemInfo.operatingSystem+"\nCPU: "+SystemInfo.processorType+"\nGPU: "+SystemInfo.graphicsDeviceName+"\nRAM MiB: "+SystemInfo.systemMemorySize+"\nResolution: "+Screen.width+"x"+Screen.height+"\nFrames during work: "+frames+"\nObserved expanded nodes: "+expanded+"\nCancel-to-worker-exit ms: "+cancellation.ElapsedMilliseconds+"\nTotal ms: "+clock.ElapsedMilliseconds+"\nEnvironment: Mac Unity PlayMode; not Windows measurement.\n");
            }
        }
        [UnityTest] public IEnumerator NormalBudgetStopsRealSixteenBoxSearchAndReportsMeasuredWork()
        {
            var dispatcher=new MainThreadDispatcher();coordinator=new AnalysisCoordinator(dispatcher);var level=StressLevel();
            coordinator.Start(level,new AnalysisRequestIdentity("normal","isolated",LevelFingerprint.Compute(level),"solve","initial",1),AnalysisBudget.Normal);
            int frames=0;var clock=Stopwatch.StartNew();
            while(!coordinator.LastTask.IsCompleted&&clock.ElapsedMilliseconds<5000){dispatcher.Drain();frames++;yield return null;}
            dispatcher.Drain();Assert.That(coordinator.LastTask.IsCompleted,Is.True);Assert.That(coordinator.State,Is.EqualTo(AnalysisJobState.Completed));
            var result=coordinator.LastResult;Assert.That(result.Outcome,Is.EqualTo(AnalysisOutcome.Unknown));
            Assert.That(result.StopReason,Is.EqualTo(AnalysisStopReason.TimeBudget).Or.EqualTo(AnalysisStopReason.NodeBudget).Or.EqualTo(AnalysisStopReason.MemoryBudget));
            Assert.That(result.ExpandedNodes,Is.GreaterThan(0));Assert.That(result.EstimatedPeakBytes,Is.LessThanOrEqualTo(AnalysisBudget.Normal.MaxEstimatedBytes));Assert.That(frames,Is.GreaterThan(1));
            string output=Environment.GetEnvironmentVariable("SOKOBAN_U12_EVIDENCE_DIR");
            if(!string.IsNullOrEmpty(output))
            {
                Directory.CreateDirectory(output);
                File.WriteAllText(Path.Combine(output,"normal-budget.txt"),"CPU: "+SystemInfo.processorType+"\nGPU: "+SystemInfo.graphicsDeviceName+"\nRAM MiB: "+SystemInfo.systemMemorySize+"\nOS: "+SystemInfo.operatingSystem+"\nOutcome: "+result.Outcome+"\nStop: "+result.StopReason+"\nElapsed ms: "+result.ElapsedMilliseconds+"\nExpanded: "+result.ExpandedNodes+"\nEstimated peak bytes: "+result.EstimatedPeakBytes+"\nUI frames: "+frames+"\nNormal budget: 2000 ms / 100000 expansions / 134217728 estimated bytes. Mac PlayMode, not Windows.\n");
            }
        }
        static LevelData StressLevel()
        {
            var level=new LevelData{width=20,height=20,terrain=new int[400],name="16箱预算夹具"};
            for(int y=0;y<20;y++)for(int x=0;x<20;x++)if(x==0||y==0||x==19||y==19)level.terrain[y*20+x]=1;
            level.entities.Add(new EntityData{type=EntityType.Player,x=1,y=1});
            for(int y=0;y<4;y++)for(int x=0;x<4;x++)level.entities.Add(new EntityData{type=EntityType.Box,x=3+x*2,y=3+y*2});
            for(int y=0;y<2;y++)for(int x=0;x<8;x++)level.features.Add(new FeatureData{type=FeatureType.Goal,x=2+x*2,y=14+y*2});
            return level;
        }
    }
}
