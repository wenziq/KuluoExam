using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Sokoban.Core.Data;
using Sokoban.Core.Identity;
using Sokoban.Core.Rules;
using Sokoban.Core.Validation;
namespace Sokoban.Domain.Analysis
{
    /// <summary>Unpruned push BFS for tiny correctness oracles. No heuristic or deadlock filtering.</summary>
    public sealed class PushBfsSolver
    {
        sealed class Node
        {
            public SearchStateKey Key;
            public int Parent;
            public PushStep Push;
        }
        public AnalysisResult Solve(LevelData level, AnalysisBudget budget = null, CancellationToken cancellationToken = default)
        {
            budget = budget ?? AnalysisBudget.Normal; var clock = Stopwatch.StartNew(); var result = new AnalysisResult();
            long peak = 0;
            AnalysisResult Finish(AnalysisOutcome outcome, AnalysisStopReason reason, string explanation)
            {
                result.Outcome = outcome; result.StopReason = reason; result.Explanation = explanation;
                result.ElapsedMilliseconds = clock.ElapsedMilliseconds; result.EstimatedPeakBytes = peak; return result;
            }
            AnalysisStopReason Stop(long bytes)
            {
                peak = Math.Max(peak, bytes);
                if (cancellationToken.IsCancellationRequested) return AnalysisStopReason.Cancelled;
                if (clock.ElapsedMilliseconds >= budget.MaxElapsedMilliseconds) return AnalysisStopReason.TimeBudget;
                if (result.ExpandedNodes >= budget.MaxExpandedNodes) return AnalysisStopReason.NodeBudget;
                if (bytes > budget.MaxEstimatedBytes) return AnalysisStopReason.MemoryBudget;
                return AnalysisStopReason.None;
            }
            try
            {
                if (!StructureValidator.Validate(level).IsValid) return Finish(AnalysisOutcome.Invalid, AnalysisStopReason.InvalidStructure, "关卡不满足必要结构。");
                var root = level.DeepCopy(); result.LevelFingerprint = LevelFingerprint.Compute(root);
                var board = new BoardState(root); var boxes = Reachability.Boxes(board);
                // Reserve reconstruction strings, traversal scratch, collection spare capacity and per-node keys/parents.
                const long fixedBytes = 1024 * 1024;
                long bytesPerNode = 256L + boxes.Length * 8;
                var stop = Stop(fixedBytes + bytesPerNode); if (stop != AnalysisStopReason.None) return Finish(AnalysisOutcome.Unknown, stop, "分析已停止，尚未证明有解或无解。");
                var reachable = Reachability.Find(root, boxes, board.Player.ToIndex(root.width), cancellationToken);
                var start = new SearchStateKey(boxes, Reachability.Representative(reachable));
                var nodes = new List<Node> { new Node { Key = start, Parent = -1 } }; var visited = new HashSet<SearchStateKey> { start };
                var goals = new bool[root.width * root.height]; foreach (var goal in root.features) goals[goal.y * root.width + goal.x] = true;
                for (int head = 0; head < nodes.Count; head++)
                {
                    stop = Stop(fixedBytes + nodes.Count * bytesPerNode); if (stop != AnalysisStopReason.None) return Finish(AnalysisOutcome.Unknown, stop, "分析已停止，尚未证明有解或无解。");
                    var node = nodes[head]; bool won = true;
                    for (int i = 0; i < node.Key.BoxCount; i++) if (!goals[node.Key.BoxAt(i)]) { won = false; break; }
                    if (won)
                    {
                        var pushes = new List<PushStep>(); for (int at = head; nodes[at].Parent >= 0; at = nodes[at].Parent) pushes.Add(nodes[at].Push);
                        pushes.Reverse(); var witness = SolutionReconstructor.Reconstruct(root, pushes, cancellationToken);
                        stop = Stop(fixedBytes + nodes.Count * bytesPerNode); if (stop != AnalysisStopReason.None) return Finish(AnalysisOutcome.Unknown, stop, "验证期间达到预算，未发布结论。");
                        result.Witness = witness; result.Optimality = AnalysisOptimality.PushOptimal;
                        return Finish(AnalysisOutcome.Solvable, AnalysisStopReason.SolutionFound, "推动BFS完成，解法已由共同规则重放；推动次数最少。");
                    }
                    result.ExpandedNodes++; boxes = node.Key.CopyBoxes();
                    reachable = Reachability.Find(root, boxes, node.Key.Region, cancellationToken);
                    foreach (int box in boxes) for (int d = 0; d < 4; d++)
                    {
                        var direction = (Direction)d;
                        int support = Reachability.Neighbor(box, Reachability.Opposite(direction), root.width, root.height);
                        int target = Reachability.Neighbor(box, direction, root.width, root.height);
                        if (support < 0 || !reachable[support] || !Reachability.Floor(root, target) || Array.IndexOf(boxes, target) >= 0) continue;
                        // Node budget counts completed expansions, so it is checked at the next dequeue.
                        if (cancellationToken.IsCancellationRequested) return Finish(AnalysisOutcome.Unknown, AnalysisStopReason.Cancelled, "分析已取消。");
                        if (clock.ElapsedMilliseconds >= budget.MaxElapsedMilliseconds) return Finish(AnalysisOutcome.Unknown, AnalysisStopReason.TimeBudget, "时间预算用尽。");
                        long nextBytes = fixedBytes + (nodes.Count + 1L) * bytesPerNode; peak = Math.Max(peak, nextBytes);
                        if (nextBytes > budget.MaxEstimatedBytes) return Finish(AnalysisOutcome.Unknown, AnalysisStopReason.MemoryBudget, "内存预算用尽。");
                        var nextBoxes = (int[])boxes.Clone(); nextBoxes[Array.IndexOf(nextBoxes, box)] = target;
                        var nextReach = Reachability.Find(root, nextBoxes, box, cancellationToken);
                        var key = new SearchStateKey(nextBoxes, Reachability.Representative(nextReach));
                        if (visited.Add(key)) nodes.Add(new Node { Key = key, Parent = head, Push = new PushStep(box, direction) });
                    }
                }
                // Exhaustion is a proof even when the last permitted expansion completes the frontier.
                if (cancellationToken.IsCancellationRequested) return Finish(AnalysisOutcome.Unknown, AnalysisStopReason.Cancelled, "分析已取消。");
                if (clock.ElapsedMilliseconds >= budget.MaxElapsedMilliseconds) return Finish(AnalysisOutcome.Unknown, AnalysisStopReason.TimeBudget, "时间预算用尽。");
                return Finish(AnalysisOutcome.Unsolvable, AnalysisStopReason.Exhausted, "所有可达推动状态已完整搜索，没有解法。");
            }
            catch (OperationCanceledException) { return Finish(AnalysisOutcome.Unknown, AnalysisStopReason.Cancelled, "分析已取消。"); }
            catch (Exception error) { return Finish(AnalysisOutcome.Unknown, AnalysisStopReason.Error, "分析内部验证失败：" + error.Message); }
        }
    }
}
