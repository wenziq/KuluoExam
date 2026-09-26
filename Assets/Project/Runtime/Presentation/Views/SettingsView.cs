using System;
using Sokoban.Runtime.Presentation.App;
using Sokoban.Runtime.Presentation.Style;
using UnityEngine;
using UnityEngine.UI;
namespace Sokoban.Runtime.Presentation.Views
{
 public static class SettingsView
 {
  public static void Show(ApplicationController app,SettingsController controller)
  {
   Action refresh=null;app.Modal.Show("设置","",()=>{if(refresh!=null)controller.Changed-=refresh;});app.Modal.Body.gameObject.SetActive(false);var content=app.Modal.Body.transform.parent;
   var heading=UiFactory.Text("SoundHeading",content,"操作音效",app.theme,20);UiFactory.Preferred(heading.gameObject,34);
   var note=UiFactory.Text("SoundNote",content,"轻量反馈，区分移动、推动、受阻、到位、撤销与通关。",app.theme,14,app.theme.secondary);UiFactory.Preferred(note.gameObject,28);
   var sound=UiFactory.Button("SoundToggle",content,"",app.theme,()=>{var s=controller.Current;s.sound=!s.sound;controller.Set(s);});UiFactory.Preferred(sound.gameObject,36);
   var volume=UiFactory.Text("VolumeLabel",content,"",app.theme,15);UiFactory.Preferred(volume.gameObject,24);
   var sliderRoot=UiFactory.Rect("VolumeSlider",content);UiFactory.Preferred(sliderRoot.gameObject,28);var hit=sliderRoot.gameObject.AddComponent<Image>();hit.color=Color.clear;var slider=sliderRoot.gameObject.AddComponent<Slider>();slider.minValue=0;slider.maxValue=1;
   var track=UiFactory.Panel("Track",sliderRoot,app.theme.border);UiFactory.Fill(track.rectTransform,0,12,0,12);
   var fill=UiFactory.Panel("Fill",sliderRoot,app.theme.accent);UiFactory.Fill(fill.rectTransform,0,12,0,12);slider.fillRect=fill.rectTransform;
   var handleArea=UiFactory.Rect("HandleArea",sliderRoot);UiFactory.Fill(handleArea,10,0,10,0);var handle=UiFactory.Panel("Handle",handleArea,app.theme.accent,true);handle.rectTransform.anchorMin=handle.rectTransform.anchorMax=new Vector2(0,.5f);handle.rectTransform.sizeDelta=new Vector2(18,0);slider.handleRect=handle.rectTransform;slider.targetGraphic=handle;
   slider.SetValueWithoutNotify(controller.Current.volume);slider.onValueChanged.AddListener(v=>{var s=controller.Current;s.volume=v;controller.Set(s);});
   var display=UiFactory.Text("DisplayHeading",content,"显示模式",app.theme,20);UiFactory.Preferred(display.gameObject,34);
   var fullscreen=UiFactory.Button("FullscreenToggle",content,"",app.theme,()=>{var s=controller.Current;s.fullscreen=!s.fullscreen;controller.Set(s);});UiFactory.Preferred(fullscreen.gameObject,36);
   var help=UiFactory.Button("SettingsHelp",content,"查看规则和快捷键",app.theme,()=>controller.Show("Help"));UiFactory.Preferred(help.gameObject,36);
   var status=UiFactory.Text("SettingsStatus",content,controller.Status,app.theme,13,app.theme.secondary);UiFactory.Preferred(status.gameObject,32);
   refresh=()=>{if(status==null)return;var s=controller.Current;sound.GetComponentInChildren<TMPro.TMP_Text>().text=s.sound?"音效已开启 · 点击关闭":"音效已关闭 · 点击开启";volume.text="主音量  "+Mathf.RoundToInt(s.volume*100)+"%";fullscreen.GetComponentInChildren<TMPro.TMP_Text>().text=s.fullscreen?"全屏 · 切换为窗口":"窗口 · 切换为全屏";status.text=controller.Status;UiFactory.Preferred(status.gameObject,Mathf.Max(32,status.GetPreferredValues(status.text,550,0).y+6));};controller.Changed+=refresh;refresh();
  }
 }
}
