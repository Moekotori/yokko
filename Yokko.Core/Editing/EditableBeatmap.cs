using Yokko.Core.Beatmaps;
using Yokko.Core.Gameplay;

namespace Yokko.Core.Editing;

public sealed class EditableBeatmap
{
    private readonly List<EditableNote> notes = [];

    private EditableBeatmap(KeyMode keyMode)
    {
        KeyMode = keyMode;
    }

    public string Title { get; set; } = "Untitled Yokko Chart";

    public string Artist { get; set; } = "Unknown Artist";

    public string Creator { get; set; } = "Yokko";

    public string DifficultyName { get; set; } = "Draft";

    public KeyMode KeyMode { get; }

    public int LaneCount => (int)KeyMode;

    public int Rows { get; } = 32;

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

    public bool HasNoteAt(int lane, int row)
        => notes.Any(note => note.Lane == lane && note.Row == row);

    public void ToggleNote(int lane, int row)
    {
        int existingIndex = notes.FindIndex(note => note.Lane == lane && note.Row == row);

        if (existingIndex >= 0)
        {
            notes.RemoveAt(existingIndex);
            return;
        }

        notes.Add(new EditableNote(lane, row, row * StepMilliseconds));
        notes.Sort(static (left, right) =>
        {
            int timeComparison = left.StartTimeMilliseconds.CompareTo(right.StartTimeMilliseconds);
            return timeComparison != 0 ? timeComparison : left.Lane.CompareTo(right.Lane);
        });
    }

    public YokkoBeatmap ToBeatmap()
        => new(
            Title,
            Artist,
            Creator,
            DifficultyName,
            KeyMode,
            ChartSourceFormat.Yokko,
            null,
            notes.Select(note => new YokkoHitObject(
                    note.Lane,
                    note.StartTimeMilliseconds,
                    null,
                    HitObjectKind.Tap))
                 .ToArray());
}
