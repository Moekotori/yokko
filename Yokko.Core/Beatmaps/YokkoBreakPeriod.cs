namespace Yokko.Core.Beatmaps;

public sealed record YokkoBreakPeriod
{
    public YokkoBreakPeriod(
        double StartTimeMilliseconds,
        double EndTimeMilliseconds)
    {
        if (!double.IsFinite(StartTimeMilliseconds))
        {
            throw new ArgumentOutOfRangeException(
                nameof(StartTimeMilliseconds));
        }

        if (!double.IsFinite(EndTimeMilliseconds)
            || EndTimeMilliseconds < StartTimeMilliseconds)
        {
            throw new ArgumentOutOfRangeException(
                nameof(EndTimeMilliseconds));
        }

        this.StartTimeMilliseconds = StartTimeMilliseconds;
        this.EndTimeMilliseconds = EndTimeMilliseconds;
    }

    public double StartTimeMilliseconds { get; }

    public double EndTimeMilliseconds { get; }

    public double DurationMilliseconds =>
        EndTimeMilliseconds - StartTimeMilliseconds;
}
