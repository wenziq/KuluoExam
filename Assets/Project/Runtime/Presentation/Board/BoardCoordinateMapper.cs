using UnityEngine;
namespace Sokoban.Runtime.Presentation.Board
{
    public static class BoardCoordinateMapper
    {
        public static bool TryMapLocal(Rect rect, Vector2 local, int width, int height, out Vector2Int cell)
        {
            cell = default;
            if (width <= 0 || height <= 0 || rect.width <= 0 || rect.height <= 0 || !rect.Contains(local))
                return false;
            cell = new Vector2Int(Mathf.FloorToInt((local.x - rect.xMin) * width / rect.width), Mathf.FloorToInt((local.y - rect.yMin) * height / rect.height));
            return cell.x >= 0 && cell.y >= 0 && cell.x < width && cell.y < height;
        }
        public static bool TryMapScreen(RectTransform board, RectTransform viewport, Vector2 screen, Camera camera, int width, int height, out Vector2Int cell)
        {
            cell = default;
            if (board == null || viewport == null || !RectTransformUtility.RectangleContainsScreenPoint(viewport, screen, camera))
                return false;
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(board, screen, camera, out var local) && TryMapLocal(board.rect, local, width, height, out cell);
        }
    }
}
