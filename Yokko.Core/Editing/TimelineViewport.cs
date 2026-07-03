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

    public int VisibleRows { get; private set; }

    public int EndRowExclusive => StartRow + VisibleRows;

    public void MoveByRows(int rowDelta, int totalRows)
        => MoveToRow(StartRow + rowDelta, totalRows);

    public void MoveToRow(int row, int totalRows)
    {
        int maxStart = Math.Max(0, totalRows - VisibleRows);
        StartRow = Math.Clamp(row, 0, maxStart);
    }

    public void SetVisibleRows(int visibleRows, int totalRows)
    {
        if (visibleRows <= 0)
            throw new ArgumentOutOfRangeException(nameof(visibleRows));

        int centreRow = StartRow + VisibleRows / 2;
        VisibleRows = visibleRows;
        MoveToRow(centreRow - VisibleRows / 2, totalRows);
    }

    public double StartMilliseconds(double stepMilliseconds) => StartRow * stepMilliseconds;

    public double EndMilliseconds(double stepMilliseconds) => EndRowExclusive * stepMilliseconds;
}
