using System;
using Sokoban.Runtime.Persistence;
using Sokoban.Runtime.Presentation.App;
namespace Sokoban.Runtime.Presentation.Controls
{
    public static class RecoveryModal
    {
        public static void Show(ApplicationController app,RecoverySnapshot snapshot,bool hasManual,Action restore,Action manual)
        {
            app.Modal.Show("发现恢复草稿",snapshot.Pack.name+"\n恢复时间："+snapshot.SavedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")+"\n\n恢复副本与手动保存内容不同。请按名称和时间选择要打开的版本；恢复后仍需手动保存。");
            UnsavedChangesModal.RemoveDone(app);
            app.Modal.AddAction("恢复制作内容",restore);
            var openManual=app.Modal.AddAction("打开手动版本",manual,false);openManual.interactable=hasManual;
            if(!hasManual)app.Tooltip(openManual,"这份草稿还没有可读取的手动保存版本");
            app.Modal.AddAction("取消",app.Modal.Close,false);
        }
    }
}
