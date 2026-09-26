using System.Threading;
using NUnit.Framework;
using Sokoban.Core.Data;
using Sokoban.Core.Identity;
using Sokoban.Core.Rules;
namespace Sokoban.Tests.EditMode.Rules
{
    public class WitnessTests
    {
        private static WitnessData Witness(LevelData level, string path) => new WitnessData { levelId = level.levelId, levelFingerprint = LevelFingerprint.Compute(level), moves = path };
        [Test] public void UnverifiedResultDefaultsToFailureAndNullWitnessHasReason()
        {
            Assert.That(new WitnessVerification().IsValid, Is.False);
            var missing = WitnessVerifier.Verify(SokobanRulesTests.Straight(), null);
            Assert.That(missing.Failure, Is.EqualTo(WitnessFailure.MissingWitness));
            Assert.That(missing.Reason, Is.Not.Empty);
        }
        [Test] public void ActualReplayRequiresEveryMoveAndFinalWin()
        {
            var level = SokobanRulesTests.Straight();
            var valid = WitnessVerifier.Verify(level, Witness(level, "RR"));
            Assert.That(valid.IsValid, Is.True);
            Assert.That(valid.Moves, Is.EqualTo(2));
            Assert.That(valid.Pushes, Is.EqualTo(1));
            var illegal = WitnessVerifier.Verify(level, Witness(level, "RLDD"));
            Assert.That(illegal.Failure, Is.EqualTo(WitnessFailure.IllegalMove));
            Assert.That(illegal.FailedMoveIndex, Is.EqualTo(2));
            Assert.That(illegal.Moves, Is.EqualTo(2));
            Assert.That(WitnessVerifier.Verify(level, Witness(level, "R")).Failure, Is.EqualTo(WitnessFailure.NotWon));
            Assert.That(WitnessVerifier.Verify(level, Witness(level, "")).Failure, Is.EqualTo(WitnessFailure.NotWon));
        }
        [TestCase("rules", WitnessFailure.RulesMismatch)]
        [TestCase("level", WitnessFailure.LevelMismatch)]
        [TestCase("fingerprint", WitnessFailure.FingerprintMismatch)]
        [TestCase("path", WitnessFailure.InvalidPath)]
        public void RejectsMismatchedEvidence(string change, WitnessFailure expected)
        {
            var level = SokobanRulesTests.Straight();
            var witness = Witness(level, "RR");
            if (change == "rules") witness.rulesVersion = 2;
            if (change == "level") witness.levelId = "other";
            if (change == "fingerprint") witness.levelFingerprint = new string('0', 64);
            if (change == "path") witness.moves = "Rr";
            Assert.That(WitnessVerifier.Verify(level, witness).Failure, Is.EqualTo(expected));
        }
        [Test] public void BudgetAndCancellationNeverBecomeProofAndExternalSourceIsOnlyMetadata()
        {
            var level = SokobanRulesTests.Straight();
            var witness = Witness(level, "RR");
            Assert.That(WitnessVerifier.Verify(level, witness, 1).Failure, Is.EqualTo(WitnessFailure.MoveLimit));
            Assert.That(WitnessVerifier.Verify(level, witness, cancellationToken: new CancellationToken(true)).Failure, Is.EqualTo(WitnessFailure.Cancelled));
            witness.source = WitnessSource.Solver;
            Assert.That(WitnessVerifier.Verify(level, witness).IsValid, Is.True);
            witness.source = WitnessSource.Manual;
            Assert.That(WitnessVerifier.Verify(level, witness).IsValid, Is.True);
            level.entities.Clear();
            Assert.That(WitnessVerifier.Verify(level, witness).Failure, Is.EqualTo(WitnessFailure.InvalidStructure));
        }
    }
}
