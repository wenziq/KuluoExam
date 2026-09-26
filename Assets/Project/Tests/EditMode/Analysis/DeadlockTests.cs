using NUnit.Framework;
using Sokoban.Core.Data;
using Sokoban.Domain.Analysis;
namespace Sokoban.Tests.EditMode.Analysis
{
    public sealed class DeadlockTests
    {
        [Test] public void ReversePushRequiresBothPreviousBoxAndTwoStepSupportFloor()
        {
            var root=AsciiLevelFactory.Create("######","# .  #","# $@ #","#    #","######");
            var map=new ReversePushDistances(root);
            // Goal (2,3): box below (2,2) has support (2,1), but box at (2,1) has wall support.
            Assert.That(map.Distance(0,2+2*6),Is.EqualTo(1));
            Assert.That(map.Distance(0,2+1*6),Is.EqualTo(MinimumCostMatching.Infinity));
            Assert.That(map.IsDead(2+1*6),Is.True);
            Assert.That(map.IsDead(2+3*6),Is.False);
        }
        [Test] public void NonCornerWallStripCanBeDeadAndReverseReachableDoesNotProveSolvable()
        {
            var root=AsciiLevelFactory.Create("#######","#     #","# .$@ #","#     #","#######");
            var map=new ReversePushDistances(root);
            Assert.That(map.IsDead(3+3*7),Is.True,"top strip middle is not a geometric corner");
            Assert.That(map.IsDead(3+2*7),Is.False);
        }
    }
}
