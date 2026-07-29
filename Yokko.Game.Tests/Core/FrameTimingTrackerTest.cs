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

    [Test]
    public void SnapshotReportsTailLatencyAndBudgetMisses()
    {
        var tracker = new FrameTimingTracker();
        for (int index = 0; index < 98; index++)
            tracker.Record(2);
        tracker.Record(4);
        tracker.Record(8);

        FrameTimingSnapshot snapshot = tracker.Snapshot(2);

        Assert.That(
            snapshot.P95FrameTimeMilliseconds,
            Is.EqualTo(2).Within(0.001));
        Assert.That(
            snapshot.P99FrameTimeMilliseconds,
            Is.GreaterThanOrEqualTo(4));
        Assert.That(snapshot.MaximumFrameTimeMilliseconds, Is.EqualTo(8));
        Assert.That(snapshot.BudgetMissCount, Is.EqualTo(2));
        Assert.That(snapshot.BudgetMissRatio, Is.EqualTo(0.02).Within(0.001));
    }

    [Test]
    public void SnapshotUsesApproximatelyTwoSecondWindow()
    {
        var tracker = new FrameTimingTracker();
        tracker.Record(50);
        for (int index = 0; index < 1000; index++)
            tracker.Record(2);

        FrameTimingSnapshot snapshot = tracker.Snapshot(2);

        Assert.That(snapshot.MaximumFrameTimeMilliseconds, Is.EqualTo(2));
        Assert.That(snapshot.BudgetMissCount, Is.Zero);
    }

    [Test]
    public void ResetDropsPreviousModeHistory()
    {
        var tracker = new FrameTimingTracker();
        tracker.Record(20);
        tracker.Reset();
        tracker.Record(2);

        FrameTimingSnapshot snapshot = tracker.Snapshot(2);

        Assert.That(snapshot.Count, Is.EqualTo(1));
        Assert.That(snapshot.MaximumFrameTimeMilliseconds, Is.EqualTo(2));
    }

    [Test]
    public void DisplayIgnoresSmallNumericNoise()
    {
        Assert.That(
            FrameTimingTracker.ShouldUpdateDisplay(2.1, 2.19),
            Is.False);
        Assert.That(
            FrameTimingTracker.ShouldUpdateDisplay(2.1, 2.31),
            Is.True);
        Assert.That(
            FrameTimingTracker.ShouldUpdateDisplay(16.7, 17.1),
            Is.False);
        Assert.That(
            FrameTimingTracker.ShouldUpdateDisplay(16.7, 17.6),
            Is.True);
    }

    [TestCase(478, 480)]
    [TestCase(482, 480)]
    [TestCase(239, 240)]
    [TestCase(121, 120)]
    [TestCase(60, 60)]
    public void HighFpsDisplayUsesStableBuckets(
        int framesPerSecond,
        int expected)
    {
        Assert.That(
            FrameTimingTracker.QuantizeFramesPerSecond(
                framesPerSecond),
            Is.EqualTo(expected));
    }
}
