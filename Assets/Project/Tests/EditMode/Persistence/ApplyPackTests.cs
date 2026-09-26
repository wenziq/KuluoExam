using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Sokoban.Core.Data;
using Sokoban.Core.Identity;
using Sokoban.Domain.Analysis;
using Sokoban.Runtime.Persistence;
namespace Sokoban.Tests.EditMode.Persistence
{
    public sealed class ApplyPackTests
    {
        string root;UserDataPaths paths;
        [SetUp] public void Setup(){root=Path.Combine(Path.GetTempPath(),"sokoban-u15-"+Guid.NewGuid().ToString("N"));paths=new UserDataPaths(root);}
        [TearDown] public void Cleanup(){if(Directory.Exists(root))Directory.Delete(root,true);}
        static PackData Draft()
        {
            var pack=new PackData{name="固定应用快照"};var level=AsciiLevelFactory.Create("######","#@ $.#","#    #","######");level.name="一号关";pack.levels.Add(level);pack.levelOrder.Add(level.levelId);return pack;
        }
        [Test] public async Task ApplySavesDraftThenInstallsWholePlayableSnapshotWithVerifiedWitnessAndRevision()
        {
            var pack=Draft();var service=new ApplyPackService(paths);var result=await service.ApplyAsync(pack);
            Assert.That(result.Applied,Is.True,result.Error);Assert.That(result.DraftSaved,Is.True);Assert.That(result.SnapshotHash,Is.EqualTo(DocumentHash.Compute(pack)));
            var installed=new InstalledPackRepository(paths).Load(pack.packId);ContentCatalog.ValidatePlayable(installed);Assert.That(installed.contentRevision,Is.EqualTo(1));
            Assert.That(new DraftRepository(paths).Load(pack.packId).Pack.documentKind,Is.EqualTo(DocumentKind.DraftPack));
            pack.name="第二版";result=await service.ApplyAsync(pack);Assert.That(result.InstalledPack.contentRevision,Is.EqualTo(2));
            Assert.That(StrictPackJson.Parse(File.ReadAllBytes(paths.Backup(StorageArea.Installed,pack.packId))).contentRevision,Is.EqualTo(1));
        }
        [Test] public async Task EmptyAndInvalidDraftsAreSavedButCannotReplaceInstalledContent()
        {
            var pack=Draft();var service=new ApplyPackService(paths);Assert.That((await service.ApplyAsync(pack)).Applied,Is.True);
            var original=File.ReadAllBytes(paths.Primary(StorageArea.Installed,pack.packId));pack.levels[0].entities.RemoveAll(e=>e.type==EntityType.Player);
            var result=await service.ApplyAsync(pack);Assert.That(result.Status,Is.EqualTo(ApplyStatus.Blocked));Assert.That(result.DraftSaved,Is.True);Assert.That(result.Problems[0].LevelId,Is.EqualTo(pack.levels[0].levelId));
            Assert.That(File.ReadAllBytes(paths.Primary(StorageArea.Installed,pack.packId)),Is.EqualTo(original));
            pack.levels.Clear();pack.levelOrder.Clear();result=await service.ApplyAsync(pack);Assert.That(result.Status,Is.EqualTo(ApplyStatus.Blocked));Assert.That(result.Problems,Is.Not.Empty);
        }
        [Test] public async Task InstalledReplaceFailureKeepsPreviousWholeVersionAndRetryAdvancesOnlyOnce()
        {
            var files=new FaultFileSystem();var pack=Draft();var service=new ApplyPackService(paths,files);
            Assert.That((await service.ApplyAsync(pack)).Applied,Is.True);var original=File.ReadAllBytes(paths.Primary(StorageArea.Installed,pack.packId));
            files.AfterReplace=()=>files.Failure="replace";pack.name="提交失败的新稿";var result=await service.ApplyAsync(pack);
            Assert.That(result.Status,Is.EqualTo(ApplyStatus.Failed));Assert.That(result.DraftSaved,Is.True);Assert.That(File.ReadAllBytes(paths.Primary(StorageArea.Installed,pack.packId)),Is.EqualTo(original));
            files.AfterReplace=null;files.Failure=null;result=await service.ApplyAsync(pack);Assert.That(result.Applied,Is.True);Assert.That(result.InstalledPack.contentRevision,Is.EqualTo(2));
        }
        [Test] public async Task DuplicateApplyCannotSubmitTwice()
        {
            var files=new FaultFileSystem{BlockNextFlush=true};var pack=Draft();var service=new ApplyPackService(paths,files);var first=service.ApplyAsync(pack);
            try
            {
                Assert.That(files.Entered.Wait(5000),Is.True);Assert.That(service.IsRunning,Is.True);
                Assert.That((await service.ApplyAsync(pack)).Status,Is.EqualTo(ApplyStatus.Busy));
            }
            finally{files.Release.Set();await first;files.Entered.Dispose();files.Release.Dispose();}
            Assert.That(first.Result.Applied,Is.True);Assert.That(new InstalledPackRepository(paths).Load(pack.packId).contentRevision,Is.EqualTo(1));
        }
        [Test] public async Task FailedRemovalPreservesPlayableFileAndDraftAndCanRetry()
        {
            var pack=Draft();Assert.That((await new ApplyPackService(paths).ApplyAsync(pack)).Applied,Is.True);
            var bytes=File.ReadAllBytes(paths.Primary(StorageArea.Installed,pack.packId));
            var files=new FaultFileSystem{Failure="permission"};var repository=new InstalledPackRepository(paths,files);
            var failed=await repository.RemoveAsync(pack.packId);
            Assert.That(failed.Committed,Is.False);
            Assert.That(File.ReadAllBytes(paths.Primary(StorageArea.Installed,pack.packId)),Is.EqualTo(bytes));
            Assert.That(new DraftRepository(paths).Load(pack.packId).Succeeded,Is.True);
            files.Failure=null;var removed=await repository.RemoveAsync(pack.packId);
            Assert.That(removed.Committed,Is.True);
            Assert.That(File.ReadAllBytes(removed.PreservedOriginalPath),Is.EqualTo(bytes));
            Assert.That(repository.Load(pack.packId),Is.Null);
            Assert.That((await repository.RemoveAsync(pack.packId)).Committed,Is.True);
        }
        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
        public async Task DraftRemovalRollsBackEveryMovedCopyOnFailureAndCanRetry(int failMoveAt)
        {
            var pack=Draft();Assert.That((await new ApplyPackService(paths).ApplyAsync(pack)).Applied,Is.True);
            var drafts=new DraftRepository(paths);await drafts.SaveAsync(pack);
            var recovery=new RecoveryService(paths);var document=new Sokoban.Domain.Workshop.WorkshopDocument(pack);
            await recovery.SaveAsync(document);await recovery.SaveAsync(document);
            string[] originals={paths.Primary(StorageArea.Drafts,pack.packId),paths.Backup(StorageArea.Drafts,pack.packId),
                paths.Primary(StorageArea.Recovery,pack.packId),paths.Backup(StorageArea.Recovery,pack.packId)};
            var bytes=originals.Select(File.ReadAllBytes).ToArray();
            var files=new FaultFileSystem{FailMoveAt=failMoveAt};var repository=new DraftRepository(paths,files);
            Assert.That((await repository.RemoveAsync(pack.packId)).Committed,Is.False);
            for(int i=0;i<originals.Length;i++)Assert.That(File.ReadAllBytes(originals[i]),Is.EqualTo(bytes[i]));
            var removed=await repository.RemoveAsync(pack.packId);
            Assert.That(removed.Committed,Is.True);
            Assert.That(Directory.GetFiles(removed.PreservedOriginalPath).Length,Is.EqualTo(4));
            Assert.That(originals.Any(File.Exists),Is.False);
            Assert.That(recovery.Enumerate(),Is.Empty);
            Assert.That(new InstalledPackRepository(paths).Load(pack.packId),Is.Not.Null);
            Assert.That((await repository.RemoveAsync(pack.packId)).Committed,Is.True);
        }
        [Test] public async Task DraftRemovalWaitsForAnExistingRecoveryWrite()
        {
            var pack=Draft();var drafts=new DraftRepository(paths);await drafts.SaveAsync(pack);
            var files=new FaultFileSystem{BlockNextFlush=true};var recovery=new RecoveryService(paths,files);
            var saving=recovery.SaveAsync(new Sokoban.Domain.Workshop.WorkshopDocument(pack));
            Task<TransactionResult> removal=null;
            try
            {
                Assert.That(files.Entered.Wait(5000),Is.True);
                removal=drafts.RemoveAsync(pack.packId);
                Assert.That(removal.IsCompleted,Is.False);
            }
            finally{files.Release.Set();await saving;if(removal!=null)await removal;files.Entered.Dispose();files.Release.Dispose();}
            Assert.That(removal.Result.Committed,Is.True);
            Assert.That(drafts.Load(pack.packId).Missing,Is.True);
            Assert.That(recovery.Enumerate(),Is.Empty);
        }
        sealed class HeldSolver:IAnalysisSolver,IDisposable
        {
            public readonly ManualResetEventSlim Entered=new ManualResetEventSlim(),Release=new ManualResetEventSlim();
            public AnalysisResult Solve(LevelData root,AnalysisBudget budget,CancellationToken token,Action<AnalysisProgress> progress)
            {Entered.Set();while(!Release.Wait(2))token.ThrowIfCancellationRequested();return new PushAStarSolver().Solve(root,budget,token,progress);}
            public void Dispose(){Entered.Dispose();Release.Dispose();}
        }
        [Test] public async Task ApplicationsKeepInvocationOrderWithoutOverwritingALaterManualSave()
        {
            using(var held=new HeldSolver())
            {
                var pack=Draft();var firstService=new ApplyPackService(paths,solver:held);var first=firstService.ApplyAsync(pack);Task<ApplyResult> second=null;
                try
                {
                    Assert.That(held.Entered.Wait(5000),Is.True);pack.name="应用二";second=new ApplyPackService(paths).ApplyAsync(pack);
                    pack.name="手动保存三";Assert.That((await new DraftRepository(paths).SaveAsync(pack)).Committed,Is.True);Assert.That(second.IsCompleted,Is.False);
                }
                finally{held.Release.Set();await first;if(second!=null)await second;}
                Assert.That(first.Result.InstalledPack.contentRevision,Is.EqualTo(1));Assert.That(second.Result.InstalledPack.contentRevision,Is.EqualTo(2));
                Assert.That(new InstalledPackRepository(paths).Load(pack.packId).name,Is.EqualTo("应用二"));Assert.That(new DraftRepository(paths).Load(pack.packId).Pack.name,Is.EqualTo("手动保存三"));
            }
        }
        [Test] public async Task CorruptInstalledContentAndBadWitnessCannotBeSilentlyPromoted()
        {
            var pack=Draft();var witness=new PushBfsSolver().Solve(pack.levels[0]).Witness;witness.moves="L";pack.solutionWitnesses.Add(witness);
            var result=await new ApplyPackService(paths,solver:new UnknownSolver()).ApplyAsync(pack);Assert.That(result.Status,Is.EqualTo(ApplyStatus.Blocked));
            Directory.CreateDirectory(paths.AreaDirectory(StorageArea.Installed));File.WriteAllText(paths.Primary(StorageArea.Installed,pack.packId),"corrupt");
            result=await new ApplyPackService(paths).ApplyAsync(pack);Assert.That(result.Status,Is.EqualTo(ApplyStatus.Failed));Assert.That(File.ReadAllText(paths.Primary(StorageArea.Installed,pack.packId)),Is.EqualTo("corrupt"));
        }
        sealed class UnknownSolver:IAnalysisSolver
        {
            public AnalysisResult Solve(LevelData root,AnalysisBudget budget,CancellationToken token,Action<AnalysisProgress> progress)=>new AnalysisResult{Outcome=AnalysisOutcome.Unknown,LevelFingerprint=LevelFingerprint.Compute(root),StopReason=AnalysisStopReason.TimeBudget,Explanation="测试预算耗尽"};
        }
        [Test] public async Task UnknownNeedsRealManualEvidenceAndReservedBuiltInIdsCannotBeInstalled()
        {
            var pack=Draft();var service=new ApplyPackService(paths,solver:new UnknownSolver());var result=await service.ApplyAsync(pack);
            Assert.That(result.Status,Is.EqualTo(ApplyStatus.Blocked));Assert.That(result.Problems[0].Outcome,Is.EqualTo(AnalysisOutcome.Unknown));Assert.That(new InstalledPackRepository(paths).Load(pack.packId),Is.Null);
            var witness=new PushBfsSolver().Solve(pack.levels[0]).Witness;witness.source=WitnessSource.Manual;
            Assert.That((await service.ApplyAsync(pack,new[]{witness})).Applied,Is.True);
            var reserved=new ApplyPackService(paths,reservedIds:new[]{pack.packId});Assert.That((await reserved.ApplyAsync(pack)).Applied,Is.False);
        }
        [Test] public async Task PreviouslyInstalledCurrentWitnessSurvivesRestartAndMetadataOnlyChanges()
        {
            var pack=Draft();var proof=new PushBfsSolver().Solve(pack.levels[0]).Witness;
            Assert.That((await new ApplyPackService(paths,solver:new UnknownSolver()).ApplyAsync(pack,new[]{proof})).Applied,Is.True);
            pack=new DraftRepository(paths).Load(pack.packId).Pack;Assert.That(pack.solutionWitnesses,Is.Empty);pack.name="只修改名字后再次应用";
            var result=await new ApplyPackService(paths,solver:new UnknownSolver()).ApplyAsync(pack);
            Assert.That(result.Applied,Is.True,"The installed route is still current and must be reverified instead of discarded.");Assert.That(result.InstalledPack.contentRevision,Is.EqualTo(2));
        }
        [Test] public async Task CapturesInputBeforeAsyncWorkAndDoesNotInstallAfterCancellation()
        {
            var pack=Draft();string name=pack.name;var service=new ApplyPackService(paths);var task=service.ApplyAsync(pack);pack.name="后来编辑";
            Assert.That((await task).InstalledPack.name,Is.EqualTo(name));
            var before=File.ReadAllBytes(paths.Primary(StorageArea.Installed,pack.packId));var result=await service.ApplyAsync(pack,token:new CancellationToken(true));
            Assert.That(result.Status,Is.EqualTo(ApplyStatus.Cancelled));Assert.That(File.ReadAllBytes(paths.Primary(StorageArea.Installed,pack.packId)),Is.EqualTo(before));
        }
    }
}
