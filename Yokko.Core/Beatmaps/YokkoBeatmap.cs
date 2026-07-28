using Yokko.Core.Gameplay;
using Yokko.Core.Timing;

namespace Yokko.Core.Beatmaps;

public sealed record YokkoBeatmap
{
    public YokkoBeatmap(
        string Title,
        string Artist,
        string Creator,
        string DifficultyName,
        KeyMode KeyMode,
        ChartSourceFormat SourceFormat,
        IReadOnlyList<YokkoTimingPoint> TimingPoints,
        string? AudioPath,
        IReadOnlyList<YokkoHitObject> HitObjects,
        double OverallDifficulty = 5,
        IReadOnlyList<YokkoScrollVelocity>? ScrollVelocities = null,
        double InitialScrollVelocity = 1,
        IReadOnlyList<YokkoScrollSpeedFactor>? ScrollSpeedFactors = null,
        IReadOnlyDictionary<string, YokkoScrollProfile>? ScrollProfiles = null)
    {
        if (!double.IsFinite(OverallDifficulty)
            || OverallDifficulty is < 0 or > 10)
        {
            throw new ArgumentOutOfRangeException(
                nameof(OverallDifficulty),
                "Overall difficulty must be finite and between 0 and 10.");
        }

        if (!double.IsFinite(InitialScrollVelocity))
            throw new ArgumentOutOfRangeException(nameof(InitialScrollVelocity));

        this.Title = Title;
        this.Artist = Artist;
        this.Creator = Creator;
        this.DifficultyName = DifficultyName;
        this.KeyMode = KeyMode;
        this.SourceFormat = SourceFormat;
        this.TimingPoints = TimingPoints;
        this.AudioPath = AudioPath;
        this.HitObjects = HitObjects;
        this.OverallDifficulty = OverallDifficulty;
        this.ScrollVelocities = ScrollVelocities ?? [];
        this.InitialScrollVelocity = InitialScrollVelocity;
        this.ScrollSpeedFactors = ScrollSpeedFactors ?? [];
        this.ScrollProfiles = ScrollProfiles
                              ?? new Dictionary<string, YokkoScrollProfile>();
    }

    public string Title { get; init; }
    public string Artist { get; init; }
    public string Creator { get; init; }
    public string DifficultyName { get; init; }
    public KeyMode KeyMode { get; init; }
    public ChartSourceFormat SourceFormat { get; init; }
    public IReadOnlyList<YokkoTimingPoint> TimingPoints { get; init; }
    public string? AudioPath { get; init; }
    public IReadOnlyList<YokkoHitObject> HitObjects { get; init; }
    public double OverallDifficulty { get; }
    public IReadOnlyList<YokkoScrollVelocity> ScrollVelocities { get; init; }
    public double InitialScrollVelocity { get; init; }
    public IReadOnlyList<YokkoScrollSpeedFactor> ScrollSpeedFactors { get; init; }
    public IReadOnlyDictionary<string, YokkoScrollProfile> ScrollProfiles { get; init; }

    public int NoteCount => HitObjects.Count(static hitObject => hitObject.Kind is HitObjectKind.Tap or HitObjectKind.Hold);
}
