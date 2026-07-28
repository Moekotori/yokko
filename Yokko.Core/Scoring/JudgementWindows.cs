namespace Yokko.Core.Scoring;

/// <summary>
/// osu!lazer mania hit windows.
/// Ported from ppy/osu
/// osu.Game.Rulesets.Mania/Scoring/ManiaHitWindows.cs
/// commit cb3d5da8b441afd8d2cf3e03ceebc6b027e2074d (MIT).
/// </summary>
public sealed class JudgementWindows
{
    private readonly record struct DifficultyRange(
        double Minimum,
        double Average,
        double Maximum);

    private static readonly DifficultyRange perfectRange = new(22.4, 19.4, 13.9);
    private static readonly DifficultyRange greatRange = new(64, 49, 34);
    private static readonly DifficultyRange goodRange = new(97, 82, 67);
    private static readonly DifficultyRange okRange = new(127, 112, 97);
    private static readonly DifficultyRange mehRange = new(151, 136, 121);
    private static readonly DifficultyRange missRange = new(188, 173, 158);

    public JudgementWindows(
        double overallDifficulty = 5,
        double speedMultiplier = 1,
        double difficultyMultiplier = 1)
    {
        if (!double.IsFinite(overallDifficulty)
            || overallDifficulty is < 0 or > 10)
        {
            throw new ArgumentOutOfRangeException(nameof(overallDifficulty));
        }

        if (!double.IsFinite(speedMultiplier) || speedMultiplier <= 0)
            throw new ArgumentOutOfRangeException(nameof(speedMultiplier));

        if (!double.IsFinite(difficultyMultiplier) || difficultyMultiplier <= 0)
            throw new ArgumentOutOfRangeException(nameof(difficultyMultiplier));

        OverallDifficulty = overallDifficulty;
        SpeedMultiplier = speedMultiplier;
        DifficultyMultiplier = difficultyMultiplier;

        double totalMultiplier = speedMultiplier / difficultyMultiplier;
        PerfectMilliseconds = windowFor(perfectRange, totalMultiplier);
        GreatMilliseconds = windowFor(greatRange, totalMultiplier);
        GoodMilliseconds = windowFor(goodRange, totalMultiplier);
        OkMilliseconds = windowFor(okRange, totalMultiplier);
        MehMilliseconds = windowFor(mehRange, totalMultiplier);
        MissMilliseconds = windowFor(missRange, totalMultiplier);
    }

    public static JudgementWindows DefaultMania { get; } = new(5);

    public double OverallDifficulty { get; }

    public double SpeedMultiplier { get; }

    public double DifficultyMultiplier { get; }

    public double PerfectMilliseconds { get; }

    public double GreatMilliseconds { get; }

    public double GoodMilliseconds { get; }

    public double OkMilliseconds { get; }

    public double MehMilliseconds { get; }

    public double MissMilliseconds { get; }

    public JudgementRating Judge(double hitErrorMilliseconds)
    {
        double absoluteError = Math.Abs(hitErrorMilliseconds);

        if (absoluteError <= PerfectMilliseconds)
            return JudgementRating.Perfect;

        if (absoluteError <= GreatMilliseconds)
            return JudgementRating.Great;

        if (absoluteError <= GoodMilliseconds)
            return JudgementRating.Good;

        if (absoluteError <= OkMilliseconds)
            return JudgementRating.Ok;

        if (absoluteError <= MehMilliseconds)
            return JudgementRating.Meh;

        if (absoluteError <= MissMilliseconds)
            return JudgementRating.Miss;

        return JudgementRating.None;
    }

    public double WindowFor(JudgementRating rating) => rating switch
    {
        JudgementRating.Perfect => PerfectMilliseconds,
        JudgementRating.Great => GreatMilliseconds,
        JudgementRating.Good => GoodMilliseconds,
        JudgementRating.Ok => OkMilliseconds,
        JudgementRating.Meh => MehMilliseconds,
        JudgementRating.Miss => MissMilliseconds,
        _ => throw new ArgumentOutOfRangeException(nameof(rating), rating, null),
    };

    public bool CanBeHit(double hitErrorMilliseconds)
        => hitErrorMilliseconds <= MehMilliseconds;

    private double windowFor(DifficultyRange range, double multiplier)
        => Math.Floor(difficultyRange(OverallDifficulty, range) * multiplier) + 0.5;

    private static double difficultyRange(double difficulty, DifficultyRange range)
    {
        return difficulty > 5
            ? range.Average + (range.Maximum - range.Average) * (difficulty - 5) / 5
            : range.Minimum + (range.Average - range.Minimum) * difficulty / 5;
    }
}
