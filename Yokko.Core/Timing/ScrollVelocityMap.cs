namespace Yokko.Core.Timing;

/// <summary>
/// Integrates piecewise-constant scroll velocities into a continuous visual
/// position axis.
/// </summary>
public sealed class ScrollVelocityMap
{
    private readonly Segment[] segments;
    private readonly double[] rangeMinimums;
    private readonly double[] rangeMaximums;

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
        rangeMinimums =
            new double[Math.Max(1, segments.Length * 4)];
        rangeMaximums =
            new double[Math.Max(1, segments.Length * 4)];

        if (segments.Length == 0)
            return;

        double position = ScrollVelocities[0].TimeMilliseconds * InitialMultiplier;
        bool isNegativeDirection = InitialMultiplier < 0;

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

            if (velocity.Multiplier != 0)
                isNegativeDirection = velocity.Multiplier < 0;

            segments[i] = new Segment(
                velocity.TimeMilliseconds,
                position,
                velocity.Multiplier,
                isNegativeDirection);
        }

        buildPositionRange(1, 0, segments.Length);
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
    /// Returns the active visual direction. A zero-velocity segment keeps the
    /// direction of the last non-zero segment, matching Quaver's SV rules.
    /// </summary>
    public bool IsNegativeDirectionAt(double timeMilliseconds)
    {
        if (!double.IsFinite(timeMilliseconds))
            throw new ArgumentOutOfRangeException(nameof(timeMilliseconds));

        int index = segmentIndexAt(timeMilliseconds);
        return index < 0
            ? InitialMultiplier < 0
            : segments[index].IsNegativeDirection;
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

        int rangeStart = firstSegmentAfter(startTimeMilliseconds);
        int rangeEnd = firstSegmentAtOrAfter(endTimeMilliseconds);

        if (rangeStart < rangeEnd)
        {
            queryPositionRange(
                1,
                0,
                segments.Length,
                rangeStart,
                rangeEnd,
                ref minimum,
                ref maximum);
        }

        return new ScrollPositionRange(minimum, maximum);
    }

    private void buildPositionRange(int node, int start, int end)
    {
        if (end - start == 1)
        {
            rangeMinimums[node] =
                rangeMaximums[node] =
                    segments[start].StartPosition;
            return;
        }

        int middle = start + (end - start) / 2;
        buildPositionRange(node * 2, start, middle);
        buildPositionRange(node * 2 + 1, middle, end);
        rangeMinimums[node] = Math.Min(
            rangeMinimums[node * 2],
            rangeMinimums[node * 2 + 1]);
        rangeMaximums[node] = Math.Max(
            rangeMaximums[node * 2],
            rangeMaximums[node * 2 + 1]);
    }

    private void queryPositionRange(
        int node,
        int start,
        int end,
        int queryStart,
        int queryEnd,
        ref double minimum,
        ref double maximum)
    {
        if (queryEnd <= start || end <= queryStart)
            return;

        if (queryStart <= start && end <= queryEnd)
        {
            minimum = Math.Min(minimum, rangeMinimums[node]);
            maximum = Math.Max(maximum, rangeMaximums[node]);
            return;
        }

        int middle = start + (end - start) / 2;
        queryPositionRange(
            node * 2,
            start,
            middle,
            queryStart,
            queryEnd,
            ref minimum,
            ref maximum);
        queryPositionRange(
            node * 2 + 1,
            middle,
            end,
            queryStart,
            queryEnd,
            ref minimum,
            ref maximum);
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

    private int firstSegmentAfter(double timeMilliseconds)
    {
        int low = 0;
        int high = segments.Length;

        while (low < high)
        {
            int middle = low + (high - low) / 2;

            if (segments[middle].StartTimeMilliseconds
                <= timeMilliseconds)
            {
                low = middle + 1;
            }
            else
                high = middle;
        }

        return low;
    }

    private int firstSegmentAtOrAfter(double timeMilliseconds)
    {
        int low = 0;
        int high = segments.Length;

        while (low < high)
        {
            int middle = low + (high - low) / 2;

            if (segments[middle].StartTimeMilliseconds
                < timeMilliseconds)
            {
                low = middle + 1;
            }
            else
                high = middle;
        }

        return low;
    }

    private sealed record Segment(
        double StartTimeMilliseconds,
        double StartPosition,
        double Multiplier,
        bool IsNegativeDirection);
}

public readonly record struct ScrollPositionRange(
    double Minimum,
    double Maximum);
