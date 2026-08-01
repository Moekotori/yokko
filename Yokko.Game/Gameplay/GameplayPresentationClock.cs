using System;
using Yokko.Audio;

namespace Yokko.Game.Gameplay;

internal static class GameplayPresentationClock
{
    /// <summary>
    /// Smoothly projects the endpoint clock to the current frame while keeping
    /// judgement on the authoritative sampled gameplay time.
    /// </summary>
    internal static double EstimateVisualTime(
        double authoritativeGameplayTimeMilliseconds,
        ITimestampedAudioClock timestampedAudioClock,
        AudioEngineSnapshot snapshot,
        long presentationTimestamp,
        long timestampFrequency,
        double userOffsetMilliseconds)
    {
        if (!GameplayInputClock.TryAtAudioTimestamp(
                timestampedAudioClock,
                snapshot,
                presentationTimestamp,
                timestampFrequency,
                userOffsetMilliseconds,
                out double projectedGameplayTimeMilliseconds))
        {
            return authoritativeGameplayTimeMilliseconds;
        }

        return Math.Max(
            authoritativeGameplayTimeMilliseconds,
            projectedGameplayTimeMilliseconds);
    }
}
