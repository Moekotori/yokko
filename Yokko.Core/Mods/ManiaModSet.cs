using System.Globalization;
using Yokko.Core.Scoring;

namespace Yokko.Core.Mods;

/// <summary>
/// Immutable, canonically ordered set of Mania Mods selected for one gameplay
/// session. Configurable values are carried in the canonical fingerprint
/// without changing the stable Mod identifiers.
/// </summary>
public sealed class ManiaModSet : IEquatable<ManiaModSet>
{
    private static readonly ManiaModId[] rateMods =
    [
        ManiaModId.HalfTime,
        ManiaModId.Daycore,
        ManiaModId.DoubleTime,
        ManiaModId.Nightcore,
    ];

    private static readonly ManiaModId[] holdRuleMods =
    [
        ManiaModId.NoRelease,
        ManiaModId.HoldOff,
    ];

    private static readonly ManiaModId[] visibilityMods =
    [
        ManiaModId.FadeIn,
        ManiaModId.Hidden,
        ManiaModId.Cover,
        ManiaModId.Flashlight,
    ];

    private static readonly ManiaModId[] failRuleMods =
    [
        ManiaModId.NoFail,
        ManiaModId.SuddenDeath,
        ManiaModId.Perfect,
    ];

    private static readonly ManiaModId[] difficultyRuleMods =
    [
        ManiaModId.Easy,
        ManiaModId.HardRock,
    ];

    private static readonly ManiaModId[] automationMods =
    [
        ManiaModId.Autoplay,
        ManiaModId.Cinema,
    ];

    private static readonly ManiaModId[] keyMods =
    [
        ManiaModId.Key1,
        ManiaModId.Key2,
        ManiaModId.Key3,
        ManiaModId.Key4,
        ManiaModId.Key5,
        ManiaModId.Key6,
        ManiaModId.Key7,
        ManiaModId.Key8,
        ManiaModId.Key9,
        ManiaModId.Key10,
    ];

    private static readonly ManiaModId[] variableRateMods =
    [
        ManiaModId.WindUp,
        ManiaModId.WindDown,
        ManiaModId.AdaptiveSpeed,
    ];

    private readonly ManiaModId[] mods;

    public static ManiaModSet Empty { get; } = new([]);

