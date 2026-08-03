using NUnit.Framework;
using Yokko.Core.Analysis;
using Yokko.Core.Beatmaps;
using Yokko.Core.Gameplay;
using Yokko.Core.Timing;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class ManiaChartAnalysisTest
{
    [Test]
    public void ComputesPhysicalKpsAndPerLaneCounts()
    {
        var beatmap = new YokkoBeatmap(
            "Analysis",
            "Yokko",
            "Yokko",
            "4K",
            KeyMode.FourKey,
            ChartSourceFormat.Yokko,
            [YokkoTimingPoint.Default],
            null,
            [
                new YokkoHitObject(0, 0, null, HitObjectKind.Tap),
                new YokkoHitObject(0, 250, 700, HitObjectKind.Hold),
                new YokkoHitObject(1, 500, null, HitObjectKind.Tap),
                new YokkoHitObject(2, 750, null, HitObjectKind.Tap),
                new YokkoHitObject(3, 1000, null, HitObjectKind.Tap),
            ]);

        ManiaChartAnalysisResult result =
            ManiaChartAnalysis.Analyse(beatmap, 2);

        Assert.Multiple(() =>
        {
            Assert.That(result.NoteCount, Is.EqualTo(5));
            Assert.That(result.HoldCount, Is.EqualTo(1));
            Assert.That(result.HoldRatio, Is.EqualTo(0.2));
            Assert.That(result.AverageKps, Is.EqualTo(5));
            Assert.That(result.PeakKps, Is.EqualTo(5));
            Assert.That(result.LaneNoteCounts, Is.EqualTo(new[] { 2, 1, 1, 1 }));
            Assert.That(result.BusiestLane, Is.EqualTo(0));
            Assert.That(result.LaneImbalance, Is.EqualTo(0.6).Within(0.0001));
        });
    }
}
