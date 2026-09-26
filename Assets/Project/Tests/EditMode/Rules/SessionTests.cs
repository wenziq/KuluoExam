using System;
using NUnit.Framework;
using Sokoban.Core.Data;
using Sokoban.Core.Rules;
using Sokoban.Domain.Gameplay;
namespace Sokoban.Tests.EditMode.Rules
{
    public class SessionTests
    {
        [Test] public void CountsUndoRestartAndCompletionFactsFollowEffectivePath()
        {
            double now = 10;
            var session = new GameSession(SokobanRulesTests.Straight(), SessionMode.Formal, () => now);
            session.TryMove(Direction.Left);
            Assert.That(session.HistoryCount, Is.Zero);
            session.TryMove(Direction.Right);
            Assert.That(session.Moves, Is.EqualTo(1));
            Assert.That(session.Pushes, Is.Zero);
            session.TryMove(Direction.Right);
            Assert.That(session.Path, Is.EqualTo("RR"));
            Assert.That(session.Pushes, Is.EqualTo(1));
            Assert.That(session.IsCompleted, Is.True);
            Assert.That(session.Completions.Count, Is.EqualTo(1));
            Assert.That(session.Completions[0].Path, Is.EqualTo("RR"));
            Assert.That(session.TryMove(Direction.Up).Failure, Is.EqualTo(MoveFailure.SessionCompleted));
            now = 15;
            Assert.That(session.Undo(), Is.True);
            Assert.That(session.IsCompleted, Is.False);
            Assert.That(session.Path, Is.EqualTo("R"));
            Assert.That(session.Moves, Is.EqualTo(1));
            Assert.That(session.Pushes, Is.Zero);
            Assert.That(session.State.GetBoxPosition(0), Is.EqualTo(new Coordinate(3, 1)));
            session.TryMove(Direction.Right);
            Assert.That(session.Completions.Count, Is.EqualTo(2));
            Assert.That(session.Completions[0].Path, Is.EqualTo("RR"));
            Assert.That(session.Completions[1].Path, Is.EqualTo("RR"));
            session.Restart();
            Assert.That(session.Path, Is.Empty);
            Assert.That(session.Moves, Is.Zero);
            Assert.That(session.Pushes, Is.Zero);
            Assert.That(session.HistoryCount, Is.Zero);
            Assert.That(session.State.Player, Is.EqualTo(new Coordinate(1, 1)));
            Assert.That(session.Statistics.UndoCount, Is.EqualTo(1));
            Assert.That(session.Statistics.RestartCount, Is.EqualTo(1));
            Assert.That(session.Statistics.ElapsedSeconds, Is.EqualTo(5));
            Assert.That(session.Completions.Count, Is.EqualTo(2));
            session.TryMove(Direction.Right);
            session.TryMove(Direction.Right);
            Assert.That(session.Completions.Count, Is.EqualTo(3));
        }
        [Test] public void InitialWinCompletesOnceAndHasValidEmptyWitness()
        {
            var level = AsciiLevelFactory.Create("####", "#@ #", "# *#", "####");
            var session = new GameSession(level, SessionMode.Trial);
            Assert.That(session.IsCompleted, Is.True);
            Assert.That(session.Completions.Count, Is.EqualTo(1));
            Assert.That(session.Undo(), Is.False);
            Assert.That(session.TryMove(Direction.Right).Succeeded, Is.False);
            Assert.That(WitnessVerifier.Verify(level, session.CreateWitness()).IsValid, Is.True);
            session.Restart();
            Assert.That(session.Completions.Count, Is.EqualTo(2));
        }
        [Test] public void TakeoverRetainsRootPrefixAndUndoHistoryForRealWitness()
        {
            var level = SokobanRulesTests.Straight();
            var session = GameSession.FromReplayPrefix(level, "R");
            Assert.That(session.Mode, Is.EqualTo(SessionMode.AssistedReplayTakeover));
            Assert.That(session.Path, Is.EqualTo("R"));
            Assert.That(session.HistoryCount, Is.EqualTo(1));
            Assert.That(session.InitialState.Player, Is.EqualTo(new Coordinate(1, 1)));
            session.TryMove(Direction.Right);
            var verified = WitnessVerifier.Verify(level, session.CreateWitness());
            Assert.That(verified.IsValid, Is.True);
            Assert.That(verified.Moves, Is.EqualTo(2));
            Assert.That(verified.Pushes, Is.EqualTo(1));
            Assert.That(session.Undo(), Is.True);
            Assert.That(session.Undo(), Is.True);
            Assert.That(session.State.Player, Is.EqualTo(new Coordinate(1, 1)));
            Assert.Throws<ArgumentException>(() => GameSession.FromReplayPrefix(level, "L"));
            Assert.Throws<InvalidOperationException>(() => session.CreateWitness());
        }
    }
}
