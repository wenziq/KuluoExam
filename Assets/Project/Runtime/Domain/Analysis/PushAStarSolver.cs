using System;
using System.Collections.Generic;
using System.Threading;
using Sokoban.Core.Data;
using Sokoban.Core.Identity;
using Sokoban.Core.Rules;
using Sokoban.Core.Validation;
namespace Sokoban.Domain.Analysis
{
    public interface IAnalysisSolver
    {
        AnalysisResult Solve(LevelData root, AnalysisBudget budget, CancellationToken cancellationToken, Action<AnalysisProgress> progress);
    }
    public sealed class PushAStarSolver : IAnalysisSolver
    {
        public const string AlgorithmVersion = "push-astar-v1";
        sealed class Node
        {
            public SearchStateKey Key;
            public Node Parent;
            public PushStep Push;
            public int G, H;
            public long Serial;
        }
        sealed class OpenHeap
        {
            readonly List<Node> items = new List<Node>();
            public int Count => items.Count;
            static bool Before(Node a, Node b)
            {
                long f = (long)a.G + a.H, other = (long)b.G + b.H;
                return f != other ? f < other : a.H != b.H ? a.H < b.H : a.Serial < b.Serial;
            }
            public void Add(Node node)
            {
                int index = items.Count; items.Add(node);
                while (index > 0)
                {
                    int parent = (index - 1) / 2; if (!Before(node, items[parent])) break;
                    items[index] = items[parent]; index = parent;
                }
                items[index] = node;
            }
            public Node Pop()
            {
                var result = items[0]; var last = items[items.Count - 1]; items.RemoveAt(items.Count - 1);
                if (items.Count == 0) return result;
                int index = 0;
                while (index * 2 + 1 < items.Count)
                {
                    int child = index * 2 + 1;
                    if (child + 1 < items.Count && Before(items[child + 1], items[child])) child++;
                    if (!Before(items[child], last)) break;
                    items[index] = items[child]; index = child;
                }
                items[index] = last; return result;
            }
        }
        public AnalysisResult Solve(LevelData root, AnalysisBudget budget = null, CancellationToken cancellationToken = default, Action<AnalysisProgress> progress = null)
        {
            var tracker = new SearchBudget(budget ?? AnalysisBudget.Normal, cancellationToken);
            var result = new AnalysisResult(); long nextProgress = 0;
            AnalysisResult Finish(AnalysisOutcome outcome, AnalysisStopReason reason, string explanation)
            {
                result.Outcome = outcome; result.StopReason = reason; result.Explanation = explanation;
                result.ExpandedNodes = tracker.ExpandedNodes; result.ElapsedMilliseconds = tracker.ElapsedMilliseconds; result.EstimatedPeakBytes = tracker.PeakBytes;
                return result;
            }
            AnalysisResult Stopped() => Finish(AnalysisOutcome.Unknown, tracker.StopReason, "分析已停止；预算不足或取消不能证明无解。");
            try
            {
                if (!StructureValidator.Validate(root).IsValid) return Finish(AnalysisOutcome.Invalid, AnalysisStopReason.InvalidStructure, "关卡不满足必要结构。");
                result.LevelFingerprint = LevelFingerprint.Compute(root);
                if (!tracker.Check()) return Stopped();
                // Includes root/static tables, flood/matching scratch, reconstruction and replay working space.
                if (!tracker.TryReserve(2 * 1024 * 1024L + root.width * root.height * 16L * 4)) return Stopped();
                root = root.DeepCopy();
                var initial = new BoardState(root); var boxes = Reachability.Boxes(initial);
                var reverse = new ReversePushDistances(root, cancellationToken, tracker.ThrowIfStopped);
                tracker.ThrowIfStopped();
                foreach (int box in boxes) if (reverse.IsDead(box))
                {
                    result.ProblemCell = Coordinate.FromIndex(box, root.width);
                    return Finish(AnalysisOutcome.Unsolvable, AnalysisStopReason.StaticDeadlock, "箱子位于任何目标都无法反向到达的静态死格。");
                }
                int lower = reverse.MatchingLowerBound(boxes, cancellationToken, tracker.ThrowIfStopped);
                tracker.ThrowIfStopped();
                if (lower == MinimumCostMatching.Infinity) return Finish(AnalysisOutcome.Unsolvable, AnalysisStopReason.MatchingImpossible, "放宽约束后仍无法给所有箱子分配不同目标。");
                var reachable = Reachability.Find(root, boxes, initial.Player.ToIndex(root.width), cancellationToken, tracker.ThrowIfStopped);
                long nodeBytes = 384L + boxes.Length * 8;
                if (!tracker.TryReserve(nodeBytes)) return Stopped();
                var first = new Node { Key = new SearchStateKey(boxes, Reachability.Representative(reachable)), H = lower };
                var best = new Dictionary<SearchStateKey, Node> { [first.Key] = first }; var open = new OpenHeap(); open.Add(first); long serial = 0;
                var goals = new bool[root.width * root.height]; foreach (var goal in root.features) goals[goal.y * root.width + goal.x] = true;
                while (open.Count > 0)
                {
                    tracker.ThrowIfStopped(); var node = open.Pop();
                    if (!ReferenceEquals(best[node.Key], node)) continue;
                    bool won = true; for (int i = 0; i < node.Key.BoxCount; i++) if (!goals[node.Key.BoxAt(i)]) { won = false; break; }
                    if (won)
                    {
                        var pushes = new List<PushStep>(); for (var at = node; at.Parent != null; at = at.Parent) pushes.Add(at.Push);
                        pushes.Reverse(); var witness = SolutionReconstructor.Reconstruct(root, pushes, cancellationToken, tracker.ThrowIfStopped);
                        tracker.ThrowIfStopped(); result.Witness = witness; result.Optimality = AnalysisOptimality.PushOptimal;
                        return Finish(AnalysisOutcome.Solvable, AnalysisStopReason.SolutionFound, "A*已证明最少推动；完整操作序列已由共同规则重放。");
                    }
                    if (!tracker.TryExpand()) return Stopped();
                    if (tracker.ElapsedMilliseconds >= nextProgress)
                    {
                        progress?.Invoke(new AnalysisProgress { ExpandedNodes = tracker.ExpandedNodes, ElapsedMilliseconds = tracker.ElapsedMilliseconds, EstimatedBytes = tracker.EstimatedBytes });
                        nextProgress = tracker.ElapsedMilliseconds + 100;
                    }
                    boxes = node.Key.CopyBoxes(); reachable = Reachability.Find(root, boxes, node.Key.Region, cancellationToken, tracker.ThrowIfStopped);
                    foreach (int box in boxes) for (int d = 0; d < 4; d++)
                    {
                        tracker.ThrowIfStopped(); var direction = (Direction)d;
                        int support = Reachability.Neighbor(box, Reachability.Opposite(direction), root.width, root.height);
                        int target = Reachability.Neighbor(box, direction, root.width, root.height);
                        if (support < 0 || !reachable[support] || !Reachability.Floor(root, target) || Array.IndexOf(boxes, target) >= 0 || reverse.IsDead(target)) continue;
                        var nextBoxes = (int[])boxes.Clone(); nextBoxes[Array.IndexOf(nextBoxes, box)] = target;
                        var nextReachable = Reachability.Find(root, nextBoxes, box, cancellationToken, tracker.ThrowIfStopped);
                        var key = new SearchStateKey(nextBoxes, Reachability.Representative(nextReachable)); int g = node.G + 1;
                        if (best.TryGetValue(key, out var prior) && prior.G <= g) continue;
                        int h = reverse.MatchingLowerBound(nextBoxes, cancellationToken, tracker.ThrowIfStopped);
                        if (h == MinimumCostMatching.Infinity) continue;
                        // Charge every retained candidate, including replaced entries and stale open nodes/parent chains.
                        // Never discount a popped node while a descendant may still reference it.
                        if (!tracker.TryReserve(nodeBytes)) return Stopped();
                        var next = new Node { Key = key, Parent = node, Push = new PushStep(box, direction), G = g, H = h, Serial = ++serial };
                        best[key] = next; open.Add(next); // Better paths reopen states; old heap entries are skipped by reference.
                    }
                }
                tracker.ThrowIfStopped(); return Finish(AnalysisOutcome.Unsolvable, AnalysisStopReason.Exhausted, "完整搜索已耗尽；只使用可靠的静态死格与匹配剪枝。");
            }
            catch (SearchStoppedException) { return Stopped(); }
            catch (OperationCanceledException) { return Finish(AnalysisOutcome.Unknown, AnalysisStopReason.Cancelled, "分析已取消。"); }
            catch (Exception error) { return Finish(AnalysisOutcome.Unknown, AnalysisStopReason.Error, "分析内部验证失败：" + error.Message); }
        }
    }
}
