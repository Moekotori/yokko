using System.Collections.Generic;
using System.Globalization;
using Yokko.Core.Difficulty;

namespace Yokko.Game.Presentation;

internal static class ManiaStarRatingPresentation
{
    public static string FormatValue(
        ManiaStarRatingResult rating,
        string format = "0.00")
    {
        if (rating?.Value is not double value)
            return "--";

        string prefix = rating.IsPartial ? "~" : string.Empty;
        return prefix + value.ToString(
            format,
            CultureInfo.CurrentCulture);
    }

    public static string FormatStar(ManiaStarRatingResult rating) =>
        rating?.IsSuccess == true
            ? $"{FormatValue(rating)} STAR"
            : "-- STAR";

    public static string Qualifier(ManiaStarRatingResult rating)
    {
        if (rating?.IsPartial != true)
            return "BETA";

        var reasons = new List<string>();
        if (rating.Limitations.HasFlag(
                ManiaStarRatingLimitations.MinesExcluded))
        {
            reasons.Add("MINE");
        }

        if (rating.Limitations.HasFlag(
                ManiaStarRatingLimitations.NoReleaseNotModelled))
        {
            reasons.Add("NR");
        }

        if (rating.Limitations.HasFlag(
                ManiaStarRatingLimitations.DynamicRateApproximation))
        {
            reasons.Add("RATE");
        }

        string detail = reasons.Count == 0
            ? string.Empty
            : $" {string.Join("/", reasons)}";
        return $"PARTIAL{detail} · BETA";
    }
}
