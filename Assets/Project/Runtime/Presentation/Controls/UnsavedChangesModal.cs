using System;
using Sokoban.Runtime.Presentation.App;
using UnityEngine;
namespace Sokoban.Runtime.Presentation.Controls
{
    public static class UnsavedChangesModal
    {
        public static void Show(ApplicationController app,Action save,Action discard,Action cancel,string error=null)
        {
            bool chosen=false;
            app.Modal.Show("保留当前修改？",(error==null?"":error+"\n\n")+"当前关卡集有未手动保存的修改。保存后离开，或明确丢弃本次修改。",()=>{if(!chosen)cancel();});
            RemoveDone(app);
            app.Modal.AddAction("保存后继续",()=>{chosen=true;save();});
            app.Modal.AddAction("不保存并丢弃",()=>{chosen=true;discard();},false);
            app.Modal.AddAction("取消",app.Modal.Close,false);
        }
        internal static void RemoveDone(ApplicationController app)
        {
            var done=app.Modal.Actions.Find("Done");if(done!=null){done.gameObject.SetActive(false);UnityEngine.Object.Destroy(done.gameObject);}
        }
    }
}
