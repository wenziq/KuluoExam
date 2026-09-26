using Sokoban.Core.Data;
namespace Sokoban.Domain.Gameplay
{
    public static class UnlockPolicyEvaluator
    {
        public static bool CanEnter(PackData pack, string levelId, ProgressService progress)
        {
            int index = pack.levelOrder.IndexOf(levelId);
            if (index < 0) return false;
            if (pack.unlockPolicy == UnlockPolicy.AllOpen || index == 0) return true;
            var level = pack.levels.Find(l => l.levelId == levelId);
            if (progress.Best(pack.packId, level) != null) return true;
            for (int i = 0; i < index; i++)
                if (progress.Best(pack.packId, pack.levels.Find(l => l.levelId == pack.levelOrder[i])) == null) return false;
            return true;
        }
        public static bool AllComplete(PackData pack, ProgressService progress)
        {
            if (pack.levelOrder.Count == 0) return false;
            foreach (var id in pack.levelOrder)
                if (progress.Best(pack.packId, pack.levels.Find(l => l.levelId == id)) == null) return false;
            return true;
        }
        public static string Next(PackData pack, string levelId)
        {
            int index = pack.levelOrder.IndexOf(levelId);
            return index >= 0 && index + 1 < pack.levelOrder.Count ? pack.levelOrder[index + 1] : null;
        }
    }
}
