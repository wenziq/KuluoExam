using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace Sokoban.Runtime.Presentation.Style
{
    public static class UiFactory
    {
        private static Sprite roundedSprite;
        public static void Round(Image image)
        {
            if (roundedSprite == null)
            {
                const int size = 24;
                const float radius = 6;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "UI rounded corners", filterMode = FilterMode.Bilinear };
                var pixels = new Color[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float dx = Mathf.Max(radius - x - .5f, x + .5f - (size - radius), 0);
                        float dy = Mathf.Max(radius - y - .5f, y + .5f - (size - radius), 0);
                        pixels[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01(radius + .5f - Mathf.Sqrt(dx * dx + dy * dy)));
                    }
                texture.SetPixels(pixels);
                texture.Apply(false, true);
                roundedSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
            }
            image.sprite = roundedSprite;
            image.type = Image.Type.Sliced;
        }
        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }
        public static void Fill(RectTransform rect, float left = 0, float bottom = 0, float right = 0, float top = 0)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }
        public static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }
        public static Image Panel(string name, Transform parent, Color color, bool hit = false)
        {
            var rect = Rect(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = hit;
            return image;
        }
        public static TextMeshProUGUI Text(string name, Transform parent, string value, UiTheme theme, float size = 13, Color? color = null)
        {
            var rect = Rect(name, parent);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = theme.font;
            text.text = value;
            text.fontSize = size;
            text.color = color ?? theme.text;
            text.richText = false;
            text.raycastTarget = false;
            text.enableAutoSizing = false;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.verticalAlignment = VerticalAlignmentOptions.Middle;
            return text;
        }
        public static Button Button(string name, Transform parent, string title, UiTheme theme, Action clicked = null, bool primary = false)
        {
            var image = Panel(name, parent, primary ? theme.accent : UiTheme.Hex("252c22"), true);
            Round(image);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f);
            colors.pressedColor = new Color(.82f, .9f, .76f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(1, 1, 1, .36f);
            button.colors = colors;
            var outline = image.gameObject.AddComponent<Outline>();
            outline.effectColor = theme.border;
            outline.effectDistance = new Vector2(1, -1);
            var label = Text("Label", image.transform, title, theme, 13, primary ? UiTheme.Hex("1b2b13") : theme.text);
            Fill(label.rectTransform, 8, 2, 8, 2);
            label.alignment = TextAlignmentOptions.Center;
            if (clicked != null)
                button.onClick.AddListener(() => clicked());
            return button;
        }
        public static RectTransform Vertical(Transform parent, string name, float spacing = 8)
        {
            var rect = Rect(name, parent);
            var layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            return rect;
        }
        public static void Preferred(GameObject go, float height, float width = -1)
        {
            var layout = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            layout.preferredHeight = height;
            layout.minHeight = height;
            if (width >= 0)
                layout.preferredWidth = width;
        }
        public static ScrollRect Scroll(string name, Transform parent, UiTheme theme, out RectTransform content, float cardHeight=0)
        {
            var root = Rect(name, parent);
            var scroll = root.gameObject.AddComponent<ScrollRect>();
            var viewport = Panel("Viewport", root, Color.clear, true).rectTransform;
            Fill(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            content = cardHeight>0?Rect("Content",viewport):Vertical(viewport, "Content");
            if(cardHeight>0)content.gameObject.AddComponent<ResponsiveCardGrid>().cardHeight=cardHeight;
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(.5f, 1);
            content.sizeDelta = Vector2.zero;
            var fit = content.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 25;
            var track = Panel("Scrollbar", root, theme.background, true).rectTransform;
            track.anchorMin = new Vector2(1, 0);
            track.anchorMax = Vector2.one;
            track.pivot = new Vector2(1, .5f);
            track.sizeDelta = new Vector2(6, 0);
            var thumb = Panel("Handle", track, theme.border, true);
            Fill(thumb.rectTransform);
            var scrollbar = track.gameObject.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.handleRect = thumb.rectTransform;
            scrollbar.targetGraphic = thumb;
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            viewport.offsetMax = new Vector2(-10, 0);

            return scroll;
        }
    }
}
