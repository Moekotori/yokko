using System;
using NUnit.Framework;
using osuTK.Input;
using Yokko.Core.Beatmaps;
using Yokko.Core.Gameplay;
using Yokko.Core.Scoring;
using Yokko.Core.Timing;
using Yokko.Game.Gameplay;
using Yokko.Game.Input;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class GameplayInputClockTest
{
    [Test]
    public void EventAgeIsRemovedFromObservedGameplayTime()
    {
        double eventTime = GameplayInputClock.AtEventTimestamp(
            1200,
            10_000_000,
            10_025_000,
            10_000_000);

        Assert.That(eventTime, Is.EqualTo(1197.5).Within(0.0001));
    }

    [Test]
    public void EventBeforeSongStartRemainsNegative()
    {
        double eventTime = GameplayInputClock.AtEventTimestamp(
            1,
            10_000_000,
            10_100_000,
            10_000_000);

        Assert.That(eventTime, Is.EqualTo(-9).Within(0.0001));
    }

    [Test]
    public void MissingOrFutureTimestampUsesObservedTime()
    {
        Assert.That(
            GameplayInputClock.AtEventTimestamp(250, 0, 10, 1000),
            Is.EqualTo(250));
        Assert.That(
            GameplayInputClock.AtEventTimestamp(250, 11, 10, 1000),
            Is.EqualTo(250));
    }

    [Test]
    public void TimestampSourceCapturesEachPhysicalEdgeOnce()
    {
        using var source = new KeyInputTimestampSource();
        source.BeginCapture();

        source.Record(Key.D, true, 100);
        source.Record(Key.D, true, 110);
        source.Record(Key.D, false, 120);

        Assert.That(source.TryTake(Key.D, true, out long pressed), Is.True);
        Assert.That(pressed, Is.EqualTo(100));
        Assert.That(source.TryTake(Key.D, true, out _), Is.False);
        Assert.That(source.TryTake(Key.D, false, out long released), Is.True);
        Assert.That(released, Is.EqualTo(120));
    }

    [Test]
    public void TimestampSourceDoesNotLeakEdgesAcrossGameplaySessions()
    {
        using var source = new KeyInputTimestampSource();
        source.BeginCapture();
        source.Record(Key.F, true, 100);

        source.EndCapture();
        source.BeginCapture();

        Assert.That(source.TryTake(Key.F, true, out _), Is.False);
    }

    [Test]
    public void TimestampSourceSubscribesToFrameworkStyleWindowEvents()
    {
        using var source = new KeyInputTimestampSource();
        var window = new FakeWindowEvents();

        Assert.That(source.AttachWindowEvents(window), Is.True);
        source.BeginCapture();
        window.RaiseKeyDown(Key.J);

        Assert.That(source.TryTake(Key.J, true, out long timestamp), Is.True);
        Assert.That(timestamp, Is.GreaterThan(0));
    }

    [Test]
    public void RawInputUsesIndependentAuthoritativeQueue()
    {
        using var backend = new FakeTimestampBackend();
        using var source = new KeyInputTimestampSource(backend);
        source.BeginCapture();
        backend.Enqueue(Key.K, true, 90);
        source.Record(Key.K, true, 100);

        Assert.That(source.TryDequeueRaw(out TimestampedKeyInput input), Is.True);
        Assert.That(input.Key, Is.EqualTo(Key.K));
        Assert.That(input.IsPressed, Is.True);
        Assert.That(input.Timestamp, Is.EqualTo(90));
        Assert.That(source.TryTake(Key.K, true, out _), Is.False);
    }

    [Test]
    public void InputAgeStatisticsReportTailLatencyAndRawCoverage()
    {
        var tracker = new InputAgeTracker();
        for (int index = 1; index <= 100; index++)
        {
            tracker.Record(
                index,
                index <= 75
                    ? KeyInputTimestampKind.RawInput
                    : KeyInputTimestampKind.FrameworkWindow);
        }

        InputAgeStatistics statistics = tracker.Snapshot();

        Assert.That(statistics.Count, Is.EqualTo(100));
        Assert.That(statistics.RawInputCount, Is.EqualTo(75));
        Assert.That(statistics.P50Milliseconds, Is.EqualTo(50));
        Assert.That(statistics.P95Milliseconds, Is.EqualTo(95));
        Assert.That(statistics.P99Milliseconds, Is.EqualTo(99));
    }

    [TestCase(1000.5)]
    [TestCase(1002.083333)]
    [TestCase(1008.333333)]
    [TestCase(1033.0)]
    public void TimestampedJudgementIsIndependentOfObservationDelay(
        double gameplayTimeAtObservation)
    {
        const long frequency = 1_000_000;
        const long eventTimestamp = 10_000_000;
        long observationTimestamp = eventTimestamp
                                    + (long)Math.Round(
                                        (gameplayTimeAtObservation - 1000)
                                        * frequency
                                        / 1000);
        double inputTime = GameplayInputClock.AtEventTimestamp(
            gameplayTimeAtObservation,
            eventTimestamp,
            observationTimestamp,
            frequency);
        var state = new BeatmapJudgementState(
            createSingleTapBeatmap(),
            JudgementWindows.DefaultMania);

        JudgementEvent judgement =
            state.TryJudgeLanePress(0, inputTime);

        Assert.That(inputTime, Is.EqualTo(1000).Within(0.001));
        Assert.That(judgement, Is.Not.Null);
        Assert.That(judgement.Rating, Is.EqualTo(JudgementRating.Perfect));
        Assert.That(judgement.HitErrorMilliseconds, Is.EqualTo(0).Within(0.001));
    }

    [Test]
    public void TimestampSourceReportsFallbackQueueOverflow()
    {
        using var source = new KeyInputTimestampSource();
        source.BeginCapture();

        for (int index = 0; index < 17; index++)
        {
            source.Record(Key.D, true, index * 2 + 1);
            source.Record(Key.D, false, index * 2 + 2);
        }

        KeyInputTimestampBackendStatus status = source.Status;

        Assert.That(status.Name, Is.EqualTo("SDL window fallback"));
        Assert.That(status.CapturedEdgeCount, Is.EqualTo(34));
        Assert.That(status.PendingEdgeCount, Is.EqualTo(32));
        Assert.That(status.DroppedEdgeCount, Is.EqualTo(2));
    }

    private static YokkoBeatmap createSingleTapBeatmap() => new(
        "Input clock test",
        "Yokko",
        "Yokko",
        "4K",
        KeyMode.FourKey,
        ChartSourceFormat.Yokko,
        [YokkoTimingPoint.Default],
        null,
        [new YokkoHitObject(0, 1000, null, HitObjectKind.Tap)]);

    private sealed class FakeWindowEvents
    {
        public event Action<Key> KeyDown;

        public event Action<Key> KeyUp;

        public void RaiseKeyDown(Key key) => KeyDown?.Invoke(key);

        public void RaiseKeyUp(Key key) => KeyUp?.Invoke(key);
    }

    private sealed class FakeTimestampBackend : IKeyInputTimestampBackend
    {
        private readonly System.Collections.Generic.Queue<TimestampedKeyInput>
            pending = new();

        public string Name => "Fake raw input";

        public bool IsAvailable => true;

        public KeyInputTimestampBackendStatus Status => new(
            Name,
            IsAvailable,
            true,
            pending.Count,
            pending.Count,
            0);

        public bool Attach(osu.Framework.Platform.IWindow window) => true;

        public void BeginCapture() => pending.Clear();

        public void EndCapture() => pending.Clear();

        public bool TryDequeue(out TimestampedKeyInput input)
        {
            if (pending.Count > 0)
            {
                input = pending.Dequeue();
                return true;
            }

            input = default;
            return false;
        }

        public void Enqueue(Key key, bool isPressed, long timestamp) =>
            pending.Enqueue(new TimestampedKeyInput(
                key,
                isPressed,
                timestamp));

        public void Dispose()
        {
        }
    }
}
