using System.Collections.Generic;
using NUnit.Framework;
using Yokko.Core.Timing;
using Yokko.Game.Screens.Gameplay;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public class ScrollRangeIndexTest
{
    [Test]
    public void TestReturnsOnlyIntersectingRanges()
    {
        var index = new ScrollRangeIndex(
        [
            (0, new ScrollPositionRange(0, 10)),
            (1, new ScrollPositionRange(20, 40)),
            (2, new ScrollPositionRange(35, 60)),
            (3, new ScrollPositionRange(80, 70)),
        ]);
        var results = new List<int>();

        index.CollectOverlapping(38, 72, results);

        Assert.That(results, Is.EquivalentTo(new[] { 1, 2, 3 }));
    }

    [Test]
    public void TestDenseHoldQueryStaysNearVisibleSet()
    {
        const int totalHolds = 4000;
        var ranges =
            new (int Index, ScrollPositionRange Range)[totalHolds];

        for (int i = 0; i < ranges.Length; i++)
        {
            ranges[i] = (
                i,
                new ScrollPositionRange(i * 10, i * 10 + 30));
        }

        var index = new ScrollRangeIndex(ranges);
        var results = new List<int>();

        int visitedNodes =
            index.CollectOverlapping(20_000, 21_300, results);

        Assert.That(results, Has.Count.EqualTo(134));
        Assert.That(results, Does.Contain(1997));
        Assert.That(results, Does.Contain(2130));
        Assert.That(
            visitedNodes,
            Is.LessThan(400),
            "The query should prune the thousands of past and future holds.");
    }
}
