using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;
using Yokko.Audio;
using Yokko.Core.Beatmaps;
using Yokko.Core.Difficulty;
using Yokko.Core.Mods;
using Yokko.Core.Scoring;
using Yokko.Game.Presentation;

namespace Yokko.Game.Screens.Gameplay;

public partial class GameplayHud : CompositeDrawable
{
    private readonly YokkoBeatmap beatmap;
    private readonly ManiaModSet mods;
    private readonly JudgementConfiguration judgementConfiguration;
    private readonly SpriteText timeText;
    private readonly SpriteText modsText;
    private readonly SpriteText comboText;
    private readonly SpriteText accuracyText;
    private readonly SpriteText challengeText;
    private readonly SpriteText mutedText;
    private readonly SpriteText rateText;
    private readonly SpriteText countsText;
    private readonly SpriteText audioText;
    private readonly Box healthFill;
    private readonly SpriteText healthText;
    private readonly Container healthContainer;
    private AudioReadoutState displayedAudioState;
    private bool hasDisplayedAudioState;

    internal string DisplayedAudioStatus =>
        audioText?.Text.ToString() ?? string.Empty;
    internal double DisplayedHealth { get; private set; } = 1;
    internal int DisplayedExtraLives { get; private set; }
    internal int ExtraLifePulseCount { get; private set; }
    internal double DisplayedChallengeAccuracy { get; private set; } = 1;
    internal string DisplayedRuleStatus =>
        challengeText?.Text.ToString() ?? string.Empty;
    internal string DisplayedDynamicRate =>
        rateText?.Text.ToString() ?? string.Empty;
    internal string DisplayedMods =>
        modsText?.Text.ToString() ?? string.Empty;
    internal bool UsesLegacySkinHealthBar { get; }

