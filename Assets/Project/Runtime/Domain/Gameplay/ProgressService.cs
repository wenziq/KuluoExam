using System;
using System.Collections.Generic;
using Sokoban.Core.Data;
namespace Sokoban.Domain.Gameplay
{
    [Serializable] public sealed class CompletionRecord
    {
        public string packId, levelId, fingerprint, path;
        public int moves, pushes;
        public CompletionRecord Copy() => (CompletionRecord)MemberwiseClone();
    }
    [Serializable] public sealed class ProgressSnapshot
    {
        public int version = 1;
        public string recentPackId = "";
        public List<CompletionRecord> records = new List<CompletionRecord>();
    }
    public sealed class ProgressService
    {
        private readonly Dictionary<string, CompletionRecord> records = new Dictionary<string, CompletionRecord>(StringComparer.Ordinal);
        // Records are privately owned; a content-keyed replay result remains valid until that record is replaced.
        private readonly Dictionary<string, bool> verifiedRecords = new Dictionary<string, bool>(StringComparer.Ordinal);
        public string RecentPackId { get; set; } = "";
        public ProgressService(ProgressSnapshot snapshot = null)
        {
            if (snapshot == null) return;
            RecentPackId = snapshot.recentPackId ?? "";
            foreach (var record in snapshot.records)
                records[Key(record.packId, record.levelId, record.fingerprint)] = record.Copy();
        }
        private static string Key(string pack, string level, string fingerprint) => pack + "\n" + level + "\n" + fingerprint;
        public CompletionRecord Best(string packId, LevelData level)
        {
            string fingerprint = Sokoban.Core.Identity.LevelFingerprint.Compute(level);
            string key = Key(packId, level.levelId, fingerprint);
            if (!records.TryGetValue(key, out var record)) return null;
            if (!verifiedRecords.TryGetValue(key, out bool valid))
            {
                var witness = new WitnessData { levelId = level.levelId, levelFingerprint = fingerprint, moves = record.path };
                var verification = Sokoban.Core.Rules.WitnessVerifier.Verify(level, witness);
                valid = verification.IsValid && verification.Moves == record.moves && verification.Pushes == record.pushes;
                verifiedRecords[key] = valid;
            }
            if (!valid) return null;
            return record.Copy();
        }
        public bool Record(string packId, GameSession session)
        {
            if (session.Mode != SessionMode.Formal || !session.IsCompleted) return false;
            var witness = session.CreateWitness();
            string key = Key(packId, witness.levelId, witness.levelFingerprint);
            var previous = Best(packId, session.InitialState.ToLevelData());
            if (previous != null && (previous.pushes < session.Pushes || previous.pushes == session.Pushes && previous.moves <= session.Moves)) return false;
            verifiedRecords.Remove(key);
            records[key] = new CompletionRecord { packId = packId, levelId = witness.levelId, fingerprint = witness.levelFingerprint, path = session.Path, moves = session.Moves, pushes = session.Pushes };
            return true;
        }
        public ProgressSnapshot Snapshot()
        {
            var snapshot = new ProgressSnapshot { recentPackId = RecentPackId };
            foreach (var record in records.Values) snapshot.records.Add(record.Copy());
            return snapshot;
        }
    }
}
