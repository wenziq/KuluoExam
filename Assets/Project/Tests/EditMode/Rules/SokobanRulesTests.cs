using System;
using NUnit.Framework;
using Sokoban.Core.Data;
using Sokoban.Core.Rules;
namespace Sokoban.Tests.EditMode.Rules
{
    public class SokobanRulesTests
    {
        internal static LevelData Straight() => AsciiLevelFactory.Create("######", "#    #", "#@ $.#", "######");
        [Test] public void WalkAndPushAreAtomicAndKeepStableBoxIdentity()
        {
            var initial = new BoardState(Straight());
            var walk = SokobanRules.TryMove(initial, Direction.Right);
            Assert.That(walk.Succeeded, Is.True);
            Assert.That(walk.Pushed, Is.False);
            Assert.That(walk.State.Player, Is.EqualTo(new Coordinate(2, 1)));
            var push = SokobanRules.TryMove(walk.State, Direction.Right);
            Assert.That(push.Pushed, Is.True);
            Assert.That(push.BoxId, Is.EqualTo(initial.GetBoxId(0)));
            Assert.That(push.State.GetBoxPosition(0), Is.EqualTo(new Coordinate(4, 1)));
            Assert.That(push.State.IsWon, Is.True);
            Assert.That(initial.Player, Is.EqualTo(new Coordinate(1, 1)));
            Assert.That(initial.GetBoxPosition(0), Is.EqualTo(new Coordinate(3, 1)));
        }
        [TestCase(Direction.Left)] [TestCase(Direction.Down)] [TestCase((Direction)99)]
        public void IllegalMoveReturnsUnchangedBoard(Direction direction)
        {
            var board = new BoardState(Straight());
            var result = SokobanRules.TryMove(board, direction);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.State, Is.SameAs(board));
            Assert.That(result.Pushed, Is.False);
        }
        [Test] public void OutsideAndDoublePushAreRejectedAndWalkingAwayNeverPulls()
        {
            var outside = new BoardState(AsciiLevelFactory.Create("    ", " $. ", "    ", "@   "));
            Assert.That(SokobanRules.TryMove(outside, Direction.Left).Failure, Is.EqualTo(MoveFailure.OutsideBoard));
            var boxes = new BoardState(AsciiLevelFactory.Create("######", "# .. #", "#@$$ #", "######"));
            Assert.That(SokobanRules.TryMove(boxes, Direction.Right).Succeeded, Is.False);
            var away = SokobanRules.TryMove(boxes, Direction.Up);
            Assert.That(away.State.GetBoxPosition(0), Is.EqualTo(new Coordinate(2, 1)));
        }
        [Test] public void GoalLayerPersistsWhenPlayerAndBoxLeaveIt()
        {
            var board = new BoardState(AsciiLevelFactory.Create("######", "#  $ #", "#@* .#", "######"));
            var push = SokobanRules.TryMove(board, Direction.Right);
            Assert.That(push.Succeeded, Is.True);
            Assert.That(push.State.IsGoal(push.State.Player), Is.True);
            Assert.That(push.State.GetBoxPosition(1), Is.EqualTo(new Coordinate(3, 1)));
            Assert.That(push.State.IsWon, Is.False);
            Assert.That(SokobanRules.TryMove(push.State, Direction.Left).State.IsGoal(new Coordinate(2, 1)), Is.True);
        }
        [TestCase(Direction.Up, 1, 2, 'U')]
        [TestCase(Direction.Down, 1, 0, 'D')]
        [TestCase(Direction.Left, 0, 1, 'L')]
        [TestCase(Direction.Right, 2, 1, 'R')]
        public void CardinalDirectionsUseBottomLeftCoordinates(Direction direction, int x, int y, char symbol)
        {
            var board = new BoardState(AsciiLevelFactory.Create("    ", " $. ", " @  ", "    "));
            var move = SokobanRules.TryMove(board, direction);
            Assert.That(move.Succeeded, Is.True);
            Assert.That(move.State.Player, Is.EqualTo(new Coordinate(x, y)));
            Assert.That(SokobanRules.ToCharacter(direction), Is.EqualTo(symbol));
            Assert.That(SokobanRules.TryParseDirection(symbol, out var parsed), Is.True);
            Assert.That(parsed, Is.EqualTo(direction));
        }
        [Test] public void PushIntoWallOrOutsideCannotPartiallyMovePlayerOrBox()
        {
            var wall = new BoardState(AsciiLevelFactory.Create("#####", "# . #", "#@$##", "#####"));
            var edge = new BoardState(AsciiLevelFactory.Create("    ", " .  ", "  @$", "    "));
            foreach (var board in new[] { wall, edge })
            {
                var result = SokobanRules.TryMove(board, Direction.Right);
                Assert.That(result.Failure, Is.EqualTo(MoveFailure.BoxBlocked));
                Assert.That(result.State, Is.SameAs(board));
            }
        }
        [Test] public void InputAndExportAreIsolatedAndMalformedStructureIsRejected()
        {
            var level = Straight();
            var board = new BoardState(level);
            level.entities.Clear();
            level.terrain[7] = 1;
            var exported = board.ToLevelData();
            exported.entities.Clear();
            exported.features.Clear();
            Assert.That(board.Player, Is.EqualTo(new Coordinate(1, 1)));
            Assert.That(board.BoxCount, Is.EqualTo(1));
            Assert.That(board.IsGoal(new Coordinate(4, 1)), Is.True);
            Assert.Throws<ArgumentException>(() => new BoardState(level));
        }
    }
}
