using NUnit.Framework;
using Sokoban.Core.Data;
using Sokoban.Core.Rules;
using Sokoban.Runtime.Presentation.Board;
using UnityEngine;
namespace Sokoban.PlayModeTests
{
    public sealed class GameplayInputTests
    {
        [Test] public void InterpolationMovesPlayerAndPushedBoxTogetherAndFinalRefreshCancelsOffsets()
        {
            var root = new GameObject("U7 interpolation", typeof(RectTransform));
            try
            {
                var level = AsciiLevelFactory.Create("######", "#    #", "# @$.#", "######");
                var before = new BoardState(level);
                var after = SokobanRules.TryMove(before, Direction.Right).State;
                var board = BoardView.Create(root.transform);
                board.Show(level);
                board.InterpolateEntities(before, after, .5f);
                string playerId = level.entities.Find(e => e.type == EntityType.Player).id;
                string boxId = level.entities.Find(e => e.type == EntityType.Box).id;
                var player = (RectTransform)board.entityLayer.Find(playerId);
                var box = (RectTransform)board.entityLayer.Find(boxId);
                Assert.That(player.anchorMin.x, Is.EqualTo(2.5f / 6).Within(.0001f));
                Assert.That(box.anchorMin.x, Is.EqualTo(3.5f / 6).Within(.0001f));
                Assert.That(before.Player.x, Is.EqualTo(2));
                Assert.That(after.Player.x, Is.EqualTo(3));
                board.RefreshEntities(after);
                Assert.That(player.anchorMin.x, Is.EqualTo(3f / 6).Within(.0001f));
                Assert.That(box.anchorMin.x, Is.EqualTo(4f / 6).Within(.0001f));
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
