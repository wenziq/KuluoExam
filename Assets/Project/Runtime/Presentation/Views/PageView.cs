using System;
using Sokoban.Runtime.Presentation.App;
using Sokoban.Runtime.Presentation.Style;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
namespace Sokoban.Runtime.Presentation.Views
{
    public abstract class PageView : MonoBehaviour
    {
        protected ApplicationController App;
        protected RectTransform Root;
        protected UiTheme Theme => App.theme;
        protected void Begin(ApplicationController app)
        {
            App = app;
            Root = (RectTransform)transform;
            UiFactory.Fill(Root);
        }
        protected TextMeshProUGUI Label(string name, string value, float x, float y, float w, float h, float size = 14, Color? color = null)
        {
            var label = UiFactory.Text(name, Root, value, Theme, size, color);
            UiFactory.Place(label.rectTransform, x, y, w, h);
            return label;
        }
        protected Button Action(string name, string title, float x, float y, float w, Action click, bool primary = false)
        {
            var button = UiFactory.Button(name, Root, title, Theme, click, primary);
            UiFactory.Place((RectTransform)button.transform, x, y, w, 42);
            return button;
        }
        protected RectTransform ScrollBody(float top, float bottom = 24, float cardHeight=0)
        {
            var scroll = UiFactory.Scroll("PageScroll", Root, Theme, out var content,cardHeight);
            UiFactory.Fill((RectTransform)scroll.transform, 40, bottom, 40, top);
            return content;
        }
        protected void PageHeading(string eyebrow, string title, string description)
        {
            Label("Eyebrow", eyebrow, 40, 24, 700, 24, 12, Theme.secondary);
            Label("Heading", title, 40, 52, 900, 48, 32);
            Label("Description", description, 40, 104, 900, 30, 14, Theme.secondary);
        }
    }
}
