using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Sokoban.Runtime.Persistence;

namespace Sokoban.Tests.EditMode.Persistence
{
    public abstract class StorageTestBase
    {
        internal string Root;
        internal UserDataPaths Paths;
        internal FaultFileSystem FileSystem;
        internal TransactionalFileStore Store;
        internal static byte[] Bytes(string value) { return Encoding.UTF8.GetBytes(value); }
        internal static void Validate(byte[] bytes)
        {
            if (!Encoding.UTF8.GetString(bytes).StartsWith("valid:")) throw new FormatException("Invalid fixture");
        }
        [SetUp] public void SetUp()
        {
            Root = Path.Combine(Path.GetTempPath(), "sokoban-u5-" + Guid.NewGuid().ToString("N"));
            Paths = new UserDataPaths(Root);
            FileSystem = new FaultFileSystem();
            Store = new TransactionalFileStore(Paths, FileSystem, 1024);
        }
        [TearDown] public void TearDown()
        {
            FileSystem.Release.Set();
            if (Directory.Exists(Root)) Directory.Delete(Root, true);
        }
        internal TransactionResult Save(string value, string id = "doc1", CancellationToken token = default)
        {
            return Store.SaveAsync(StorageArea.Drafts, id, Bytes(value), Validate, token).GetAwaiter().GetResult();
        }
        internal string Read(string path) { return Encoding.UTF8.GetString(File.ReadAllBytes(path)); }

    }

