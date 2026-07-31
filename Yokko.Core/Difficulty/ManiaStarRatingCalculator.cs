using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using StarRatingRebirth;
using Yokko.Core.Beatmaps;

namespace Yokko.Core.Difficulty;

public enum ManiaStarRatingStatus
{
    Success,
    TooFewNotes,
    InvalidLane,
    InvalidTime,
    InvalidRate,
    AlgorithmFailure,
}

public sealed record ManiaStarRatingResult(
    ManiaStarRatingStatus Status,
    double? Value,
    double PlaybackRate,
    string AlgorithmIdentifier,
    string? FailureReason = null,
    ManiaStarRatingLimitations Limitations =
        ManiaStarRatingLimitations.None,
    double? EffectiveOverallDifficulty = null)
{
    public bool IsSuccess =>
        Status == ManiaStarRatingStatus.Success && Value.HasValue;

    public bool IsPartial =>
        IsSuccess && Limitations != ManiaStarRatingLimitations.None;
}

/// <summary>
/// Adapts Yokko's format-independent beatmap model to Star Rating Rebirth.
/// </summary>
public static class ManiaStarRatingCalculator
{
    public const string PackageVersion = "0.1.1";
    public const string AlgorithmVersion = "2025/04/15";
    public const string AlgorithmIdentifier =
        "StarRatingRebirth 0.1.1 (2025/04/15)";
    public const int MinimumNoteCount = 20;

    private const string adapter_cache_version = "YokkoAdapter/3";

    /// <summary>
    /// Calculates an input-difficulty rating at the requested playback rate.
    /// A rate of 1.5 corresponds to DT-style time compression.
    /// </summary>
    /// <exception cref="InvalidDataException">
    /// The beatmap cannot be represented by the upstream calculator.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The upstream calculator failed or returned an invalid value.
    /// </exception>
    public static double Calculate(
        YokkoBeatmap beatmap,
        double playbackRate = 1)
        => calculateOrThrow(CalculateResult(beatmap, playbackRate));

    public static double Calculate(
        YokkoBeatmap beatmap,
        ManiaStarRatingContext context,
        double playbackRate = 1)
        => calculateOrThrow(CalculateResult(
            beatmap,
            context,
            playbackRate));

    private static double calculateOrThrow(
        ManiaStarRatingResult result)
    {
        if (result.IsSuccess)
            return result.Value!.Value;

        string message = result.FailureReason
                         ?? "Star Rating Rebirth could not calculate this chart.";

        if (result.Status == ManiaStarRatingStatus.AlgorithmFailure)
            throw new InvalidOperationException(message);

        throw new InvalidDataException(message);
    }

    /// <summary>
    /// Calculates a diagnostic result without allowing unsupported chart data
    /// or an upstream failure to interrupt library loading.
    /// </summary>
    public static ManiaStarRatingResult CalculateResult(
        YokkoBeatmap beatmap,
        double playbackRate = 1)
    {
        ArgumentNullException.ThrowIfNull(beatmap);
        return CalculateResult(
            beatmap,
            ManiaStarRatingContext.ForBeatmap(beatmap),
            playbackRate);
    }

