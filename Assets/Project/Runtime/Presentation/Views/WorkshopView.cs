using System;
using Sokoban.Core.Data;
using Sokoban.Core.Validation;
using Sokoban.Domain.Workshop;
using Sokoban.Runtime.Presentation.App;
using Sokoban.Runtime.Presentation.Board;
using Sokoban.Runtime.Presentation.Style;
using Sokoban.Runtime.Presentation.Workshop;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
namespace Sokoban.Runtime.Presentation.Views
{
    public sealed class WorkshopView : MonoBehaviour
    {
        public WorkshopApplicationCoordinator Owner { get; private set; }
        public BoardView Board { get; private set; }
        public LevelViewportState State { get; private set; } = new LevelViewportState();
        public Sokoban.Runtime.Presentation.Analysis.AnalysisPanelView AnalysisPanel { get; private set; }
        public PaintPaletteView Palette { get; private set; }
        public BoardInputController BoardInput { get; private set; }
        private RectTransform root,left,right,center,paletteRoot,infoRoot,issuesRoot,analysisRoot;
        private WorkshopToolbar toolbar;private LevelListView list;private InspectorView inspector;private IssueListView issues;private WorkshopStatusBar status;
        private TextMeshProUGUI current;private string visibleId,hover="";private bool collapsed,composing;private Vector2 lastSize;
        private Keyboard keyboard;
        public void Build(WorkshopApplicationCoordinator owner)
        {
            Owner=owner;collapsed=owner.InspectorCollapsed;root=(RectTransform)transform;UiFactory.Fill(root);var theme=owner.App.theme;
            if(owner.Document==null)
            {
                var title=UiFactory.Text("Welcome",root,"关卡工坊",theme,36);UiFactory.Place(title.rectTransform,70,75,750,65);
                var subtitle=UiFactory.Text("StartHint",root,"从一个想法开始。画地图、试玩、分析，再把作品带入游戏。",theme,17,theme.secondary);UiFactory.Place(subtitle.rectTransform,70,157,940,65);
                var create=UiFactory.Button("CreateDraft",root,"＋ 新建关卡集",theme,owner.NewPack,true);UiFactory.Place((RectTransform)create.transform,70,255,210,48);
                var copy=UiFactory.Button("CopyExample",root,"从示例复制",theme,owner.CopyExample);UiFactory.Place((RectTransform)copy.transform,300,255,210,48);return;
            }
            var top=UiFactory.Panel("WorkshopToolbar",root,theme.panel,true).rectTransform;UiFactory.Fill(top);top.anchorMin=new Vector2(0,1);top.pivot=new Vector2(.5f,1);top.sizeDelta=new Vector2(0,54);toolbar=new WorkshopToolbar(top,owner);
            var bottom=UiFactory.Panel("WorkshopStatus",root,UiTheme.Hex("232c1e")).rectTransform;UiFactory.Fill(bottom);bottom.anchorMax=new Vector2(1,0);bottom.pivot=new Vector2(.5f,0);bottom.sizeDelta=new Vector2(0,28);status=new WorkshopStatusBar(bottom,theme);
            left=UiFactory.Panel("LeftColumn",root,theme.panel,true).rectTransform;
            right=UiFactory.Panel("RightColumn",root,theme.panel,true).rectTransform;
            center=UiFactory.Panel("MapArea",root,theme.background).rectTransform;
            Layout();list=new LevelListView(left,owner);
            current=UiFactory.Text("CurrentLevel",center,"",theme,14);UiFactory.Place(current.rectTransform,20,8,470,36);
            var zoomOut=UiFactory.Button("ZoomOut",center,"−",theme,()=>{Board.SetView(Board.Zoom/1.1f,Board.Pan);RememberViewport();});RightTop((RectTransform)zoomOut.transform,133,9,30,30);
            var zoomIn=UiFactory.Button("ZoomIn",center,"+",theme,()=>{Board.SetView(Board.Zoom*1.1f,Board.Pan);RememberViewport();});RightTop((RectTransform)zoomIn.transform,98,9,30,30);
            var fit=UiFactory.Button("FitMap",center,"适应",theme,()=>{Board.Fit();RememberViewport();});RightTop((RectTransform)fit.transform,48,9,45,30);
            var toggle=UiFactory.Button("ToggleInspector",center,"i",theme,()=>{collapsed=!collapsed;Owner.InspectorCollapsed=collapsed;Layout();});RightTop((RectTransform)toggle.transform,10,9,32,30);
            Board=BoardView.Create(center);UiFactory.Fill(Board.viewport,0,85,0,52);Board.fitPadding=new Vector2(60,60);Board.maxCellSize=68;
            BoardInput=Board.gameObject.AddComponent<BoardInputController>();BoardInput.Initialize(this);
            paletteRoot=UiFactory.Panel("PaintPalette",center,UiTheme.Hex("242a21"),true).rectTransform;paletteRoot.anchorMin=paletteRoot.anchorMax=new Vector2(.5f,0);paletteRoot.pivot=new Vector2(.5f,0);paletteRoot.anchoredPosition=new Vector2(0,13);paletteRoot.sizeDelta=new Vector2(348,63);UiFactory.Round(paletteRoot.GetComponent<Image>());Palette=new PaintPaletteView(paletteRoot,this);
            string[] tabs={"信息","分析","问题"};for(int i=0;i<3;i++){int tab=i;var button=UiFactory.Button("InspectorTab"+i,right,tabs[i],theme,()=>SetTab(tab));UiFactory.Place((RectTransform)button.transform,14+i*75,11,66,32);}
            var infoScroll=UiFactory.Scroll("Information",right,theme,out infoRoot);UiFactory.Fill((RectTransform)infoScroll.transform,16,15,12,62);inspector=new InspectorView(infoRoot,owner);
            var issueScroll=UiFactory.Scroll("Issues",right,theme,out issuesRoot);UiFactory.Fill((RectTransform)issueScroll.transform,16,15,12,62);issues=new IssueListView(issuesRoot,owner);
            var analysisScroll=UiFactory.Scroll("AnalysisDetails",right,theme,out analysisRoot);UiFactory.Fill((RectTransform)analysisScroll.transform,16,15,12,62);
            AnalysisPanel=new Sokoban.Runtime.Presentation.Analysis.AnalysisPanelView(analysisRoot,owner.GetComponent<Sokoban.Runtime.Presentation.Analysis.AnalysisMenuController>());
            SetTab(owner.InspectorTab);keyboard=Keyboard.current;if(keyboard!=null)keyboard.onIMECompositionChange+=CompositionChanged;
            Refresh();
        }
        static void RightTop(RectTransform r,float right,float top,float w,float h){r.anchorMin=r.anchorMax=Vector2.one;r.pivot=Vector2.one;r.anchoredPosition=new Vector2(-right,-top);r.sizeDelta=new Vector2(w,h);}
        private void CompositionChanged(IMECompositionString text){composing=text.Count>0;}
        private void Layout()
        {
            if(root==null||left==null)return;lastSize=root.rect.size;
            float leftWidth=root.rect.width>=1600?258:213;float rightWidth=collapsed?0:root.rect.width>=1600?318:278;
            UiFactory.Fill(left,0,28,0,54);left.anchorMax=new Vector2(0,1);left.offsetMax=new Vector2(leftWidth,-54);
            UiFactory.Fill(right,0,28,0,54);right.anchorMin=new Vector2(1,0);right.offsetMin=new Vector2(-rightWidth,28);right.gameObject.SetActive(!collapsed);
            UiFactory.Fill(center,leftWidth+1,28,rightWidth+1,54);
            list?.RevealAfterResize();
        }
        public void SetTab(int index)
        { Owner.InspectorTab=index;infoRoot.parent.parent.gameObject.SetActive(index==0);analysisRoot.parent.parent.gameObject.SetActive(index==1);issuesRoot.parent.parent.gameObject.SetActive(index==2); }
        public void Refresh()
        {
            if(Owner?.Document==null||toolbar==null)return;
            var doc=Owner.Document;var pack=doc.Snapshot();var level=pack.levels.Find(l=>l.levelId==doc.SelectedLevelId);
            if(visibleId!=doc.SelectedLevelId)
            {
                RememberViewport();visibleId=doc.SelectedLevelId;State=visibleId==null?new LevelViewportState():Owner.Viewports.Get(visibleId);
                if(level!=null){Board.Show(level);Board.SetView(State.Zoom,new Vector2(State.PanX,State.PanY));}
            }
            else if(level!=null)Board.Show(level,false);
            Board.gameObject.SetActive(level!=null);paletteRoot.gameObject.SetActive(level!=null);
            toolbar.Refresh(Owner);list.Refresh(pack,doc.SelectedLevelId);Palette.Refresh();
            current.text=level==null?"新建关卡，开始制作第一张地图":(pack.levelOrder.IndexOf(level.levelId)+1).ToString("00")+"  "+level.name+"   "+level.width+" × "+level.height;
            if(level!=null){inspector.Refresh(level,State);issues.Refresh(StructureValidator.Validate(level));}
            infoRoot.gameObject.SetActive(level!=null);issuesRoot.gameObject.SetActive(level!=null);
            RefreshStatus();RefreshSelection();Owner.GetComponent<Sokoban.Runtime.Presentation.Analysis.AnalysisMenuController>()?.RefreshSelection();
        }
        public void RefreshAnalysisBadges(){if(Owner?.Document!=null)list?.RefreshBadges(Owner.Document.Snapshot());}
        public void RefreshBoard()
        {var level=CurrentLevel();if(level!=null)Board.Show(level,false);RefreshStatus();}
        private LevelData CurrentLevel(){var pack=Owner.Document.Snapshot();return pack.levels.Find(l=>l.levelId==Owner.Document.SelectedLevelId);}
        public void SetHover(string text){if(hover==text)return;hover=text;RefreshStatus();}
        public void RefreshStatus(){if(status!=null)status.Refresh(Owner.Document,CurrentLevel(),State,hover,Owner.PersistenceStatus,Owner.GetComponent<Sokoban.Runtime.Presentation.Controls.ApplyPackModal>()?.ApplicationStatus);}
        public void RememberViewport()
        {
            if(visibleId==null||Board==null)return;State.Zoom=Board.Zoom;State.PanX=Board.Pan.x;State.PanY=Board.Pan.y;Owner.Viewports.Set(visibleId,State);
        }
        public void CommitGesture()=>BoardInput?.Commit();
        public void CommitFields()
        {
            if(composing)return;
            var selected=EventSystem.current?.currentSelectedGameObject;
            if(selected!=null&&selected.GetComponentInParent<TMP_InputField>()!=null)
            {
                // TMP may defer activation/end-edit until the next frame. Commit the fields before dispatching Save.
                inspector?.Commit();
                EventSystem.current.SetSelectedGameObject(null);
            }
        }
        public void SelectCell(int x,int y)
        {
            var level=CurrentLevel();if(level==null||!new Coordinate(x,y).IsInside(level.width,level.height))return;
            State.SelectedCellX=x;State.SelectedCellY=y;
            var entity=level.entities.Find(e=>e.x==x&&e.y==y);var feature=level.features.Find(e=>e.x==x&&e.y==y);
            State.SelectionLayer=entity!=null?SelectionLayer.Entity:feature!=null?SelectionLayer.Feature:level.terrain[y*level.width+x]==1?SelectionLayer.Terrain:SelectionLayer.None;
            State.SelectedObjectId=entity?.id??feature?.id;RememberViewport();inspector.Refresh(level,State);RefreshSelection();
        }
        private void RefreshSelection()
        { if(State.SelectionLayer==SelectionLayer.None)BoardInput.Overlay.ClearSelection();else BoardInput.Overlay.Select(State.SelectedCellX,State.SelectedCellY); }
        public void SelectLayer(SelectionLayer layer)
        {
            var level=CurrentLevel();if(level==null)return;int x=State.SelectedCellX,y=State.SelectedCellY;
            var entity=level.entities.Find(e=>e.x==x&&e.y==y);var feature=level.features.Find(e=>e.x==x&&e.y==y);
            bool available=layer==SelectionLayer.Entity?entity!=null:layer==SelectionLayer.Feature?feature!=null:new Coordinate(x,y).IsInside(level.width,level.height)&&level.terrain[y*level.width+x]==1;
            if(!available){Owner.App.Modal.Show("此格没有该对象","请选择包含该层对象的格子。");return;}
            State.SelectionLayer=layer;State.SelectedObjectId=layer==SelectionLayer.Entity?entity.id:layer==SelectionLayer.Feature?feature.id:null;RememberViewport();inspector.Refresh(level,State);RefreshSelection();
        }
        public void DeleteSelection()
        {
            string level=Owner.Document.SelectedLevelId,id=State.SelectedObjectId;
            if(State.SelectionLayer==SelectionLayer.Entity)Owner.Run(()=>LevelOperations.DeleteEntity(Owner.Document,level,id));
            else if(State.SelectionLayer==SelectionLayer.Feature)Owner.Run(()=>LevelOperations.DeleteFeature(Owner.Document,level,id));
            else if(State.SelectionLayer==SelectionLayer.Terrain)Owner.Run(()=>LevelOperations.DeleteWall(Owner.Document,level,State.SelectedCellX,State.SelectedCellY));
            State.SelectionLayer=SelectionLayer.None;State.SelectedObjectId=null;Refresh();
        }
        private void Update()
        {
            if(Owner?.Document==null||Board==null)return;if(lastSize!=root.rect.size)Layout();
            var k=Keyboard.current;if(k==null||composing||Owner.App.Input.Context>InputContext.Page)return;
            bool control=k.ctrlKey.isPressed||k.leftCommandKey.isPressed||k.rightCommandKey.isPressed;
            if(control&&k.sKey.wasPressedThisFrame){CommitFields();Owner.Operation("SaveDraft");return;}
            var selected=EventSystem.current?.currentSelectedGameObject;if(selected!=null&&selected.GetComponentInParent<TMP_InputField>()!=null)return;
            if(control&&k.zKey.wasPressedThisFrame){if(k.shiftKey.isPressed)Owner.Redo();else Owner.Undo();return;}
            for(int i=0;i<6;i++)if(k[(Key)((int)Key.Digit1+i)].wasPressedThisFrame)Palette.Choose(i);
            if(k.vKey.wasPressedThisFrame)Palette.Choose(6);
            if(k.deleteKey.wasPressedThisFrame||k.backspaceKey.wasPressedThisFrame)DeleteSelection();
            if(k.f5Key.wasPressedThisFrame)Owner.StartTrial(false);
            if(k.f6Key.wasPressedThisFrame)Owner.Operation(control?"AnalyzePack":"Solve");
        }
        private void OnDisable()
        {
            if(keyboard!=null)keyboard.onIMECompositionChange-=CompositionChanged;
            if(Owner==null)return;CommitGesture();RememberViewport();list?.CancelDrag();Owner.ViewClosed(this);
        }
    }
}
