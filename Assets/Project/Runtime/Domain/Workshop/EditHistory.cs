using System;
using System.Collections.Generic;
using Sokoban.Core.Data;
namespace Sokoban.Domain.Workshop
{
    /// <summary>Whole-pack immutable snapshots, bounded across both undo and redo stacks.</summary>
    internal sealed class EditHistory
    {
        internal sealed class Entry
        {
            public PackData Before, After;
            public string BeforeSelection, AfterSelection, Description;
            public long Bytes;
        }
        private readonly List<Entry> undo = new List<Entry>(), redo = new List<Entry>();
        private readonly int maxCount;
        private readonly long budget;
        public int UndoCount => undo.Count;
        public int RedoCount => redo.Count;
        public long EstimatedBytes { get; private set; }
        public EditHistory(int maxCount, long budget)
        {
            if (maxCount < 0 || budget < 0) throw new ArgumentOutOfRangeException();
            this.maxCount = maxCount; this.budget = budget;
        }
        public void Push(PackData before, PackData after, string beforeSelection, string afterSelection, string description)
        {
            foreach (var entry in redo) EstimatedBytes -= entry.Bytes; redo.Clear();
            var next = new Entry { Before = before, After = after, BeforeSelection = beforeSelection, AfterSelection = afterSelection, Description = description,
                Bytes = EstimateSnapshotBytes(before) + EstimateSnapshotBytes(after) + 128 + TextBytes(description) };
            undo.Add(next); EstimatedBytes += next.Bytes;
            while (undo.Count > maxCount || EstimatedBytes > budget) { EstimatedBytes -= undo[0].Bytes; undo.RemoveAt(0); }
        }
        public Entry Undo() { if (undo.Count == 0) return null; var entry = undo[undo.Count - 1]; undo.RemoveAt(undo.Count - 1); redo.Add(entry); return entry; }
        public Entry Redo() { if (redo.Count == 0) return null; var entry = redo[redo.Count - 1]; redo.RemoveAt(redo.Count - 1); undo.Add(entry); return entry; }
        // Conservative 64-bit managed heap estimate: headers/alignment, list backing capacities, UTF-16 strings,
        // primitive arrays and every DTO. Shared references are counted repeatedly, intentionally overestimating retention.
        internal static long EstimateSnapshotBytes(PackData pack)
        {
            long bytes = 128 + TextBytes(pack.packId) + TextBytes(pack.name) + TextBytes(pack.description) + ListBytes(pack.levels.Capacity) + ListBytes(pack.levelOrder.Capacity) + ListBytes(pack.solutionWitnesses.Capacity);
            foreach (string id in pack.levelOrder) bytes += TextBytes(id);
            foreach (var level in pack.levels)
            {
                bytes += 128 + TextBytes(level.levelId) + TextBytes(level.name) + TextBytes(level.designNotes) + 32 + level.terrain.LongLength * 4 + ListBytes(level.features.Capacity) + ListBytes(level.entities.Capacity);
                foreach (var item in level.features) bytes += 48 + TextBytes(item.id);
                foreach (var item in level.entities) bytes += 48 + TextBytes(item.id);
            }
            foreach (var item in pack.solutionWitnesses) bytes += 64 + TextBytes(item.levelId) + TextBytes(item.levelFingerprint) + TextBytes(item.moves);
            return bytes;
        }
        private static long ListBytes(int capacity) => 64L + capacity * 8L;
        private static long TextBytes(string value) => value == null ? 0 : ((32L + value.Length * 2L + 7) / 8) * 8;
    }
}
