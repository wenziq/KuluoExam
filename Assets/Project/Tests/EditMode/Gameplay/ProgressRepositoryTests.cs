using System;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using Sokoban.Domain.Gameplay;
using Sokoban.Runtime.Persistence;
namespace Sokoban.Tests.EditMode.Gameplay
{
    public sealed class ProgressRepositoryTests
    {
        private string root;
        [SetUp] public void Setup() { root = Path.Combine(Path.GetTempPath(), "sokoban-u7-" + Guid.NewGuid().ToString("N")); }
        [TearDown] public void Cleanup() { if (Directory.Exists(root)) Directory.Delete(root, true); }
        [Test] public async Task RealFileRoundTripAndFailedSavePreservesPreviousRecord()
        {
            var paths = new UserDataPaths(root);
            var files = new FaultFiles();
            var repository = new ProgressRepository(paths, files);
            var progress = new ProgressService();
            var level = ProgressTests.Level("A");
            progress.Record("pack", ProgressTests.Win(level));
            Assert.That((await repository.SaveAsync(progress.Snapshot())).Committed, Is.True);
            var reloaded = new ProgressService(new ProgressRepository(paths).Load().Snapshot);
            Assert.That(reloaded.Best("pack", level).moves, Is.EqualTo(1));
            progress.RecentPackId = "new-pack";
            files.FailFlush = true;
            Assert.That((await repository.SaveAsync(progress.Snapshot())).Committed, Is.False);
            Assert.That(new ProgressRepository(paths).Load().Snapshot.recentPackId, Is.Empty);
            files.FailFlush = false;
            Assert.That((await repository.SaveAsync(progress.Snapshot())).Committed, Is.True);
            Assert.That(new ProgressRepository(paths).Load().Snapshot.recentPackId, Is.EqualTo("new-pack"));
        }
        [Test] public void CorruptLoadReportsFailureAndPreservesExactBytes()
        {
            var paths = new UserDataPaths(root);
            string path = paths.Primary(StorageArea.Progress, "player-progress-v1");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, "broken progress");
            var result = new ProgressRepository(paths).Load();
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Snapshot, Is.Null);
            Assert.That(File.ReadAllText(path), Is.EqualTo("broken progress"));
        }
        private sealed class FaultFiles : IFileSystem
        {
            private readonly PhysicalFileSystem inner = new PhysicalFileSystem();
            public bool FailFlush;
            public void CreateDirectory(string path) => inner.CreateDirectory(path);
            public bool FileExists(string path) => inner.FileExists(path);
            public Stream CreateNew(string path) => inner.CreateNew(path);
            public void Flush(Stream stream) { if (FailFlush) throw new IOException("Injected flush failure"); inner.Flush(stream); }
            public byte[] ReadAllBytes(string path, int maxBytes) => inner.ReadAllBytes(path, maxBytes);
            public string[] GetFiles(string directory, string pattern) => inner.GetFiles(directory, pattern);
            public void CopyNew(string source, string destination) => inner.CopyNew(source, destination);
            public void Move(string source, string destination) => inner.Move(source, destination);
            public void Replace(string source, string destination) => inner.Replace(source, destination);
            public void Delete(string path) => inner.Delete(path);
        }
    }
}
