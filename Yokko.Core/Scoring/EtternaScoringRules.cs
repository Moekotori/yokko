namespace Yokko.Core.Scoring;

/// <summary>
/// Gameplay scoring rules used by Etterna's default configuration.
/// Sources:
/// - src/RageUtil/Utils/RageUtil.h (Wife3)
/// - src/Etterna/Models/ScoreKeepers/ScoreKeeperNormal.cpp (combo)
/// - Themes/_fallback/Scripts/03 Gameplay.lua (W3 combo threshold)
/// - Themes/_fallback/metrics.ini (life and mine/hold behaviour)
/// Etterna commit b65660062ef2a23121e331c36e23c23a8f6eafaa (MIT).
/// </summary>
public static class EtternaScoringRules
{
    public const double Wife3MaximumPoints = 2;
    public const double Wife3MissWeight = -5.5;
    public const double Wife3HoldDropWeight = -4.5;
    public const double Wife3MineHitWeight = -7;

    public static bool ContinuesCombo(JudgementRating rating) => rating is
        JudgementRating.Perfect
        or JudgementRating.Great
        or JudgementRating.Good;

    public static bool BreaksCombo(JudgementRating rating) => rating is
        JudgementRating.Ok
        or JudgementRating.Meh
        or JudgementRating.Miss;

    public static double Wife3(
        double hitErrorMilliseconds,
        double timingScale)
    {
        if (!double.IsFinite(hitErrorMilliseconds))
        {
            throw new ArgumentOutOfRangeException(
                nameof(hitErrorMilliseconds));
        }

        if (!double.IsFinite(timingScale) || timingScale <= 0)
            throw new ArgumentOutOfRangeException(nameof(timingScale));

        const double judgePower = 0.75;
        double absoluteError = Math.Abs(hitErrorMilliseconds);
        double perfectRegion = 5 * timingScale;
        if (absoluteError <= perfectRegion)
            return Wife3MaximumPoints;

        double zero = 65 * Math.Pow(timingScale, judgePower);
        double deviation = 22.7 * Math.Pow(timingScale, judgePower);
        if (absoluteError <= zero)
        {
            return Wife3MaximumPoints
                   * errorFunction((zero - absoluteError) / deviation);
        }

        double outerWeightBoundary = 180 * timingScale;
        if (absoluteError <= outerWeightBoundary)
        {
            return (absoluteError - zero)
                   * Wife3MissWeight
                   / (outerWeightBoundary - zero);
        }

        return Wife3MissWeight;
    }

    public static string GradeLabel(double wifeAccuracy)
    {
        // Etterna defaults UseMidGrades to false.
        return wifeAccuracy switch
        {
            >= 0.999935 => "AAAAA",
            >= 0.99955 => "AAAA",
            >= 0.997 => "AAA",
            >= 0.93 => "AA",
            >= 0.8 => "A",
            >= 0.7 => "B",
            >= 0.6 => "C",
            _ => "D",
        };
    }

    public static ScoreRank ApproximateScoreRank(double wifeAccuracy) =>
        wifeAccuracy switch
        {
            >= 0.997 => ScoreRank.X,
            >= 0.93 => ScoreRank.S,
            >= 0.8 => ScoreRank.A,
            >= 0.7 => ScoreRank.B,
            >= 0.6 => ScoreRank.C,
            _ => ScoreRank.D,
        };

    public static double LifeDelta(JudgementEvent judgement)
    {
        ArgumentNullException.ThrowIfNull(judgement);

        if (judgement.Phase == JudgementPhase.Mine)
        {
            return judgement.Rating == JudgementRating.IgnoreMiss
                ? -0.16
                : 0;
        }

        if (judgement.Phase == JudgementPhase.HoldBody)
        {
            return judgement.Rating == JudgementRating.ComboBreak
                ? -0.08
                : judgement.Rating == JudgementRating.IgnoreHit
                    ? 0.008
                    : 0;
        }

        if (judgement.Phase is JudgementPhase.Hold
            or JudgementPhase.HoldTail)
        {
            return 0;
        }

        return judgement.Rating switch
        {
            JudgementRating.Perfect => 0.008,
            JudgementRating.Great => 0.008,
            JudgementRating.Good => 0.004,
            JudgementRating.Ok => 0,
            JudgementRating.Meh => -0.04,
            JudgementRating.Miss => -0.08,
            _ => 0,
        };
    }

    // Abramowitz and Stegun formula 7.1.26, matching Etterna's Wife3 source.
    private static double errorFunction(double value)
    {
        const double a1 = 0.254829592;
        const double a2 = -0.284496736;
        const double a3 = 1.421413741;
        const double a4 = -1.453152027;
        const double a5 = 1.061405429;
        const double p = 0.3275911;

        int sign = value < 0 ? -1 : 1;
        double absoluteValue = Math.Abs(value);
        double t = 1 / (1 + p * absoluteValue);
        double approximation =
            1
            - (((((a5 * t + a4) * t) + a3) * t + a2) * t + a1)
            * t
            * Math.Exp(-absoluteValue * absoluteValue);
        return sign * approximation;
    }
}
