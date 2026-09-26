using System;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using Sokoban.Domain.Analysis;
using Sokoban.Core.Identity;
using NUnit.Framework;
using Sokoban.Core.Data;
using Sokoban.Runtime.Persistence;
namespace Sokoban.Tests.EditMode.Persistence
{
 public sealed class ImportExportTests
 {
  string root; UserDataPaths paths;
  [SetUp] public void Setup(){root=Path.Combine(Path.GetTempPath(),"sokoban-u16-"+Guid.NewGuid().ToString("N"));paths=new UserDataPaths(root);Directory.CreateDirectory(root);}
  [TearDown] public void Cleanup(){Directory.Delete(root,true);}
  static PackData Draft(){var p=new PackData{name="中文关卡"};var l=AsciiLevelFactory.Create("######","#@ $.#","#    #","######");p.levels.Add(l);p.levelOrder.Add(l.levelId);return p;}
  [Test] public async Task DraftRoundTripDefaultsToIndependentCopyAndPreservesPolicy()
  {
   var pack=Draft();var service=new ImportExportService(paths);var path=Path.Combine(root,"中文 空格.sokopack.json");
   Assert.That((await service.ExportAsync(pack,path,false,false)).Committed,Is.True);
   var preview=await service.ReadAsync(path);var imported=await service.ImportAsync(preview);
   Assert.That(imported.Committed,Is.True);Assert.That(imported.Pack.packId,Is.Not.EqualTo(pack.packId));Assert.That(imported.Pack.levels[0].levelId,Is.Not.EqualTo(pack.levels[0].levelId));
   Assert.That(imported.Pack.unlockPolicy,Is.EqualTo(pack.unlockPolicy));Assert.That(new DraftRepository(paths).Load(imported.Pack.packId).Succeeded,Is.True);
  }
  [Test] public async Task PlayableExportInstallsIntoFreshRootOnlyAfterLocalProofAndSurvivesReload()
  {
   var pack=Draft();var service=new ImportExportService(paths);var path=Path.Combine(root,"可玩.sokopack.json");
   Assert.That((await service.ExportAsync(pack,path,true,false)).Committed,Is.True);
   var other=new UserDataPaths(Path.Combine(root,"fresh"));var importer=new ImportExportService(other);var imported=await importer.ImportAsync(await importer.ReadAsync(path));
   Assert.That(imported.Committed,Is.True,imported.Error);ContentCatalog.ValidatePlayable(new InstalledPackRepository(other).Load(imported.Pack.packId));
   Assert.That(new DraftRepository(paths).Load(pack.packId).Missing,Is.True,"Export must not apply or save source draft.");
  }
  [Test] public async Task OverwriteRequiresConsentAndInvalidPlayableDoesNotDamageExistingFile()
  {
   var service=new ImportExportService(paths);var path=Path.Combine(root,"旧.sokopack.json");var p=Draft();Assert.That((await service.ExportAsync(p,path,false,false)).Committed,Is.True);var old=File.ReadAllBytes(path);
   p.name="新版";Assert.That((await service.ExportAsync(p,path,false,false)).Committed,Is.False);Assert.That(File.ReadAllBytes(path),Is.EqualTo(old));
   p.levels[0].entities.Clear();Assert.That((await service.ExportAsync(p,path,true,true)).Committed,Is.False);Assert.That(File.ReadAllBytes(path),Is.EqualTo(old));
  }
  sealed class UnknownSolver:IAnalysisSolver
  {public AnalysisResult Solve(LevelData root,AnalysisBudget budget,CancellationToken token,Action<AnalysisProgress> progress)=>new AnalysisResult{Outcome=AnalysisOutcome.Unknown,StopReason=AnalysisStopReason.TimeBudget,LevelFingerprint=LevelFingerprint.Compute(root),Explanation="暂未判定"};}
  [Test] public async Task InvalidExternalProofStaysStagedAndCanBecomeOneIndependentDraft()
  {
   var pack=Draft();pack.documentKind=DocumentKind.PlayablePack;var w=new PushBfsSolver().Solve(pack.levels[0]).Witness;w.moves="L";pack.solutionWitnesses.Add(w);
   var service=new ImportExportService(paths,solver:new UnknownSolver());var pending=await service.ImportAsync(pack);
   Assert.That(pending.Committed,Is.False);Assert.That(pending.StagingId,Is.Not.Null);Assert.That(new InstalledPackRepository(paths).Load(pack.packId),Is.Null);
   var staged=service.Staging.Load(pending.StagingId);Assert.That(staged.packId,Is.Not.EqualTo(pack.packId));
   var draft=await service.ImportAsync(staged,asDraft:true,stagingId:pending.StagingId);Assert.That(draft.Committed,Is.True);Assert.That(draft.Pack.packId,Is.EqualTo(staged.packId));Assert.That(service.Staging.List(),Is.Empty);
  }
  [Test] public async Task BuiltinReplacementAndCancellationNeverOverwriteExistingContent()
  {
   var pack=Draft();var service=new ImportExportService(paths,reservedIds:new[]{pack.packId});var result=await service.ImportAsync(pack,replace:true);Assert.That(result.Committed,Is.False);Assert.That(new DraftRepository(paths).Load(pack.packId).Missing,Is.True);
   var token=new CancellationToken(true);result=await service.ImportAsync(pack,token:token);Assert.That(result.Committed,Is.False);Assert.That(service.Staging.List(),Is.Empty);
   bool rejected=false;try{await service.ReadAsync(Path.Combine(root,"bad.txt"));}catch(ArgumentException){rejected=true;}Assert.That(rejected,Is.True);
  }
  [Test] public async Task ExportReplaceFailurePreservesOriginalBytesAndGoodBackup()
  {
   var files=new FaultFileSystem();var service=new ImportExportService(paths,files);var pack=Draft();string path=Path.Combine(root,"fault.sokopack.json");Assert.That((await service.ExportAsync(pack,path,false,false)).Committed,Is.True);
   var original=File.ReadAllBytes(path);pack.name="新版本";files.Failure="replace";Assert.That((await service.ExportAsync(pack,path,false,true)).Committed,Is.False);Assert.That(File.ReadAllBytes(path),Is.EqualTo(original));Assert.That(File.ReadAllBytes(path+".bak"),Is.EqualTo(original));
  }

  [Test] public async Task ExportPermissionFailureAndMissingTargetDoNotClaimSuccess()
  {
   var files=new FaultFileSystem{Failure="permission"};var service=new ImportExportService(paths,files);string path=Path.Combine(root,"readonly.sokopack.json");var result=await service.ExportAsync(Draft(),path,false,false);Assert.That(result.Committed,Is.False);Assert.That(result.Error,Is.Not.Null);Assert.That(File.Exists(path),Is.False);
   result=await new ImportExportService(paths).ExportAsync(Draft(),Path.Combine(root,"missing","pack.sokopack.json"),false,false);Assert.That(result.Committed,Is.False);Assert.That(result.Error,Is.TypeOf<DirectoryNotFoundException>());
  }

 }
}
