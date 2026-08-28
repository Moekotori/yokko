namespace Yokko.Core.Timing;

public sealed class BeatTimingMap
{
    private readonly TimingSegment[] segments;

    public BeatTimingMap(IEnumerable<YokkoTimingPoint> timingPoints, int beatDivisor = 4)
    {
        if (beatDivisor <= 0)
            throw new ArgumentOutOfRangeException(nameof(beatDivisor));

        BeatDivisor = beatDivisor;
        TimingPoints = timingPoints?.ToArray() ?? [];

        YokkoTimingPoint[] activeTimingPoints = TimingPoints
                                                .Where(point =>
                                                    point.Uninherited
                                                    && double.IsFinite(
                                                        point.BeatLengthMilliseconds)
                                                    && point.BeatLengthMilliseconds
                                                    > 0)
                                                .OrderBy(point => point.TimeMilliseconds)
                                                .GroupBy(point => point.TimeMilliseconds)
                                                .Select(group => group.Last())
                                                .ToArray();

        if (activeTimingPoints.Length == 0)
            activeTimingPoints = [YokkoTimingPoint.Default];

        segments = new TimingSegment[activeTimingPoints.Length];
        int startRow = 0;

        for (int i = 0; i < activeTimingPoints.Length; i++)
        {
            YokkoTimingPoint point = activeTimingPoints[i];
            double startTime = point.TimeMilliseconds;

            if (i > 0)
            {
                TimingSegment previous = segments[i - 1];
                double duration = Math.Max(0, point.TimeMilliseconds - previous.StartTimeMilliseconds);
                int rowsUntilPoint = Math.Max(1, (int)Math.Ceiling(duration / previous.StepMilliseconds - 0.0000001));
                startRow = previous.StartRow + rowsUntilPoint;
            }
            else
            {
                double step = point.BeatLengthMilliseconds / BeatDivisor;

                if (startTime > 0)
                    startTime -= Math.Floor(startTime / step) * step;
                else if (startTime < 0)
                    startTime += Math.Ceiling(-startTime / step) * step;
            }

            segments[i] = new TimingSegment(
                startRow,
                startTime,
                point.BeatLengthMilliseconds / BeatDivisor,
                point);
        }
    }

    public int BeatDivisor { get; }

    public IReadOnlyList<YokkoTimingPoint> TimingPoints { get; }

    public double TimeAtRow(int row)
    {
        row = Math.Max(0, row);
        TimingSegment segment = segmentAtRow(row);
        return segment.StartTimeMilliseconds + (row - segment.StartRow) * segment.StepMilliseconds;
    }

    public int ClosestRowAt(double timeMilliseconds)
    {
        int segmentIndex = segmentIndexAtTime(timeMilliseconds);
        TimingSegment segment = segments[segmentIndex];
        int localRow = (int)Math.Round(
            (timeMilliseconds - segment.StartTimeMilliseconds) / segment.StepMilliseconds,
            MidpointRounding.AwayFromZero);
        int candidate = Math.Max(segment.StartRow, segment.StartRow + localRow);

        if (segmentIndex < segments.Length - 1)
        {
            TimingSegment next = segments[segmentIndex + 1];
            candidate = Math.Min(candidate, next.StartRow - 1);

            if (Math.Abs(timeMilliseconds - next.StartTimeMilliseconds) <= Math.Abs(timeMilliseconds - TimeAtRow(candidate)))
                return next.StartRow;
        }

        return candidate;
    }

    public double ClosestSnappedTime(double timeMilliseconds)
        => TimeAtRow(ClosestRowAt(timeMilliseconds));

    public double StepAtTime(double timeMilliseconds)
        => segmentAtTime(timeMilliseconds).StepMilliseconds;

    public YokkoTimingPoint TimingPointAt(double timeMilliseconds)
        => segmentAtTime(timeMilliseconds).Point;

    public bool IsBeatRow(int row)
    {
        TimingSegment segment = segmentAtRow(Math.Max(0, row));
        return (row - segment.StartRow) % BeatDivisor == 0;
    }

    public bool IsMeasureRow(int row)
    {
        TimingSegment segment = segmentAtRow(Math.Max(0, row));
        int rowsPerMeasure = BeatDivisor * Math.Max(1, segment.Point.Meter);
        return (row - segment.StartRow) % rowsPerMeasure == 0;
    }

    private TimingSegment segmentAtRow(int row)
    {
        for (int i = segments.Length - 1; i >= 0; i--)
        {
            if (row >= segments[i].StartRow)
                return segments[i];
        }

        return segments[0];
    }

    private TimingSegment segmentAtTime(double timeMilliseconds)
        => segments[segmentIndexAtTime(timeMilliseconds)];

    /// <summary>
    /// Binary-searches the last segment starting at or before the given time,
    /// mirroring <see cref="ScrollVelocityMap"/>'s segment lookup. Times
    /// before the first segment clamp to index 0.
    /// </summary>
    private int segmentIndexAtTime(double timeMilliseconds)
    {
        int low = 0;
        int high = segments.Length - 1;
        int result = 0;

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

    private sealed record TimingSegment(
        int StartRow,
        double StartTimeMilliseconds,
        double StepMilliseconds,
        YokkoTimingPoint Point);
}
