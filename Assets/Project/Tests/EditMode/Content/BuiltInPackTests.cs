using System.IO;
using System;
using System.Threading.Tasks;
using Sokoban.Core.Identity;
using Sokoban.Domain.Workshop;
using Sokoban.Domain.Gameplay;
using System.Linq;
using NUnit.Framework;
using Sokoban.Core.Data;
using Sokoban.Core.Rules;
using Sokoban.Domain.Analysis;
using Sokoban.Runtime.Persistence;
using UnityEngine;
namespace Sokoban.Tests.EditMode.Content
{
 public sealed class BuiltInPackTests
 {
  [Test]public void ShippedTutorialContainsSixOriginalOrderedProvenLevels()
  {
   string path=Path.Combine(Application.streamingAssetsPath,"BuiltInPacks","tutorial.sokopack.json");Assert.That(File.Exists(path),Is.True,"The final workbench-produced tutorial is missing.");var pack=StrictPackJson.Parse(File.ReadAllBytes(path));ContentCatalog.ValidatePlayable(pack);
   Assert.That(pack.levels.Count,Is.EqualTo(6));Assert.That(pack.unlockPolicy,Is.EqualTo(UnlockPolicy.Sequential));Assert.That(pack.levelOrder.Select(id=>pack.levels.Single(l=>l.levelId==id).name),Is.EqualTo(new[]{"第一次推动","不能拉回","学会绕行","临时让位","多箱协调","综合挑战"}));
   int[] pushes={1,2,2,3,8,10};
   for(int i=0;i<6;i++)
   {
    var level=pack.levels.Single(l=>l.levelId==pack.levelOrder[i]);Assert.That(new BoardState(level).IsWon,Is.False);var solved=new PushAStarSolver().Solve(level,AnalysisBudget.Normal,default,null);Assert.That(solved.Outcome,Is.EqualTo(AnalysisOutcome.Solvable),level.name);var replay=WitnessVerifier.Verify(level,solved.Witness);Assert.That(replay.IsValid,Is.True);Assert.That(replay.Pushes,Is.EqualTo(pushes[i]),level.name);
    var metrics=ReferenceSolutionMetrics.Calculate(level,solved.Witness);if(i==3||i==5)Assert.That(metrics.GoalsLeft,Is.GreaterThan(0),level.name+" must move a goal box away temporarily.");if(i==2)Assert.That(metrics.Walks,Is.GreaterThan(5),"绕行 requires meaningful walking.");if(i==4)Assert.That(metrics.BoxSwitches,Is.GreaterThan(0),"多箱协调 must switch boxes.");
   }
  }
  [Test]public async Task WorkbenchRevisionReexportsAndInstallsTheSameNewLayoutAndIdentity()
  {
   var original=StrictPackJson.Parse(File.ReadAllBytes(Path.Combine(Application.streamingAssetsPath,"BuiltInPacks","tutorial.sokopack.json")));var copy=ContentIdentity.CreateIndependentCopy(original);copy.documentKind=DocumentKind.DraftPack;var document=new WorkshopDocument(copy);string id=copy.levelOrder[0],before=LevelFingerprint.Compute(copy.levels.Single(l=>l.levelId==id));
   string root=Path.Combine(Path.GetTempPath(),"sokoban-u18-revision-"+Guid.NewGuid().ToString("N"));var paths=new UserDataPaths(root);
   try
   {
    var apply=new ApplyPackService(paths,reservedIds:new[]{original.packId});Assert.That((await apply.ApplyAsync(copy)).Applied,Is.True);document.SelectLevel(id);using(var stroke=document.BeginStroke(PaintTool.Wall)){stroke.AddPoint(1,1);stroke.Commit();}
    var changed=document.Snapshot();string after=LevelFingerprint.Compute(changed.levels.Single(l=>l.levelId==id));Assert.That(after,Is.Not.EqualTo(before));var applied=await apply.ApplyAsync(changed);Assert.That(applied.Applied,Is.True);Assert.That(applied.InstalledPack.contentRevision,Is.EqualTo(2));
    string file=Path.Combine(root,"修改后重新导出.sokopack.json");var exchange=new ImportExportService(paths);Assert.That((await exchange.ExportAsync(applied.InstalledPack,file,true,false)).Committed,Is.True);
    var recipient=new UserDataPaths(Path.Combine(root,"recipient"));var imported=await new ImportExportService(recipient).ImportAsync(await exchange.ReadAsync(file),replace:true);Assert.That(imported.Committed,Is.True,imported.Error);Assert.That(imported.Pack.packId,Is.EqualTo(changed.packId));Assert.That(imported.Pack.levelOrder,Is.EqualTo(changed.levelOrder));Assert.That(LevelFingerprint.Compute(imported.Pack.levels.Single(l=>l.levelId==id)),Is.EqualTo(after));ContentCatalog.ValidatePlayable(imported.Pack);
    Assert.That(LevelFingerprint.Compute(original.levels[0]),Is.EqualTo(before),"Editing a copy must preserve the bundled original.");
   }
   finally{if(Directory.Exists(root))Directory.Delete(root,true);}
  }

  [Test]public void WrongDirectionAndWrongTunnelOrderReallyCreateTheAdvertisedProblems()
  {
   var pack=StrictPackJson.Parse(File.ReadAllBytes(Path.Combine(Application.streamingAssetsPath,"BuiltInPacks","tutorial.sokopack.json")));
   var second=new GameSession(pack.levels.Single(l=>l.name=="不能拉回"),SessionMode.Trial);Assert.That(second.TryMove(Direction.Right).Succeeded,Is.True);Assert.That(second.TryMove(Direction.Up).Pushed,Is.True);
   Assert.That(new PushAStarSolver().Solve(second.State.ToLevelData(),AnalysisBudget.Normal,default,null).Outcome,Is.EqualTo(AnalysisOutcome.Unsolvable));Assert.That(second.Undo(),Is.True);Assert.That(second.Undo(),Is.True);Assert.That(second.Moves,Is.Zero);
   var fifth=new GameSession(pack.levels.Single(l=>l.name=="多箱协调"),SessionMode.Trial);
   foreach(char c in "RRRRLLLUURDLDRR"){var d=c=='U'?Direction.Up:c=='D'?Direction.Down:c=='L'?Direction.Left:Direction.Right;Assert.That(fifth.TryMove(d).Succeeded,Is.True);}
   Assert.That(fifth.IsCompleted,Is.False);Assert.That(new PushAStarSolver().Solve(fifth.State.ToLevelData(),AnalysisBudget.Normal,default,null).Outcome,Is.EqualTo(AnalysisOutcome.Unsolvable));
  }

 }
}
