namespace Yokko.Core.Beatmaps;

/// <summary>
/// Ruleset-neutral osu! hit objects retained so Mania conversion Mods can
/// regenerate a chart instead of mutating an already-converted lane chart.
/// </summary>
public sealed record ManiaConversionSource(
    double CircleSize,
    double OverallDifficulty,
    double ApproachRate,
    double DrainRate,
    IReadOnlyList<ManiaConversionHitObject> HitObjects);

public sealed record ManiaConversionHitObject(
    double X,
    double StartTimeMilliseconds,
    double EndTimeMilliseconds,
    ManiaConversionObjectKind Kind,
    int HitSound = 0,
    int SpanCount = 1,
    double Y = 192);

public enum ManiaConversionObjectKind
{
    Circle,
    Slider,
    Spinner,
}