    public class TransactionalStoreTests : StorageTestBase
    {
        [Test] public void StrictPackJsonRoundTripsThroughRealTransactionalStorage()
        {
            const string json = "{\"formatVersion\":1,\"rulesVersion\":1,\"documentKind\":\"DraftPack\",\"packId\":\"p1\",\"name\":\"中文\",\"description\":\"\",\"contentRevision\":0,\"unlockPolicy\":\"Sequential\",\"levels\":[],\"levelOrder\":[],\"solutionWitnesses\":[]}";
            var original = StrictPackJson.Parse(json);
            var result = Store.SaveAsync(StorageArea.Drafts, original.packId, StrictPackJson.Serialize(original), b => StrictPackJson.Parse(b)).GetAwaiter().GetResult();
            Assert.That(result.Committed, Is.True);
            var loaded = StrictPackJson.Parse(File.ReadAllBytes(Paths.Primary(StorageArea.Drafts, original.packId)));
            Assert.That(loaded.packId, Is.EqualTo(original.packId));
            Assert.That(loaded.name, Is.EqualTo("中文"));
        }
        [Test] public void FirstSaveCreatesDirectoryAndReplacementKeepsPreviousValidBackup()
        {
            Assert.That(Directory.Exists(Root), Is.False);
            Assert.That(Save("valid:one").Committed, Is.True);
            Assert.That(Save("valid:two").Committed, Is.True);
            Assert.That(Read(Paths.Primary(StorageArea.Drafts, "doc1")), Is.EqualTo("valid:two"));
            Assert.That(Read(Paths.Backup(StorageArea.Drafts, "doc1")), Is.EqualTo("valid:one"));
        }
        [TestCase("write")]
        [TestCase("flush")]
        [TestCase("readback")]
        [TestCase("backup")]
        [TestCase("replace")]
        [TestCase("permission")]
        public void PrecommitFailureLeavesOldContentRecoverable(string failure)
        {
            Assert.That(Save("valid:old").Committed, Is.True);
            FileSystem.Failure = failure;
            var result = Save("valid:new");
            Assert.That(result.Status, Is.EqualTo(TransactionStatus.Failed));
            Assert.That(result.Error, Is.Not.Null);
            Assert.That(Read(Paths.Primary(StorageArea.Drafts, "doc1")), Is.EqualTo("valid:old"));
        }
        [Test] public void InvalidReadbackNeverCommits()
        {
            Save("valid:old");
            var result = Save("broken");
            Assert.That(result.Status, Is.EqualTo(TransactionStatus.Failed));
            Assert.That(Read(Paths.Primary(StorageArea.Drafts, "doc1")), Is.EqualTo("valid:old"));
        }
        [Test] public void OversizedInputFailsWithoutReplacingPrimary()
        {
            Save("valid:old");
            Assert.That(Save("valid:" + new string('x', 1024)).Status, Is.EqualTo(TransactionStatus.Failed));
            Assert.That(Read(Paths.Primary(StorageArea.Drafts, "doc1")), Is.EqualTo("valid:old"));
        }
        [Test] public void CancellationBeforeCommitPreservesOldAndAfterCommitReportsCommitted()
        {
            Save("valid:old");
            using (var before = new CancellationTokenSource())
            {
                var result = Store.SaveAsync(StorageArea.Drafts, "doc1", Bytes("valid:cancel"), b => { Validate(b); before.Cancel(); }, before.Token).GetAwaiter().GetResult();
                Assert.That(result.Status, Is.EqualTo(TransactionStatus.Cancelled));
                Assert.That(Read(Paths.Primary(StorageArea.Drafts, "doc1")), Is.EqualTo("valid:old"));
            }
            using (var after = new CancellationTokenSource())
            {
                FileSystem.AfterReplace = () => after.Cancel();
                Assert.That(Save("valid:committed", token: after.Token).Committed, Is.True);
                Assert.That(after.IsCancellationRequested, Is.True);
                Assert.That(Read(Paths.Primary(StorageArea.Drafts, "doc1")), Is.EqualTo("valid:committed"));
            }
        }
        [Test] public void SameDocumentWritesStayOrderedCloneQueuedInputAndDoNotBlockOtherIds()
        {
            FileSystem.BlockNextFlush = true;
            var first = Store.SaveAsync(StorageArea.Drafts, "doc1", Bytes("valid:S1"), Validate);
            Task<TransactionResult> second = null;
            try
            {
                Assert.That(FileSystem.Entered.Wait(5000), Is.True);
                var mutable = Bytes("valid:S2");
                var secondStore = new TransactionalFileStore(Paths, FileSystem, 1024);
                second = secondStore.SaveAsync(StorageArea.Drafts, "doc1", mutable, Validate);
                mutable[mutable.Length - 1] = (byte)'X';
                var independent = Store.SaveAsync(StorageArea.Drafts, "doc2", Bytes("valid:other"), Validate);
                Assert.That(independent.Wait(5000), Is.True);
                Assert.That(independent.Result.Committed, Is.True);
                Assert.That(second.IsCompleted, Is.False);
            }
            finally
            {
                FileSystem.Release.Set();
                first.GetAwaiter().GetResult();
                if (second != null) second.GetAwaiter().GetResult();
            }
            Assert.That(first.Result.Committed, Is.True);
            Assert.That(second.Result.Committed, Is.True);
            Assert.That(Read(Paths.Primary(StorageArea.Drafts, "doc1")), Is.EqualTo("valid:S2"));
            Assert.That(Read(Paths.Backup(StorageArea.Drafts, "doc1")), Is.EqualTo("valid:S1"));
            Assert.That(Read(Paths.Primary(StorageArea.Drafts, "doc2")), Is.EqualTo("valid:other"));
        }
        [TestCase("../escape")]
        [TestCase("a/b")]
        [TestCase("a\\b")]
        [TestCase("..")]
        [TestCase("a:b")]
        [TestCase("")]
        public void IdCannotBecomeAnExternalPath(string id)
        {
            Assert.Throws<ArgumentException>(() => Paths.Primary(StorageArea.Drafts, id));
        }
        [Test] public void ProtocolIdsIncludingWindowsDeviceNamesAndCaseVariantsStayDistinct()
        {
            Assert.That(Save("valid:device", "CON").Committed, Is.True);
            Assert.That(Path.GetFileName(Paths.Primary(StorageArea.Drafts, "CON")).Split('.')[0], Is.Not.EqualTo("CON").IgnoreCase);
            Assert.That(Save("valid:lower", "doc").Committed, Is.True);
            Assert.That(Save("valid:upper", "DOC").Committed, Is.True);
            Assert.That(Read(Paths.Primary(StorageArea.Drafts, "doc")), Is.EqualTo("valid:lower"));
            Assert.That(Read(Paths.Primary(StorageArea.Drafts, "DOC")), Is.EqualTo("valid:upper"));
        }
        [Test] public void RealPhysicalReadHasByteLimit()
        {
            Directory.CreateDirectory(Root);
            var path = Path.Combine(Root, "oversized");
            File.WriteAllBytes(path, new byte[1025]);
            Assert.Throws<InvalidDataException>(() => new PhysicalFileSystem().ReadAllBytes(path, 1024));
        }
        [Test] public void LogRotationKeepsBoundedFilesAndBytes()
        {
            var log = new DiagnosticLog(Paths, 128, 3);
            for (int i = 0; i < 20; i++) log.Write(new string('中', 200));
            var files = Directory.GetFiles(Paths.LogDirectory);
            Assert.That(files.Length, Is.InRange(1, 3));
            Assert.That(files.All(p => new FileInfo(p).Length <= 128), Is.True);
        }
    }

