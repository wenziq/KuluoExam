using System;
using Sokoban.Core.Data;
namespace Sokoban.Domain.Workshop
{
    public enum PaintTool { Floor, Wall, Goal, Player, Box, EraseEntity, EraseGoal, EraseTerrain }
    public sealed class PaintPreview
    {
        public bool CanApply { get; internal set; }
        public string Reason { get; internal set; }
        public int PlayersRemoved { get; internal set; }
        public int BoxesRemoved { get; internal set; }
        public int GoalsRemoved { get; internal set; }
    }
    /// <summary>A single transaction; the document's committed snapshot is the earliest state of every touched cell.</summary>
    public sealed class PaintStroke : IDisposable
    {
        private readonly WorkshopDocument document;
        private readonly LevelData level;
        private Coordinate? previous;
        private bool ended;
        internal PackData Working { get; }
        internal string LevelId { get; }
        internal PaintTool Tool { get; }
        internal string ToolLabel
        {
            get
            {
                switch (Tool)
                {
                    case PaintTool.Floor: return "地板";
                    case PaintTool.Wall: return "墙";
                    case PaintTool.Goal: return "目标";
                    case PaintTool.Player: return "玩家";
                    case PaintTool.Box: return "箱子";
                    case PaintTool.EraseEntity: return "实体橡皮";
                    case PaintTool.EraseGoal: return "目标橡皮";
                    default: return "地形橡皮";
                }
            }
        }
        internal PaintStroke(WorkshopDocument document, PackData working, string levelId, PaintTool tool)
        {
            this.document = document; Working = working; LevelId = levelId; Tool = tool;
            level = working.levels.Find(item => item.levelId == levelId);
        }
        public PaintPreview PreviewPoint(int x, int y)
        {
            if (ended) throw new InvalidOperationException("笔画已经结束。");
            return Assess(level,Tool,x,y);
        }
        internal static PaintPreview Assess(LevelData level, PaintTool tool, int x, int y)
        {
            if (!Enum.IsDefined(typeof(PaintTool),tool)) return new PaintPreview { CanApply=false, Reason="画笔无效" };
            if (level == null) return new PaintPreview { CanApply=false, Reason="请先新建关卡" };
            var result = new PaintPreview { CanApply = true, Reason = "" };
            if (!new Coordinate(x,y).IsInside(level.width,level.height)) { result.CanApply = false; result.Reason = "棋盘范围外"; return result; }
            bool placing = tool == PaintTool.Goal || tool == PaintTool.Player || tool == PaintTool.Box;
            if (placing && level.terrain[y * level.width + x] != 0) { result.CanApply = false; result.Reason = "必须放在地板上"; return result; }
            if (tool == PaintTool.Player && level.entities.Exists(item => item.type == EntityType.Box && item.x == x && item.y == y))
            { result.CanApply = false; result.Reason = "该格已有箱子"; }
            if (tool == PaintTool.Box)
            {
                if (level.entities.Exists(item => item.x == x && item.y == y)) { result.CanApply = false; result.Reason = "该格已有实体"; }
                else if (level.entities.FindAll(item => item.type == EntityType.Box).Count >= ContentLimits.MaxBoxes) { result.CanApply = false; result.Reason = "箱子最多 16 个"; }
            }
            if (tool == PaintTool.Wall || tool == PaintTool.EraseEntity)
            {
                result.PlayersRemoved = level.entities.FindAll(item => item.x == x && item.y == y && item.type == EntityType.Player).Count;
                result.BoxesRemoved = level.entities.FindAll(item => item.x == x && item.y == y && item.type == EntityType.Box).Count;
            }
            if (tool == PaintTool.Wall || tool == PaintTool.EraseGoal) result.GoalsRemoved = level.features.FindAll(item => item.x == x && item.y == y).Count;
            return result;
        }
        public void AddPoint(int x, int y)
        {
            if (ended) throw new InvalidOperationException("笔画已经结束。");
            var point = new Coordinate(x,y);
            if (!point.IsInside(level.width, level.height)) { previous = null; return; }
            if (previous.HasValue)
            {
                int fromX = previous.Value.x, fromY = previous.Value.y;
                int dx = Math.Abs(x - fromX), sx = fromX < x ? 1 : -1;
                int dy = -Math.Abs(y - fromY), sy = fromY < y ? 1 : -1, error = dx + dy;
                while (true)
                {
                    Apply(fromX, fromY); if (fromX == x && fromY == y) break;
                    int twice = error * 2;
                    if (twice >= dy) { error += dy; fromX += sx; }
                    if (twice <= dx) { error += dx; fromY += sy; }
                }
            }
            else Apply(x,y);
            previous = point;
        }
        private void Apply(int x, int y)
        {
            int index = y * level.width + x;
            switch (Tool)
            {
                case PaintTool.Floor:
                case PaintTool.EraseTerrain: level.terrain[index] = 0; return;
                case PaintTool.Wall:
                    level.terrain[index] = 1;
                    level.entities.RemoveAll(item => item.x == x && item.y == y);
                    level.features.RemoveAll(item => item.x == x && item.y == y); return;
                case PaintTool.EraseEntity: level.entities.RemoveAll(item => item.x == x && item.y == y); return;
                case PaintTool.EraseGoal: level.features.RemoveAll(item => item.x == x && item.y == y); return;
            }
            if (level.terrain[index] != 0) return;
            if (Tool == PaintTool.Goal)
            {
                if (!level.features.Exists(item => item.x == x && item.y == y)) level.features.Add(new FeatureData { type = FeatureType.Goal, x = x, y = y });
            }
            else if (Tool == PaintTool.Box)
            {
                if (!level.entities.Exists(item => item.x == x && item.y == y) && level.entities.FindAll(item => item.type == EntityType.Box).Count < ContentLimits.MaxBoxes)
                    level.entities.Add(new EntityData { type = EntityType.Box, x = x, y = y });
            }
            else if (Tool == PaintTool.Player)
            {
                if (level.entities.Exists(item => item.type == EntityType.Box && item.x == x && item.y == y)) return;
                var player = level.entities.Find(item => item.type == EntityType.Player);
                if (player == null) level.entities.Add(new EntityData { type = EntityType.Player, x = x, y = y });
                else
                {
                    player.x = x; player.y = y;
                    // Safe imported drafts may contain extra players; painting a birthpoint establishes one.
                    level.entities.RemoveAll(item => item.type == EntityType.Player && !ReferenceEquals(item, player));
                }
            }
        }
        public bool Commit()
        {
            if (ended) return false; ended = true; return document.EndStroke(this, true);
        }
        public bool OnFocusLost() => Commit();
        public void Cancel() { if (ended) return; ended = true; document.EndStroke(this, false); }
        public void Dispose() => Cancel();
    }
}
