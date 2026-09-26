using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Sokoban.Core.Data;
using Sokoban.Core.Identity;
using Sokoban.Core.Rules;
using Sokoban.Domain.Analysis;
using Sokoban.Runtime.Persistence;
using Sokoban.Runtime.Presentation.Analysis;
using Sokoban.Runtime.Presentation.App;
using Sokoban.Runtime.Presentation.Controls;
using Sokoban.Runtime.Presentation.Style;
using Sokoban.Runtime.Presentation.Workshop;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
namespace Sokoban.PlayModeTests
{
 public sealed class ContentProductionFlowTests
 {
  [Serializable]public sealed class Observation{public string name,fingerprint,path;public int pushes,moves,walks,switches,goalsLeft;}
  [Serializable]public sealed class ProductionReceipt{public string unity,utc,packId;public List<string> operations=new List<string>();public List<Observation> levels=new List<Observation>();}
  sealed class Blueprint{public string Name,Note;public string[] Rows;public int Pushes,Difficulty;}
  static Blueprint[] Designs()=>new[]{
   new Blueprint{Name="第一次推动",Note="把箱子推到圆形目标上。每次推动也算一步移动。",Pushes=1,Difficulty=1,Rows=new[]{"#######","#     #","# @$. #","#     #","#######"}},
   new Blueprint{Name="不能拉回",Note="推之前，想想下一次从哪里推动。走错了可以撤销。",Pushes=2,Difficulty=1,Rows=new[]{"########","#      #","#  $ . #","# @    #","#      #","########"}},
   new Blueprint{Name="学会绕行",Note="箱子只能向远离你的方向移动。找一条路，走到合适的一侧。",Pushes=2,Difficulty=2,Rows=new[]{"########","#      #","# ###  #","#  $@. #","# # #  #","#      #","########"}},
   new Blueprint{Name="临时让位",Note="目标上的箱子也能继续推。有时先让出通道，再把它送回。",Pushes=3,Difficulty=2,Rows=new[]{"#########","##     ##","## ### ##","#@*  $.##","##     ##","##  #  ##","#########"}},
   new Blueprint{Name="多箱协调",Note="窄通道里，箱子无法互相穿过。先想好哪个目标应该先到位。",Pushes=8,Difficulty=3,Rows=new[]{"#########","#   #####","#  $#####","#@ $  ..#","#   #####","#   #####","#########"}},
   new Blueprint{Name="综合挑战",Note="让出入口、绕到箱后、安排顺序，最后把临时移开的箱子送回。",Pushes=10,Difficulty=3,Rows=new[]{"#########","##  #####","## $#####","## $  ..#","#@*  ####","##  #####","#########"}}
  };
  static Observation Observe(LevelData level,WitnessData witness)
  {var m=ReferenceSolutionMetrics.Calculate(level,witness);return new Observation{name=level.name,fingerprint=m.LevelFingerprint,path=witness.moves,pushes=m.Pushes,moves=m.Moves,walks=m.Walks,switches=m.BoxSwitches,goalsLeft=m.GoalsLeft};}
  [Test]public void AuthoredDesignsProveTheIntendedMechanicsBeforeProduction()
  {
   foreach(var design in Designs())
   {
    var level=AsciiLevelFactory.Create(design.Rows);level.name=design.Name;var result=new PushAStarSolver().Solve(level,AnalysisBudget.Normal,default,null);Assert.That(result.Outcome,Is.EqualTo(AnalysisOutcome.Solvable),design.Name);var obs=Observe(level,result.Witness);Assert.That(obs.pushes,Is.EqualTo(design.Pushes),design.Name);
    if(design.Name=="临时让位"||design.Name=="综合挑战")Assert.That(obs.goalsLeft,Is.GreaterThan(0),design.Name);if(design.Name=="学会绕行")Assert.That(obs.walks,Is.GreaterThan(5));if(design.Name=="多箱协调")Assert.That(obs.switches,Is.GreaterThan(0));Debug.Log("U18 original design: "+JsonUtility.ToJson(obs));
   }
  }
  GameObject root;ApplicationController app;GameApplicationCoordinator game;WorkshopApplicationCoordinator workshop;WorkshopPersistenceController persistence;AnalysisMenuController analysis;ApplyPackModal apply;ImportPreviewModal exchange;string directory;uint oldW,oldH;ProductionReceipt receipt;
  IEnumerator Create(bool shipped=false)
  {
#if UNITY_EDITOR
   UnityEditor.PlayModeWindow.GetRenderingResolution(out oldW,out oldH);UnityEditor.PlayModeWindow.SetViewType(UnityEditor.PlayModeWindow.PlayModeViewTypes.GameView);UnityEditor.PlayModeWindow.SetCustomRenderingResolution(1280,720,"Content production");
#endif
   directory=Path.Combine(Path.GetTempPath(),"sokoban-u18-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);root=new GameObject("Content production");app=root.AddComponent<ApplicationController>();
#if UNITY_EDITOR
   app.theme=UnityEditor.AssetDatabase.LoadAssetAtPath<UiTheme>("Assets/Project/Settings/SokobanTheme.asset");
#endif
   app.Initialize();game=root.AddComponent<GameApplicationCoordinator>();game.Initialize(app,Path.Combine(directory,"data"),shipped?Path.Combine(Application.streamingAssetsPath,"BuiltInPacks"):Path.Combine(directory,"empty"));workshop=root.GetComponent<WorkshopApplicationCoordinator>();persistence=root.GetComponent<WorkshopPersistenceController>();analysis=root.GetComponent<AnalysisMenuController>();apply=root.GetComponent<ApplyPackModal>();exchange=root.GetComponent<ImportPreviewModal>();exchange.ConfigureDialogs(new Sokoban.Runtime.Platform.FileDialogs.InGameFileDialogService(app));receipt=new ProductionReceipt{unity=Application.unityVersion,utc=DateTime.UtcNow.ToString("O")};yield return null;
  }
  [UnityTearDown]public IEnumerator Cleanup()
  {
   var save=persistence?.LastSave;var recovery=persistence?.LastRecovery;
   if(root!=null)UnityEngine.Object.Destroy(root);yield return null;
   if(save!=null)while(!save.IsCompleted)yield return null;if(recovery!=null)while(!recovery.IsCompleted)yield return null;
   if(directory!=null&&Directory.Exists(directory))Directory.Delete(directory,true);
#if UNITY_EDITOR
   if(oldW>0)UnityEditor.PlayModeWindow.SetCustomRenderingResolution(oldW,oldH,"Previous");
#endif
  }
  Button Button(string name)=>root.GetComponentsInChildren<Button>().Single(b=>b.name==name);
  TMP_InputField Field(string name)=>root.GetComponentsInChildren<TMP_InputField>().Single(f=>f.name==name);
  void Click(string name){var button=Button(name);Assert.That(button.IsInteractable(),Is.True,name);button.onClick.Invoke();}
  void SetField(string name,string value){var field=Field(name);field.text=value;field.onEndEdit.Invoke(value);}
  void Paint(int tool,int x,int y)
  {
   Click("Tool"+tool);var board=workshop.View.Board;var r=board.boardRoot.rect;var world=board.boardRoot.TransformPoint(new Vector3(r.xMin+(x+.5f)*r.width/board.Width,r.yMin+(y+.5f)*r.height/board.Height,0));var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,world),button=PointerEventData.InputButton.Left};board.OnPointerDown(pointer);board.OnPointerUp(pointer);
  }
  IEnumerator Wait(System.Threading.Tasks.Task task,float seconds=20){float deadline=Time.realtimeSinceStartup+seconds;while(!task.IsCompleted){Assert.That(Time.realtimeSinceStartup,Is.LessThan(deadline));yield return null;}Assert.That(task.IsFaulted,Is.False,task.Exception?.ToString());yield return null;}
  static Direction DirectionOf(char c)=>c=='U'?Direction.Up:c=='D'?Direction.Down:c=='L'?Direction.Left:Direction.Right;
  [UnityTest]public IEnumerator ProduceSixLevelsWithActualWorkbenchThenTrialApplyExportAndPlay()
  {
   yield return Create();workshop.NewPack();yield return null;workshop.Operation("PackInfo");SetField("PackName","推箱子 · 六步入门");SetField("PackDescription","六个原创关卡，从第一次推动到安排箱子顺序。可在工坊创建副本，继续修改和分析。");Click("保存信息");
   foreach(var design in Designs())
   {
    Click("AddLevel");yield return null;SetField("LevelNameInput",design.Name);SetField("DesignNotes",design.Note);Field("WidthInput").text=design.Rows[0].Length.ToString();Field("HeightInput").text=design.Rows.Length.ToString();Click("ResizeLevel");if(app.Modal.IsOpen)Click("确认裁剪");yield return null;Canvas.ForceUpdateCanvases();
    int width=design.Rows[0].Length,height=design.Rows.Length;
    for(int y=0;y<height;y++)for(int x=0;x<width;x++)Paint(design.Rows[height-1-y][x]=='#'?1:0,x,y);
    for(int y=0;y<height;y++)for(int x=0;x<width;x++){char c=design.Rows[height-1-y][x];if(c=='.'||c=='*')Paint(4,x,y);if(c=='$'||c=='*')Paint(3,x,y);if(c=='@')Paint(2,x,y);}
    for(int i=0;i<design.Difficulty;i++)Click("IntendedDifficulty");
    var actual=workshop.Document.Snapshot().levels.Single(l=>l.levelId==workshop.Document.SelectedLevelId);Assert.That(LevelFingerprint.Compute(actual),Is.EqualTo(LevelFingerprint.Compute(AsciiLevelFactory.Create(design.Rows))),design.Name);receipt.operations.Add("UI新建/命名/备注/尺寸/地形与对象绘制/难度："+design.Name);yield return null;
   }
   // A real reversible edit proves that the displayed map is the editable source of the export.
   string original=workshop.Document.CurrentHash;Paint(1,2,1);workshop.Undo();Assert.That(workshop.Document.CurrentHash,Is.EqualTo(original));receipt.operations.Add("综合关卡实际画墙后撤销，恢复原布局");
   Click("SaveDraft");yield return Wait(persistence.LastSave);Assert.That(persistence.LastSave.Result.Committed,Is.True);workshop.Operation("AnalyzePack");yield return Wait(analysis.Production.LastTask,30);Assert.That(analysis.Production.Batch.Entries.All(e=>e.Result?.Outcome==AnalysisOutcome.Solvable),Is.True);app.Modal.Close();
   var authored=workshop.Document.Snapshot();receipt.packId=authored.packId;foreach(string id in authored.levelOrder){var level=authored.levels.Single(l=>l.levelId==id);receipt.levels.Add(Observe(level,authored.solutionWitnesses.Single(w=>w.levelId==id)));}receipt.operations.Add("完整草稿保存，批量分析六关全部有解");
   string output=Environment.GetEnvironmentVariable("SOKOBAN_U18_EVIDENCE_DIR");
#if UNITY_EDITOR
   if(!string.IsNullOrEmpty(output))yield return GameViewEvidence.Capture(Path.Combine(output,"workbench-six-levels.png"),1280,720);
#endif
   workshop.StartTrial(true);yield return null;
   for(int i=0;i<6;i++)
   {
    var trial=workshop.Trial;Assert.That(trial,Is.Not.Null);if(i==1){trial.RequestMove(Direction.Right);yield return new WaitForSecondsRealtime(.1f);trial.RequestMove(Direction.Up);yield return new WaitForSecondsRealtime(.1f);Assert.That(trial.Undo(),Is.True);Assert.That(trial.Undo(),Is.True);receipt.operations.Add("不能拉回：错误向上推动，再撤销两次恢复");}
    foreach(char c in receipt.levels[i].path){trial.RequestMove(DirectionOf(c));yield return new WaitForSecondsRealtime(.095f);}yield return new WaitForSecondsRealtime(.1f);Assert.That(trial.Session.IsCompleted,Is.True,receipt.levels[i].name);Assert.That(WitnessVerifier.Verify(authored.levels.Single(l=>l.levelId==authored.levelOrder[i]),trial.Session.CreateWitness()).IsValid,Is.True);receipt.operations.Add("实际试玩通关："+receipt.levels[i].name);if(i<5)Click("试玩下一关");else Click("返回编辑");yield return null;
   }
   Assert.That(game.Progress.Snapshot().records.Count,Is.Zero,"Trial must not write formal progress.");workshop.Operation("ApplyPack");yield return Wait(apply.LastApply,30);Assert.That(apply.LastApply.Result.Applied,Is.True);app.Modal.Close();receipt.operations.Add("应用整个可玩包，正式进度保持隔离");
   string exported=Path.Combine(directory,"六步入门.sokopack.json");workshop.Operation("ExportPlayable");yield return null;Field("FileName").text=exported;Click("保存到此处");yield return Wait(exchange.LastTask,30);var playable=StrictPackJson.Parse(File.ReadAllBytes(exported));ContentCatalog.ValidatePlayable(playable);Assert.That(playable.packId,Is.EqualTo(authored.packId));app.Modal.Close();receipt.operations.Add("实际文件窗口导出可玩包");
   game.ReloadCatalog();game.SelectPack(playable);game.StartLevel(playable.levelOrder[0]);yield return null;
   for(int i=0;i<6;i++){Assert.That(game.ActiveLevelId,Is.EqualTo(playable.levelOrder[i]));foreach(char c in receipt.levels[i].path){game.Gameplay.RequestMove(DirectionOf(c));yield return new WaitForSecondsRealtime(.095f);}yield return new WaitForSecondsRealtime(.1f);Assert.That(game.Gameplay.Session.IsCompleted,Is.True);yield return Wait(game.LastSave);if(i<5)Click("下一关 →");yield return null;}
   Assert.That(game.Progress.Snapshot().records.Count,Is.EqualTo(6));receipt.operations.Add("正式顺序解锁六关通关，保存六条真实成绩");
   if(!string.IsNullOrEmpty(output)){Directory.CreateDirectory(output);File.Copy(exported,Path.Combine(output,"tutorial.sokopack.json"),true);File.WriteAllText(Path.Combine(output,"production-receipt.json"),JsonUtility.ToJson(receipt,true));}
  }
  [UnityTest]public IEnumerator ShippedTutorialStartsFromMainMenuAndCompletesInDeclaredOrder()
  {
   yield return Create(true);Assert.That(game.Catalog.Entries.Count,Is.EqualTo(1));var pack=game.Catalog.Entries[0].Pack;Assert.That(pack.levels.Count,Is.EqualTo(6));Click("StartGame");yield return null;
   for(int i=0;i<6;i++)
   {
    Assert.That(game.ActiveLevelId,Is.EqualTo(pack.levelOrder[i]));var witness=pack.solutionWitnesses.Single(w=>w.levelId==pack.levelOrder[i]);foreach(char c in witness.moves){game.Gameplay.RequestMove(DirectionOf(c));yield return new WaitForSecondsRealtime(.095f);}yield return new WaitForSecondsRealtime(.1f);Assert.That(game.Gameplay.Session.IsCompleted,Is.True);yield return Wait(game.LastSave);if(i<5)Click("下一关 →");yield return null;
   }
   Assert.That(game.Progress.Snapshot().records.Count,Is.EqualTo(6));Assert.That(app.modalLayer.GetComponentsInChildren<TMP_Text>().Any(t=>t.text.Contains("关卡集完成")),Is.True);
  }

 }
}
