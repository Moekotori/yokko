namespace Yokko.Core.Beatmaps;

/// <summary>
/// A chart-authored one-shot sample played from the gameplay timeline.
/// </summary>
public sealed record YokkoScheduledSample
{
    public YokkoScheduledSample(
        double TimeMilliseconds,
        string Path,
        int Volume = 100,
        bool UnaffectedByRate = false,
        bool UseMusicBus = false)
    {
        if (!double.IsFinite(TimeMilliseconds))
            throw new ArgumentOutOfRangeException(nameof(TimeMilliseconds));
        if (string.IsNullOrWhiteSpace(Path))
            throw new ArgumentException("A sample path is required.", nameof(Path));
        if (Volume is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(Volume));

        this.TimeMilliseconds = TimeMilliseconds;
        this.Path = Path;
        this.Volume = Volume;
        this.UnaffectedByRate = UnaffectedByRate;
        this.UseMusicBus = UseMusicBus;
    }

    public double TimeMilliseconds { get; }
    public string Path { get; }
    public int Volume { get; }
    public bool UnaffectedByRate { get; }
    public bool UseMusicBus { get; }
}
