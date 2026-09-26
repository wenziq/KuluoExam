using System;
using System.Collections.Generic;
using System.Threading;
using Sokoban.Core.Data;
using Sokoban.Core.Rules;
namespace Sokoban.Domain.Analysis
{
    public static class Reachability
    {
        // Direction enum order is a documented, deterministic U/D/L/R traversal.
        public static int Neighbor(int cell, Direction direction, int width, int height)
        {
            if (cell < 0 || cell >= width * height) return -1;
            int x = cell % width, y = cell / width;
            switch (direction)
            {
                case Direction.Up: y++; break;
                case Direction.Down: y--; break;
                case Direction.Left: x--; break;
                case Direction.Right: x++; break;
                default: return -1;
            }
            return x >= 0 && x < width && y >= 0 && y < height ? y * width + x : -1;
        }
        public static Direction Opposite(Direction direction) => (Direction)((int)direction ^ 1);
        public static bool Floor(LevelData level, int cell) => cell >= 0 && cell < level.terrain.Length && level.terrain[cell] == 0;
        public static bool[] Find(LevelData level, int[] boxes, int player, CancellationToken token = default, Action checkpoint = null)
        {
            int size = level.width * level.height; var reachable = new bool[size]; var blocked = new bool[size];
            foreach (int box in boxes) blocked[box] = true;
            if (!Floor(level, player) || blocked[player]) return reachable;
            var queue = new int[size]; int head = 0, tail = 0; queue[tail++] = player; reachable[player] = true;
            while (head < tail)
            {
                checkpoint?.Invoke(); token.ThrowIfCancellationRequested(); int cell = queue[head++];
                for (int d = 0; d < 4; d++)
                {
                    int next = Neighbor(cell, (Direction)d, level.width, level.height);
                    if (!Floor(level, next) || blocked[next] || reachable[next]) continue;
                    reachable[next] = true; queue[tail++] = next;
                }
            }
            return reachable;
        }
        public static int Representative(bool[] reachable)
        {
            for (int i = 0; i < reachable.Length; i++) if (reachable[i]) return i;
            throw new InvalidOperationException("玩家没有可达区域。");
        }
        public static int[] Boxes(BoardState state)
        {
            var boxes = new int[state.BoxCount];
            for (int i = 0; i < boxes.Length; i++) boxes[i] = state.GetBoxPosition(i).ToIndex(state.Width);
            return boxes;
        }
        public static string WalkPath(BoardState state, int target, CancellationToken token = default, Action checkpoint = null)
        {
            var level = state.ToLevelData(); int size = state.Width * state.Height, start = state.Player.ToIndex(state.Width);
            if (target < 0 || target >= size) throw new InvalidOperationException("步行目标越界。");
            var blocked = new bool[size]; foreach (int box in Boxes(state)) blocked[box] = true;
            var previous = new int[size]; for (int i = 0; i < size; i++) previous[i] = -1;
            var entered = new Direction[size]; var queue = new int[size]; int head = 0, tail = 0;
            queue[tail++] = start; previous[start] = start;
            while (head < tail && previous[target] < 0)
            {
                checkpoint?.Invoke(); token.ThrowIfCancellationRequested(); int cell = queue[head++];
                for (int d = 0; d < 4; d++)
                {
                    int next = Neighbor(cell, (Direction)d, state.Width, state.Height);
                    if (!Floor(level, next) || blocked[next] || previous[next] >= 0) continue;
                    previous[next] = cell; entered[next] = (Direction)d; queue[tail++] = next;
                }
            }
            if (previous[target] < 0) throw new InvalidOperationException("推动支撑位不可达。");
            var path = new List<char>();
            for (int at = target; at != start; at = previous[at]) path.Add(SokobanRules.ToCharacter(entered[at]));
            path.Reverse(); return new string(path.ToArray());
        }
    }
}
