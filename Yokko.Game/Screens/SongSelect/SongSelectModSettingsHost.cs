using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;
using Yokko.Core.Beatmaps;
using Yokko.Core.Mods;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Screens.SongSelect;

internal partial class SongSelectModSettingsHost : CompositeDrawable
{
    private readonly Container accuracyPage;
    private readonly Container perfectPage;
    private readonly Container difficultyPage;
    private readonly Container mutedPage;
    private readonly Container fixedRatePage;
    private readonly Container timeRampPage;
    private readonly Container adaptivePage;
    private readonly Container keyPage;
    private readonly PageTab accuracyTab;
    private readonly PageTab difficultyTab;
    private readonly PageTab mutedTab;
    private readonly PageTab timeRampTab;
    private readonly PageTab keyTab;
    private ManiaModId activePage = ManiaModId.AccuracyChallenge;

    internal SongSelectAccuracyChallengeSettings AccuracySettings { get; }
    internal SongSelectPerfectSettings PerfectSettings { get; }
    internal SongSelectDifficultyAdjustSettings DifficultySettings { get; }
    internal SongSelectMutedSettings MutedSettings { get; }
    internal SongSelectFixedRateSettings FixedRateSettings { get; }
    internal SongSelectTimeRampSettings TimeRampSettings { get; }
    internal SongSelectAdaptiveSpeedSettings AdaptiveSettings { get; }
    internal SongSelectKeyConversionSettings KeySettings { get; }
    internal ManiaModId ActivePage => activePage;

    public SongSelectModSettingsHost(
        Action<double> accuracyMinimumChanged,
        Action<ManiaAccuracyMode> accuracyModeChanged,
        Action<bool> perfectRequireHitsChanged,
        Action<double?> drainRateChanged,
        Action<double?> overallDifficultyChanged,
        Action useMapDifficulty,
        Action<bool> extendedLimitsChanged,
        Action<bool> mutedInverseChanged,
        Action<bool> mutedMetronomeChanged,
        Action<int> mutedComboChanged,
        Action<bool> mutedHitSoundsChanged,
        Action<double> fixedRateChanged,
        Action<bool> fixedRatePitchChanged,
        Action<double> timeRampInitialChanged,
        Action<double> timeRampFinalChanged,
        Action<bool> timeRampPitchChanged,
        Action<double> adaptiveInitialChanged,
        Action<bool> adaptivePitchChanged)
        : this(
            accuracyMinimumChanged,
            accuracyModeChanged,
            perfectRequireHitsChanged,
            drainRateChanged,
            overallDifficultyChanged,
            useMapDifficulty,
            extendedLimitsChanged,
            mutedInverseChanged,
            mutedMetronomeChanged,
            mutedComboChanged,
            mutedHitSoundsChanged,
            fixedRateChanged,
            fixedRatePitchChanged,
            timeRampInitialChanged,
            timeRampFinalChanged,
            timeRampPitchChanged,
            adaptiveInitialChanged,
            adaptivePitchChanged,
            _ => { })
    {
    }

