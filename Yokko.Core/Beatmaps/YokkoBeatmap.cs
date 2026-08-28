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
        IReadOnlyDictionary<string, YokkoScrollProfile>? ScrollProfiles = null,
        double DrainRate = 5,
        ManiaConversionSource? ConversionSource = null,
        int StageCount = 1,
        double PreviewTimeMilliseconds = -1,
        double LocalOffsetMilliseconds = 0,
        IReadOnlyList<YokkoBreakPeriod>? BreakPeriods = null,
        bool LegacyLongNoteRendering = false,
        IReadOnlyList<YokkoScheduledSample>? ScheduledSamples = null,
        int? ScratchLane = null,
        BmsJudgementMetadata? BmsJudgement = null,
        string? RomanisedTitle = null,
        string? RomanisedArtist = null,
        string Source = "",
        string Tags = "",
        int OnlineBeatmapId = -1,
        int OnlineBeatmapSetId = -1)
    {
        if (!double.IsFinite(OverallDifficulty)
            || OverallDifficulty is < -15 or > 15)
        {
            throw new ArgumentOutOfRangeException(
                nameof(OverallDifficulty),
                "Overall difficulty must be finite and between -15 and 15.");
        }

        if (!double.IsFinite(InitialScrollVelocity))
            throw new ArgumentOutOfRangeException(nameof(InitialScrollVelocity));

        if (!double.IsFinite(DrainRate)
            || DrainRate is < 0 or > 11)
        {
            throw new ArgumentOutOfRangeException(
                nameof(DrainRate),
                "Drain rate must be finite and between 0 and 11.");
        }

        if (!double.IsFinite(PreviewTimeMilliseconds))
            throw new ArgumentOutOfRangeException(nameof(PreviewTimeMilliseconds));

        if (!double.IsFinite(LocalOffsetMilliseconds))
            throw new ArgumentOutOfRangeException(nameof(LocalOffsetMilliseconds));

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
        this.DrainRate = DrainRate;
        this.ConversionSource = ConversionSource;
        if (StageCount is < 1 or > 2
            || (int)KeyMode % StageCount != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(StageCount));
        }
        if (ScratchLane is int scratchLane
            && (SourceFormat is not (
                    ChartSourceFormat.Bms
                    or ChartSourceFormat.Lr2Bms)
                || scratchLane < 0
                || scratchLane >= (int)KeyMode))
        {
            throw new ArgumentOutOfRangeException(
                nameof(ScratchLane),
                "Only BMS charts may identify a scratch lane inside the playfield.");
        }
        this.StageCount = StageCount;
        this.PreviewTimeMilliseconds = PreviewTimeMilliseconds;
        this.LocalOffsetMilliseconds = LocalOffsetMilliseconds;
        this.BreakPeriods = BreakPeriods ?? [];
        this.LegacyLongNoteRendering = LegacyLongNoteRendering;
        this.ScheduledSamples = ScheduledSamples ?? [];
        this.ScratchLane = ScratchLane;
        if (BmsJudgement is { } bmsJudgement
            && (SourceFormat is not (
                    ChartSourceFormat.Bms
                    or ChartSourceFormat.Lr2Bms)
                || !double.IsFinite(bmsJudgement.WindowMultiplier)
                || bmsJudgement.WindowMultiplier < 0
                || bmsJudgement.RegularKeysPerStage is not (null or 5 or 7)))
        {
            throw new ArgumentOutOfRangeException(nameof(BmsJudgement));
        }
        this.BmsJudgement = BmsJudgement;
        this.RomanisedTitle = string.IsNullOrWhiteSpace(RomanisedTitle)
            ? Title
            : RomanisedTitle;
        this.RomanisedArtist = string.IsNullOrWhiteSpace(RomanisedArtist)
            ? Artist
            : RomanisedArtist;
        this.Source = Source ?? string.Empty;
        this.Tags = Tags ?? string.Empty;
        this.OnlineBeatmapId = OnlineBeatmapId;
        this.OnlineBeatmapSetId = OnlineBeatmapSetId;
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
    public double DrainRate { get; }

    /// <summary>
    /// Original non-Mania objects, when this chart was generated through the
    /// Mania converter. Null means the source was already a lane ruleset.
    /// </summary>
    public ManiaConversionSource? ConversionSource { get; init; }

    public int StageCount { get; init; }

    /// <summary>
    /// Preferred song-select preview position, or a negative value when the
    /// source chart did not provide one.
    /// </summary>
    public double PreviewTimeMilliseconds { get; init; }

    /// <summary>
    /// Beatmap-specific audio offset in milliseconds. Positive values delay
    /// audio relative to the chart timeline.
    /// </summary>
    public double LocalOffsetMilliseconds { get; init; }

    public IReadOnlyList<YokkoBreakPeriod> BreakPeriods { get; init; }

    /// <summary>
    /// Uses endpoint-only hold-body bounds instead of including scroll
    /// direction extrema. This preserves Quaver's LegacyLNRendering mode.
    /// </summary>
    public bool LegacyLongNoteRendering { get; init; }

    public IReadOnlyList<YokkoScheduledSample> ScheduledSamples { get; init; }

    /// <summary>
    /// The playable BMS turntable lane, or null for ordinary key charts.
    /// Double-play BMS charts expose both turntables through
    /// <see cref="ScratchLanes"/> while retaining null here for compatibility.
    /// </summary>
    public int? ScratchLane { get; init; }

    public BmsJudgementMetadata? BmsJudgement { get; init; }

    /// <summary>
    /// Playable BMS turntable lanes. BMS DP is imported stage-by-stage, so its
    /// two turntables are the first lane of each six- or eight-lane stage.
    /// </summary>
    public IReadOnlyList<int> ScratchLanes => ScratchLane is int scratchLane
        ? [scratchLane]
        : SourceFormat is ChartSourceFormat.Bms or ChartSourceFormat.Lr2Bms
          && StageCount == 2
          && KeysPerStage is 6 or 8
            ? [0, KeysPerStage]
            : [];

    public string RomanisedTitle { get; init; }

    public string RomanisedArtist { get; init; }

    public string Source { get; init; }

    public string Tags { get; init; }

    public int OnlineBeatmapId { get; init; }

    public int OnlineBeatmapSetId { get; init; }

    public int KeysPerStage => (int)KeyMode / StageCount;

    public int RegularLaneCount => (int)KeyMode - ScratchLanes.Count;

    public int NoteCount => HitObjects.Count(static hitObject => hitObject.Kind is HitObjectKind.Tap or HitObjectKind.Hold);
}
