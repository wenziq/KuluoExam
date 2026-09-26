using System.Threading;
using Sokoban.Core.Data;
using Sokoban.Core.Identity;
using Sokoban.Core.Rules;
using Sokoban.Core.Validation;
namespace Sokoban.Domain.Analysis
{
    public static class WitnessRecheckService
    {
        public static AnalysisResult Recheck(LevelData root,WitnessData oldWitness,CancellationToken token=default)
        {
            var result=new AnalysisResult{Outcome=AnalysisOutcome.Unknown,Explanation="旧路线不可用；这不能证明当前地图无解。"};
            if(!StructureValidator.Validate(root).IsValid){result.Outcome=AnalysisOutcome.Invalid;result.StopReason=AnalysisStopReason.InvalidStructure;return result;}
            result.LevelFingerprint=LevelFingerprint.Compute(root);if(oldWitness==null)return result;
            var candidate=oldWitness.DeepCopy();candidate.levelId=root.levelId;candidate.levelFingerprint=result.LevelFingerprint;
            var verified=WitnessVerifier.Verify(root,candidate,cancellationToken:token);
            token.ThrowIfCancellationRequested();
            if(!verified.IsValid)
            {
                result.Explanation+="\n"+verified.Reason;
                if(verified.FailedMoveIndex>=0){result.Explanation+=" · 第 "+(verified.FailedMoveIndex+1)+" 步";result.ProblemCell=verified.FinalState?.Player;}
                if(result.ProblemCell.HasValue)result.Explanation+=" · 玩家位置 ("+result.ProblemCell.Value.x+","+result.ProblemCell.Value.y+")";
                return result;
            }
            result.Outcome=AnalysisOutcome.Solvable;result.Optimality=AnalysisOptimality.NotProven;result.StopReason=AnalysisStopReason.SolutionFound;result.Witness=candidate;
            result.Explanation="旧路线在当前布局重放成功，只证明有解，未证明最优。";return result;
        }
    }
}
