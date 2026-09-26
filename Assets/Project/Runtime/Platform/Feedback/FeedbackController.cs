using System;
using Sokoban.Runtime.Persistence;
using UnityEngine;
namespace Sokoban.Runtime.Platform.Feedback
{
 public enum FeedbackKind{Move,Push,Invalid,Goal,Undo,Win}
 public sealed class FeedbackController:MonoBehaviour
 {
  AudioSource source;AudioClip[] clips;GameSettings settings=new GameSettings();float lastInvalid=-10;
  public event Action<FeedbackKind> Emitted;
  public void Configure(GameSettings value)
  {
   settings=value.Copy();Ensure();source.volume=settings.volume;source.mute=!settings.sound;
  }
  void Ensure()
  {
   if(source!=null)return;source=gameObject.AddComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=0;source.priority=64;
   if(FindAnyObjectByType<AudioListener>()==null)gameObject.AddComponent<AudioListener>();
   clips=new AudioClip[6];float[] frequencies={220,145,95,660,330,440};float[] lengths={.045f,.085f,.055f,.15f,.09f,.45f};
   for(int i=0;i<clips.Length;i++)
   {
    int samples=Mathf.CeilToInt(lengths[i]*22050);var data=new float[samples];double phase=0;
    for(int n=0;n<samples;n++)
    {
     float time=n/22050f,progress=time/lengths[i];float frequency=frequencies[i];
     if(i==4)frequency*=1-progress*.4f;if(i==5)frequency*=progress<.33f?1:progress<.66f?1.25f:1.5f;
     phase+=2*Math.PI*frequency/22050;float envelope=Mathf.Min(1,time/.005f)*Mathf.Min(1,(lengths[i]-time)/.018f);
     data[n]=(float)Math.Sin(phase)*.18f*Mathf.Max(0,envelope);
    }
    clips[i]=AudioClip.Create("Original "+(FeedbackKind)i,samples,1,22050,false);clips[i].SetData(data,0);
   }
  }
  public bool Emit(FeedbackKind kind)
  {
   if(kind==FeedbackKind.Invalid){if(Time.unscaledTime-lastInvalid<.3f)return false;lastInvalid=Time.unscaledTime;}
   Ensure();Emitted?.Invoke(kind);if(settings.sound&&settings.volume>0)source.PlayOneShot(clips[(int)kind]);return true;
  }
  void OnDestroy(){if(clips!=null)foreach(var clip in clips)if(clip!=null)Destroy(clip);}
 }
}
