using System;
using System.IO;
using System.Threading.Tasks;
using Sokoban.Runtime.Persistence;
using Sokoban.Runtime.Presentation.App;
using Sokoban.Runtime.Presentation.Controls;

namespace Sokoban.Runtime.Platform.FileDialogs
{
    public sealed class RuntimeFileDialogService : IFileDialogService
    {
        readonly ApplicationController app;
        readonly Func<bool,string,string,Task<string>> picker;
        string lastDirectory;
        bool choosing;
        public RuntimeFileDialogService(ApplicationController app,Func<bool,string,string,Task<string>> picker=null)
        {this.app=app;this.picker=picker??NativeDesktopDialogs.ShowAsync;}
        public Task<string> OpenAsync(string initialDirectory=null)=>Pick(false,null,initialDirectory);
        public Task<string> SaveAsync(string fileName,string initialDirectory=null)=>Pick(true,fileName,initialDirectory);
        async Task<string> Pick(bool save,string fileName,string directory)
        {
            if(choosing)return null;
            choosing=true;
            var oldCursor=UnityEngine.Cursor.lockState;bool oldVisible=UnityEngine.Cursor.visible;
            UnityEngine.Cursor.lockState=UnityEngine.CursorLockMode.None;UnityEngine.Cursor.visible=true;
            app.Modal.Show(save?"选择导出位置":"选择要导入的关卡包","请在系统文件窗口中选择文件或保存位置。");
            var marker=app.Modal.Body;
            try
            {
                // Assign the caller's task before a native modal starts its nested event loop.
                await Task.Yield();
                if(app==null)return null;
                directory=Directory.Exists(directory)?directory:Directory.Exists(lastDirectory)?lastDirectory:Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string path=await picker(save,fileName,directory);
                if(app==null||string.IsNullOrEmpty(path))return null;
                // Never alter a selected save path after the native overwrite confirmation.
                ImportExportService.RequireExtension(path);
                path=Path.GetFullPath(path);
                if(!save&&!File.Exists(path))throw new FileNotFoundException("所选文件已经不存在，请重新选择。",path);
                lastDirectory=Path.GetDirectoryName(path);
                return path;
            }
            catch(Exception e)
            {
                if(app!=null&&app.Modal.Body==marker)
                    app.Modal.Show("文件选择未完成",e.Message+"\n请重试，并使用 .sokopack.json 文件名。");
                return null;
            }
            finally
            {
                choosing=false;UnityEngine.Cursor.lockState=oldCursor;UnityEngine.Cursor.visible=oldVisible;
                if(app!=null&&app.Modal.Body==marker)app.Modal.Close();
            }
        }
    }
    // Explicit adapter retained for isolated legacy UI tests; desktop users receive native dialogs.
    public sealed class InGameFileDialogService : IFileDialogService
    {
        readonly ApplicationController app;
        public InGameFileDialogService(ApplicationController app){this.app=app;}
        public Task<string> OpenAsync(string initialDirectory=null)=>FilePickerView.Show(app,false,null,initialDirectory);
        public Task<string> SaveAsync(string fileName,string initialDirectory=null)=>FilePickerView.Show(app,true,fileName,initialDirectory);
    }
}
