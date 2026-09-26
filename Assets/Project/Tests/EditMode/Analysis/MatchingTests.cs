using System;
using NUnit.Framework;
using Sokoban.Domain.Analysis;
namespace Sokoban.Tests.EditMode.Analysis
{
    public sealed class MatchingTests
    {
        [Test] public void MatchingAssignsDistinctGoalsAndSafelyRejectsImpossibleMatrix()
        {
            const int inf=MinimumCostMatching.Infinity;
            Assert.That(MinimumCostMatching.Cost(new[,]{{1,inf},{2,inf}}),Is.EqualTo(inf));
            Assert.That(MinimumCostMatching.Cost(new[,]{{1,2},{1,10}}),Is.EqualTo(3));
            Assert.That(MinimumCostMatching.Cost(new int[0,0]),Is.Zero);
            var costs=new int[16,16];for(int i=0;i<16;i++)for(int j=0;j<16;j++)costs[i,j]=i==j?399:inf;
            Assert.That(MinimumCostMatching.Cost(costs),Is.EqualTo(6384));
        }
        [Test] public void HungarianMatchesIndependentPermutationEnumeration()
        {
            var random=new Random(271828);
            for(int n=1;n<=6;n++)for(int trial=0;trial<50;trial++)
            {
                var costs=new int[n,n];for(int i=0;i<n;i++)for(int j=0;j<n;j++)costs[i,j]=random.Next(4)==0?MinimumCostMatching.Infinity:random.Next(20);
                Assert.That(MinimumCostMatching.Cost(costs),Is.EqualTo(Brute(costs,0,0)),"n="+n+", trial="+trial);
            }
        }
        static int Brute(int[,] c,int row,int used)
        {
            if(row==c.GetLength(0))return 0;int best=MinimumCostMatching.Infinity;
            for(int col=0;col<c.GetLength(1);col++)if((used&(1<<col))==0)
                best=Math.Min(best,Math.Min(MinimumCostMatching.Infinity,c[row,col]+Brute(c,row+1,used|(1<<col))));
            return best;
        }
    }
}
