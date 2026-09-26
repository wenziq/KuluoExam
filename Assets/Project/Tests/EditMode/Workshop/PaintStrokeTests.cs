using System;
using NUnit.Framework;
using Sokoban.Core.Data;
using Sokoban.Domain.Workshop;
namespace Sokoban.Tests.EditMode.Workshop
{
    public class PaintStrokeTests
    {
        internal static bool Paint(WorkshopDocument doc, PaintTool tool, int x, int y)
        { using (var stroke = doc.BeginStroke(tool)) { stroke.AddPoint(x,y); return stroke.Commit(); } }
        [Test] public void ContinuousStrokeIsOneUndoAndEscapeRestoresWholeStroke()
        {
            var doc = EditHistoryTests.Create(); var hash = doc.CurrentHash;
            using (var stroke = doc.BeginStroke(PaintTool.Wall)) { stroke.AddPoint(1,1); stroke.AddPoint(5,1); stroke.AddPoint(1,1); stroke.Commit(); }
            Assert.That(doc.UndoCount, Is.EqualTo(1)); for (int x = 1; x <= 5; x++) Assert.That(doc.Snapshot().levels[0].terrain[10+x], Is.EqualTo(1));
            doc.Undo(); Assert.That(doc.CurrentHash, Is.EqualTo(hash));
            using (var stroke = doc.BeginStroke(PaintTool.Wall)) { stroke.AddPoint(1,1); stroke.AddPoint(8,8); stroke.Cancel(); }
            Assert.That(doc.CurrentHash, Is.EqualTo(hash)); Assert.That(doc.RedoCount, Is.EqualTo(1));
        }
        [Test] public void OutsideBreaksSegmentAndFocusLossCommitsValidParts()
        {
            var doc = EditHistoryTests.Create();
            using (var stroke = doc.BeginStroke(PaintTool.Wall))
            { stroke.AddPoint(1,1); stroke.AddPoint(int.MinValue,int.MaxValue); stroke.AddPoint(8,1); Assert.That(stroke.OnFocusLost(), Is.True); }
            var level = doc.Snapshot().levels[0]; Assert.That(level.terrain[11], Is.EqualTo(1)); Assert.That(level.terrain[18], Is.EqualTo(1));
            for (int x = 2; x < 8; x++) Assert.That(level.terrain[10+x], Is.Zero);
            Assert.That(doc.UndoCount, Is.EqualTo(1));
        }
        [Test] public void BresenhamFillsDiagonalAndOutsideOnlyDoesNotCreateHistory()
        {
            var doc = EditHistoryTests.Create(); using (var stroke = doc.BeginStroke(PaintTool.Wall)) { stroke.AddPoint(1,1); stroke.AddPoint(5,5); stroke.Commit(); }
            for (int i = 1; i <= 5; i++) Assert.That(doc.Snapshot().levels[0].terrain[i*10+i], Is.EqualTo(1));
            using (var stroke = doc.BeginStroke(PaintTool.Wall)) { stroke.AddPoint(-1,0); stroke.AddPoint(10,0); Assert.That(stroke.Commit(), Is.False); }
            Assert.That(doc.UndoCount, Is.EqualTo(1));
        }
        [TestCase(PaintTool.Floor, 0, 1, 1)]
        [TestCase(PaintTool.EraseTerrain, 0, 1, 1)]
        [TestCase(PaintTool.Wall, 1, 0, 0)]
        [TestCase(PaintTool.EraseEntity, 0, 0, 1)]
        [TestCase(PaintTool.EraseGoal, 0, 1, 0)]
        public void CoverageAndErasersModifyOnlyTheirContractedLayers(PaintTool tool, int terrain, int entities, int features)
        {
            var doc = EditHistoryTests.Create(); Paint(doc,PaintTool.Goal,2,2); Paint(doc,PaintTool.Box,2,2); var hash = doc.CurrentHash;
            bool changed = Paint(doc,tool,2,2); var level = doc.Snapshot().levels[0];
            Assert.That(level.terrain[22], Is.EqualTo(terrain)); Assert.That(level.entities.Count, Is.EqualTo(entities)); Assert.That(level.features.Count, Is.EqualTo(features));
            if (changed) { doc.Undo(); Assert.That(doc.CurrentHash, Is.EqualTo(hash)); }
        }
        [Test] public void FloorAndTerrainEraserPreserveOtherLayersEvenInIncompleteDraft()
        {
            foreach (var tool in new[] { PaintTool.Floor, PaintTool.EraseTerrain })
            {
                var doc = EditHistoryTests.Create(); doc.Edit("draft",doc.SelectedLevelId,p => { p.levels[0].terrain[22] = 1; p.levels[0].entities.Add(new EntityData { type=EntityType.Box,x=2,y=2 }); p.levels[0].features.Add(new FeatureData { x=2,y=2 }); });
                Paint(doc,tool,2,2); var level = doc.Snapshot().levels[0]; Assert.That(level.terrain[22], Is.Zero); Assert.That(level.entities.Count, Is.EqualTo(1)); Assert.That(level.features.Count, Is.EqualTo(1));
            }
        }
        [Test] public void PlayerMovesIdentityAndCannotReplaceBoxOrWall()
        {
            var doc = EditHistoryTests.Create(); Paint(doc,PaintTool.Player,1,1); var playerId = doc.Snapshot().levels[0].entities[0].id;
            Paint(doc,PaintTool.Goal,2,2); Paint(doc,PaintTool.Player,2,2);
            var level = doc.Snapshot().levels[0]; Assert.That(level.entities.Count, Is.EqualTo(1)); Assert.That(level.entities[0].id, Is.EqualTo(playerId)); Assert.That(level.features.Count, Is.EqualTo(1));
            Paint(doc,PaintTool.Box,3,3); Assert.That(Paint(doc,PaintTool.Player,3,3), Is.False);
            Paint(doc,PaintTool.Wall,4,4); Assert.That(Paint(doc,PaintTool.Player,4,4), Is.False); Assert.That(Paint(doc,PaintTool.Goal,4,4), Is.False); Assert.That(Paint(doc,PaintTool.Box,4,4), Is.False);
            Assert.That(Paint(doc,PaintTool.Box,2,2), Is.False); Assert.That(Paint(doc,PaintTool.Box,3,3), Is.False); Assert.That(Paint(doc,PaintTool.Goal,2,2), Is.False);
        }
        [Test] public void HoverPreviewHandlesEmptyInvalidAndOutsideWithoutStartingGesture()
        {
            var empty=new WorkshopDocument(new PackData()); Assert.That(empty.PreviewPaint(PaintTool.Wall,0,0).CanApply, Is.False); Assert.That(empty.PreviewPaint(PaintTool.Wall,0,0).Reason, Is.Not.Empty);
            var doc=EditHistoryTests.Create(); var hash=doc.CurrentHash;
            Assert.That(doc.PreviewPaint((PaintTool)999,0,0).CanApply, Is.False); Assert.That(doc.PreviewPaint((PaintTool)999,0,0).Reason, Is.Not.Empty);
            Assert.That(doc.PreviewPaint(PaintTool.Wall,-1,0).CanApply, Is.False); Assert.That(doc.PreviewPaint(PaintTool.Wall,2,2).CanApply, Is.True);
            Assert.That(doc.CurrentHash, Is.EqualTo(hash)); Assert.That(doc.UndoCount, Is.Zero); Assert.That(doc.HasActiveStroke, Is.False);
            Paint(doc,PaintTool.Player,2,2); var preview=doc.PreviewPaint(PaintTool.Wall,2,2); Assert.That(preview.PlayersRemoved, Is.EqualTo(1));
        }
        [Test] public void PreviewDescribesWallOverwriteAndPlacementRejectionWithoutEditing()
        {
            var doc=EditHistoryTests.Create(); Paint(doc,PaintTool.Box,2,2); Paint(doc,PaintTool.Goal,2,2); var hash=doc.CurrentHash;
            using(var stroke=doc.BeginStroke(PaintTool.Wall)) { var preview=stroke.PreviewPoint(2,2); Assert.That(preview.CanApply, Is.True); Assert.That(preview.BoxesRemoved, Is.EqualTo(1)); Assert.That(preview.GoalsRemoved, Is.EqualTo(1)); Assert.That(doc.CurrentHash, Is.EqualTo(hash)); }
            using(var stroke=doc.BeginStroke(PaintTool.Player)) { var preview=stroke.PreviewPoint(2,2); Assert.That(preview.CanApply, Is.False); Assert.That(preview.Reason, Is.Not.Empty); }
            Paint(doc,PaintTool.Wall,3,3); Assert.That(doc.LastOperationDescription, Is.EqualTo("绘制：墙"));
        }
        [Test] public void BoxLimitAndGestureOwnershipDoNotCorruptDraft()
        {
            var doc = EditHistoryTests.Create(); for (int i = 0; i < 16; i++) Paint(doc,PaintTool.Box,i%10,i/10);
            Assert.That(Paint(doc,PaintTool.Box,8,8), Is.False); Assert.That(doc.Snapshot().levels[0].entities.Count, Is.EqualTo(16));
            using (var stroke = doc.BeginStroke(PaintTool.Wall))
            { Assert.Throws<InvalidOperationException>(() => doc.Undo()); Assert.Throws<InvalidOperationException>(() => doc.BeginStroke(PaintTool.Box)); stroke.Cancel(); }
        }
    }
}
