using System;
using System.Linq;
using System.Threading;
using Sokoban.Core.Data;
using Sokoban.Core.Rules;
namespace Sokoban.Domain.Analysis
{
    /// <summary>Relaxed reverse pushes: predecessor and its player support must both be floor.</summary>
    public sealed class ReversePushDistances
    {
        readonly int[][] distances;
        readonly bool[] dead;
        public int GoalCount => distances.Length;
        public ReversePushDistances(LevelData level, CancellationToken token = default, Action checkpoint = null)
        {
            int size = level.width * level.height;
            var goals = level.features.Select(g => g.y * level.width + g.x).OrderBy(p => p).ToArray();
            distances = new int[goals.Length][]; dead = new bool[size];
            for (int i = 0; i < size; i++) dead[i] = Reachability.Floor(level, i);
            var queue = new int[size];
            for (int goal = 0; goal < goals.Length; goal++)
            {
                var distance = new int[size]; for (int i = 0; i < size; i++) distance[i] = MinimumCostMatching.Infinity;
                distances[goal] = distance; int head = 0, tail = 0; queue[tail++] = goals[goal]; distance[goals[goal]] = 0;
                while (head < tail)
                {
                    checkpoint?.Invoke(); token.ThrowIfCancellationRequested(); int current = queue[head++]; dead[current] = false;
                    for (int d = 0; d < 4; d++)
                    {
                        int prior = Reachability.Neighbor(current, (Direction)d, level.width, level.height);
                        int support = Reachability.Neighbor(prior, (Direction)d, level.width, level.height);
                        if (!Reachability.Floor(level, prior) || !Reachability.Floor(level, support) || distance[prior] != MinimumCostMatching.Infinity) continue;
                        distance[prior] = distance[current] + 1; queue[tail++] = prior;
                    }
                }
            }
        }
        public int Distance(int goalIndex, int cell) => distances[goalIndex][cell];
        public bool IsDead(int cell) => cell >= 0 && cell < dead.Length && dead[cell];
        public int MatchingLowerBound(int[] boxes, CancellationToken token = default, Action checkpoint = null)
        {
            if (boxes.Length != GoalCount) return MinimumCostMatching.Infinity;
            var costs = new int[boxes.Length, GoalCount];
            for (int i = 0; i < boxes.Length; i++) for (int j = 0; j < GoalCount; j++) costs[i,j] = distances[j][boxes[i]];
            return MinimumCostMatching.Cost(costs, token, checkpoint);
        }
    }
}
