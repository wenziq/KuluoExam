using Sokoban.Runtime.Presentation.Style;
using UnityEngine;
using UnityEngine.EventSystems;
namespace Sokoban.Runtime.Presentation.Controls
{
    public sealed class TooltipView : MonoBehaviour
    {
        public UiTheme theme;
        private RectTransform bubble; private RectTransform anchor; private string message; private float due;
        public void Request(RectTransform source, string text)
        {
            Hide();
            anchor = source;
            message = text;
            due = Time.unscaledTime + theme.tooltipDelay;
        }
        public void Hide()
        {
            if (bubble != null)
            {
                bubble.gameObject.SetActive(false);
                Destroy(bubble.gameObject);
            }
            bubble = null;
            anchor = null;
        }
        private void Update()
        {
            if (anchor == null || bubble != null || Time.unscaledTime < due)
                return;
            bubble = UiFactory.Panel("Tooltip", transform, theme.raised).rectTransform;
            bubble.sizeDelta = new Vector2(Mathf.Min(320, Mathf.Max(100, message.Length * 13 + 24)), 36);
            var label = UiFactory.Text("Explanation", bubble, message, theme, 12);
            UiFactory.Fill(label.rectTransform, 10, 3, 10, 3);
            OverlayPlacement.Below(bubble, anchor, 4);
        }
    }
    public sealed class TooltipTarget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public TooltipView host; public string explanation;
        public void OnPointerEnter(PointerEventData e) => host.Request((RectTransform)transform, explanation);
        public void OnPointerExit(PointerEventData e) => host.Hide();
        private void OnDisable()
        {
            if (host != null)
                host.Hide();
        }
    }
    public static class OverlayPlacement
    {
        public static void Below(RectTransform popup, RectTransform anchor, float gap)
        {
            var root = (RectTransform)popup.parent;
            var corners = new Vector3[4];
            anchor.GetWorldCorners(corners);
            var local = (Vector2)root.InverseTransformPoint(corners[0]);
            popup.anchorMin = popup.anchorMax = new Vector2(.5f, .5f);
            popup.pivot = new Vector2(0, 1);
            local.x = Mathf.Clamp(local.x, root.rect.xMin + 8, Mathf.Max(root.rect.xMin + 8, root.rect.xMax - popup.rect.width - 8));
            local.y = Mathf.Clamp(local.y - gap, root.rect.yMin + popup.rect.height + 8, root.rect.yMax - 8);
            popup.anchoredPosition = local;
        }
    }
}
