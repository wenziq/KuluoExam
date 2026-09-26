using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Sokoban.Core.Data;
using Sokoban.Runtime.Persistence;
using Sokoban.Runtime.Platform.FileDialogs;
namespace Sokoban.Tests.EditMode.Persistence
{
 public sealed class ImportStagingTests
 {
  string root;[SetUp]public void Setup(){root=Path.Combine(Path.GetTempPath(),"sokoban-stage-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);}
  [TearDown]public void Cleanup(){Directory.Delete(root,true);}
  [Test]public async Task StagingIsSeparateSurvivesRestartAndCancellationDoesNotReplaceIt()
  {
   var paths=new UserDataPaths(root);var repo=new ImportStagingRepository(paths);var pack=new PackData{name="待验证"};Assert.That((await repo.SaveAsync("request1",pack)).Committed,Is.True);
   pack.name="取消的修改";Assert.That((await repo.SaveAsync("request1",pack,new CancellationToken(true))).Status,Is.EqualTo(TransactionStatus.Cancelled));
   var restarted=new ImportStagingRepository(paths);Assert.That(restarted.List(),Is.EqualTo(new[]{"request1"}));Assert.That(restarted.Load("request1").name,Is.EqualTo("待验证"));Assert.That(new DraftRepository(paths).Load(pack.packId).Missing,Is.True);Assert.That(new InstalledPackRepository(paths).Load(pack.packId),Is.Null);await restarted.RemoveAsync("request1");Assert.That(restarted.List(),Is.Empty);
  }
  [Test]public async Task DirectoryListingFiltersFilesBoundsLargeFoldersAndReportsMissingDirectories()
  {
   File.WriteAllText(Path.Combine(root,"中文.sokopack.json"),"data");File.WriteAllText(Path.Combine(root,"hidden.txt"),"data");Directory.CreateDirectory(Path.Combine(root,"子目录"));var listing=await new DirectoryListingService().ListAsync(root);
   Assert.That(listing.Files.Count,Is.EqualTo(1));Assert.That(listing.Directories.Count,Is.EqualTo(1));Assert.That(listing.Error,Is.Null);
   for(int i=0;i<220;i++)File.WriteAllText(Path.Combine(root,i+".sokopack.json"),"data");listing=await new DirectoryListingService().ListAsync(root);Assert.That(listing.Truncated,Is.True);Assert.That(listing.Files.Count+listing.Directories.Count,Is.LessThanOrEqualTo(200));
   listing=await new DirectoryListingService().ListAsync(Path.Combine(root,"missing"));Assert.That(listing.Error,Is.Not.Null);
  }
 }
}
