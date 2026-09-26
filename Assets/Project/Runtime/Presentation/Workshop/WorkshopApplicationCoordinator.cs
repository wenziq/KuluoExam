using System;
using System.Linq;
using Sokoban.Core.Data;
using Sokoban.Core.Identity;
using Sokoban.Core.Validation;
using Sokoban.Domain.Gameplay;
using Sokoban.Domain.Workshop;
using Sokoban.Runtime.Persistence;
using Sokoban.Runtime.Presentation.App;
using Sokoban.Runtime.Presentation.Style;
using Sokoban.Runtime.Presentation.Views;
using UnityEngine;
using UnityEngine.EventSystems;
namespace Sokoban.Runtime.Presentation.Workshop
{
    public sealed class WorkshopApplicationCoordinator : MonoBehaviour
    {
        public WorkshopDocument Document { get; private set; }
        public WorkshopView View { get; private set; }
        public GameplayView Trial { get; private set; }
        public WorkshopViewportState Viewports { get; } = new WorkshopViewportState();
        public GameApplicationCoordinator Game { get; private set; }
        public ApplicationController App => Game.App;
        public event Action<string> OperationRequested;
        public event Action DocumentChanged;
        public event Action DocumentOpened;
        public Func<Action,bool> BeforeDocumentChange { get; set; }
        public string PersistenceStatus { get; set; } = "";
        private readonly System.Collections.Generic.Dictionary<string,Action> operations=new System.Collections.Generic.Dictionary<string,Action>();
        public void RegisterOperation(string id,Action action)=>operations[id]=action;
        public void UnregisterOperation(string id)=>operations.Remove(id);
        public event Action<GameSession> TrialCompleted;
        public bool IsTrial => Trial != null;
        public bool IsReferencePlayback {get; private set;}
        public bool InspectorCollapsed { get; set; }
        public int InspectorTab { get; set; }
        private PackData trialPack;
        private string trialId;
        private bool sequential;
        public void Initialize(GameApplicationCoordinator game)
        {
            if (Game != null) return;
            Game = game;
            game.ExtensionRequested += Route;
            game.EditRequested += EditEntry;
        }
        private void OnDestroy()
        {
            if (Game == null) return;
            Game.ExtensionRequested -= Route;
            Game.EditRequested -= EditEntry;
        }
        private void Route(string page)
        {
            if (page == "Workshop") Show();
            else if (page == "WorkshopLibrary") ShowLibrary();
            else if (page == "NewPack") NewPack();
            else if (page == "CopyExample") CopyExample();
        }
        private void EditEntry(CatalogEntry entry)
        {
            if (entry.IsDraft) Open(entry.Pack);
            else
            {
                var copy=ContentIdentity.CreateIndependentCopy(entry.Pack);
                copy.documentKind=DocumentKind.DraftPack;
                Open(copy,true);
            }
        }
        public void NewPack() => Open(new PackData { name="我的第一套关卡", documentKind=DocumentKind.DraftPack },true);
        public void CopyExample()
        {
            var entry=Game.Catalog.Entries.FirstOrDefault(e=>e.Source==ContentSource.BuiltIn);
            if(entry==null) { App.Modal.Show("没有示例", "当前没有内置关卡集，可以先新建关卡集。");return; }
            EditEntry(entry);
        }
        public void Open(PackData pack,bool unsaved=false)
        {
            if(pack.documentKind!=DocumentKind.DraftPack) throw new ArgumentException("工坊只编辑草稿，请先创建副本。");
            var document=new WorkshopDocument(pack);
            if(unsaved)document.MarkUnsaved();
            OpenDocument(document);
        }
        public void OpenDocument(WorkshopDocument document)
        {
            if(BeforeDocumentChange!=null&&!BeforeDocumentChange(()=>OpenDocumentCore(document)))return;
            OpenDocumentCore(document);
        }
        private void OpenDocumentCore(WorkshopDocument document)
        {
            View?.CommitGesture();App.ClearPage();
            Document=document;Viewports.Clear();trialPack=null;Trial=null;IsReferencePlayback=false;PersistenceStatus="";
            App.Navigate("Workshop");DocumentOpened?.Invoke();
        }
        public void ForgetDocument(WorkshopDocument expected)
        {
            if(ReferenceEquals(Document,expected)){Document=null;Viewports.Clear();}
        }
        public void ShowLibrary()
        {
            App.ClearPage(); View=null; Trial=null; IsReferencePlayback=false;
            Game.ReloadCatalog();
            var root=UiFactory.Rect("WorkshopLibrary",App.pageRoot);
            UiFactory.Fill(root);
            root.gameObject.AddComponent<PackLibraryView>().Build(App,Game.Catalog.Entries,null,EditEntry,
                draftsOnly:true,delete:entry=>GetComponent<WorkshopPersistenceController>().ConfirmDeleteDraft(entry),resume:Document==null?(Action)null:()=>App.Navigate("Workshop"));
        }
        private void Show()
        {
            if(Document==null){ShowLibrary();return;}
            App.ClearPage();Trial=null;IsReferencePlayback=false;
            var prefab=Resources.Load<GameObject>("UI/Pages/Workshop");
            var go=prefab!=null?Instantiate(prefab,App.pageRoot):UiFactory.Rect("Workshop",App.pageRoot).gameObject;
            UiFactory.Fill((RectTransform)go.transform);
            View=go.GetComponent<WorkshopView>()??go.AddComponent<WorkshopView>();
            View.Build(this);
        }
        public void ViewClosed(WorkshopView view) { if(ReferenceEquals(View,view)) View=null; }
        public void Run(Action action)
        {
            try { View?.CommitGesture();action();Changed(); }
            catch(Exception ex) { App.Modal.Show("操作未完成",ex.Message+"\n当前编辑内容仍保留。"); }
        }
        public void Changed()
        {
            View?.Refresh();DocumentChanged?.Invoke();
        }
        public void Select(string id)
        {
            View?.CommitFields();View?.CommitGesture();View?.RememberViewport();Document.SelectLevel(id);View?.Refresh();
        }
        public void Undo() => Run(()=>Document.Undo());
        public void Redo() => Run(()=>Document.Redo());
        public void Operation(string id)
        {
            View?.CommitFields();View?.CommitGesture();
            if(id=="PackInfo") { ShowPackInfo();return; }
            OperationRequested?.Invoke(id);
            if(operations.TryGetValue(id,out var action))action();
            else App.Modal.Show("操作不可用", "当前无法执行这项操作。请关闭窗口后重新进入关卡工坊；编辑内容仍然保留。");
        }
        public void ConfirmDelete()
        {
            if(Document?.SelectedLevelId==null)return;
            string id=Document.SelectedLevelId;
            App.Modal.Show("删除关卡？","此操作可以撤销。删除只影响当前草稿；正式游玩版本需要再次更新可玩关卡集才会改变。");
            App.Modal.AddAction("删除关卡",()=>{App.Modal.Close();Run(()=>LevelOperations.Delete(Document,id));});
        }
        public void Resize(int width,int height)
        {
            try
            {
                View.CommitGesture();var resize=new ResizeOperation(Document,Document.SelectedLevelId,width,height);
                if(!resize.RequiresCropConfirmation) { Run(()=>resize.Commit());return; }
                App.Modal.Show("确认裁剪地图", "以左下角为锚点调整为 "+width+" × "+height+"。\n将裁掉玩家 "+resize.PlayersRemoved+"、箱子 "+resize.BoxesRemoved+"、目标 "+resize.GoalsRemoved+"。\n可以撤销整个尺寸修改。");
                App.Modal.AddAction("确认裁剪",()=>{App.Modal.Close();Run(()=>resize.Commit());});
            }
            catch(Exception ex) { App.Modal.Show("尺寸未修改",ex.Message); }
        }
        public void StartTrial(bool fromFirst)
        {
            View?.CommitFields();View?.CommitGesture();View?.RememberViewport();
            if(Document==null)return;
            var snapshot=Document.Snapshot();
            string id=fromFirst?snapshot.levelOrder.FirstOrDefault():Document.SelectedLevelId;
            var level=snapshot.levels.Find(l=>l.levelId==id);
            var validation=level==null?null:StructureValidator.Validate(level);
            if(level==null||!validation.IsValid)
            {
                App.Modal.Show("暂时不能试玩",level==null?"请先新建关卡，再放置玩家、箱子和目标。":string.Join("\n",validation.Issues.Select(i=>i.Message)));
                return;
            }
            trialPack=snapshot;sequential=fromFirst;StartTrialLevel(id);
        }
        private void StartTrialLevel(string id)
        {
            var level=trialPack.levels.Find(l=>l.levelId==id);
            var validation=StructureValidator.Validate(level);
            if(!validation.IsValid)
            {
                App.Modal.Show("下一关尚不完整", string.Join("\n",validation.Issues.Select(i=>i.Message)));
                App.Modal.AddAction("返回编辑",ReturnToEditor);return;
            }
            PresentTrial(id,new GameSession(level,SessionMode.Trial));
        }
        public void StartAssistedTrial(LevelData root,GameSession session)
        {
            if(session.Mode!=SessionMode.AssistedReplayTakeover)throw new ArgumentException("需要辅助试玩会话。");
            var initial=session.InitialState.ToLevelData();if(root.levelId!=initial.levelId||LevelFingerprint.Compute(root)!=LevelFingerprint.Compute(initial))throw new ArgumentException("接管会话与参考解根布局不一致。");
            trialPack=new PackData{name="参考解辅助试玩"};trialPack.levels.Add(root.DeepCopy());trialPack.levelOrder.Add(root.levelId);sequential=false;
            PresentTrial(root.levelId,session);
        }
        public Sokoban.Runtime.Presentation.Analysis.PlaybackView ShowPlayback(LevelData level,SolutionPlaybackSession session)
        {
            View?.CommitFields();View?.CommitGesture();View?.RememberViewport();App.Navigate("Gameplay");App.ClearPage();View=null;Trial=null;IsReferencePlayback=true;
            var root=UiFactory.Rect("ReferencePlayback",App.pageRoot);UiFactory.Fill(root);
            var playback=root.gameObject.AddComponent<Sokoban.Runtime.Presentation.Analysis.PlaybackView>();
            playback.Build(App,level,session,ReturnToEditor,assisted=>StartAssistedTrial(level,assisted));return playback;
        }
        private void PresentTrial(string id,GameSession session)
        {
            trialId=id;App.Navigate("Gameplay");App.ClearPage();View=null;IsReferencePlayback=false;
            var root=UiFactory.Rect("DraftTrial",App.pageRoot);UiFactory.Fill(root);
            Trial=root.gameObject.AddComponent<GameplayView>();
            Trial.Build(App,trialPack,id,session,ReturnToEditor);
            Trial.Completed+=ShowTrialResult;
            Trial.CompletionCommitted+=()=>TrialCompleted?.Invoke(Trial.Session);
            Trial.PauseRequested+=PauseTrial;
            Trial.RestartRequested+=()=>
            {
                if(Trial.Session.Moves==0) { Trial.Restart();return; }
                App.Modal.Show("重新试玩？","本次试玩会从初始布局开始，编辑内容保持不变。");
                App.Modal.AddAction("重新开始",()=>{App.Modal.Close();Trial.Restart();});
            };
        }
        private void ShowTrialResult()
        {
            string next=sequential?UnlockPolicyEvaluator.Next(trialPack,trialId):null;
            App.Modal.Show(Trial.Session.Mode==SessionMode.AssistedReplayTakeover?"辅助试玩完成":"试玩完成", "本次 "+Trial.Session.Pushes+" 推动 · "+Trial.Session.Moves+" 步。\n试玩不计入正式成绩。编辑内容和历史已保留。");
            App.Modal.AddAction("返回编辑",ReturnToEditor);
            if(next!=null)App.Modal.AddAction("试玩下一关",()=>StartTrialLevel(next));
            App.Modal.AddAction("再试一次",()=>PresentTrial(trialId,new GameSession(trialPack.levels.Find(l=>l.levelId==trialId),Trial.Session.Mode)),false);
        }
        private void PauseTrial()
        {
            App.Modal.Show("暂停试玩","正在试玩草稿。返回编辑后可以继续修改地图。");
            App.Modal.AddAction("继续试玩",App.Modal.Close);
            App.Modal.AddAction("返回编辑",ReturnToEditor);
        }
        public void ReturnToEditor()
        {
            if(Document==null)return;
            App.Navigate("Workshop");trialPack=null;
        }
        private void Update()
        {
            var keyboard=UnityEngine.InputSystem.Keyboard.current;
            if(Trial!=null&&keyboard!=null&&App.Input.Context==InputContext.Gameplay&&keyboard.shiftKey.isPressed&&keyboard.f5Key.wasPressedThisFrame)ReturnToEditor();
        }
        private void ShowPackInfo()
        {
            if(Document==null)return;
            var pack=Document.Snapshot();
            App.Modal.Show("修改关卡集信息","");
            var content=App.Modal.Body.transform.parent;App.Modal.Body.gameObject.SetActive(false);
            var name=WorkshopFields.Field(content,"PackName",pack.name,App.theme,80,48);
            var description=WorkshopFields.Field(content,"PackDescription",pack.description,App.theme,2000,130,true);
            var policy=pack.unlockPolicy;
            var button=UiFactory.Button("UnlockPolicy",content,policy==UnlockPolicy.Sequential?"顺序解锁":"全部可选",App.theme);
            UiFactory.Preferred(button.gameObject,40);
            button.onClick.AddListener(()=>{policy=policy==UnlockPolicy.Sequential?UnlockPolicy.AllOpen:UnlockPolicy.Sequential;button.GetComponentInChildren<TMPro.TextMeshProUGUI>().text=policy==UnlockPolicy.Sequential?"顺序解锁":"全部可选";});
            App.Modal.AddAction("保存信息",()=>{Run(()=>LevelOperations.SetPackMetadata(Document,name.text,description.text,policy));App.Modal.Close();});
        }
    }
}
