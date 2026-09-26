using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Sokoban.Core.Data;
using Sokoban.Core.Identity;
using Sokoban.Domain.Workshop;
using Sokoban.Runtime.Persistence;
using Sokoban.Runtime.Presentation.Controls;
using UnityEngine;
namespace Sokoban.Runtime.Presentation.Workshop
{
    public sealed class WorkshopPersistenceController:MonoBehaviour
    {
        public Task<TransactionResult> LastSave { get; private set; } = Task.FromResult<TransactionResult>(null);
        public Task<TransactionResult> LastRecovery { get; private set; } = Task.FromResult<TransactionResult>(null);
        public Task<TransactionResult> LastRemoval { get; private set; } = Task.FromResult<TransactionResult>(null);
        public bool IsDeleting { get; private set; }
        public bool IsLeaving { get; private set; }
        public bool IsSaving => pendingSaves>0;
        public DraftRepository Drafts { get; private set; }
        public RecoveryService Recovery { get; private set; }
        private WorkshopApplicationCoordinator owner;
        private int pendingSaves,leaveGeneration;
        private bool bypass,allowQuit,destroyed;
        private double recoveryDue=double.PositiveInfinity;
        private string recoveryHash;
        private Action pendingLeave;
        private WorkshopDocument leavingDocument;
        private readonly Queue<RecoveryItem> recovered=new Queue<RecoveryItem>();
        private readonly Queue<string> damagedDrafts=new Queue<string>();
        private string scanError,pendingSaveError;
        private WorkshopDocument errorDocument;
        public void Initialize(WorkshopApplicationCoordinator owner,UserDataPaths paths,IFileSystem files=null)
        {
            if(this.owner!=null)return;
            this.owner=owner;Drafts=new DraftRepository(paths,files);Recovery=new RecoveryService(paths,files);
            owner.RegisterOperation("SaveDraft",()=>{LastSave=SaveAsync();});
            owner.RegisterOperation("SaveCopy",()=>{LastSave=SaveCopyAsync();});
            owner.BeforeDocumentChange=Guard;
            owner.DocumentChanged+=ScheduleRecovery;owner.DocumentOpened+=DocumentOpened;
            owner.App.BeforeNavigation+=BeforeNavigate;
            Application.wantsToQuit+=WantsToQuit;
            _=ScanRecoveryAsync();
        }
        public void ConfirmDeleteDraft(CatalogEntry entry)
        {
            if(entry==null||!entry.IsDraft||IsDeleting||IsLeaving)return;
            owner.App.Modal.Show("删除工坊关卡集", "确定删除“"+entry.Pack.name+"”吗？\n\n这份制作草稿及其恢复副本将从工坊移除。当前正在编辑的同一份草稿也会关闭。\n\n已加入的可玩关卡集和历史成绩会保留。");
            UnsavedChangesModal.RemoveDone(owner.App);
            owner.App.Modal.AddAction("取消",owner.App.Modal.Close,false);
            owner.App.Modal.AddAction("删除关卡集",()=>{if(!IsDeleting)LastRemoval=RemoveDraftAsync(entry.PackId);});
        }
        private async Task<TransactionResult> RemoveDraftAsync(string id)
        {
            IsDeleting=true;
            owner.App.Modal.Show("正在删除工坊关卡集","正在等待已有保存完成，并移除草稿和恢复副本。");
            var marker=owner.App.Modal.Body;
            TransactionResult result;
            try
            {
                // Applying can save a captured draft too; drain it before removing any files.
                var apply=GetComponent<ApplyPackModal>();
                if(apply!=null)await apply.LastApply;
                await LastSave;await LastRecovery;
                while(IsSaving)await Task.Delay(10);
                result=await Drafts.RemoveAsync(id);
                if(destroyed)return result;
                if(result.Committed)
                {
                    if(owner.Document?.Snapshot().packId==id)
                    {
                        owner.ForgetDocument(owner.Document);
                        recoveryDue=double.PositiveInfinity;recoveryHash=null;pendingSaveError=null;errorDocument=null;
                    }
                    owner.Game.ReloadCatalog();
                }
            }
            catch(Exception ex){result=new TransactionResult{Status=TransactionStatus.Failed,Error=ex};}
            finally{IsDeleting=false;}
            if(destroyed)return result;
            if(owner.App.Modal.Body==marker)
            {
                owner.App.Modal.Close();
                if(!result.Committed)owner.App.Modal.Show("删除未完成", "请重试。\n"+result.Error?.Message);
            }
            if(result.Committed&&(owner.App.CurrentPage=="WorkshopLibrary"||owner.App.CurrentPage=="Workshop"))owner.ShowLibrary();
            return result;
        }
        private async Task ScanRecoveryAsync()
        {
            try
            {
                var items=await Task.Run(()=>Recovery.Enumerate());
                var damaged=await Task.Run(()=>Drafts.ListDamaged());
                if(destroyed)return;
                foreach(var item in items)recovered.Enqueue(item);
                foreach(var id in damaged)damagedDrafts.Enqueue(id);
            }
            catch(Exception ex){if(!destroyed)scanError=ex.Message;}
        }
        private void DocumentOpened(){recoveryHash=null;pendingSaveError=null;errorDocument=null;ScheduleRecovery();}
        private void ScheduleRecovery(){recoveryDue=Time.realtimeSinceStartupAsDouble+2;}
        public Task<TransactionResult> SaveAsync(bool showFailure=true)
        {
            owner.View?.CommitFields();owner.View?.CommitGesture();
            LastSave=SaveCoreAsync(owner.Document,showFailure);return LastSave;
        }
        public Task<TransactionResult> SaveSnapshotAsync(WorkshopDocument document,PackData snapshot)
        {
            if(document==null||snapshot==null||document.Snapshot().packId!=snapshot.packId)throw new ArgumentException("保存快照与草稿身份不一致。");
            LastSave=SaveCoreAsync(document,false,snapshot);return LastSave;
        }
        private async Task<TransactionResult> SaveCoreAsync(WorkshopDocument document,bool showFailure,PackData captured=null)
        {
            if(IsDeleting||document==null)return new TransactionResult{Status=TransactionStatus.Cancelled};
            var snapshot=captured?.DeepCopy()??document.Snapshot();string hash=DocumentHash.Compute(snapshot);pendingSaves++;
            Status("正在保存草稿快照…");
            try
            {
                var result=await Drafts.SaveAsync(snapshot);
                if(result.Committed)
                {
                    string secondaryWarning=null;
                    document.MarkSaved(hash);
                    if(destroyed)return result;
                    owner.Game.ReloadCatalog();
                    if(document.IsDirty)
                    {
                        LastRecovery=Recovery.SaveAsync(document);var recoveryResult=await LastRecovery;
                        if(recoveryResult.Status==TransactionStatus.Failed)secondaryWarning="恢复副本更新失败";
                    }
                    else
                    {
                        try{await Recovery.DeleteAsync(snapshot.packId);}
                        catch(Exception ex){secondaryWarning="恢复副本清理失败："+ex.Message;}
                    }
                    if(!destroyed&&ReferenceEquals(owner.Document,document))Status((document.IsDirty?"已保存先前快照，当前还有新修改":"草稿已保存")+(secondaryWarning==null?"":"；"+secondaryWarning));
                }
                else if(!destroyed&&ReferenceEquals(owner.Document,document))
                {
                    Status("保存失败，当前修改仍保留");
                    if(showFailure)ShowSaveFailure(result.Error?.Message);
                }
                return result;
            }
            catch(Exception ex)
            {
                var result=new TransactionResult{Status=TransactionStatus.Failed,Error=ex};
                if(!destroyed&&ReferenceEquals(owner.Document,document)){Status("保存失败，当前修改仍保留");if(showFailure)ShowSaveFailure(ex.Message);}return result;
            }
            finally{pendingSaves--;if(!destroyed&&ReferenceEquals(owner.Document,document))owner.View?.Refresh();}
        }
        private void ShowSaveFailure(string error)
        {
            if(owner.App.Modal.IsOpen){pendingSaveError=error??"未知写入错误";errorDocument=owner.Document;return;}
            owner.App.Modal.Show("保存失败，当前修改仍保留",error+"\n请重试，或另存为新草稿。已有有效文件和当前编辑均保留。");
            owner.App.Modal.AddAction("重试保存",()=>{owner.App.Modal.Close();LastSave=SaveAsync();});
            owner.App.Modal.AddAction("另存为新草稿",()=>{owner.App.Modal.Close();LastSave=SaveCopyAsync();},false);
        }
        public Task<TransactionResult> SaveRecoveryNowAsync()
        {
            var document=owner.Document;
            if(IsDeleting||document==null||document.HasActiveStroke)return Task.FromResult(new TransactionResult{Status=TransactionStatus.Cancelled});
            string hash=document.CurrentHash;LastRecovery=SaveRecoveryCoreAsync(document,hash);return LastRecovery;
        }
        private async Task<TransactionResult> SaveRecoveryCoreAsync(WorkshopDocument document,string hash)
        {
            var result=await Recovery.SaveAsync(document);
            if(destroyed||!ReferenceEquals(owner.Document,document))return result;
            if(result.Committed){recoveryHash=hash;Status("恢复副本已更新");}
            else if(result.Status==TransactionStatus.Failed){Status("恢复副本写入失败，当前编辑仍保留；稍后重试");recoveryDue=Time.realtimeSinceStartupAsDouble+10;}
            return result;
        }
        private async Task<TransactionResult> SaveCopyAsync()
        {
            owner.View?.CommitFields();owner.View?.CommitGesture();var source=owner.Document;
            if(IsDeleting||source==null)return new TransactionResult{Status=TransactionStatus.Cancelled};
            string hash=source.CurrentHash;var copy=ContentIdentity.CreateIndependentCopy(source.Snapshot());copy.documentKind=DocumentKind.DraftPack;
            pendingSaves++;Status("正在另存新草稿…");
            try
            {
                var result=await Drafts.SaveAsync(copy);if(destroyed)return result;
                if(!result.Committed){ShowSaveFailure(result.Error?.Message);return result;}
                owner.Game.ReloadCatalog();
                bool CanSwitch()=>!destroyed&&!IsLeaving&&!owner.App.Modal.IsOpen&&owner.App.CurrentPage=="Workshop"&&ReferenceEquals(owner.Document,source)&&source.CurrentHash==hash;
                if(CanSwitch())
                {
                    try{await Recovery.DeleteAsync(source.Snapshot().packId);}catch(Exception ex){Status("新草稿已保存；原恢复副本仍保留："+ex.Message);}
                    // Cleanup is asynchronous: editing, navigation or cancellation may have happened meanwhile.
                    if(CanSwitch()) WithBypass(()=>owner.Open(copy));
                    else
                    {
                        if(!destroyed&&ReferenceEquals(owner.Document,source))ScheduleRecovery();
                        Status("新副本已保存；稍后的修改仍留在当前草稿");
                    }
                }
                else Status("新副本已保存；稍后的修改仍留在当前草稿");
                return result;
            }
            catch(Exception ex){if(!destroyed)ShowSaveFailure(ex.Message);return new TransactionResult{Status=TransactionStatus.Failed,Error=ex};}
            finally{pendingSaves--;}
        }
        private bool BeforeNavigate(string destination)
        {
            if(IsDeleting)return false;
            if(bypass||owner.Document==null)return true;
            bool inWorkshop=owner.App.CurrentPage=="Workshop"||owner.IsTrial||owner.IsReferencePlayback;
            if(!inWorkshop||destination=="Workshop"||destination=="Gameplay")return true;
            return Guard(()=>owner.App.Navigate(destination));
        }
        public bool Guard(Action continuation)
        {
            if(IsDeleting)return false;
            if(bypass)return true;
            owner.View?.CommitFields();owner.View?.CommitGesture();
            if(owner.Document==null||!owner.Document.IsDirty)return true;
            if(IsLeaving)return false;
            owner.App.Modal.Close();pendingLeave=continuation;leavingDocument=owner.Document;ShowLeavePrompt();return false;
        }
        private void ShowLeavePrompt(string error=null)
        {
            UnsavedChangesModal.Show(owner.App,()=>{_=SaveThenLeaveAsync();},()=>{_=DiscardThenLeaveAsync();},CancelLeave,error);
        }
        private int BeginLeaving(string title,string body)
        {
            IsLeaving=true;int generation=++leaveGeneration;
            owner.App.Modal.Show(title,body,()=>{if(IsLeaving)CancelLeave();});
            return generation;
        }
        private void CancelLeave(){pendingLeave=null;leavingDocument=null;IsLeaving=false;leaveGeneration++;ScheduleRecovery();}
        private async Task SaveThenLeaveAsync()
        {
            var document=leavingDocument;int generation=BeginLeaving("保存后继续","正在提交草稿。关闭此窗口可以取消离开，保存仍会完成。");
            var result=await SaveAsync(false);
            if(destroyed||generation!=leaveGeneration)return;
            IsLeaving=false;owner.App.Modal.Close();
            if(result.Committed&&ReferenceEquals(owner.Document,document)&&!document.IsDirty)CompleteLeave();
            else ShowLeavePrompt(result.Committed?"保存期间又有新修改，请选择如何处理。":"保存失败，当前修改仍保留："+result.Error?.Message);
        }
        private async Task DiscardThenLeaveAsync()
        {
            var document=leavingDocument;int generation=BeginLeaving("丢弃本次修改","正在清除对应恢复副本。已有手动保存文件保持不变。");
            try
            {
                await LastSave;await LastRecovery;
                if(destroyed||generation!=leaveGeneration)return;
                await Recovery.DeleteAsync(document.Snapshot().packId);
                if(destroyed||generation!=leaveGeneration){if(!destroyed)ScheduleRecovery();return;}
                IsLeaving=false;owner.App.Modal.Close();owner.ForgetDocument(document);CompleteLeave();
            }
            catch(Exception ex)
            {
                if(destroyed||generation!=leaveGeneration)return;
                IsLeaving=false;owner.App.Modal.Close();ShowLeavePrompt("无法清除恢复副本，暂未离开："+ex.Message);
            }
        }
        private void CompleteLeave()
        {
            var action=pendingLeave;pendingLeave=null;leavingDocument=null;recoveryDue=double.PositiveInfinity;
            if(action!=null)WithBypass(action);
        }
        private void WithBypass(Action action)
        {bool previous=bypass;bypass=true;try{action();}finally{bypass=previous;}}
        private bool WantsToQuit()
        {
            if(owner==null)return true;
            var settings=GetComponent<Sokoban.Runtime.Presentation.App.SettingsController>();
            if(settings!=null&&!settings.GuardQuit(()=>Application.Quit()))return false;
            if(allowQuit)return true;
            return Guard(()=>{allowQuit=true;Application.Quit();});
        }
        private void Status(string message)
        {if(destroyed||owner==null)return;owner.PersistenceStatus=message;owner.View?.RefreshStatus();}
        private void Update()
        {
            if(owner==null||destroyed||IsDeleting)return;
            if(!IsLeaving&&!IsSaving&&LastRecovery.IsCompleted&&Time.realtimeSinceStartupAsDouble>=recoveryDue&&owner.Document!=null&&!owner.Document.HasActiveStroke)
            {
                recoveryDue=double.PositiveInfinity;
                if(owner.Document.IsDirty&&owner.Document.CurrentHash!=recoveryHash)LastRecovery=SaveRecoveryNowAsync();
            }
            if(!owner.App.Modal.IsOpen&&pendingSaveError!=null)
            {
                string error=pendingSaveError;pendingSaveError=null;
                if(ReferenceEquals(owner.Document,errorDocument)){ShowSaveFailure(error);return;}
            }
            if(owner.App.Modal.IsOpen||IsLeaving||owner.Document?.IsDirty==true)return;
            if(scanError!=null){string error=scanError;scanError=null;owner.App.Modal.Show("恢复目录读取失败",error+"\n现有文件保留，当前可以继续制作。");return;}
            if(damagedDrafts.Count>0){string id=damagedDrafts.Dequeue();if(Drafts.Load(id).Error!=null)_=ShowDamagedAsync(id,true);return;}
            if(recovered.Count>0)
            {
                var item=recovered.Dequeue();
                try{item.Snapshot=Recovery.Load(item.Id);if(item.Snapshot==null)return;}
                catch(Exception){_=ShowDamagedAsync(item.Id,false);return;}
                var manual=Drafts.Load(item.Id);
                if(manual.Succeeded&&DocumentHash.Compute(manual.Pack)==DocumentHash.Compute(item.Snapshot.Pack))return;
                RecoveryModal.Show(owner.App,item.Snapshot,manual.Succeeded,()=>
                {
                    owner.App.Modal.Close();owner.OpenDocument(RecoveredDocument(item.Snapshot));
                },()=>{owner.App.Modal.Close();if(manual.Succeeded)owner.Open(manual.Pack);});
            }
        }
        private async Task ShowDamagedAsync(string id,bool manual)
        {
            owner.App.Modal.Show("检查本地备份","正在检查可恢复的备份，原文件保持不变。");
            var body=owner.App.Modal.Body;
            try
            {
                var report=await(manual?Drafts.InspectAsync(id):Recovery.InspectAsync(id));
                if(destroyed||owner.App.Modal.Body!=body)return;
                bool valid=report.Candidates.Any(c=>c.Kind==CandidateKind.Backup&&c.Status==CandidateStatus.Valid);
                owner.App.Modal.SetMessage((manual?"手动草稿":"恢复草稿")+"无法读取。\n"+(valid?"发现一份有效备份，可以明确选择恢复。":"未找到可读取的备份。原文件仍保留。"));
                if(valid)owner.App.Modal.AddAction("恢复有效备份",()=>{_=RestoreBackupAsync(id,manual);});
            }
            catch(Exception ex){if(!destroyed&&owner.App.Modal.Body==body)owner.App.Modal.SetMessage("无法检查备份："+ex.Message+"\n原文件保留。");}
        }
        private async Task RestoreBackupAsync(string id,bool manual)
        {
            var body=owner.App.Modal.Body;
            var result=await(manual?Drafts.RecoverBackupAsync(id):Recovery.RecoverBackupAsync(id));
            if(destroyed)return;
            if(owner.App.Modal.Body!=body){Status(result.Committed?"本地备份已恢复":"备份恢复未完成");return;}
            if(!result.Committed){owner.App.Modal.Show("恢复未完成",result.Error?.Message);return;}
            owner.App.Modal.Close();
            if(manual){var loaded=Drafts.Load(id);if(loaded.Succeeded){owner.Game.ReloadCatalog();owner.Open(loaded.Pack);}}
            else{var recovered=Recovery.Load(id);if(recovered!=null)owner.OpenDocument(RecoveredDocument(recovered));}
        }
        private WorkshopDocument RecoveredDocument(RecoverySnapshot snapshot)
        {
            var document=snapshot.Restore();var manual=Drafts.Load(snapshot.Pack.packId);
            if(manual.Succeeded)document.MarkSaved(DocumentHash.Compute(manual.Pack));else document.MarkUnsaved();
            return document;
        }
        private void OnDestroy()
        {
            destroyed=true;Application.wantsToQuit-=WantsToQuit;
            if(owner==null)return;owner.App.BeforeNavigation-=BeforeNavigate;owner.BeforeDocumentChange=null;
            owner.DocumentChanged-=ScheduleRecovery;owner.DocumentOpened-=DocumentOpened;
            owner.UnregisterOperation("SaveDraft");owner.UnregisterOperation("SaveCopy");
        }
    }
}
