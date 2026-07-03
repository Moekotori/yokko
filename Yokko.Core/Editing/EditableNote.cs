namespace Yokko.Core.Editing;

using Yokko.Core.Beatmaps;

public sealed record EditableNote(
    int Lane,
    int Row,
    double StartTimeMilliseconds,
    double? EndTimeMilliseconds,
    HitObjectKind Kind);
