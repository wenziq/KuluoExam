using System;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using Sokoban.Runtime.Persistence;
namespace Sokoban.Tests.EditMode.Persistence
{
 public sealed class SettingsTests
 {
  string root;UserDataPaths paths;[SetUp]public void Setup(){root=Path.Combine(Path.GetTempPath(),"sokoban-settings-"+Guid.NewGuid().ToString("N"));paths=new UserDataPaths(root);}
  [TearDown]public void Cleanup(){if(Directory.Exists(root))Directory.Delete(root,true);}
  [Test]public async Task SettingsPersistSnapshotAndCorruptionFallsBackWithoutDestroyingOriginal()
  {
   var repository=new SettingsRepository(paths);var value=new GameSettings{volume=.6f,sound=false,fullscreen=true};var save=repository.SaveAsync(value);value.volume=.1f;Assert.That((await save).Committed,Is.True);
   var loaded=new SettingsRepository(paths).Load();Assert.That(loaded.Settings.volume,Is.EqualTo(.6f));Assert.That(loaded.Settings.sound,Is.False);Assert.That(loaded.Settings.fullscreen,Is.True);
   string file=paths.Primary(StorageArea.Settings,"preferences");File.WriteAllText(file,"broken");loaded=repository.Load();Assert.That(loaded.Error,Is.Not.Null);Assert.That(loaded.Settings.volume,Is.EqualTo(.35f));Assert.That(File.ReadAllText(file),Is.EqualTo("broken"));
   Assert.That((await repository.SaveAsync(loaded.Settings)).Committed,Is.True);Assert.That(Directory.GetFiles(Path.GetDirectoryName(paths.Backup(StorageArea.Settings,"preferences")),"*.corrupt").Length,Is.EqualTo(1));
  }
  [Test]public async Task InvalidSettingsAndWriteFailureDoNotReplaceLastGoodPreferences()
  {
   var files=new FaultFileSystem();var repo=new SettingsRepository(paths,files);Assert.That((await repo.SaveAsync(new GameSettings{volume=.2f})).Committed,Is.True);
   Assert.That((await repo.SaveAsync(new GameSettings{volume=float.NaN})).Committed,Is.False);files.Failure="replace";Assert.That((await repo.SaveAsync(new GameSettings{volume=.9f})).Committed,Is.False);Assert.That(repo.Load().Settings.volume,Is.EqualTo(.2f));
  }
 }
}
