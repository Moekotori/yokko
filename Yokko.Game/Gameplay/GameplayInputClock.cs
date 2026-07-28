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

        if (!TryGetEventAgeMilliseconds(
                eventTimestamp,
                observationTimestamp,
                frequency,
                out double eventAgeMilliseconds))
            return gameplayTimeAtObservationMilliseconds;

        return gameplayTimeAtObservationMilliseconds - eventAgeMilliseconds;
    }

    public static bool TryGetEventAgeMilliseconds(
        long eventTimestamp,
        long observationTimestamp,
        long timestampFrequency,
        out double eventAgeMilliseconds)
    {
        long frequency = timestampFrequency > 0
            ? timestampFrequency
            : Stopwatch.Frequency;

        if (eventTimestamp <= 0
            || observationTimestamp <= eventTimestamp
            || frequency <= 0)
        {
            eventAgeMilliseconds = 0;
            return false;
        }

        eventAgeMilliseconds =
            (observationTimestamp - eventTimestamp) * 1000.0 / frequency;
        return true;
    }
}
