using System;
using System.Threading.Tasks;
using Sokoban.Runtime.Persistence;
using Sokoban.Runtime.Platform.Feedback;
using Sokoban.Runtime.Presentation.Views;
using Sokoban.Runtime.Presentation.Workshop;
using UnityEngine;
namespace Sokoban.Runtime.Presentation.App
{
 public sealed class SettingsController:MonoBehaviour
 {
  ApplicationController app;SettingsRepository repository;GameSettings current;FeedbackController feedback;int revision;bool quitWithoutSaving,waitingToQuit;int quitGeneration;
  public GameSettings Current=>current.Copy();public string Status {get;private set;}public event Action Changed;
  public Task<TransactionResult> LastSave {get;private set;}=Task.FromResult<TransactionResult>(null);
  public void Initialize(ApplicationController value,UserDataPaths paths,IFileSystem files)
  {app=value;repository=new SettingsRepository(paths,files);var loaded=repository.Load();current=loaded.Settings;Status=loaded.Error??"设置自动保存在这台电脑。";feedback=GetComponent<FeedbackController>()??gameObject.AddComponent<FeedbackController>();Apply();}
  void Apply()
  {
   feedback.Configure(current);
#if !UNITY_EDITOR
   Screen.fullScreenMode=current.fullscreen?FullScreenMode.FullScreenWindow:FullScreenMode.Windowed;
#endif
  }
  public void Set(GameSettings value){current=value.Copy();current.volume=Mathf.Clamp01(current.volume);Apply();Status="正在保存设置…";Changed?.Invoke();LastSave=Save(current.Copy(),++revision);}
  async Task<TransactionResult> Save(GameSettings snapshot,int generation)
  {var result=await repository.SaveAsync(snapshot);if(this!=null&&generation==revision){Status=result.Committed?"设置已保存。":"设置保存失败："+result.Error?.Message;Changed?.Invoke();}return result;}
  public bool GuardQuit(Action continuation)
  {
   if(quitWithoutSaving||LastSave.IsCompleted&&(LastSave.Result==null||LastSave.Result.Committed))return true;
   if(waitingToQuit)return false;waitingToQuit=true;int request=++quitGeneration;
   app.Modal.Show("保存设置后退出","正在等待设置写入。关闭此窗口会取消退出。",()=>{waitingToQuit=false;quitGeneration++;});
   _=FinishQuit(LastSave,request,continuation);return false;
  }
  async Task FinishQuit(Task<TransactionResult> saving,int request,Action continuation)
  {
   var result=await saving;if(this==null||request!=quitGeneration)return;waitingToQuit=false;
   if(result?.Committed==true){app.Modal.Close();continuation();return;}
   app.Modal.Show("设置尚未保存", "退出后可能丢失本次设置。可重试保存或取消退出。\n"+result?.Error?.Message);
   app.Modal.AddAction("重试保存",()=>{Set(current);GuardQuit(continuation);});
   app.Modal.AddAction("仍然退出",()=>{quitWithoutSaving=true;app.Modal.Close();continuation();},false);
  }
  public void Show(string page)
  {
   var workshop=GetComponent<WorkshopApplicationCoordinator>();workshop?.View?.CommitFields();workshop?.View?.CommitGesture();app.Input.ClearPending();app.Tooltips.Hide();
   foreach(var menu in app.GetComponentsInChildren<Sokoban.Runtime.Presentation.Controls.HoverMenu>())menu.Close();
   if(page=="Settings")SettingsView.Show(app,this);else HelpView.Show(app);
  }
 }
}
