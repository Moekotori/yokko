namespace Yokko.Core.Timing;

/// <summary>
/// Integrates piecewise-constant scroll velocities into a continuous visual
/// position axis.
/// </summary>
public sealed class ScrollVelocityMap
{
    private readonly Segment[] segments;

    public ScrollVelocityMap(
        IEnumerable<YokkoScrollVelocity>? scrollVelocities,
        double initialMultiplier = 1)
    {
        if (!double.IsFinite(initialMultiplier))
            throw new ArgumentOutOfRangeException(nameof(initialMultiplier));

        InitialMultiplier = initialMultiplier;
        ScrollVelocities = scrollVelocities?
                           .OrderBy(static velocity => velocity.TimeMilliseconds)
                           .GroupBy(static velocity => velocity.TimeMilliseconds)
                           .Select(static group => group.Last())
                           .ToArray()
                           ?? [];

        segments = new Segment[ScrollVelocities.Count];

        if (segments.Length == 0)
            return;

        double position = ScrollVelocities[0].TimeMilliseconds * InitialMultiplier;

        for (int i = 0; i < ScrollVelocities.Count; i++)
        {
            YokkoScrollVelocity velocity = ScrollVelocities[i];

            if (i > 0)
            {
                Segment previous = segments[i - 1];
                position = previous.StartPosition
                           + (velocity.TimeMilliseconds - previous.StartTimeMilliseconds)
                           * previous.Multiplier;
            }

            segments[i] = new Segment(
                velocity.TimeMilliseconds,
                position,
                velocity.Multiplier);
        }
    }

    public double InitialMultiplier { get; }

    public IReadOnlyList<YokkoScrollVelocity> ScrollVelocities { get; }

    public double PositionAt(double timeMilliseconds)
    {
        if (!double.IsFinite(timeMilliseconds))
            throw new ArgumentOutOfRangeException(nameof(timeMilliseconds));

        int index = segmentIndexAt(timeMilliseconds);

        if (index < 0)
            return timeMilliseconds * InitialMultiplier;

        Segment segment = segments[index];
        return segment.StartPosition
               + (timeMilliseconds - segment.StartTimeMilliseconds)
               * segment.Multiplier;
    }

    public double DistanceBetween(
        double startTimeMilliseconds,
        double endTimeMilliseconds)
        => PositionAt(endTimeMilliseconds) - PositionAt(startTimeMilliseconds);

    public double MultiplierAt(double timeMilliseconds)
    {
        if (!double.IsFinite(timeMilliseconds))
            throw new ArgumentOutOfRangeException(nameof(timeMilliseconds));

        int index = segmentIndexAt(timeMilliseconds);
        return index < 0 ? InitialMultiplier : segments[index].Multiplier;
    }

    /// <summary>
    /// Returns the complete visual-position range traversed between two
    /// times, including extrema at SV change points.
    /// </summary>
    public ScrollPositionRange PositionRangeBetween(
        double startTimeMilliseconds,
        double endTimeMilliseconds)
    {
        if (!double.IsFinite(startTimeMilliseconds))
            throw new ArgumentOutOfRangeException(nameof(startTimeMilliseconds));

        if (!double.IsFinite(endTimeMilliseconds))
            throw new ArgumentOutOfRangeException(nameof(endTimeMilliseconds));

        if (endTimeMilliseconds < startTimeMilliseconds)
        {
            (startTimeMilliseconds, endTimeMilliseconds) =
                (endTimeMilliseconds, startTimeMilliseconds);
        }

        double startPosition = PositionAt(startTimeMilliseconds);
        double endPosition = PositionAt(endTimeMilliseconds);
        double minimum = Math.Min(startPosition, endPosition);
        double maximum = Math.Max(startPosition, endPosition);

        foreach (Segment segment in segments)
        {
            if (segment.StartTimeMilliseconds <= startTimeMilliseconds)
                continue;

            if (segment.StartTimeMilliseconds >= endTimeMilliseconds)
                break;

            minimum = Math.Min(minimum, segment.StartPosition);
            maximum = Math.Max(maximum, segment.StartPosition);
        }

        return new ScrollPositionRange(minimum, maximum);
    }

    private int segmentIndexAt(double timeMilliseconds)
    {
        int low = 0;
        int high = segments.Length - 1;
        int result = -1;

        while (low <= high)
        {
            int middle = low + (high - low) / 2;

            if (segments[middle].StartTimeMilliseconds <= timeMilliseconds)
            {
                result = middle;
                low = middle + 1;
            }
            else
                high = middle - 1;
        }

        return result;
    }

    private sealed record Segment(
        double StartTimeMilliseconds,
        double StartPosition,
        double Multiplier);
}

public readonly record struct ScrollPositionRange(
    double Minimum,
    double Maximum);