    public GameplayHud(
        YokkoBeatmap beatmap,
        ManiaModSet mods = null,
        JudgementConfiguration? judgementConfiguration = null,
        bool useLegacySkinHealthBar = false)
    {
        this.beatmap = beatmap;
        this.mods = mods ?? ManiaModSet.Empty;
        this.judgementConfiguration =
            judgementConfiguration ?? JudgementConfiguration.YokkoDefault;
        UsesLegacySkinHealthBar = useLegacySkinHealthBar;
        Width = 340;
        Height = 330;
        Masking = true;

        InternalChildren = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = new Color4(0.035f, 0.045f, 0.065f, 0.92f),
            },
            new FillFlowContainer
            {
                RelativeSizeAxes = Axes.Both,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 10),
                Padding = new MarginPadding(18),
                Children = new Drawable[]
                {
                    new SpriteText
                    {
                        Text =
                            $"{beatmap.Title} [{beatmap.DifficultyName}]",
                        Font = FontUsage.Default.With(size: 20),
                        Colour = YokkoPalette.Text,
                    },
                    modsText = new SpriteText
                    {
                        Text = formatRulesLabel(
                            this.mods,
                            this.judgementConfiguration),
                        Font = FontUsage.Default.With(
                            size: 13,
                            weight: "SemiBold"),
                        Colour = YokkoPalette.Rose,
                    },
                    timeText = createLine(),
                    comboText = createLine(),
                    accuracyText = createLine(),
                    challengeText = createLine(14),
                    mutedText = createLine(14),
                    rateText = createLine(14),
                    countsText = createLine(16),
                    healthContainer = new Container
                    {
                        Size = useLegacySkinHealthBar
                            ? Vector2.Zero
                            : new Vector2(304, 25),
                        Masking = true,
                        CornerRadius = 5,
                        Children = new Drawable[]
                        {
                            new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Colour = new Color4(
                                    0.08f,
                                    0.1f,
                                    0.15f,
                                    1f),
                            },
                            healthFill = new Box
                            {
                                RelativeSizeAxes = Axes.Y,
                                Width = 304,
                                Colour = YokkoPalette.Cyan,
                            },
                            healthText = new SpriteText
                            {
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                Text = "HP 100%",
                                Font = FontUsage.Default.With(
                                    size: 13,
                                    weight: "SemiBold"),
                                Colour = Color4.White,
                            },
                        },
                    },
                    audioText = createLine(14),
                },
            },
        };
    }

    private static string formatRulesLabel(
        ManiaModSet mods,
        JudgementConfiguration judgementConfiguration)
    {
        string modLabel = mods.IsEmpty
            ? "NM"
            : string.Join("  ", mods.DisplayLabels);
        return judgementConfiguration.Mode == JudgementMode.Etterna
            ? $"MODS · {modLabel}  ·  ETTERNA "
              + judgementConfiguration.EtternaJusticeLabel.ToUpperInvariant()
            : $"MODS · {modLabel}";
    }

    public void UpdateState(
        double gameplayTimeMilliseconds,
        BeatmapJudgementState state,
        ManiaHealthState healthState = null)
    {
        timeText.Text = $"Time {Math.Max(0, gameplayTimeMilliseconds / 1000):0.00}s";
        bool etterna =
            judgementConfiguration.Mode == JudgementMode.Etterna;
        comboText.Text = etterna
            ? $"Score {state.Score:0000000}  Combo {state.Combo}"
              + $" / Miss {state.MissCombo} / Max {state.MaxCombo}"
              + $"  ·  CB {state.ComboBreaks}"
            : $"Score {state.Score:0000000}  Combo {state.Combo} / Max {state.MaxCombo}";
        comboText.Colour =
            etterna && state.MissCombo > 0
                ? YokkoPalette.Rose
                : YokkoPalette.TextMuted;
        string rank = etterna
            ? EtternaScoringRules.GradeLabel(state.Accuracy)
            : mods.AdjustRank(state.Rank).ToDisplayLabel();
        accuracyText.Text = etterna
            ? $"WIFE3 {state.Accuracy * 100:0.00}%  Grade {rank}"
            : $"Accuracy {state.Accuracy * 100:0.00}%  Rank {rank}";
        updateAccuracyChallenge(state);
        countsText.Text =
            judgementConfiguration.Mode == JudgementMode.Etterna
                ? $"M {state.Counts.Perfect}  P {state.Counts.Great}  "
                  + $"Great {state.Counts.Good}  Good {state.Counts.Ok}  "
                  + $"Bad {state.Counts.Meh}  Miss {state.Counts.Miss}"
                : $"P {state.Counts.Perfect}  G {state.Counts.Great}  "
                  + $"Good {state.Counts.Good}  Ok {state.Counts.Ok}  "
                  + $"Meh {state.Counts.Meh}  M {state.Counts.Miss}";

        if (healthState != null)
            updateHealth(healthState);
    }

    private void updateAccuracyChallenge(
        BeatmapJudgementState state)
    {
        if (!mods.Contains(ManiaModId.AccuracyChallenge))
        {
            updateDifficultyAndScrollStatus();
            return;
        }

        DisplayedChallengeAccuracy =
            mods.AccuracyChallengeMode
            == ManiaAccuracyMode.MaximumAchievable
                ? state.MaximumAchievableAccuracy
                : state.Accuracy;
        string mode = mods.AccuracyChallengeMode
                      == ManiaAccuracyMode.MaximumAchievable
            ? "MAX"
            : "CURRENT";
        challengeText.Text =
            $"AC {mode} {DisplayedChallengeAccuracy * 100:0.00}%"
            + $"  ·  TARGET {mods.AccuracyChallengeMinimum * 100:0.0}%";
        challengeText.Colour =
            DisplayedChallengeAccuracy
            < mods.AccuracyChallengeMinimum
                ? YokkoPalette.Rose
                : DisplayedChallengeAccuracy
                  < mods.AccuracyChallengeMinimum + 0.03
                    ? new Color4(1f, 0.72f, 0.16f, 1f)
                    : YokkoPalette.Cyan;
    }

    private void updateDifficultyAndScrollStatus()
    {
        string difficulty = mods.Contains(
            ManiaModId.DifficultyAdjust)
            ? $"DA HP {mods.EffectiveDrainRate(beatmap.DrainRate):0.0}"
              + $"  ·  OD {mods.EffectiveOverallDifficulty(beatmap.OverallDifficulty):0.0}"
            : string.Empty;
        string constantSpeed =
            mods.Contains(ManiaModId.ConstantSpeed)
                ? "CS · CONSTANT SCROLL"
                : string.Empty;
        challengeText.Text = difficulty.Length > 0
                             && constantSpeed.Length > 0
            ? difficulty + "  ·  CS"
            : difficulty.Length > 0
                ? difficulty
                : constantSpeed;
        challengeText.Colour = YokkoPalette.Cyan;
    }

    private void updateHealth(ManiaHealthState healthState)
    {
        DisplayedHealth = healthState.Health;
        DisplayedExtraLives = healthState.RemainingExtraLives;

        healthFill.ResizeWidthTo(
            304 * (float)healthState.Health,
            110,
            Easing.OutQuint);
        healthFill.FadeColour(
            healthState.Health switch
            {
                <= 0.2 => YokkoPalette.Rose,
                <= 0.5 => new Color4(1f, 0.72f, 0.16f, 1f),
                _ => YokkoPalette.Cyan,
            },
            110,
            Easing.OutQuint);
        string lives = healthState.RemainingExtraLives > 0
            ? $"  ·  LIFE ×{healthState.RemainingExtraLives}"
            : string.Empty;
        healthText.Text =
            $"HP {healthState.Health * 100:0}%{lives}";
    }

    public void ShowExtraLifeUsed()
    {
        ExtraLifePulseCount++;
        healthFill.FlashColour(
            new Color4(1f, 0.82f, 0.2f, 1f),
            520,
            Easing.OutQuint);
        healthText.FlashColour(
            new Color4(1f, 0.9f, 0.3f, 1f),
            520,
            Easing.OutQuint);
        healthText.ScaleTo(1.12f)
                  .ScaleTo(1, 360, Easing.OutBack);
    }

    public void UpdateAudioStatus(
        AudioEngineStatus status,
        AudioBackendKind requestedBackend)
    {
        bool hasSustainedCadenceMisses =
            status.CallbackCount >= 128
            && status.CallbackCadenceMissCount
               / (double)status.CallbackCount >= 0.01;
        bool hasBackendOverload = status.BackendOverloadCount > 0;
        var nextState = new AudioReadoutState(
            requestedBackend,
            status.ActiveBackend,
            status.SampleRate,
            status.BufferSize,
            status.DevicePeriodFrames,
            status.EstimatedOutputLatencyMilliseconds,
            status.UsesWasapiSharedExplicitPeriod,
            status.IsFaulted,
            status.HasUnderrun,
            status.CallbackDeadlineMissCount > 0,
            hasSustainedCadenceMisses,
            hasBackendOverload);
        if (hasDisplayedAudioState
            && nextState == displayedAudioState)
            return;

        displayedAudioState = nextState;
        hasDisplayedAudioState = true;

        bool fellBack =
            requestedBackend == AudioBackendKind.WasapiExclusive
            && status.ActiveBackend == AudioBackendKind.SharedWasapi;
        bool unhealthy =
            status.IsFaulted
            || status.HasUnderrun
            || status.CallbackDeadlineMissCount > 0
            || hasSustainedCadenceMisses
            || hasBackendOverload;
        string backend = status.ActiveBackend switch
        {
            AudioBackendKind.WasapiExclusive => "WASAPI EXCLUSIVE",
            AudioBackendKind.SharedWasapi =>
                status.UsesWasapiSharedExplicitPeriod
                    ? "WASAPI SHARED EXPLICIT PERIOD"
                    : "WASAPI SHARED LEGACY",
            AudioBackendKind.Asio => "ASIO",
            _ => "AUDIO STARTING",
        };
        string state = status.IsFaulted
            ? " · FAULT"
            : fellBack
                ? " · FALLBACK"
                : unhealthy
                    ? " · UNSTABLE"
                    : string.Empty;
        string timing = status.SampleRate > 0 && status.BufferSize > 0
            ? $" · {status.BufferSize}f · "
              + (status.DevicePeriodFrames > 0
                  ? $"{status.DevicePeriodFrames}f period · "
                  : string.Empty)
              + $"{status.EstimatedOutputLatencyMilliseconds:0.00} ms"
            : string.Empty;

        audioText.Text = backend + timing + state;
        audioText.Colour = unhealthy || fellBack
            ? YokkoPalette.Rose
            : YokkoPalette.TextMuted;
    }

    public void ShowFrameClock()
    {
        hasDisplayedAudioState = false;
        audioText.Text = "NO AUDIO · FRAME CLOCK";
        audioText.Colour = YokkoPalette.TextMuted;
    }

    public void UpdateMutedMix(ManiaMutedMix mix)
    {
        if (!mods.Contains(ManiaModId.Muted))
        {
            mutedText.Text = string.Empty;
            return;
        }

        mutedText.Text =
            $"MU · MUSIC {mix.MusicVolume * 100:0}%"
            + $" · KEYS {mix.HitSoundVolume * 100:0}%"
            + (mods.MutedMetronome
                ? $" · METRO {mix.MetronomeVolume * 100:0}%"
                : string.Empty);
        mutedText.Colour = YokkoPalette.Cyan;
    }

    public void UpdatePlaybackRate(
        double rate,
        double bpm,
        ManiaStarRatingResult difficulty,
        bool visible,
        bool practice)
    {
        if (!visible)
        {
            rateText.Text = string.Empty;
            return;
        }

        string mode = mods.HasAdaptiveSpeed
            ? "AS"
            : mods.Contains(ManiaModId.WindUp)
                ? "WU"
                : mods.Contains(ManiaModId.WindDown)
                    ? "WD"
                    : "RATE";
        string bpmText = bpm > 0
            ? $"{bpm:0.##} BPM"
            : "-- BPM";
        string difficultyText =
            ManiaStarRatingPresentation.FormatStar(difficulty);
        rateText.Text =
            $"{mode} · LIVE RATE {rate:0.00}×"
            + $" · {bpmText} · {difficultyText}"
            + (practice ? " · PRACTICE" : string.Empty);
        rateText.Colour = YokkoPalette.Cyan;
    }

    private readonly record struct AudioReadoutState(
        AudioBackendKind RequestedBackend,
        AudioBackendKind ActiveBackend,
        int SampleRate,
        int BufferSize,
        int DevicePeriodFrames,
        double EstimatedOutputLatencyMilliseconds,
        bool UsesWasapiSharedExplicitPeriod,
        bool IsFaulted,
        bool HasUnderrun,
        bool HasCallbackDeadlineMiss,
        bool HasSustainedCallbackCadenceMisses,
        bool HasBackendOverload);

    private static SpriteText createLine(float size = 18) => new()
    {
        Font = FontUsage.Default.With(size: size),
        Colour = YokkoPalette.TextMuted,
    };
}
