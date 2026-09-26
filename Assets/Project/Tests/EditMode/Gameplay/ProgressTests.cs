using NUnit.Framework;
using Sokoban.Core.Data;
using Sokoban.Core.Rules;
using Sokoban.Domain.Gameplay;
namespace Sokoban.Tests.EditMode.Gameplay
{
    public sealed class ProgressTests
    {
        public static LevelData Level(string id)
        {
            var level = AsciiLevelFactory.Create("######", "#    #", "# @$.#", "######");
            level.levelId = id;
            return level;
        }
        public static GameSession Win(LevelData level, SessionMode mode = SessionMode.Formal, string path = "R")
        {
            var session = new GameSession(level, mode);
            foreach (char c in path)
            {
                Assert.That(SokobanRules.TryParseDirection(c, out var d), Is.True);
                Assert.That(session.TryMove(d).Succeeded, Is.True);
            }
            Assert.That(session.IsCompleted, Is.True);
            return session;
        }
        [Test] public void IdentityKeepsRenameButRejectsChangedLayout()
        {
            var level = Level("A");
            var service = new ProgressService();
            Assert.That(service.Record("pack", Win(level)), Is.True);
            level.name = "改名";
            Assert.That(service.Best("pack", level).moves, Is.EqualTo(1));
            level.terrain[7] = 1;
            Assert.That(service.Best("pack", level), Is.Null);
        }
        [Test] public void BestKeepsWholeRealRecordAndSnapshotIsIsolated()
        {
            var level = Level("A");
            var service = new ProgressService();
            service.Record("pack", Win(level, path: "ULDRR"));
            service.Record("pack", Win(level));
            service.Record("pack", Win(level, path: "ULDRR"));
            var best = service.Best("pack", level);
            Assert.That(best.path, Is.EqualTo("R"));
            Assert.That(best.moves, Is.EqualTo(1));
            Assert.That(best.pushes, Is.EqualTo(1));
            best.moves = 99;
            var snapshot = service.Snapshot();
            snapshot.records.Clear();
            Assert.That(service.Best("pack", level).moves, Is.EqualTo(1));
        }
        [Test] public void FewerPushesWinsEvenWithMoreMovesWithoutMixingRecords()
        {
            var level = Level("A");
            var progress = new ProgressService();
            var shortWalk = Win(level, path: "URRDLULLDRR");
            var fewerPushes = Win(level, path: "ULDRULDRULDRR");
            Assert.That(shortWalk.Pushes, Is.GreaterThan(fewerPushes.Pushes));
            Assert.That(shortWalk.Moves, Is.LessThan(fewerPushes.Moves));
            progress.Record("pack", shortWalk);
            progress.Record("pack", fewerPushes);
            progress.Record("pack", shortWalk);
            var best = progress.Best("pack", level);
            Assert.That(best.pushes, Is.EqualTo(fewerPushes.Pushes));
            Assert.That(best.moves, Is.EqualTo(fewerPushes.Moves));
            Assert.That(best.path, Is.EqualTo(fewerPushes.Path));
        }
        [Test] public void InvalidLoadedRecordCannotBlockNewGenuineCompletion()
        {
            var level = Level("A");
            var snapshot = new ProgressSnapshot();
            snapshot.records.Add(new CompletionRecord { packId = "pack", levelId = "A", fingerprint = Sokoban.Core.Identity.LevelFingerprint.Compute(level), path = "", moves = 0, pushes = 0 });
            var progress = new ProgressService(snapshot);
            Assert.That(progress.Best("pack", level), Is.Null);
            Assert.That(progress.Record("pack", Win(level)), Is.True);
            Assert.That(progress.Best("pack", level).moves, Is.EqualTo(1));
        }
        [TestCase(SessionMode.Trial)] [TestCase(SessionMode.AssistedReplayTakeover)]
        public void TemporarySessionsNeverWriteFormalProgress(SessionMode mode)
        {
            var service = new ProgressService();
            Assert.That(service.Record("pack", Win(Level("A"), mode)), Is.False);
            Assert.That(service.Snapshot().records, Is.Empty);
        }
        [Test] public void SequentialRequiresEveryPredecessorNotOnlyPreviousLevel()
        {
            var pack = new PackData { packId = "pack", unlockPolicy = UnlockPolicy.Sequential };
            pack.levels.AddRange(new[] { Level("A"), Level("C"), Level("B") });
            pack.levelOrder.AddRange(new[] { "A", "C", "B" });
            var progress = new ProgressService();
            progress.Record("pack", Win(pack.levels[1]));
            Assert.That(UnlockPolicyEvaluator.CanEnter(pack, "B", progress), Is.False);
            Assert.That(UnlockPolicyEvaluator.CanEnter(pack, "C", progress), Is.True);
        }
        [Test] public void CurrentOrderDrivesUnlockNextAndWholePackCompletion()
        {
            var pack = new PackData { packId = "pack", unlockPolicy = UnlockPolicy.Sequential };
            pack.levels.AddRange(new[] { Level("A"), Level("B"), Level("C") });
            pack.levelOrder.AddRange(new[] { "A", "C", "B" });
            var service = new ProgressService();
            Assert.That(UnlockPolicyEvaluator.CanEnter(pack, "A", service), Is.True);
            Assert.That(UnlockPolicyEvaluator.CanEnter(pack, "C", service), Is.False);
            Assert.That(UnlockPolicyEvaluator.Next(pack, "A"), Is.EqualTo("C"));
            service.Record("pack", Win(pack.levels[1]));
            Assert.That(UnlockPolicyEvaluator.AllComplete(pack, service), Is.False);
            Assert.That(UnlockPolicyEvaluator.CanEnter(pack, "B", service), Is.True);
            service.Record("pack", Win(pack.levels[0]));
            Assert.That(UnlockPolicyEvaluator.CanEnter(pack, "C", service), Is.True);
            service.Record("pack", Win(pack.levels[2]));
            Assert.That(UnlockPolicyEvaluator.AllComplete(pack, service), Is.True);
            pack.unlockPolicy = UnlockPolicy.AllOpen;
            Assert.That(UnlockPolicyEvaluator.CanEnter(pack, "C", new ProgressService()), Is.True);
            Assert.That(UnlockPolicyEvaluator.CanEnter(pack, "missing", service), Is.False);
        }
    }
}
