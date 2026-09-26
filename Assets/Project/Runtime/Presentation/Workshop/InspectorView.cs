using System;
using Sokoban.Core.Data;
using Sokoban.Domain.Workshop;
using Sokoban.Runtime.Presentation.Style;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace Sokoban.Runtime.Presentation.Workshop
{
    public sealed class InspectorView
    {
        readonly WorkshopApplicationCoordinator owner;readonly TMP_InputField name,notes,width,height;readonly TextMeshProUGUI selection;
        readonly Button difficulty;IntendedDifficulty currentDifficulty;string id;bool refreshing;
        public InspectorView(RectTransform content,WorkshopApplicationCoordinator owner)
        {
            this.owner=owner;var theme=owner.App.theme;
            void Label(string title){var text=UiFactory.Text(title,content,title,theme,12,theme.secondary);UiFactory.Preferred(text.gameObject,25);}
            Label("关卡名称");name=WorkshopFields.Field(content,"LevelNameInput","",theme,80,38);
            Label("地图尺寸 · 左下角固定");
            var row=UiFactory.Rect("SizeFields",content);UiFactory.Preferred(row.gameObject,38);var layout=row.gameObject.AddComponent<HorizontalLayoutGroup>();layout.spacing=8;layout.childControlWidth=true;layout.childForceExpandWidth=true;
            width=WorkshopFields.Field(row,"WidthInput","10",theme,2,38);width.contentType=TMP_InputField.ContentType.IntegerNumber;
            height=WorkshopFields.Field(row,"HeightInput","10",theme,2,38);height.contentType=TMP_InputField.ContentType.IntegerNumber;
            var resize=UiFactory.Button("ResizeLevel",content,"预览并修改尺寸",theme,()=>
            {
                if(int.TryParse(width.text,out int w)&&int.TryParse(height.text,out int h))owner.Resize(w,h);
                else owner.App.Modal.Show("尺寸未修改","宽高需要填写 4～20 的整数。");
            });UiFactory.Preferred(resize.gameObject,34);
            Label("设计备注");notes=WorkshopFields.Field(content,"DesignNotes","",theme,2000,110,true);
            Label("目标难度 · 策划填写");
            difficulty=UiFactory.Button("IntendedDifficulty",content,"未填写",theme,()=>{currentDifficulty=(IntendedDifficulty)(((int)currentDifficulty+1)%5);Commit();});UiFactory.Preferred(difficulty.gameObject,36);
            selection=UiFactory.Text("SelectedObject",content,"未选择对象",theme,13);UiFactory.Preferred(selection.gameObject,70);
            var layers=UiFactory.Rect("SelectionLayers",content);UiFactory.Preferred(layers.gameObject,30);var layerLayout=layers.gameObject.AddComponent<HorizontalLayoutGroup>();layerLayout.spacing=6;layerLayout.childControlWidth=true;layerLayout.childForceExpandWidth=true;
            string[] layerNames={"实体","目标","墙"};SelectionLayer[] layerValues={SelectionLayer.Entity,SelectionLayer.Feature,SelectionLayer.Terrain};
            for(int i=0;i<3;i++){var layer=layerValues[i];UiFactory.Button("SelectLayer"+i,layers,layerNames[i],theme,()=>owner.View.SelectLayer(layer));}
            var delete=UiFactory.Button("DeleteSelection",content,"删除选中对象",theme,()=>owner.View.DeleteSelection());UiFactory.Preferred(delete.gameObject,34);
            name.onEndEdit.AddListener(_=>Commit());notes.onEndEdit.AddListener(_=>Commit());
        }
        public void Refresh(LevelData level,LevelViewportState state)
        {
            refreshing=true;id=level.levelId;currentDifficulty=level.intendedDifficulty;
            if(!name.isFocused)name.SetTextWithoutNotify(level.name);if(!notes.isFocused)notes.SetTextWithoutNotify(level.designNotes);
            if(!width.isFocused)width.SetTextWithoutNotify(level.width.ToString());if(!height.isFocused)height.SetTextWithoutNotify(level.height.ToString());
            string[] labels={"未填写","入门","简单","中等","困难"};difficulty.GetComponentInChildren<TextMeshProUGUI>().text=labels[(int)currentDifficulty];
            selection.text=state.SelectionLayer==SelectionLayer.None?"使用选择工具点选箱子、目标或墙。":(state.SelectionLayer==SelectionLayer.Terrain?"已选择墙":state.SelectionLayer==SelectionLayer.Feature?"已选择目标":"已选择实体")+"\n坐标 ("+state.SelectedCellX+", "+state.SelectedCellY+")";
            refreshing=false;
        }
        public void Commit()
        {
            if(refreshing||id==null)return;
            string target=id,value=name.text,remark=notes.text;var intent=currentDifficulty;
            owner.Run(()=>LevelOperations.SetLevelMetadata(owner.Document,target,value,remark,intent));
        }
    }
}