    public ManiaModSet(
        IEnumerable<ManiaModId> mods,
        int? randomSeed = null,
        double coverCoverage = 0.5,
        ManiaCoverDirection coverDirection =
            ManiaCoverDirection.AlongScroll,
        double flashlightSizeMultiplier = 1,
        bool flashlightComboBasedSize = false,
        double accuracyChallengeMinimum = 0.9,
        ManiaAccuracyMode accuracyChallengeMode =
            ManiaAccuracyMode.MaximumAchievable,
        double? difficultyAdjustDrainRate = null,
        double? difficultyAdjustOverallDifficulty = null,
        bool difficultyAdjustExtendedLimits = false,
        bool mutedInverse = false,
        bool mutedMetronome = true,
        int mutedComboCount = 100,
        bool mutedAffectsHitSounds = true,
        double? timeRampInitialRate = null,
        double? timeRampFinalRate = null,
        bool timeRampAdjustPitch = true,
        double adaptiveInitialRate = 1,
        bool adaptiveAdjustPitch = true,
        bool perfectRequirePerfectHits = false,
        double? fixedRateSpeedChange = null,
        bool fixedRateAdjustPitch = false)
    {
        ArgumentNullException.ThrowIfNull(mods);
        this.mods = mods.Distinct()
                        .OrderBy(static mod => mod)
                        .ToArray();
        RandomSeed = Contains(ManiaModId.Random)
            ? randomSeed ?? 0
            : null;
        if (!double.IsFinite(coverCoverage)
            || coverCoverage is < 0.2 or > 0.8)
        {
            throw new ArgumentOutOfRangeException(
                nameof(coverCoverage),
                "Cover coverage must be between 0.2 and 0.8.");
        }
        if (!double.IsFinite(flashlightSizeMultiplier)
            || flashlightSizeMultiplier is < 0.5 or > 3)
        {
            throw new ArgumentOutOfRangeException(
                nameof(flashlightSizeMultiplier),
                "Flashlight size must be between 0.5x and 3x.");
        }
        CoverCoverage = Contains(ManiaModId.Cover)
            ? coverCoverage
            : 0.5;
        CoverDirection = Contains(ManiaModId.Cover)
            ? coverDirection
            : ManiaCoverDirection.AlongScroll;
        FlashlightSizeMultiplier = Contains(ManiaModId.Flashlight)
            ? flashlightSizeMultiplier
            : 1;
        FlashlightComboBasedSize = Contains(ManiaModId.Flashlight)
                                   && flashlightComboBasedSize;
        if (!double.IsFinite(accuracyChallengeMinimum)
            || accuracyChallengeMinimum is < 0.6 or > 0.999)
        {
            throw new ArgumentOutOfRangeException(
                nameof(accuracyChallengeMinimum),
                "Accuracy Challenge must be between 60.0% and 99.9%.");
        }
        AccuracyChallengeMinimum =
            Contains(ManiaModId.AccuracyChallenge)
                ? accuracyChallengeMinimum
                : 0.9;
        AccuracyChallengeMode =
            Contains(ManiaModId.AccuracyChallenge)
                ? accuracyChallengeMode
                : ManiaAccuracyMode.MaximumAchievable;
        PerfectRequirePerfectHits =
            Contains(ManiaModId.Perfect)
            && perfectRequirePerfectHits;
        validateDifficultyAdjustValue(
            difficultyAdjustDrainRate,
            0,
            difficultyAdjustExtendedLimits ? 11 : 10,
            nameof(difficultyAdjustDrainRate));
        validateDifficultyAdjustValue(
            difficultyAdjustOverallDifficulty,
            difficultyAdjustExtendedLimits ? -15 : 0,
            difficultyAdjustExtendedLimits ? 15 : 10,
            nameof(difficultyAdjustOverallDifficulty));
        DifficultyAdjustDrainRate =
            Contains(ManiaModId.DifficultyAdjust)
                ? difficultyAdjustDrainRate
                : null;
        DifficultyAdjustOverallDifficulty =
            Contains(ManiaModId.DifficultyAdjust)
                ? difficultyAdjustOverallDifficulty
                : null;
        DifficultyAdjustExtendedLimits =
            Contains(ManiaModId.DifficultyAdjust)
            && difficultyAdjustExtendedLimits;
        if (mutedComboCount is < 0 or > 500
            || mutedInverse && mutedComboCount == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(mutedComboCount),
                "Muted combo must be 0-500, or 1-500 when starting muted.");
        }
        MutedInverse = Contains(ManiaModId.Muted)
                       && mutedInverse;
        MutedMetronome = !Contains(ManiaModId.Muted)
                         || mutedMetronome;
        MutedComboCount = Contains(ManiaModId.Muted)
            ? mutedComboCount
            : 100;
        MutedAffectsHitSounds = !Contains(ManiaModId.Muted)
                               || mutedAffectsHitSounds;
        bool windUp = Contains(ManiaModId.WindUp);
        bool windDown = Contains(ManiaModId.WindDown);
        double initialRate = timeRampInitialRate ?? 1;
        double finalRate = timeRampFinalRate
                           ?? (windDown ? 0.75 : 1.5);
        if (windUp || windDown)
        {
            double initialMinimum = windUp ? 0.5 : 0.51;
            double initialMaximum = windUp ? 1.99 : 2;
            double finalMinimum = windUp ? 0.51 : 0.5;
            double finalMaximum = windUp ? 2 : 1.99;
            if (!double.IsFinite(initialRate)
                || initialRate < initialMinimum
                || initialRate > initialMaximum)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(timeRampInitialRate));
            }
            if (!double.IsFinite(finalRate)
                || finalRate < finalMinimum
                || finalRate > finalMaximum)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(timeRampFinalRate));
            }
            if (windUp && initialRate >= finalRate
                || windDown && initialRate <= finalRate)
            {
                throw new ArgumentException(
                    "Wind Up must accelerate and Wind Down must decelerate.",
                    nameof(timeRampFinalRate));
            }
        }
        TimeRampInitialRate = windUp || windDown
            ? initialRate
            : 1;
        TimeRampFinalRate = windUp || windDown
            ? finalRate
            : 1;
        TimeRampAdjustPitch = (windUp || windDown)
                              && timeRampAdjustPitch;
        bool adaptive = Contains(ManiaModId.AdaptiveSpeed);
        if (adaptive
            && (!double.IsFinite(adaptiveInitialRate)
                || adaptiveInitialRate is < 0.5 or > 2))
        {
            throw new ArgumentOutOfRangeException(
                nameof(adaptiveInitialRate),
                "Adaptive Speed initial rate must be between 0.5 and 2.");
        }
        AdaptiveInitialRate = adaptive ? adaptiveInitialRate : 1;
        AdaptiveAdjustPitch = adaptive && adaptiveAdjustPitch;

        int selectedRateMods = this.mods.Count(rateMods.Contains);
        if (selectedRateMods > 1)
        {
            throw new ArgumentException(
                "Only one fixed-rate Mod may be selected.",
                nameof(mods));
        }
        ManiaModId? fixedRateMod = this.mods
            .Where(rateMods.Contains)
            .Cast<ManiaModId?>()
            .FirstOrDefault();
        if (fixedRateMod is ManiaModId selectedFixedRateMod)
        {
            double speedChange =
                fixedRateSpeedChange
                ?? defaultFixedRateFor(selectedFixedRateMod);
            double minimum = isSlowRateMod(selectedFixedRateMod)
                ? 0.5
                : 1.01;
            double maximum = isSlowRateMod(selectedFixedRateMod)
                ? 0.99
                : 2;
            if (!double.IsFinite(speedChange)
                || speedChange < minimum
                || speedChange > maximum)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(fixedRateSpeedChange),
                    $"The selected fixed-rate Mod requires a speed between {minimum:0.00}x and {maximum:0.00}x.");
            }

            FixedRateSpeedChange = speedChange;
            FixedRateAdjustPitch =
                selectedFixedRateMod is ManiaModId.HalfTime
                    or ManiaModId.DoubleTime
                && fixedRateAdjustPitch;
        }
        else
        {
            FixedRateSpeedChange = 1;
            FixedRateAdjustPitch = false;
        }

        int selectedHoldRuleMods = this.mods.Count(holdRuleMods.Contains);
        if (selectedHoldRuleMods > 1)
        {
            throw new ArgumentException(
                "No Release and Hold Off are mutually exclusive.",
                nameof(mods));
        }

        if (this.mods.Count(visibilityMods.Contains) > 1)
        {
            throw new ArgumentException(
                "Fade In, Hidden, Cover and Flashlight are mutually exclusive.",
                nameof(mods));
        }

        if (this.mods.Count(failRuleMods.Contains) > 1)
        {
            throw new ArgumentException(
                "No Fail, Sudden Death and Perfect are mutually exclusive.",
                nameof(mods));
        }

        if (this.mods.Count(difficultyRuleMods.Contains) > 1)
        {
            throw new ArgumentException(
                "Easy and Hard Rock are mutually exclusive.",
                nameof(mods));
        }

        if (Contains(ManiaModId.DifficultyAdjust)
            && this.mods.Any(difficultyRuleMods.Contains))
        {
            throw new ArgumentException(
                "Difficulty Adjust is incompatible with Easy and Hard Rock.",
                nameof(mods));
        }

        if (this.mods.Count(automationMods.Contains) > 1)
        {
            throw new ArgumentException(
                "Autoplay and Cinema are mutually exclusive.",
                nameof(mods));
        }

        if (this.mods.Count(keyMods.Contains) > 1)
        {
            throw new ArgumentException(
                "Only one Mania key conversion Mod may be selected.",
                nameof(mods));
        }

        if (this.mods.Count(variableRateMods.Contains) > 1)
        {
            throw new ArgumentException(
                "Wind Up, Wind Down and Adaptive Speed are mutually exclusive.",
                nameof(mods));
        }
        if (this.mods.Any(variableRateMods.Contains)
            && this.mods.Any(rateMods.Contains))
        {
            throw new ArgumentException(
                "Fixed and dynamic rate Mods are mutually exclusive.",
                nameof(mods));
        }
        if (Contains(ManiaModId.AdaptiveSpeed)
            && this.mods.Any(automationMods.Contains))
        {
            throw new ArgumentException(
                "Adaptive Speed is incompatible with automation Mods.",
                nameof(mods));
        }

        if (Contains(ManiaModId.Invert)
            && this.mods.Any(holdRuleMods.Contains))
        {
            throw new ArgumentException(
                "Invert is incompatible with Hold Off and No Release.",
                nameof(mods));
        }

        if (Contains(ManiaModId.Cinema)
            && (this.mods.Any(failRuleMods.Contains)
                || Contains(ManiaModId.AccuracyChallenge)))
        {
            throw new ArgumentException(
                "Cinema is incompatible with fail-condition Mods.",
                nameof(mods));
        }

        if (Contains(ManiaModId.AccuracyChallenge)
            && (Contains(ManiaModId.Easy)
                || Contains(ManiaModId.NoFail)
                || Contains(ManiaModId.Perfect)))
        {
            throw new ArgumentException(
                "Accuracy Challenge is incompatible with Easy, No Fail and Perfect.",
                nameof(mods));
        }
    }

    public IReadOnlyList<ManiaModId> Mods => mods;

    public int? RandomSeed { get; }

    public double CoverCoverage { get; }

    public ManiaCoverDirection CoverDirection { get; }

    public double FlashlightSizeMultiplier { get; }

    public bool FlashlightComboBasedSize { get; }

    public double AccuracyChallengeMinimum { get; }

    public ManiaAccuracyMode AccuracyChallengeMode { get; }

    public bool PerfectRequirePerfectHits { get; }

    public double? DifficultyAdjustDrainRate { get; }

    public double? DifficultyAdjustOverallDifficulty { get; }

    public bool DifficultyAdjustExtendedLimits { get; }

    public bool MutedInverse { get; }

    public bool MutedMetronome { get; }

    public int MutedComboCount { get; }

    public bool MutedAffectsHitSounds { get; }

    public double TimeRampInitialRate { get; }

    public double TimeRampFinalRate { get; }

    public bool TimeRampAdjustPitch { get; }

    public double AdaptiveInitialRate { get; }

    public bool AdaptiveAdjustPitch { get; }

    public double FixedRateSpeedChange { get; }

    public bool FixedRateAdjustPitch { get; }

    public bool HasTimeRamp =>
        Contains(ManiaModId.WindUp)
        || Contains(ManiaModId.WindDown);

    public bool HasAdaptiveSpeed =>
        Contains(ManiaModId.AdaptiveSpeed);

    public ManiaModId? FixedRateMod => mods
        .Where(rateMods.Contains)
        .Select(static mod => (ManiaModId?)mod)
        .FirstOrDefault();

    public bool HasDualStages => Contains(ManiaModId.DualStages);

    public bool HasDynamicRate => HasTimeRamp || HasAdaptiveSpeed;

    public int? KeyConversionTarget => mods
        .Where(keyMods.Contains)
        .Select(static mod => mod switch
        {
            ManiaModId.Key1 => 1,
            ManiaModId.Key2 => 2,
            ManiaModId.Key3 => 3,
            ManiaModId.Key4 => 4,
            ManiaModId.Key5 => 5,
            ManiaModId.Key6 => 6,
            ManiaModId.Key7 => 7,
            ManiaModId.Key8 => 8,
            ManiaModId.Key9 => 9,
            ManiaModId.Key10 => 10,
            _ => (int?)null,
        })
        .FirstOrDefault();

    public bool IsEmpty => mods.Length == 0;

    public double PlaybackRate =>
        HasTimeRamp
            ? TimeRampInitialRate
            : HasAdaptiveSpeed
                ? AdaptiveInitialRate
                : mods.Any(rateMods.Contains)
                    ? FixedRateSpeedChange
                    : 1;

    /// <summary>
    /// Playback-rate multiplier applied to osu!lazer Mania hit windows.
    /// At the pinned upstream baseline only the fixed-rate Mania Mods implement
    /// IManiaRateAdjustmentMod; WU, WD and AS leave hit windows unchanged.
    /// </summary>
    public double HitWindowSpeedMultiplier =>
        HasDynamicRate ? 1 : PlaybackRate;

    public bool ChangesAudioPitch =>
        Contains(ManiaModId.Nightcore)
        || Contains(ManiaModId.Daycore)
        || FixedRateAdjustPitch
        || HasTimeRamp && TimeRampAdjustPitch
        || HasAdaptiveSpeed && AdaptiveAdjustPitch;

    /// <summary>
    /// Fixed frequency adjustment used by lazer Daycore and Nightcore.
    /// Their configurable speed is completed with an independent tempo
    /// adjustment while pitch remains at the Mod's default frequency.
    /// </summary>
    public double? FixedAudioFrequencyScale =>
        Contains(ManiaModId.Daycore)
            ? 0.75
            : Contains(ManiaModId.Nightcore)
                ? 1.5
                : null;

    public double PlaybackRateAt(
        double timeMilliseconds,
        double firstObjectTimeMilliseconds,
        double lastObjectTimeMilliseconds)
    {
        if (!HasTimeRamp)
            return PlaybackRate;

        double finalRateTime = firstObjectTimeMilliseconds
                               + 0.75
                               * (lastObjectTimeMilliseconds
                                  - firstObjectTimeMilliseconds);
        double amount =
            (timeMilliseconds - firstObjectTimeMilliseconds)
            / Math.Max(1, finalRateTime - firstObjectTimeMilliseconds);
        double rate = TimeRampInitialRate
                      + (TimeRampFinalRate - TimeRampInitialRate)
                      * Math.Clamp(amount, 0, 1);
        return Math.Round(rate, 2);
    }

    public bool IsAutomation => mods.Any(automationMods.Contains);

    public bool IsCinema => Contains(ManiaModId.Cinema);

    public double HitWindowDifficultyMultiplier =>
        Contains(ManiaModId.Easy)
            ? 1 / 1.4
            : Contains(ManiaModId.HardRock)
                ? 1.4
                : 1;

    /// <summary>
    /// Multiplier applied to lazer's rounded score-without-Mods value.
    /// Source: ppy/osu
    /// osu.Game.Rulesets.Mania/Scoring/ManiaScoreMultiplierCalculator.cs
    /// commit 9f227ed28b6c8ba46dfea1f000f778d8b2827ad0 (MIT).
    /// </summary>
    public double ScoreMultiplier => mods.Aggregate(
        1d,
        (multiplier, mod) =>
            multiplier * scoreMultiplierFor(mod));

    public double EffectiveOverallDifficulty(double beatmapValue) =>
        Contains(ManiaModId.DifficultyAdjust)
            ? DifficultyAdjustOverallDifficulty ?? beatmapValue
            : beatmapValue;

    public double EffectiveDrainRate(double beatmapValue)
    {
        if (Contains(ManiaModId.DifficultyAdjust))
            return DifficultyAdjustDrainRate ?? beatmapValue;

        if (Contains(ManiaModId.Easy))
            return beatmapValue * 0.5;

        return Contains(ManiaModId.HardRock)
            ? Math.Min(10, beatmapValue * 1.4)
            : beatmapValue;
    }

    private double scoreMultiplierFor(ManiaModId mod)
        => mod switch
        {
            ManiaModId.Easy
                or ManiaModId.NoFail
                or ManiaModId.DifficultyAdjust
                or ManiaModId.WindUp
                or ManiaModId.WindDown
                or ManiaModId.AdaptiveSpeed => 0.5,
            ManiaModId.HalfTime
                or ManiaModId.Daycore =>
                rateAdjustScoreMultiplier(FixedRateSpeedChange),
            ManiaModId.NoRelease
                or ManiaModId.ConstantSpeed
                or ManiaModId.HoldOff
                or >= ManiaModId.Key1 and <= ManiaModId.Key10 => 0.9,
            _ => 1,
        };

    private static double rateAdjustScoreMultiplier(double speedChange)
    {
        double value = (int)(speedChange * 10) / 10.0;
        value -= 1;
        return speedChange >= 1
            ? 1 + value / 5
            : 0.6 + value;
    }

    public string Fingerprint => IsEmpty
        ? "nm"
        : string.Join(
            '+',
            mods.Select(mod =>
            {
                string key = OsuManiaModParityCatalog.Get(mod).Key;
                return mod switch
                {
                    ManiaModId.Random =>
                        $"{key}:{RandomSeed}",
                    ManiaModId.Cover =>
                        $"{key}:"
                        + CoverCoverage.ToString(
                            "R",
                            CultureInfo.InvariantCulture)
                        + ":"
                        + (CoverDirection
                           == ManiaCoverDirection.AlongScroll
                            ? "along"
                            : "against"),
                    ManiaModId.Flashlight =>
                        $"{key}:"
                        + FlashlightSizeMultiplier.ToString(
                            "R",
                            CultureInfo.InvariantCulture)
                        + ":"
                        + (FlashlightComboBasedSize
                            ? "combo"
                            : "fixed"),
                    ManiaModId.AccuracyChallenge =>
                        $"{key}:"
                        + AccuracyChallengeMinimum.ToString(
                            "R",
                            CultureInfo.InvariantCulture)
                        + ":"
                        + (AccuracyChallengeMode
                           == ManiaAccuracyMode.MaximumAchievable
                            ? "maximum"
                            : "standard"),
                    ManiaModId.Perfect
                        when PerfectRequirePerfectHits =>
                        $"{key}:require-perfect",
                    ManiaModId.HalfTime
                        or ManiaModId.DoubleTime
                        when !FixedRateSpeedChange.Equals(
                                  defaultFixedRateFor(mod))
                             || FixedRateAdjustPitch =>
                        $"{key}:"
                        + FixedRateSpeedChange.ToString(
                            "R",
                            CultureInfo.InvariantCulture)
                        + ":"
                        + (FixedRateAdjustPitch
                            ? "pitch"
                            : "tempo"),
                    ManiaModId.Daycore
                        or ManiaModId.Nightcore
                        when !FixedRateSpeedChange.Equals(
                            defaultFixedRateFor(mod)) =>
                        $"{key}:"
                        + FixedRateSpeedChange.ToString(
                            "R",
                            CultureInfo.InvariantCulture),
                    ManiaModId.DifficultyAdjust =>
                        $"{key}:hp="
                        + formatOptional(
                            DifficultyAdjustDrainRate)
                        + ":od="
                        + formatOptional(
                            DifficultyAdjustOverallDifficulty)
                        + ":"
                        + (DifficultyAdjustExtendedLimits
                            ? "extended"
                            : "normal"),
                    ManiaModId.Muted =>
                        $"{key}:"
                        + (MutedInverse ? "inverse" : "normal")
                        + ":"
                        + (MutedMetronome ? "metronome" : "no-metronome")
                        + $":combo={MutedComboCount}:"
                        + (MutedAffectsHitSounds
                            ? "hitsounds"
                            : "music-only"),
                    ManiaModId.WindUp or ManiaModId.WindDown =>
                        $"{key}:"
                        + TimeRampInitialRate.ToString(
                            "R",
                            CultureInfo.InvariantCulture)
                        + ">"
                        + TimeRampFinalRate.ToString(
                            "R",
                            CultureInfo.InvariantCulture)
                        + ":"
                        + (TimeRampAdjustPitch
                            ? "pitch"
                            : "tempo"),
                    ManiaModId.AdaptiveSpeed =>
                        $"{key}:initial="
                        + AdaptiveInitialRate.ToString(
                            "R",
                            CultureInfo.InvariantCulture)
                        + ":"
                        + (AdaptiveAdjustPitch ? "pitch" : "tempo"),
                    _ => key,
                };
            }));

    public IReadOnlyList<string> Acronyms =>
        mods.Select(static mod =>
                OsuManiaModParityCatalog.Get(mod).Acronym)
            .ToArray();

    public IReadOnlyList<string> DisplayLabels =>
        mods.Select(mod =>
            mod switch
            {
                ManiaModId.AccuracyChallenge =>
                    "AC "
                    + (AccuracyChallengeMinimum * 100).ToString(
                        "0.0",
                        CultureInfo.InvariantCulture)
                    + "% "
                    + (AccuracyChallengeMode
                       == ManiaAccuracyMode.MaximumAchievable
                        ? "MAX"
                        : "CURRENT"),
                ManiaModId.Perfect
                    when PerfectRequirePerfectHits => "PF MAX",
                ManiaModId.HalfTime
                    or ManiaModId.Daycore
                    or ManiaModId.DoubleTime
                    or ManiaModId.Nightcore
                    when !FixedRateSpeedChange.Equals(
                              defaultFixedRateFor(mod))
                         || FixedRateAdjustPitch =>
                    $"{OsuManiaModParityCatalog.Get(mod).Acronym} "
                    + $"{FixedRateSpeedChange:0.00}×"
                    + (FixedRateAdjustPitch ? " PITCH" : string.Empty),
                ManiaModId.DifficultyAdjust =>
                    difficultyAdjustDisplayLabel(),
                ManiaModId.Muted =>
                    $"MU {(MutedInverse ? "IN" : "OUT")}{MutedComboCount}",
                ManiaModId.WindUp or ManiaModId.WindDown =>
                    $"{OsuManiaModParityCatalog.Get(mod).Acronym} "
                    + $"{TimeRampInitialRate:0.00}→{TimeRampFinalRate:0.00}",
                ManiaModId.AdaptiveSpeed =>
                    $"AS {AdaptiveInitialRate:0.00}",
                _ => OsuManiaModParityCatalog.Get(mod).Acronym,
            })
            .ToArray();

    public bool Contains(ManiaModId mod) => Array.IndexOf(mods, mod) >= 0;

    public ScoreRank AdjustRank(ScoreRank rank)
    {
        if (!mods.Any(visibilityMods.Contains))
            return rank;

        return rank switch
        {
            ScoreRank.X => ScoreRank.XH,
            ScoreRank.S => ScoreRank.SH,
            _ => rank,
        };
    }

    public ManiaModSet With(ManiaModId mod, bool enabled)
    {
        var next = mods.ToList();
        next.Remove(mod);

        if (enabled)
        {
            if (rateMods.Contains(mod))
            {
                next.RemoveAll(rateMods.Contains);
                next.RemoveAll(variableRateMods.Contains);
            }
            if (holdRuleMods.Contains(mod))
                next.RemoveAll(holdRuleMods.Contains);
            if (visibilityMods.Contains(mod))
                next.RemoveAll(visibilityMods.Contains);
            if (failRuleMods.Contains(mod))
                next.RemoveAll(failRuleMods.Contains);
            if (difficultyRuleMods.Contains(mod))
                next.RemoveAll(difficultyRuleMods.Contains);
            if (automationMods.Contains(mod))
                next.RemoveAll(automationMods.Contains);
            if (keyMods.Contains(mod))
                next.RemoveAll(keyMods.Contains);
            if (variableRateMods.Contains(mod))
            {
                next.RemoveAll(variableRateMods.Contains);
                next.RemoveAll(rateMods.Contains);
            }
            if (mod == ManiaModId.AdaptiveSpeed)
                next.RemoveAll(automationMods.Contains);
            else if (automationMods.Contains(mod))
                next.Remove(ManiaModId.AdaptiveSpeed);
            if (mod == ManiaModId.Invert)
                next.RemoveAll(holdRuleMods.Contains);
            else if (holdRuleMods.Contains(mod))
                next.Remove(ManiaModId.Invert);
            if (mod == ManiaModId.DifficultyAdjust)
                next.RemoveAll(difficultyRuleMods.Contains);
            else if (difficultyRuleMods.Contains(mod))
                next.Remove(ManiaModId.DifficultyAdjust);
            if (mod == ManiaModId.AccuracyChallenge)
            {
                next.Remove(ManiaModId.Easy);
                next.Remove(ManiaModId.NoFail);
                next.Remove(ManiaModId.Perfect);
            }
            else if (mod is ManiaModId.Easy
                     or ManiaModId.NoFail
                     or ManiaModId.Perfect)
            {
                next.Remove(ManiaModId.AccuracyChallenge);
            }
            if (mod == ManiaModId.Cinema)
            {
                next.RemoveAll(failRuleMods.Contains);
                next.Remove(ManiaModId.AccuracyChallenge);
            }
            else if (failRuleMods.Contains(mod)
                     || mod == ManiaModId.AccuracyChallenge)
            {
                next.Remove(ManiaModId.Cinema);
            }

            next.Add(mod);
        }

        return next.Count == 0
            ? Empty
            : new ManiaModSet(
                next,
                next.Contains(ManiaModId.Random)
                    ? RandomSeed
                    : null,
                CoverCoverage,
                CoverDirection,
                FlashlightSizeMultiplier,
                FlashlightComboBasedSize,
                AccuracyChallengeMinimum,
                AccuracyChallengeMode,
                DifficultyAdjustDrainRate,
                DifficultyAdjustOverallDifficulty,
                DifficultyAdjustExtendedLimits,
                MutedInverse,
                MutedMetronome,
                MutedComboCount,
                MutedAffectsHitSounds,
                enabled
                && mod is (ManiaModId.WindUp
                    or ManiaModId.WindDown)
                    ? null
                    : HasTimeRamp
                        ? TimeRampInitialRate
                        : null,
                enabled
                && mod is (ManiaModId.WindUp
                    or ManiaModId.WindDown)
                    ? null
                    : HasTimeRamp
                        ? TimeRampFinalRate
                        : null,
                TimeRampAdjustPitch,
                AdaptiveInitialRate,
                AdaptiveAdjustPitch,
                PerfectRequirePerfectHits,
                enabled
                && rateMods.Contains(mod)
                && !Contains(mod)
                    ? null
                    : FixedRateSpeedChange,
                FixedRateAdjustPitch);
    }

    public ManiaModSet WithRandomSeed(int seed)
    {
        var next = mods.ToList();
        if (!next.Contains(ManiaModId.Random))
            next.Add(ManiaModId.Random);

        return new ManiaModSet(
            next,
            seed,
            CoverCoverage,
            CoverDirection,
            FlashlightSizeMultiplier,
            FlashlightComboBasedSize,
            AccuracyChallengeMinimum,
            AccuracyChallengeMode,
            DifficultyAdjustDrainRate,
            DifficultyAdjustOverallDifficulty,
            DifficultyAdjustExtendedLimits,
            MutedInverse,
            MutedMetronome,
            MutedComboCount,
            MutedAffectsHitSounds,
            HasTimeRamp ? TimeRampInitialRate : null,
            HasTimeRamp ? TimeRampFinalRate : null,
            TimeRampAdjustPitch,
            AdaptiveInitialRate,
            AdaptiveAdjustPitch,
            PerfectRequirePerfectHits,
            FixedRateSpeedChange,
            FixedRateAdjustPitch);
    }

    public ManiaModSet WithCover(
        double coverage,
        ManiaCoverDirection direction)
    {
        var next = mods.ToList();
        next.RemoveAll(visibilityMods.Contains);
        next.Add(ManiaModId.Cover);
        return new ManiaModSet(
            next,
            RandomSeed,
            coverage,
            direction,
            FlashlightSizeMultiplier,
            FlashlightComboBasedSize,
            AccuracyChallengeMinimum,
            AccuracyChallengeMode,
            DifficultyAdjustDrainRate,
            DifficultyAdjustOverallDifficulty,
            DifficultyAdjustExtendedLimits,
            MutedInverse,
            MutedMetronome,
            MutedComboCount,
            MutedAffectsHitSounds,
            HasTimeRamp ? TimeRampInitialRate : null,
            HasTimeRamp ? TimeRampFinalRate : null,
            TimeRampAdjustPitch,
            AdaptiveInitialRate,
            AdaptiveAdjustPitch,
            PerfectRequirePerfectHits,
            FixedRateSpeedChange,
            FixedRateAdjustPitch);
    }

    public ManiaModSet WithFlashlight(
        double sizeMultiplier,
        bool comboBasedSize)
    {
        var next = mods.ToList();
        next.RemoveAll(visibilityMods.Contains);
        next.Add(ManiaModId.Flashlight);
        return new ManiaModSet(
            next,
            RandomSeed,
            CoverCoverage,
            CoverDirection,
            sizeMultiplier,
            comboBasedSize,
            AccuracyChallengeMinimum,
            AccuracyChallengeMode,
            DifficultyAdjustDrainRate,
            DifficultyAdjustOverallDifficulty,
            DifficultyAdjustExtendedLimits,
            MutedInverse,
            MutedMetronome,
            MutedComboCount,
            MutedAffectsHitSounds,
            HasTimeRamp ? TimeRampInitialRate : null,
            HasTimeRamp ? TimeRampFinalRate : null,
            TimeRampAdjustPitch,
            AdaptiveInitialRate,
            AdaptiveAdjustPitch,
            PerfectRequirePerfectHits,
            FixedRateSpeedChange,
            FixedRateAdjustPitch);
    }

    public ManiaModSet WithAccuracyChallenge(
        double minimumAccuracy,
        ManiaAccuracyMode mode)
    {
        var next = mods.ToList();
        next.Remove(ManiaModId.Easy);
        next.Remove(ManiaModId.NoFail);
        next.Remove(ManiaModId.Perfect);
        if (!next.Contains(ManiaModId.AccuracyChallenge))
            next.Add(ManiaModId.AccuracyChallenge);

        return new ManiaModSet(
            next,
            RandomSeed,
            CoverCoverage,
            CoverDirection,
            FlashlightSizeMultiplier,
            FlashlightComboBasedSize,
            minimumAccuracy,
            mode,
            DifficultyAdjustDrainRate,
            DifficultyAdjustOverallDifficulty,
            DifficultyAdjustExtendedLimits,
            MutedInverse,
            MutedMetronome,
            MutedComboCount,
            MutedAffectsHitSounds,
            HasTimeRamp ? TimeRampInitialRate : null,
            HasTimeRamp ? TimeRampFinalRate : null,
            TimeRampAdjustPitch,
            AdaptiveInitialRate,
            AdaptiveAdjustPitch,
            PerfectRequirePerfectHits,
            FixedRateSpeedChange,
            FixedRateAdjustPitch);
    }

    public ManiaModSet WithPerfect(bool requirePerfectHits)
    {
        ManiaModSet enabled = With(ManiaModId.Perfect, true);

        return new ManiaModSet(
            enabled.mods,
            enabled.RandomSeed,
            enabled.CoverCoverage,
            enabled.CoverDirection,
            enabled.FlashlightSizeMultiplier,
            enabled.FlashlightComboBasedSize,
            enabled.AccuracyChallengeMinimum,
            enabled.AccuracyChallengeMode,
            enabled.DifficultyAdjustDrainRate,
            enabled.DifficultyAdjustOverallDifficulty,
            enabled.DifficultyAdjustExtendedLimits,
            enabled.MutedInverse,
            enabled.MutedMetronome,
            enabled.MutedComboCount,
            enabled.MutedAffectsHitSounds,
            enabled.HasTimeRamp
                ? enabled.TimeRampInitialRate
                : null,
            enabled.HasTimeRamp
                ? enabled.TimeRampFinalRate
                : null,
            enabled.TimeRampAdjustPitch,
            enabled.AdaptiveInitialRate,
            enabled.AdaptiveAdjustPitch,
            requirePerfectHits,
            enabled.FixedRateSpeedChange,
            enabled.FixedRateAdjustPitch);
    }

    public ManiaModSet WithFixedRate(
        ManiaModId mod,
        double speedChange,
        bool adjustPitch = false)
    {
        if (!rateMods.Contains(mod))
            throw new ArgumentOutOfRangeException(nameof(mod));

        ManiaModSet enabled = With(mod, true);
        return new ManiaModSet(
            enabled.mods,
            enabled.RandomSeed,
            enabled.CoverCoverage,
            enabled.CoverDirection,
            enabled.FlashlightSizeMultiplier,
            enabled.FlashlightComboBasedSize,
            enabled.AccuracyChallengeMinimum,
            enabled.AccuracyChallengeMode,
            enabled.DifficultyAdjustDrainRate,
            enabled.DifficultyAdjustOverallDifficulty,
            enabled.DifficultyAdjustExtendedLimits,
            enabled.MutedInverse,
            enabled.MutedMetronome,
            enabled.MutedComboCount,
            enabled.MutedAffectsHitSounds,
            enabled.HasTimeRamp
                ? enabled.TimeRampInitialRate
                : null,
            enabled.HasTimeRamp
                ? enabled.TimeRampFinalRate
                : null,
            enabled.TimeRampAdjustPitch,
            enabled.AdaptiveInitialRate,
            enabled.AdaptiveAdjustPitch,
            enabled.PerfectRequirePerfectHits,
            speedChange,
            adjustPitch);
    }

    public ManiaModSet WithDifficultyAdjust(
        double? drainRate,
        double? overallDifficulty,
        bool extendedLimits)
    {
        var next = mods.ToList();
        next.RemoveAll(difficultyRuleMods.Contains);
        if (!next.Contains(ManiaModId.DifficultyAdjust))
            next.Add(ManiaModId.DifficultyAdjust);

        if (!extendedLimits)
        {
            drainRate = drainRate is double hp
                ? Math.Clamp(hp, 0, 10)
                : null;
            overallDifficulty = overallDifficulty is double od
                ? Math.Clamp(od, 0, 10)
                : null;
        }

        return new ManiaModSet(
            next,
            RandomSeed,
            CoverCoverage,
            CoverDirection,
            FlashlightSizeMultiplier,
            FlashlightComboBasedSize,
            AccuracyChallengeMinimum,
            AccuracyChallengeMode,
            drainRate,
            overallDifficulty,
            extendedLimits,
            MutedInverse,
            MutedMetronome,
            MutedComboCount,
            MutedAffectsHitSounds,
            HasTimeRamp ? TimeRampInitialRate : null,
            HasTimeRamp ? TimeRampFinalRate : null,
            TimeRampAdjustPitch,
            AdaptiveInitialRate,
            AdaptiveAdjustPitch,
            PerfectRequirePerfectHits,
            FixedRateSpeedChange,
            FixedRateAdjustPitch);
    }

    public ManiaModSet WithMuted(
        bool inverse,
        bool metronome,
        int comboCount,
        bool affectsHitSounds)
    {
        comboCount = Math.Clamp(comboCount, inverse ? 1 : 0, 500);
        var next = mods.ToList();
        if (!next.Contains(ManiaModId.Muted))
            next.Add(ManiaModId.Muted);

        return new ManiaModSet(
            next,
            RandomSeed,
            CoverCoverage,
            CoverDirection,
            FlashlightSizeMultiplier,
            FlashlightComboBasedSize,
            AccuracyChallengeMinimum,
            AccuracyChallengeMode,
            DifficultyAdjustDrainRate,
            DifficultyAdjustOverallDifficulty,
            DifficultyAdjustExtendedLimits,
            inverse,
            metronome,
            comboCount,
            affectsHitSounds,
            HasTimeRamp ? TimeRampInitialRate : null,
            HasTimeRamp ? TimeRampFinalRate : null,
            TimeRampAdjustPitch,
            AdaptiveInitialRate,
            AdaptiveAdjustPitch,
            PerfectRequirePerfectHits,
            FixedRateSpeedChange,
            FixedRateAdjustPitch);
    }

    public ManiaModSet WithTimeRamp(
        ManiaModId mod,
        double initialRate,
        double finalRate,
        bool adjustPitch)
    {
        if (mod is not ManiaModId.WindUp
            and not ManiaModId.WindDown)
        {
            throw new ArgumentOutOfRangeException(nameof(mod));
        }

        var next = mods.ToList();
        next.RemoveAll(rateMods.Contains);
        next.RemoveAll(variableRateMods.Contains);
        next.Add(mod);
        return new ManiaModSet(
            next,
            RandomSeed,
            CoverCoverage,
            CoverDirection,
            FlashlightSizeMultiplier,
            FlashlightComboBasedSize,
            AccuracyChallengeMinimum,
            AccuracyChallengeMode,
            DifficultyAdjustDrainRate,
            DifficultyAdjustOverallDifficulty,
            DifficultyAdjustExtendedLimits,
            MutedInverse,
            MutedMetronome,
            MutedComboCount,
            MutedAffectsHitSounds,
            initialRate,
            finalRate,
            adjustPitch,
            AdaptiveInitialRate,
            AdaptiveAdjustPitch,
            PerfectRequirePerfectHits,
            null,
            false);
    }

    public ManiaModSet WithAdaptiveSpeed(
        double initialRate,
        bool adjustPitch)
    {
        var next = mods.ToList();
        next.RemoveAll(rateMods.Contains);
        next.RemoveAll(variableRateMods.Contains);
        next.RemoveAll(automationMods.Contains);
        next.Add(ManiaModId.AdaptiveSpeed);
        return new ManiaModSet(
            next,
            RandomSeed,
            CoverCoverage,
            CoverDirection,
            FlashlightSizeMultiplier,
            FlashlightComboBasedSize,
            AccuracyChallengeMinimum,
            AccuracyChallengeMode,
            DifficultyAdjustDrainRate,
            DifficultyAdjustOverallDifficulty,
            DifficultyAdjustExtendedLimits,
            MutedInverse,
            MutedMetronome,
            MutedComboCount,
            MutedAffectsHitSounds,
            null,
            null,
            true,
            initialRate,
            adjustPitch,
            PerfectRequirePerfectHits,
            null,
            false);
    }

    public bool Equals(ManiaModSet? other) =>
        ReferenceEquals(this, other)
        || other != null
        && mods.SequenceEqual(other.mods)
        && RandomSeed == other.RandomSeed
        && CoverCoverage.Equals(other.CoverCoverage)
        && CoverDirection == other.CoverDirection
        && FlashlightSizeMultiplier.Equals(
            other.FlashlightSizeMultiplier)
        && FlashlightComboBasedSize
           == other.FlashlightComboBasedSize
        && AccuracyChallengeMinimum.Equals(
            other.AccuracyChallengeMinimum)
        && AccuracyChallengeMode == other.AccuracyChallengeMode
        && PerfectRequirePerfectHits
           == other.PerfectRequirePerfectHits
        && DifficultyAdjustDrainRate
           == other.DifficultyAdjustDrainRate
        && DifficultyAdjustOverallDifficulty
           == other.DifficultyAdjustOverallDifficulty
        && DifficultyAdjustExtendedLimits
           == other.DifficultyAdjustExtendedLimits
        && MutedInverse == other.MutedInverse
        && MutedMetronome == other.MutedMetronome
        && MutedComboCount == other.MutedComboCount
        && MutedAffectsHitSounds == other.MutedAffectsHitSounds
        && TimeRampInitialRate.Equals(other.TimeRampInitialRate)
        && TimeRampFinalRate.Equals(other.TimeRampFinalRate)
        && TimeRampAdjustPitch == other.TimeRampAdjustPitch
        && AdaptiveInitialRate.Equals(other.AdaptiveInitialRate)
        && AdaptiveAdjustPitch == other.AdaptiveAdjustPitch
        && FixedRateSpeedChange.Equals(other.FixedRateSpeedChange)
        && FixedRateAdjustPitch == other.FixedRateAdjustPitch;

    public override bool Equals(object? obj) =>
        obj is ManiaModSet other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (ManiaModId mod in mods)
            hash.Add(mod);
        hash.Add(RandomSeed);
        hash.Add(CoverCoverage);
        hash.Add(CoverDirection);
        hash.Add(FlashlightSizeMultiplier);
        hash.Add(FlashlightComboBasedSize);
        hash.Add(AccuracyChallengeMinimum);
        hash.Add(AccuracyChallengeMode);
        hash.Add(PerfectRequirePerfectHits);
        hash.Add(DifficultyAdjustDrainRate);
        hash.Add(DifficultyAdjustOverallDifficulty);
        hash.Add(DifficultyAdjustExtendedLimits);
        hash.Add(MutedInverse);
        hash.Add(MutedMetronome);
        hash.Add(MutedComboCount);
        hash.Add(MutedAffectsHitSounds);
        hash.Add(TimeRampInitialRate);
        hash.Add(TimeRampFinalRate);
        hash.Add(TimeRampAdjustPitch);
        hash.Add(AdaptiveInitialRate);
        hash.Add(AdaptiveAdjustPitch);
        hash.Add(FixedRateSpeedChange);
        hash.Add(FixedRateAdjustPitch);

        return hash.ToHashCode();
    }

    private static void validateDifficultyAdjustValue(
        double? value,
        double minimum,
        double maximum,
        string parameterName)
    {
        if (value is not double actual)
            return;

        if (!double.IsFinite(actual)
            || actual < minimum
            || actual > maximum)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                $"Difficulty Adjust value must be between {minimum} and {maximum}.");
        }
    }

    private static string formatOptional(double? value) =>
        value?.ToString("R", CultureInfo.InvariantCulture) ?? "map";

    private static bool isSlowRateMod(ManiaModId mod) =>
        mod is ManiaModId.HalfTime or ManiaModId.Daycore;

    private static double defaultFixedRateFor(ManiaModId mod) =>
        isSlowRateMod(mod) ? 0.75 : 1.5;

    private string difficultyAdjustDisplayLabel()
    {
        string hp = DifficultyAdjustDrainRate is double drain
            ? " HP"
              + drain.ToString("0.0", CultureInfo.InvariantCulture)
            : string.Empty;
        string od =
            DifficultyAdjustOverallDifficulty is double difficulty
                ? " OD"
                  + difficulty.ToString(
                      "0.0",
                      CultureInfo.InvariantCulture)
                : string.Empty;
        return hp.Length == 0 && od.Length == 0
            ? "DA"
            : "DA" + hp + od;
    }
}
