namespace Yokko.Core.Scoring;

public sealed record JudgementEvent(
    int HitObjectIndex,
    int Lane,
    double ObjectTimeMilliseconds,
    double? HitTimeMilliseconds,
    double HitErrorMilliseconds,
    JudgementRating Rating)
{
    public bool IsMiss => Rating == JudgementRating.Miss;
}
