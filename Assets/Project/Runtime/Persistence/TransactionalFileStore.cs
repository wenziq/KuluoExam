using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Sokoban.Runtime.Persistence
{
    public enum TransactionStatus { Failed, Cancelled, Committed }
    public enum TransactionStage { Preparing, Writing, Readback, Backup, Commit }

    public sealed class TransactionResult
    {
        public TransactionStatus Status { get; internal set; }
        public TransactionStage Stage { get; internal set; }
        public Exception Error { get; internal set; }
        public string PreservedOriginalPath { get; internal set; }
        public bool Committed => Status == TransactionStatus.Committed;
    }

    /// <summary>
    /// One-file transactions. Validators throw when bytes are invalid and must have no side effects.
    /// Calls for a document execute in invocation order, including across store instances.
    /// The caller associates each result with its own immutable document hash/save point.
    /// </summary>
    public sealed class TransactionalFileStore
    {
        private static readonly object QueueGate = new object();
        private static readonly Dictionary<string, TaskCompletionSource<bool>> Tails =
            new Dictionary<string, TaskCompletionSource<bool>>(StringComparer.OrdinalIgnoreCase);
        internal UserDataPaths Paths { get; }
        internal IFileSystem Files { get; }
        internal int MaxBytes { get; }

        public TransactionalFileStore(UserDataPaths paths, IFileSystem fileSystem = null, int maxBytes = 8388608)
        {
            Paths = paths ?? throw new ArgumentNullException(nameof(paths));
            Files = fileSystem ?? new PhysicalFileSystem();
            if (maxBytes < 1) throw new ArgumentOutOfRangeException(nameof(maxBytes));
            MaxBytes = maxBytes;
        }

        public Task<TransactionResult> SaveAsync(StorageArea area, string id, byte[] bytes, Action<byte[]> validate, CancellationToken cancellationToken = default)
        {
            string primary = Paths.Primary(area, id);
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            if (validate == null) throw new ArgumentNullException(nameof(validate));
            if (bytes.Length > MaxBytes)
            {
                return Task.FromResult(new TransactionResult { Error = new InvalidDataException("文件超过写入字节上限。") });
            }
            // This executes synchronously, before the write joins the queue or awaits anything.
            var snapshot = (byte[])bytes.Clone();
            return Enqueue(primary, () => Commit(area, id, snapshot, validate, cancellationToken, true));
        }

        internal Task<T> Enqueue<T>(string primary, Func<T> operation)
        {
            Task predecessor;
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (QueueGate)
            {
                predecessor = Tails.TryGetValue(primary, out var tail) ? tail.Task : Task.CompletedTask;
                Tails[primary] = completion;
            }
            return Task.Run(async () =>
            {
                await predecessor.ConfigureAwait(false);
                try { return operation(); }
                finally
                {
                    lock (QueueGate)
                    {
                        // Remove only our own tail. A newly queued successor retains its gate.
                        if (Tails.TryGetValue(primary, out var tail) && ReferenceEquals(tail, completion))
                        {
                            Tails.Remove(primary);
                        }
                        completion.TrySetResult(true);
                    }
                }
            });
        }

        internal TransactionResult Commit(StorageArea area, string id, byte[] snapshot, Action<byte[]> validate, CancellationToken token, bool updateBackup)
        {
            return CommitPath(Paths.Primary(area, id), Paths.Backup(area, id), snapshot, validate, token, updateBackup);
        }

        internal TransactionResult CommitPath(string primary, string backup, byte[] snapshot, Action<byte[]> validate, CancellationToken token, bool updateBackup)
        {
            var result = new TransactionResult();
            string temporary = Paths.Temporary(primary);
            try
            {
                token.ThrowIfCancellationRequested();
                Files.CreateDirectory(Path.GetDirectoryName(primary));
                result.Stage = TransactionStage.Writing;
                WriteNew(temporary, snapshot);
                result.Stage = TransactionStage.Readback;
                byte[] readback = Files.ReadAllBytes(temporary, MaxBytes);
                if (!Equal(snapshot, readback)) throw new InvalidDataException("临时文件读回内容不一致。");
                validate(readback);
                token.ThrowIfCancellationRequested();
                result.Stage = TransactionStage.Backup;
                bool existed = Files.FileExists(primary);
                if (existed)
                {
                    byte[] previous = null;
                    bool previousValid = false;
                    try { previous = Files.ReadAllBytes(primary, MaxBytes); }
                    catch (InvalidDataException) { /* Preserve oversized corruption by streaming below. */ }
                    if (previous != null)
                    {
                        try
                        {
                            validate((byte[])previous.Clone());
                            previousValid = true;
                        }
                        catch (Exception) { previousValid = false; }
                    }

                    if (previousValid && updateBackup)
                    {
                        WriteSidecar(backup, previous);
                    }
                    else
                    {
                        // A corrupt primary never overwrites a good backup. Explicit recovery also
                        // retains the displaced primary and leaves the selected backup unchanged.
                        string preserved = backup + "." + Guid.NewGuid().ToString("N") + ".corrupt";
                        PreserveOriginal(primary, preserved);
                        result.PreservedOriginalPath = preserved;
                    }
                }
                token.ThrowIfCancellationRequested();
                result.Stage = TransactionStage.Commit;
                // No cancellation checks after entering this boundary: report the real commit.
                if (existed) Files.Replace(temporary, primary);
                else Files.Move(temporary, primary);
                result.Status = TransactionStatus.Committed;
            }
            catch (Exception error)
            {
                // An adapter may observe cancellation immediately after the OS has renamed.
                // Confirm the resulting file instead of misreporting that commit as cancelled.
                if (result.Stage == TransactionStage.Commit && IsCommitted(temporary, primary, snapshot))
                {
                    result.Status = TransactionStatus.Committed;
                }
                else
                {
                    result.Status = error is OperationCanceledException ? TransactionStatus.Cancelled : TransactionStatus.Failed;
                    result.Error = error;
                }
            }
            finally
            {
                TryDelete(temporary);
            }
            return result;
        }

        internal void WriteNew(string path, byte[] bytes)
        {
            using (Stream stream = Files.CreateNew(path))
            {
                stream.Write(bytes, 0, bytes.Length);
                Files.Flush(stream);
            }
        }

        private void WriteSidecar(string path, byte[] bytes)
        {
            Files.CreateDirectory(Path.GetDirectoryName(path));
            string temporary = Paths.Temporary(path);
            try
            {
                WriteNew(temporary, bytes);
                if (!Equal(bytes, Files.ReadAllBytes(temporary, MaxBytes)))
                {
                    throw new InvalidDataException("备份读回内容不一致。");
                }
                if (Files.FileExists(path)) Files.Replace(temporary, path);
                else Files.Move(temporary, path);
            }
            finally { TryDelete(temporary); }
        }

        private void PreserveOriginal(string source, string destination)
        {
            Files.CreateDirectory(Path.GetDirectoryName(destination));
            string temporary = Paths.Temporary(destination);
            try
            {
                Files.CopyNew(source, temporary);
                Files.Move(temporary, destination);
            }
            finally { TryDelete(temporary); }
        }

        private bool IsCommitted(string temporary, string primary, byte[] expected)
        {
            try
            {
                return !Files.FileExists(temporary) && Equal(expected, Files.ReadAllBytes(primary, MaxBytes));
            }
            catch (Exception) { return false; }
        }

        private void TryDelete(string path)
        {
            try { Files.Delete(path); }
            catch (Exception) { /* A residual temp is inspected on next recovery scan. */ }
        }

        private static bool Equal(byte[] left, byte[] right)
        {
            if (left.Length != right.Length) return false;
            for (int i = 0; i < left.Length; i++)
            {
                if (left[i] != right[i]) return false;
            }
            return true;
        }
    }
}
