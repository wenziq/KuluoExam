using Sokoban.Core.Data;
using Sokoban.Core.Identity;
using Sokoban.Domain.Gameplay;
namespace Sokoban.Runtime.Fixtures
{
    // Integration fixture only. U18 replaces the shipped built-in file with authored content.
    public static class PlayableFlowFixture
    {
        public static PackData Create()
        {
            var pack = new PackData { packId = "u7-flow-fixture", name = "入门练习", description = "从观察开始，把箱子推到目标。", documentKind = DocumentKind.PlayablePack, unlockPolicy = UnlockPolicy.Sequential };
            Add("A", "第一次推动", new[] { "######", "#    #", "# @$.#", "######" }, "R");
            Add("C", "留出一步空间", new[] { "#######", "#     #", "# @ $.#", "#######" }, "RR");
            Add("B", "沿着方向前进", new[] { "########", "#      #", "# @ $ .#", "########" }, "RRR");
            return pack;
            void Add(string id, string name, string[] rows, string path)
            {
                var level = AsciiLevelFactory.Create(rows);
                level.levelId = id;
                level.name = name;
                level.designNotes = "观察箱子前方的空间，推动之前想一想下一步。";
                // Stable fixture object identities make repeated build generation deterministic.
                for (int i = 0; i < level.entities.Count; i++) level.entities[i].id = id + "-entity-" + i;
                for (int i = 0; i < level.features.Count; i++) level.features[i].id = id + "-goal-" + i;
                pack.levels.Add(level);
                pack.levelOrder.Add(id);
                pack.solutionWitnesses.Add(new WitnessData { levelId = id, levelFingerprint = LevelFingerprint.Compute(level), moves = path, source = WitnessSource.Manual });
            }
        }
    }
}
