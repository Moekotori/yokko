using StarRatingRebirth;
using Yokko.Core.Beatmaps;

namespace Yokko.Core.Difficulty;

/// <summary>
/// Adapts Yokko's format-independent beatmap model to Star Rating Rebirth.
/// </summary>
public static class ManiaStarRatingCalculator
{
    /// <summary>
    /// The upstream algorithm revision exposed by StarRatingRebirth 0.1.1.
    /// </summary>
    public const string AlgorithmVersion = "2025/04/15";

    /// <summary>
    /// The minimum playable note count accepted by the upstream algorithm.
    /// </summary>
    public const int MinimumNoteCount = 20;

    /// <summary>
    /// Calculates a mania star rating from Yokko's canonical beatmap model.
    /// </summary>
    /// <exception cref="InvalidDataException">
    /// The beatmap cannot be represented by the upstream calculator.
    /// </exception>
    public static double Calculate(YokkoBeatmap beatmap)
    {
        ArgumentNullException.ThrowIfNull(beatmap);

        int keyCount = (int)beatmap.KeyMode;
        var notes = beatmap.HitObjects
                           .Where(static hitObject =>
                               hitObject.Kind is HitObjectKind.Tap
                                   or HitObjectKind.Hold)
                           .Select(hitObject => toUpstreamNote(
                               hitObject,
                               keyCount))
                           .ToList();

        if (notes.Count < MinimumNoteCount)
        {
            throw new InvalidDataException(
                $"Star Rating Rebirth requires at least {MinimumNoteCount} playable notes.");
        }

        double rating = SRCalculator.Calculate(new ManiaData
        {
            CS = keyCount,
            OD = beatmap.OverallDifficulty,
            Notes = notes,
        });

        if (!double.IsFinite(rating) || rating < 0)
            throw new InvalidDataException("Star Rating Rebirth returned an invalid rating.");

        return rating;
    }

    /// <summary>
    /// Attempts a calculation without allowing an unsupported or malformed chart
    /// to interrupt library loading.
    /// </summary>
    public static bool TryCalculate(
        YokkoBeatmap beatmap,
        out double starRating)
    {
        ArgumentNullException.ThrowIfNull(beatmap);

        try
        {
            starRating = Calculate(beatmap);
            return true;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            starRating = 0;
            return false;
        }
    }

    private static Note toUpstreamNote(
        YokkoHitObject hitObject,
        int keyCount)
    {
        if (hitObject.Lane < 0 || hitObject.Lane >= keyCount)
        {
            throw new InvalidDataException(
                $"Lane {hitObject.Lane} is outside the {keyCount}K playfield.");
        }

        int head = toWholeMilliseconds(
            hitObject.StartTimeMilliseconds,
            nameof(hitObject.StartTimeMilliseconds));
        int tail = hitObject.Kind == HitObjectKind.Hold
            ? toWholeMilliseconds(
                hitObject.EndTimeMilliseconds!.Value,
                nameof(hitObject.EndTimeMilliseconds))
            : -1;

        return new Note(hitObject.Lane, head, tail);
    }

    private static int toWholeMilliseconds(double value, string fieldName)
    {
        double rounded = Math.Round(value, MidpointRounding.AwayFromZero);

        if (rounded is < int.MinValue or > int.MaxValue)
        {
            throw new InvalidDataException(
                $"{fieldName} cannot be represented as whole milliseconds.");
        }

        return (int)rounded;
    }
}
