using System.Collections.Generic;
using System.Diagnostics;
using Yokko.Audio;

namespace Yokko.Game.Gameplay;

internal static class GameplayHitSamplePlayer
{
    internal readonly record struct TriggerResult(
        bool AnyTriggered,
        ulong TriggeredSampleMask,
        long LastAudioEnqueueTimestamp);

    internal static bool TriggerSamples(
        IAudioSamplePlayback samplePlayback,
        IReadOnlyList<GameplayHitSamplePlaybackBinding> samples)
        => TriggerSamples(
            samplePlayback,
            samples,
            0,
            0,
            0).AnyTriggered;

    internal static TriggerResult TriggerSamples(
        IAudioSamplePlayback samplePlayback,
        IReadOnlyList<GameplayHitSamplePlaybackBinding> samples,
        ulong alreadyTriggeredMask,
        long captureTimestamp,
        long timestampFrequency)
    {
        bool triggered = false;
        ulong triggeredMask = alreadyTriggeredMask;
        long lastAudioEnqueueTimestamp = 0;
        for (int index = 0; index < samples.Count; index++)
        {
            if (index < 64
                && (alreadyTriggeredMask & (1UL << index)) != 0)
                continue;

            GameplayHitSamplePlaybackBinding sample = samples[index];
            bool sampleTriggered;
            if (sample.HasPreparedHandle
                && captureTimestamp > 0
                && timestampFrequency > 0
                && samplePlayback is
                    ITimestampedPreparedAudioSamplePlayback timestamped
                && timestamped.SupportsSampleTriggerTelemetry)
            {
                sampleTriggered = timestamped.TriggerPreparedSample(
                    sample.PreparedHandle,
                    sample.Gain,
                    captureTimestamp,
                    timestampFrequency,
                    out _);
            }
            else if (sample.HasPreparedHandle
                && samplePlayback is IPreparedAudioSamplePlayback prepared)
            {
                sampleTriggered = prepared.TriggerPreparedSample(
                    sample.PreparedHandle,
                    sample.Gain);
            }
            else if (samplePlayback is IAudioSamplePlaybackWithGain withGain)
            {
                sampleTriggered = withGain.TriggerSample(
                    sample.Path,
                    sample.Gain);
            }
            else if (sample.Gain > 0)
            {
                sampleTriggered = samplePlayback.TriggerSample(sample.Path);
            }
            else
            {
                sampleTriggered = false;
            }

            if (sampleTriggered)
            {
                triggered = true;
                if (index < 64)
                    triggeredMask |= 1UL << index;
                lastAudioEnqueueTimestamp = Stopwatch.GetTimestamp();
            }
        }

        return new TriggerResult(
            triggered || alreadyTriggeredMask != 0,
            triggeredMask,
            lastAudioEnqueueTimestamp);
    }

    internal static uint StartLoopingSample(
        IAudioLoopingSamplePlayback samplePlayback,
        GameplayHitSamplePlaybackBinding sample)
    {
        if (sample.HasPreparedHandle
            && samplePlayback is IPreparedAudioSamplePlayback prepared)
        {
            return prepared.StartLoopingPreparedSample(
                sample.PreparedHandle,
                sample.Gain);
        }

        return samplePlayback.StartLoopingSample(
            sample.Path,
            sample.Gain);
    }
}
