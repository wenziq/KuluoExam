using System;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using Sokoban.Core.Data;
using Sokoban.Core.Identity;
using Sokoban.Domain.Workshop;
using Sokoban.Runtime.Persistence;
namespace Sokoban.Tests.EditMode.Persistence
{
    public sealed class DraftRecoveryTests
    {
        string root;UserDataPaths paths;
        [SetUp] public void Setup(){root=Path.Combine(Path.GetTempPath(),"sokoban-u10-"+Guid.NewGuid().ToString("N"));paths=new UserDataPaths(root);}
        [TearDown] public void Cleanup(){if(Directory.Exists(root))Directory.Delete(root,true);}
        [Test] public async Task EmptyAndIncompleteDraftsRoundTripWithPreviousManualBackup()
        {
            var repository=new DraftRepository(paths);var document=new WorkshopDocument(new PackData{name="空草稿"});document.MarkUnsaved();
            Assert.That((await repository.SaveAsync(document.Snapshot())).Committed,Is.True);string id=document.Snapshot().packId;
            Assert.That(repository.Load(id).Pack.levels,Is.Empty);
            LevelOperations.Add(document);Assert.That((await repository.SaveAsync(document.Snapshot())).Committed,Is.True);
            Assert.That(repository.Load(id).Pack.levels.Count,Is.EqualTo(1));
            File.WriteAllText(paths.Primary(StorageArea.Drafts,id),"broken");Assert.That(repository.Load(id).Succeeded,Is.False);
            Assert.That((await repository.RecoverBackupAsync(id)).Committed,Is.True);Assert.That(repository.Load(id).Pack.levels,Is.Empty);
        }
        [Test] public async Task SaveCapturesSnapshotBeforeCallerEditsAndDoesNotClearNewDirtyState()
        {
            var repository=new DraftRepository(paths);var document=new WorkshopDocument(new PackData{name="版本一"});document.MarkUnsaved();
            var snapshot=document.Snapshot();string hash=DocumentHash.Compute(snapshot);var task=repository.SaveAsync(snapshot);
            snapshot.name="调用方也继续修改";LevelOperations.SetPackMetadata(document,"版本二","",UnlockPolicy.Sequential);
            Assert.That((await task).Committed,Is.True);document.MarkSaved(hash);
            Assert.That(document.IsDirty,Is.True);Assert.That(repository.Load(snapshot.packId).Pack.name,Is.EqualTo("版本一"));
        }
        [Test] public async Task RecoveryKeepsManualBaselineAndExplicitDiscardRemovesRecoveryAndBackup()
        {
            var service=new RecoveryService(paths);var document=new WorkshopDocument(new PackData{name="手动版本"});string baseline=document.SavedHash;
            LevelOperations.SetPackMetadata(document,"恢复版本一","",UnlockPolicy.Sequential);Assert.That((await service.SaveAsync(document)).Committed,Is.True);
            LevelOperations.SetPackMetadata(document,"恢复版本二","",UnlockPolicy.Sequential);Assert.That((await service.SaveAsync(document)).Committed,Is.True);
            string id=document.Snapshot().packId;var restored=service.Load(id).Restore();
            Assert.That(restored.SavedHash,Is.EqualTo(baseline));Assert.That(restored.IsDirty,Is.True);Assert.That(restored.Snapshot().name,Is.EqualTo("恢复版本二"));
            await service.DeleteAsync(id);Assert.That(service.Load(id),Is.Null);Assert.That(File.Exists(paths.Backup(StorageArea.Recovery,id)),Is.False);
        }
        [Test] public async Task NeverSavedRecoveryRemainsDirtyAndDoesNotWriteDraftDirectory()
        {
            var service=new RecoveryService(paths);var document=new WorkshopDocument(new PackData{name="新草稿"});document.MarkUnsaved();
            Assert.That((await service.SaveAsync(document)).Committed,Is.True);string id=document.Snapshot().packId;
            var restored=service.Load(id).Restore();Assert.That(restored.SavedHash,Is.Null);Assert.That(restored.IsDirty,Is.True);
            Assert.That(File.Exists(paths.Primary(StorageArea.Drafts,id)),Is.False);
        }
        [Test] public async Task CorruptRecoveryNeverSilentlyLoadsAndExplicitBackupCanRecover()
        {
            var service=new RecoveryService(paths);var document=new WorkshopDocument(new PackData{name="第一恢复稿"});document.MarkUnsaved();
            await service.SaveAsync(document);LevelOperations.SetPackMetadata(document,"第二恢复稿","",UnlockPolicy.Sequential);await service.SaveAsync(document);
            string id=document.Snapshot().packId,path=paths.Primary(StorageArea.Recovery,id);string json=File.ReadAllText(path);
            string duplicate=json.Replace("\"version\":1","\"version\":1,\"version\":1");File.WriteAllText(path,duplicate);
            Assert.That(()=>service.Load(id),Throws.Exception);Assert.That(File.ReadAllText(path),Is.EqualTo(duplicate));
            Assert.That(service.Enumerate()[0].Error,Is.Not.Null);Assert.That((await service.RecoverBackupAsync(id)).Committed,Is.True);Assert.That(service.Load(id).Pack.name,Is.EqualTo("第一恢复稿"));
        }
        [Test] public async Task PlayablePackCannotBeSavedIntoDraftDirectory()
        {
            var repository=new DraftRepository(paths);var pack=new PackData{name="类型错误",documentKind=DocumentKind.PlayablePack};
            Assert.That((await repository.SaveAsync(pack)).Committed,Is.False);Assert.That(repository.Load(pack.packId).Missing,Is.True);
        }

    }
}
