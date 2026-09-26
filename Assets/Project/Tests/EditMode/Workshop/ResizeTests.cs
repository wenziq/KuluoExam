using System;
using NUnit.Framework;
using Sokoban.Domain.Workshop;
namespace Sokoban.Tests.EditMode.Workshop
{
    public class ResizeTests
    {
        [Test] public void ResizePreviewCountsThenCropsInOneUndo()
        {
            var doc = EditHistoryTests.Create(); PaintStrokeTests.Paint(doc,PaintTool.Player,9,9); PaintStrokeTests.Paint(doc,PaintTool.Box,8,8); PaintStrokeTests.Paint(doc,PaintTool.Goal,8,8);
            var hash = doc.CurrentHash; int count = doc.UndoCount;
            var resize = new ResizeOperation(doc, doc.SelectedLevelId, 4,4); Assert.That(resize.PlayersRemoved, Is.EqualTo(1)); Assert.That(resize.BoxesRemoved, Is.EqualTo(1)); Assert.That(resize.GoalsRemoved, Is.EqualTo(1));
            Assert.That(doc.Snapshot().levels[0].width, Is.EqualTo(10)); resize.Commit();
            Assert.That(doc.Snapshot().levels[0].entities, Is.Empty); Assert.That(doc.UndoCount, Is.EqualTo(count+1)); doc.Undo(); Assert.That(doc.CurrentHash, Is.EqualTo(hash));
        }
        [Test] public void EmptyShrinkAndMixedShrinkStillRequireConfirmation()
        {
            var doc=EditHistoryTests.Create();
            var shrink=new ResizeOperation(doc,doc.SelectedLevelId,4,4);
            Assert.That(shrink.RequiresCropConfirmation, Is.True); Assert.That(shrink.PlayersRemoved+shrink.BoxesRemoved+shrink.GoalsRemoved, Is.Zero);
            Assert.That(new ResizeOperation(doc,doc.SelectedLevelId,20,4).RequiresCropConfirmation, Is.True);
            Assert.That(new ResizeOperation(doc,doc.SelectedLevelId,20,20).RequiresCropConfirmation, Is.False);
        }
        [Test] public void ExpansionAnchorsLowerLeftAndFillsFloor()
        {
            var doc = EditHistoryTests.Create(); PaintStrokeTests.Paint(doc,PaintTool.Wall,9,9); PaintStrokeTests.Paint(doc,PaintTool.Player,1,1);
            new ResizeOperation(doc,doc.SelectedLevelId,20,20).Commit(); var level=doc.Snapshot().levels[0];
            Assert.That(level.terrain[9*20+9], Is.EqualTo(1)); Assert.That(level.terrain[19*20+19], Is.Zero); Assert.That(level.entities[0].x, Is.EqualTo(1)); Assert.That(level.entities[0].y, Is.EqualTo(1));
        }
        [Test] public void InvalidAndStaleResizeAreAtomicAndSameSizeIsNoOp()
        {
            var doc=EditHistoryTests.Create(); var hash=doc.CurrentHash;
            Assert.Throws<ArgumentOutOfRangeException>(() => new ResizeOperation(doc,doc.SelectedLevelId,3,10)); Assert.Throws<ArgumentOutOfRangeException>(() => new ResizeOperation(doc,doc.SelectedLevelId,10,21));
            Assert.That(new ResizeOperation(doc,doc.SelectedLevelId,10,10).Commit(), Is.False); Assert.That(doc.CurrentHash, Is.EqualTo(hash)); Assert.That(doc.UndoCount, Is.Zero);
            var preview = new ResizeOperation(doc,doc.SelectedLevelId,4,4); PaintStrokeTests.Paint(doc,PaintTool.Player,9,9); hash=doc.CurrentHash;
            Assert.Throws<InvalidOperationException>(() => preview.Commit()); Assert.That(doc.CurrentHash, Is.EqualTo(hash));
        }
    }
}
