using Yokko.Core.Beatmaps;

namespace Yokko.Core.Scoring;

public enum BmsJudgeObjectType
{
    Note,
    Scratch,
    LongNoteEnd,
    LongScratchEnd,
}

/// <summary>
/// beatoraja 5KEY/10KEY and 7KEY/14KEY judgement windows.
/// Ported from exch-bms2/beatoraja JudgeProperty.FIVEKEYS/SEVENKEYS at
/// c2ed5db1a46145ed10790c3872f717e95b59db9d (GPL-3.0-or-later).
/// Values are independently re-expressed here as millisecond boundaries.
/// </summary>
public sealed class BmsJudgementWindows
{
    private readonly double multiplier;
    private readonly int regularKeysPerStage;

    public BmsJudgementWindows(
        BmsJudgementMetadata metadata,
        double speedMultiplier = 1,
        int regularKeysPerStage = 7)
    {
        if (!double.IsFinite(metadata.WindowMultiplier)
            || metadata.WindowMultiplier <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(metadata));
        }
        if (!double.IsFinite(speedMultiplier) || speedMultiplier <= 0)
            throw new ArgumentOutOfRangeException(nameof(speedMultiplier));
        if (regularKeysPerStage is not (5 or 7))
            throw new ArgumentOutOfRangeException(nameof(regularKeysPerStage));

        Metadata = metadata;
        SpeedMultiplier = speedMultiplier;
        this.regularKeysPerStage = regularKeysPerStage;
        multiplier = metadata.WindowMultiplier * speedMultiplier;
    }

    public BmsJudgementMetadata Metadata { get; }

    public double SpeedMultiplier { get; }

    /// <summary>
    /// beatoraja's 5KEYS rule resets combo on an empty MS, while its 7KEYS
    /// rule preserves the current combo without incrementing it.
    /// </summary>
    public bool EmptyPressBreaksCombo => regularKeysPerStage == 5;

    public double LateMissBoundaryMilliseconds(BmsJudgeObjectType type) =>
        baseWindows(type).BadLate * multiplier;

    public double EarlySearchBoundaryMilliseconds(BmsJudgeObjectType type) =>
        Math.Max(
            baseWindows(type).BadEarly * multiplier,
            500 * SpeedMultiplier);

    public double LateSearchBoundaryMilliseconds(BmsJudgeObjectType type) =>
        Math.Max(
            baseWindows(type).BadLate * multiplier,
            (type is BmsJudgeObjectType.Scratch
                or BmsJudgeObjectType.LongScratchEnd
                ? 160
                : 150) * SpeedMultiplier);

    public JudgementRating Judge(
        double hitErrorMilliseconds,
        BmsJudgeObjectType type)
    {
        WindowSet windows = baseWindows(type);
        if (inside(hitErrorMilliseconds, windows.Pgreat, windows.Pgreat))
            return JudgementRating.Perfect;
        if (inside(hitErrorMilliseconds, windows.Great, windows.Great))
            return JudgementRating.Great;
        if (inside(hitErrorMilliseconds, windows.Good, windows.Good))
            return JudgementRating.Good;
        if (inside(hitErrorMilliseconds, windows.BadEarly, windows.BadLate))
            return JudgementRating.Ok;

        return JudgementRating.None;
    }

    public bool IsEmptyPress(
        double hitErrorMilliseconds,
        BmsJudgeObjectType type)
        => IsWithinEmptyPressWindow(hitErrorMilliseconds, type)
           && Judge(hitErrorMilliseconds, type) == JudgementRating.None;

    public bool IsWithinEmptyPressWindow(
        double hitErrorMilliseconds,
        BmsJudgeObjectType type)
    {
        double early = 500 * SpeedMultiplier;
        double late = type is BmsJudgeObjectType.Scratch
            or BmsJudgeObjectType.LongScratchEnd
            ? 160 * SpeedMultiplier
            : 150 * SpeedMultiplier;
        return hitErrorMilliseconds >= -early
               && hitErrorMilliseconds <= late;
    }

    private bool inside(double error, double early, double late) =>
        error >= -early * multiplier && error <= late * multiplier;

    private WindowSet baseWindows(BmsJudgeObjectType type) =>
        (regularKeysPerStage, type) switch
        {
            (5, BmsJudgeObjectType.Note) => new(20, 50, 100, 150, 150),
            (5, BmsJudgeObjectType.Scratch) => new(30, 60, 110, 160, 160),
            (5, BmsJudgeObjectType.LongNoteEnd) =>
                new(120, 150, 200, 250, 250),
            (5, BmsJudgeObjectType.LongScratchEnd) =>
                // The raw GOOD pair is +/-110ms, but JudgeWindowRule.create
                // expands nested windows so it cannot be narrower than GREAT.
                new(130, 160, 160, 260, 260),
            (7, BmsJudgeObjectType.Note) => new(20, 60, 150, 220, 280),
            (7, BmsJudgeObjectType.Scratch) => new(30, 70, 160, 230, 290),
            (7, BmsJudgeObjectType.LongNoteEnd) =>
                new(120, 160, 200, 220, 280),
            (7, BmsJudgeObjectType.LongScratchEnd) =>
                new(130, 170, 210, 230, 290),
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };

    private readonly record struct WindowSet(
        double Pgreat,
        double Great,
        double Good,
        double BadEarly,
        double BadLate);
}
