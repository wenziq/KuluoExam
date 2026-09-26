using System;
using System.Collections.Generic;
using Sokoban.Core.Data;
using Sokoban.Core.Identity;
using Sokoban.Core.Rules;
using Sokoban.Core.Validation;
namespace Sokoban.Domain.Analysis
{
    /// <summary>Main-thread LRU. Verified path evidence survives failed jobs; job statistics are historical.</summary>
    public sealed class AnalysisCache
    {
        sealed class Entry
        {
            public string Fingerprint, Version, EvidenceVersion;
            public AnalysisResult Latest, Evidence;
            public int Pushes, Moves;
            public long Bytes;
        }
        readonly int capacity;
        readonly long byteCapacity;
        readonly LinkedList<Entry> lru = new LinkedList<Entry>();
        readonly Dictionary<string, LinkedListNode<Entry>> entries = new Dictionary<string, LinkedListNode<Entry>>();
        public AnalysisCache(int capacity = 64, long byteCapacity = 8 * 1024 * 1024)
        {
            if (capacity < 1 || byteCapacity < 1) throw new ArgumentOutOfRangeException();
            this.capacity = capacity; this.byteCapacity = byteCapacity;
        }
        public int Count => entries.Count;
        public long EstimatedBytes { get; private set; }
        public void Record(LevelData root, AnalysisResult result, string algorithmVersion)
        {
            if (result == null || string.IsNullOrEmpty(algorithmVersion)) throw new ArgumentException("分析结果或算法版本缺失。");
            if (!StructureValidator.Validate(root).IsValid) return;
            string fingerprint = LevelFingerprint.Compute(root);
            if (result.LevelFingerprint != fingerprint) throw new ArgumentException("分析结果与根布局不一致。");
            WitnessVerification verified = null;
            if (result.Outcome == AnalysisOutcome.Solvable)
            {
                verified = WitnessVerifier.Verify(root, result.Witness);
                if (!verified.IsValid) throw new ArgumentException("不能缓存无效通关证据：" + verified.Reason);
            }
            if (!entries.TryGetValue(fingerprint, out var node))
            {
                node = lru.AddFirst(new Entry { Fingerprint = fingerprint }); entries.Add(fingerprint, node);
            }
            else { lru.Remove(node); lru.AddFirst(node); EstimatedBytes -= node.Value.Bytes; }
            var entry = node.Value; entry.Version = algorithmVersion; entry.Latest = result.Copy();
            if (verified != null && (entry.Evidence == null || verified.Pushes < entry.Pushes || verified.Pushes == entry.Pushes && (verified.Moves < entry.Moves || result.Optimality == AnalysisOptimality.PushOptimal)))
            {
                entry.Evidence = result.Copy(); entry.EvidenceVersion = algorithmVersion; entry.Pushes = verified.Pushes; entry.Moves = verified.Moves;
            }
            entry.Bytes = 1024 + Size(entry.Latest) + Size(entry.Evidence) + 2L * (algorithmVersion.Length + (entry.EvidenceVersion?.Length ?? 0));
            EstimatedBytes += entry.Bytes;
            while (entries.Count > capacity || EstimatedBytes > byteCapacity)
            { var last = lru.Last; EstimatedBytes -= last.Value.Bytes; entries.Remove(last.Value.Fingerprint); lru.RemoveLast(); }
        }
        static long Size(AnalysisResult result) => result == null ? 0 : 512 + 2L * ((result.Witness?.moves?.Length ?? 0) + (result.Explanation?.Length ?? 0));
        Entry Find(LevelData root)
        {
            if (!StructureValidator.Validate(root).IsValid) return null;
            if (!entries.TryGetValue(LevelFingerprint.Compute(root), out var node)) return null;
            lru.Remove(node); lru.AddFirst(node); return node.Value;
        }
        public AnalysisResult Evidence(LevelData root, string algorithmVersion)
        {
            var entry = Find(root); if (entry?.Evidence == null) return null;
            var result = CachedCopy(entry.Evidence, root);
            if (!WitnessVerifier.Verify(root, result.Witness).IsValid) return null;
            if (entry.EvidenceVersion != algorithmVersion) result.Optimality = AnalysisOptimality.NotProven;
            return result;
        }
        public AnalysisResult Latest(LevelData root, string algorithmVersion)
        {
            var entry = Find(root); return entry == null || entry.Version != algorithmVersion ? null : CachedCopy(entry.Latest, root);
        }
        static AnalysisResult CachedCopy(AnalysisResult result, LevelData root)
        {
            var copy = result.Copy(); copy.FromCache = true;
            if (copy.Witness != null) copy.Witness.levelId = root.levelId;
            return copy;
        }
    }
}
