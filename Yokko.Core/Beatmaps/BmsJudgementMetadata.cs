namespace Yokko.Core.Beatmaps;

public enum BmsJudgementRankSource
{
    Default,
    Rank,
    DefExRank,
}

/// <summary>
/// Effective beatoraja judge-rank metadata retained from a BMS chart.
/// The multiplier is relative to beatoraja's EASY 7KEY windows.
/// </summary>
public readonly record struct BmsJudgementMetadata(
    double WindowMultiplier,
    BmsJudgementRankSource Source,
    int Value,
    int? RegularKeysPerStage = null)
{
    public static BmsJudgementMetadata Default { get; } =
        FromRank(2, BmsJudgementRankSource.Default);

    public static BmsJudgementMetadata FromRank(
        int rank,
        BmsJudgementRankSource source = BmsJudgementRankSource.Rank)
    {
        if (rank is < 0 or > 4)
            throw new ArgumentOutOfRangeException(nameof(rank));

        return new BmsJudgementMetadata(
            (rank + 1) * 0.25,
            source,
            rank);
    }

    public static BmsJudgementMetadata FromDefExRank(int rank)
    {
        if (rank <= 0)
            throw new ArgumentOutOfRangeException(nameof(rank));

        // beatoraja normalises DEFEXRANK through Java integer arithmetic
        // before creating the windows. DEFEXRANK 100 is NORMAL (75%), while
        // values such as 101 still normalise to 75 rather than 75.75.
        int effectivePercentage = (int)((long)rank * 75 / 100);
        return new BmsJudgementMetadata(
            effectivePercentage / 100d,
            BmsJudgementRankSource.DefExRank,
            rank);
    }

    public string DisplayLabel => Source switch
    {
        BmsJudgementRankSource.DefExRank => $"DEFEXRANK {Value}",
        _ => Value switch
        {
            0 => "VERY HARD",
            1 => "HARD",
            2 => "NORMAL",
            3 => "EASY",
            4 => "VERY EASY",
            _ => "CUSTOM",
        },
    };
}
