using System;
using System.Collections.Generic;
namespace Sokoban.Domain.Workshop
{
    public enum SelectionLayer { None, Terrain, Feature, Entity }
    public sealed class LevelViewportState
    {
        public PaintTool Tool { get; set; } = PaintTool.Wall;
        public bool IsSelectionTool { get; set; }
        public SelectionLayer SelectionLayer { get; set; }
        public int SelectedCellX { get; set; }
        public int SelectedCellY { get; set; }
        public string SelectedObjectId { get; set; }
        public float Zoom { get; set; } = 1;
        public float PanX { get; set; }
        public float PanY { get; set; }
        internal LevelViewportState Copy() => (LevelViewportState)MemberwiseClone();
    }
    /// <summary>Ephemeral per-level tools and camera state. Deliberately independent of content and history.</summary>
    public sealed class WorkshopViewportState
    {
        private readonly Dictionary<string, LevelViewportState> levels = new Dictionary<string, LevelViewportState>(StringComparer.Ordinal);
        public LevelViewportState Get(string levelId)
        {
            if (levelId == null) throw new ArgumentNullException(nameof(levelId));
            return levels.TryGetValue(levelId, out var state) ? state.Copy() : new LevelViewportState();
        }
        public void Set(string levelId, LevelViewportState state)
        {
            if (levelId == null || state == null) throw new ArgumentNullException();
            if (!Enum.IsDefined(typeof(PaintTool),state.Tool) || !Enum.IsDefined(typeof(SelectionLayer),state.SelectionLayer) || !Finite(state.Zoom) || state.Zoom <= 0 || !Finite(state.PanX) || !Finite(state.PanY)) throw new ArgumentException("视口参数无效。");
            levels[levelId] = state.Copy();
        }
        public void Clear() => levels.Clear();
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
