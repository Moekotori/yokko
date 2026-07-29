using Yokko.Core.Beatmaps;
using Yokko.Core.Timing;

namespace Yokko.Core.Mods;

/// <summary>
/// Converts retained osu!standard objects into a playable Mania lane chart.
/// Column selection follows ManiaBeatmapConverter at the pinned lazer commit.
/// Pattern generation is deterministic and always starts from the retained
/// source, allowing key-count Mods to regenerate rather than remap a result.
/// </summary>
public static class OsuStandardManiaConverter
{
    public static int DetermineDefaultColumnCount(
        ManiaConversionSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        double roundedCircleSize = Math.Round(source.CircleSize);
        double roundedOverallDifficulty =
            Math.Round(source.OverallDifficulty);
        int total = source.HitObjects.Count;
        if (total > 0)
        {
            int special = source.HitObjects.Count(hitObject =>
                hitObject.Kind is ManiaConversionObjectKind.Slider
                    or ManiaConversionObjectKind.Spinner);
            double percentSpecial = (double)special / total;
            if (percentSpecial < 0.2)
                return 7;
            if (percentSpecial < 0.3 || roundedCircleSize >= 5)
                return roundedOverallDifficulty > 5 ? 7 : 6;
            if (percentSpecial > 0.6)
                return roundedOverallDifficulty > 4 ? 5 : 4;
        }

        return Math.Max(
            4,
            Math.Min((int)roundedOverallDifficulty + 1, 7));
    }

    public static IReadOnlyList<YokkoHitObject> Convert(
        ManiaConversionSource source,
        int totalColumns,
        IReadOnlyList<YokkoTimingPoint>? timingPoints = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (totalColumns is < 1 or > 20)
        {
            throw new ArgumentOutOfRangeException(
                nameof(totalColumns),
                "Mania conversion supports up to two 10-key stages.");
        }

        return new OsuStandardPatternGenerator(
            source,
            totalColumns,
            timingPoints).Generate();
    }
}