    internal SongSelectModSettingsHost(
        Action<double> accuracyMinimumChanged,
        Action<ManiaAccuracyMode> accuracyModeChanged,
        Action<bool> perfectRequireHitsChanged,
        Action<double?> drainRateChanged,
        Action<double?> overallDifficultyChanged,
        Action useMapDifficulty,
        Action<bool> extendedLimitsChanged,
        Action<bool> mutedInverseChanged,
        Action<bool> mutedMetronomeChanged,
        Action<int> mutedComboChanged,
        Action<bool> mutedHitSoundsChanged,
        Action<double> fixedRateChanged,
        Action<bool> fixedRatePitchChanged,
        Action<double> timeRampInitialChanged,
        Action<double> timeRampFinalChanged,
        Action<bool> timeRampPitchChanged,
        Action<double> adaptiveInitialChanged,
        Action<bool> adaptivePitchChanged,
        Action<ManiaModId> keyModToggled)
    {
        Size = new Vector2(202, 270);

        AccuracySettings = new SongSelectAccuracyChallengeSettings(
            accuracyMinimumChanged,
            accuracyModeChanged);
        PerfectSettings = new SongSelectPerfectSettings(
            perfectRequireHitsChanged);
        DifficultySettings = new SongSelectDifficultyAdjustSettings(
            drainRateChanged,
            overallDifficultyChanged,
            useMapDifficulty,
            extendedLimitsChanged);
        MutedSettings = new SongSelectMutedSettings(
            mutedInverseChanged,
            mutedMetronomeChanged,
            mutedComboChanged,
            mutedHitSoundsChanged);
        FixedRateSettings = new SongSelectFixedRateSettings(
            fixedRateChanged,
            fixedRatePitchChanged);
        TimeRampSettings = new SongSelectTimeRampSettings(
            timeRampInitialChanged,
            timeRampFinalChanged,
            timeRampPitchChanged);
        AdaptiveSettings = new SongSelectAdaptiveSpeedSettings(
            adaptiveInitialChanged,
            adaptivePitchChanged);
        KeySettings = new SongSelectKeyConversionSettings(
            keyModToggled);

        InternalChildren = new Drawable[]
        {
            new SpriteText
            {
                Y = 5,
                Text = "CFG",
                Font = HomeTypography.Display(10),
                Colour = SongSelectTheme.Muted,
            },
            accuracyTab = new PageTab(
                "AC",
                () => Show(activeFailPage()))
            {
                Position = new Vector2(27, 0),
            },
            difficultyTab = new PageTab(
                "DA",
                () => Show(ManiaModId.DifficultyAdjust))
            {
                Position = new Vector2(62, 0),
            },
            mutedTab = new PageTab(
                "MU",
                () => Show(ManiaModId.Muted))
            {
                Position = new Vector2(97, 0),
            },
            timeRampTab = new PageTab(
                "RATE",
                () => Show(activeRatePage()))
            {
                Position = new Vector2(132, 0),
            },
            keyTab = new PageTab(
                "KEY",
                () => Show(ManiaModId.Key4))
            {
                Position = new Vector2(167, 0),
            },
            new Box
            {
                Y = 34,
                Size = new Vector2(202, 1),
                Colour = new Color4(
                    SongSelectTheme.Cyan.R,
                    SongSelectTheme.Cyan.G,
                    SongSelectTheme.Cyan.B,
                    0.25f),
            },
            accuracyPage = new Container
            {
                Y = 43,
                Size = new Vector2(202, 224),
                Child = AccuracySettings,
            },
            perfectPage = new Container
            {
                Y = 43,
                Size = new Vector2(202, 224),
                Alpha = 0,
                Child = PerfectSettings,
            },
            difficultyPage = new Container
            {
                Y = 43,
                Size = new Vector2(202, 224),
                Alpha = 0,
                Child = DifficultySettings,
            },
            mutedPage = new Container
            {
                Y = 43,
                Size = new Vector2(202, 224),
                Alpha = 0,
                Child = MutedSettings,
            },
            fixedRatePage = new Container
            {
                Y = 43,
                Size = new Vector2(202, 224),
                Alpha = 0,
                Child = FixedRateSettings,
            },
            timeRampPage = new Container
            {
                Y = 43,
                Size = new Vector2(202, 224),
                Alpha = 0,
                Child = TimeRampSettings,
            },
            adaptivePage = new Container
            {
                Y = 43,
                Size = new Vector2(202, 224),
                Alpha = 0,
                Child = AdaptiveSettings,
            },
            keyPage = new Container
            {
                Y = 43,
                Size = new Vector2(202, 224),
                Alpha = 0,
                Child = KeySettings,
            },
        };

        updatePage();
    }

