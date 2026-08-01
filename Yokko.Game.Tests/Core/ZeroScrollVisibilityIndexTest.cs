using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Yokko.Core.Beatmaps;
using Yokko.Core.Gameplay;
using Yokko.Core.Scoring;
using Yokko.Core.Timing;
using Yokko.Game.Screens.Gameplay;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class ZeroScrollVisibilityIndexTest
{
    [Test]
    public void DenseCollapsedChartKeepsOnlyFrontMostObjectPerLane()
    {
        const int tapCount = 4000;
        var hitObjects = new List<YokkoHitObject>
        {
            new(0, 0, 100_000, HitObjectKind.Hold),
        };
        hitObjects.AddRange(
            Enumerable.Range(0, tapCount)
                      .Select(index => new YokkoHitObject(
                          index % 4,
                          10_000 + index * 10,
                          null,
                          HitObjectKind.Tap)));
        var beatmap = new YokkoBeatmap(
            "Zero factor fixture",
            "Yokko",
            "Codex",
            "4K",
            KeyMode.FourKey,
            ChartSourceFormat.Quaver,
            [YokkoTimingPoint.Default],
            null,
            hitObjects);
        var state = new BeatmapJudgementState(beatmap);
        var index = new ZeroScrollVisibilityIndex(
            hitObjects.Select((hitObject, objectIndex) =>
                new ZeroScrollVisibilityIndex.Entry(
                    objectIndex,
                    hitObject.Lane,
                    hitObject.StartTimeMilliseconds,
                    hitObject.EndTimeMilliseconds)),
            4);
        var candidates = new List<int>();

        index.Collect(50_000, 2_000, state, candidates);

        Assert.Multiple(() =>
        {
            Assert.That(candidates, Has.Count.EqualTo(4));
            Assert.That(candidates, Does.Contain(0));
            Assert.That(
                candidates.Select(candidate => hitObjects[candidate].Lane),
                Is.Unique);
        });
    }
}
