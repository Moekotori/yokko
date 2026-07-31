using System;

namespace Yokko.Game.Input;

internal readonly record struct InputDropObservation(
    long NewlyDropped,
    long TotalDropped)
{
    internal bool RequiresRecovery => NewlyDropped > 0;
}

internal sealed class InputDropTracker
{
    private long previousBackendCount;
    private long totalDropped;

    internal InputDropObservation Observe(long backendCount)
    {
        if (backendCount < 0)
            throw new ArgumentOutOfRangeException(nameof(backendCount));

        if (backendCount < previousBackendCount)
            previousBackendCount = 0;

        long newlyDropped = backendCount - previousBackendCount;
        previousBackendCount = backendCount;
        totalDropped += newlyDropped;
        return new InputDropObservation(newlyDropped, totalDropped);
    }

    internal void MarkBackendReset() => previousBackendCount = 0;
}
