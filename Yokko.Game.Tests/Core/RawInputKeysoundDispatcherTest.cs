using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osuTK.Input;
using Yokko.Audio;
using Yokko.Core.Beatmaps;
using Yokko.Core.Gameplay;
using Yokko.Core.Scoring;
using Yokko.Game.Gameplay;
using Yokko.Game.Input;
using Yokko.Game.Screens.Gameplay;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class RawInputKeysoundDispatcherTest
{
    [Test]
    public void RawPressTriggersPreparedSampleOnceAndAdvancesAfterUpdate()
    {
        YokkoBeatmap beatmap = createBeatmap();
        var judgementState = new BeatmapJudgementState(beatmap);
        var selector = new GameplayKeysoundSelector(
            beatmap,
            judgementState);
        var audio = new FastPathAudioEngine { InputTime = 1000 };
        RawInputKeysoundDispatcher dispatcher = createDispatcher(
            beatmap,
            selector,
            audio);
        dispatcher.RefreshAllAndEnable();

        Assert.That(
            dispatcher.TryDispatch(
                Key.D,
                true,
                100,
                out KeyInputFastPathResult first),
            Is.True);
        Assert.That(first.HitObjectIndex, Is.Zero);
        Assert.That(first.TriggeredSampleMask, Is.EqualTo(1));
        Assert.That(
            dispatcher.TryDispatch(Key.D, true, 101, out _),
            Is.False,
            "A lane remains claimed until Update consumes the edge.");

        selector.Select(0, 1000);
        judgementState.JudgeLanePress(0, 1000);
        dispatcher.RefreshLane(0);
        audio.InputTime = 1800;

        Assert.That(
            dispatcher.TryDispatch(
                Key.D,
                true,
                200,
                out KeyInputFastPathResult second),
            Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(second.HitObjectIndex, Is.EqualTo(1));
            Assert.That(audio.TimestampedTriggerCount, Is.EqualTo(2));
        });
    }

    [Test]
    public void AmbiguousLateSelectionFallsBackWithoutPlaying()
    {
        YokkoBeatmap beatmap = createBeatmap();
        var judgementState = new BeatmapJudgementState(beatmap);
        var selector = new GameplayKeysoundSelector(
            beatmap,
            judgementState);
        var audio = new FastPathAudioEngine { InputTime = 5000 };
        RawInputKeysoundDispatcher dispatcher = createDispatcher(
            beatmap,
            selector,
            audio);
        dispatcher.RefreshAllAndEnable();

        Assert.That(
            dispatcher.TryDispatch(Key.D, true, 100, out _),
            Is.False);
        Assert.That(audio.TimestampedTriggerCount, Is.Zero);
    }

    [Test]
    public void DispatchDoesNotAllocateAfterWarmup()
    {
        YokkoBeatmap beatmap = createBeatmap();
        var selector = new GameplayKeysoundSelector(
            beatmap,
            new BeatmapJudgementState(beatmap));
        var audio = new FastPathAudioEngine { InputTime = 1000 };
        RawInputKeysoundDispatcher dispatcher = createDispatcher(
            beatmap,
            selector,
            audio);
        dispatcher.RefreshAllAndEnable();
        for (int index = 0; index < 1000; index++)
        {
            dispatcher.TryDispatch(Key.D, true, index + 1, out _);
            dispatcher.RefreshLane(0);
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < 10_000; index++)
        {
            dispatcher.TryDispatch(Key.D, true, index + 1, out _);
            dispatcher.RefreshLane(0);
        }

        Assert.That(
            GC.GetAllocatedBytesForCurrentThread() - before,
            Is.Zero);
    }

    private static RawInputKeysoundDispatcher createDispatcher(
        YokkoBeatmap beatmap,
        GameplayKeysoundSelector selector,
        FastPathAudioEngine audio)
    {
        object owner = new();
        GameplayHitSamplePlaybackBinding[][] samples =
        [
            [
                new GameplayHitSamplePlaybackBinding(
                    "first.wav",
                    1,
                    new PreparedAudioSampleHandle(owner, 1, 0),
                    true),
            ],
            [
                new GameplayHitSamplePlaybackBinding(
                    "second.wav",
                    1,
                    new PreparedAudioSampleHandle(owner, 1, 1),
                    true),
            ],
        ];
        return new RawInputKeysoundDispatcher(
            KeyModeBindings.ForMode(KeyMode.FourKey),
            audio,
            audio,
            audio,
            selector,
            samples);
    }

    private static YokkoBeatmap createBeatmap() => new(
        "Raw keysound test",
        "Yokko",
        "Yokko",
        "4K",
        KeyMode.FourKey,
        ChartSourceFormat.Yokko,
        [Yokko.Core.Timing.YokkoTimingPoint.Default],
        null,
        [
            new YokkoHitObject(
                0,
                1000,
                null,
                HitObjectKind.Tap,
                "first.wav"),
            new YokkoHitObject(
                0,
                2000,
                null,
                HitObjectKind.Tap,
                "second.wav"),
        ]);

    private sealed class FastPathAudioEngine :
        IAudioEngine,
        ITimestampedAudioClock,
        ITimestampedPreparedAudioSamplePlayback
    {
        public double InputTime { get; set; }

        public int TimestampedTriggerCount { get; private set; }

        public AudioEngineStatus Status => default;

        public double PlaybackTimeMilliseconds => InputTime;

        public double DurationMilliseconds => 10_000;

        public IReadOnlyList<AudioBackendCapabilities> Backends => [];

        public bool SupportsSampleTriggerTelemetry => true;

        public AudioSampleTriggerTelemetryStatus
            SampleTriggerTelemetryStatus => default;

        public ValueTask<IReadOnlyList<AudioDeviceInfo>>
            GetOutputDevicesAsync(
                CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<AudioDeviceInfo>>([]);

        public ValueTask StartAsync(
            AudioEngineStartRequest request,
            CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        public ValueTask PauseAsync(
            CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        public ValueTask SeekAsync(
            double timeMilliseconds,
            CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        public ValueTask StopAsync(
            CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public bool TryGetPlaybackTimeAtTimestamp(
            AudioEngineSnapshot snapshot,
            long timestamp,
            long timestampFrequency,
            out double playbackTimeMilliseconds)
        {
            playbackTimeMilliseconds = InputTime;
            return true;
        }

        public ValueTask PrepareSamplesAsync(
            IReadOnlyCollection<string> samplePaths,
            CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        public bool TriggerSample(string samplePath) => true;

        public bool TriggerSample(string samplePath, double gain) => true;

        public uint StartLoopingSample(string samplePath, double gain) => 1;

        public bool StopLoopingSample(uint loopId) => true;

        public bool TryGetPreparedSampleHandle(
            string samplePath,
            out PreparedAudioSampleHandle handle)
        {
            handle = default;
            return false;
        }

        public bool TriggerPreparedSample(
            PreparedAudioSampleHandle handle,
            double gain) => true;

        public bool TriggerPreparedSample(
            PreparedAudioSampleHandle handle,
            double gain,
            long captureTimestamp,
            long timestampFrequency,
            out ulong traceId)
        {
            TimestampedTriggerCount++;
            traceId = (ulong)TimestampedTriggerCount;
            return true;
        }

        public uint StartLoopingPreparedSample(
            PreparedAudioSampleHandle handle,
            double gain) => 1;

        public bool TryDequeueSampleTriggerTelemetry(
            out AudioSampleTriggerTelemetry telemetry)
        {
            telemetry = default;
            return false;
        }
    }
}
