using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Sokoban.Runtime.Persistence
{
    public enum CandidateKind { Primary, Backup, ResidualTemporary }
    public enum CandidateStatus { Missing, Valid, Corrupt, Unreadable }

    public sealed class RecoveryCandidate
    {
        public string Path { get; internal set; }
        public CandidateKind Kind { get; internal set; }
        public CandidateStatus Status { get; internal set; }
        public Exception Error { get; internal set; }
    }

    public sealed class RecoveryReport
    {
        public IReadOnlyList<RecoveryCandidate> Candidates { get; internal set; }
        public Exception EnumerationError { get; internal set; }
    }

    /// <summary>Inspection never changes files. Backup promotion requires an explicit separate call.</summary>
    public sealed class StorageRecovery
    {
        private readonly TransactionalFileStore store;

        public StorageRecovery(TransactionalFileStore store)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public Task<RecoveryReport> InspectAsync(StorageArea area, string id, Action<byte[]> validate)
        {
            if (validate == null) throw new ArgumentNullException(nameof(validate));
            string primary = store.Paths.Primary(area, id);
            string backup = store.Paths.Backup(area, id);
            return store.Enqueue(primary, () =>
            {
                var candidates = new List<RecoveryCandidate>
                {
                    Inspect(primary, CandidateKind.Primary, validate),
                    Inspect(backup, CandidateKind.Backup, validate)
                };
                var errors = new List<Exception>();
                CollectTemporary(primary, validate, candidates, errors);
                CollectTemporary(backup, validate, candidates, errors);
                return new RecoveryReport
                {
                    Candidates = candidates.AsReadOnly(),
                    EnumerationError = errors.Count == 0 ? null : new AggregateException(errors)
                };
            });
        }

        public Task<TransactionResult> RecoverBackupAsync(StorageArea area, string id, Action<byte[]> validate, CancellationToken cancellationToken = default)
        {
            if (validate == null) throw new ArgumentNullException(nameof(validate));
            string primary = store.Paths.Primary(area, id);
            string backup = store.Paths.Backup(area, id);
            return store.Enqueue(primary, () =>
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    byte[] bytes = store.Files.ReadAllBytes(backup, store.MaxBytes);
                    // Revalidate at recovery time, rather than trusting an earlier inspection.
                    validate((byte[])bytes.Clone());
                    return store.Commit(area, id, bytes, validate, cancellationToken, false);
                }
                catch (OperationCanceledException error)
                {
                    return new TransactionResult { Status = TransactionStatus.Cancelled, Error = error };
                }
                catch (Exception error)
                {
                    return new TransactionResult { Status = TransactionStatus.Failed, Error = error };
                }
            });
        }

        private RecoveryCandidate Inspect(string path, CandidateKind kind, Action<byte[]> validate)
        {
            var candidate = new RecoveryCandidate { Path = path, Kind = kind };
            byte[] bytes;
            try
            {
                if (!store.Files.FileExists(path)) return candidate;
                bytes = store.Files.ReadAllBytes(path, store.MaxBytes);
            }
            catch (InvalidDataException error)
            {
                candidate.Status = CandidateStatus.Corrupt;
                candidate.Error = error;
                return candidate;
            }
            catch (Exception error)
            {
                candidate.Status = CandidateStatus.Unreadable;
                candidate.Error = error;
                return candidate;
            }
            try
            {
                validate(bytes);
                candidate.Status = CandidateStatus.Valid;
            }
            catch (Exception error)
            {
                candidate.Status = CandidateStatus.Corrupt;
                candidate.Error = error;
            }
            return candidate;
        }

        private void CollectTemporary(string destination, Action<byte[]> validate, List<RecoveryCandidate> candidates, List<Exception> errors)
        {
            try
            {
                string[] paths = store.Files.GetFiles(Path.GetDirectoryName(destination), Path.GetFileName(destination) + ".*.tmp");
                Array.Sort(paths, StringComparer.Ordinal);
                foreach (string path in paths)
                {
                    candidates.Add(Inspect(path, CandidateKind.ResidualTemporary, validate));
                }
            }
            catch (Exception error) { errors.Add(error); }
        }
    }
}
