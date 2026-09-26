using System;
using System.Collections.Generic;
using Sokoban.Core.Data;
using Sokoban.Core.Rules;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Sokoban.Runtime.Presentation.Style;
namespace Sokoban.Runtime.Presentation.Board
{
    public sealed class BoardView : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, IScrollHandler
    {
        public RectTransform viewport, boardRoot, terrainLayer, featureLayer, entityLayer, overlayLayer;
        public event Action<Vector2Int, PointerEventData> CellPressed, CellDragged;
        public event Action<PointerEventData> PointerReleased;
        public event Action PanStarted;
        public Func<bool> CanInteract;
        public bool IsPanning { get; private set; }
        private readonly Dictionary<string, BoardTileGraphic> entities = new Dictionary<string, BoardTileGraphic>();
        private readonly Dictionary<int, BoardTileGraphic> terrain = new Dictionary<int, BoardTileGraphic>();
        private readonly Dictionary<string, BoardTileGraphic> features = new Dictionary<string, BoardTileGraphic>();
        private LevelData level;
        public float maxCellSize = 60;
        public Vector2 fitPadding = new Vector2(100, 140);
        public float Zoom { get; private set; } = 1;
        public Vector2 Pan
        {
            get; private set;
        }
        public int TerrainObjectCount => terrain.Count;
        public int Width => level?.width ?? 0;
        public int Height => level?.height ?? 0;
        public static BoardView Create(Transform parent)
        {
            var hit = UiFactory.Panel("BoardViewport", parent, UiTheme.Hex("141714"), true);
            hit.gameObject.AddComponent<RectMask2D>();
            var view = hit.gameObject.AddComponent<BoardView>();
            view.viewport = hit.rectTransform;
            view.boardRoot = UiFactory.Rect("BoardRoot", view.viewport);
            view.boardRoot.anchorMin = view.boardRoot.anchorMax = new Vector2(.5f, .5f);
            view.terrainLayer = Layer("TerrainLayer");
            view.featureLayer = Layer("FeatureLayer");
            view.entityLayer = Layer("EntityLayer");
            view.overlayLayer = Layer("OverlayLayer");
            return view;
            RectTransform Layer(string name)
            {
                var r = UiFactory.Rect(name, view.boardRoot);
                UiFactory.Fill(r);
                return r;
            }
        }
        public void Show(LevelData snapshot, bool resetView = true)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));
            if (snapshot.width < 1 || snapshot.height < 1 || snapshot.terrain == null || snapshot.terrain.Length != snapshot.width * snapshot.height)
                throw new ArgumentException("Invalid board dimensions");
            bool resize = level == null || level.width != snapshot.width || level.height != snapshot.height;
            level = snapshot.DeepCopy();
            if (resize)
            {
                Clear(terrainLayer);
                Clear(featureLayer);
                Clear(entityLayer);
                Clear(overlayLayer);
                terrain.Clear();
                features.Clear();
                entities.Clear();
            }
            for (int i = 0; i < level.terrain.Length; i++)
            {
                if (!terrain.TryGetValue(i, out var graphic))
                {
                    graphic = Tile(terrainLayer, "Cell" + i);
                    terrain.Add(i, graphic);
                }
                graphic.SetSymbol(level.terrain[i] == 1 ? BoardSymbol.Wall : BoardSymbol.Floor);
                Position(graphic.rectTransform, i % Width, i / Width);
            }
            SyncFeatures();
            SyncEntities(level.entities);
            if (resetView)
            {
                Zoom = 1;
                Pan = Vector2.zero;
            }
            ApplyTransform();
        }
        public void RefreshEntities(BoardState state)
        {
            if (level == null || state.Width != Width || state.Height != Height)
                throw new InvalidOperationException("Show level geometry before state");
            foreach (var entity in level.entities)
            {
                var p = state.Player;
                if (entity.type == EntityType.Box)
                {
                    for (int i = 0; i < state.BoxCount; i++)
                        if (state.GetBoxId(i) == entity.id)
                        {
                            p = state.GetBoxPosition(i);
                            break;
                        }
                }
                entity.x = p.x;
                entity.y = p.y;
            }
            SyncEntities(level.entities);
        }
        public void InterpolateEntities(BoardState from, BoardState to, float progress)
        {
            RefreshEntities(to);
            float t = Mathf.Clamp01(progress);
            foreach (var entity in level.entities)
            {
                Coordinate start = from.Player;
                Coordinate end = to.Player;
                if (entity.type == EntityType.Box)
                {
                    for (int i = 0; i < to.BoxCount; i++)
                        if (to.GetBoxId(i) == entity.id)
                        {
                            start = from.GetBoxPosition(i);
                            end = to.GetBoxPosition(i);
                            break;
                        }
                }
                float x = Mathf.Lerp(start.x, end.x, t);
                float y = Mathf.Lerp(start.y, end.y, t);
                var rect = entities[entity.id].rectTransform;
                rect.anchorMin = new Vector2(x / Width, y / Height);
                rect.anchorMax = new Vector2((x + 1) / Width, (y + 1) / Height);
            }
        }
        private void SyncFeatures()
        {
            var live = new HashSet<string>();
            foreach (var f in level.features)
            {
                live.Add(f.id);
                if (!features.TryGetValue(f.id, out var g))
                {
                    g = Tile(featureLayer, f.id);
                    features.Add(f.id, g);
                }
                g.SetSymbol(BoardSymbol.Goal);
                Position(g.rectTransform, f.x, f.y);
            }
            RemoveMissing(features, live);
        }
        private void SyncEntities(List<EntityData> data)
        {
            var live = new HashSet<string>();
            foreach (var e in data)
            {
                live.Add(e.id);
                if (!entities.TryGetValue(e.id, out var g))
                {
                    g = Tile(entityLayer, e.id);
                    entities.Add(e.id, g);
                }
                bool goal = level.features.Exists(f => f.x == e.x && f.y == e.y);
                g.SetSymbol(e.type == EntityType.Player ? BoardSymbol.Player : goal ? BoardSymbol.BoxOnGoal : BoardSymbol.Box);
                Position(g.rectTransform, e.x, e.y);
            }
            RemoveMissing(entities, live);
        }
        private static void RemoveMissing(Dictionary<string, BoardTileGraphic> items, HashSet<string> live)
        {
            var remove = new List<string>();
            foreach (var pair in items)
                if (!live.Contains(pair.Key))
                {
                    pair.Value.gameObject.SetActive(false);
                    Destroy(pair.Value.gameObject);
                    remove.Add(pair.Key);
                }
            foreach (var id in remove)
                items.Remove(id);
        }
        private void Position(RectTransform rect, int x, int y)
        {
            rect.anchorMin = new Vector2((float)x / Width, (float)y / Height);
            rect.anchorMax = new Vector2((float)(x + 1) / Width, (float)(y + 1) / Height);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
        private static BoardTileGraphic Tile(Transform parent, string name)
        {
            var r = UiFactory.Rect(name, parent);
            var g = r.gameObject.AddComponent<BoardTileGraphic>();
            g.raycastTarget = false;
            g.color = UiTheme.Hex("c3e997");
            return g;
        }
        public void SetOverlay(IEnumerable<Vector2Int> cells, BoardSymbol symbol)
        {
            Clear(overlayLayer);
            foreach (var cell in cells)
            {
                var tile = Tile(overlayLayer, symbol.ToString());
                tile.SetSymbol(symbol);
                Position(tile.rectTransform, cell.x, cell.y);
            }
        }
        public void SetView(float zoom, Vector2 pan)
        {
            Zoom = Mathf.Clamp(zoom, .35f, 3f);
            Pan = new Vector2(float.IsNaN(pan.x) || float.IsInfinity(pan.x) ? 0 : pan.x, float.IsNaN(pan.y) || float.IsInfinity(pan.y) ? 0 : pan.y);
            ApplyTransform();
        }
        public void Fit()
        {
            SetView(1, Vector2.zero);
        }
        private void OnRectTransformDimensionsChange()
        {
            if (boardRoot != null)
                ApplyTransform();
        }
        private void ApplyTransform()
        {
            if (level == null || viewport == null || viewport.rect.width<=0 || viewport.rect.height<=0)
                return;
            float size = Mathf.Max(4, Mathf.Min(maxCellSize, (viewport.rect.width - fitPadding.x) / Width, (viewport.rect.height - fitPadding.y) / Height));
            boardRoot.sizeDelta = new Vector2(Width * size, Height * size);
            boardRoot.localScale = Vector3.one * Zoom;
            float limitX=Mathf.Max(0,(boardRoot.sizeDelta.x*Zoom+viewport.rect.width)/2-40);
            float limitY=Mathf.Max(0,(boardRoot.sizeDelta.y*Zoom+viewport.rect.height)/2-40);
            Pan = new Vector2(Mathf.Clamp(Pan.x,-limitX,limitX),Mathf.Clamp(Pan.y,-limitY,limitY));
            boardRoot.anchoredPosition = Pan;
        }
        public bool TryCell(Vector2 screen, Camera camera, out Vector2Int cell) => BoardCoordinateMapper.TryMapScreen(boardRoot, viewport, screen, camera, Width, Height, out cell);
        public void OnPointerDown(PointerEventData e)
        {
            if (CanInteract != null && !CanInteract()) return;
            IsPanning = e.button == PointerEventData.InputButton.Middle || e.button == PointerEventData.InputButton.Left && UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.spaceKey.isPressed;
            if (IsPanning) { PanStarted?.Invoke(); return; }
            if (TryCell(e.position, e.pressEventCamera, out var cell))
                CellPressed?.Invoke(cell, e);
        }
        public void OnDrag(PointerEventData e)
        {
            if (CanInteract != null && !CanInteract()) { CellDragged?.Invoke(new Vector2Int(-1,-1),e); return; }
            if (IsPanning)
            {
                SetView(Zoom, Pan + e.delta / viewport.lossyScale.x);
                return;
            }
            if (TryCell(e.position, e.pressEventCamera, out var cell))
                CellDragged?.Invoke(cell, e);
            else CellDragged?.Invoke(new Vector2Int(-1,-1), e);
        }
        public void CancelPan() => IsPanning = false;
        public void OnPointerUp(PointerEventData e) { IsPanning=false; PointerReleased?.Invoke(e); }
        public void OnScroll(PointerEventData e) { if (CanInteract == null || CanInteract()) SetView(Zoom * Mathf.Pow(1.1f, e.scrollDelta.y), Pan); }
        private static void Clear(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var go = parent.GetChild(i).gameObject;
                go.SetActive(false);
                Destroy(go);
            }
        }
    }
}
