using System;
using Sokoban.Core.Data;
namespace Sokoban.Core.Rules
{
    public static class SokobanRules
    {
        public static MoveResult TryMove(BoardState state, Direction direction)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            int dx = 0;
            int dy = 0;
            switch (direction)
            {
                case Direction.Up: dy = 1; break;
                case Direction.Down: dy = -1; break;
                case Direction.Left: dx = -1; break;
                case Direction.Right: dx = 1; break;
                default: return new MoveResult(state, MoveFailure.InvalidDirection);
            }
            var next = new Coordinate(state.Player.x + dx, state.Player.y + dy);
            if (!next.IsInside(state.Width, state.Height))
                return new MoveResult(state, MoveFailure.OutsideBoard);
            if (state.IsWall(next))
                return new MoveResult(state, MoveFailure.Wall);
            int box = state.FindBox(next);
            var destination = new Coordinate(next.x + dx, next.y + dy);
            if (box >= 0 && (state.IsWall(destination) || state.FindBox(destination) >= 0))
                return new MoveResult(state, MoveFailure.BoxBlocked);
            return new MoveResult(state.Move(next, box, destination), MoveFailure.None, box >= 0 ? state.GetBoxId(box) : null);
        }
        public static bool TryParseDirection(char value, out Direction direction)
        {
            switch (value)
            {
                case 'U': direction = Direction.Up; return true;
                case 'D': direction = Direction.Down; return true;
                case 'L': direction = Direction.Left; return true;
                case 'R': direction = Direction.Right; return true;
                default: direction = default; return false;
            }
        }
        public static char ToCharacter(Direction direction)
        {
            switch (direction)
            {
                case Direction.Up: return 'U';
                case Direction.Down: return 'D';
                case Direction.Left: return 'L';
                case Direction.Right: return 'R';
                default: throw new ArgumentOutOfRangeException(nameof(direction));
            }
        }
    }
}
