using System;
using NUnit.Framework;
using Sokoban.Core.Data;
using Sokoban.Core.Identity;
using Sokoban.Core.Rules;
using Sokoban.Domain.Gameplay;
namespace Sokoban.Tests.EditMode.Analysis
{
    public sealed class PlaybackTests
    {
        static WitnessData Witness(LevelData root,string moves)=>new WitnessData{levelId=root.levelId,levelFingerprint=LevelFingerprint.Compute(root),moves=moves};
        [Test] public void PushBoundariesIncludeWalkingAndTakeoverRetainsFullUndoPrefix()
        {
            var root=AsciiLevelFactory.Create(SolverOracleTests.GoalBox);const string path="RDRURULULD";
            var playback=new SolutionPlaybackSession(root,Witness(root,path));
            Assert.That(playback.TotalPushes,Is.EqualTo(5));Assert.That(playback.MoveOffset,Is.Zero);
            playback.SeekPush(2);Assert.That(playback.MoveOffset,Is.EqualTo(4));
            var expected=GameSession.FromReplayPrefix(root,path.Substring(0,4));
            Assert.That(playback.State.Player,Is.EqualTo(expected.State.Player));
            playback.SeekPush(1);Assert.That(playback.MoveOffset,Is.EqualTo(1));playback.SeekPush(3);
            var takeover=playback.CreateTakeover();Assert.That(takeover.Mode,Is.EqualTo(SessionMode.AssistedReplayTakeover));
            Assert.That(takeover.Path,Is.EqualTo(path.Substring(0,5)));Assert.That(takeover.HistoryCount,Is.EqualTo(5));
            Assert.That(takeover.Undo(),Is.True);Assert.That(takeover.Path,Is.EqualTo(path.Substring(0,4)));
            foreach(char c in path.Substring(4)){SokobanRules.TryParseDirection(c,out var direction);Assert.That(takeover.TryMove(direction).Succeeded,Is.True);}
            Assert.That(WitnessVerifier.Verify(root,takeover.CreateWitness()).IsValid,Is.True);
            Assert.That(takeover.Completions[0].Mode,Is.EqualTo(SessionMode.AssistedReplayTakeover));
            Assert.Throws<ArgumentOutOfRangeException>(()=>playback.SeekPush(6));
        }
        [Test] public void PostWinMovesRemainVisibleButCannotBeTakenOverAsAnOrdinarySession()
        {
            var root=AsciiLevelFactory.Create("######","#@$. #","#    #","######");
            var witness=Witness(root,"RDLUR");Assert.That(WitnessVerifier.Verify(root,witness).IsValid,Is.True);
            var playback=new SolutionPlaybackSession(root,witness);playback.SeekPush(playback.TotalPushes);
            Assert.That(playback.MoveOffset,Is.EqualTo(5));Assert.That(playback.CanTakeOver,Is.False);
            Assert.Throws<InvalidOperationException>(()=>playback.CreateTakeover());
        }
        [Test] public void EmptyCompletedRootAndInvalidEvidenceAreExplicit()
        {
            var root=AsciiLevelFactory.Create("#####","#@* #","#   #","#####");
            var playback=new SolutionPlaybackSession(root,Witness(root,""));Assert.That(playback.TotalPushes,Is.Zero);
            Assert.That(playback.CreateTakeover().IsCompleted,Is.True);
            Assert.Throws<ArgumentException>(()=>new SolutionPlaybackSession(root,Witness(root,"L")));
        }
    }
}
