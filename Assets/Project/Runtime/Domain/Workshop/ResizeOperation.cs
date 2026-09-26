using System;
using Sokoban.Core.Data;
namespace Sokoban.Domain.Workshop
{
    /// <summary>Immutable crop preview. Commit rejects any edit since preview, so confirmation always describes its actual crop.</summary>
    public sealed class ResizeOperation
    {
        private readonly WorkshopDocument document;
        private readonly string id, previewHash;
        private readonly int width, height;
        public ResizeOperation(WorkshopDocument document, string id, int width, int height)
        {
            if (width < ContentLimits.MinWidth || width > ContentLimits.MaxWidth || height < ContentLimits.MinHeight || height > ContentLimits.MaxHeight)
                throw new ArgumentOutOfRangeException(nameof(width), "地图宽高必须为 4～20。");
            this.document = document ?? throw new ArgumentNullException(nameof(document)); this.id = id; this.width = width; this.height = height;
            if (document.HasActiveStroke) throw new InvalidOperationException("请先结束当前笔画。");
            var level = LevelOperations.Find(document.Snapshot(),id); previewHash = document.CurrentHash;
            RequiresCropConfirmation = width < level.width || height < level.height;
            PlayersRemoved = level.entities.FindAll(item => Cropped(item.x,item.y) && item.type == EntityType.Player).Count;
            BoxesRemoved = level.entities.FindAll(item => Cropped(item.x,item.y) && item.type == EntityType.Box).Count;
            GoalsRemoved = level.features.FindAll(item => Cropped(item.x,item.y)).Count;
        }
        public int PlayersRemoved { get; }
        public int BoxesRemoved { get; }
        public int GoalsRemoved { get; }
        public bool RequiresCropConfirmation { get; }
        public bool Commit()
        {
            if (document.CurrentHash != previewHash) throw new InvalidOperationException("预览后文档已修改，请重新预览裁剪结果。");
            return document.Edit("修改地图尺寸", id, pack =>
            {
                var level = LevelOperations.Find(pack,id); var terrain = new int[width * height];
                for (int y = 0; y < Math.Min(height,level.height); y++) for (int x = 0; x < Math.Min(width,level.width); x++)
                    terrain[y * width + x] = level.terrain[y * level.width + x];
                level.entities.RemoveAll(item => Cropped(item.x,item.y)); level.features.RemoveAll(item => Cropped(item.x,item.y));
                level.width = width; level.height = height; level.terrain = terrain;
            });
        }
        private bool Cropped(int x, int y) => x >= width || y >= height;
    }
}
