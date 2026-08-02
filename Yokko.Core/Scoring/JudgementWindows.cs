namespace Yokko.Core.Scoring;

/// <summary>
/// osu!lazer mania hit windows.
/// Ported from ppy/osu
/// osu.Game.Rulesets.Mania/Scoring/ManiaHitWindows.cs
/// commit 9f227ed28b6c8ba46dfea1f000f778d8b2827ad0 (MIT).
/// </summary>
public sealed class JudgementWindows
{
    private const double etternaW1Milliseconds = 22.5;
    private const double etternaW2Milliseconds = 45;
    private const double etternaW3Milliseconds = 90;
    private const double etternaW4Milliseconds = 135;
    private const double etternaMissBoundaryMilliseconds = 180;

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
        double difficultyMultiplier = 1,
        bool classic = false,
        bool scoreV2 = false,
        bool isConvert = false,
        JudgementConfiguration? configuration = null,
        double bmsJudgeWindowMultiplier = 0.75,
        int bmsRegularKeysPerStage = 7)
    {
        if (!double.IsFinite(overallDifficulty)
            || overallDifficulty is < -15 or > 15)
        {
            throw new ArgumentOutOfRangeException(nameof(overallDifficulty));
        }

        if (!double.IsFinite(speedMultiplier) || speedMultiplier <= 0)
            throw new ArgumentOutOfRangeException(nameof(speedMultiplier));

        if (!double.IsFinite(difficultyMultiplier) || difficultyMultiplier <= 0)
            throw new ArgumentOutOfRangeException(nameof(difficultyMultiplier));
        if (!double.IsFinite(bmsJudgeWindowMultiplier)
            || bmsJudgeWindowMultiplier <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(bmsJudgeWindowMultiplier));
        }

        OverallDifficulty = overallDifficulty;
        SpeedMultiplier = speedMultiplier;
        DifficultyMultiplier = difficultyMultiplier;
        Classic = classic;
        ScoreV2 = scoreV2;
        IsConvert = isConvert;
        Configuration =
            configuration ?? JudgementConfiguration.YokkoDefault;
        BmsJudgeWindowMultiplier = bmsJudgeWindowMultiplier;
        BmsRegularKeysPerStage = bmsRegularKeysPerStage == 5 ? 5 : 7;

        double totalMultiplier = speedMultiplier / difficultyMultiplier;
        if (Configuration.Mode == JudgementMode.BmsBeatoraja)
        {
            double bmsMultiplier =
                bmsJudgeWindowMultiplier * speedMultiplier;
            PerfectMilliseconds = 20 * bmsMultiplier;
            GreatMilliseconds = (BmsRegularKeysPerStage == 5 ? 50 : 60)
                                * bmsMultiplier;
            GoodMilliseconds = (BmsRegularKeysPerStage == 5 ? 100 : 150)
                               * bmsMultiplier;
            OkMilliseconds = (BmsRegularKeysPerStage == 5 ? 150 : 280)
                             * bmsMultiplier;
            MehMilliseconds = OkMilliseconds;
            MissMilliseconds = 500 * speedMultiplier;
        }
        else if (Configuration.Mode == JudgementMode.OsuStable)
        {
            // osu!stable ScoreV1 mania windows. Stable truncates each formula
            // to an integer and accepts rounded hit errors inclusively, which
            // produces the observable half-millisecond boundaries below.
            // Source: osu! wiki, Gameplay/Judgement/osu!mania (CC BY-NC-SA 4.0),
            // and ppy/osu ManiaHitWindows at commit
            // 9f227ed28b6c8ba46dfea1f000f778d8b2827ad0 (MIT).
            // Stable leaves the chart-time windows unchanged for DT/NC/HT.
            // The faster/slower clock rate alone changes their real-time
            // duration. Difficulty multipliers (notably HR) still apply.
            double stableMultiplier = 1 / difficultyMultiplier;
            PerfectMilliseconds = classicWindow(16, stableMultiplier);
            if (isConvert)
            {
                GreatMilliseconds = classicWindow(
                    Math.Round(overallDifficulty) > 4 ? 34 : 47,
                    stableMultiplier);
                GoodMilliseconds = classicWindow(
                    Math.Round(overallDifficulty) > 4 ? 67 : 77,
                    stableMultiplier);
                OkMilliseconds = classicWindow(97, stableMultiplier);
                MehMilliseconds = classicWindow(121, stableMultiplier);
                MissMilliseconds = classicWindow(158, stableMultiplier);
            }
            else
            {
                double invertedOd = Math.Clamp(
                    10 - overallDifficulty,
                    0,
                    10);
                GreatMilliseconds = classicWindow(
                    34 + 3 * invertedOd,
                    stableMultiplier);
                GoodMilliseconds = classicWindow(
                    67 + 3 * invertedOd,
                    stableMultiplier);
                OkMilliseconds = classicWindow(
                    97 + 3 * invertedOd,
                    stableMultiplier);
                MehMilliseconds = classicWindow(
                    121 + 3 * invertedOd,
                    stableMultiplier);
                MissMilliseconds = classicWindow(
                    158 + 3 * invertedOd,
                    stableMultiplier);
            }
        }
        else if (Configuration.Mode == JudgementMode.Quaver)
        {
            // Quaver's standard 4K/7K windows. Playback-rate scaling is
            // applied by Quaver's score processor to every window.
            PerfectMilliseconds = 18 * totalMultiplier;
            GreatMilliseconds = 43 * totalMultiplier;
            GoodMilliseconds = 76 * totalMultiplier;
            OkMilliseconds = 106 * totalMultiplier;
            MehMilliseconds = 127 * totalMultiplier;
            MissMilliseconds = 164 * totalMultiplier;
        }
        else if (Configuration.Mode == JudgementMode.Etterna)
        {
            // Etterna scales W1-W4 by the selected judge. W5 input and the
            // automatic miss boundary stay at 180 ms for every judge because
            // Player::Step takes max(scaled W5, MISS_WINDOW_BEGIN_SEC).
            // Source: etternagame/etterna Player.cpp and GameState.h
            // commit 939a26ae042d3a689999a0dae630721c7701f187 (MIT).
            double judgeMultiplier =
                Configuration.EtternaTimingScale * speedMultiplier;
            PerfectMilliseconds =
                etternaW1Milliseconds * judgeMultiplier;
            GreatMilliseconds =
                etternaW2Milliseconds * judgeMultiplier;
            GoodMilliseconds =
                etternaW3Milliseconds * judgeMultiplier;
            OkMilliseconds =
                etternaW4Milliseconds * judgeMultiplier;
            MehMilliseconds =
                etternaMissBoundaryMilliseconds * speedMultiplier;
            MissMilliseconds = MehMilliseconds;
        }
        else if (classic && !scoreV2)
        {
            if (isConvert)
            {
                PerfectMilliseconds = classicWindow(16, totalMultiplier);
                GreatMilliseconds = classicWindow(
                    Math.Round(overallDifficulty) > 4 ? 34 : 47,
                    totalMultiplier);
                GoodMilliseconds = classicWindow(
                    Math.Round(overallDifficulty) > 4 ? 67 : 77,
                    totalMultiplier);
                OkMilliseconds = classicWindow(97, totalMultiplier);
                MehMilliseconds = classicWindow(121, totalMultiplier);
                MissMilliseconds = classicWindow(158, totalMultiplier);
            }
            else
            {
                double invertedOd = Math.Clamp(10 - overallDifficulty, 0, 10);
                PerfectMilliseconds = classicWindow(16, totalMultiplier);
                GreatMilliseconds = classicWindow(
                    34 + 3 * invertedOd,
                    totalMultiplier);
                GoodMilliseconds = classicWindow(
                    67 + 3 * invertedOd,
                    totalMultiplier);
                OkMilliseconds = classicWindow(
                    97 + 3 * invertedOd,
                    totalMultiplier);
                MehMilliseconds = classicWindow(
                    121 + 3 * invertedOd,
                    totalMultiplier);
                MissMilliseconds = classicWindow(
                    158 + 3 * invertedOd,
                    totalMultiplier);
            }
        }
        else
        {
            PerfectMilliseconds = windowFor(perfectRange, totalMultiplier);
            GreatMilliseconds = windowFor(greatRange, totalMultiplier);
            GoodMilliseconds = windowFor(goodRange, totalMultiplier);
            OkMilliseconds = windowFor(okRange, totalMultiplier);
            MehMilliseconds = windowFor(mehRange, totalMultiplier);
            MissMilliseconds = windowFor(missRange, totalMultiplier);
        }
    }

    public static JudgementWindows DefaultMania { get; } = new(5);

    public double OverallDifficulty { get; }

    public double SpeedMultiplier { get; }

    public double DifficultyMultiplier { get; }

    public bool Classic { get; }

    public bool ScoreV2 { get; }

    public bool IsConvert { get; }

    public JudgementConfiguration Configuration { get; }

    public double BmsJudgeWindowMultiplier { get; }

    public int BmsRegularKeysPerStage { get; }

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

    private static double classicWindow(double value, double multiplier) =>
        Math.Floor(value * multiplier) + 0.5;

    private static double difficultyRange(double difficulty, DifficultyRange range)
    {
        return difficulty > 5
            ? range.Average + (range.Maximum - range.Average) * (difficulty - 5) / 5
            : range.Minimum + (range.Average - range.Minimum) * difficulty / 5;
    }
}
