using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using Sokoban.Core.Data;
using Sokoban.Domain.Workshop;
using Sokoban.Runtime.Persistence;
using Sokoban.Runtime.Presentation.App;
using Sokoban.Runtime.Presentation.Style;
using Sokoban.Runtime.Presentation.Workshop;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
namespace Sokoban.PlayModeTests
{
    public sealed class DraftWorkflowTests
    {
        GameObject root;string directory;UserDataPaths paths;ApplicationController app;WorkshopApplicationCoordinator workshop;WorkshopPersistenceController persistence;GateFiles files;
        [UnitySetUp] public IEnumerator Setup()
        {
            directory=Path.Combine(Path.GetTempPath(),"sokoban-u10-ui-"+Guid.NewGuid().ToString("N"));paths=new UserDataPaths(Path.Combine(directory,"data"));
            CreateRoot();workshop.NewPack();yield return null;
        }
        private void CreateRoot()
        {
            root=new GameObject("U10 isolated workflow");app=root.AddComponent<ApplicationController>();
#if UNITY_EDITOR
            app.theme=UnityEditor.AssetDatabase.LoadAssetAtPath<UiTheme>("Assets/Project/Settings/SokobanTheme.asset");
#endif
            app.Initialize();files=new GateFiles();var game=root.AddComponent<GameApplicationCoordinator>();game.Initialize(app,paths.Root,Path.Combine(directory,"empty-builtins"),files);
            workshop=root.GetComponent<WorkshopApplicationCoordinator>();persistence=root.GetComponent<WorkshopPersistenceController>()??root.AddComponent<WorkshopPersistenceController>();persistence.Initialize(workshop,paths,files);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            files?.Release();if(persistence!=null)while(!persistence.LastSave.IsCompleted||!persistence.LastRecovery.IsCompleted)yield return null;
            UnityEngine.Object.Destroy(root);yield return null;if(Directory.Exists(directory))Directory.Delete(directory,true);
        }
        Button Button(string name)=>root.GetComponentsInChildren<Button>().Single(b=>b.name==name);
        [UnityTest] public IEnumerator OldSaveCompletionKeepsNewEditDirtyAndRealFileHasOldSnapshot()
        {
            var document=workshop.Document;string id=document.Snapshot().packId;files.Block=true;
            var save=persistence.SaveAsync(false);float deadline=Time.realtimeSinceStartup+3;
            while(!files.Entered&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(files.Entered,Is.True);workshop.Run(()=>LevelOperations.SetPackMetadata(document,"保存途中继续编辑","",UnlockPolicy.Sequential));files.Release();
            while(!save.IsCompleted)yield return null;
            Assert.That(save.Result.Committed,Is.True);Assert.That(document.IsDirty,Is.True);Assert.That(new DraftRepository(paths).Load(id).Pack.name,Is.EqualTo("我的第一套关卡"));
        }
        [UnityTest] public IEnumerator LeaveCanCancelThenExplicitDiscardDeletesRecoveryBeforeNavigation()
        {
            string id=workshop.Document.Snapshot().packId;var recovery=persistence.SaveRecoveryNowAsync();while(!recovery.IsCompleted)yield return null;Assert.That(recovery.Result.Committed,Is.True);
            app.Navigate("MainMenu");Assert.That(app.CurrentPage,Is.EqualTo("Workshop"));Assert.That(app.Modal.IsOpen,Is.True);
            Button("取消").onClick.Invoke();Assert.That(app.CurrentPage,Is.EqualTo("Workshop"));
            app.Navigate("MainMenu");Button("不保存并丢弃").onClick.Invoke();while(persistence.IsLeaving)yield return null;
            Assert.That(app.CurrentPage,Is.EqualTo("MainMenu"));Assert.That(new RecoveryService(paths).Load(id),Is.Null);Assert.That(workshop.Document,Is.Null);
        }
        [UnityTest] public IEnumerator FailedSaveKeepsDocumentAndRetryPersistsIt()
        {
            var document=workshop.Document;string id=document.Snapshot().packId;files.Fail=true;
            var save=persistence.SaveAsync();while(!save.IsCompleted)yield return null;
            Assert.That(save.Result.Committed,Is.False);Assert.That(document.IsDirty,Is.True);Assert.That(app.Modal.IsOpen,Is.True);Assert.That(new DraftRepository(paths).Load(id).Missing,Is.True);
            files.Fail=false;Button("重试保存").onClick.Invoke();while(!persistence.LastSave.IsCompleted)yield return null;
            Assert.That(persistence.LastSave.Result.Committed,Is.True);Assert.That(document.IsDirty,Is.False);Assert.That(new DraftRepository(paths).Load(id).Succeeded,Is.True);
        }
        [UnityTest] public IEnumerator IdleRecoveryWaitsForStrokeThenReloadOffersRecoverableDirtyVersion()
        {
            var document=workshop.Document;string id=document.Snapshot().packId;
            var save=persistence.SaveAsync(false);while(!save.IsCompleted)yield return null;
            workshop.Run(()=>LevelOperations.Add(document));var stroke=document.BeginStroke(PaintTool.Wall);stroke.AddPoint(2,2);
            yield return new WaitForSecondsRealtime(2.15f);Assert.That(new RecoveryService(paths).Load(id),Is.Null,"An active cancellable stroke must not be persisted.");
            stroke.Commit();workshop.Changed();yield return new WaitForSecondsRealtime(2.15f);
            while(!persistence.LastRecovery.IsCompleted)yield return null;
            Assert.That(new RecoveryService(paths).Load(id).Pack.levels.Count,Is.EqualTo(1));Assert.That(document.IsDirty,Is.True);
            UnityEngine.Object.Destroy(root);yield return null;CreateRoot();
            float deadline=Time.realtimeSinceStartup+5;while(!app.Modal.IsOpen&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(app.Modal.IsOpen,Is.True);Button("恢复制作内容").onClick.Invoke();yield return null;
            Assert.That(workshop.Document.IsDirty,Is.True);Assert.That(workshop.Document.Snapshot().levels.Count,Is.EqualTo(1));Assert.That(new DraftRepository(paths).Load(id).Pack.levels,Is.Empty);
        }
        [UnityTest] public IEnumerator NewDocumentUsesSameGuardAndSaveBeforeContinuePreservesOldFile()
        {
            var original=workshop.Document;string id=original.Snapshot().packId;
            workshop.NewPack();Assert.That(workshop.Document,Is.SameAs(original));Button("取消").onClick.Invoke();Assert.That(workshop.Document,Is.SameAs(original));
            workshop.NewPack();Button("保存后继续").onClick.Invoke();while(persistence.IsLeaving)yield return null;
            Assert.That(workshop.Document,Is.Not.SameAs(original));Assert.That(workshop.Document.IsDirty,Is.True);Assert.That(new DraftRepository(paths).Load(id).Succeeded,Is.True);
        }
        [UnityTest] public IEnumerator FailedRecoveryDeletionDoesNotDiscardMemoryOrNavigate()
        {
            var original=workshop.Document;string id=original.Snapshot().packId;var recovery=persistence.SaveRecoveryNowAsync();while(!recovery.IsCompleted)yield return null;
            files.FailDelete=true;app.Navigate("MainMenu");Button("不保存并丢弃").onClick.Invoke();while(persistence.IsLeaving)yield return null;
            Assert.That(app.CurrentPage,Is.EqualTo("Workshop"));Assert.That(workshop.Document,Is.SameAs(original));Assert.That(new RecoveryService(paths).Load(id),Is.Not.Null);Assert.That(app.Modal.IsOpen,Is.True);
            files.FailDelete=false;Button("取消").onClick.Invoke();
        }
        [UnityTest] public IEnumerator SaveCopyDoesNotReplaceEditsMadeDuringRecoveryCleanup()
        {
            var original=workshop.Document;string id=original.Snapshot().packId;
            var recovery=persistence.SaveRecoveryNowAsync();while(!recovery.IsCompleted)yield return null;
            files.BlockDelete=true;workshop.Operation("SaveCopy");
            float deadline=Time.realtimeSinceStartup+5;while(!files.DeleteEntered&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(files.DeleteEntered,Is.True,"Copy must reach awaited cleanup before the concurrent edit.");
            workshop.Run(()=>LevelOperations.SetPackMetadata(original,"清理期间的新修改","",UnlockPolicy.Sequential));files.Release();
            while(!persistence.LastSave.IsCompleted)yield return null;
            Assert.That(persistence.LastSave.Result.Committed,Is.True);
            Assert.That(workshop.Document,Is.SameAs(original),"Late cleanup must not switch away from newer edits.");
            Assert.That(workshop.Document.Snapshot().name,Is.EqualTo("清理期间的新修改"));Assert.That(workshop.Document.IsDirty,Is.True);
            yield return new WaitForSecondsRealtime(2.15f);while(!persistence.LastRecovery.IsCompleted)yield return null;
            Assert.That(new RecoveryService(paths).Load(id).Pack.name,Is.EqualTo("清理期间的新修改"));
        }
        sealed class GateFiles:IFileSystem
        {
            readonly PhysicalFileSystem inner=new PhysicalFileSystem();readonly ManualResetEventSlim gate=new ManualResetEventSlim(false);
            public bool Block,Fail,FailDelete,BlockDelete;public volatile bool Entered,DeleteEntered;
            public void Release(){Block=false;BlockDelete=false;gate.Set();}
            public void CreateDirectory(string path)=>inner.CreateDirectory(path);public bool FileExists(string path)=>inner.FileExists(path);public Stream CreateNew(string path)=>inner.CreateNew(path);
            public void Flush(Stream stream){if(Fail)throw new IOException("Injected save failure");if(Block){Entered=true;gate.Wait(10000);}inner.Flush(stream);}
            public byte[] ReadAllBytes(string path,int maxBytes)=>inner.ReadAllBytes(path,maxBytes);public string[] GetFiles(string directory,string pattern)=>inner.GetFiles(directory,pattern);
            public void Move(string source,string destination)=>inner.Move(source,destination);public void Replace(string source,string destination)=>inner.Replace(source,destination);public void Delete(string path){if(BlockDelete&&path.Contains(Path.DirectorySeparatorChar+"Recovery"+Path.DirectorySeparatorChar)){DeleteEntered=true;gate.Wait(10000);}if(FailDelete)throw new IOException("Injected delete failure");inner.Delete(path);}public void CopyNew(string source,string destination)=>inner.CopyNew(source,destination);
        }
    }
}
