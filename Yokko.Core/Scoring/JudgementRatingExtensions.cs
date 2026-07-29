namespace Yokko.Core.Scoring;

/// <summary>
/// Mirrors the gameplay-relevant HitResult semantics from osu!lazer.
/// Source: ppy/osu osu.Game/Rulesets/Scoring/HitResult.cs
/// commit 9f227ed28b6c8ba46dfea1f000f778d8b2827ad0 (MIT).
/// </summary>
public static class JudgementRatingExtensions
{
    public static bool IsHit(this JudgementRating rating) => rating switch
    {
        JudgementRating.None
            or JudgementRating.Miss
            or JudgementRating.IgnoreMiss
            or JudgementRating.ComboBreak => false,
        _ => true,
    };

    public static bool IsMiss(this JudgementRating rating) => rating is
        JudgementRating.Miss
        or JudgementRating.IgnoreMiss
        or JudgementRating.ComboBreak;

    public static bool AffectsCombo(this JudgementRating rating) => rating is
        JudgementRating.Miss
        or JudgementRating.Meh
        or JudgementRating.Ok
        or JudgementRating.Good
        or JudgementRating.Great
        or JudgementRating.Perfect
        or JudgementRating.ComboBreak;

    public static bool IncreasesCombo(this JudgementRating rating)
        => rating.AffectsCombo() && rating.IsHit();

    public static bool BreaksCombo(this JudgementRating rating)
        => rating.AffectsCombo() && !rating.IsHit();

    public static bool AffectsAccuracy(this JudgementRating rating)
        => rating is >= JudgementRating.Miss and <= JudgementRating.Perfect;

    public static bool IsScorable(this JudgementRating rating)
        => rating is >= JudgementRating.Miss and <= JudgementRating.Perfect
           || rating == JudgementRating.ComboBreak;
}
