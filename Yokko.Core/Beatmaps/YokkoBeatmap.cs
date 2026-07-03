using Yokko.Core.Gameplay;

namespace Yokko.Core.Beatmaps;

public sealed record YokkoBeatmap(
    string Title,
    string Artist,
    string Creator,
    string DifficultyName,
    KeyMode KeyMode,
    ChartSourceFormat SourceFormat,
    string? AudioPath,
    IReadOnlyList<YokkoHitObject> HitObjects)
{
    public int NoteCount => HitObjects.Count(static hitObject => hitObject.Kind is HitObjectKind.Tap or HitObjectKind.Hold);
}
