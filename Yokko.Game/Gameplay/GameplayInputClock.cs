using System;
using System.Diagnostics;

namespace Yokko.Game.Gameplay;

/// <summary>
/// Correlates a timestamped input edge with the authoritative gameplay clock.
/// </summary>
internal static class GameplayInputClock
{
    public static double AtEventTimestamp(
        double gameplayTimeAtObservationMilliseconds,
        long eventTimestamp,
        long observationTimestamp,
        long timestampFrequency = 0)
    {
        long frequency = timestampFrequency > 0
            ? timestampFrequency
            : Stopwatch.Frequency;

        if (eventTimestamp <= 0
            || observationTimestamp <= eventTimestamp
            || frequency <= 0)
            return gameplayTimeAtObservationMilliseconds;

        double eventAgeMilliseconds =
            (observationTimestamp - eventTimestamp) * 1000.0 / frequency;

        return Math.Max(
            0,
            gameplayTimeAtObservationMilliseconds - eventAgeMilliseconds);
    }
}
