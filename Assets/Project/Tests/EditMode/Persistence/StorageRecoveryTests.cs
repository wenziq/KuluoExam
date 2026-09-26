using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Sokoban.Runtime.Persistence;

namespace Sokoban.Tests.EditMode.Persistence
{
    public class StorageRecoveryTests : StorageTestBase
    {
        [Test] public void InspectionClassifiesPrimaryBackupAndResidualTempsWithoutPromotingAnything()
        {
            Save("valid:backup");
            Save("valid:primary");
            var primary = Paths.Primary(StorageArea.Drafts, "doc1");
            File.WriteAllBytes(primary + ".leftover.tmp", Bytes("valid:temporary"));
            File.WriteAllBytes(primary + ".bad.tmp", Bytes("truncated"));
            var report = new StorageRecovery(Store).InspectAsync(StorageArea.Drafts, "doc1", Validate).GetAwaiter().GetResult();
            Assert.That(report.Candidates.Count, Is.EqualTo(4));
            Assert.That(report.Candidates.Count(c => c.Status == CandidateStatus.Valid), Is.EqualTo(3));
            Assert.That(report.Candidates.Single(c => c.Status == CandidateStatus.Corrupt).Path, Is.EqualTo(primary + ".bad.tmp"));
            Assert.That(Read(primary), Is.EqualTo("valid:primary"));
        }
        [Test] public void CorruptPrimaryCanRecoverBackupAndKeepsExactOriginalForDiagnosis()
        {
            Save("valid:backup");
            Save("valid:newer");
            var primary = Paths.Primary(StorageArea.Drafts, "doc1");
            File.WriteAllBytes(primary, Bytes("truncated"));
            var recovery = new StorageRecovery(Store);
            var report = recovery.InspectAsync(StorageArea.Drafts, "doc1", Validate).GetAwaiter().GetResult();
            Assert.That(report.Candidates.Single(c => c.Kind == CandidateKind.Primary).Status, Is.EqualTo(CandidateStatus.Corrupt));
            Assert.That(recovery.RecoverBackupAsync(StorageArea.Drafts, "doc1", Validate).GetAwaiter().GetResult().Committed, Is.True);
            Assert.That(Read(primary), Is.EqualTo("valid:backup"));
            Assert.That(Read(Paths.Backup(StorageArea.Drafts, "doc1")), Is.EqualTo("valid:backup"));
            Assert.That(Directory.GetFiles(Root, "*.corrupt", SearchOption.AllDirectories).Any(p => Read(p) == "truncated"), Is.True);
        }
        [Test] public void OversizedCorruptPrimaryIsPreservedByStreamingBeforeBackupRecovery()
        {
            Save("valid:backup");
            Save("valid:newer");
            byte[] oversized = new byte[100000];
            oversized[99999] = 123;
            var primary = Paths.Primary(StorageArea.Drafts, "doc1");
            File.WriteAllBytes(primary, oversized);
            var recovery = new StorageRecovery(Store);
            var report = recovery.InspectAsync(StorageArea.Drafts, "doc1", Validate).GetAwaiter().GetResult();
            Assert.That(report.Candidates.Single(c => c.Kind == CandidateKind.Primary).Status, Is.EqualTo(CandidateStatus.Corrupt));
            var result = recovery.RecoverBackupAsync(StorageArea.Drafts, "doc1", Validate).GetAwaiter().GetResult();
            Assert.That(result.Committed, Is.True);
            Assert.That(File.ReadAllBytes(result.PreservedOriginalPath), Is.EqualTo(oversized));
            Assert.That(Read(primary), Is.EqualTo("valid:backup"));
        }

        [Test] public void SavingOverCorruptPrimaryNeverReplacesGoodBackupWithCorruption()
        {
            Save("valid:backup");
            Save("valid:newer");
            File.WriteAllBytes(Paths.Primary(StorageArea.Drafts, "doc1"), Bytes("truncated"));
            Assert.That(Save("valid:replacement").Committed, Is.True);
            Assert.That(Read(Paths.Backup(StorageArea.Drafts, "doc1")), Is.EqualTo("valid:backup"));
        }
        [Test] public void MissingUnreadableAndCorruptBackupAreDifferentOutcomes()
        {
            var recovery = new StorageRecovery(Store);
            Assert.That(recovery.InspectAsync(StorageArea.Drafts, "doc1", Validate).GetAwaiter().GetResult().Candidates.All(c => c.Status == CandidateStatus.Missing), Is.True);
            Save("valid:one");
            Save("valid:two");
            FileSystem.Failure = "readpermission";
            Assert.That(recovery.InspectAsync(StorageArea.Drafts, "doc1", Validate).GetAwaiter().GetResult().Candidates.All(c => c.Status == CandidateStatus.Unreadable), Is.True);
            FileSystem.Failure = null;
            File.WriteAllBytes(Paths.Backup(StorageArea.Drafts, "doc1"), Bytes("corrupt"));
            Assert.That(recovery.RecoverBackupAsync(StorageArea.Drafts, "doc1", Validate).GetAwaiter().GetResult().Status, Is.EqualTo(TransactionStatus.Failed));
            Assert.That(Read(Paths.Primary(StorageArea.Drafts, "doc1")), Is.EqualTo("valid:two"));
        }
    }
}
