using System;
using NUnit.Framework;
using Sokoban.Core.Data;
using Sokoban.Domain.Workshop;
namespace Sokoban.Tests.EditMode.Workshop
{
    public class LevelOperationsTests
    {
        [Test] public void DuplicateInsertsAfterSourceAndDeleteLastIsUndoable()
        {
            var doc = EditHistoryTests.Create(); var source = doc.SelectedLevelId;
            PaintStrokeTests.Paint(doc,PaintTool.Player,1,1); PaintStrokeTests.Paint(doc,PaintTool.Goal,2,2); var original=doc.Snapshot().levels[0];
            var copy = LevelOperations.Duplicate(doc, source); Assert.That(copy, Is.Not.EqualTo(source));
            var pack=doc.Snapshot(); Assert.That(pack.levelOrder[1], Is.EqualTo(copy)); Assert.That(pack.levels[1].entities[0].id, Is.Not.EqualTo(original.entities[0].id)); Assert.That(pack.levels[1].features[0].id, Is.Not.EqualTo(original.features[0].id));
            Assert.That(pack.levels[1].entities[0].x, Is.EqualTo(original.entities[0].x)); Assert.That(doc.SelectedLevelId, Is.EqualTo(copy));
            LevelOperations.Delete(doc, copy); LevelOperations.Delete(doc, source);
            Assert.That(doc.SelectedLevelId, Is.Null); Assert.That(doc.Undo(), Is.True); Assert.That(doc.SelectedLevelId, Is.EqualTo(source)); Assert.That(doc.Snapshot().levels[0].entities[0].id, Is.EqualTo(original.entities[0].id));
        }
        [Test] public void NewLevelDefaultsAndMaximumCountAreEnforcedAtomically()
        {
            var doc=new WorkshopDocument(new PackData()); var first=LevelOperations.Add(doc); var level=doc.Snapshot().levels[0];
            Assert.That(level.width, Is.EqualTo(10)); Assert.That(level.height, Is.EqualTo(10)); Assert.That(level.entities, Is.Empty); Assert.That(level.features, Is.Empty);
            for(int y=0;y<10;y++) for(int x=0;x<10;x++) Assert.That(level.terrain[y*10+x], Is.EqualTo(x==0 || y==0 || x==9 || y==9 ? 1:0));
            for(int i=1;i<30;i++) LevelOperations.Add(doc); var hash=doc.CurrentHash;
            Assert.Throws<ArgumentException>(() => LevelOperations.Add(doc)); Assert.Throws<ArgumentException>(() => LevelOperations.Duplicate(doc,first)); Assert.That(doc.CurrentHash, Is.EqualTo(hash)); Assert.That(doc.Snapshot().levels.Count, Is.EqualTo(30));
        }
        [Test] public void ReorderRenameAndCrossLevelUndoPreserveIdentityAndChooseRelevantLevel()
        {
            var doc=EditHistoryTests.Create(); var a=doc.SelectedLevelId; var b=LevelOperations.Add(doc); var c=LevelOperations.Add(doc);
            LevelOperations.Reorder(doc,c,1); CollectionAssert.AreEqual(new[]{a,c,b},doc.Snapshot().levelOrder);
            LevelOperations.Rename(doc,a,"名称"); doc.SelectLevel(c); int history=doc.UndoCount;
            Assert.That(doc.UndoCount, Is.EqualTo(history)); doc.Undo(); Assert.That(doc.SelectedLevelId, Is.EqualTo(a)); Assert.That(doc.LastOperationDescription, Is.EqualTo("修改关卡名称"));
            doc.Redo(); Assert.That(doc.Snapshot().levels.Find(item=>item.levelId==a).name, Is.EqualTo("名称"));
            var hash=doc.CurrentHash; Assert.Throws<ArgumentOutOfRangeException>(()=>LevelOperations.Reorder(doc,a,99)); Assert.That(doc.CurrentHash, Is.EqualTo(hash));
        }
        [Test] public void MetadataCommitIsOneHistoryItemAndInvalidTextRollsBack()
        {
            var doc=EditHistoryTests.Create(); var id=doc.SelectedLevelId;
            LevelOperations.SetLevelMetadata(doc,id,"名字","完整备注",IntendedDifficulty.Hard); Assert.That(doc.UndoCount, Is.EqualTo(1));
            doc.Undo(); Assert.That(doc.Snapshot().levels[0].name, Is.Empty); Assert.That(doc.Snapshot().levels[0].designNotes, Is.Empty);
            var hash=doc.CurrentHash; Assert.Throws<ArgumentException>(()=>LevelOperations.SetLevelMetadata(doc,id,new string('x',81),"notes",IntendedDifficulty.Easy)); Assert.That(doc.CurrentHash, Is.EqualTo(hash));
            Assert.Throws<ArgumentException>(()=>LevelOperations.SetPackMetadata(doc,"pack",new string('x',2001),UnlockPolicy.AllOpen)); Assert.That(doc.CurrentHash, Is.EqualTo(hash));
            LevelOperations.SetPackMetadata(doc,"pack","description",UnlockPolicy.AllOpen); Assert.That(doc.Snapshot().unlockPolicy, Is.EqualTo(UnlockPolicy.AllOpen));
        }
        [Test] public void SelectedObjectDeleteRemovesOnlyNamedLayerAndUndoRestoresId()
        {
            var doc=EditHistoryTests.Create(); PaintStrokeTests.Paint(doc,PaintTool.Goal,1,1); PaintStrokeTests.Paint(doc,PaintTool.Box,1,1);
            var level=doc.Snapshot().levels[0]; var entity=level.entities[0].id; var goal=level.features[0].id;
            LevelOperations.DeleteEntity(doc,level.levelId,entity); Assert.That(doc.Snapshot().levels[0].features.Count, Is.EqualTo(1)); doc.Undo(); Assert.That(doc.Snapshot().levels[0].entities[0].id, Is.EqualTo(entity));
            LevelOperations.DeleteFeature(doc,level.levelId,goal); Assert.That(doc.Snapshot().levels[0].entities.Count, Is.EqualTo(1));
            PaintStrokeTests.Paint(doc,PaintTool.Wall,2,2); LevelOperations.DeleteWall(doc,level.levelId,2,2); Assert.That(doc.Snapshot().levels[0].terrain[22], Is.Zero);
        }
        [Test] public void DeletingLevelDropsItsWitnessAndUndoRestoresHistoricalReference()
        {
            var doc=EditHistoryTests.Create(); var id=doc.SelectedLevelId;
            doc.Edit("derived",id,p=>p.solutionWitnesses.Add(new WitnessData { levelId=id,levelFingerprint=new string('a',64) }));
            LevelOperations.Delete(doc,id); Assert.That(doc.Snapshot().solutionWitnesses, Is.Empty); doc.Undo(); Assert.That(doc.Snapshot().solutionWitnesses[0].levelId, Is.EqualTo(id));
        }
    }
}
