using Yokko.Core.Beatmaps;
using Yokko.Core.Gameplay;

namespace Yokko.Core.Editing;

public sealed class EditableBeatmap
{
    private readonly List<EditableNote> notes = [];

    private EditableBeatmap(KeyMode keyMode, int rows = 32)
    {
        KeyMode = keyMode;
        Rows = rows;
    }

    public string Title { get; set; } = "Untitled Yokko Chart";

    public string Artist { get; set; } = "Unknown Artist";

    public string Creator { get; set; } = "Yokko";

    public string DifficultyName { get; set; } = "Draft";

    public string? AudioPath { get; set; }

    public string? SourcePath { get; set; }

    public KeyMode KeyMode { get; }

    public int LaneCount => (int)KeyMode;

    public int Rows { get; private set; }

    public double StepMilliseconds { get; } = 125;

    public IReadOnlyList<EditableNote> Notes => notes;

    public static EditableBeatmap Create(KeyMode keyMode) => new(keyMode)
    {
        DifficultyName = keyMode switch
        {
            KeyMode.FourKey => "4K Draft",
            KeyMode.SevenKey => "7K Draft",
            _ => "Draft",
        },
    };

    public static EditableBeatmap FromBeatmap(YokkoBeatmap beatmap, string? sourcePath = null)
    {
        int rows = Math.Max(32, beatmap.HitObjects.Count == 0
            ? 32
            : (int)Math.Ceiling(beatmap.HitObjects.Max(hitObject => hitObject.EndTimeMilliseconds ?? hitObject.StartTimeMilliseconds) / 125d) + 4);

        var editable = new EditableBeatmap(beatmap.KeyMode, rows)
        {
            Title = beatmap.Title,
            Artist = beatmap.Artist,
            Creator = beatmap.Creator,
            DifficultyName = beatmap.DifficultyName,
            AudioPath = beatmap.AudioPath,
            SourcePath = sourcePath,
        };

        foreach (YokkoHitObject hitObject in beatmap.HitObjects)
        {
            if (hitObject.Kind is not (HitObjectKind.Tap or HitObjectKind.Hold))
                continue;

            editable.notes.Add(new EditableNote(
                hitObject.Lane,
                editable.timeToRow(hitObject.StartTimeMilliseconds),
                hitObject.StartTimeMilliseconds,
                hitObject.EndTimeMilliseconds,
                hitObject.Kind));
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
        notes.Add(new EditableNote(lane, row, rowToTime(row), null, HitObjectKind.Tap));
        sortNotes();
    }

    public YokkoBeatmap ToBeatmap()
        => new(
            Title,
            Artist,
            Creator,
            DifficultyName,
            KeyMode,
            ChartSourceFormat.Yokko,
            AudioPath,
            notes.Select(note => new YokkoHitObject(
                    note.Lane,
                    note.StartTimeMilliseconds,
                    note.EndTimeMilliseconds,
                    note.Kind))
                 .ToArray());

    private double rowToTime(int row) => row * StepMilliseconds;

    private int timeToRow(double timeMilliseconds) => Math.Max(0, (int)Math.Round(timeMilliseconds / StepMilliseconds));

    private void sortNotes()
    {
        notes.Sort(static (left, right) =>
        {
            int timeComparison = left.StartTimeMilliseconds.CompareTo(right.StartTimeMilliseconds);
            return timeComparison != 0 ? timeComparison : left.Lane.CompareTo(right.Lane);
        });
    }
}
