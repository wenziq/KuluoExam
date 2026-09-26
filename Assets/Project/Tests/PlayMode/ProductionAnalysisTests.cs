using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Sokoban.Core.Data;
using Sokoban.Core.Rules;
using Sokoban.Domain.Analysis;
using Sokoban.Domain.Gameplay;
using Sokoban.Domain.Workshop;
using Sokoban.Runtime.Presentation.Analysis;
using Sokoban.Runtime.Presentation.App;
using Sokoban.Runtime.Presentation.Style;
using Sokoban.Runtime.Presentation.Workshop;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
namespace Sokoban.PlayModeTests
{
    public sealed class ProductionAnalysisTests
    {
        uint previousWidth,previousHeight;GateSolver held;
        GameObject root;string directory;ApplicationController app;WorkshopApplicationCoordinator workshop;AnalysisMenuController analysis;
        IEnumerator Create(IAnalysisSolver backend=null)
        {
#if UNITY_EDITOR
            UnityEditor.PlayModeWindow.GetRenderingResolution(out previousWidth,out previousHeight);
            UnityEditor.PlayModeWindow.SetViewType(UnityEditor.PlayModeWindow.PlayModeViewTypes.GameView);
            UnityEditor.PlayModeWindow.SetCustomRenderingResolution(1280,720,"Production analysis test");
#endif
            directory=Path.Combine(Path.GetTempPath(),"sokoban-u14-"+Guid.NewGuid().ToString("N"));root=new GameObject("Production analysis fixture");app=root.AddComponent<ApplicationController>();
#if UNITY_EDITOR
            app.theme=UnityEditor.AssetDatabase.LoadAssetAtPath<UiTheme>("Assets/Project/Settings/SokobanTheme.asset");
#endif
            app.Initialize();if(backend!=null){var configured=root.AddComponent<AnalysisMenuController>();configured.Configure(backend);}var game=root.AddComponent<GameApplicationCoordinator>();game.Initialize(app,Path.Combine(directory,"data"),Path.Combine(directory,"empty"));
            workshop=root.GetComponent<WorkshopApplicationCoordinator>();analysis=root.GetComponent<AnalysisMenuController>();
            var level=AsciiLevelFactory.Create("########","##    ##","##    ##","#@* $.##","##    ##","########");level.name="多箱参考解";var pack=new PackData{name="生产分析测试"};pack.levels.Add(level);pack.levelOrder.Add(level.levelId);workshop.Open(pack);yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            held?.Release.Set();var search=analysis?.Job?.LastTask;var task=analysis?.Production?.LastTask;if(root!=null)UnityEngine.Object.Destroy(root);yield return null;
            if(search!=null)while(!search.IsCompleted)yield return null;if(task!=null)while(!task.IsCompleted)yield return null;
            held?.Dispose();held=null;
            if(directory!=null&&Directory.Exists(directory))Directory.Delete(directory,true);
#if UNITY_EDITOR
            if(previousWidth>0&&previousHeight>0)UnityEditor.PlayModeWindow.SetCustomRenderingResolution(previousWidth,previousHeight,"Previous");
#endif
        }
        string Text()=>string.Join("\n",app.modalLayer.GetComponentsInChildren<TMP_Text>().Select(t=>t.text));
        Button Button(string name)=>app.modalLayer.GetComponentsInChildren<Button>().Single(b=>b.name==name);
        IEnumerator Done(bool production=false)
        {
            float deadline=Time.realtimeSinceStartup+12;
            while(production?!analysis.Production.LastTask.IsCompleted:analysis.Running){Assert.That(Time.realtimeSinceStartup,Is.LessThan(deadline));yield return null;}yield return null;
            if(production)Assert.That(analysis.Production.LastTask.IsFaulted,Is.False);
        }
        [UnityTest] public IEnumerator MetricEventsReplayAndTakeoverUseRealPrefixWithoutFormalProgress()
        {
            yield return Create();workshop.Operation("Playability");yield return Done();var report=analysis.SelectedReport;Assert.That(report.Metrics.GoalsLeft,Is.GreaterThan(0));
            string hash=workshop.Document.CurrentHash;int history=workshop.Document.UndoCount;
            MetricsView.ShowEvents(analysis,report,"G");Assert.That(Text(),Does.Contain("参考解"));Button("Event1").onClick.Invoke();yield return Done(true);
            var playback=analysis.Production.Playback;Assert.That(playback,Is.Not.Null);Assert.That(playback.Session.PushIndex,Is.EqualTo(1));Assert.That(workshop.IsReferencePlayback,Is.True);
            playback.Seek(2);var prefix=playback.Session.MoveOffset;var takeover=playback.GetComponentsInChildren<Button>().Single(b=>b.name=="TakeOver");takeover.onClick.Invoke();yield return null;
            Assert.That(workshop.Trial.Session.Mode,Is.EqualTo(SessionMode.AssistedReplayTakeover));Assert.That(workshop.Trial.Session.Moves,Is.EqualTo(prefix));Assert.That(workshop.Trial.Session.HistoryCount,Is.EqualTo(prefix));
            Assert.That(workshop.Trial.Undo(),Is.True);Assert.That(workshop.Trial.Session.Moves,Is.EqualTo(prefix-1));
            workshop.ReturnToEditor();yield return null;Assert.That(workshop.Document.CurrentHash,Is.EqualTo(hash));Assert.That(workshop.Document.UndoCount,Is.EqualTo(history));Assert.That(workshop.Game.Progress.Snapshot().records,Is.Empty);
        }
        sealed class FailedSearch:IAnalysisSolver
        {
            public AnalysisResult Solve(LevelData level,AnalysisBudget budget,System.Threading.CancellationToken token,Action<AnalysisProgress> progress)=>throw new InvalidOperationException("injected search failure");
        }
        [UnityTest] public IEnumerator FailedBatchRowRemainsUnknownAndUpdatesTheLevelBadge()
        {
            yield return Create(new FailedSearch());workshop.Operation("AnalyzePack");yield return Done(true);
            Assert.That(Text(),Does.Contain("失败 · 未判定"));Assert.That(analysis.SummaryFor(analysis.CurrentLevel()),Is.EqualTo("未知"));
            Assert.That(workshop.Document.Snapshot().solutionWitnesses,Is.Empty);
        }
        sealed class GateSolver:IAnalysisSolver,IDisposable
        {
            public readonly System.Threading.ManualResetEventSlim Entered=new System.Threading.ManualResetEventSlim();
            public readonly System.Threading.ManualResetEventSlim Release=new System.Threading.ManualResetEventSlim();
            public AnalysisResult Solve(LevelData level,AnalysisBudget budget,System.Threading.CancellationToken token,Action<AnalysisProgress> progress)
            {Entered.Set();while(!Release.Wait(2))token.ThrowIfCancellationRequested();token.ThrowIfCancellationRequested();return new PushAStarSolver().Solve(level,budget,token,progress);}
            public void Dispose(){Entered.Dispose();Release.Dispose();}
        }
        [UnityTest] public IEnumerator ClosedBatchCannotPublishIntoANewerWindow()
        {
            held=new GateSolver();yield return Create(held);workshop.Operation("AnalyzePack");float deadline=Time.realtimeSinceStartup+3;
            while(!held.Entered.IsSet){Assert.That(Time.realtimeSinceStartup,Is.LessThan(deadline));yield return null;}
            app.Modal.Close();app.Modal.Show("新窗口","新的用户操作");held.Release.Set();yield return Done(true);
            Assert.That(Text(),Does.Contain("新的用户操作"));Assert.That(workshop.Document.Snapshot().solutionWitnesses,Is.Empty);
            Assert.That(analysis.Production.Batch.Entries[0].State,Is.EqualTo(AnalysisJobState.Cancelled));
        }
        sealed class LongerReference:IAnalysisSolver
        {
            public AnalysisResult Solve(LevelData level,AnalysisBudget budget,System.Threading.CancellationToken token,Action<AnalysisProgress> progress)
            {
                var witness=new WitnessData{levelId=level.levelId,levelFingerprint=Sokoban.Core.Identity.LevelFingerprint.Compute(level),moves="RDUDRURULULD"};
                Assert.That(WitnessVerifier.Verify(level,witness).IsValid,Is.True);
                return new AnalysisResult{Outcome=AnalysisOutcome.Solvable,Optimality=AnalysisOptimality.NotProven,LevelFingerprint=witness.levelFingerprint,Witness=witness};
            }
        }
        [UnityTest] public IEnumerator ReportMetricsAlwaysDescribeItsDisplayedWitnessWhenCacheHasAShorterWalk()
        {
            yield return Create(new LongerReference());var level=analysis.CurrentLevel();var witness=new WitnessData{levelId=level.levelId,levelFingerprint=Sokoban.Core.Identity.LevelFingerprint.Compute(level),moves="RDRURULULD"};
            analysis.Cache.Record(level,new AnalysisResult{Outcome=AnalysisOutcome.Solvable,Optimality=AnalysisOptimality.PushOptimal,LevelFingerprint=witness.levelFingerprint,Witness=witness},PushAStarSolver.AlgorithmVersion);
            workshop.Operation("Solve");yield return Done();var report=analysis.SelectedReport;
            Assert.That(report.Metrics.Moves,Is.EqualTo(WitnessVerifier.Verify(report.Level,report.Evidence.Witness).Moves));
        }
        [UnityTest] public IEnumerator DeadCellsAreOffByDefaultAndDoNotRemoveSelectionOrGoals()
        {
            yield return Create();Assert.That(analysis.Production.DeadCellsVisible,Is.False);
            workshop.View.SelectCell(2,2);analysis.Production.ToggleDeadCells();yield return null;
            Assert.That(workshop.View.Board.overlayLayer.Find("SelectedCell"),Is.Not.Null);var marks=workshop.View.Board.overlayLayer.Find("StaticDeadCells");Assert.That(marks,Is.Not.Null);
            foreach(var goal in analysis.CurrentLevel().features)Assert.That(marks.Find("Mark_"+goal.x+"_"+goal.y),Is.Null);
            analysis.Production.ToggleDeadCells();Assert.That(marks.gameObject.activeSelf,Is.False);
        }
        [UnityTest] public IEnumerator BatchPersistsRealWitnessesAndRecheckHasRealResults()
        {
            yield return Create();workshop.Operation("AnalyzePack");yield return Done(true);
            Assert.That(analysis.Production.Batch.Entries[0].Result.Outcome,Is.EqualTo(AnalysisOutcome.Solvable));Assert.That(Text(),Does.Contain("1 / 1").And.Contain("P/M/W/S/G"));
            Assert.That(workshop.Document.Snapshot().solutionWitnesses.Count,Is.EqualTo(1));Button("关闭结果").onClick.Invoke();
            string id=workshop.Document.SelectedLevelId;workshop.Run(()=>workshop.Document.Edit("改不影响旧路线的墙",id,p=>p.levels[0].terrain[2*p.levels[0].width+6]=0));
            workshop.Operation("RecheckWitness");yield return Done(true);Assert.That(Text(),Does.Contain("重放成功").And.Contain("未证明最优"));Button("关闭结果").onClick.Invoke();
        }
        [UnityTest] public IEnumerator PlaybackLeavingDirtyWorkshopUsesExistingLeaveProtection()
        {
            yield return Create();workshop.Run(()=>LevelOperations.Rename(workshop.Document,workshop.Document.SelectedLevelId,"未保存地图"));workshop.Operation("Solve");yield return Done();Button("查看参考解").onClick.Invoke();yield return Done(true);
            app.Navigate("MainMenu");Assert.That(app.CurrentPage,Is.EqualTo("Gameplay"));Assert.That(app.Modal.IsOpen,Is.True);Assert.That(Text(),Does.Contain("保存"));
            Button("取消").onClick.Invoke();Assert.That(app.CurrentPage,Is.EqualTo("Gameplay"));Assert.That(workshop.Document.IsDirty,Is.True);
        }
#if UNITY_EDITOR
        [UnityTest] public IEnumerator CaptureProductionAnalysisAndPlayback()
        {
            string output=Environment.GetEnvironmentVariable("SOKOBAN_U14_EVIDENCE_DIR");if(string.IsNullOrEmpty(output))Assert.Ignore("Explicit evidence directory required.");
            yield return Create();foreach(int width in new[]{1280,1920})
            {
                int height=width==1280?720:1080;UnityEditor.PlayModeWindow.SetCustomRenderingResolution((uint)width,(uint)height,"Production evidence");yield return null;workshop.Operation("Playability");yield return Done();var report=analysis.SelectedReport;MetricsView.ShowEvents(analysis,report,"G");yield return GameViewEvidence.Capture(Path.Combine(output,"events-"+width+".png"),width,height);
                Button("Event1").onClick.Invoke();yield return Done(true);yield return GameViewEvidence.Capture(Path.Combine(output,"playback-"+width+".png"),width,height);workshop.ReturnToEditor();yield return null;
                analysis.Production.ToggleDeadCells();yield return GameViewEvidence.Capture(Path.Combine(output,"dead-cells-"+width+".png"),width,height);analysis.Production.ToggleDeadCells();
                workshop.Operation("AnalyzePack");yield return Done(true);yield return GameViewEvidence.Capture(Path.Combine(output,"batch-"+width+".png"),width,height);app.Modal.Close();
            }
        }
#endif
    }
}
