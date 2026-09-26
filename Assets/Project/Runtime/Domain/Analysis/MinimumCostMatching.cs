using System;
using System.Threading;
namespace Sokoban.Domain.Analysis
{
    public static class MinimumCostMatching
    {
        // Maximum real distance <= 399 and maximum box count 16. Long potentials prevent overflow.
        public const int Infinity = 1000000;
        public static int Cost(int[,] costs, CancellationToken token = default, Action checkpoint = null)
        {
            if (costs == null) throw new ArgumentNullException(nameof(costs));
            int n = costs.GetLength(0);
            if (n != costs.GetLength(1) || n > 16) throw new ArgumentException("匹配矩阵必须为最多16阶方阵。", nameof(costs));
            for (int i = 0; i < n; i++) for (int j = 0; j < n; j++)
                if (costs[i,j] < 0 || costs[i,j] > Infinity) throw new ArgumentOutOfRangeException(nameof(costs));
            var u = new long[n+1]; var v = new long[n+1]; var assigned = new int[n+1]; var way = new int[n+1];
            var minimum = new long[n+1]; var used = new bool[n+1];
            for (int row = 1; row <= n; row++)
            {
                checkpoint?.Invoke(); token.ThrowIfCancellationRequested(); assigned[0] = row; int column = 0;
                Array.Clear(used, 0, used.Length); for (int i = 0; i <= n; i++) minimum[i] = long.MaxValue / 4;
                do
                {
                    used[column] = true; int currentRow = assigned[column], next = 0; long delta = long.MaxValue / 4;
                    for (int j = 1; j <= n; j++) if (!used[j])
                    {
                        long reduced = costs[currentRow-1,j-1] - u[currentRow] - v[j];
                        if (reduced < minimum[j]) { minimum[j] = reduced; way[j] = column; }
                        if (minimum[j] < delta) { delta = minimum[j]; next = j; }
                    }
                    for (int j = 0; j <= n; j++)
                        if (used[j]) { u[assigned[j]] += delta; v[j] -= delta; } else minimum[j] -= delta;
                    column = next;
                } while (assigned[column] != 0);
                do { int prior = way[column]; assigned[column] = assigned[prior]; column = prior; } while (column != 0);
            }
            long total = 0;
            for (int j = 1; j <= n; j++)
            {
                int cost = costs[assigned[j]-1,j-1]; if (cost >= Infinity) return Infinity;
                total += cost;
            }
            return (int)Math.Min(total, Infinity);
        }
    }
}
