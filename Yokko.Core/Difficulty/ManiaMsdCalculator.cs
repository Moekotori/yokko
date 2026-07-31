using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Yokko.Core.Beatmaps;

namespace Yokko.Core.Difficulty;

public enum ManiaMsdStatus
{
    Success,
    TooFewRows,
    InvalidLane,
    InvalidTime,
    InvalidRate,
    NativeUnavailable,
    AlgorithmFailure,
}

public enum EtternaMsdSkillset
{
    Overall,
    Stream,
    Jumpstream,
    Handstream,
    Stamina,
    JackSpeed,
    Chordjack,
    Technical,
}

public sealed record EtternaMsdValues(
    double Overall,
    double Stream,
    double Jumpstream,
    double Handstream,
    double Stamina,
    double JackSpeed,
    double Chordjack,
    double Technical)
{
    public double this[EtternaMsdSkillset skillset] => skillset switch
    {
        EtternaMsdSkillset.Overall => Overall,
        EtternaMsdSkillset.Stream => Stream,
        EtternaMsdSkillset.Jumpstream => Jumpstream,
        EtternaMsdSkillset.Handstream => Handstream,
        EtternaMsdSkillset.Stamina => Stamina,
        EtternaMsdSkillset.JackSpeed => JackSpeed,
        EtternaMsdSkillset.Chordjack => Chordjack,
        EtternaMsdSkillset.Technical => Technical,
        _ => throw new ArgumentOutOfRangeException(nameof(skillset)),
    };

    public EtternaMsdSkillset DominantSkillset =>
        Enum.GetValues<EtternaMsdSkillset>()
            .Where(static skillset =>
                skillset != EtternaMsdSkillset.Overall)
            .MaxBy(skillset => this[skillset]);
}

public sealed record ManiaMsdResult(
    ManiaMsdStatus Status,
    EtternaMsdValues? Skillsets,
    double PlaybackRate,
    string AlgorithmIdentifier,
    string? FailureReason = null)
{
    public bool IsSuccess =>
        Status == ManiaMsdStatus.Success && Skillsets != null;

    public double? Value => Skillsets?.Overall;
}

/// <summary>
/// Adapts Yokko's canonical lane chart to Etterna MinaCalc's NoteInfo rows.
/// Hold tails, mines and samples are omitted exactly like Etterna's
/// NoteData::SerializeNoteData2 path.
/// </summary>
public static class ManiaMsdCalculator
{
    public const int CalculatorVersion = 515;
    public const string UpstreamCommit =
        "b65660062ef2a23121e331c36e23c23a8f6eafaa";
    public const string AlgorithmIdentifier =
        "Etterna MinaCalc v515 (b6566006)";
    public const int MinimumRowCount = 2;

    private const string adapter_cache_version = "YokkoEtternaAdapter/1";

    public static bool IsAvailable => EtternaMsdNativeLibrary.IsAvailable;

    public static ManiaMsdResult CalculateResult(
        YokkoBeatmap beatmap,
        double playbackRate = 1)
    {
        ArgumentNullException.ThrowIfNull(beatmap);

        PreparedMsdInput input;
        try
        {
            input = prepareInput(beatmap, playbackRate);
        }
        catch (MsdInputException exception)
        {
            return failure(
                exception.Status,
                playbackRate,
                exception.Message);
        }

        if (!EtternaMsdNativeLibrary.IsAvailable)
        {
            return failure(
                ManiaMsdStatus.NativeUnavailable,
                playbackRate,
                "Etterna MinaCalc native library is unavailable.");
        }

        try
        {
            EtternaMsdNativeLibrary.EnsureLoaded();
            if (EtternaMsdNative.GetVersion() != CalculatorVersion)
            {
                return failure(
                    ManiaMsdStatus.AlgorithmFailure,
                    playbackRate,
                    "The loaded MinaCalc version does not match Yokko's adapter.");
            }

            EtternaMsdNativeOutput output =
                EtternaMsdNativeOutput.Create();
            EtternaMsdNativeResult nativeResult;

            unsafe
            {
                fixed (EtternaMsdNativeNote* notes = input.Rows)
                {
                    nativeResult = EtternaMsdNative.Calculate(
                        notes,
                        (nuint)input.Rows.Length,
                        input.KeyCount,
                        input.PlaybackRate,
                        ref output);
                }
            }

            if (nativeResult != EtternaMsdNativeResult.Ok)
            {
                return failure(
                    nativeResult == EtternaMsdNativeResult.InvalidChart
                        ? ManiaMsdStatus.InvalidTime
                        : ManiaMsdStatus.AlgorithmFailure,
                    playbackRate,
                    $"Etterna MinaCalc returned {nativeResult}.");
            }

            var values = new EtternaMsdValues(
                output.Overall,
                output.Stream,
                output.Jumpstream,
                output.Handstream,
                output.Stamina,
                output.JackSpeed,
                output.Chordjack,
                output.Technical);
            if (!allValuesAreValid(values))
            {
                return failure(
                    ManiaMsdStatus.AlgorithmFailure,
                    playbackRate,
                    "Etterna MinaCalc returned an invalid MSD value.");
            }

            return new ManiaMsdResult(
                ManiaMsdStatus.Success,
                values,
                playbackRate,
                AlgorithmIdentifier);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return failure(
                ManiaMsdStatus.AlgorithmFailure,
                playbackRate,
                exception.Message);
        }
    }

