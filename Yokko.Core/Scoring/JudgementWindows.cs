namespace Yokko.Core.Scoring;

public readonly record struct JudgementWindows(
    double PerfectMilliseconds,
    double GreatMilliseconds,
    double GoodMilliseconds,
    double BadMilliseconds)
{
    public static JudgementWindows DefaultMania { get; } = new(16, 34, 67, 100);

    public JudgementRating Judge(double hitErrorMilliseconds)
    {
        double absoluteError = Math.Abs(hitErrorMilliseconds);

        if (absoluteError <= PerfectMilliseconds)
            return JudgementRating.Perfect;

        if (absoluteError <= GreatMilliseconds)
            return JudgementRating.Great;

        if (absoluteError <= GoodMilliseconds)
            return JudgementRating.Good;

        if (absoluteError <= BadMilliseconds)
            return JudgementRating.Bad;

        return JudgementRating.Miss;
    }
}
