using System;
namespace Sokoban.Domain.Analysis
{
    /// <summary>Box identity is irrelevant to search. Player connectivity is not.</summary>
    public sealed class SearchStateKey : IEquatable<SearchStateKey>
    {
        readonly int[] boxes;
        readonly int hash;
        public int Region { get; }
        public int BoxCount => boxes.Length;
        public int BoxAt(int index) => boxes[index];
        public int[] CopyBoxes() => (int[])boxes.Clone();
        public SearchStateKey(int[] positions, int region)
        {
            if (positions == null) throw new ArgumentNullException(nameof(positions));
            boxes = (int[])positions.Clone(); Array.Sort(boxes); Region = region;
            unchecked { int value = 17; foreach (int box in boxes) value = value * 31 + box; hash = value * 31 + region; }
        }
        public bool Equals(SearchStateKey other)
        {
            if (ReferenceEquals(this, other)) return true;
            if (other == null || Region != other.Region || boxes.Length != other.boxes.Length) return false;
            for (int i = 0; i < boxes.Length; i++) if (boxes[i] != other.boxes[i]) return false;
            return true;
        }
        public override bool Equals(object other) => Equals(other as SearchStateKey);
        public override int GetHashCode() => hash;
    }
}
