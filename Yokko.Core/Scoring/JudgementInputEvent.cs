namespace Yokko.Core.Scoring;

/// <summary>
/// A real player input matched to a judgement point. This is separate from
/// scoring events because some rulesets defer or passively resolve LN scores.
/// </summary>
public sealed record JudgementInputEvent(
    int HitObjectIndex,
    int Lane,
    double ObjectTimeMilliseconds,
    double HitTimeMilliseconds,
    double HitErrorMilliseconds,
    JudgementRating Rating,
    JudgementPhase Phase,
    double TimingWindowScale = 1);
