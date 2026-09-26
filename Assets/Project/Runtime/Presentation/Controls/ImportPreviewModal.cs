using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Sokoban.Core.Data;
using Sokoban.Runtime.Persistence;
using Sokoban.Runtime.Platform.FileDialogs;
using Sokoban.Runtime.Presentation.App;
using Sokoban.Runtime.Presentation.Style;
using Sokoban.Runtime.Presentation.Workshop;
using UnityEngine;
namespace Sokoban.Runtime.Presentation.Controls
{
 public sealed class ImportPreviewModal:MonoBehaviour
 {
  WorkshopApplicationCoordinator owner;ImportExportService service;IFileDialogService dialogs;CancellationTokenSource operation;int generation;bool picking;
  public Task LastTask {get;private set;}=Task.CompletedTask;
  public void Initialize(WorkshopApplicationCoordinator value)
  {
   if(owner!=null)return;owner=value;service=new ImportExportService(owner.Game.DataPaths,owner.Game.Files,reservedIds:owner.Game.Catalog.Entries.Where(e=>e.Source==ContentSource.BuiltIn).Select(e=>e.PackId));dialogs=dialogs??new RuntimeFileDialogService(owner.App);
   owner.RegisterOperation("ImportPack",BeginImport);owner.RegisterOperation("ExportDraft",()=>BeginExport(false));owner.RegisterOperation("ExportPlayable",()=>BeginExport(true));
   owner.Game.ExtensionRequested+=Route;
  }
  public void ConfigureDialogs(IFileDialogService value)
  {
   if(picking||!LastTask.IsCompleted)throw new InvalidOperationException("文件操作尚未完成。");
   dialogs=value??throw new ArgumentNullException(nameof(value));
  }
  async Task<string> Pick(bool save,string name=null)
  {
   picking=true;
   try{return await(save?dialogs.SaveAsync(name):dialogs.OpenAsync());}
   catch(Exception e){if(this!=null)owner.App.Modal.Show("文件选择未完成",e.Message);return null;}
   finally{picking=false;}
  }
  void Route(string page){if(page=="ImportPack")BeginImport();else if(page=="PendingImports")ShowPending();}
  public void BeginImport(){if(!picking)LastTask=PickImport();}
  async Task PickImport()
  {
   int request=++generation;var path=await Pick(false);if(path==null||this==null||request!=generation)return;
   operation?.Cancel();var source=new CancellationTokenSource();operation=source;
   owner.App.Modal.Show("读取关卡包","正在检查文件格式与大小。",()=>source.Cancel());
   try{var pack=await service.ReadAsync(path,source.Token);if(this!=null&&request==generation&&!source.IsCancellationRequested)Preview(pack,null);}
   catch(OperationCanceledException){}
   catch(Exception e){if(this!=null&&request==generation&&!source.IsCancellationRequested)owner.App.Modal.Show("无法读取关卡包",e.Message);}
  }
  void Preview(PackData pack,string stagingId)
  {
   bool conflict=owner.Game.Catalog.Entries.Any(e=>e.PackId==pack.packId);bool builtin=owner.Game.Catalog.Entries.Any(e=>e.Source==ContentSource.BuiltIn&&e.PackId==pack.packId);
   owner.App.Modal.Show("导入预览",pack.name+"\n"+pack.levels.Count+" 个关卡 · "+(pack.documentKind==DocumentKind.DraftPack?"草稿包":"可玩包，通关证据将在本机验证")+"\n"+(conflict?"发现同身份内容，默认创建独立副本。":"创建独立副本，保留关卡顺序和解锁方式。"));
   owner.App.Modal.AddAction("导入副本",()=>StartImport(pack,false,false,stagingId));
   if(pack.documentKind==DocumentKind.PlayablePack)owner.App.Modal.AddAction("转为草稿",()=>StartImport(pack,false,true,stagingId),false);
   if(conflict&&!builtin)
   {
    var b=UiFactory.Button("ReplaceImport",owner.App.Modal.Body.transform.parent,"替换同身份内容…",owner.App.theme,()=>
    {owner.App.Modal.Show("确认替换", "全部内容验证通过后才替换完整文件，旧版本保留备份。");owner.App.Modal.AddAction("确认替换",()=>StartImport(pack,true,false,stagingId));});UiFactory.Preferred(b.gameObject,40);
   }
  }
  void StartImport(PackData pack,bool replace,bool draft,string stagingId){LastTask=Import(pack,replace,draft,stagingId);}
  async Task Import(PackData pack,bool replace,bool draft,string stagingId)
  {
   operation?.Cancel();var source=new CancellationTokenSource();operation=source;int request=++generation;
   owner.App.Modal.Show("正在导入", "正在验证文件与每一关的通关路线。关闭窗口将取消尚未提交的操作。",()=>source.Cancel());
   var result=await service.ImportAsync(pack,replace,draft,source.Token,stagingId);
   if(this==null)return;owner.Game.ReloadCatalog();
   if(request!=generation||source.IsCancellationRequested)return;
   owner.App.Modal.Show(result.Committed?"导入完成":"导入尚未完成",result.Committed?result.Pack.name+" 已保存在这台电脑。\n"+result.Error:result.Error+"\n"+(result.StagingId!=null?"内容保留在待验证列表，可重试或转为草稿。":""));
   if(result.Committed)
    owner.App.Modal.AddAction(result.Pack.documentKind==DocumentKind.DraftPack?"打开草稿":"前往关卡集",()=>{owner.App.Modal.Close();if(result.Pack.documentKind==DocumentKind.DraftPack)owner.Open(result.Pack);else owner.App.Navigate("PackLibrary");});
   else if(result.StagingId!=null)owner.App.Modal.AddAction("查看待验证",ShowPending);
  }
  void BeginExport(bool playable){if(!picking)LastTask=Export(playable);}
  async Task Export(bool playable)
  {
   if(owner.Document==null)return;owner.View?.CommitFields();owner.View?.CommitGesture();var snapshot=owner.Document.Snapshot();
   var path=await Pick(true,(playable?"可玩关卡包":"草稿关卡包")+ImportExportService.Extension);if(path==null||this==null)return;
   var source=new CancellationTokenSource();operation?.Cancel();operation=source;int request=++generation;
   owner.App.Modal.Show("正在导出","正在准备完整文件，关闭窗口可取消尚未提交的导出。",()=>source.Cancel());
   var result=await service.ExportAsync(snapshot,path,playable,true,source.Token);
   if(this==null||request!=generation||source.IsCancellationRequested)return;
   owner.App.Modal.Show(result.Committed?"导出完成":"导出失败",result.Committed?path:result.Error?.Message??"未完成写入。");
  }
  public void ShowPending()
  {
   owner.App.Modal.Show("待验证的导入", "这些内容尚未进入正式关卡库。可重新验证，或转为草稿继续制作。");
   try
   {
    var ids=service.Staging.List();if(ids.Count==0)owner.App.Modal.SetMessage("没有待验证内容。");
    foreach(var id in ids.Take(100))
    {
     try{var pack=service.Staging.Load(id);string captured=id;var b=UiFactory.Button("PendingImport",owner.App.Modal.Body.transform.parent,pack.name,owner.App.theme,()=>Preview(pack,captured));UiFactory.Preferred(b.gameObject,40);}
     catch(Exception e){var text=UiFactory.Text("DamagedImport",owner.App.Modal.Body.transform.parent,"暂存读取失败："+e.Message,owner.App.theme,13);UiFactory.Preferred(text.gameObject,60);}
    }
   }
   catch(Exception e){owner.App.Modal.SetMessage(e.Message);}
  }
  void OnDestroy(){generation++;operation?.Cancel();if(owner==null)return;owner.Game.ExtensionRequested-=Route;owner.UnregisterOperation("ImportPack");owner.UnregisterOperation("ExportDraft");owner.UnregisterOperation("ExportPlayable");}
 }
}
