namespace Yokko.Game.Input;

/// <summary>
/// Accumulates the native sample-trigger telemetry dropped counter across
/// audio engine restarts, which reset the native counter back to zero.
/// </summary>
internal sealed class AudioSampleTelemetryDropTracker
{
    private ulong previousDroppedCount;
    private ulong accumulatedDroppedCount;

    internal ulong TotalDropped
        => accumulatedDroppedCount + previousDroppedCount;

    internal void Observe(ulong droppedCount)
    {
        if (droppedCount < previousDroppedCount)
            accumulatedDroppedCount += previousDroppedCount;

        previousDroppedCount = droppedCount;
    }
}
