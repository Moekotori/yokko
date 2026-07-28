namespace Yokko.Core.Timing;

public sealed record YokkoTimingPoint(
    double TimeMilliseconds,
    double BeatLengthMilliseconds,
    int Meter = 4,
    int SampleSet = 2,
    int SampleIndex = 0,
    int Volume = 100,
    bool Uninherited = true,
    int Effects = 0)
{
    public static YokkoTimingPoint Default { get; } = new(0, 500);

    public double BeatsPerMinute => Uninherited && BeatLengthMilliseconds > 0
        ? 60000 / BeatLengthMilliseconds
        : 0;
}
