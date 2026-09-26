using System;
using Sokoban.Core.Data;
namespace Sokoban.Domain.Workshop
{
    public static class LevelOperations
    {
        public static string Add(WorkshopDocument document)
        {
            var level = new LevelData { name = "新关卡" };
            for (int y = 0; y < level.height; y++) for (int x = 0; x < level.width; x++)
                level.terrain[y * level.width + x] = x == 0 || y == 0 || x == level.width - 1 || y == level.height - 1 ? 1 : 0;
            document.Edit("新建关卡", level.levelId, pack => { pack.levels.Add(level); pack.levelOrder.Add(level.levelId); });
            return level.levelId;
        }
        public static string Duplicate(WorkshopDocument document, string id)
        {
            var copy = Find(document.Snapshot(), id).DeepCopy(); copy.levelId = ContentIds.NewId();
            foreach (var entity in copy.entities) entity.id = ContentIds.NewId();
            foreach (var feature in copy.features) feature.id = ContentIds.NewId();
            document.Edit("复制关卡", copy.levelId, pack =>
            {
                pack.levels.Insert(pack.levels.FindIndex(item => item.levelId == id) + 1, copy);
                pack.levelOrder.Insert(pack.levelOrder.IndexOf(id) + 1, copy.levelId);
                // Source evidence stays with its original identity. A copied level needs replay before new evidence is attached.
            });
            return copy.levelId;
        }
        public static bool Delete(WorkshopDocument document, string id) => document.Edit("删除关卡", id, pack =>
        {
            Find(pack,id); pack.levels.RemoveAll(item => item.levelId == id); pack.levelOrder.Remove(id);
            pack.solutionWitnesses.RemoveAll(item => item.levelId == id);
        });
        public static bool Rename(WorkshopDocument document, string id, string name) => document.Edit("修改关卡名称", id, pack => Find(pack,id).name = name);
        public static bool SetLevelMetadata(WorkshopDocument document, string id, string name, string notes, IntendedDifficulty difficulty) => document.Edit("修改关卡属性", id, pack =>
        {
            var level = Find(pack,id); level.name = name; level.designNotes = notes; level.intendedDifficulty = difficulty;
        });
        public static bool SetPackMetadata(WorkshopDocument document, string name, string description, UnlockPolicy unlockPolicy) => document.Edit("修改关卡集信息", document.SelectedLevelId, pack =>
        {
            pack.name = name; pack.description = description; pack.unlockPolicy = unlockPolicy;
        });
        public static bool Reorder(WorkshopDocument document, string id, int index) => document.Edit("移动关卡顺序", id, pack =>
        {
            Find(pack,id); if (index < 0 || index >= pack.levelOrder.Count) throw new ArgumentOutOfRangeException(nameof(index));
            pack.levelOrder.Remove(id); pack.levelOrder.Insert(index,id);
        });
        public static bool DeleteEntity(WorkshopDocument document, string levelId, string objectId) => document.Edit("删除实体", levelId, pack => Find(pack,levelId).entities.RemoveAll(item => item.id == objectId));
        public static bool DeleteFeature(WorkshopDocument document, string levelId, string objectId) => document.Edit("删除目标", levelId, pack => Find(pack,levelId).features.RemoveAll(item => item.id == objectId));
        public static bool DeleteWall(WorkshopDocument document, string levelId, int x, int y) => document.Edit("删除墙", levelId, pack =>
        {
            var level = Find(pack,levelId); if (!new Coordinate(x,y).IsInside(level.width, level.height)) throw new ArgumentOutOfRangeException();
            level.terrain[y * level.width + x] = 0;
        });
        internal static LevelData Find(PackData pack, string id) => pack.levels.Find(item => item.levelId == id) ?? throw new ArgumentException("关卡不存在。", nameof(id));
    }
}
