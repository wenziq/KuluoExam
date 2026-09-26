using System.Collections.Generic;
using System.Linq;
using Sokoban.Core.Data;
using Sokoban.Domain.Workshop;
using Sokoban.Runtime.Presentation.App;
using Sokoban.Runtime.Presentation.Board;
using Sokoban.Runtime.Presentation.Style;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace Sokoban.Runtime.Presentation.Workshop
{
    public sealed class LevelListView
    {
        private sealed class Row { public Button Button;public TextMeshProUGUI Title,Number,Status;public BoardThumbnailView Thumbnail; }
        private readonly Dictionary<string,Row> rows=new Dictionary<string,Row>();
        private readonly RectTransform content;
        private readonly ScrollRect scroll;
        private string visibleSelection; private int visibleIndex=-1;
        private readonly WorkshopApplicationCoordinator owner;
        private readonly TextMeshProUGUI count,applicationStatus;
        private string dragging;private int insertion;
        private RectTransform line;
        public LevelListView(RectTransform root,WorkshopApplicationCoordinator owner)
        {
            this.owner=owner;var theme=owner.App.theme;
            count=UiFactory.Text("LevelCount",root,"",theme,13);UiFactory.Place(count.rectTransform,16,12,170,30);
            var hint=UiFactory.Text("OrderHint",root,"这里的顺序，就是游玩的顺序",theme,11,theme.secondary);UiFactory.Place(hint.rectTransform,16,45,200,32);
            scroll=UiFactory.Scroll("Levels",root,theme,out content);UiFactory.Fill((RectTransform)scroll.transform,9,140,7,87);content.GetComponent<VerticalLayoutGroup>().spacing=6;
            var add=UiFactory.Button("AddLevel",root,"＋ 新建关卡",theme,()=>owner.Run(()=>LevelOperations.Add(owner.Document)));Bottom((RectTransform)add.transform,14,88,root.rect.width-28,36,true);
            string[] labels={"复制","↑","↓","删除"};string[] names={"DuplicateLevel","MoveLevelUp","MoveLevelDown","DeleteLevel"};
            for(int i=0;i<4;i++)
            {
                int action=i;var button=UiFactory.Button(names[i],root,labels[i],theme,()=>
                {
                    string id=owner.Document.SelectedLevelId;if(id==null)return;var pack=owner.Document.Snapshot();int index=pack.levelOrder.IndexOf(id);
                    if(action==0)owner.Run(()=>LevelOperations.Duplicate(owner.Document,id));
                    else if(action==3)owner.ConfirmDelete();
                    else {int next=index+(action==1?-1:1);if(next>=0&&next<pack.levelOrder.Count)owner.Run(()=>LevelOperations.Reorder(owner.Document,id,next));}
                });
                var rect=(RectTransform)button.transform;rect.anchorMin=new Vector2(.065f+i*.235f,0);rect.anchorMax=new Vector2(.065f+i*.235f+.2f,0);rect.offsetMin=new Vector2(0,43);rect.offsetMax=new Vector2(0,75);
            }
            applicationStatus=UiFactory.Text("LocalDraft",root,"草稿 · 尚未加入可玩关卡集",theme,11,theme.secondary);Bottom(applicationStatus.rectTransform,16,7,190,27);
        }
        static void Bottom(RectTransform r,float x,float y,float w,float h,bool stretch=false)
        { r.anchorMin=Vector2.zero;r.anchorMax=stretch?new Vector2(1,0):Vector2.zero;r.pivot=Vector2.zero;r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(stretch?-2*x:w,h); }
        public void Refresh(PackData pack,string selected)
        {
            count.text="关卡顺序   "+pack.levelOrder.Count+" / 30";
            applicationStatus.text=owner.GetComponent<Controls.ApplyPackModal>()?.ApplicationStatus??"草稿 · 尚未加入可玩关卡集";
            foreach(var id in rows.Keys.Where(id=>!pack.levelOrder.Contains(id)).ToArray()) { rows[id].Button.gameObject.SetActive(false);Object.Destroy(rows[id].Button.gameObject);rows.Remove(id); }
            for(int i=0;i<pack.levelOrder.Count;i++)
            {
                string id=pack.levelOrder[i];var level=pack.levels.Find(l=>l.levelId==id);
                if(!rows.TryGetValue(id,out var row))
                {
                    row=new Row();rows.Add(id,row);row.Button=UiFactory.Button("Level_"+id,content,"",owner.App.theme,()=>owner.Select(id));UiFactory.Preferred(row.Button.gameObject,72);
                    row.Number=UiFactory.Text("Number",row.Button.transform,"",owner.App.theme,10,owner.App.theme.secondary);UiFactory.Place(row.Number.rectTransform,22,5,26,20);
                    row.Thumbnail=new BoardThumbnailView(row.Button.transform);UiFactory.Place(row.Thumbnail.Rect,38,24,43,40);
                    row.Title=UiFactory.Text("LevelName",row.Button.transform,"",owner.App.theme,12);UiFactory.Fill(row.Title.rectTransform,87,29,8,8);
                    row.Status=UiFactory.Text("AnalysisBadge",row.Button.transform,"待分析",owner.App.theme,10,owner.App.theme.secondary);UiFactory.Fill(row.Status.rectTransform,87,5,8,48);
                    var handle=UiFactory.Panel("DragHandle",row.Button.transform,Color.clear,true);UiFactory.Place(handle.rectTransform,0,0,20,72);
                    var glyph=UiFactory.Text("HandleGlyph",handle.transform,"::",owner.App.theme,15,owner.App.theme.secondary);UiFactory.Fill(glyph.rectTransform);
                    var drag=handle.gameObject.AddComponent<LevelDragHandle>();drag.List=this;drag.Id=id;
                    owner.App.Tooltip(row.Button,level.name);
                }
                row.Button.transform.SetSiblingIndex(i);row.Number.text=(i+1).ToString("00");row.Title.text=level.name;row.Thumbnail.Show(level);
                row.Button.GetComponent<Image>().color=id==selected?UiTheme.Hex("35432b"):owner.App.theme.panel;
                row.Button.GetComponent<Controls.TooltipTarget>().explanation=level.name;
            }
            RevealSelection(selected,pack.levelOrder.IndexOf(selected));
        }
        public void RevealAfterResize()=>RevealSelection(visibleSelection,visibleIndex,true);
        private void RevealSelection(string selected,int index,bool force=false)
        {
            if(!force&&selected==visibleSelection&&index==visibleIndex)return;
            visibleSelection=selected;visibleIndex=index;
            if(selected==null||!rows.TryGetValue(selected,out var row))return;
            Canvas.ForceUpdateCanvases();
            var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport,row.Button.transform);
            float delta=bounds.min.y<scroll.viewport.rect.yMin?scroll.viewport.rect.yMin-bounds.min.y:
                bounds.max.y>scroll.viewport.rect.yMax?scroll.viewport.rect.yMax-bounds.max.y:0;
            scroll.StopMovement();content.anchoredPosition+=new Vector2(0,delta);
        }
        public void RefreshBadges(PackData pack)
        {
            var analysis=owner.GetComponent<Sokoban.Runtime.Presentation.Analysis.AnalysisMenuController>();if(analysis==null)return;
            foreach(var level in pack.levels)if(rows.TryGetValue(level.levelId,out var row))row.Status.text=analysis.SummaryFor(level);
        }
        public void BeginDrag(string id)
        {
            if(owner.App.Input.Context>InputContext.Page)return;
            owner.View.CommitFields();dragging=id;insertion=owner.Document.Snapshot().levelOrder.IndexOf(id);
            owner.App.Input.Capture(this,InputContext.MenuOrGesture);owner.App.Input.SetEscapeHandler(this,CancelDrag);
            line=UiFactory.Panel("InsertLine",content.parent,owner.App.theme.accent).rectTransform;line.sizeDelta=new Vector2(content.rect.width,3);
        }
        public void Drag(PointerEventData e)
        {
            if(dragging==null)return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(content,e.position,e.pressEventCamera,out var point);
            insertion=Mathf.Clamp(Mathf.FloorToInt(-point.y/78),0,rows.Count-1);
            line.position=content.TransformPoint(new Vector3(content.rect.center.x,-insertion*78,0));
        }
        public void EndDrag()
        { if(dragging==null)return;string id=dragging;int index=insertion;CancelDrag();owner.Run(()=>LevelOperations.Reorder(owner.Document,id,index)); }
        public void CancelDrag()
        {
            dragging=null;if(line!=null){line.gameObject.SetActive(false);Object.Destroy(line.gameObject);line=null;}
            owner.App.Input.RemoveEscapeHandler(this);owner.App.Input.Release(this);
        }
    }
    public sealed class LevelDragHandle:MonoBehaviour,IBeginDragHandler,IDragHandler,IEndDragHandler
    {
        [System.NonSerialized] public LevelListView List;public string Id;
        public void OnBeginDrag(PointerEventData e){if(e.button==PointerEventData.InputButton.Left)List.BeginDrag(Id);}
        public void OnDrag(PointerEventData e)=>List.Drag(e);
        public void OnEndDrag(PointerEventData e)=>List.EndDrag();
    }
}
