using NUnit.Framework;
using Yokko.Game.Presentation;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class FrameTimingTrackerTest
{
    [TestCase(2.0, 500)]
    [TestCase(1000.0 / 480, 480)]
    [TestCase(1000.0 / 120, 120)]
    [TestCase(1000.0 / 60, 60)]
    public void StableFrameTimeAndFpsUseOneSourceOfTruth(
        double frameTimeMilliseconds,
        int expectedFramesPerSecond)
    {
        var tracker = new FrameTimingTracker();
        for (int index = 0; index < 240; index++)
            tracker.Record(frameTimeMilliseconds);

        FrameTimingSnapshot snapshot = tracker.Snapshot();

        Assert.That(
            snapshot.FrameTimeMilliseconds,
            Is.EqualTo(frameTimeMilliseconds).Within(0.001));
        Assert.That(
            snapshot.FramesPerSecond,
            Is.EqualTo(expectedFramesPerSecond));
    }

    [Test]
    public void RecentSamplesKeepChronologicalNewestFive()
    {
        var tracker = new FrameTimingTracker();
        for (int index = 1; index <= 7; index++)
            tracker.Record(index);

        Assert.That(
            tracker.Snapshot().RecentFrameTimes,
            Is.EqualTo(new double[] { 3, 4, 5, 6, 7 }));
    }

    [Test]
    public void InvalidSamplesAreIgnored()
    {
        var tracker = new FrameTimingTracker();
        tracker.Record(0);
        tracker.Record(double.NaN);
        tracker.Record(double.PositiveInfinity);

        Assert.That(tracker.Snapshot().Count, Is.Zero);
    }
}
