using System.Collections.Generic;
using Yokko.Audio;

namespace Yokko.Game.Gameplay;

internal static class GameplayHitSamplePlayer
{
    internal static void TriggerSamples(
        IAudioSamplePlayback samplePlayback,
        IReadOnlyList<GameplayHitSamplePlaybackBinding> samples)
    {
        foreach (GameplayHitSamplePlaybackBinding sample in samples)
        {
            if (sample.HasPreparedHandle
                && samplePlayback is IPreparedAudioSamplePlayback prepared)
            {
                prepared.TriggerPreparedSample(
                    sample.PreparedHandle,
                    sample.Gain);
            }
            else if (samplePlayback is IAudioSamplePlaybackWithGain withGain)
            {
                withGain.TriggerSample(sample.Path, sample.Gain);
            }
            else if (sample.Gain > 0)
            {
                samplePlayback.TriggerSample(sample.Path);
            }
        }
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
