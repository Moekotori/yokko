using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Yokko.Audio;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class AudioClockCorrelationTest
{
    [Test]
    public void CorrelationMapsDifferentMonotonicFrequenciesPrecisely()
    {
        const long observationSeconds = 900_000;
        var correlation = new AudioClockCorrelation(
            48_000,
            96_000,
            48_000,
            observationSeconds * 10_000_000,
            10_000_000);
        long eventTimestamp = observationSeconds * 3_000_000
                              + 7_500;

        Assert.That(
            correlation.TryGetOutputTimeAtTimestamp(
                eventTimestamp,
                3_000_000,
                out double outputTime),
            Is.True);
        Assert.That(outputTime, Is.EqualTo(1002.5).Within(0.000001));
    }

    [Test]
    public void CorrelationDoesNotExtrapolateBeyondSubmittedAudio()
    {
        var correlation = new AudioClockCorrelation(
            48_000,
            48_240,
            48_000,
            100_000_000,
            10_000_000);

        Assert.That(
            correlation.TryGetOutputTimeAtTimestamp(
                100_200_000,
                10_000_000,
                out double outputTime),
            Is.True);
        Assert.That(outputTime, Is.EqualTo(1005).Within(0.000001));
    }

    [Test]
    public void RateTimelinePreservesEveryHistoricalSegment()
    {
        var timeline = new PlaybackRateTimeline();
        timeline.Reset(1000, 1);
        timeline.SetRate(100, 2);
        timeline.SetRate(200, 0.5);

        Assert.Multiple(() =>
        {
            Assert.That(timeline.Map(50), Is.EqualTo(1050));
            Assert.That(timeline.Map(150), Is.EqualTo(1200));
            Assert.That(timeline.Map(250), Is.EqualTo(1325));
        });
    }

    [TestCase(0.5)]
    [TestCase(2.083333)]
    [TestCase(8.333333)]
    [TestCase(33)]
    public void CorrelatedResultIsIndependentOfUpdateDelay(
        double updateDelayMilliseconds)
    {
        const long frequency = 10_000_000;
        const long eventTimestamp = 500_000_000;
        var correlation = new AudioClockCorrelation(
            48_000,
            96_000,
            48_000,
            eventTimestamp,
            frequency);
        long observationTimestamp = eventTimestamp
                                    + (long)Math.Round(
                                        updateDelayMilliseconds
                                        * frequency
                                        / 1000);

        Assert.That(
            correlation.TryGetOutputTimeAtTimestamp(
                eventTimestamp,
                frequency,
                out double eventOutputTime),
            Is.True);
        Assert.That(
            correlation.TryGetOutputTimeAtTimestamp(
                observationTimestamp,
                frequency,
                out double observedOutputTime),
            Is.True);
        Assert.That(eventOutputTime, Is.EqualTo(1000).Within(0.000001));
        Assert.That(
            observedOutputTime - eventOutputTime,
            Is.EqualTo(updateDelayMilliseconds).Within(0.0001));
    }

    [Test]
    public void EventBeforeRateChangeUsesHistoricalRate()
    {
        var timeline = new PlaybackRateTimeline();
        timeline.Reset(0, 1);
        timeline.SetRate(1000, 1.5);
        timeline.SetRate(1100, 0.75);

        var correlation = new AudioClockCorrelation(
            57_600,
            96_000,
            48_000,
            12_000_000,
            10_000_000);

        Assert.That(
            correlation.TryGetOutputTimeAtTimestamp(
                9_000_000,
                10_000_000,
                out double eventOutputTime),
            Is.True);
        Assert.That(eventOutputTime, Is.EqualTo(900).Within(0.000001));
        Assert.That(timeline.Map(eventOutputTime), Is.EqualTo(900));
    }

    [Test]
    public async Task RateTimelineSupportsConcurrentLockFreeReaders()
    {
        var timeline = new PlaybackRateTimeline();
        var failures = new ConcurrentQueue<string>();

        Task writer = Task.Run(() =>
        {
            for (int index = 1; index <= 10_000; index++)
                timeline.SetRate(index, 0.5 + index % 7 * 0.25);
        });
        Task[] readers = Enumerable.Range(0, 4)
            .Select(_ => Task.Run(() =>
            {
                while (!writer.IsCompleted)
                {
                    double mapped = timeline.Map(10_000);
                    double rate = timeline.PlaybackRate;
                    if (!double.IsFinite(mapped)
                        || !double.IsFinite(rate)
                        || mapped < 0
                        || rate <= 0)
                    {
                        failures.Enqueue($"mapped={mapped}, rate={rate}");
                        return;
                    }
                }
            }))
            .ToArray();

        await writer;
        await Task.WhenAll(readers);

        Assert.That(failures, Is.Empty);
        Assert.That(timeline.Map(10_000), Is.GreaterThan(0));
    }

    [Test]
    public void RateTimelineReadsDoNotAllocateAfterWarmup()
    {
        var timeline = new PlaybackRateTimeline();
        timeline.SetRate(100, 1.5);
        for (int index = 0; index < 1000; index++)
            _ = timeline.Map(index);

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < 10_000; index++)
        {
            _ = timeline.Map(index);
            _ = timeline.PlaybackRate;
        }

        Assert.That(
            GC.GetAllocatedBytesForCurrentThread() - before,
            Is.Zero);
    }
}
