namespace Yokko.Audio;

/// <summary>
/// Correlates an endpoint-presented audio frame with a monotonic timestamp.
/// </summary>
public readonly record struct AudioClockCorrelation(
    ulong PresentedFramePosition,
    ulong MaximumPresentedFramePosition,
    int SampleRate,
    long ObservationTimestamp,
    long TimestampFrequency)
{
    public bool IsValid =>
        SampleRate > 0
        && ObservationTimestamp > 0
        && TimestampFrequency > 0;

    public double PresentedTimeMilliseconds =>
        SampleRate > 0
            ? PresentedFramePosition * 1000.0 / SampleRate
            : 0;

    public bool TryGetOutputTimeAtTimestamp(
        long timestamp,
        long timestampFrequency,
        out double outputTimeMilliseconds)
    {
        if (!IsValid || timestamp <= 0 || timestampFrequency <= 0)
        {
            outputTimeMilliseconds = 0;
            return false;
        }

        double elapsedMilliseconds = differenceMilliseconds(
            timestamp,
            timestampFrequency,
            ObservationTimestamp,
            TimestampFrequency);
        outputTimeMilliseconds = PresentedTimeMilliseconds
                                 + elapsedMilliseconds;

        if (MaximumPresentedFramePosition >= PresentedFramePosition)
        {
            outputTimeMilliseconds = Math.Min(
                outputTimeMilliseconds,
                MaximumPresentedFramePosition * 1000.0 / SampleRate);
        }

        return double.IsFinite(outputTimeMilliseconds);
    }

    private static double differenceMilliseconds(
        long timestamp,
        long timestampFrequency,
        long anchorTimestamp,
        long anchorFrequency)
    {
        long timestampSeconds = Math.DivRem(
            timestamp,
            timestampFrequency,
            out long timestampRemainder);
        long anchorSeconds = Math.DivRem(
            anchorTimestamp,
            anchorFrequency,
            out long anchorRemainder);

        return (timestampSeconds - anchorSeconds) * 1000.0
               + timestampRemainder * 1000.0 / timestampFrequency
               - anchorRemainder * 1000.0 / anchorFrequency;
    }
}
