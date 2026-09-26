using System;
using System.Collections.Generic;
namespace Sokoban.Core.Data
{
    public enum TerrainType { Floor = 0, Wall = 1 }
    public enum FeatureType { Goal }
    public enum EntityType { Player, Box }
    public enum IntendedDifficulty { Unspecified, Intro, Easy, Medium, Hard }
    [Serializable] public struct Coordinate : IEquatable<Coordinate>
    {
        public int x, y;
        public Coordinate(int x, int y) { this.x = x; this.y = y; }
        public int ToIndex(int width) => checked(y * width + x);
        public static Coordinate FromIndex(int index, int width) => new Coordinate(index % width, index / width);
        public bool IsInside(int width, int height) => x >= 0 && y >= 0 && x < width && y < height;
        public bool Equals(Coordinate other) => x == other.x && y == other.y;
        public override bool Equals(object obj) => obj is Coordinate other && Equals(other);
        public override int GetHashCode() => unchecked(x * 397 ^ y);
    }
    [Serializable] public sealed class FeatureData
    {
        public string id = ContentIds.NewId(); public FeatureType type; public int x, y;
        public FeatureData DeepCopy() => (FeatureData)MemberwiseClone();
    }
    [Serializable] public sealed class EntityData
    {
        public string id = ContentIds.NewId(); public EntityType type; public int x, y;
        public EntityData DeepCopy() => (EntityData)MemberwiseClone();
    }
    [Serializable] public sealed class LevelData
    {
        public string levelId = ContentIds.NewId(), name = "", designNotes = "";
        public IntendedDifficulty intendedDifficulty;
        public int width = ContentLimits.DefaultWidth, height = ContentLimits.DefaultHeight;
        public int[] terrain = new int[ContentLimits.DefaultWidth * ContentLimits.DefaultHeight];
        public List<FeatureData> features = new List<FeatureData>();
        public List<EntityData> entities = new List<EntityData>();
        public LevelData DeepCopy()
        {
            var copy = (LevelData)MemberwiseClone();
            copy.terrain = terrain == null ? null : (int[])terrain.Clone();
            copy.features = features?.ConvertAll(f => f?.DeepCopy());
            copy.entities = entities?.ConvertAll(e => e?.DeepCopy());
            return copy;
        }
    }
    public static class ContentIds { public static string NewId() => Guid.NewGuid().ToString("N"); }
}
