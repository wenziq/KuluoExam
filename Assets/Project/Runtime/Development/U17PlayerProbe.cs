#if SOKOBAN_VERIFICATION_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Sokoban.Runtime.Presentation.App;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace Sokoban.Runtime.Development
{
 public sealed class U17PlayerProbe:MonoBehaviour
 {
  [Serializable]sealed class Receipt{public bool passed;public string phase,unity,dataRoot,error;public float volume;public bool sound,fullscreen;}
  Receipt receipt;float deadline;
  public static string DataRootOverride
  {get{
#if SOKOBAN_VERIFICATION_BUILD && !UNITY_EDITOR
   var a=Environment.GetCommandLineArgs();int i=Array.IndexOf(a,"--sokoban-u17-probe");if(i>=0&&i+1<a.Length&&Path.IsPathRooted(a[i+1]))return Path.GetFullPath(a[i+1]);
#endif
   return null;
  }}
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Begin(){if(DataRootOverride!=null)new GameObject("U17 settings verification").AddComponent<U17PlayerProbe>();}
  IEnumerator Start()
  {
   Application.runInBackground=true;receipt=new Receipt{phase=Environment.GetCommandLineArgs().Contains("--sokoban-u17-reload")?"reload":"save",unity=Application.unityVersion,dataRoot=DataRootOverride};Directory.CreateDirectory(receipt.dataRoot);deadline=Time.realtimeSinceStartup+30;var run=Run();
   while(true){object next;try{if(!run.MoveNext())break;next=run.Current;}catch(Exception e){receipt.error=e.ToString();Finish(false);yield break;}yield return next;}Finish(true);
  }
  IEnumerator Run()
  {
   GameApplicationCoordinator game;while((game=FindAnyObjectByType<GameApplicationCoordinator>())==null||!game.Initialized){Check();yield return null;}
   var settings=game.GetComponent<SettingsController>();
   if(receipt.phase=="save")
   {
    game.App.Navigate("Settings");game.App.modalLayer.GetComponentsInChildren<Button>().Single(b=>b.name=="SoundToggle").onClick.Invoke();game.App.modalLayer.GetComponentInChildren<Slider>().value=.61f;game.App.modalLayer.GetComponentsInChildren<Button>().Single(b=>b.name=="FullscreenToggle").onClick.Invoke();
    while(!settings.LastSave.IsCompleted){Check();yield return null;}Require(settings.LastSave.Result.Committed,"Settings write failed.");game.App.Modal.Close();
   }
   var value=settings.Current;receipt.volume=value.volume;receipt.sound=value.sound;receipt.fullscreen=value.fullscreen;Require(Mathf.Abs(value.volume-.61f)<.001f&&!value.sound&&value.fullscreen,"Preferences did not survive reload.");var audio=game.GetComponent<AudioSource>();Require(audio!=null&&audio.mute&&Mathf.Abs(audio.volume-.61f)<.001f,"Audio source settings differ from persisted values.");
  }
  void Check(){if(Time.realtimeSinceStartup>deadline)throw new TimeoutException("Settings verification timed out.");}
  static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
  void Finish(bool passed){receipt.passed=passed;File.WriteAllText(Path.Combine(receipt.dataRoot,"u17-"+receipt.phase+"-receipt.json"),JsonUtility.ToJson(receipt,true));Debug.Log("U17 Player "+(passed?"PASS":receipt.error));if(passed)Application.Quit(0);}
 }
}
#endif
