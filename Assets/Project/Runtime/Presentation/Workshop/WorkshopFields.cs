using System;
using Sokoban.Runtime.Presentation.Style;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace Sokoban.Runtime.Presentation.Workshop
{
    public static class WorkshopFields
    {
        public static TMP_InputField Field(Transform parent,string name,string value,UiTheme theme,int limit,float height,bool multiline=false)
        {
            var panel=UiFactory.Panel(name,parent,theme.background,true);UiFactory.Round(panel);
            UiFactory.Preferred(panel.gameObject,height);
            var viewport=UiFactory.Rect("TextViewport",panel.transform);UiFactory.Fill(viewport,10,6,10,6);viewport.gameObject.AddComponent<RectMask2D>();
            var text=UiFactory.Text("Text",viewport,value,theme,14);UiFactory.Fill(text.rectTransform);text.overflowMode=TextOverflowModes.Overflow;
            if(multiline)text.verticalAlignment=VerticalAlignmentOptions.Top;
            var input=panel.gameObject.AddComponent<TMP_InputField>();input.targetGraphic=panel;input.textViewport=viewport;input.textComponent=text;
            input.fontAsset=theme.font;input.pointSize=14;input.richText=false;input.characterLimit=limit;
            input.lineType=multiline?TMP_InputField.LineType.MultiLineNewline:TMP_InputField.LineType.SingleLine;
            input.SetTextWithoutNotify(value);return input;
        }
        public static void Clear(Transform parent)
        { for(int i=parent.childCount-1;i>=0;i--){var go=parent.GetChild(i).gameObject;go.SetActive(false);UnityEngine.Object.Destroy(go);} }
    }
}