    public void Show(ManiaModId mod)
    {
        if (mod is not ManiaModId.Perfect
            and not ManiaModId.AccuracyChallenge
            and not ManiaModId.DifficultyAdjust
            and not ManiaModId.Muted
            and not ManiaModId.HalfTime
            and not ManiaModId.Daycore
            and not ManiaModId.DoubleTime
            and not ManiaModId.Nightcore
            and not ManiaModId.WindUp
            and not ManiaModId.WindDown
            and not ManiaModId.AdaptiveSpeed
            and not ManiaModId.Key1
            and not ManiaModId.Key2
            and not ManiaModId.Key3
            and not ManiaModId.Key4
            and not ManiaModId.Key5
            and not ManiaModId.Key6
            and not ManiaModId.Key7
            and not ManiaModId.Key8
            and not ManiaModId.Key9
            and not ManiaModId.Key10
            and not ManiaModId.DualStages)
        {
            return;
        }

        activePage = mod;
        updatePage();
    }

    public void SetState(
        ManiaModSet mods,
        YokkoBeatmap beatmap)
    {
        ArgumentNullException.ThrowIfNull(mods);
        ArgumentNullException.ThrowIfNull(beatmap);

        bool accuracyEnabled =
            mods.Contains(ManiaModId.AccuracyChallenge);
        bool perfectEnabled =
            mods.Contains(ManiaModId.Perfect);
        bool difficultyEnabled =
            mods.Contains(ManiaModId.DifficultyAdjust);
        bool mutedEnabled = mods.Contains(ManiaModId.Muted);
        ManiaModId fixedRateMod =
            mods.FixedRateMod
            ?? (activePage is ManiaModId.HalfTime
                or ManiaModId.Daycore
                or ManiaModId.DoubleTime
                or ManiaModId.Nightcore
                ? activePage
                : ManiaModId.HalfTime);
        bool fixedRateEnabled = mods.FixedRateMod.HasValue;
        bool windUpEnabled = mods.Contains(ManiaModId.WindUp);
        bool windDownEnabled = mods.Contains(ManiaModId.WindDown);
        bool adaptiveEnabled = mods.HasAdaptiveSpeed;
        if (activePage is ManiaModId.Perfect
                or ManiaModId.AccuracyChallenge
                or ManiaModId.DifficultyAdjust
            && !isActivePageEnabled())
        {
            activePage = perfectEnabled
                ? ManiaModId.Perfect
                : accuracyEnabled
                    ? ManiaModId.AccuracyChallenge
                    : difficultyEnabled
                        ? ManiaModId.DifficultyAdjust
                        : activePage;
        }

        PerfectSettings.SetState(
            perfectEnabled,
            mods.PerfectRequirePerfectHits);
        AccuracySettings.SetState(
            accuracyEnabled,
            mods.AccuracyChallengeMinimum,
            mods.AccuracyChallengeMode);
        DifficultySettings.SetState(
            difficultyEnabled,
            beatmap.DrainRate,
            beatmap.OverallDifficulty,
            mods.DifficultyAdjustDrainRate,
            mods.DifficultyAdjustOverallDifficulty,
            mods.DifficultyAdjustExtendedLimits);
        MutedSettings.SetState(
            mutedEnabled,
            mods.MutedInverse,
            mods.MutedMetronome,
            mods.MutedComboCount,
            mods.MutedAffectsHitSounds);
        FixedRateSettings.SetState(
            fixedRateEnabled,
            fixedRateMod,
            fixedRateEnabled
                ? mods.FixedRateSpeedChange
                : fixedRateMod is ManiaModId.HalfTime
                    or ManiaModId.Daycore
                    ? 0.75
                    : 1.5,
            fixedRateEnabled && mods.FixedRateAdjustPitch);
        ManiaModId timeRampMod = windDownEnabled
            ? ManiaModId.WindDown
            : ManiaModId.WindUp;
        TimeRampSettings.SetState(
            windUpEnabled || windDownEnabled,
            timeRampMod,
            mods.HasTimeRamp ? mods.TimeRampInitialRate : 1,
            mods.HasTimeRamp
                ? mods.TimeRampFinalRate
                : timeRampMod == ManiaModId.WindDown
                    ? 0.75
                    : 1.5,
            !mods.HasTimeRamp || mods.TimeRampAdjustPitch);
        AdaptiveSettings.SetState(
            adaptiveEnabled,
            adaptiveEnabled ? mods.AdaptiveInitialRate : 1,
            !adaptiveEnabled || mods.AdaptiveAdjustPitch);
        KeySettings.SetState(
            beatmap.ConversionSource is not null,
            mods.KeyConversionTarget,
            mods.HasDualStages);
        updatePage();

        bool isActivePageEnabled() => activePage switch
        {
            ManiaModId.Perfect => perfectEnabled,
            ManiaModId.AccuracyChallenge => accuracyEnabled,
            ManiaModId.DifficultyAdjust => difficultyEnabled,
            _ => true,
        };
    }

