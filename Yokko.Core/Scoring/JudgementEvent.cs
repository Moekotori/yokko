namespace Yokko.Core.Scoring;

public sealed record JudgementEvent(
    int HitObjectIndex,
    int Lane,
    double ObjectTimeMilliseconds,
    double? HitTimeMilliseconds,
    double HitErrorMilliseconds,
    JudgementRating Rating,
    JudgementPhase Phase = JudgementPhase.Tap)
{
    public bool IsMiss => Rating == JudgementRating.Miss;
}
