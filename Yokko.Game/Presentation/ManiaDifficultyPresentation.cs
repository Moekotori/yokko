using Yokko.Core.Difficulty;

namespace Yokko.Game.Presentation;

internal static class ManiaDifficultyPresentation
{
    public static string FormatValue(
        ManiaDifficultyRatings ratings,
        ManiaDifficultyRatingMode mode) => mode switch
    {
        ManiaDifficultyRatingMode.EtternaMsd =>
            ratings?.EtternaMsd == null
                ? "--"
                : ManiaMsdPresentation.FormatValue(
                    ratings.EtternaMsd),
        ManiaDifficultyRatingMode.RebirthStars =>
            ManiaStarRatingPresentation.FormatValue(
                ratings?.RebirthStars),
        _ => "--",
    };

    public static string Unit(
        ManiaDifficultyRatingMode mode) => mode switch
    {
        ManiaDifficultyRatingMode.EtternaMsd => "MSD",
        ManiaDifficultyRatingMode.RebirthStars => "STAR",
        _ => string.Empty,
    };

    public static string Qualifier(
        ManiaDifficultyRatings ratings,
        ManiaDifficultyRatingMode mode) => mode switch
    {
        ManiaDifficultyRatingMode.EtternaMsd =>
            ratings?.EtternaMsd == null
                ? "ETTERNA MSD"
                : ManiaMsdPresentation.Qualifier(
                    ratings.EtternaMsd),
        ManiaDifficultyRatingMode.RebirthStars =>
            ManiaStarRatingPresentation.Qualifier(
                ratings?.RebirthStars),
        _ => string.Empty,
    };

    public static string FormatInline(
        ManiaDifficultyRatings ratings,
        ManiaDifficultyRatingMode mode) =>
        $"{FormatValue(ratings, mode)} {Unit(mode)}";
}
