using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using Sokoban.Core.Data;
using Sokoban.Domain.Analysis;
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
    public sealed class AnalysisModalTests
    {
        uint previousWidth,previousHeight;
        GameObject root;string directory;ApplicationController app;WorkshopApplicationCoordinator workshop;AnalysisMenuController analysis;HoldSolver held;
        IEnumerator Create(IAnalysisSolver solver=null)
        {
#if UNITY_EDITOR
            UnityEditor.PlayModeWindow.GetRenderingResolution(out previousWidth,out previousHeight);
            UnityEditor.PlayModeWindow.SetViewType(UnityEditor.PlayModeWindow.PlayModeViewTypes.GameView);
            UnityEditor.PlayModeWindow.SetCustomRenderingResolution(1280,720,"Analysis modal test");
#endif
            directory=Path.Combine(Path.GetTempPath(),"sokoban-u13-"+Guid.NewGuid().ToString("N"));root=new GameObject("Analysis modal fixture");app=root.AddComponent<ApplicationController>();
#if UNITY_EDITOR
            app.theme=UnityEditor.AssetDatabase.LoadAssetAtPath<UiTheme>("Assets/Project/Settings/SokobanTheme.asset");
#endif
            app.Initialize();analysis=root.AddComponent<AnalysisMenuController>();analysis.Configure(solver);
            var game=root.AddComponent<GameApplicationCoordinator>();game.Initialize(app,Path.Combine(directory,"data"),Path.Combine(directory,"empty"));
            workshop=root.GetComponent<WorkshopApplicationCoordinator>();analysis.Initialize(workshop);Open("#####","#@$.#","#   #","#####");yield return null;
        }
        void Open(params string[] map)
        {var level=AsciiLevelFactory.Create(map);level.name="报告测试关卡";var pack=new PackData{name="报告关卡集"};pack.levels.Add(level);pack.levelOrder.Add(level.levelId);workshop.Open(pack);}
        [UnityTearDown] public IEnumerator Cleanup()
        {
            held?.Release.Set();var task=analysis?.Job?.LastTask;if(root!=null)UnityEngine.Object.Destroy(root);yield return null;
            if(task!=null)while(!task.IsCompleted)yield return null;held?.Dispose();held=null;
            if(directory!=null&&Directory.Exists(directory))Directory.Delete(directory,true);
#if UNITY_EDITOR
            if(previousWidth>0&&previousHeight>0)UnityEditor.PlayModeWindow.SetCustomRenderingResolution(previousWidth,previousHeight,"Previous");
#endif
        }
        string Text()=>string.Join("\n",app.modalLayer.GetComponentsInChildren<TMP_Text>().Select(t=>t.text));
        Button Button(string name)=>app.modalLayer.GetComponentsInChildren<Button>().Single(b=>b.name==name);
        bool HasExtendButton()=>app.modalLayer.GetComponentsInChildren<Button>().Any(b=>b.name=="延长搜索时间，再试一次");
        IEnumerator Done()
        {
            float deadline=Time.realtimeSinceStartup+5;
            while(analysis.Job.State==AnalysisJobState.Queued||analysis.Job.State==AnalysisJobState.Running)
            {Assert.That(Time.realtimeSinceStartup,Is.LessThan(deadline));yield return null;}
            yield return null;
        }
        [UnityTest] public IEnumerator SolvabilityAndPlayabilityShowDistinctPersistentRealReports()
        {
            yield return Create();workshop.Operation("Solve");yield return Done();
            Assert.That(analysis.Job.LastResult.Outcome,Is.EqualTo(AnalysisOutcome.Solvable));Assert.That(Text(),Does.Contain("最少推动").And.Contain("人物移动总步数"));
            yield return new WaitForSecondsRealtime(.25f);Assert.That(app.Modal.IsOpen,Is.True);Button("关闭结果").onClick.Invoke();Assert.That(app.Modal.IsOpen,Is.False);
            workshop.Operation("Playability");yield return Done();
            Assert.That(Text(),Does.Contain("不推箱子的步数").And.Contain("换一个箱子推").And.Contain("把箱子推离目标").And.Contain("其他解可能不同"));
            Assert.That(app.modalLayer.GetComponentsInChildren<TMP_Text>().Count(t=>t.name=="MetricName"),Is.EqualTo(5));
            Assert.That(app.modalLayer.GetComponentsInChildren<TMP_Text>().Where(t=>t.name.StartsWith("MetricHelp")&&t.name.Length==11).Count(),Is.EqualTo(5));
        }
        [UnityTest] public IEnumerator MissingPlayerHasRealLocateAndRepairExit()
        {
            yield return Create();Open("#####","# $.#","#   #","#####");workshop.Operation("CheckStructure");
            Assert.That(Text(),Does.Contain("PLAYER_MISSING").And.Contain("仍可保存草稿"));Button("定位_PLAYER_MISSING").onClick.Invoke();yield return null;
            Assert.That(app.Modal.IsOpen,Is.False);Assert.That(workshop.InspectorTab,Is.EqualTo(2));Assert.That(workshop.View.State.Tool,Is.EqualTo(PaintTool.Player));
        }
        [UnityTest] public IEnumerator UnknownCanRunDeeperAndDeadlockDoesNotInventMetrics()
        {
            yield return Create();analysis.Analyze(budget:new AnalysisBudget(0,0,0));yield return Done();
            Assert.That(Text(),Does.Contain("暂未判定").And.Contain("不可用"));Button("延长搜索时间，再试一次").onClick.Invoke();yield return Done();Assert.That(analysis.Job.LastResult.Outcome,Is.EqualTo(AnalysisOutcome.Solvable));
            Button("关闭结果").onClick.Invoke();Open("#######","# $   #","# .@  #","#     #","#######");workshop.Operation("Solve");yield return Done();
            Assert.That(analysis.Job.LastResult.Outcome,Is.EqualTo(AnalysisOutcome.Unsolvable));Assert.That(Text(),Does.Contain("静态死格").And.Contain("不可用"));
            Button("定位死格").onClick.Invoke();Assert.That(app.Modal.IsOpen,Is.False);Assert.That(workshop.View.State.SelectedCellX,Is.EqualTo(2));Assert.That(workshop.View.State.SelectedCellY,Is.EqualTo(3));
            Open("########","# .  . #","# $$   #","# $$@  #","# .  . #","########");workshop.Operation("Solve");yield return Done();
            Assert.That(analysis.Job.LastResult.StopReason,Is.EqualTo(AnalysisStopReason.Exhausted));Assert.That(Text(),Does.Contain("未生成可定位的局部原因"));
            Assert.That(app.modalLayer.GetComponentsInChildren<Button>().Any(b=>b.name=="定位死格"),Is.False);Assert.That(HasExtendButton(),Is.False);
        }
        [UnityTest] public IEnumerator ExtendedSearchIsOnlyOfferedAfterBudgetStops()
        {
            var solver=new BudgetStopSolver();yield return Create(solver);
            var menu=root.GetComponentsInChildren<Sokoban.Runtime.Presentation.Controls.HoverMenu>().Single(m=>m.name=="Analysis");
            Assert.That(menu.Options.Select(o=>o.Title),Does.Not.Contain("更深入分析有解性"));
            Assert.That(root.GetComponentsInChildren<Button>(true).Any(b=>b.name=="AnalyzeDeep"),Is.False);
            foreach(var reason in new[]{AnalysisStopReason.TimeBudget,AnalysisStopReason.NodeBudget,AnalysisStopReason.MemoryBudget})
            {
                solver.Reason=reason;analysis.Analyze();yield return Done();
                Assert.That(HasExtendButton(),Is.True);Assert.That(Text(),Does.Contain("本次搜索达到限制"));
                Button("延长搜索时间，再试一次").onClick.Invoke();yield return Done();
                Assert.That(solver.LastBudget.MaxElapsedMilliseconds,Is.EqualTo(10000));
                Assert.That(HasExtendButton(),Is.False);Assert.That(Text(),Does.Contain("延长搜索后仍未找到答案"));
                Button("关闭结果").onClick.Invoke();
            }
        }
        [UnityTest] public IEnumerator ClosingRunningReportCancelsAndLateFailureCannotReplaceAnotherWindow()
        {
            held=new HoldSolver();yield return Create(held);workshop.Operation("Solve");
            float deadline=Time.realtimeSinceStartup+3;while(!held.Entered.IsSet){Assert.That(Time.realtimeSinceStartup,Is.LessThan(deadline));yield return null;}
            app.Navigate("MainMenu");app.Modal.Show("另一个窗口","这里是新页面的内容");held.Release.Set();
            while(!analysis.Job.LastTask.IsCompleted)yield return null;yield return null;
            Assert.That(app.CurrentPage,Is.EqualTo("MainMenu"));Assert.That(app.Modal.Body.text,Is.EqualTo("这里是新页面的内容"));
        }
        [UnityTest] public IEnumerator FailureHasRetryAndRemainsUnknown()
        {
            yield return Create(new FailingSolver());workshop.Operation("Solve");yield return Done();
            Assert.That(analysis.Job.State,Is.EqualTo(AnalysisJobState.Failed));Assert.That(analysis.Job.LastResult.Outcome,Is.EqualTo(AnalysisOutcome.Unknown));Assert.That(Text(),Does.Contain("分析未完成"));
            Assert.That(HasExtendButton(),Is.False);Assert.That(Button("重试分析").interactable,Is.True);Button("重试分析").onClick.Invoke();yield return Done();Assert.That(app.Modal.IsOpen,Is.True);
        }
        [UnityTest] public IEnumerator RenameKeepsProofWhileLayoutEditAndUndoUpdateCurrentness()
        {
            yield return Create();workshop.Operation("Solve");yield return Done();Button("关闭结果").onClick.Invoke();
            string id=workshop.Document.SelectedLevelId;workshop.Run(()=>LevelOperations.Rename(workshop.Document,id,"只改名字"));
            Assert.That(analysis.StatusText,Does.Contain("有解"));
            workshop.Run(()=>workshop.Document.Edit("测试改墙",id,p=>p.levels[0].terrain[6]=1));Assert.That(analysis.StatusText,Does.Contain("过期"));
            workshop.Undo();Assert.That(analysis.StatusText,Does.Contain("有解"));
        }
        [UnityTest] public IEnumerator CancelButtonKeepsAnExplicitPersistentCancelledReport()
        {
            held=new HoldSolver();yield return Create(held);workshop.Operation("Solve");
            float deadline=Time.realtimeSinceStartup+3;while(!held.Entered.IsSet){Assert.That(Time.realtimeSinceStartup,Is.LessThan(deadline));yield return null;}
            Button("取消分析").onClick.Invoke();Assert.That(analysis.Job.State,Is.EqualTo(AnalysisJobState.Cancelled));
            Assert.That(app.Modal.IsOpen,Is.True);Assert.That(Text(),Does.Contain("本次分析已取消"));Assert.That(HasExtendButton(),Is.False);held.Release.Set();
            while(!analysis.Job.LastTask.IsCompleted)yield return null;yield return null;Assert.That(Text(),Does.Contain("本次分析已取消"));
        }
        [UnityTest] public IEnumerator ManualTrialEvidenceIsValidButNeverClaimsPushOptimality()
        {
            yield return Create();string hash=workshop.Document.CurrentHash;int history=workshop.Document.UndoCount;
            workshop.StartTrial(false);yield return null;workshop.Trial.RequestMove(Sokoban.Core.Rules.Direction.Right);yield return new WaitForSecondsRealtime(.15f);
            workshop.ReturnToEditor();yield return null;
            var evidence=analysis.Cache.Evidence(analysis.CurrentLevel(),PushAStarSolver.AlgorithmVersion);
            Assert.That(evidence.Optimality,Is.EqualTo(AnalysisOptimality.NotProven));Assert.That(evidence.Witness.source,Is.EqualTo(WitnessSource.Manual));
            Assert.That(workshop.Document.CurrentHash,Is.EqualTo(hash));Assert.That(workshop.Document.UndoCount,Is.EqualTo(history));Assert.That(root.GetComponent<GameApplicationCoordinator>().Progress.Snapshot().records,Is.Empty);
            workshop.Operation("Playability");yield return Done();Assert.That(Text(),Does.Contain("最优性未证明").And.Contain("参考解推动"));
        }
#if UNITY_EDITOR
        [UnityTest] public IEnumerator CaptureAnalysisReportsAndMenusAtBothTargetSizes()
        {
            string output=Environment.GetEnvironmentVariable("SOKOBAN_U13_EVIDENCE_DIR");if(string.IsNullOrEmpty(output))Assert.Ignore("Explicit visual evidence directory required.");
            yield return Create();Assert.That(Resources.Load<GameObject>("UI/Pages/AnalysisResult"),Is.Not.Null,"Runtime must use the generated report prefab.");
            foreach(int width in new[]{1280,1920})
            {
                int height=width==1280?720:1080;UnityEditor.PlayModeWindow.SetCustomRenderingResolution((uint)width,(uint)height,"Analysis evidence");yield return null;
                Open("########","##    ##","##    ##","#@* $.##","##    ##","########");
                var menu=root.GetComponentsInChildren<Sokoban.Runtime.Presentation.Controls.HoverMenu>().Single(m=>m.name=="Analysis");
                Assert.That(menu.Options.Select(o=>o.Title),Is.EqualTo(new[]{"检查遗漏项","分析有解性","分析可玩性","分析整个关卡集","复检旧参考解"}));Assert.That(menu.Options.All(o=>!string.IsNullOrWhiteSpace(o.Description)),Is.True);
                menu.Open();yield return GameViewEvidence.Capture(Path.Combine(output,"menu-"+width+".png"),width,height);menu.Close();
                analysis.Analyze(budget:new AnalysisBudget(0,0,0));yield return Done();yield return Capture("search-limit");Button("关闭结果").onClick.Invoke();
                workshop.Operation("Solve");yield return Done();Assert.That(HasExtendButton(),Is.False);yield return Capture("solvability");Button("关闭结果").onClick.Invoke();
                workshop.Operation("Playability");yield return Done();yield return Capture("playability-top");
                var scroll=app.modalLayer.GetComponentsInChildren<ScrollRect>().Single(s=>s.name=="BodyScroll");
                Assert.That(scroll.content.rect.height,Is.GreaterThan(scroll.viewport.rect.height));scroll.verticalNormalizedPosition=.75f;yield return Capture("playability-metrics");scroll.verticalNormalizedPosition=.4f;yield return Capture("playability-guide");scroll.verticalNormalizedPosition=0;yield return Capture("playability-bottom");Button("关闭结果").onClick.Invoke();
                analysis.Analyze(budget:new AnalysisBudget(0,0,0));yield return Done();Assert.That(analysis.CurrentReport.Metrics,Is.Not.Null,"A budget stop must not erase metrics for retained current evidence.");yield return Capture("budget-with-evidence");Button("关闭结果").onClick.Invoke();
                Open("#####","# $.#","#   #","#####");workshop.Operation("CheckStructure");yield return Capture("structure");Button("关闭结果").onClick.Invoke();
                IEnumerator Capture(string state)
                {
                    yield return GameViewEvidence.Capture(Path.Combine(output,state+"-"+width+".png"),width,height);
                    var dialog=(RectTransform)app.modalLayer.GetComponentsInChildren<RectTransform>().Single(r=>r.name=="Dialog");
                    var center=RectTransformUtility.WorldToScreenPoint(null,dialog.TransformPoint(dialog.rect.center));Assert.That(center.x,Is.EqualTo(width/2f).Within(2));Assert.That(center.y,Is.EqualTo(height/2f).Within(2));
                    foreach(var button in app.Modal.Actions.GetComponentsInChildren<Button>())
                    {
                        var corners=new Vector3[4];((RectTransform)button.transform).GetWorldCorners(corners);
                        foreach(var point in corners){var screen=RectTransformUtility.WorldToScreenPoint(null,point);Assert.That(screen.x,Is.InRange(0f,(float)width));Assert.That(screen.y,Is.InRange(0f,(float)height));}
                    }
                }
            }
        }
#endif
        sealed class BudgetStopSolver:IAnalysisSolver
        {
            public AnalysisStopReason Reason;public AnalysisBudget LastBudget;
            public AnalysisResult Solve(LevelData root,AnalysisBudget budget,CancellationToken token,Action<AnalysisProgress> progress)
            {LastBudget=budget;return new AnalysisResult{Outcome=AnalysisOutcome.Unknown,LevelFingerprint=Sokoban.Core.Identity.LevelFingerprint.Compute(root),StopReason=Reason,Explanation="搜索达到限制。"};}
        }
        sealed class FailingSolver:IAnalysisSolver
        {public AnalysisResult Solve(LevelData root,AnalysisBudget budget,CancellationToken token,Action<AnalysisProgress> progress)=>throw new IOException("Injected solver failure");}
        sealed class HoldSolver:IAnalysisSolver,IDisposable
        {
            public readonly ManualResetEventSlim Entered=new ManualResetEventSlim(),Release=new ManualResetEventSlim();
            public AnalysisResult Solve(LevelData root,AnalysisBudget budget,CancellationToken token,Action<AnalysisProgress> progress){Entered.Set();Release.Wait(4000);throw new IOException("Old failed callback");}
            public void Dispose(){Entered.Dispose();Release.Dispose();}
        }
    }
}
