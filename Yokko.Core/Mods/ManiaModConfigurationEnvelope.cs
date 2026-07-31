namespace Yokko.Core.Mods;

/// <summary>
/// Versioned, storage-safe representation of one Mania Mod configuration.
/// Stable catalogue keys are persisted instead of enum ordinals.
/// </summary>
public sealed record ManiaModConfigurationEnvelope(
    int SchemaVersion,
    IReadOnlyList<ManiaModConfigurationEntry> Mods)
{
    public const int CurrentSchemaVersion = 1;
}

public sealed record ManiaModConfigurationEntry(
    string Key,
    ManiaModConfigurationSettings? Settings = null);

public sealed record ManiaModConfigurationSettings(
    int? Seed = null,
    double? Coverage = null,
    string? Direction = null,
    double? SizeMultiplier = null,
    bool? ComboBasedSize = null,
    double? MinimumAccuracy = null,
    string? AccuracyMode = null,
    bool? RequirePerfectHits = null,
    double? DrainRate = null,
    double? OverallDifficulty = null,
    bool? ExtendedLimits = null,
    bool? Inverse = null,
    bool? Metronome = null,
    int? ComboCount = null,
    bool? AffectsHitSounds = null,
    double? InitialRate = null,
    double? FinalRate = null,
    bool? AdjustPitch = null,
    double? SpeedChange = null,
    int? AllowedPauses = null);

/// <summary>
/// Converts the immutable runtime Mod set to and from the versioned envelope.
/// Unknown Mod keys fail closed so a replay cannot silently run different
/// rules from the rules it was recorded with.
/// </summary>
public static class ManiaModConfigurationCodec
{
    public static ManiaModConfigurationEnvelope Capture(
        ManiaModSet? mods)
    {
        mods ??= ManiaModSet.Empty;
        ManiaModConfigurationEntry[] entries = mods.Mods
            .Select(mod => new ManiaModConfigurationEntry(
                OsuManiaModParityCatalog.Get(mod).Key,
                captureSettings(mods, mod)))
            .ToArray();
        return new ManiaModConfigurationEnvelope(
            ManiaModConfigurationEnvelope.CurrentSchemaVersion,
            entries);
    }

    public static ManiaModSet Restore(
        ManiaModConfigurationEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        if (envelope.SchemaVersion
            != ManiaModConfigurationEnvelope.CurrentSchemaVersion)
        {
            throw new NotSupportedException(
                $"Unsupported Mania Mod configuration schema "
                + $"{envelope.SchemaVersion}.");
        }

        if (envelope.Mods is null)
            throw new InvalidDataException("The Mod list is missing.");

        var ids = new List<ManiaModId>(envelope.Mods.Count);
        var settings = new Dictionary<
            ManiaModId,
            ManiaModConfigurationSettings>();
        foreach (ManiaModConfigurationEntry entry in envelope.Mods)
        {
            if (entry is null
                || !OsuManiaModParityCatalog.TryGet(
                    entry.Key,
                    out ManiaModDefinition? definition)
                || definition is null)
            {
                throw new InvalidDataException(
                    $"Unknown Mania Mod key '{entry?.Key}'.");
            }

            if (!settings.TryAdd(
                    definition.Id,
                    entry.Settings ?? new ManiaModConfigurationSettings()))
            {
                throw new InvalidDataException(
                    $"Duplicate Mania Mod key '{entry.Key}'.");
            }

            ids.Add(definition.Id);
        }

        ManiaModConfigurationSettings random =
            get(settings, ManiaModId.Random);
        ManiaModConfigurationSettings cover =
            get(settings, ManiaModId.Cover);
        ManiaModConfigurationSettings flashlight =
            get(settings, ManiaModId.Flashlight);
        ManiaModConfigurationSettings accuracy =
            get(settings, ManiaModId.AccuracyChallenge);
        ManiaModConfigurationSettings perfect =
            get(settings, ManiaModId.Perfect);
        ManiaModConfigurationSettings difficulty =
            get(settings, ManiaModId.DifficultyAdjust);
        ManiaModConfigurationSettings muted =
            get(settings, ManiaModId.Muted);
        ManiaModConfigurationSettings ramp =
            get(
                settings,
                ids.Contains(ManiaModId.WindDown)
                    ? ManiaModId.WindDown
                    : ManiaModId.WindUp);
        ManiaModConfigurationSettings adaptive =
            get(settings, ManiaModId.AdaptiveSpeed);
        ManiaModConfigurationSettings noPause =
            get(settings, ManiaModId.NoPause);
        ManiaModId? fixedRate = ids
            .Where(static id => id is ManiaModId.HalfTime
                or ManiaModId.Daycore
                or ManiaModId.DoubleTime
                or ManiaModId.Nightcore)
            .Select(static id => (ManiaModId?)id)
            .FirstOrDefault();
        ManiaModConfigurationSettings fixedRateSettings =
            fixedRate is ManiaModId fixedRateId
                ? get(settings, fixedRateId)
                : new ManiaModConfigurationSettings();

        try
        {
            return new ManiaModSet(
                ids,
                random.Seed,
                cover.Coverage ?? 0.5,
                parseDirection(cover.Direction),
                flashlight.SizeMultiplier ?? 1,
                flashlight.ComboBasedSize ?? false,
                accuracy.MinimumAccuracy ?? 0.9,
                parseAccuracyMode(accuracy.AccuracyMode),
                difficulty.DrainRate,
                difficulty.OverallDifficulty,
                difficulty.ExtendedLimits ?? false,
                muted.Inverse ?? false,
                muted.Metronome ?? true,
                muted.ComboCount ?? 100,
                muted.AffectsHitSounds ?? true,
                ramp.InitialRate,
                ramp.FinalRate,
                ramp.AdjustPitch ?? true,
                adaptive.InitialRate ?? 1,
                adaptive.AdjustPitch ?? true,
                perfect.RequirePerfectHits ?? false,
                fixedRateSettings.SpeedChange,
                fixedRateSettings.AdjustPitch ?? false,
                noPause.AllowedPauses ?? 0);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException(
                "The persisted Mania Mod configuration is invalid.",
                exception);
        }
    }