    public static ManiaStarRatingResult CalculateResult(
        YokkoBeatmap beatmap,
        ManiaStarRatingContext context,
        double playbackRate = 1)
    {
        ArgumentNullException.ThrowIfNull(beatmap);
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            PreparedStarRatingInput input = prepareData(
                beatmap,
                context,
                playbackRate);
            double rating = SRCalculator.Calculate(input.Data);

            if (!double.IsFinite(rating) || rating < 0)
            {
                return failure(
                    ManiaStarRatingStatus.AlgorithmFailure,
                    playbackRate,
                    "Star Rating Rebirth returned an invalid rating.");
            }

            return new ManiaStarRatingResult(
                ManiaStarRatingStatus.Success,
                rating,
                playbackRate,
                AlgorithmIdentifier,
                Limitations: input.Limitations,
                EffectiveOverallDifficulty: input.Data.OD);
        }
        catch (StarRatingInputException exception)
        {
            return failure(
                exception.Status,
                playbackRate,
                exception.Message);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return failure(
                ManiaStarRatingStatus.AlgorithmFailure,
                playbackRate,
                exception.Message);
        }
    }

    public static bool TryCalculate(
        YokkoBeatmap beatmap,
        out double starRating,
        double playbackRate = 1)
    {
        ManiaStarRatingResult result = CalculateResult(
            beatmap,
            playbackRate);
        starRating = result.Value ?? 0;
        return result.IsSuccess;
    }

    /// <summary>
    /// Creates a stable cache key from only the inputs which affect the rating.
    /// </summary>
    public static string CreateCacheKey(
        YokkoBeatmap beatmap,
        double playbackRate = 1)
    {
        ArgumentNullException.ThrowIfNull(beatmap);
        return CreateCacheKey(
            beatmap,
            ManiaStarRatingContext.ForBeatmap(beatmap),
            playbackRate);
    }

    public static string CreateCacheKey(
        YokkoBeatmap beatmap,
        ManiaStarRatingContext context,
        double playbackRate = 1)
    {
        ArgumentNullException.ThrowIfNull(beatmap);
        ArgumentNullException.ThrowIfNull(context);
        PreparedStarRatingInput input = prepareData(
            beatmap,
            context,
            playbackRate);
        ManiaData data = input.Data;
        var source = new StringBuilder()
                     .Append(adapter_cache_version).Append('\u001f')
                     .Append(AlgorithmIdentifier).Append('\u001f')
                     .Append(playbackRate.ToString(
                         "R",
                         CultureInfo.InvariantCulture)).Append('\u001f')
                     .Append(data.CS).Append('\u001f')
                     .Append(data.OD.ToString(
                         "R",
                         CultureInfo.InvariantCulture)).Append('\u001f')
                     .Append((int)input.Limitations);

        foreach (Note note in data.Notes)
        {
            source.Append('\u001e')
                  .Append(note.Key).Append(',')
                  .Append(note.Head).Append(',')
                  .Append(note.Tail);
        }

        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(source.ToString())));
    }

    private static PreparedStarRatingInput prepareData(
        YokkoBeatmap beatmap,
        ManiaStarRatingContext context,
        double playbackRate)
    {
        if (!double.IsFinite(playbackRate) || playbackRate <= 0)
        {
            throw new StarRatingInputException(
                ManiaStarRatingStatus.InvalidRate,
                "Playback rate must be finite and greater than zero.");
        }

        int keyCount = (int)beatmap.KeyMode;
        List<Note> notes = beatmap.HitObjects
                                        .Where(static hitObject =>
                                            hitObject.Kind
                                                is HitObjectKind.Tap
                                                or HitObjectKind.Hold)
                                        .Select(hitObject => toUpstreamNote(
                                            hitObject,
                                            keyCount,
                                            playbackRate))
                                        .OrderBy(static note => note.Head)
                                        .ThenBy(static note => note.Key)
                                        .ThenBy(static note => note.Tail)
                                        .ToList();

        if (notes.Count < MinimumNoteCount)
        {
            throw new StarRatingInputException(
                ManiaStarRatingStatus.TooFewNotes,
                $"Star Rating Rebirth requires at least {MinimumNoteCount} playable notes.");
        }

        var data = new ManiaData
        {
            CS = keyCount,
            OD = equivalentOverallDifficulty(
                context.GreatWindowMilliseconds),
            Notes = notes,
        };
        ManiaStarRatingLimitations limitations =
            context.AdditionalLimitations;
        if (context.MinesEnabled
            && beatmap.HitObjects.Any(static hitObject =>
                hitObject.Kind == HitObjectKind.Mine))
        {
            limitations |= ManiaStarRatingLimitations.MinesExcluded;
        }

        if (!context.ReleaseJudgementsRequired
            && beatmap.HitObjects.Any(static hitObject =>
                hitObject.Kind == HitObjectKind.Hold))
        {
            limitations |=
                ManiaStarRatingLimitations.NoReleaseNotModelled;
        }

        return new PreparedStarRatingInput(data, limitations);
    }

    private static double equivalentOverallDifficulty(
        double greatWindowMilliseconds) =>
        (64.5 - greatWindowMilliseconds) / 3;

    private static Note toUpstreamNote(
        YokkoHitObject hitObject,
        int keyCount,
        double playbackRate)
    {
        if (hitObject.Lane < 0 || hitObject.Lane >= keyCount)
        {
            throw new StarRatingInputException(
                ManiaStarRatingStatus.InvalidLane,
                $"Lane {hitObject.Lane} is outside the {keyCount}K playfield.");
        }

        int head = toWholeMilliseconds(
            hitObject.StartTimeMilliseconds / playbackRate,
            nameof(hitObject.StartTimeMilliseconds));
        int tail = hitObject.Kind == HitObjectKind.Hold
            ? toWholeMilliseconds(
                hitObject.EndTimeMilliseconds!.Value / playbackRate,
                nameof(hitObject.EndTimeMilliseconds))
            : -1;

        return new Note(hitObject.Lane, head, tail);
    }

    private static int toWholeMilliseconds(double value, string fieldName)
    {
        double rounded = Math.Round(value, MidpointRounding.AwayFromZero);

        if (!double.IsFinite(rounded)
            || rounded is < int.MinValue or > int.MaxValue)
        {
            throw new StarRatingInputException(
                ManiaStarRatingStatus.InvalidTime,
                $"{fieldName} cannot be represented as whole milliseconds.");
        }

        return (int)rounded;
    }

    private static ManiaStarRatingResult failure(
        ManiaStarRatingStatus status,
        double playbackRate,
        string reason) =>
        new(
            status,
            null,
            playbackRate,
            AlgorithmIdentifier,
            reason);

    private sealed class StarRatingInputException(
        ManiaStarRatingStatus status,
        string message)
        : Exception(message)
    {
        public ManiaStarRatingStatus Status { get; } = status;
    }

    private sealed record PreparedStarRatingInput(
        ManiaData Data,
        ManiaStarRatingLimitations Limitations);
}
