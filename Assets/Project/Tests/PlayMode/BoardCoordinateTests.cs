using NUnit.Framework;
using UnityEngine;
using Sokoban.Runtime.Presentation.Board;
namespace Sokoban.PlayModeTests
{
    public class BoardCoordinateTests
    {
        [Test]
        public void LocalCoordinatesRespectCornersAndExclusiveUpperBounds()
        {
            var r = new Rect(-140, -120, 280, 240);
            Assert.That(BoardCoordinateMapper.TryMapLocal(r, new Vector2(-139, -119), 7, 6, out var cell), Is.True);
            Assert.That(cell, Is.EqualTo(new Vector2Int(0, 0)));
            Assert.That(BoardCoordinateMapper.TryMapLocal(r, new Vector2(139, 119), 7, 6, out cell), Is.True);
            Assert.That(cell, Is.EqualTo(new Vector2Int(6, 5)));
            Assert.That(BoardCoordinateMapper.TryMapLocal(r, new Vector2(140, 0), 7, 6, out cell), Is.False);
            Assert.That(BoardCoordinateMapper.TryMapLocal(r, new Vector2(-141, 0), 7, 6, out cell), Is.False);
            Assert.That(BoardCoordinateMapper.TryMapLocal(r, new Vector2(0, 120), 7, 6, out cell), Is.False);
        }
        [TestCase(1f, .5f)]
        [TestCase(1.5f, 2f)]
        public void ScreenMappingUsesActualCanvasTransformAndViewport(float canvasScale, float zoom)
        {
            var root = new GameObject("Canvas", typeof(Canvas));
            try
            {
                var canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.scaleFactor = canvasScale;
                var viewport = new GameObject("Viewport", typeof(RectTransform)).GetComponent<RectTransform>();
                viewport.SetParent(root.transform, false);
                viewport.sizeDelta = new Vector2(400, 300);
                var board = new GameObject("Board", typeof(RectTransform)).GetComponent<RectTransform>();
                board.SetParent(viewport, false);
                board.sizeDelta = new Vector2(280, 240);
                board.localScale = Vector3.one * zoom;
                board.anchoredPosition = new Vector2(17, -11);
                Canvas.ForceUpdateCanvases();
                var screen = RectTransformUtility.WorldToScreenPoint(null, board.TransformPoint(new Vector3(-139, -119)));
                bool visible = RectTransformUtility.RectangleContainsScreenPoint(viewport, screen, null);
                Assert.That(BoardCoordinateMapper.TryMapScreen(board, viewport, screen, null, 7, 6, out var cell), Is.EqualTo(visible));
                if (visible)
                    Assert.That(cell, Is.EqualTo(Vector2Int.zero));
                var inside = RectTransformUtility.WorldToScreenPoint(null, board.TransformPoint(new Vector3(10, -20)));
                Assert.That(BoardCoordinateMapper.TryMapScreen(board, viewport, inside, null, 7, 6, out var middle), Is.True);
                Assert.That(middle, Is.EqualTo(new Vector2Int(3, 2)));
                var outside = RectTransformUtility.WorldToScreenPoint(null, viewport.TransformPoint(new Vector3(201, 0)));
                if (zoom == 2)
                    Assert.That(RectTransformUtility.RectangleContainsScreenPoint(board, outside, null), Is.True, "The clip rejection must cover a point that is still inside the zoomed board.");
                Assert.That(BoardCoordinateMapper.TryMapScreen(board, viewport, outside, null, 7, 6, out _), Is.False);
            }
            finally { Object.DestroyImmediate(root); }
        }
        [Test]
        public void InvalidGeometryNeverMaps()
        {
            Assert.That(BoardCoordinateMapper.TryMapLocal(new Rect(0, 0, 0, 5), Vector2.zero, 4, 4, out _), Is.False);
            Assert.That(BoardCoordinateMapper.TryMapLocal(new Rect(0, 0, 5, 5), Vector2.zero, 0, 4, out _), Is.False);
        }
    }
}
