namespace Yokko.Core.Editing;

public sealed class TimelineViewport
{
    public TimelineViewport(int startRow, int visibleRows)
    {
        if (visibleRows <= 0)
            throw new ArgumentOutOfRangeException(nameof(visibleRows));

        VisibleRows = visibleRows;
        StartRow = Math.Max(0, startRow);
    }

    public int StartRow { get; private set; }

    public int VisibleRows { get; }

    public int EndRowExclusive => StartRow + VisibleRows;

    public void MoveByRows(int rowDelta, int totalRows)
        => MoveToRow(StartRow + rowDelta, totalRows);

    public void MoveToRow(int row, int totalRows)
    {
        int maxStart = Math.Max(0, totalRows - VisibleRows);
        StartRow = Math.Clamp(row, 0, maxStart);
    }

    public double StartMilliseconds(double stepMilliseconds) => StartRow * stepMilliseconds;

    public double EndMilliseconds(double stepMilliseconds) => EndRowExclusive * stepMilliseconds;
}
