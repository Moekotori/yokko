using NUnit.Framework;
using Yokko.Game.Presentation;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class FrameTimingGraphTrackerTest
{
    [Test]
    public void NormalHighFpsVariationDoesNotBecomeStutter()
    {
        var tracker = new FrameTimingGraphTracker();

        foreach (double frameTime in new[] { 1.9, 2.2, 2.0, 2.4 })
            tracker.Record(frameTime);

        FrameTimingGraphSnapshot snapshot =
            tracker.CompleteBucket(2.1);

        Assert.That(
            snapshot.FrameTimes,
            Has.All.LessThan(snapshot.StutterThresholdMilliseconds));
        Assert.That(
            snapshot.FrameTimes,
            Has.All.Matches<double>(frameTime =>
                !FrameTimingGraphTracker.IsStutter(
                    frameTime,
                    snapshot.StutterThresholdMilliseconds)));
    }

    [Test]
    public void OnlyGenuineHitchCrossesHighFpsThreshold()
    {
        double threshold =
            FrameTimingGraphTracker.StutterThreshold(2.1);

        Assert.That(threshold, Is.EqualTo(25));
        Assert.That(
            FrameTimingGraphTracker.IsStutter(12, threshold),
            Is.False);
        Assert.That(
            FrameTimingGraphTracker.IsStutter(25, threshold),
            Is.True);
    }

    [Test]
    public void SixtyFpsUsesBaselineRelativeThreshold()
    {
        double threshold =
            FrameTimingGraphTracker.StutterThreshold(1000.0 / 60);

        Assert.That(threshold, Is.EqualTo(1000.0 / 24).Within(0.001));
        Assert.That(
            FrameTimingGraphTracker.IsStutter(33, threshold),
            Is.False);
        Assert.That(
            FrameTimingGraphTracker.IsStutter(42, threshold),
            Is.True);
    }

    [Test]
    public void StableNoiseProducesOnlyTinyHeightChanges()
    {
        double threshold =
            FrameTimingGraphTracker.StutterThreshold(2.1);
        double low = FrameTimingGraphTracker.HeightRatio(2, threshold);
        double high = FrameTimingGraphTracker.HeightRatio(2.4, threshold);

        Assert.That(high - low, Is.LessThan(0.02));
    }

    [Test]
    public void BucketsAdvanceAtOneStableStep()
    {
        var tracker = new FrameTimingGraphTracker();
        tracker.Record(2);
        tracker.CompleteBucket(2);
        tracker.Record(30);

        FrameTimingGraphSnapshot snapshot =
            tracker.CompleteBucket(2);

        Assert.That(
            snapshot.FrameTimes,
            Is.EqualTo(new double[] { 2, 2, 2, 2, 30 }));
    }
}