    private void updatePage()
    {
        bool showPerfect = activePage == ManiaModId.Perfect;
        bool showAccuracy =
            activePage == ManiaModId.AccuracyChallenge;
        bool showDifficulty =
            activePage == ManiaModId.DifficultyAdjust;
        perfectPage.Alpha = showPerfect ? 1 : 0;
        accuracyPage.Alpha = showAccuracy ? 1 : 0;
        difficultyPage.Alpha = showDifficulty ? 1 : 0;
        mutedPage.Alpha =
            activePage == ManiaModId.Muted ? 1 : 0;
        bool showFixedRate =
            activePage is ManiaModId.HalfTime
                or ManiaModId.Daycore
                or ManiaModId.DoubleTime
                or ManiaModId.Nightcore;
        fixedRatePage.Alpha = showFixedRate ? 1 : 0;
        bool showTimeRamp =
            activePage is ManiaModId.WindUp
                or ManiaModId.WindDown;
        timeRampPage.Alpha = showTimeRamp ? 1 : 0;
        bool showAdaptive =
            activePage == ManiaModId.AdaptiveSpeed;
        adaptivePage.Alpha = showAdaptive ? 1 : 0;
        bool showKey = activePage is >= ManiaModId.Key1
            and <= ManiaModId.Key10
            || activePage == ManiaModId.DualStages;
        keyPage.Alpha = showKey ? 1 : 0;
        accuracyTab.SetLabel(showPerfect ? "PF" : "AC");
        accuracyTab.SetSelected(showPerfect || showAccuracy);
        difficultyTab.SetSelected(showDifficulty);
        mutedTab.SetSelected(activePage == ManiaModId.Muted);
        timeRampTab.SetSelected(
            showFixedRate || showTimeRamp || showAdaptive);
        keyTab.SetSelected(showKey);
    }

    private ManiaModId activeRatePage() =>
        activePage is ManiaModId.WindUp
            or ManiaModId.WindDown
            or ManiaModId.AdaptiveSpeed
            or ManiaModId.HalfTime
            or ManiaModId.Daycore
            or ManiaModId.DoubleTime
            or ManiaModId.Nightcore
            ? activePage
            : ManiaModId.HalfTime;

    private ManiaModId activeFailPage() =>
        activePage is ManiaModId.Perfect
            or ManiaModId.AccuracyChallenge
            ? activePage
            : ManiaModId.AccuracyChallenge;

    private partial class PageTab : ClickableContainer
    {
        private readonly Box background;
        private readonly SpriteText label;

        public PageTab(string text, Action action)
        {
            Action = action;
            Size = new Vector2(33, 27);
            Masking = true;
            CornerRadius = 4;
            BorderThickness = 1;
            InternalChildren = new Drawable[]
            {
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                },
                label = new SpriteText
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Text = text,
                    Font = HomeTypography.Display(10),
                },
            };
        }

        public void SetSelected(bool selected)
        {
            BorderColour = selected
                ? SongSelectTheme.Yellow
                : SongSelectTheme.Cyan;
            background.Colour = selected
                ? SongSelectTheme.Pink
                : new Color4(
                    SongSelectTheme.Navy.R,
                    SongSelectTheme.Navy.G,
                    SongSelectTheme.Navy.B,
                    0.72f);
            label.Colour = selected
                ? SongSelectTheme.DeepNavy
                : SongSelectTheme.PaleCyan;
        }

        public void SetLabel(string text) =>
            label.Text = text;
    }
}
