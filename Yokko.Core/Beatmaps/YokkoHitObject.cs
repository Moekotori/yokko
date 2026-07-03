namespace Yokko.Core.Beatmaps;

public sealed record YokkoHitObject(
    int Lane,
    double StartTimeMilliseconds,
    double? EndTimeMilliseconds,
    HitObjectKind Kind,
    string? SampleKey = null);
