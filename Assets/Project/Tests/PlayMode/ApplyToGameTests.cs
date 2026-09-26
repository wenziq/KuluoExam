using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using Sokoban.Core.Data;
using Sokoban.Core.Identity;
using Sokoban.Domain.Analysis;
using Sokoban.Domain.Workshop;
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
    public sealed class ApplyToGameTests
    {
        uint previousWidth,previousHeight;
        GameObject root;string directory;ApplicationController app;WorkshopApplicationCoordinator workshop;ApplyPackModal apply;HeldSolver held;
        IEnumerator Create(IAnalysisSolver solver=null)
        {
#if UNITY_EDITOR
            UnityEditor.PlayModeWindow.GetRenderingResolution(out previousWidth,out previousHeight);
            UnityEditor.PlayModeWindow.SetViewType(UnityEditor.PlayModeWindow.PlayModeViewTypes.GameView);
            UnityEditor.PlayModeWindow.SetCustomRenderingResolution(1280,720,"Apply tests");
#endif
            directory=Path.Combine(Path.GetTempPath(),"sokoban-u15-ui-"+Guid.NewGuid().ToString("N"));root=new GameObject("Apply fixture");app=root.AddComponent<ApplicationController>();
#if UNITY_EDITOR
            app.theme=UnityEditor.AssetDatabase.LoadAssetAtPath<UiTheme>("Assets/Project/Settings/SokobanTheme.asset");
#endif
            app.Initialize();apply=root.AddComponent<ApplyPackModal>();apply.Configure(solver);
            var game=root.AddComponent<GameApplicationCoordinator>();game.Initialize(app,Path.Combine(directory,"data"),Path.Combine(directory,"empty"));workshop=root.GetComponent<WorkshopApplicationCoordinator>();
            var a=AsciiLevelFactory.Create("#####","#@$.#","#   #","#####");a.name="第二关";var b=AsciiLevelFactory.Create("######","#@ $.#","#    #","######");b.name="第一关";
            var pack=new PackData{name="应用测试"};pack.levels.AddRange(new[]{a,b});pack.levelOrder.AddRange(new[]{b.levelId,a.levelId});workshop.Open(pack,true);yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            held?.Release.Set();var task=apply?.LastApply;if(root!=null)UnityEngine.Object.Destroy(root);yield return null;if(task!=null)while(!task.IsCompleted)yield return null;
            held?.Dispose();held=null;if(directory!=null&&Directory.Exists(directory))Directory.Delete(directory,true);
#if UNITY_EDITOR
            if(previousWidth>0&&previousHeight>0)UnityEditor.PlayModeWindow.SetCustomRenderingResolution(previousWidth,previousHeight,"Previous");
#endif
        }
        string Text()=>string.Join("\n",app.modalLayer.GetComponentsInChildren<TMP_Text>().Select(t=>t.text));
        Button Button(string name)=>app.modalLayer.GetComponentsInChildren<Button>().Single(b=>b.name==name);
        IEnumerator Done()
        {
            float deadline=Time.realtimeSinceStartup+8;while(!apply.LastApply.IsCompleted){Assert.That(Time.realtimeSinceStartup,Is.LessThan(deadline));yield return null;}
            Assert.That(apply.LastApply.IsFaulted,Is.False);yield return null;
        }
        [UnityTest] public IEnumerator ApplyButtonInstallsCatalogVersionAndStartsFirstFormalLevelInOrder()
        {
            yield return Create();string first=workshop.Document.Snapshot().levelOrder[0];
            Assert.That(workshop.View.GetComponentsInChildren<Button>().Any(b=>b.name=="ApplyPack"),Is.False);
            var menu=workshop.View.GetComponentsInChildren<HoverMenu>().Single(m=>m.name=="PackOperations");
            Assert.That(menu.Options[0].Title,Is.EqualTo("加入可玩关卡集"));
            menu.Open();yield return null;
            app.menuLayer.GetComponentsInChildren<Button>().Single(b=>b.GetComponentsInChildren<TMP_Text>().Any(t=>t.text=="加入可玩关卡集")).onClick.Invoke();
            yield return Done();
            Assert.That(menu.Options[0].Title,Is.EqualTo("更新可玩关卡集"));
            Assert.That(apply.LastApply.Result.Applied,Is.True);Assert.That(Text(),Does.Contain("加入可玩关卡集成功").And.Contain("2"));Assert.That(workshop.Document.IsDirty,Is.False);
            Assert.That(workshop.View.GetComponentsInChildren<TMP_Text>().Single(t=>t.name=="Status").text,Does.Not.Contain("尚未应用这些修改").And.Contain("已加入可玩关卡集 r1"));
            Assert.That(root.GetComponent<Sokoban.Runtime.Presentation.Analysis.AnalysisMenuController>().SummaryFor(workshop.Document.Snapshot().levels[0]),Is.EqualTo("有解"));
            Assert.That(workshop.Game.Catalog.Entries.Any(e=>e.Source==ContentSource.Installed),Is.True);Button("从第一关开始").onClick.Invoke();yield return null;
            Assert.That(workshop.Game.ActiveLevelId,Is.EqualTo(first));Assert.That(workshop.Game.Gameplay.Session.Mode,Is.EqualTo(Sokoban.Domain.Gameplay.SessionMode.Formal));Assert.That(workshop.Game.ActivePack.contentRevision,Is.EqualTo(1));
            workshop.Game.Gameplay.RequestMove(Sokoban.Core.Rules.Direction.Right);workshop.Game.Gameplay.RequestMove(Sokoban.Core.Rules.Direction.Right);yield return new WaitForSecondsRealtime(.25f);
            Button("下一关 →").onClick.Invoke();yield return null;Assert.That(workshop.Game.ActiveLevelId,Is.EqualTo(workshop.Game.ActivePack.levelOrder[1]));
            workshop.Game.Gameplay.RequestMove(Sokoban.Core.Rules.Direction.Right);yield return new WaitForSecondsRealtime(.2f);Assert.That(Text(),Does.Contain("关卡集完成"));
            while(!workshop.Game.LastSave.IsCompleted)yield return null;Assert.That(workshop.Game.Progress.Snapshot().records.Count,Is.EqualTo(2));
        }
        [UnityTest] public IEnumerator CatalogRefreshCannotChangeTheOrderOfAnAlreadyRunningFormalSession()
        {
            yield return Create();workshop.Operation("ApplyPack");yield return Done();Button("从第一关开始").onClick.Invoke();yield return null;
            var game=workshop.Game;string[] original=game.ActivePack.levelOrder.ToArray();var updated=game.ActivePack.DeepCopy();updated.levelOrder.Reverse();
            var install=new InstalledPackRepository(game.DataPaths,game.Files).InstallAsync(updated,true);while(!install.IsCompleted)yield return null;Assert.That(install.Result.Committed,Is.True);
            game.ReloadCatalog();Assert.That(game.ActivePack.contentRevision,Is.EqualTo(1));Assert.That(game.ActivePack.levelOrder,Is.EqualTo(original));
            Assert.That(game.Catalog.Entries.Single(e=>e.Source==ContentSource.Installed).Pack.contentRevision,Is.EqualTo(2));
        }
        [UnityTest] public IEnumerator StructuralProblemLocatesActualLevelAndRetainsTheDraft()
        {
            yield return Create();string id=workshop.Document.Snapshot().levelOrder[0];workshop.Run(()=>workshop.Document.Edit("删除玩家",id,p=>p.levels.Find(l=>l.levelId==id).entities.RemoveAll(e=>e.type==EntityType.Player)));
            workshop.Operation("ApplyPack");yield return Done();Assert.That(apply.LastApply.Result.Status,Is.EqualTo(ApplyStatus.Blocked));Assert.That(Text(),Does.Contain("请放置一个玩家"));
            Button("Locate_"+id).onClick.Invoke();Assert.That(workshop.Document.SelectedLevelId,Is.EqualTo(id));Assert.That(workshop.InspectorTab,Is.EqualTo(2));Assert.That(workshop.Game.Catalog.Entries.Any(e=>e.Source==ContentSource.Installed),Is.False);
        }
        sealed class HeldSolver:IAnalysisSolver,IDisposable
        {
            public readonly ManualResetEventSlim Entered=new ManualResetEventSlim(),Release=new ManualResetEventSlim();
            public AnalysisResult Solve(LevelData root,AnalysisBudget budget,CancellationToken token,Action<AnalysisProgress> progress){Entered.Set();while(!Release.Wait(2))token.ThrowIfCancellationRequested();return new PushAStarSolver().Solve(root,budget,token,progress);}
            public void Dispose(){Entered.Dispose();Release.Dispose();}
        }
        [UnityTest] public IEnumerator EditsDuringApplyStayDirtyAndResultIdentifiesTheOlderSnapshot()
        {
            held=new HeldSolver();yield return Create(held);string original=workshop.Document.Snapshot().name;workshop.Operation("ApplyPack");
            float deadline=Time.realtimeSinceStartup+5;while(!held.Entered.IsSet&&!apply.LastApply.IsCompleted){Assert.That(Time.realtimeSinceStartup,Is.LessThan(deadline));yield return null;}Assert.That(held.Entered.IsSet,Is.True);
            workshop.Run(()=>LevelOperations.SetPackMetadata(workshop.Document,"之后的新修改","",UnlockPolicy.Sequential));held.Release.Set();yield return Done();
            Assert.That(apply.LastApply.Result.InstalledPack.name,Is.EqualTo(original));Assert.That(workshop.Document.IsDirty,Is.True);Assert.That(Text(),Does.Contain("后续修改尚未同步到可玩关卡集"));
            Button("从第一关开始").onClick.Invoke();Assert.That(app.CurrentPage,Is.EqualTo("Workshop"));Assert.That(Text(),Does.Contain("保留当前修改"));Button("取消").onClick.Invoke();Assert.That(workshop.Document.IsDirty,Is.True);
        }
        [UnityTest] public IEnumerator DraftsStayInWorkshopAndPlayableDeletionPreservesDraftAndProgress()
        {
            yield return Create();
            var game=workshop.Game;var persistence=root.GetComponent<WorkshopPersistenceController>();
            var save=persistence.SaveAsync();while(!save.IsCompleted)yield return null;
            Assert.That(save.Result.Committed,Is.True);
            string id=workshop.Document.Snapshot().packId;
            app.Navigate("PackLibrary");yield return null;
            Assert.That(app.pageRoot.GetComponentsInChildren<TMP_Text>().Count(t=>t.name=="Name"),Is.Zero);
            app.Navigate("WorkshopLibrary");yield return null;
            Assert.That(app.pageRoot.GetComponentsInChildren<TMP_Text>().Single(t=>t.name=="Name").text,Is.EqualTo("应用测试"));
            string output=Environment.GetEnvironmentVariable("SOKOBAN_U15_EVIDENCE_DIR");
            if(!string.IsNullOrEmpty(output))yield return GameViewEvidence.Capture(Path.Combine(output,"workshop-drafts.png"),1280,720);
            app.pageRoot.GetComponentsInChildren<Button>().Single(b=>b.name=="SelectPack").onClick.Invoke();yield return null;
            workshop.Operation("ApplyPack");yield return Done();app.Modal.Close();
            game.SelectPack(apply.LastApply.Result.InstalledPack);yield return null;
            var level=game.ActivePack.levels[0];
            var session=new Sokoban.Domain.Gameplay.GameSession(level,Sokoban.Domain.Gameplay.SessionMode.Formal);
            session.TryMove(Sokoban.Core.Rules.Direction.Right);
            Assert.That(game.Progress.Record(id,session),Is.True);
            var progressSave=game.SaveProgressAsync();while(!progressSave.IsCompleted)yield return null;
            app.Navigate("PackLibrary");yield return null;
            Assert.That(app.pageRoot.GetComponentsInChildren<TMP_Text>().Count(t=>t.name=="Name"),Is.EqualTo(1));
            if(!string.IsNullOrEmpty(output))yield return GameViewEvidence.Capture(Path.Combine(output,"playable-library.png"),1280,720);
            app.pageRoot.GetComponentsInChildren<Button>().Single(b=>b.name=="DeletePack").onClick.Invoke();
            Button("取消").onClick.Invoke();
            Assert.That(new InstalledPackRepository(game.DataPaths).Load(id),Is.Not.Null);
            app.pageRoot.GetComponentsInChildren<Button>().Single(b=>b.name=="DeletePack").onClick.Invoke();
            if(!string.IsNullOrEmpty(output))yield return GameViewEvidence.Capture(Path.Combine(output,"delete-confirmation.png"),1280,720);
            Button("删除关卡集").onClick.Invoke();while(!game.LastPackRemoval.IsCompleted)yield return null;yield return null;
            Assert.That(game.LastPackRemoval.Result.Committed,Is.True);
            Assert.That(File.Exists(game.LastPackRemoval.Result.PreservedOriginalPath),Is.True);
            Assert.That(game.ActivePack,Is.Null);
            Assert.That(app.pageRoot.GetComponentsInChildren<Button>().Any(b=>b.name=="DeletePack"),Is.False);
            Assert.That(new DraftRepository(game.DataPaths).Load(id).Succeeded,Is.True);
            Assert.That(game.Progress.Best(id,level),Is.Not.Null);
            while(!game.LastSave.IsCompleted)yield return null;
            var catalog=new ContentCatalog();catalog.Load(Path.Combine(directory,"empty"),game.DataPaths);
            Assert.That(catalog.Entries.Single().Source,Is.EqualTo(ContentSource.Draft));
            app.Navigate("Workshop");yield return null;
            Assert.That(apply.ActionLabel,Is.EqualTo("加入可玩关卡集"));
            workshop.Operation("ApplyPack");yield return Done();
            Assert.That(apply.LastApply.Result.Applied,Is.True);
        }
        [UnityTest] public IEnumerator WorkshopDeletionClearsCurrentDraftAndRecoveryButPreservesPlayableVersion()
        {
            yield return Create();workshop.Operation("ApplyPack");yield return Done();app.Modal.Close();
            var persistence=root.GetComponent<WorkshopPersistenceController>();var game=workshop.Game;
            string id=workshop.Document.Snapshot().packId;
            var level=apply.LastApply.Result.InstalledPack.levels[0];
            var session=new Sokoban.Domain.Gameplay.GameSession(level,Sokoban.Domain.Gameplay.SessionMode.Formal);
            session.TryMove(Sokoban.Core.Rules.Direction.Right);Assert.That(game.Progress.Record(id,session),Is.True);
            var progressSave=game.SaveProgressAsync();while(!progressSave.IsCompleted)yield return null;
            var recovery=persistence.Recovery.SaveAsync(workshop.Document);while(!recovery.IsCompleted)yield return null;
            recovery=persistence.Recovery.SaveAsync(workshop.Document);while(!recovery.IsCompleted)yield return null;
            app.Navigate("WorkshopLibrary");yield return null;
            var remove=app.pageRoot.GetComponentsInChildren<Button>().Single(b=>b.name=="DeletePack");
            remove.onClick.Invoke();Button("取消").onClick.Invoke();
            Assert.That(persistence.Drafts.Load(id).Succeeded,Is.True);Assert.That(workshop.Document,Is.Not.Null);
            remove.onClick.Invoke();
            Assert.That(Text(),Does.Contain("已加入的可玩关卡集和历史成绩会保留"));
            string output=Environment.GetEnvironmentVariable("SOKOBAN_U15_EVIDENCE_DIR");
            if(!string.IsNullOrEmpty(output))yield return GameViewEvidence.Capture(Path.Combine(output,"delete-workshop-confirmation.png"),1280,720);
            Button("删除关卡集").onClick.Invoke();
            while(!persistence.LastRemoval.IsCompleted)yield return null;yield return null;
            Assert.That(persistence.LastRemoval.Result.Committed,Is.True);
            Assert.That(workshop.Document,Is.Null);
            Assert.That(persistence.Drafts.Load(id).Missing,Is.True);
            Assert.That(persistence.Recovery.Enumerate(),Is.Empty);
            Assert.That(File.Exists(game.DataPaths.Backup(StorageArea.Recovery,id)),Is.False);
            Assert.That(new InstalledPackRepository(game.DataPaths).Load(id),Is.Not.Null);
            Assert.That(game.Progress.Best(id,level),Is.Not.Null);
            Assert.That(app.pageRoot.GetComponentsInChildren<Button>().Any(b=>b.name=="ResumeDraft"||b.name=="DeletePack"),Is.False);
            var lateRecovery=persistence.SaveRecoveryNowAsync();while(!lateRecovery.IsCompleted)yield return null;
            Assert.That(lateRecovery.Result.Status,Is.EqualTo(TransactionStatus.Cancelled));
            app.Navigate("Workshop");yield return null;
            Assert.That(workshop.View,Is.Null);
            Assert.That(app.pageRoot.GetComponentsInChildren<TMP_Text>().Any(t=>t.text.Contains("这里还没有制作草稿")),Is.True);
            game.ReloadCatalog();Assert.That(game.Catalog.Entries.Single().Source,Is.EqualTo(ContentSource.Installed));
        }
#if UNITY_EDITOR
        [UnityTest] public IEnumerator CaptureApplySuccessAndRepairAtBothSizes()
        {
            string output=Environment.GetEnvironmentVariable("SOKOBAN_U15_EVIDENCE_DIR");if(string.IsNullOrEmpty(output))Assert.Ignore("Explicit screenshot directory required.");
            yield return Create();foreach(int width in new[]{1280,1920})
            {
                int height=width==1280?720:1080;UnityEditor.PlayModeWindow.SetCustomRenderingResolution((uint)width,(uint)height,"Apply evidence");yield return null;
                var menu=workshop.View.GetComponentsInChildren<HoverMenu>().Single(m=>m.name=="PackOperations");menu.Open();yield return null;
                yield return GameViewEvidence.Capture(Path.Combine(output,"pack-operations-"+width+".png"),width,height);menu.Close();
                workshop.Operation("ApplyPack");yield return Done();Assert.That(apply.LastApply.Result.Applied,Is.True);yield return GameViewEvidence.Capture(Path.Combine(output,"applied-"+width+".png"),width,height);Button("继续编辑").onClick.Invoke();
                string id=workshop.Document.SelectedLevelId;workshop.Run(()=>workshop.Document.Edit("移除玩家",id,p=>p.levels.Find(l=>l.levelId==id).entities.RemoveAll(e=>e.type==EntityType.Player)));
                workshop.Operation("ApplyPack");yield return Done();Assert.That(apply.LastApply.Result.Status,Is.EqualTo(ApplyStatus.Blocked));yield return GameViewEvidence.Capture(Path.Combine(output,"blocked-"+width+".png"),width,height);Button("关闭结果").onClick.Invoke();workshop.Undo();
            }
        }
#endif
    }
}
