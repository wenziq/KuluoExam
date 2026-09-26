using System;
using System.Linq;
using NUnit.Framework;
using Sokoban.Core.Data;
using Sokoban.Core.Validation;
using Sokoban.Core.Identity;
namespace Sokoban.Tests.EditMode.Data
{
    public class PackValidationTests
    {
        internal static LevelData Level() => new LevelData { levelId = "level1", width = 5, height = 4,
            terrain = new[] {1,1,1,1,1, 1,0,0,0,1, 1,0,0,0,1, 1,1,1,1,1},
            features = { new FeatureData { id = "goal1", x = 1, y = 2 } },
            entities = {new EntityData { id = "player1", type = EntityType.Player, x = 1, y = 1 }, new EntityData { id = "box1", type = EntityType.Box, x = 2, y = 1 }} };
        internal static PackData Pack() => new PackData { packId = "pack1", levels = { Level() }, levelOrder = { "level1" } };
        [Test] public void SafeIncompleteDraftIsNotPlayable()
        {
            var pack = Pack(); pack.levels[0].entities.RemoveAt(0);
            Assert.That(PackValidator.Validate(pack).IsValid, Is.True);
            Assert.That(StructureValidator.Validate(pack).Issues.Any(i => i.Code == "PLAYER_MISSING" && i.LevelId == "level1"), Is.True);
        }
        [Test] public void EmptyDraftSafeButPlayablePackRequiresLevels()
        {
            var pack = new PackData(); Assert.That(PackValidator.Validate(pack).IsValid, Is.True);
            pack.documentKind = DocumentKind.PlayablePack;
            Assert.That(PackValidator.Validate(pack).IsValid, Is.False);
        }
        [Test] public void GoodStructureDoesNotClaimSolvability()
        { Assert.That(StructureValidator.Validate(Pack()).IsValid, Is.True); }
        [Test] public void OrderMissingDuplicateAndUnknownReferencesAreLocated()
        {
            var pack = Pack(); pack.levelOrder.Clear();
            Assert.That(PackValidator.Validate(pack).Issues.Any(i => i.Code == "ORDER_REFERENCE_MISSING" && i.LevelId == "level1"), Is.True);
            pack.levelOrder.AddRange(new[] { "level1", "level1", "absent" });
            var issues = PackValidator.Validate(pack).Issues;
            Assert.That(issues.Any(i => i.Code == "ORDER_DUPLICATE"), Is.True);
            Assert.That(issues.Any(i => i.Code == "ORDER_REFERENCE_UNKNOWN"), Is.True);
        }
        [Test] public void ObjectIdsShareNamespaceAndBoundsHaveCoordinates()
        {
            var level = Level(); level.features[0].id = level.entities[0].id; level.features[0].x = 5;
            var issues = PackValidator.ValidateLevel(level).Issues;
            Assert.That(issues.Any(i => i.Code == "OBJECT_ID_DUPLICATE"), Is.True);
            var bounds = issues.Single(i => i.Code == "OBJECT_OUT_OF_BOUNDS");
            Assert.That(bounds.Position, Is.EqualTo(new Coordinate(5,2)));
            Assert.That(bounds.LocationAction, Is.EqualTo(IssueLocationAction.SelectObject));
        }
        [Test] public void OccupancyAndCountsArePlayableErrorsOnly()
        {
            var level = Level(); level.entities[1].x = 0; level.entities.Add(new EntityData { id="box2", type=EntityType.Box, x=1, y=1 });
            Assert.That(PackValidator.ValidateLevel(level).IsValid, Is.True);
            var issues = StructureValidator.Validate(level).Issues;
            Assert.That(issues.Any(i => i.Code == "ENTITY_ON_WALL"), Is.True);
            Assert.That(issues.Any(i => i.Code == "ENTITY_OVERLAP"), Is.True);
            Assert.That(issues.Any(i => i.Code == "BOX_GOAL_COUNT_MISMATCH"), Is.True);
        }
        [Test] public void InvalidVersionsEnumsIdsAndTerrainFailSafely()
        {
            var pack = Pack(); pack.rulesVersion=2; pack.packId="../escape"; pack.unlockPolicy=(UnlockPolicy)77;
            pack.levels[0].terrain=new[] {2};
            Assert.That(PackValidator.Validate(pack).IsValid, Is.False);
            Assert.That(() => StructureValidator.Validate(pack), Throws.Nothing);
            Assert.That(PackValidator.Validate(null).IsValid, Is.False);
            Assert.That(PackValidator.ValidateLevel(null).IsValid, Is.False);
        }
        [Test] public void LimitsAcceptBoundaryAndRejectOverflow()
        {
            var level = new LevelData { width=20,height=20,terrain=new int[400],name=new string('中',80),designNotes=new string('注',2000) };
            for(int i=0;i<16;i++) level.entities.Add(new EntityData {type=EntityType.Box,x=i,y=0});
            Assert.That(PackValidator.ValidateLevel(level).IsValid, Is.True);
            level.entities.Add(new EntityData {type=EntityType.Box,x=16,y=0});
            Assert.That(PackValidator.ValidateLevel(level).IsValid, Is.False);
            level.entities.Clear(); level.name+="字";
            Assert.That(PackValidator.ValidateLevel(level).IsValid, Is.False);
        }
        [TestCase(3, 4, false)] [TestCase(4, 3, false)] [TestCase(4, 4, true)] [TestCase(20, 20, true)] [TestCase(21, 20, false)]
        public void SizeBoundariesFollowProductContract(int width, int height, bool valid)
        {
            var level=new LevelData {width=width,height=height,terrain=new int[width*height]};
            Assert.That(PackValidator.ValidateLevel(level).IsValid,Is.EqualTo(valid));
        }
        [Test] public void SnapshotIsDeepAndIndependentCopyRebindsWitness()
        {
            var pack=Pack(); pack.solutionWitnesses.Add(new WitnessData {levelId="level1",levelFingerprint=LevelFingerprint.Compute(pack.levels[0]),moves="UR"});
            var snapshot=pack.DeepCopy(); snapshot.levels[0].entities[0].x=3; snapshot.levels[0].terrain[0]=0; snapshot.solutionWitnesses[0].moves="L";
            Assert.That(pack.levels[0].entities[0].x,Is.EqualTo(1)); Assert.That(pack.levels[0].terrain[0],Is.EqualTo(1)); Assert.That(pack.solutionWitnesses[0].moves,Is.EqualTo("UR"));
            var copy=ContentIdentity.CreateIndependentCopy(pack);
            Assert.That(copy.packId,Is.Not.EqualTo(pack.packId)); Assert.That(copy.levels[0].levelId,Is.Not.EqualTo("level1"));
            Assert.That(copy.levelOrder[0],Is.EqualTo(copy.levels[0].levelId)); Assert.That(copy.solutionWitnesses[0].levelId,Is.EqualTo(copy.levelOrder[0]));
            Assert.That(copy.levels[0].entities[0].id,Is.Not.EqualTo(pack.levels[0].entities[0].id));
            Assert.That(copy.solutionWitnesses[0].levelFingerprint,Is.EqualTo(pack.solutionWitnesses[0].levelFingerprint));
            Assert.That(copy.documentKind,Is.EqualTo(DocumentKind.DraftPack)); Assert.That(copy.contentRevision,Is.Zero);
        }
        [TestCase(DocumentKind.DraftPack)] [TestCase(DocumentKind.PlayablePack)]
        public void IdentityCopyPreservesDocumentKind(DocumentKind kind)
        {
            var pack=Pack();pack.documentKind=kind;pack.contentRevision=7;
            var copy=ContentIdentity.CreateIndependentCopy(pack);
            Assert.That(copy.documentKind,Is.EqualTo(kind));Assert.That(copy.contentRevision,Is.Zero);
            Assert.That(PackValidator.Validate(copy).IsValid,Is.True);
        }
        [Test] public void AsciiFlipsRowsAndPreservesCornersAndAsymmetricGoal()
        {
            var level=AsciiLevelFactory.Create("#.. "," @$#"," #  ");
            Assert.That(level.terrain[0],Is.Zero); Assert.That(level.terrain[3],Is.Zero);
            Assert.That(level.terrain[8],Is.EqualTo(1)); Assert.That(level.terrain[11],Is.Zero);
            Assert.That(level.features.Any(f=>f.x==1 && f.y==2),Is.True);
            Assert.That(level.entities.Single(e=>e.type==EntityType.Player).y,Is.EqualTo(1));
        }
        [Test] public void NoBoxesIsIncompleteDraftNotInitialWin()
        {
            var level=new LevelData { entities={new EntityData {type=EntityType.Player}} };
            Assert.That(PackValidator.ValidateLevel(level).IsValid,Is.True);
            Assert.That(StructureValidator.Validate(level).Issues.Any(i=>i.Code=="BOX_MISSING"),Is.True);
        }
        [Test] public void WitnessSyntaxIsCheckedButTrustRequiresLaterReplay()
        {
            var pack=Pack(); var witness=new WitnessData {levelId="level1",levelFingerprint=new string('a',64),moves=new string('U',100000)};
            pack.solutionWitnesses.Add(witness); Assert.That(PackValidator.Validate(pack).IsValid,Is.True);
            witness.moves+="U";Assert.That(PackValidator.Validate(pack).IsValid,Is.False);
            witness.moves="Q";Assert.That(PackValidator.Validate(pack).Issues.Any(i=>i.Code=="WITNESS_MOVES_INVALID"),Is.True);
            witness.moves="";witness.levelFingerprint=new string('A',64);
            Assert.That(PackValidator.Validate(pack).Issues.Any(i=>i.Code=="WITNESS_FINGERPRINT_INVALID"),Is.True);
        }
        [Test] public void NullCollectionsAndDuplicateLevelIdsAreRejected()
        {
            var pack=Pack();pack.levels.Add(Level());
            Assert.That(PackValidator.Validate(pack).Issues.Any(i=>i.Code=="LEVEL_ID_DUPLICATE"),Is.True);
            pack=Pack();pack.levels[0].features=null;
            Assert.That(PackValidator.Validate(pack).IsValid,Is.False);
            Assert.DoesNotThrow(()=>StructureValidator.Validate(pack));
        }
        [Test] public void InitialWinIsWarningNotError()
        {
            var level=AsciiLevelFactory.Create("####","#  #","#@*#","####");
            var report=StructureValidator.Validate(level);
            Assert.That(report.IsValid,Is.True); Assert.That(report.Issues.Any(i=>i.Code=="INITIAL_COMPLETED" && i.Severity==IssueSeverity.Warning),Is.True);
        }
    }
}
