using TMPro;
using UnityEngine;
namespace Sokoban.Runtime.Presentation.Style
{
    [CreateAssetMenu(menuName = "Sokoban/UI Theme")]
    public sealed class UiTheme : ScriptableObject
    {
        public TMP_FontAsset font;
        public Color background = Hex("151716"), panel = Hex("1b1e1c"), raised = Hex("20251f"), border = Hex("30382c");
        public Color text = Hex("e8eadf"), secondary = Hex("969f8e"), accent = Hex("c3e997"), warning = Hex("e4bf7c"), error = Hex("ed9d8e");
        public Color floor = Hex("c9d1bc"), wall = Hex("46533a"), box = Hex("b9915e"), goal = Hex("83965e");
        public float tooltipDelay = .15f, menuBridgeDelay = .16f;
        public static Color Hex(string value)
        {
            ColorUtility.TryParseHtmlString("#" + value, out var color);
            return color;
        }
        public float LeftWidth(float width) => width >= 1600 ? 258 : width <= 1280 ? 213 : 232;
        public float RightWidth(float width) => width >= 1600 ? 318 : width <= 1280 ? 278 : 296;
    }
}
