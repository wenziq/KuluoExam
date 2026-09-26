using System;
using System.Collections.Generic;
using Sokoban.Runtime.Presentation.App;
using Sokoban.Runtime.Presentation.Style;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace Sokoban.Runtime.Presentation.Controls
{
    public sealed class MenuOption
    {
        public string Title, Description; public Action Action;
        public MenuOption(string title, string description, Action action)
        {
            Title = title;
            Description = description;
            Action = action;
        }
    }
    public sealed class HoverMenu : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public UiTheme theme; public InputRouter router; public RectTransform overlay;
        public readonly List<MenuOption> Options = new List<MenuOption>();
        private RectTransform popup; private float closeAt = float.PositiveInfinity;
        public bool IsOpen => popup != null;
        public void Open()
        {
            Bind();
            if (IsOpen)
                return;
            foreach (var other in overlay.GetComponentInParent<ApplicationController>().GetComponentsInChildren<HoverMenu>())
                if (other != this)
                    other.Close();
            router.Capture(this, InputContext.MenuOrGesture);
            popup = UiFactory.Panel("MenuPopup", overlay, theme.raised, true).rectTransform;
            UiFactory.Round(popup.GetComponent<UnityEngine.UI.Image>());
            popup.sizeDelta = new Vector2(302, Mathf.Min(Options.Count * 62 + 24, overlay.rect.height - 30));
            var bridge = popup.gameObject.AddComponent<MenuHoverBridge>();
            bridge.owner = this;
            var scroll = UiFactory.Scroll("Options", popup, theme, out var content);
            UiFactory.Fill((RectTransform)scroll.transform, 12, 12, 12, 12);
            content.GetComponent<VerticalLayoutGroup>().spacing = 2;
            foreach (var item in Options)
            {
                var captured = item;
                var button = UiFactory.Button("Option", content, "", theme, () => { Close(); captured.Action?.Invoke(); });
                UiFactory.Preferred(button.gameObject, 60);
                button.GetComponent<Image>().color = theme.raised;
                var title = UiFactory.Text("Title", button.transform, item.Title, theme, 14);
                UiFactory.Place(title.rectTransform, 16, 4, 242, 25);
                var hint = UiFactory.Text("Description", button.transform, item.Description, theme, 11, theme.secondary);
                UiFactory.Place(hint.rectTransform, 16, 29, 242, 23);
            }
            OverlayPlacement.Below(popup, (RectTransform)transform, 8);
            Stay();
        }
        public void Close()
        {
            if (popup != null)
            {
                popup.gameObject.SetActive(false);
                Destroy(popup.gameObject);
                popup = null;
            }
            router.Release(this);
            closeAt = float.PositiveInfinity;
        }
        public void Stay() => closeAt = float.PositiveInfinity;
        public void Leave() => closeAt = Time.unscaledTime + theme.menuBridgeDelay;
        public void OnPointerEnter(PointerEventData e)
        {
            Open();
            Stay();
        }
        public void OnPointerExit(PointerEventData e) => Leave();
        private void Update()
        {
            if (IsOpen && Time.unscaledTime >= closeAt)
                Close();
        }
        private bool subscribed;
        private void Bind()
        {
            if (!subscribed && router != null)
            {
                router.SetEscapeHandler(this, Close);
                subscribed = true;
            }
        }
        private void Start()
        {
            Bind();
        }
        private void OnEnable()
        {
            Bind();
        }
        private void OnDisable()
        {
            if (router != null)
            {
                router.RemoveEscapeHandler(this);
                subscribed = false;
                Close();
            }
        }
    }
    public sealed class MenuHoverBridge : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public HoverMenu owner; public void OnPointerEnter(PointerEventData e) => owner.Stay(); public void OnPointerExit(PointerEventData e) => owner.Leave();
    }
}
