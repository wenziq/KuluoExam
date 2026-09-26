using NUnit.Framework;
using Sokoban.Domain.Analysis;
namespace Sokoban.Tests.EditMode.Data
{
    public class AnalysisContractTests
    {
        [Test] public void UnassignedResultCannotDefaultToSolvableOrCurrent()
        {
            var result=new AnalysisResult();
            Assert.That(result.Outcome,Is.EqualTo(AnalysisOutcome.Unknown));
            Assert.That(result.Optimality,Is.EqualTo(AnalysisOptimality.NotApplicable));
            Assert.That(result.IsCurrent(null),Is.False);
            result.LevelFingerprint=new string('a',64);
            Assert.That(result.IsCurrent(new string('a',64)),Is.True);
            Assert.That(result.IsCurrent(new string('b',64)),Is.False);
        }
        [Test] public void ZeroBudgetIsValidButNegativeIsNot()
        {
            Assert.DoesNotThrow(()=>new AnalysisBudget(0,0,0));
            Assert.Throws<System.ArgumentOutOfRangeException>(()=>new AnalysisBudget(-1,1,1));
        }
        [Test] public void LateCallbacksMustMatchAllIdentityDimensions()
        {
            var current=new AnalysisRequestIdentity("r","d","hash","solve","root",1);
            Assert.That(current.Matches(new AnalysisRequestIdentity("r","d","hash","solve","root",1)),Is.True);
            Assert.That(current.Matches(new AnalysisRequestIdentity("old","d","hash","solve","root",1)),Is.False);
            Assert.That(current.Matches(new AnalysisRequestIdentity("r","d","hash","solve","root",2)),Is.False);
            Assert.That(current.Matches(new AnalysisRequestIdentity("r","d","hash","solve","other",1)),Is.False);
        }
    }
}
