using Yokko.Core.Beatmaps;

namespace Yokko.Core.Difficulty;

public enum ManiaDifficultyRatingMode
{
    EtternaMsd,
    RebirthStars,
}

public sealed record ManiaDifficultyRatings(
    ManiaMsdResult EtternaMsd,
    ManiaStarRatingResult RebirthStars)
{
    public double? Value(ManiaDifficultyRatingMode mode) => mode switch
    {
        ManiaDifficultyRatingMode.EtternaMsd => EtternaMsd?.Value,
        ManiaDifficultyRatingMode.RebirthStars => RebirthStars?.Value,
        _ => null,
    };

    public bool IsSuccess(ManiaDifficultyRatingMode mode) => mode switch
    {
        ManiaDifficultyRatingMode.EtternaMsd =>
            EtternaMsd?.IsSuccess == true,
        ManiaDifficultyRatingMode.RebirthStars =>
            RebirthStars?.IsSuccess == true,
        _ => false,
    };
}

public static class ManiaDifficultyCalculator
{
    public static ManiaDifficultyRatings CalculateResult(
        YokkoBeatmap beatmap,
        double playbackRate = 1) =>
        new(
            ManiaMsdCalculator.CalculateResult(beatmap, playbackRate),
            ManiaStarRatingCalculator.CalculateResult(
                beatmap,
                playbackRate));

    public static ManiaDifficultyRatings CalculateResult(
        YokkoBeatmap beatmap,
        ManiaStarRatingContext starRatingContext,
        double playbackRate = 1) =>
        new(
            ManiaMsdCalculator.CalculateResult(beatmap, playbackRate),
            ManiaStarRatingCalculator.CalculateResult(
                beatmap,
                starRatingContext,
                playbackRate));
}
