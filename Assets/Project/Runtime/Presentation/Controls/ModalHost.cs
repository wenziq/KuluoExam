using System;
using System.Collections.Generic;
using Sokoban.Runtime.Presentation.App;
using Sokoban.Runtime.Presentation.Style;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace Sokoban.Runtime.Presentation.Controls
{
    public sealed class ModalHost : MonoBehaviour
    {
        public UiTheme theme; public InputRouter router;
        public bool IsOpen => panel != null;
        private RectTransform panel;
        private Action onClose;
        private GameObject previousSelection;
        private readonly Dictionary<CanvasGroup, bool> blockedGroups = new Dictionary<CanvasGroup, bool>();
        // Keep neutral groups owned by this host between openings. Destroying a component
        // is deferred, which would otherwise break Close followed by Show in one frame.
        private readonly List<CanvasGroup> ownedGroups = new List<CanvasGroup>();
        public TextMeshProUGUI Body
        {
            get; private set;
        }
        public RectTransform Actions
        {
            get; private set;
        }
        public void Show(string title, string message, Action closed = null)
        {
            Bind();
            Close();
            onClose = closed;
            router.Capture(this, InputContext.Modal);
            previousSelection = UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject;
            BlockUnderlyingSelection();
            panel = UiFactory.Panel("ModalBackdrop", transform, new Color(0, 0, 0, .65f), true).rectTransform;
            UiFactory.Fill(panel);
            var window = UiFactory.Panel("Dialog", panel, theme.panel, true).rectTransform;
            UiFactory.Round(window.GetComponent<UnityEngine.UI.Image>());
            window.anchorMin = window.anchorMax = new Vector2(.5f, .5f);
            window.sizeDelta = new Vector2(Mathf.Min(640, ((RectTransform)transform).rect.width - 40), Mathf.Min(540, ((RectTransform)transform).rect.height - 40));
            var heading = UiFactory.Text("Title", window, title, theme, 20);
            UiFactory.Fill(heading.rectTransform, 24, 0, 70, 0);
            heading.rectTransform.anchorMin = new Vector2(0, 1);
            heading.rectTransform.sizeDelta = new Vector2(-94, 64);
            heading.rectTransform.pivot = new Vector2(.5f, 1);
            var close = UiFactory.Button("Close", window, "×", theme, Close);
            var cr = (RectTransform)close.transform;
            cr.anchorMin = cr.anchorMax = new Vector2(1, 1);
            cr.pivot = Vector2.one;
            cr.anchoredPosition = new Vector2(-16, -16);
            cr.sizeDelta = new Vector2(32, 32);
            var scroll = UiFactory.Scroll("BodyScroll", window, theme, out var content);
            UiFactory.Fill((RectTransform)scroll.transform, 24, 82, 24, 76);
            Body = UiFactory.Text("Body", content, message, theme, 14);
            Body.overflowMode = TextOverflowModes.Overflow;
            UiFactory.Preferred(Body.gameObject, Mathf.Max(140, Body.GetPreferredValues(message, 550, 0).y + 12));
            Actions = UiFactory.Rect("Actions", window);
            Actions.anchorMin = Vector2.zero;
            Actions.anchorMax = new Vector2(1, 0);
            Actions.pivot = Vector2.zero;
            Actions.offsetMin = new Vector2(24, 20);
            Actions.offsetMax = new Vector2(-24, 60);
            var row = Actions.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 8;
            row.childForceExpandWidth = false;
            row.childControlWidth = true;
            row.childControlHeight = true;
            var done = UiFactory.Button("Done", Actions, "关闭", theme, Close);
            UiFactory.Preferred(done.gameObject, 36, 100);
            close.Select();
        }
        public Button AddAction(string title, Action action, bool primary = true)
        {
            var button = UiFactory.Button(title, Actions, title, theme, action, primary);
            UiFactory.Preferred(button.gameObject, 36, Mathf.Max(110, title.Length * 15 + 28));
            return button;
        }
        public void SetMessage(string message)
        {
            if (Body == null)
                return;
            Body.text = message;
            UiFactory.Preferred(Body.gameObject, Mathf.Max(140, Body.GetPreferredValues(message, 550, 0).y + 12));
        }
        public void Close()
        {
            CloseCurrent(invokeCallback: true);
        }
        private void CloseCurrent(bool invokeCallback)
        {
            if (panel != null)
            {
                panel.gameObject.SetActive(false);
                Destroy(panel.gameObject);
                panel = null;
            }
            RestoreUnderlyingSelection();
            if (router != null)
                router.Release(this);
            var selection = previousSelection;
            previousSelection = null;
            var callback = onClose;
            onClose = null;
            // Restore before the callback: a callback may open the next modal and acquire
            // its own selection/gates. Never overwrite that new modal's ownership.
            if (selection != null && selection.activeInHierarchy)
                UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(selection);
            if (invokeCallback)
                callback?.Invoke();
        }
        private void BlockUnderlyingSelection()
        {
            var app = GetComponentInParent<ApplicationController>();
            if (app == null)
                return;
            Block(app.pageRoot);
            Block(app.topbar);
            Block(app.menuLayer);
        }
        private void Block(RectTransform root)
        {
            if (root == null)
                return;
            var group = root.GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = root.gameObject.AddComponent<CanvasGroup>();
                ownedGroups.Add(group);
            }
            // Nested groups may ignore ancestors. Gate each existing group so explicit
            // navigation cannot bypass the root gate through ignoreParentGroups.
            foreach (var current in root.GetComponentsInChildren<CanvasGroup>(true))
            {
                if (!blockedGroups.ContainsKey(current))
                    blockedGroups.Add(current, current.interactable);
                current.interactable = false;
            }
        }
        private void RestoreUnderlyingSelection()
        {
            foreach (var pair in blockedGroups)
                if (pair.Key != null)
                    pair.Key.interactable = pair.Value;
            blockedGroups.Clear();
        }
        private void OnDestroy()
        {
            RestoreUnderlyingSelection();
            foreach (var group in ownedGroups)
                if (group != null)
                    Destroy(group);
            ownedGroups.Clear();
        }
        private bool subscribed;
        private void Start()
        {
            Bind();
        }
        private void Bind()
        {
            if (!subscribed && router != null)
            {
                router.SetEscapeHandler(this, Escape);
                subscribed = true;
            }
        }
        private void OnEnable()
        {
            Bind();
        }
        private void OnDisable()
        {
            CloseCurrent(invokeCallback: false);
            if (router != null)
            {
                router.RemoveEscapeHandler(this);
                subscribed = false;
                router.Release(this);
            }
        }
        private void Escape()
        {
            if (IsOpen)
                Close();
        }
    }
}
