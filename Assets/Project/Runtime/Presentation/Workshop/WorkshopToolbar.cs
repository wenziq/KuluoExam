using System;
using Sokoban.Runtime.Presentation.App;
using Sokoban.Runtime.Presentation.Controls;
using Sokoban.Runtime.Presentation.Style;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace Sokoban.Runtime.Presentation.Workshop
{
    public sealed class WorkshopToolbar
    {
        private TextMeshProUGUI title;private Button undo,redo;
        private MenuOption playablePackOption;
        public WorkshopToolbar(RectTransform root,WorkshopApplicationCoordinator owner)
        {
            var app=owner.App;var theme=app.theme;
            var back=UiFactory.Button("BackToLibrary",root,"← 工坊关卡集",theme,()=>app.Navigate("WorkshopLibrary"));UiFactory.Place((RectTransform)back.transform,12,10,125,34);
            title=UiFactory.Text("PackTitle",root,"",theme,14);UiFactory.Place(title.rectTransform,147,10,220,34);
            var row=UiFactory.Rect("Actions",root);row.anchorMin=row.anchorMax=new Vector2(1,1);row.pivot=Vector2.one;row.anchoredPosition=new Vector2(-12,-10);row.sizeDelta=new Vector2(630,34);
            float x=0;
            Button Add(string name,string label,float width,Action action,bool primary=false,IconKind? icon=null)
            {
                var b=UiFactory.Button(name,row,label,theme,action,primary);UiFactory.Place((RectTransform)b.transform,x,0,width,34);x+=width+5;
                if(icon.HasValue)
                {
                    var ir=UiFactory.Rect("Icon",b.transform);UiFactory.Place(ir,7,9,15,15);var graphic=ir.gameObject.AddComponent<UiIcon>();graphic.kind=icon.Value;graphic.color=theme.text;graphic.raycastTarget=false;
                    b.GetComponentInChildren<TextMeshProUGUI>().rectTransform.offsetMin=new Vector2(23,2);
                }
                return b;
            }
            Add("SaveDraft","保存",59,()=>owner.Operation("SaveDraft"),false,IconKind.Save);
            undo=Add("UndoEdit","",30,owner.Undo,false,IconKind.Undo);redo=Add("RedoEdit","",30,owner.Redo,false,IconKind.Redo);
            app.Tooltip(undo,"撤销上一次编辑");app.Tooltip(redo,"重做上一次编辑");
            var analysis=Add("Analysis","分析 v",70,null,false,IconKind.Analysis);
            string[] titles={"检查遗漏项","分析有解性","分析可玩性","分析整个关卡集","复检旧参考解"};
            string[] hints={"检查玩家、箱子、目标和地图结构","寻找通关路线，判断是否有解","观察参考解的推动、走位和事件","按关卡顺序检查整套内容","在当前地图重放已有路线"};
            string[] ids={"CheckStructure","Solve","Playability","AnalyzePack","RecheckWitness"};
            var options=new MenuOption[titles.Length];for(int i=0;i<titles.Length;i++){string id=ids[i];options[i]=new MenuOption(titles[i],hints[i],()=>owner.Operation(id));}app.Menu(analysis,options);
            var current=Add("TrialCurrent","试玩当前",84,()=>owner.StartTrial(false),false,IconKind.Play);app.Tooltip(current,"从当前关卡开始玩");
            var first=Add("TrialFirst","从头试玩",84,()=>owner.StartTrial(true),false,IconKind.PlayFromStart);app.Tooltip(first,"从头开始");
            var pack=Add("PackOperations","关卡集操作 v",125,null,false,IconKind.Folder);
            playablePackOption=new MenuOption("加入可玩关卡集","检查每一关有解后，加入关卡集供正式游玩",()=>owner.Operation("ApplyPack"));
            app.Menu(pack,playablePackOption,new MenuOption("导出可玩包","导出当前已验证的完整可玩内容",()=>owner.Operation("ExportPlayable")),new MenuOption("导出草稿包","保留未完成的地图和编辑信息",()=>owner.Operation("ExportDraft")),new MenuOption("导入关卡包","从本机选择关卡包文件",()=>owner.Operation("ImportPack")),new MenuOption("另存为新草稿","创建独立身份，不覆盖原草稿",()=>owner.Operation("SaveCopy")),new MenuOption("修改关卡集信息","修改名称、描述与解锁方式",()=>owner.Operation("PackInfo")));
            row.sizeDelta=new Vector2(x-5,34);
            Refresh(owner);
        }
        public void Refresh(WorkshopApplicationCoordinator owner)
        {
            if(owner.Document==null)return;
            var publish=owner.GetComponent<ApplyPackModal>();
            playablePackOption.Title=publish?.ActionLabel??"加入可玩关卡集";
            playablePackOption.Description=publish?.HasPlayableVersion==true?"检查每一关有解后，更新游戏中的整套关卡":"检查每一关有解后，加入关卡集供正式游玩";
            title.text=owner.Document.Snapshot().name+(owner.Document.IsDirty?" *":"")+"   · 草稿";
            undo.interactable=owner.Document.UndoCount>0;redo.interactable=owner.Document.RedoCount>0;
        }
    }
}
