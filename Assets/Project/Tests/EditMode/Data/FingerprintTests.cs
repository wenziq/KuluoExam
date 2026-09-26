using System;
using NUnit.Framework;
using Sokoban.Core.Data;
using Sokoban.Core.Identity;
namespace Sokoban.Tests.EditMode.Data
{
    public class FingerprintTests
    {
        [Test] public void V1GoldenHasExactLfTextAndIndependentSha256()
        {
            var level=PackValidationTests.Level();
            Assert.That(LevelFingerprint.CanonicalText(level),Is.EqualTo("sokoban-level-v1\nrules=1\nsize=5,4\nterrain=11111100011000111111\ngoals=11\nplayer=6\nboxes=7\n"));
            // Independently computed using Python hashlib.sha256 over the literal above.
            Assert.That(LevelFingerprint.Compute(level),Is.EqualTo("f94506b3cd92604032606ff349585146c719e6e1f81c20b4cec6741d21ca1779"));
        }
        [Test] public void LabelsIdsAndListOrderDoNotAffectGameplayFingerprint()
        {
            var level=PackValidationTests.Level(); var old=LevelFingerprint.Compute(level);
            level.name="新名字";level.designNotes="说明";level.intendedDifficulty=IntendedDifficulty.Hard;level.levelId="other";
            level.entities[0].id="renamed";level.entities.Reverse();
            Assert.That(LevelFingerprint.Compute(level),Is.EqualTo(old));
            level.entities.Find(e=>e.type==EntityType.Player).x=3;
            Assert.That(LevelFingerprint.Compute(level),Is.Not.EqualTo(old));
        }
        [Test] public void WallsAffectFingerprintAndInvalidLayoutCannotGetOne()
        {
            var level=PackValidationTests.Level(); var old=LevelFingerprint.Compute(level);level.terrain[13]=1;
            Assert.That(LevelFingerprint.Compute(level),Is.Not.EqualTo(old));
            level.entities.RemoveAt(0); Assert.Throws<ArgumentException>(()=>LevelFingerprint.Compute(level));
            Assert.Throws<ArgumentException>(()=>LevelFingerprint.Compute(PackValidationTests.Level(),2));
        }
        [Test] public void DocumentHashIncludesEditorialFieldsButExcludesDerivedData()
        {
            var pack=PackValidationTests.Pack(); var old=DocumentHash.Compute(pack);
            pack.contentRevision=23;pack.solutionWitnesses.Add(new WitnessData());
            Assert.That(DocumentHash.Compute(pack),Is.EqualTo(old));
            pack.name="changed";Assert.That(DocumentHash.Compute(pack),Is.Not.EqualTo(old));
            pack.name="";pack.levels[0].entities[0].id="newid";Assert.That(DocumentHash.Compute(pack),Is.Not.EqualTo(old));
        }
        [TestCase("packId")] [TestCase("description")] [TestCase("unlockPolicy")] [TestCase("levelOrder")]
        [TestCase("levelName")] [TestCase("notes")] [TestCase("difficulty")] [TestCase("terrain")]
        [TestCase("width")] [TestCase("height")] [TestCase("featureId")] [TestCase("goalPosition")] [TestCase("entityPosition")]
        public void DocumentHashTracksEveryEditorialChange(string field)
        {
            var pack=PackValidationTests.Pack();
            var second=PackValidationTests.Level(); second.levelId="second"; pack.levels.Add(second);pack.levelOrder.Add(second.levelId);
            var before=DocumentHash.Compute(pack);var level=pack.levels[0];
            switch(field)
            {
                case "packId":pack.packId="different";break;
                case "description":pack.description="说明";break;
                case "unlockPolicy":pack.unlockPolicy=UnlockPolicy.AllOpen;break;
                case "levelOrder":pack.levelOrder.Reverse();break;
                case "levelName":level.name="关卡名称";break;
                case "notes":level.designNotes="设计说明";break;
                case "difficulty":level.intendedDifficulty=IntendedDifficulty.Intro;break;
                case "terrain":level.terrain[13]=1;break;
                case "width":level.width++;break;
                case "height":level.height++;break;
                case "featureId":level.features[0].id="otherGoal";break;
                case "goalPosition":level.features[0].x++;break;
                case "entityPosition":level.entities[0].x++;break;
            }
            Assert.That(DocumentHash.Compute(pack),Is.Not.EqualTo(before));
        }
        [Test] public void DocumentHashUsesUnambiguousStringsAndExplicitListOrder()
        {
            var a=PackValidationTests.Pack();var b=PackValidationTests.Pack();
            a.name="a\nb";a.description="c";b.name="a";b.description="b\nc";
            Assert.That(DocumentHash.Compute(a),Is.Not.EqualTo(DocumentHash.Compute(b)));
            b=a.DeepCopy(); b.levels[0].entities.Reverse(); Assert.That(DocumentHash.Compute(a),Is.Not.EqualTo(DocumentHash.Compute(b)));
        }
    }
}