    private static ManiaModConfigurationSettings? captureSettings(
        ManiaModSet mods,
        ManiaModId mod) =>
        mod switch
        {
            ManiaModId.Random => new(
                Seed: mods.RandomSeed),
            ManiaModId.Cover => new(
                Coverage: mods.CoverCoverage,
                Direction: mods.CoverDirection
                    == ManiaCoverDirection.AlongScroll
                    ? "along-scroll"
                    : "against-scroll"),
            ManiaModId.Flashlight => new(
                SizeMultiplier: mods.FlashlightSizeMultiplier,
                ComboBasedSize: mods.FlashlightComboBasedSize),
            ManiaModId.AccuracyChallenge => new(
                MinimumAccuracy: mods.AccuracyChallengeMinimum,
                AccuracyMode: mods.AccuracyChallengeMode
                    == ManiaAccuracyMode.MaximumAchievable
                    ? "maximum-achievable"
                    : "standard"),
            ManiaModId.Perfect => new(
                RequirePerfectHits: mods.PerfectRequirePerfectHits),
            ManiaModId.DifficultyAdjust => new(
                DrainRate: mods.DifficultyAdjustDrainRate,
                OverallDifficulty:
                    mods.DifficultyAdjustOverallDifficulty,
                ExtendedLimits: mods.DifficultyAdjustExtendedLimits),
            ManiaModId.Muted => new(
                Inverse: mods.MutedInverse,
                Metronome: mods.MutedMetronome,
                ComboCount: mods.MutedComboCount,
                AffectsHitSounds: mods.MutedAffectsHitSounds),
            ManiaModId.WindUp or ManiaModId.WindDown => new(
                InitialRate: mods.TimeRampInitialRate,
                FinalRate: mods.TimeRampFinalRate,
                AdjustPitch: mods.TimeRampAdjustPitch),
            ManiaModId.AdaptiveSpeed => new(
                InitialRate: mods.AdaptiveInitialRate,
                AdjustPitch: mods.AdaptiveAdjustPitch),
            ManiaModId.NoPause => new(
                AllowedPauses: mods.NoPauseAllowedPauses),
            ManiaModId.HalfTime
                or ManiaModId.Daycore
                or ManiaModId.DoubleTime
                or ManiaModId.Nightcore => new(
                    SpeedChange: mods.FixedRateSpeedChange,
                    AdjustPitch: mods.FixedRateAdjustPitch),
            _ => null,
        };

    private static ManiaModConfigurationSettings get(
        IReadOnlyDictionary<
            ManiaModId,
            ManiaModConfigurationSettings> settings,
        ManiaModId id) =>
        settings.GetValueOrDefault(id)
        ?? new ManiaModConfigurationSettings();

    private static ManiaCoverDirection parseDirection(string? value) =>
        value switch
        {
            null or "along-scroll" => ManiaCoverDirection.AlongScroll,
            "against-scroll" => ManiaCoverDirection.AgainstScroll,
            _ => throw new InvalidDataException(
                $"Unknown Cover direction '{value}'."),
        };

    private static ManiaAccuracyMode parseAccuracyMode(string? value) =>
        value switch
        {
            null or "maximum-achievable" =>
                ManiaAccuracyMode.MaximumAchievable,
            "standard" => ManiaAccuracyMode.Standard,
            _ => throw new InvalidDataException(
                $"Unknown Accuracy Challenge mode '{value}'."),
        };
}
