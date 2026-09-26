#if SOKOBAN_VERIFICATION_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Sokoban.Core.Data;
using Sokoban.Runtime.Persistence;
using Sokoban.Runtime.Presentation.App;
using Sokoban.Runtime.Presentation.Workshop;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace Sokoban.Runtime.Development
{
    // Explicit development launch only. The first process ends without Unity quit callbacks
    // after confirmed recovery persistence, exercising the next process's recovery prompt.
    public sealed class U10PlayerProbe:MonoBehaviour
    {
        [Serializable] private sealed class Receipt
        { public bool passed;public string phase,unity,dataRoot,utc,error;public int manualLevels,recoveredLevels; }
        private Receipt receipt;private float deadline;
        public static string DataRootOverride
        {
            get
            {
#if SOKOBAN_VERIFICATION_BUILD && !UNITY_EDITOR
                var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"--sokoban-u10-probe");
                if(index>=0&&index+1<args.Length&&Path.IsPathRooted(args[index+1]))return Path.GetFullPath(args[index+1]);
#endif
                return null;
            }
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] private static void StartProbe()
        {if(DataRootOverride!=null)new GameObject("U10 development verification").AddComponent<U10PlayerProbe>();}
        private IEnumerator Start()
        {
            Application.runInBackground=true;
            receipt=new Receipt{phase=Environment.GetCommandLineArgs().Contains("--sokoban-u10-verify-load")?"reload":"edit",unity=Application.unityVersion,dataRoot=DataRootOverride,utc=DateTime.UtcNow.ToString("O")};
            Directory.CreateDirectory(receipt.dataRoot);deadline=Time.realtimeSinceStartup+60;var run=Run();
            while(true)
            {
                object next;try{if(!run.MoveNext())break;next=run.Current;}
                catch(Exception ex){receipt.error=ex.ToString();Finish(false);yield break;}
                yield return next;
            }
            Finish(true);
        }
        private IEnumerator Run()
        {
            GameApplicationCoordinator game;
            while((game=FindAnyObjectByType<GameApplicationCoordinator>())==null||!game.Initialized){CheckTime();yield return null;}
            var workshop=game.GetComponent<WorkshopApplicationCoordinator>();var persistence=game.GetComponent<WorkshopPersistenceController>();
            Require(workshop!=null&&persistence!=null,"Workshop persistence components missing.");
            string idFile=Path.Combine(receipt.dataRoot,"probe-pack-id.txt");
            if(receipt.phase=="reload")
            {
                string id=File.ReadAllText(idFile);
                while(!game.App.Modal.IsOpen){CheckTime();yield return null;}
                Click(game,"恢复制作内容");yield return null;
                Require(workshop.Document!=null&&workshop.Document.IsDirty,"Recovered draft must remain manually unsaved.");
                Require(workshop.Document.Snapshot().packId==id,"Recovery changed the document identity.");
                receipt.recoveredLevels=workshop.Document.Snapshot().levels.Count;
                receipt.manualLevels=persistence.Drafts.Load(id).Pack.levels.Count;
                Require(receipt.recoveredLevels==1&&receipt.manualLevels==0,"Recovery silently overwrote the manual baseline.");
                Require(workshop.Document.Snapshot().levels[0].name=="独立进程恢复验证","Unsaved level text did not survive restart.");
                Click(game,"SaveDraft");while(!persistence.LastSave.IsCompleted){CheckTime();yield return null;}
                Require(persistence.LastSave.Result.Committed&&!workshop.Document.IsDirty,"Saving recovered content did not advance the manual save point.");
                Require(persistence.Drafts.Load(id).Pack.levels.Count==1,"Recovered draft did not save to the real file.");
                Require(game.Progress.Snapshot().records.Count==0,"Draft workflow touched formal progress.");yield break;
            }
            Require(!game.App.Modal.IsOpen,"Unexpected startup dialog in fresh isolated root.");
            workshop.NewPack();yield return null;
            string packId=workshop.Document.Snapshot().packId;File.WriteAllText(idFile,packId);
            Click(game,"SaveDraft");while(!persistence.LastSave.IsCompleted){CheckTime();yield return null;}
            Require(persistence.LastSave.Result.Committed,"Initial empty draft save failed.");
            Click(game,"AddLevel");yield return null;
            var field=game.GetComponentsInChildren<TMP_InputField>().Single(f=>f.name=="LevelNameInput");field.text="独立进程恢复验证";field.onEndEdit.Invoke(field.text);
            yield return new WaitForSecondsRealtime(2.3f);
            while(!persistence.LastRecovery.IsCompleted){CheckTime();yield return null;}
            Require(persistence.LastRecovery.Result!=null&&persistence.LastRecovery.Result.Committed,"Idle recovery never committed.");
            receipt.manualLevels=persistence.Drafts.Load(packId).Pack.levels.Count;
            receipt.recoveredLevels=persistence.Recovery.Load(packId).Pack.levels.Count;
            Require(receipt.manualLevels==0&&receipt.recoveredLevels==1&&workshop.Document.IsDirty,"Recovery and manual state are not isolated.");
            Application.Quit(0);yield return null;
            Require(game.App.Modal.IsOpen&&workshop.Document.IsDirty,"Native quit did not protect the dirty draft.");
            Click(game,"取消");Require(!game.App.Modal.IsOpen,"Quit cancellation did not return to editing.");
        }
        private static void Click(GameApplicationCoordinator game,string name)
        {var button=game.GetComponentsInChildren<Button>().Single(b=>b.name==name);Require(button.IsInteractable(),"Blocked button: "+name);button.onClick.Invoke();}
        private static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        private void CheckTime(){if(Time.realtimeSinceStartup>deadline)throw new TimeoutException("U10 Player probe timed out.");}
        private void Finish(bool passed)
        {
            receipt.passed=passed;File.WriteAllText(Path.Combine(receipt.dataRoot,"player-"+receipt.phase+"-receipt.json"),JsonUtility.ToJson(receipt,true));
            Debug.Log("U10 independent Player: "+(passed?"PASS":receipt.error));
            // The host terminates the edit process after reading this complete receipt.
            // This deliberately bypasses Unity quit callbacks without relying on Mono shutdown.
            if(receipt.phase=="reload"&&passed)Application.Quit(0);
        }
    }
}
#endif