    internal sealed class FaultFileSystem : IFileSystem
    {
        private readonly PhysicalFileSystem physical = new PhysicalFileSystem();
        public string Failure;
        public bool BlockNextFlush;
        public Action AfterReplace;
        public int FailMoveAt;
        private int moveCount;
        public readonly ManualResetEventSlim Entered = new ManualResetEventSlim();
        public readonly ManualResetEventSlim Release = new ManualResetEventSlim();
        public void CreateDirectory(string path)
        {
            if (Failure == "permission") throw new UnauthorizedAccessException("Injected directory denial");
            physical.CreateDirectory(path);
        }
        public bool FileExists(string path) { return physical.FileExists(path); }
        public Stream CreateNew(string path)
        {
            if (Failure == "backup" && path.Contains(Path.DirectorySeparatorChar + "Backups" + Path.DirectorySeparatorChar)) throw new IOException("Injected backup failure");
            var stream = physical.CreateNew(path);
            return Failure == "write" ? new FailingWriteStream(stream) : stream;
        }
        public void Flush(Stream stream)
        {
            if (Failure == "flush") throw new IOException("Injected flush failure");
            if (BlockNextFlush)
            {
                BlockNextFlush = false;
                Entered.Set();
                if (!Release.Wait(10000)) throw new TimeoutException("Test did not release writer");
            }
            physical.Flush(stream);
        }
        public byte[] ReadAllBytes(string path, int maxBytes)
        {
            if (Failure == "readback" && path.EndsWith(".tmp", StringComparison.Ordinal)) throw new IOException("Injected readback failure");
            if (Failure == "readpermission") throw new UnauthorizedAccessException("Injected read denial");
            return physical.ReadAllBytes(path, maxBytes);
        }
        public string[] GetFiles(string directory, string pattern) { return physical.GetFiles(directory, pattern); }
        public void CopyNew(string source, string destination) { physical.CopyNew(source, destination); }
        public void Move(string source, string destination)
        {
            if(++moveCount==FailMoveAt)throw new IOException("Injected move failure");
            physical.Move(source, destination);
        }
        public void Replace(string source, string destination)
        {
            if (Failure == "replace") throw new IOException("Injected replace failure");
            physical.Replace(source, destination);
            AfterReplace?.Invoke();
        }
        public void Delete(string path) { physical.Delete(path); }
        private sealed class FailingWriteStream : Stream
        {
            private readonly Stream inner;
            public FailingWriteStream(Stream inner) { this.inner = inner; }
            public override bool CanRead => false;
            public override bool CanSeek => false;
            public override bool CanWrite => true;
            public override long Length => inner.Length;
            public override long Position { get => inner.Position; set => throw new NotSupportedException(); }
            public override void Flush() { inner.Flush(); }
            public override int Read(byte[] b, int o, int c) { throw new NotSupportedException(); }
            public override long Seek(long offset, SeekOrigin origin) { throw new NotSupportedException(); }
            public override void SetLength(long value) { inner.SetLength(value); }
            public override void Write(byte[] b, int o, int c) { throw new IOException("Injected disk write failure"); }
            protected override void Dispose(bool disposing)
            {
                if (disposing) inner.Dispose();
                base.Dispose(disposing);
            }
        }
    }
}
