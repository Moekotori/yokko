namespace Yokko.Core.Scoring;

public sealed class JudgementCounter
{
    public int Perfect { get; private set; }
    public int Great { get; private set; }
    public int Good { get; private set; }
    public int Bad { get; private set; }
    public int Miss { get; private set; }

    public int Total => Perfect + Great + Good + Bad + Miss;

    public double WeightedAccuracy
    {
        get
        {
            if (Total == 0)
                return 1;

            double weighted = Perfect + Great * 0.8 + Good * 0.5 + Bad * 0.2;
            return weighted / Total;
        }
    }

    public void Add(JudgementRating rating)
    {
        switch (rating)
        {
            case JudgementRating.Perfect:
                Perfect++;
                break;

            case JudgementRating.Great:
                Great++;
                break;

            case JudgementRating.Good:
                Good++;
                break;

            case JudgementRating.Bad:
                Bad++;
                break;

            case JudgementRating.Miss:
                Miss++;
                break;
        }
    }
}
