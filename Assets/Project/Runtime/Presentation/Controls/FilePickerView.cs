using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Sokoban.Runtime.Persistence;
using Sokoban.Runtime.Platform.FileDialogs;
using Sokoban.Runtime.Presentation.App;
using Sokoban.Runtime.Presentation.Style;
using Sokoban.Runtime.Presentation.Workshop;
using TMPro;
using UnityEngine;
namespace Sokoban.Runtime.Presentation.Controls
{
 internal sealed class FilePickerLifetime:MonoBehaviour
 {public Action Closed;void OnDisable(){Closed?.Invoke();Closed=null;}}
 public sealed class FilePickerView
 {
  readonly ApplicationController app;readonly bool save;readonly TaskCompletionSource<string> completion=new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
  readonly CancellationTokenSource lifetime=new CancellationTokenSource();
  TMP_InputField address,name;RectTransform entries;TextMeshProUGUI status;string current;int generation;bool confirming;
  FilePickerView(ApplicationController app,bool save){this.app=app;this.save=save;}
  public static Task<string> Show(ApplicationController app,bool save,string fileName,string directory)
  {var view=new FilePickerView(app,save);view.Build(fileName,directory);return view.completion.Task;}
  void Build(string fileName,string directory)
  {
   current=directory??DocumentsDirectory();if(string.IsNullOrEmpty(current))current=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
   app.Modal.Show(save?"导出关卡包 · 选择位置":"导入关卡包 · 选择文件","",()=>{lifetime.Cancel();completion.TrySetResult(null);});
   var content=app.Modal.Body.transform.parent;app.Modal.Body.gameObject.SetActive(false);
   var addressLabel=UiFactory.Text("AddressLabel",content,"所在目录",app.theme,13,app.theme.secondary);UiFactory.Preferred(addressLabel.gameObject,22);
   address=WorkshopFields.Field(content,"DirectoryPath",current,app.theme,4096,38);address.onSubmit.AddListener(_=>Navigate(address.text));
   var navigation=UiFactory.Rect("DirectoryNavigation",content);UiFactory.Preferred(navigation.gameObject,34);var row=navigation.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();row.spacing=6;row.childControlWidth=true;row.childForceExpandWidth=true;
   Add(navigation,"前往此目录",()=>Navigate(address.text));
   Add(navigation,"上一级",()=>{try{Navigate(Directory.GetParent(current)?.FullName??current);}catch(Exception e){SetStatus(e.Message);}});
   Add(navigation,"文档目录",()=>Navigate(DocumentsDirectory()));
   if(Application.platform==RuntimePlatform.WindowsPlayer||Application.platform==RuntimePlatform.WindowsEditor)
    foreach(var drive in DriveInfo.GetDrives()){string path=drive.Name;Add(content,"磁盘 "+path,()=>Navigate(path));}
   var fileLabel=UiFactory.Text("FileLabel",content,"文件名或完整路径 · .sokopack.json",app.theme,13,app.theme.secondary);UiFactory.Preferred(fileLabel.gameObject,22);
   name=WorkshopFields.Field(content,"FileName",fileName??"",app.theme,4096,38);name.onValueChanged.AddListener(_=>confirming=false);
   status=UiFactory.Text("PickerStatus",content,"仅显示 .sokopack.json 文件",app.theme,13,app.theme.secondary);UiFactory.Preferred(status.gameObject,48);
   status.gameObject.AddComponent<FilePickerLifetime>().Closed=()=>{lifetime.Cancel();completion.TrySetResult(null);};
   entries=UiFactory.Vertical(content,"DirectoryEntries",4);
   var fitter=entries.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();fitter.verticalFit=UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
   app.Modal.AddAction(save?"保存到此处":"选择文件",Submit);Navigate(current);
  }
  static string DocumentsDirectory(){string home=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);string documents=Path.Combine(home,"Documents");return Directory.Exists(documents)?documents:Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);}
  void Add(Transform parent,string title,Action action){var b=UiFactory.Button(title,parent,title,app.theme,action);UiFactory.Preferred(b.gameObject,34);}
  async void Navigate(string path)
  {
   int request=++generation;confirming=false;
   try
   {
    path=Path.GetFullPath(path);current=path;address.SetTextWithoutNotify(path);WorkshopFields.Clear(entries);SetStatus("正在读取目录…");
    var listing=await new DirectoryListingService().ListAsync(path,lifetime.Token);
    if(completion.Task.IsCompleted||request!=generation)return;
    if(!confirming)SetStatus(listing.Error??(listing.Truncated?"目录较大，仅显示部分项目。可输入完整文件路径。":"选择文件，或在名称栏输入完整路径。"));
    foreach(var item in listing.Directories){string target=item;Add(entries,"目录  "+Path.GetFileName(item),()=>Navigate(target));}
    foreach(var item in listing.Files){string target=item;Add(entries,Path.GetFileName(item),()=>{name.SetTextWithoutNotify(target);confirming=false;});}
   }
   catch(OperationCanceledException){}
   catch(Exception e){if(!completion.Task.IsCompleted&&request==generation)SetStatus(e.Message);}
  }
  void SetStatus(string text){if(status!=null){status.text=text;UiFactory.Preferred(status.gameObject,Mathf.Max(48,status.GetPreferredValues(text,540,0).y+8));}}
  void Submit()
  {
   try
   {
    string path=Path.GetFullPath(Path.IsPathRooted(name.text)?name.text:Path.Combine(current,name.text));ImportExportService.RequireExtension(path);
    if(!Directory.Exists(Path.GetDirectoryName(path)))throw new DirectoryNotFoundException("目标目录不存在，请选择有效目录。");
    if(!save&&!File.Exists(path))throw new FileNotFoundException("文件不存在，请重新选择。");
    if(save&&File.Exists(path)&&!confirming){confirming=true;SetStatus("文件已经存在。再次点击“保存到此处”将覆盖该文件，并保留旧文件备份。");return;}
    completion.TrySetResult(path);app.Modal.Close();
   }
   catch(Exception e){SetStatus(e.Message);}
  }
 }
}
