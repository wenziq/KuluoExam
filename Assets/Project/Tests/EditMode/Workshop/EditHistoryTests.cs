using System;
using NUnit.Framework;
using Sokoban.Core.Data;
using Sokoban.Core.Identity;
using Sokoban.Domain.Workshop;
namespace Sokoban.Tests.EditMode.Workshop
{
    public class EditHistoryTests
    {
        internal static WorkshopDocument Create() { var pack = new PackData(); var level = new LevelData(); pack.levels.Add(level); pack.levelOrder.Add(level.levelId); return new WorkshopDocument(pack); }
        [Test] public void SaveSnapshotDoesNotSaveLaterEditsAndUndoReturnsToSavePoint()
        {
            var doc = Create(); LevelOperations.Rename(doc, doc.SelectedLevelId, "S"); var hash = doc.CurrentHash;
            LevelOperations.Rename(doc, doc.SelectedLevelId, "S+1"); doc.MarkSaved(hash);
            Assert.That(doc.IsDirty, Is.True); Assert.That(doc.UndoCount, Is.EqualTo(2)); Assert.That(doc.Undo(), Is.True); Assert.That(doc.IsDirty, Is.False);
            Assert.That(doc.Redo(), Is.True); Assert.That(doc.IsDirty, Is.True);
        }
        [Test] public void NewDraftHasNoSavedPointUntilActualManualCommit()
        {
            var doc=new WorkshopDocument(new PackData()); doc.MarkUnsaved(); Assert.That(doc.SavedHash, Is.Null); Assert.That(doc.IsDirty, Is.True);
            LevelOperations.Add(doc); doc.Undo(); Assert.That(doc.Snapshot().levels, Is.Empty); Assert.That(doc.IsDirty, Is.True);
            doc.MarkSaved(doc.CurrentHash); Assert.That(doc.IsDirty, Is.False);
        }
        [Test] public void InvalidOrThrowingEditIsAtomicAndSnapshotIsIsolated()
        {
            var doc = Create(); var hash = doc.CurrentHash;
            Assert.Throws<ArgumentException>(() => doc.Edit("invalid", doc.SelectedLevelId, p => p.levels[0].width = 99));
            Assert.Throws<InvalidOperationException>(() => doc.Edit("failure", doc.SelectedLevelId, p => { p.name = "changed"; throw new InvalidOperationException(); }));
            doc.Snapshot().levels.Clear(); Assert.That(doc.CurrentHash, Is.EqualTo(hash)); Assert.That(doc.UndoCount, Is.Zero);
            PackData captured = null; doc.Edit("captured",doc.SelectedLevelId,p => { captured=p; p.name="new"; }); hash=doc.CurrentHash;
            captured.levels.Clear(); captured.name="escaped"; Assert.That(doc.CurrentHash, Is.EqualTo(hash)); doc.Undo(); doc.Redo(); Assert.That(doc.CurrentHash, Is.EqualTo(hash));
        }
        [Test] public void ReentrantCommandsCannotEscapeTransactionRollback()
        {
            var doc=Create(); var hash=doc.CurrentHash;
            Assert.Throws<InvalidOperationException>(()=>doc.Edit("outer",doc.SelectedLevelId,p=> { p.name="temporary"; doc.Edit("nested",doc.SelectedLevelId,q=>q.name="escaped"); }));
            Assert.That(doc.CurrentHash, Is.EqualTo(hash)); Assert.That(doc.UndoCount, Is.Zero);
            Assert.That(LevelOperations.Rename(doc,doc.SelectedLevelId,"subsequent"), Is.True);
        }
        [Test] public void InputRootNoOpsAndNewEditAfterUndoRemainIsolated()
        {
            var pack = new PackData(); var level = new LevelData(); pack.levels.Add(level); pack.levelOrder.Add(level.levelId); var doc=new WorkshopDocument(pack);
            pack.levels.Clear(); Assert.That(doc.Snapshot().levels.Count, Is.EqualTo(1)); Assert.That(LevelOperations.Rename(doc,level.levelId,""), Is.False);
            LevelOperations.Rename(doc,level.levelId,"A"); LevelOperations.Rename(doc,level.levelId,"B"); doc.Undo();
            Assert.That(LevelOperations.Rename(doc,level.levelId,"A"), Is.False); Assert.That(doc.RedoCount, Is.EqualTo(1));
            LevelOperations.Rename(doc,level.levelId,"C"); Assert.That(doc.RedoCount, Is.Zero); Assert.That(doc.Redo(), Is.False);
        }
        [Test] public void CountAndMemoryEvictionDoNotLoseActiveContent()
        {
            var pack=Create().Snapshot(); var doc=new WorkshopDocument(pack,2);
            for(int i=0;i<5;i++) LevelOperations.Rename(doc,doc.SelectedLevelId,"name"+i);
            Assert.That(doc.UndoCount, Is.EqualTo(2)); Assert.That(doc.Snapshot().levels[0].name, Is.EqualTo("name4"));
            Assert.That(doc.Undo(), Is.True); Assert.That(doc.Undo(), Is.True); Assert.That(doc.Undo(), Is.False); Assert.That(doc.Snapshot().levels[0].name, Is.EqualTo("name2"));
            var tiny=new WorkshopDocument(pack,100,1); LevelOperations.Rename(tiny,tiny.SelectedLevelId,"survives"); Assert.That(tiny.UndoCount, Is.Zero); Assert.That(tiny.HistoryEstimatedBytes, Is.Zero); Assert.That(tiny.Snapshot().levels[0].name, Is.EqualTo("survives"));
        }
        [Test] public void ThirtyTwentyByTwentyLevelsUseContentSizedBudget()
        {
            var pack=new PackData(); for(int i=0;i<30;i++) { var level=new LevelData { width=20,height=20,terrain=new int[400],designNotes=new string('文',2000) }; pack.levels.Add(level); pack.levelOrder.Add(level.levelId); }
            var measuring=new WorkshopDocument(pack); LevelOperations.Rename(measuring,measuring.SelectedLevelId,"A"); long oneEntry=measuring.HistoryEstimatedBytes;
            Assert.That(oneEntry, Is.GreaterThan(30L*400*4*2));
            var bounded=new WorkshopDocument(pack,100,oneEntry+1024); for(int i=0;i<8;i++) LevelOperations.Rename(bounded,bounded.SelectedLevelId,"N"+i);
            Assert.That(bounded.HistoryEstimatedBytes, Is.LessThanOrEqualTo(oneEntry+1024)); Assert.That(bounded.UndoCount, Is.EqualTo(1)); Assert.That(bounded.Snapshot().levels.Count, Is.EqualTo(30)); Assert.That(bounded.Snapshot().levels[0].name, Is.EqualTo("N7"));
            var hundred=new WorkshopDocument(pack); for(int i=0;i<105;i++) LevelOperations.Rename(hundred,hundred.SelectedLevelId,"N"+i);
            Assert.That(hundred.UndoCount, Is.EqualTo(100)); Assert.That(hundred.HistoryEstimatedBytes, Is.LessThanOrEqualTo(64L*1024*1024));
        }
        [Test] public void DerivedEvidenceAndRevisionDoNotDirtyButGameplayLeavesOldReferenceStale()
        {
            var pack=new PackData(); var level=AsciiLevelFactory.Create("#####","#@$.#","#   #","#####"); pack.levels.Add(level); pack.levelOrder.Add(level.levelId); var doc=new WorkshopDocument(pack);
            string fingerprint=LevelFingerprint.Compute(level);
            Assert.That(doc.Edit("evidence",level.levelId,p => { p.contentRevision=2; p.solutionWitnesses.Add(new WitnessData { levelId=level.levelId,levelFingerprint=fingerprint,moves="R" }); }), Is.False);
            Assert.That(doc.IsDirty, Is.False); Assert.That(doc.UndoCount, Is.Zero);
            PaintStrokeTests.Paint(doc,PaintTool.Wall,2,1); var changed=doc.Snapshot(); Assert.That(changed.solutionWitnesses[0].levelFingerprint, Is.EqualTo(fingerprint)); Assert.That(LevelFingerprint.Compute(changed.levels[0]), Is.Not.EqualTo(fingerprint)); Assert.That(doc.IsDirty, Is.True);
        }
        [Test] public void ViewportIsDefensiveAndIndependentOfHistory()
        {
            var doc=Create(); var viewport=new WorkshopViewportState(); var state=viewport.Get(doc.SelectedLevelId); state.Zoom=2; state.PanX=7; state.SelectedObjectId="selected"; state.SelectionLayer=SelectionLayer.Terrain; state.SelectedCellX=3; state.SelectedCellY=4; state.IsSelectionTool=true; viewport.Set(doc.SelectedLevelId,state);
            state.Zoom=99; var read=viewport.Get(doc.SelectedLevelId); Assert.That(read.Zoom, Is.EqualTo(2)); Assert.That(read.SelectionLayer, Is.EqualTo(SelectionLayer.Terrain)); Assert.That(read.SelectedCellX, Is.EqualTo(3)); Assert.That(read.SelectedCellY, Is.EqualTo(4)); Assert.That(read.IsSelectionTool, Is.True); read.PanX=99; Assert.That(viewport.Get(doc.SelectedLevelId).PanX, Is.EqualTo(7));
            Assert.That(doc.IsDirty, Is.False); Assert.That(doc.UndoCount, Is.Zero); Assert.Throws<ArgumentException>(() => viewport.Set(doc.SelectedLevelId,new LevelViewportState { Zoom=float.NaN }));
        }
    }
}
