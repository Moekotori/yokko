using Yokko.Core.Beatmaps;
using Yokko.Core.Gameplay;
using Yokko.Core.Timing;

namespace Yokko.Core.Editing;

public sealed class EditableBeatmap
{
    private readonly List<EditableNote> notes = [];
    private double overallDifficulty = 5;
    private double drainRate = 5;

    private EditableBeatmap(
        KeyMode keyMode,
        IReadOnlyList<YokkoTimingPoint> timingPoints,
        IReadOnlyList<YokkoScrollVelocity>? scrollVelocities = null,
        double initialScrollVelocity = 1,
        IReadOnlyList<YokkoScrollSpeedFactor>? scrollSpeedFactors = null,
        IReadOnlyDictionary<string, YokkoScrollProfile>? scrollProfiles = null,
        int rows = 32)
    {
        KeyMode = keyMode;
        TimingPoints = timingPoints;
        ScrollVelocities = scrollVelocities ?? [];
        InitialScrollVelocity = initialScrollVelocity;
        ScrollSpeedFactors = scrollSpeedFactors ?? [];
        ScrollProfiles = scrollProfiles
                         ?? new Dictionary<string, YokkoScrollProfile>();
        TimingMap = new BeatTimingMap(timingPoints, BeatDivisor);
        Rows = rows;
    }

    public string Title { get; set; } = "Untitled Yokko Chart";

    public string Artist { get; set; } = "Unknown Artist";

    public string Creator { get; set; } = "Yokko";

    public string DifficultyName { get; set; } = "Draft";

    public double OverallDifficulty
    {
        get => overallDifficulty;
        set
        {
            if (!double.IsFinite(value) || value is < 0 or > 10)
                throw new ArgumentOutOfRangeException(nameof(value));

            overallDifficulty = value;
        }
    }

    public double DrainRate
    {
        get => drainRate;
        set
        {
            if (!double.IsFinite(value) || value is < 0 or > 10)
                throw new ArgumentOutOfRangeException(nameof(value));

            drainRate = value;
        }
    }

    public string? AudioPath { get; set; }

    public string? SourcePath { get; set; }

    public KeyMode KeyMode { get; }

    public int LaneCount => (int)KeyMode;

    public int Rows { get; private set; }

    public int BeatDivisor { get; } = 4;

    public IReadOnlyList<YokkoTimingPoint> TimingPoints { get; }

    public IReadOnlyList<YokkoScrollVelocity> ScrollVelocities { get; }

    public double InitialScrollVelocity { get; }

    public IReadOnlyList<YokkoScrollSpeedFactor> ScrollSpeedFactors { get; }

    public IReadOnlyDictionary<string, YokkoScrollProfile> ScrollProfiles { get; }

    public BeatTimingMap TimingMap { get; }

    public IReadOnlyList<EditableNote> Notes => notes;

    public static EditableBeatmap Create(KeyMode keyMode) => new(keyMode, [YokkoTimingPoint.Default])
    {
        DifficultyName = $"{(int)keyMode}K Draft",
    };

    public static EditableBeatmap FromBeatmap(YokkoBeatmap beatmap, string? sourcePath = null)
    {
        IReadOnlyList<YokkoTimingPoint> timingPoints = beatmap.TimingPoints.Count == 0
            ? [YokkoTimingPoint.Default]
            : beatmap.TimingPoints.ToArray();
        var timingMap = new BeatTimingMap(timingPoints);
        int rows = Math.Max(32, beatmap.HitObjects.Count == 0
            ? 32
            : timingMap.ClosestRowAt(beatmap.HitObjects.Max(hitObject => hitObject.EndTimeMilliseconds ?? hitObject.StartTimeMilliseconds)) + 4);

        var editable = new EditableBeatmap(
            beatmap.KeyMode,
            timingPoints,
            beatmap.ScrollVelocities.ToArray(),
            beatmap.InitialScrollVelocity,
            beatmap.ScrollSpeedFactors.ToArray(),
            new Dictionary<string, YokkoScrollProfile>(
                beatmap.ScrollProfiles),
            rows)
        {
            Title = beatmap.Title,
            Artist = beatmap.Artist,
            Creator = beatmap.Creator,
            DifficultyName = beatmap.DifficultyName,
            OverallDifficulty = beatmap.OverallDifficulty,
            DrainRate = beatmap.DrainRate,
            AudioPath = beatmap.AudioPath,
            SourcePath = sourcePath,
        };

        foreach (YokkoHitObject hitObject in beatmap.HitObjects)
        {
            if (hitObject.Kind is not (HitObjectKind.Tap or HitObjectKind.Hold))
                continue;

            editable.notes.Add(new EditableNote(
                hitObject.Lane,
                editable.ClosestRowAt(hitObject.StartTimeMilliseconds),
                hitObject.StartTimeMilliseconds,
                hitObject.EndTimeMilliseconds,
                hitObject.Kind,
                hitObject.ScrollProfileId));
        }

        editable.sortNotes();
        return editable;
    }

    public bool HasNoteAt(int lane, int row)
        => notes.Any(note => note.Lane == lane && note.Row == row);

    public void AppendRows(int rowCount)
    {
        if (rowCount <= 0)
            return;

        EnsureRows(Rows + rowCount);
    }

    public void EnsureRows(int rows)
    {
        if (rows > Rows)
            Rows = rows;
    }

    public void ToggleNote(int lane, int row)
    {
        if (lane < 0 || lane >= LaneCount)
            throw new ArgumentOutOfRangeException(nameof(lane));

        if (row < 0)
            throw new ArgumentOutOfRangeException(nameof(row));

        int existingIndex = notes.FindIndex(note => note.Lane == lane && note.Row == row);

        if (existingIndex >= 0)
        {
            notes.RemoveAt(existingIndex);
            return;
        }

        EnsureRows(row + 1);
        notes.Add(new EditableNote(lane, row, TimeAtRow(row), null, HitObjectKind.Tap));
        sortNotes();
    }

    public double TimeAtRow(int row) => TimingMap.TimeAtRow(row);

    public int ClosestRowAt(double timeMilliseconds) => TimingMap.ClosestRowAt(timeMilliseconds);

    public YokkoBeatmap ToBeatmap()
        => new(
            Title,
            Artist,
            Creator,
            DifficultyName,
            KeyMode,
            ChartSourceFormat.Yokko,
            TimingPoints,
            AudioPath,
            notes.Select(note => new YokkoHitObject(
                    note.Lane,
                    note.StartTimeMilliseconds,
                    note.EndTimeMilliseconds,
                    note.Kind,
                    ScrollProfileId: note.ScrollProfileId))
                 .ToArray(),
            OverallDifficulty,
            ScrollVelocities,
            InitialScrollVelocity,
            ScrollSpeedFactors,
            ScrollProfiles,
            DrainRate);

    private void sortNotes()
    {
        notes.Sort(static (left, right) =>
        {
            int timeComparison = left.StartTimeMilliseconds.CompareTo(right.StartTimeMilliseconds);
            return timeComparison != 0 ? timeComparison : left.Lane.CompareTo(right.Lane);
        });
    }
}