    public static string CreateCacheKey(
        YokkoBeatmap beatmap,
        double playbackRate = 1)
    {
        ArgumentNullException.ThrowIfNull(beatmap);
        PreparedMsdInput input = prepareInput(beatmap, playbackRate);
        var source = new StringBuilder()
                     .Append(adapter_cache_version).Append('\u001f')
                     .Append(AlgorithmIdentifier).Append('\u001f')
                     .Append(input.PlaybackRate.ToString(
                         "R",
                         CultureInfo.InvariantCulture)).Append('\u001f')
                     .Append(input.KeyCount);

        foreach (EtternaMsdNativeNote row in input.Rows)
        {
            source.Append('\u001e')
                  .Append(row.Notes).Append(',')
                  .Append(row.RowTime.ToString(
                      "R",
                      CultureInfo.InvariantCulture));
        }

        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(source.ToString())));
    }

    private static PreparedMsdInput prepareInput(
        YokkoBeatmap beatmap,
        double playbackRate)
    {
        if (!double.IsFinite(playbackRate) || playbackRate <= 0)
        {
            throw new MsdInputException(
                ManiaMsdStatus.InvalidRate,
                "Playback rate must be finite and greater than zero.");
        }

        int keyCount = (int)beatmap.KeyMode;
        var rows = new SortedDictionary<double, uint>();
        foreach (YokkoHitObject hitObject in beatmap.HitObjects)
        {
            if (hitObject.Kind is not (
                    HitObjectKind.Tap
                    or HitObjectKind.Hold))
            {
                continue;
            }

            if (hitObject.Lane < 0 || hitObject.Lane >= keyCount)
            {
                throw new MsdInputException(
                    ManiaMsdStatus.InvalidLane,
                    $"Lane {hitObject.Lane} is outside the "
                    + $"{keyCount}K playfield.");
            }

            double time = hitObject.StartTimeMilliseconds;
            if (!double.IsFinite(time))
            {
                throw new MsdInputException(
                    ManiaMsdStatus.InvalidTime,
                    "A note start time is not finite.");
            }

            uint laneMask = 1u << hitObject.Lane;
            rows[time] = rows.GetValueOrDefault(time) | laneMask;
        }

        if (rows.Count < MinimumRowCount)
        {
            throw new MsdInputException(
                ManiaMsdStatus.TooFewRows,
                $"Etterna MinaCalc requires at least "
                + $"{MinimumRowCount} playable rows.");
        }

        double firstTime = rows.Keys.First();
        EtternaMsdNativeNote[] prepared = rows.Select(pair =>
            {
                float rowTime = checked((float)(
                    (pair.Key - firstTime) / 1000d));
                if (!float.IsFinite(rowTime) || rowTime < 0)
                {
                    throw new MsdInputException(
                        ManiaMsdStatus.InvalidTime,
                        "A note time cannot be represented by MinaCalc.");
                }

                return new EtternaMsdNativeNote(pair.Value, rowTime);
            })
            .ToArray();

        float nativeRate = checked((float)playbackRate);
        if (!float.IsFinite(nativeRate) || nativeRate <= 0)
        {
            throw new MsdInputException(
                ManiaMsdStatus.InvalidRate,
                "Playback rate cannot be represented by MinaCalc.");
        }

        return new PreparedMsdInput(
            (uint)keyCount,
            nativeRate,
            prepared);
    }

    private static bool allValuesAreValid(EtternaMsdValues values) =>
        Enum.GetValues<EtternaMsdSkillset>()
            .All(skillset =>
                double.IsFinite(values[skillset])
                && values[skillset] >= 0);

    private static ManiaMsdResult failure(
        ManiaMsdStatus status,
        double playbackRate,
        string reason) =>
        new(
            status,
            null,
            playbackRate,
            AlgorithmIdentifier,
            reason);

    private sealed class MsdInputException(
        ManiaMsdStatus status,
        string message)
        : Exception(message)
    {
        internal ManiaMsdStatus Status { get; } = status;
    }

    private sealed record PreparedMsdInput(
        uint KeyCount,
        float PlaybackRate,
        EtternaMsdNativeNote[] Rows);
}

