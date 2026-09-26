using System;
using Sokoban.Runtime.Presentation.Board;
using Sokoban.Runtime.Presentation.Controls;
using Sokoban.Runtime.Presentation.Style;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
namespace Sokoban.Runtime.Presentation.App
{
    public sealed class ApplicationController : MonoBehaviour
    {
        public bool startGameFlow;
        public UiTheme theme;
        public RectTransform canvasRoot, topbar, pageRoot, menuLayer, modalLayer, tooltipLayer;
        public InputRouter Input
        {
            get; private set;
        }
        public ModalHost Modal
        {
            get; private set;
        }
        public TooltipView Tooltips
        {
            get; private set;
        }
        public string CurrentPage { get; private set; } = "MainMenu";
        public event Action<string> NavigationRequested;
        public event Func<string, bool> BeforeNavigation;
        public event Action<string> CommandRequested;
        private static ApplicationController instance;
        private bool initialized;
        public void Initialize()
        {
            if (initialized)
                return;
            if (instance != null && instance != this)
            {
                gameObject.SetActive(false);
                return;
            }
            instance = this;
            initialized = true;
            if (Application.isPlaying)
                UiBackgroundCamera.Ensure(transform, theme.background);
            Input = GetComponent<InputRouter>() ?? gameObject.AddComponent<InputRouter>();
            if (EventSystem.current == null)
            {
                var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                go.transform.SetParent(transform, false);
            }
            if (canvasRoot == null)
                BuildShell();
            canvasRoot.gameObject.SetActive(true);
            canvasRoot.GetComponent<Canvas>().enabled = true;
            Modal = modalLayer.GetComponent<ModalHost>() ?? modalLayer.gameObject.AddComponent<ModalHost>();
            Modal.theme = theme;
            Modal.router = Input;
            Tooltips = tooltipLayer.GetComponent<TooltipView>() ?? tooltipLayer.gameObject.AddComponent<TooltipView>();
            Tooltips.theme = theme;
            BuildNavigation();
        }
        private void Awake()
        {
            if (theme != null)
                Initialize();
        }
        private void Start()
        {
            if (startGameFlow && initialized)
            {
                var coordinator = GetComponent<GameApplicationCoordinator>() ?? gameObject.AddComponent<GameApplicationCoordinator>();
                string isolatedRoot = null;
#if SOKOBAN_VERIFICATION_BUILD
                isolatedRoot = Sokoban.Runtime.Development.U19PlayerProbe.DataRootOverride ?? Sokoban.Runtime.Development.U17PlayerProbe.DataRootOverride ?? Sokoban.Runtime.Development.U16PlayerProbe.DataRootOverride ?? Sokoban.Runtime.Development.U10PlayerProbe.DataRootOverride;
#endif
                coordinator.Initialize(this, isolatedRoot);
            }
        }
        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }
        public void BuildShell()
        {
            canvasRoot = UiFactory.Rect("AppShell", transform);
            var canvas = canvasRoot.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasRoot.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 1;
            canvasRoot.gameObject.AddComponent<GraphicRaycaster>();
            var bg = UiFactory.Panel("Background", canvasRoot, theme.background);
            UiFactory.Fill(bg.rectTransform);
            topbar = UiFactory.Panel("Topbar", canvasRoot, UiTheme.Hex("191c19")).rectTransform;
            pageRoot = UiFactory.Rect("PageRoot", canvasRoot);
            menuLayer = UiFactory.Rect("MenuLayer", canvasRoot);
            UiFactory.Fill(menuLayer);
            modalLayer = UiFactory.Rect("ModalLayer", canvasRoot);
            UiFactory.Fill(modalLayer);
            tooltipLayer = UiFactory.Rect("TooltipLayer", canvasRoot);
            UiFactory.Fill(tooltipLayer);
            Layout();
        }
        private void BuildNavigation()
        {
            if (topbar.childCount > 0)
                return;
            var brand = UiFactory.Text("Brand", topbar, "▣  推箱子", theme, 21);
            UiFactory.Place(brand.rectTransform, 20, 8, 145, 40);
            string[] labels = { "游玩", "关卡集", "关卡工坊" };
            string[] pages = { "MainMenu", "PackLibrary", "Workshop" };
            for (int i = 0; i < labels.Length; i++)
            {
                string page = pages[i];
                var button = UiFactory.Button(page, topbar, labels[i], theme, () => Navigate(page));
                button.GetComponent<Image>().color = Color.white;
                button.GetComponentInChildren<TMPro.TextMeshProUGUI>().fontSize = 14;
                UiFactory.Place((RectTransform)button.transform, 184 + i * 108, 10, 100, 36);
            }
            navigationUnderline=UiFactory.Panel("ActiveNavigation",topbar,theme.accent).rectTransform;UpdateNavigation();
            var help = UiFactory.Button("Help", topbar, "?", theme, () => Navigate("Help"));
            Right((RectTransform)help.transform, 64, 12, 32, 32);
            var settings = UiFactory.Button("Settings", topbar, "", theme, () => Navigate("Settings"));
            Right((RectTransform)settings.transform, 20, 12, 32, 32);
            var iconRect = UiFactory.Rect("SettingsIcon", settings.transform);
            UiFactory.Fill(iconRect, 8, 8, 8, 8);
            var settingsIcon = iconRect.gameObject.AddComponent<UiIcon>();
            settingsIcon.kind = IconKind.Settings;
            settingsIcon.color = theme.secondary;
            settingsIcon.raycastTarget = false;
        }
        private static void Right(RectTransform rect, float right, float top, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(1, 1);
            rect.anchoredPosition = new Vector2(-right, -top);
            rect.sizeDelta = new Vector2(width, height);
        }
        public void Navigate(string page)
        {
            if((page=="Settings"||page=="Help")&&GetComponent<SettingsController>() is SettingsController settings){settings.Show(page);return;}
            if (BeforeNavigation != null)
                foreach (Func<string, bool> guard in BeforeNavigation.GetInvocationList())
                    if (!guard(page)) return;
            Input.ClearPending();
            Tooltips.Hide();
            foreach (var menu in GetComponentsInChildren<HoverMenu>())
                menu.Close();
            Modal.Close();
            CurrentPage = page;
            UpdateNavigation();
            Input.SetContext(page == "Gameplay" ? InputContext.Gameplay : InputContext.Page);
            NavigationRequested?.Invoke(page);
        }
        public void Command(string command) => CommandRequested?.Invoke(command);
        public void ClearPage()
        {
            Input.ClearPending();
            for (int i = pageRoot.childCount - 1; i >= 0; i--)
            {
                var child = pageRoot.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
        }
        public Button Button(Transform parent, string title, Action action, bool primary = false) => UiFactory.Button(title, parent, title, theme, action, primary);
        public HoverMenu Menu(Button button, params MenuOption[] options)
        {
            var menu = button.gameObject.AddComponent<HoverMenu>();
            menu.theme = theme;
            menu.router = Input;
            menu.overlay = menuLayer;
            menu.Options.AddRange(options);
            button.onClick.AddListener(menu.Open);
            return menu;
        }
        public void Tooltip(Button button, string message)
        {
            var target = button.gameObject.AddComponent<TooltipTarget>();
            target.host = Tooltips;
            target.explanation = message;
        }
        private RectTransform navigationUnderline;
        private void UpdateNavigation()
        {
            string active=CurrentPage=="WorkshopLibrary"?"Workshop":CurrentPage=="LevelSelect"||CurrentPage=="Gameplay"?"MainMenu":CurrentPage;
            string[] pages={"MainMenu","PackLibrary","Workshop"};int index=Array.IndexOf(pages,active);
            for(int i=0;i<pages.Length;i++)
            {
                var button=topbar.Find(pages[i])?.GetComponent<UnityEngine.UI.Button>();
                if(button==null)continue;
                bool selected=i==index;
                var colors=button.colors;
                colors.normalColor=selected?theme.accent:UiTheme.Hex("303a2b");
                colors.highlightedColor=selected?UiTheme.Hex("d8f4b5"):UiTheme.Hex("4a5a3d");
                colors.selectedColor=colors.highlightedColor;
                colors.pressedColor=selected?UiTheme.Hex("a3c879"):UiTheme.Hex("25301f");
                colors.disabledColor=new Color(.3f,.34f,.28f,.5f);
                colors.fadeDuration=.1f;
                button.colors=colors;
                button.GetComponentInChildren<TMPro.TextMeshProUGUI>().color=selected?UiTheme.Hex("1b2b13"):theme.text;
                button.GetComponent<UnityEngine.UI.Outline>().effectColor=selected?theme.accent:UiTheme.Hex("5d6b50");
            }
            if(navigationUnderline!=null){navigationUnderline.gameObject.SetActive(index>=0);UiFactory.Place(navigationUnderline,194+Mathf.Max(0,index)*108,topbar.rect.height-5,80,3);}
        }
        private Vector2 lastSize;
        private void Update()
        {
            if (canvasRoot != null && lastSize != new Vector2(Screen.width, Screen.height))
                Layout();
        }
        private void Layout()
        {
            if (canvasRoot == null)
                return;
            lastSize = new Vector2(Screen.width, Screen.height);
            // Preserve readable reference pixels while adapting column widths and available space.
            var scaler = canvasRoot.GetComponent<CanvasScaler>();
            scaler.referenceResolution = new Vector2(Mathf.Max(960, Screen.width), Mathf.Max(560, Screen.height));
            float header = Screen.height <= 800 ? 56 : 65;
            UiFactory.Fill(topbar);
            topbar.anchorMin = new Vector2(0, 1);
            topbar.pivot = new Vector2(.5f, 1);
            topbar.sizeDelta = new Vector2(0, header);
            UiFactory.Fill(pageRoot, 0, 0, 0, header);
            UpdateNavigation();
        }
    }
}
