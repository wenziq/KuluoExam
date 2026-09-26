using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Sokoban.Core.Data;
using Sokoban.Core.Identity;
using Sokoban.Core.Rules;
using Sokoban.Domain.Analysis;
using Sokoban.Runtime.Platform.Tasks;
namespace Sokoban.Runtime.Persistence
{
    /// <summary>Evidence comes from local drafts/cache or imported data; every route is replayed locally.</summary>
    public sealed class WitnessRepository
    {
        readonly IAnalysisSolver solver;
        public WitnessRepository(IAnalysisSolver solver=null){this.solver=solver??new PushAStarSolver();}
        public Task<AnalysisResult> ResolveAsync(LevelData root,IReadOnlyList<WitnessData> candidates,CancellationToken token)
        {
            var snapshot=root.DeepCopy();
            if(candidates==null||candidates.Count>ContentLimits.MaxLevels*3)throw new ArgumentException("证据候选数量无效。");
            var proofs=candidates.Select(w=>w?.DeepCopy()).ToArray();
            return AnalysisWorker.RunAsync(t=>
            {
                string fingerprint=LevelFingerprint.Compute(snapshot);
                foreach(var candidate in proofs)
                {
                    t.ThrowIfCancellationRequested();if(candidate==null||candidate.levelId!=snapshot.levelId||candidate.levelFingerprint!=fingerprint)continue;
                    var verified=WitnessVerifier.Verify(snapshot,candidate,cancellationToken:t);t.ThrowIfCancellationRequested();
                    if(verified.IsValid)return new AnalysisResult{Outcome=AnalysisOutcome.Solvable,Optimality=AnalysisOptimality.NotProven,StopReason=AnalysisStopReason.SolutionFound,LevelFingerprint=fingerprint,Witness=candidate.DeepCopy(),Explanation="当前通关证据已由共同规则重放。"};
                }
                AnalysisResult result;
                try{result=solver.Solve(snapshot,AnalysisBudget.Normal,t,null);}
                catch(OperationCanceledException){throw;}
                catch(Exception error){return Unknown(fingerprint,"分析失败："+error.Message);}
                t.ThrowIfCancellationRequested();
                if(result==null||result.LevelFingerprint!=fingerprint)return Unknown(fingerprint,"分析结果与应用快照不一致。");
                if(result.Outcome==AnalysisOutcome.Solvable)
                {
                    var verified=WitnessVerifier.Verify(snapshot,result.Witness,cancellationToken:t);t.ThrowIfCancellationRequested();
                    if(!verified.IsValid)return Unknown(fingerprint,"求解结果未通过共同规则重放："+verified.Reason);
                }
                return result.Copy();
            },token);
        }
        static AnalysisResult Unknown(string fingerprint,string message)=>new AnalysisResult{Outcome=AnalysisOutcome.Unknown,StopReason=AnalysisStopReason.Error,LevelFingerprint=fingerprint,Explanation=message};
    }
}
