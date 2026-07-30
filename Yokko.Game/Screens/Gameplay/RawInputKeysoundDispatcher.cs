using System.Diagnostics;
using System.Linq;
using System.Threading;
using osuTK.Input;
using Yokko.Audio;
using Yokko.Game.Gameplay;
using Yokko.Game.Input;

namespace Yokko.Game.Screens.Gameplay;

internal sealed class RawInputKeysoundDispatcher : IKeyInputFastPathSink
{
    private readonly KeyModeBindings keyBindings;
    private readonly IAudioEngine audioEngine;
    private readonly ITimestampedAudioClock audioClock;
    private readonly ITimestampedPreparedAudioSamplePlayback samplePlayback;
    private readonly GameplayKeysoundSelector selector;
    private readonly GameplayHitSamplePlaybackBinding[][] samplesByHitObject;
    private readonly LaneSlot[] lanes;
    private int enabled;
    private int activeDispatches;
    private double userOffsetMilliseconds;

    internal RawInputKeysoundDispatcher(
        KeyModeBindings keyBindings,
        IAudioEngine audioEngine,
        ITimestampedAudioClock audioClock,
        ITimestampedPreparedAudioSamplePlayback samplePlayback,
        GameplayKeysoundSelector selector,
        GameplayHitSamplePlaybackBinding[][] samplesByHitObject)
    {
        this.keyBindings = keyBindings;
        this.audioEngine = audioEngine;
        this.audioClock = audioClock;
        this.samplePlayback = samplePlayback;
        this.selector = selector;
        this.samplesByHitObject = samplesByHitObject;
        lanes = Enumerable.Range(0, keyBindings.KeyCount)
                          .Select(static _ => new LaneSlot())
                          .ToArray();
    }

    internal void SetUserOffset(double offsetMilliseconds) =>
        Volatile.Write(ref userOffsetMilliseconds, offsetMilliseconds);

    internal void RefreshAllAndEnable()
    {
        suspendAndWait();
        for (int lane = 0; lane < lanes.Length; lane++)
            publishLane(lane);
        Volatile.Write(ref enabled, 1);
    }

    internal void RefreshLane(int lane)
    {
        if ((uint)lane >= lanes.Length)
            return;

        LaneSlot slot = lanes[lane];
        // A captured press owns the lane until Update has consumed it.
        Volatile.Write(ref slot.Claimed, 1);
        publishLane(lane);
        Volatile.Write(ref slot.Claimed, 0);
    }

    internal void Disable()
    {
        suspendAndWait();
        foreach (LaneSlot lane in lanes)
            Volatile.Write(ref lane.Claimed, 1);
    }

    public bool TryDispatch(
        Key key,
        bool isPressed,
        long captureTimestamp,
        out KeyInputFastPathResult result)
    {
        result = new KeyInputFastPathResult(-1, 0, 0);
        if (!isPressed || Volatile.Read(ref enabled) == 0)
            return false;

        Interlocked.Increment(ref activeDispatches);
        try
        {
            if (Volatile.Read(ref enabled) == 0)
                return false;

            int lane = keyBindings.GetLane(key);
            if ((uint)lane >= lanes.Length)
                return false;

            LaneSlot slot = lanes[lane];
            if (Interlocked.CompareExchange(ref slot.Claimed, 1, 0) != 0)
                return false;

            if (!GameplayInputClock.TryAtAudioTimestamp(
                    audioClock,
                    audioEngine.Snapshot,
                    captureTimestamp,
                    Stopwatch.Frequency,
                    Volatile.Read(ref userOffsetMilliseconds),
                    out double inputTime))
            {
                return false;
            }

            GameplayKeysoundFastSelection selection = readSelection(slot);
            int selected = selection.Select(inputTime);
            if ((uint)selected >= samplesByHitObject.Length)
                return false;

            GameplayHitSamplePlaybackBinding[] samples =
                samplesByHitObject[selected];
            if (samples.Length is 0 or > 64
                || samples.Any(static sample => !sample.HasPreparedHandle))
            {
                return false;
            }

            ulong triggeredMask = 0;
            for (int index = 0; index < samples.Length; index++)
            {
                GameplayHitSamplePlaybackBinding sample = samples[index];
                if (samplePlayback.TriggerPreparedSample(
                        sample.PreparedHandle,
                        sample.Gain,
                        captureTimestamp,
                        Stopwatch.Frequency,
                        out _))
                {
                    triggeredMask |= 1UL << index;
                }
            }

            if (triggeredMask == 0)
                return false;

            result = new KeyInputFastPathResult(
                selected,
                triggeredMask,
                Stopwatch.GetTimestamp());
            return true;
        }
        finally
        {
            Interlocked.Decrement(ref activeDispatches);
        }
    }

    private void suspendAndWait()
    {
        Volatile.Write(ref enabled, 0);
        var spinner = new SpinWait();
        while (Volatile.Read(ref activeDispatches) != 0)
            spinner.SpinOnce();
    }

    private void publishLane(int lane)
    {
        LaneSlot slot = lanes[lane];
        Interlocked.Increment(ref slot.Version);
        slot.Selection = selector.CaptureFastSelection(lane);
        Interlocked.Increment(ref slot.Version);
    }

    private static GameplayKeysoundFastSelection readSelection(LaneSlot slot)
    {
        var spinner = new SpinWait();
        while (true)
        {
            int before = Volatile.Read(ref slot.Version);
            if ((before & 1) != 0)
            {
                spinner.SpinOnce();
                continue;
            }

            GameplayKeysoundFastSelection selection = slot.Selection;
            if (before == Volatile.Read(ref slot.Version))
                return selection;

            spinner.SpinOnce();
        }
    }

    private sealed class LaneSlot
    {
        internal int Version;
        internal int Claimed = 1;
        internal GameplayKeysoundFastSelection Selection;
    }
}
