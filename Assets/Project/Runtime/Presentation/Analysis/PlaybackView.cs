using System;
using Sokoban.Core.Data;
using Sokoban.Domain.Gameplay;
using Sokoban.Runtime.Presentation.App;
using Sokoban.Runtime.Presentation.Board;
using Sokoban.Runtime.Presentation.Style;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace Sokoban.Runtime.Presentation.Analysis
{
    public sealed class PlaybackView:MonoBehaviour
    {
        public SolutionPlaybackSession Session {get; private set;}
        public BoardView Board {get; private set;}
        ApplicationController app;TextMeshProUGUI status;Button previous,next,takeover;bool playing;float elapsed;
        public void Build(ApplicationController app,LevelData level,SolutionPlaybackSession session,Action back,Action<GameSession> takeOver)
        {
            this.app=app;Session=session;app.Input.SetContext(InputContext.Page);
            var root=(RectTransform)transform;
            var title=UiFactory.Text("PlaybackTitle",root,"参考解回放 · "+level.name,app.theme,26);UiFactory.Place(title.rectTransform,30,26,1000,48);
            var note=UiFactory.Text("PlaybackScope",root,"这是一条已验证参考解；其他解可能不同。接管后属于辅助试玩，不计正式成绩。",app.theme,14,app.theme.secondary);UiFactory.Place(note.rectTransform,30,80,1100,36);
            Board=BoardView.Create(root);UiFactory.Fill((RectTransform)Board.transform,30,145,30,160);Board.fitPadding=new Vector2(30,30);Board.maxCellSize=68;Board.Show(level);
            var controls=UiFactory.Rect("PlaybackControls",root);controls.anchorMin=new Vector2(0,0);controls.anchorMax=new Vector2(1,0);controls.pivot=new Vector2(.5f,0);controls.offsetMin=new Vector2(30,72);controls.offsetMax=new Vector2(-30,118);
            var layout=controls.gameObject.AddComponent<HorizontalLayoutGroup>();layout.spacing=10;layout.childControlWidth=true;layout.childForceExpandWidth=true;layout.childControlHeight=true;
            UiFactory.Button("BackToWorkshop",controls,"返回编辑",app.theme,back);
            previous=UiFactory.Button("PreviousPush",controls,"上一推",app.theme,()=>Seek(Session.PushIndex-1));
            UiFactory.Button("AutoPlayback",controls,"播放 / 暂停",app.theme,()=>{playing=!playing;elapsed=0;});
            next=UiFactory.Button("NextPush",controls,"下一推",app.theme,()=>Seek(Session.PushIndex+1));
            takeover=UiFactory.Button("TakeOver",controls,"从这里接管",app.theme,()=>takeOver(Session.CreateTakeover()),true);
            app.Tooltip(takeover,"保留初始地图、全部路径前缀与撤销历史；辅助试玩不写正式成绩");
            status=UiFactory.Text("PlaybackStatus",root,"",app.theme,15,app.theme.accent);status.rectTransform.anchorMin=new Vector2(0,0);status.rectTransform.anchorMax=new Vector2(1,0);status.rectTransform.pivot=new Vector2(.5f,0);status.rectTransform.offsetMin=new Vector2(30,20);status.rectTransform.offsetMax=new Vector2(-30,62);Refresh();
        }
        public void Seek(int push)
        {playing=false;Session.SeekPush(Mathf.Clamp(push,0,Session.TotalPushes));Refresh();}
        void Refresh()
        {
            Board.RefreshEntities(Session.State);previous.interactable=Session.PushIndex>0;next.interactable=Session.PushIndex<Session.TotalPushes;takeover.interactable=Session.CanTakeOver;
            status.text="第 "+Session.PushIndex+" / "+Session.TotalPushes+" 推 · 已走 "+Session.MoveOffset+" 步（含推动间行走）"+(Session.CanTakeOver?"":" · 此段包含通关后移动，请回到之前接管");
        }
        void Update()
        {
            if(!playing||app.Modal.IsOpen)return;elapsed+=Time.unscaledDeltaTime;if(elapsed<.6f)return;elapsed=0;
            if(Session.PushIndex>=Session.TotalPushes){playing=false;return;}Session.SeekPush(Session.PushIndex+1);Refresh();
        }
    }
}
