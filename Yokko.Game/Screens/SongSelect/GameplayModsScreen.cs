using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
using osu.Framework.Screens;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;
using Yokko.Core.Beatmaps;
using Yokko.Core.Mods;
using Yokko.Core.Scoring;
using Yokko.Game.Gameplay;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Screens.SongSelect;

/// <summary>
/// Dedicated full-screen workspace for selecting and configuring gameplay
/// modifiers before starting a chart.
/// </summary>
internal partial class GameplayModsScreen : Screen
{
    private const float designed_width = 1280;
    private const float designed_height = 720;
    private const float detail_panel_width = 324;
    private const float detail_panel_right_margin = 36;
    private const float browser_left = 315;
    private const float browser_detail_gap = 55;
    private const float footer_height = 110;

    private static readonly ManiaModCategory[] visible_categories =
    [
        ManiaModCategory.DifficultyReduction,
        ManiaModCategory.DifficultyIncrease,
        ManiaModCategory.Conversion,
        ManiaModCategory.Automation,
        ManiaModCategory.Fun,
    ];

    private readonly YokkoBeatmap beatmap;
    private readonly Action<ManiaModSet> modsChanged;
    private readonly Dictionary<ManiaModCategory, GameplayModsCategoryButton>
        categoryButtons = new();
    private readonly Dictionary<ManiaModId, GameplayModListItem> visibleItems =
        new();
    private readonly List<Box> sectionDividers = new();

    private Container stage;
    private Drawable categoryRail;
    private Container modBrowser;
    private Container modList;
    private Container detailPanel;
    private Container decorations;
    private Container fixedRatePanel;
    private Container configurablePanel;
    private FillFlowContainer activeMods;
    private Box activeModsDivider;
    private Box detailPanelDivider;
    private Container detailBadge;
    private Box detailBadgeBackground;
    private SpriteText detailAcronym;
    private SpriteText detailName;
    private SpriteText detailHint;
    private TextFlowContainer detailDescription;
    private SpriteText settingsHeader;
    private Box settingsDivider;
    private SpriteText activeModsHeader;
    private SpriteText fixedRateLabel;
    private SpriteText fixedRateValue;
    private SpriteText fixedRateMinimum;
    private SpriteText fixedRateMidpoint;
    private SpriteText fixedRateMaximum;
    private GameplayModsRateSlider fixedRateSlider;
    private GameplayModsPitchButton fixedRatePitch;
    private GameplayModsResetButton resetButton;
    private SpriteText interactionHint;
    private SongSelectModSettingsHost settingsHost;
    private ManiaModCategory activeCategory =
        ManiaModCategory.DifficultyReduction;
    private ManiaModId detailMod = ManiaModId.HalfTime;
    private ManiaModSet selectedMods;
    private bool loadComplete;
    private bool selectionDirty;
    private double lastGlobalScrollAt = double.NegativeInfinity;
    private int modColumnCount = 2;
    private Vector2 lastResponsiveLayoutSize = new(-1);
    [Resolved]
    private YokkoManiaModPreferences modPreferences { get; set; }

    internal GameplayModsScreen(
        YokkoBeatmap beatmap,
        ManiaModSet selectedMods,
        Action<ManiaModSet> modsChanged)
    {
        ArgumentNullException.ThrowIfNull(beatmap);
        ArgumentNullException.ThrowIfNull(selectedMods);

        this.beatmap = beatmap;
        this.selectedMods = selectedMods;
        this.modsChanged = modsChanged;

        if (selectedMods.Mods.Count > 0)
        {
            detailMod = selectedMods.Contains(ManiaModId.HalfTime)
                ? ManiaModId.HalfTime
                : selectedMods.Mods[0];
            activeCategory =
                OsuManiaModParityCatalog.Get(detailMod).Category;
        }
    }

    internal ManiaModSet SelectedMods => selectedMods;
    internal ManiaModCategory ActiveCategory => activeCategory;
    internal ManiaModId DetailMod => detailMod;
    internal int VisibleModCount => visibleItems.Count;
    internal SongSelectModSettingsHost SettingsHost => settingsHost;
    internal bool DetailHintVisible => detailHint?.Alpha > 0;
    internal float SettingsHeaderY => settingsHeader?.Y ?? 0;
    internal float FixedRatePanelY => fixedRatePanel?.Y ?? 0;
    internal bool ResetEnabled => resetButton?.IsEnabled ?? false;
    internal static Vector2 CalculateResponsiveStageSize(Vector2 viewport) =>
        new(
            MathF.Max(viewport.X, designed_width),
            MathF.Max(viewport.Y, designed_height));

    internal static int CalculateBrowserColumnCount(float browserWidth) =>
        Math.Clamp((int)((browserWidth + 18) / 284), 2, 4);

    [BackgroundDependencyLoader]
    private void load(TextureStore textures)
    {
        Texture logo = textures.Get("home-logo-hd");

        InternalChildren = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = HomeControlColours.Ivory,
            },
            stage = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    createHeader(logo),
                    categoryRail = createCategoryRail(),
                    modBrowser = createModBrowser(),
                    detailPanel = createDetailPanel(),
                    decorations = createDecorations(),
                    createFooter(),
                },
            },
        };

        rebuildModList();
        updateSelection();
        selectDetail(detailMod);
        stage.Alpha = 0;
        stage.Y = 12;
    }

    protected override void LoadComplete()
    {
        base.LoadComplete();
        loadComplete = true;
    }

    protected override void Update()
    {
        base.Update();
        updateResponsiveLayout();
    }

    public override void OnEntering(ScreenTransitionEvent e)
    {
        base.OnEntering(e);
        stage.FadeIn(220, Easing.OutQuint)
             .MoveToY(0, 360, Easing.OutQuint);
    }

    public override bool OnExiting(ScreenExitEvent e)
    {
        CommitSelection();
        stage.FadeOut(150, Easing.OutQuint)
             .MoveToY(8, 180, Easing.OutQuint);
        return base.OnExiting(e);
    }

    private void updateResponsiveLayout()
    {
        if (stage == null || DrawWidth <= 0 || DrawHeight <= 0)
            return;

        Vector2 stageSize = CalculateResponsiveStageSize(
            new Vector2(DrawWidth, DrawHeight));
        if ((stageSize - lastResponsiveLayoutSize).LengthSquared
            < 0.01f)
        {
            return;
        }

        lastResponsiveLayoutSize = stageSize;
        Vector2 extra = stageSize
                        - new Vector2(designed_width, designed_height);
        float contentY = 118 + extra.Y * 0.5f;
        float detailLeft = stageSize.X
                           - detail_panel_right_margin
                           - detail_panel_width;
        float browserWidth = MathF.Max(
            550,
            detailLeft - browser_left - browser_detail_gap);

        categoryRail.Y = contentY + 8;
        modBrowser.Y = contentY + 1;
        modBrowser.Width = browserWidth;
        detailPanel.Y = contentY;
        detailPanelDivider.Y = contentY - 8;

        foreach (Box divider in sectionDividers)
            divider.Width = MathF.Max(browserWidth - 145, 120);

        int nextColumnCount =
            CalculateBrowserColumnCount(browserWidth);
        if (nextColumnCount == modColumnCount)
            return;

        modColumnCount = nextColumnCount;
        rebuildModList();
        selectDetail(detailMod);
    }

    protected override bool OnKeyDown(KeyDownEvent e) =>
        HandleInteractionKey(e.Key) || base.OnKeyDown(e);

    internal bool HandleInteractionKey(Key key)
    {
        switch (key)
        {
            case Key.Escape:
            case Key.M:
                this.Exit();
                return true;

            case Key.R:
                ResetMods();
                showInteractionHint(
                    selectedMods.Mods.Count == 0
                        ? "ALL MODS CLEARED"
                        : "R · RESET MODS");
                return true;

            case Key.Tab:
                CycleCategory(1);
                showInteractionHint(
                    "TAB · NEXT CATEGORY   ARROWS · NAVIGATE");
                return true;

            case Key.Left:
                MoveDetailFocus(new Vector2(-1, 0));
                return true;

            case Key.Right:
                MoveDetailFocus(new Vector2(1, 0));
                return true;

            case Key.Up:
                MoveDetailFocus(new Vector2(0, -1));
                return true;

            case Key.Down:
                MoveDetailFocus(new Vector2(0, 1));
                return true;

            case Key.Enter:
            case Key.KeypadEnter:
            case Key.Space:
                ToggleMod(detailMod);
                showInteractionHint(
                    selectedMods.Contains(detailMod)
                        ? $"{OsuManiaModParityCatalog.Get(detailMod).Acronym} · ACTIVE"
                        : $"{OsuManiaModParityCatalog.Get(detailMod).Acronym} · REMOVED");
                return true;

            case Key.P:
                if (isPitchAdjustableFixedRate(detailMod)
                    && selectedMods.FixedRateMod == detailMod)
                {
                    SetFixedRateAdjustPitch(
                        !selectedMods.FixedRateAdjustPitch);
                    showInteractionHint(
                        selectedMods.FixedRateAdjustPitch
                            ? "MUSIC PITCH · ON"
                            : "MUSIC PITCH · OFF");
                    return true;
                }
                break;

            case Key.Plus:
            case Key.KeypadPlus:
                return adjustFocusedFixedRate(0.01);

            case Key.Minus:
            case Key.KeypadMinus:
                return adjustFocusedFixedRate(-0.01);

            case Key.H:
                ToggleMod(ManiaModId.HalfTime);
                showInteractionHint(
                    selectedMods.Contains(ManiaModId.HalfTime)
                        ? "HT · ACTIVE"
                        : "HT · REMOVED");
                return true;

            default:
                break;
        }

        return false;
    }

    protected override bool OnScroll(ScrollEvent e)
    {
        if (e.ScrollDelta.Y == 0)
            return base.OnScroll(e);

        if (Time.Current - lastGlobalScrollAt < 70)
            return true;

        lastGlobalScrollAt = Time.Current;
        NavigateByScroll(e.ScrollDelta.Y);
        showInteractionHint(
            "WHEEL / ARROWS · NAVIGATE   SPACE · TOGGLE");
        return true;
    }

    internal void NavigateByScroll(double delta) =>
        MoveDetailFocus(new Vector2(0, delta > 0 ? -1 : 1));

    internal void SetCategory(ManiaModCategory category)
    {
        if (!visible_categories.Contains(category))
            return;

        activeCategory = category;
        foreach ((ManiaModCategory itemCategory,
                     GameplayModsCategoryButton button)
                 in categoryButtons)
        {
            button.SetSelected(itemCategory == category);
        }

        rebuildModList();
        focusPreferredMod(category);
        showInteractionHint(
            $"{categoryLabel(category).ToUpperInvariant()} · {visibleItems.Count} MODS");
    }

    internal void ToggleMod(ManiaModId mod)
    {
        detailMod = mod;
        if (!isSelectable(mod))
        {
            selectDetail(mod);
            return;
        }

        bool enabled = !selectedMods.Contains(mod);
        modPreferences?.Remember(selectedMods);
        selectedMods = mod == ManiaModId.Random && enabled
            ? selectedMods.WithRandomSeed(Random.Shared.Next())
            : selectedMods.With(mod, enabled);
        if (enabled)
            selectedMods = modPreferences?.Apply(
                selectedMods,
                mod) ?? selectedMods;

        if (enabled && isConfigurable(mod))
            settingsHost.Show(mod);

        updateSelection();
        selectDetail(mod);
    }

    internal void ResetMods()
    {
        selectedMods = ManiaModSet.Empty;
        updateSelection();
        selectDetail(detailMod);
    }

    internal void SetAccuracyChallengeMinimum(double value)
    {
        selectedMods = selectedMods.WithAccuracyChallenge(
            value,
            selectedMods.AccuracyChallengeMode);
        updateSelection();
    }

    internal void SetAccuracyChallengeMode(ManiaAccuracyMode mode)
    {
        selectedMods = selectedMods.WithAccuracyChallenge(
            selectedMods.AccuracyChallengeMinimum,
            mode);
        updateSelection();
    }

    internal void SetPerfectRequirePerfectHits(bool value)
    {
        selectedMods = selectedMods.WithPerfect(value);
        updateSelection();
    }

    internal void SetFixedRateSpeedChange(double value)
    {
        ManiaModId mod;
        if (isFixedRateMod(detailMod))
        {
            mod = detailMod;
        }
        else if (selectedMods.FixedRateMod is ManiaModId activeMod)
        {
            mod = activeMod;
        }
        else
        {
            return;
        }

        selectedMods = selectedMods.WithFixedRate(
            mod,
            value,
            selectedMods.FixedRateMod == mod
            && selectedMods.FixedRateAdjustPitch);
        updateSelection();
        selectDetail(detailMod);
    }

    internal void SetFixedRateAdjustPitch(bool value)
    {
        ManiaModId mod;
        if (isFixedRateMod(detailMod))
        {
            mod = detailMod;
        }
        else if (selectedMods.FixedRateMod is ManiaModId activeMod)
        {
            mod = activeMod;
        }
        else
        {
            return;
        }

        selectedMods = selectedMods.WithFixedRate(
            mod,
            selectedMods.FixedRateMod == mod
                ? selectedMods.FixedRateSpeedChange
                : fixedRateFor(mod),
            value);
        updateSelection();
        selectDetail(detailMod);
    }

    internal void SetDifficultyAdjustDrainRate(double? value)
    {
        selectedMods = selectedMods.WithDifficultyAdjust(
            value,
            selectedMods.DifficultyAdjustOverallDifficulty,
            selectedMods.DifficultyAdjustExtendedLimits);
        updateSelection();
    }

    internal void SetDifficultyAdjustOverallDifficulty(double? value)
    {
        selectedMods = selectedMods.WithDifficultyAdjust(
            selectedMods.DifficultyAdjustDrainRate,
            value,
            selectedMods.DifficultyAdjustExtendedLimits);
        updateSelection();
    }

    internal void UseMapDifficultyValues()
    {
        selectedMods = selectedMods.WithDifficultyAdjust(
            null,
            null,
            selectedMods.DifficultyAdjustExtendedLimits);
        updateSelection();
    }

    internal void SetDifficultyAdjustExtendedLimits(bool value)
    {
        selectedMods = selectedMods.WithDifficultyAdjust(
            selectedMods.DifficultyAdjustDrainRate,
            selectedMods.DifficultyAdjustOverallDifficulty,
            value);
        updateSelection();
    }

    internal void SetMutedInverse(bool value)
    {
        selectedMods = selectedMods.WithMuted(
            value,
            selectedMods.MutedMetronome,
            selectedMods.MutedComboCount,
            selectedMods.MutedAffectsHitSounds);
        updateSelection();
    }

    internal void SetMutedMetronome(bool value)
    {
        selectedMods = selectedMods.WithMuted(
            selectedMods.MutedInverse,
            value,
            selectedMods.MutedComboCount,
            selectedMods.MutedAffectsHitSounds);
        updateSelection();
    }

    internal void SetMutedComboCount(int value)
    {
        selectedMods = selectedMods.WithMuted(
            selectedMods.MutedInverse,
            selectedMods.MutedMetronome,
            value,
            selectedMods.MutedAffectsHitSounds);
        updateSelection();
    }

    internal void SetMutedAffectsHitSounds(bool value)
    {
        selectedMods = selectedMods.WithMuted(
            selectedMods.MutedInverse,
            selectedMods.MutedMetronome,
            selectedMods.MutedComboCount,
            value);
        updateSelection();
    }

    internal void SetCoverCoverage(double value)
    {
        selectedMods = selectedMods.WithCover(
            value,
            selectedMods.CoverDirection);
        updateSelection();
    }

    internal void SetCoverDirection(ManiaCoverDirection value)
    {
        selectedMods = selectedMods.WithCover(
            selectedMods.CoverCoverage,
            value);
        updateSelection();
    }

    internal void SetFlashlightSizeMultiplier(double value)
    {
        selectedMods = selectedMods.WithFlashlight(
            value,
            selectedMods.FlashlightComboBasedSize);
        updateSelection();
    }

    internal void SetFlashlightComboBasedSize(bool value)
    {
        selectedMods = selectedMods.WithFlashlight(
            selectedMods.FlashlightSizeMultiplier,
            value);
        updateSelection();
    }

    internal void SetRandomSeed(int value)
    {
        if (!selectedMods.Contains(ManiaModId.Random))
            return;

        selectedMods = selectedMods.WithRandomSeed(value);
        updateSelection();
    }

    internal void SetTimeRampInitialRate(double value)
    {
        if (!selectedMods.HasTimeRamp)
            return;

        ManiaModId mod = selectedMods.Contains(ManiaModId.WindDown)
            ? ManiaModId.WindDown
            : ManiaModId.WindUp;
        selectedMods = selectedMods.WithTimeRamp(
            mod,
            value,
            selectedMods.TimeRampFinalRate,
            selectedMods.TimeRampAdjustPitch);
        updateSelection();
    }

    internal void SetTimeRampFinalRate(double value)
    {
        if (!selectedMods.HasTimeRamp)
            return;

        ManiaModId mod = selectedMods.Contains(ManiaModId.WindDown)
            ? ManiaModId.WindDown
            : ManiaModId.WindUp;
        selectedMods = selectedMods.WithTimeRamp(
            mod,
            selectedMods.TimeRampInitialRate,
            value,
            selectedMods.TimeRampAdjustPitch);
        updateSelection();
    }

    internal void SetTimeRampAdjustPitch(bool value)
    {
        if (!selectedMods.HasTimeRamp)
            return;

        ManiaModId mod = selectedMods.Contains(ManiaModId.WindDown)
            ? ManiaModId.WindDown
            : ManiaModId.WindUp;
        selectedMods = selectedMods.WithTimeRamp(
            mod,
            selectedMods.TimeRampInitialRate,
            selectedMods.TimeRampFinalRate,
            value);
        updateSelection();
    }

    internal void SetAdaptiveInitialRate(double value)
    {
        if (!selectedMods.HasAdaptiveSpeed)
            return;

        selectedMods = selectedMods.WithAdaptiveSpeed(
            value,
            selectedMods.AdaptiveAdjustPitch);
        updateSelection();
    }

    internal void SetAdaptiveAdjustPitch(bool value)
    {
        if (!selectedMods.HasAdaptiveSpeed)
            return;

        selectedMods = selectedMods.WithAdaptiveSpeed(
            selectedMods.AdaptiveInitialRate,
            value);
        updateSelection();
    }

    private Drawable createHeader(Texture logo) => new Container
    {
        RelativeSizeAxes = Axes.X,
        Height = 112,
        Children = new Drawable[]
        {
            new Sprite
            {
                Position = new Vector2(82, 20),
                Size = new Vector2(260, 88),
                Texture = logo,
            },
            new Box
            {
                Position = new Vector2(361, 22),
                Size = new Vector2(1.5f, 56),
                Colour = new Color4(
                    HomeControlColours.Navy.R,
                    HomeControlColours.Navy.G,
                    HomeControlColours.Navy.B,
                    0.46f),
            },
            new SpriteText
            {
                Position = new Vector2(392, 19),
                Text = "GAMEPLAY MODS",
                Font = HomeTypography.Hero(36),
                Scale = new Vector2(1.04f, 1),
                Colour = HomeControlColours.Navy,
            },
            new SpriteText
            {
                Position = new Vector2(394, 67),
                Text = "Customize your play experience.",
                Font = HomeTypography.Body(14),
                Colour = new Color4(
                    HomeControlColours.Navy.R,
                    HomeControlColours.Navy.G,
                    HomeControlColours.Navy.B,
                    0.66f),
            },
        },
    };

    private Drawable createCategoryRail()
    {
        var flow = new FillFlowContainer
        {
            Position = new Vector2(68, 126),
            Width = 207,
            AutoSizeAxes = Axes.Y,
            Direction = FillDirection.Vertical,
            Spacing = new Vector2(0, 8),
        };

        foreach (ManiaModCategory category in visible_categories)
        {
            var button = new GameplayModsCategoryButton(
                categoryLabel(category),
                categoryIcon(category),
                categoryAccent(category),
                () => SetCategory(category));
            button.SetSelected(category == activeCategory);
            categoryButtons[category] = button;
            flow.Add(button);
        }

        return flow;
    }

    private Container createModBrowser() => new Container
    {
        Position = new Vector2(315, 119),
        Size = new Vector2(550, 472),
        Child = modList = new Container
        {
            RelativeSizeAxes = Axes.Both,
        },
    };

    private Container createDetailPanel()
    {
        settingsHost = new SongSelectModSettingsHost(
            SetAccuracyChallengeMinimum,
            SetAccuracyChallengeMode,
            SetPerfectRequirePerfectHits,
            SetDifficultyAdjustDrainRate,
            SetDifficultyAdjustOverallDifficulty,
            UseMapDifficultyValues,
            SetDifficultyAdjustExtendedLimits,
            SetMutedInverse,
            SetMutedMetronome,
            SetMutedComboCount,
            SetMutedAffectsHitSounds,
            SetCoverCoverage,
            SetCoverDirection,
            SetFlashlightSizeMultiplier,
            SetFlashlightComboBasedSize,
            SetFixedRateSpeedChange,
            SetFixedRateAdjustPitch,
            SetTimeRampInitialRate,
            SetTimeRampFinalRate,
            SetTimeRampAdjustPitch,
            SetAdaptiveInitialRate,
            SetAdaptiveAdjustPitch,
            SetRandomSeed,
            ToggleMod)
        {
            Position = new Vector2(20, 12),
            Scale = new Vector2(1.18f),
        };

        return new Container
        {
            Anchor = Anchor.TopRight,
            Origin = Anchor.TopRight,
            Position = new Vector2(-detail_panel_right_margin, 118),
            Size = new Vector2(324, 474),
            Children = new Drawable[]
            {
                new SpriteText
                {
                    Text = "SELECTED MOD",
                    Font = HomeTypography.Display(10),
                    Spacing = new Vector2(1.8f, 0),
                    Colour = HomeControlColours.Cyan,
                },
                new HomeMicroLine
                {
                    Position = new Vector2(111, 10),
                    Width = 165,
                },
                detailBadge = new Container
                {
                    Position = new Vector2(5, 40),
                    Size = new Vector2(78),
                    Masking = true,
                    CornerRadius = 8,
                    BorderThickness = 1.5f,
                    BorderColour = HomeControlColours.Cyan,
                    Children = new Drawable[]
                    {
                        detailBadgeBackground = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = Color4.White,
                        },
                        detailAcronym = new SpriteText
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Font = HomeTypography.Display(31),
                            Colour = HomeControlColours.Navy,
                        },
                    },
                },
                detailName = new SpriteText
                {
                    Position = new Vector2(100, 50),
                    Font = HomeTypography.Display(17),
                    Colour = HomeControlColours.Navy,
                },
                detailDescription = new TextFlowContainer(text =>
                {
                    text.Font = HomeTypography.Body(11);
                    text.Colour = new Color4(
                        HomeControlColours.Navy.R,
                        HomeControlColours.Navy.G,
                        HomeControlColours.Navy.B,
                        0.7f);
                })
                {
                    Position = new Vector2(100, 80),
                    Width = 208,
                    AutoSizeAxes = Axes.Y,
                },
                detailHint = new SpriteText
                {
                    Position = new Vector2(7, 130),
                    Text = "SPACE TO TOGGLE",
                    Font = HomeTypography.Display(9),
                    Spacing = new Vector2(1.1f, 0),
                    Colour = HomeControlColours.Cyan,
                },
                new HomeDotField
                {
                    Position = new Vector2(303, 62),
                    Size = new Vector2(9, 58),
                    Colour = HomeControlColours.Cyan,
                },
                settingsHeader = new SpriteText
                {
                    Position = new Vector2(7, 127),
                    Text = "SETTINGS",
                    Font = HomeTypography.Display(10),
                    Spacing = new Vector2(1.8f, 0),
                    Colour = HomeControlColours.Cyan,
                },
                settingsDivider = new Box
                {
                    Position = new Vector2(84, 136),
                    Size = new Vector2(206, 1),
                    Colour = HomeControlColours.Cyan,
                },
                fixedRatePanel = createFixedRatePanel(),
                configurablePanel = new Container
                {
                    Position = new Vector2(4, 149),
                    Size = new Vector2(280, 318),
                    Masking = true,
                    CornerRadius = 8,
                    Alpha = 0,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = SongSelectTheme.DeepNavy,
                        },
                        settingsHost,
                    },
                },
                activeModsHeader = new SpriteText
                {
                    Position = new Vector2(7, 299),
                    Font = HomeTypography.Display(10),
                    Spacing = new Vector2(1.8f, 0),
                    Colour = HomeControlColours.Cyan,
                },
                activeModsDivider = new Box
                {
                    Position = new Vector2(132, 308),
                    Size = new Vector2(158, 1),
                    Colour = HomeControlColours.Cyan,
                },
                activeMods = new FillFlowContainer
                {
                    Position = new Vector2(7, 326),
                    Width = 283,
                    Height = 130,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 11),
                },
            },
        };
    }

    private Container createFixedRatePanel()
    {
        var panel = new Container
        {
            Position = new Vector2(7, 153),
            Size = new Vector2(284, 132),
        };
        panel.Children = new Drawable[]
        {
            new SpriteIcon
            {
                Position = new Vector2(0, 8),
                Size = new Vector2(16),
                Icon = FontAwesome.Solid.Clock,
                Colour = HomeControlColours.Cyan,
            },
            fixedRateLabel = new SpriteText
            {
                Position = new Vector2(29, 6),
                Text = "SPEED MULTIPLIER",
                Font = HomeTypography.Display(10),
                Colour = HomeControlColours.Navy,
            },
            fixedRateValue = new SpriteText
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                Position = new Vector2(0, 6),
                Font = HomeTypography.Display(12),
                Colour = HomeControlColours.Cyan,
            },
            fixedRateSlider = new GameplayModsRateSlider(
                PreviewFixedRateSpeedChange,
                CompleteFixedRateInteraction)
            {
                Position = new Vector2(0, 35),
            },
            fixedRateMinimum = new SpriteText
            {
                Position = new Vector2(0, 54),
                Text = "0.25x",
                Font = HomeTypography.Body(9),
                Colour = new Color4(
                    HomeControlColours.Navy.R,
                    HomeControlColours.Navy.G,
                    HomeControlColours.Navy.B,
                    0.58f),
            },
            fixedRateMidpoint = new SpriteText
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Position = new Vector2(0, 54),
                Text = "1.00x",
                Font = HomeTypography.Body(9),
                Colour = new Color4(
                    HomeControlColours.Navy.R,
                    HomeControlColours.Navy.G,
                    HomeControlColours.Navy.B,
                    0.58f),
            },
            fixedRateMaximum = new SpriteText
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                Position = new Vector2(0, 54),
                Text = "2.00x",
                Font = HomeTypography.Body(9),
                Colour = new Color4(
                    HomeControlColours.Navy.R,
                    HomeControlColours.Navy.G,
                    HomeControlColours.Navy.B,
                    0.58f),
            },
            fixedRatePitch = new GameplayModsPitchButton(
                () => SetFixedRateAdjustPitch(
                    !selectedMods.FixedRateAdjustPitch))
            {
                Y = 82,
            },
        };
        return panel;
    }

    private Container createDecorations() => new Container
    {
        RelativeSizeAxes = Axes.Both,
        Depth = 5,
        Children = new Drawable[]
        {
            new HomeCornerBracket
            {
                Position = new Vector2(19, 16),
                Height = 288,
                Colour = HomeControlColours.Navy,
            },
            new SpriteIcon
            {
                Position = new Vector2(31, 106),
                Size = new Vector2(11),
                Icon = FontAwesome.Solid.Plus,
                Colour = HomeControlColours.Yellow,
            },
            new SpriteIcon
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                Position = new Vector2(-137, 19),
                Size = new Vector2(10),
                Icon = FontAwesome.Solid.Plus,
                Colour = HomeControlColours.Cyan,
            },
            new SpriteIcon
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                Position = new Vector2(-87, 50),
                Size = new Vector2(10),
                Icon = FontAwesome.Solid.Plus,
                Colour = HomeControlColours.Pink,
            },
            new SpriteIcon
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                Position = new Vector2(-37, 82),
                Size = new Vector2(10),
                Icon = FontAwesome.Solid.Plus,
                Colour = HomeControlColours.Pink,
            },
            new SpriteIcon
            {
                Anchor = Anchor.BottomLeft,
                Origin = Anchor.BottomLeft,
                Position = new Vector2(15, -135),
                Size = new Vector2(10),
                Icon = FontAwesome.Regular.Heart,
                Colour = HomeControlColours.Pink,
            },
            detailPanelDivider = new Box
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                Position = new Vector2(
                    -(detail_panel_right_margin
                      + detail_panel_width + 32),
                    110),
                Size = new Vector2(2, 482),
                Colour = new Color4(
                    HomeControlColours.Navy.R,
                    HomeControlColours.Navy.G,
                    HomeControlColours.Navy.B,
                    0.11f),
            },
        },
    };

    private Drawable createFooter() => new Container
    {
        Anchor = Anchor.BottomLeft,
        Origin = Anchor.BottomLeft,
        RelativeSizeAxes = Axes.X,
        Height = footer_height,
        Children = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = HomeControlColours.Cyan,
            },
            new Box
            {
                RelativeSizeAxes = Axes.X,
                Height = 2,
                Colour = new Color4(1f, 1f, 1f, 0.75f),
            },
            new HomeDotField
            {
                Position = new Vector2(8, 42),
                Size = new Vector2(105, 55),
                Colour = new Color4(1f, 1f, 1f, 0.32f),
            },
            new SongSelectFooterBackButton(this.Exit)
            {
                Position = new Vector2(106, 23),
                Scale = new Vector2(0.84f),
            },
            resetButton = new GameplayModsResetButton(ResetMods)
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Position = new Vector2(0, 26),
            },
            interactionHint = new SpriteText
            {
                Anchor = Anchor.BottomCentre,
                Origin = Anchor.BottomCentre,
                Y = -6,
                Font = HomeTypography.Display(8),
                Spacing = new Vector2(1.2f, 0),
                Colour = new Color4(
                    HomeControlColours.Navy.R,
                    HomeControlColours.Navy.G,
                    HomeControlColours.Navy.B,
                    0.72f),
                Alpha = 0,
            },
            new SpriteIcon
            {
                Position = new Vector2(412, 67),
                Size = new Vector2(8),
                Icon = FontAwesome.Solid.Plus,
                Colour = Color4.White,
            },
            new HomeDotField
            {
                Position = new Vector2(751, 60),
                Size = new Vector2(48, 28),
                Colour = new Color4(1f, 1f, 1f, 0.3f),
            },
            new GameplayModsDoneButton(this.Exit)
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                Position = new Vector2(-19, 16),
            },
        },
    };

    private void rebuildModList()
    {
        if (modList == null)
            return;

        visibleItems.Clear();
        sectionDividers.Clear();
        modList.Clear();

        if (activeCategory is ManiaModCategory.DifficultyReduction
            or ManiaModCategory.DifficultyIncrease)
        {
            addSection(ManiaModCategory.DifficultyReduction, 0);
            addSection(
                ManiaModCategory.DifficultyIncrease,
                sectionHeight(
                    ManiaModCategory.DifficultyReduction)
                + 18);
        }
        else
        {
            addSection(activeCategory, 0);
        }

        updateFocusVisual();
    }

    private void addSection(ManiaModCategory category, float y)
    {
        IReadOnlyList<ManiaModDefinition> definitions = definitionsFor(category);
        Color4 accent = categoryAccent(category);

        modList.Add(new SpriteText
        {
            Position = new Vector2(0, y),
            Text = categoryLabel(category).ToUpperInvariant(),
            Font = HomeTypography.Display(10),
            Spacing = new Vector2(1.7f, 0),
            Colour = accent,
        });
        var divider = new Box
        {
            Position = new Vector2(128, y + 9),
            Size = new Vector2(
                MathF.Max(modBrowser.Width - 145, 120),
                1),
            Colour = accent,
        };
        sectionDividers.Add(divider);
        modList.Add(divider);

        float rowSpacing = category switch
        {
            ManiaModCategory.DifficultyReduction => 54,
            ManiaModCategory.DifficultyIncrease => 50,
            _ => 44,
        };
        for (int index = 0; index < definitions.Count; index++)
        {
            ManiaModDefinition definition = definitions[index];
            int rowCount =
                (definitions.Count + modColumnCount - 1)
                / modColumnCount;
            int column = index / rowCount;
            int row = index % rowCount;
            var item = new GameplayModListItem(
                definition,
                accentForMod(definition.Id, category),
                isSelectable(definition.Id),
                () => ToggleMod(definition.Id),
                null)
            {
                Position = new Vector2(
                    column * 284,
                    y + 27 + row * rowSpacing),
            };
            item.SetSelected(selectedMods.Contains(definition.Id));
            visibleItems[definition.Id] = item;
            modList.Add(item);
        }
    }

    private float sectionHeight(ManiaModCategory category)
    {
        int definitionCount = definitionsFor(category).Count;
        int rowCount =
            (definitionCount + modColumnCount - 1)
            / modColumnCount;
        float rowSpacing = category switch
        {
            ManiaModCategory.DifficultyReduction => 54,
            ManiaModCategory.DifficultyIncrease => 50,
            _ => 44,
        };

        return 27 + MathF.Max(rowCount - 1, 0) * rowSpacing + 42;
    }

    private void updateSelection()
    {
        foreach ((ManiaModId mod, GameplayModListItem item) in visibleItems)
            item.SetSelected(selectedMods.Contains(mod));

        settingsHost?.SetState(selectedMods, beatmap);
        rebuildActiveMods();
        activeModsHeader.Text = selectedMods.Mods.Count == 0
            ? "ACTIVE MODS"
            : $"ACTIVE MODS ({selectedMods.Mods.Count})";
        resetButton?.SetEnabled(selectedMods.Mods.Count > 0);
        updateFocusVisual();
        if (loadComplete)
            selectionDirty = true;
    }

    internal void CommitSelection()
    {
        if (!selectionDirty)
            return;

        modPreferences?.Remember(selectedMods);
        modsChanged?.Invoke(selectedMods);
        selectionDirty = false;
    }

    private void rebuildActiveMods()
    {
        if (activeMods == null)
            return;

        activeMods.Clear();
        foreach (ManiaModId mod in selectedMods.Mods.Take(3))
        {
            ManiaModDefinition definition = OsuManiaModParityCatalog.Get(mod);
            string value = isRateMod(mod)
                ? $"{selectedMods.PlaybackRate:0.##}x"
                : string.Empty;
            activeMods.Add(new GameplayActiveModRow(
                definition,
                value,
                () => ToggleMod(mod)));
        }

        if (selectedMods.Mods.Count == 0)
        {
            activeMods.Add(new SpriteText
            {
                Text = "NO MODS ACTIVE",
                Font = HomeTypography.Display(10),
                Colour = new Color4(
                    HomeControlColours.Navy.R,
                    HomeControlColours.Navy.G,
                    HomeControlColours.Navy.B,
                    0.45f),
            });
        }
        else if (selectedMods.Mods.Count > 3)
        {
            activeMods.Add(new SpriteText
            {
                Text = $"+{selectedMods.Mods.Count - 3} MORE ACTIVE",
                Font = HomeTypography.Display(9),
                Colour = HomeControlColours.Cyan,
            });
        }
    }

    private void selectDetail(ManiaModId mod)
    {
        ManiaModDefinition definition = OsuManiaModParityCatalog.Get(mod);
        detailAcronym.Text = definition.Acronym;
        detailName.Text = definition.Name;
        detailDescription.Clear();
        detailDescription.AddText(
            isSelectable(mod)
                ? definition.Description
                : "Available only for charts imported from osu!standard.");

        bool active = selectedMods.Contains(mod);
        Color4 detailAccent = accentForMod(
            definition.Id,
            definition.Category);
        detailBadgeBackground.Colour = active
            ? HomeControlColours.PaleCyan
            : Color4.White;
        detailBadge.BorderColour = active
            ? detailAccent
            : HomeControlColours.Cyan;
        detailBadge.BorderThickness = active ? 2.2f : 1.5f;
        detailName.Colour = active
            ? detailAccent
            : HomeControlColours.Navy;

        bool fixedRateMod = isFixedRateMod(mod);
        bool configurable = isConfigurable(mod) && !fixedRateMod;
        settingsHeader.Text = configurable
            ? active
                ? "SETTINGS · ACTIVE"
                : "SETTINGS · PREVIEW"
            : "SETTINGS";
        settingsHeader.Y = configurable ? 127 : 170;
        settingsDivider.Y = configurable ? 136 : 179;
        settingsDivider.X = configurable ? 132 : 84;
        settingsDivider.Width = configurable ? 158 : 206;
        fixedRatePanel.Y = 200;
        configurablePanel.Alpha = configurable ? 1 : 0;
        fixedRatePanel.Alpha = configurable ? 0 : 1;
        activeModsHeader.Alpha = configurable ? 0 : 1;
        activeModsDivider.Alpha = configurable ? 0 : 1;
        activeMods.Alpha = configurable ? 0 : 1;
        detailHint.Alpha = configurable ? 0 : 1;
        detailHint.Text = mod == ManiaModId.HalfTime
            ? selectedMods.Contains(mod)
                ? "SHORTCUT: H · SPACE REMOVE"
                : "SHORTCUT: H · SPACE TOGGLE"
            : selectedMods.Contains(mod)
                ? "SPACE TO REMOVE"
                : "SPACE TO TOGGLE";
        if (configurable)
        {
            settingsHost.Show(mod);
        }
        else
        {
            bool enabledRateMod =
                fixedRateMod && selectedMods.FixedRateMod == mod;
            double rate = enabledRateMod
                ? selectedMods.FixedRateSpeedChange
                : fixedRateFor(mod);
            fixedRateLabel.Text = fixedRateMod
                ? "SPEED MULTIPLIER"
                : "NO EXTRA SETTINGS";
            fixedRateValue.Text = fixedRateMod
                ? $"{rate:0.##}x"
                : "—";
            double minimum = isSlowFixedRateMod(mod) ? 0.5 : 1.01;
            double maximum = isSlowFixedRateMod(mod) ? 0.99 : 2;
            double midpoint = Math.Round((minimum + maximum) / 2, 2);
            fixedRateMinimum.Text = $"{minimum:0.00}x";
            fixedRateMidpoint.Text = $"{midpoint:0.00}x";
            fixedRateMaximum.Text = $"{maximum:0.00}x";
            fixedRateSlider.SetState(
                fixedRateMod,
                minimum,
                maximum,
                rate);
            fixedRateSlider.Alpha = fixedRateMod ? 1 : 0;
            fixedRatePitch.SetState(
                fixedRateMod && enabledRateMod,
                isPitchAdjustableFixedRate(mod),
                enabledRateMod && selectedMods.FixedRateAdjustPitch);
            fixedRatePitch.Alpha = fixedRateMod ? 1 : 0;
        }
    }

    private void focusPreferredMod(ManiaModCategory category)
    {
        ManiaModId? preferred = visibleItems.Keys
            .Where(mod =>
                OsuManiaModParityCatalog.Get(mod).Category == category
                && selectedMods.Contains(mod))
            .Select(mod => (ManiaModId?)mod)
            .FirstOrDefault();
        preferred ??= visibleItems.Keys
            .Where(mod =>
                OsuManiaModParityCatalog.Get(mod).Category == category
                && isSelectable(mod))
            .Select(mod => (ManiaModId?)mod)
            .FirstOrDefault();
        preferred ??= visibleItems.Keys
            .Select(mod => (ManiaModId?)mod)
            .FirstOrDefault();

        if (!preferred.HasValue)
            return;

        detailMod = preferred.Value;
        updateFocusVisual();
        selectDetail(detailMod);
    }

    internal void CycleCategory(int offset)
    {
        int currentIndex = Array.IndexOf(
            visible_categories,
            activeCategory);
        int nextIndex = (currentIndex + offset
                         + visible_categories.Length)
                        % visible_categories.Length;
        SetCategory(visible_categories[nextIndex]);
    }

    internal void MoveDetailFocus(Vector2 direction)
    {
        if (visibleItems.Count == 0)
            return;

        if (!visibleItems.TryGetValue(detailMod, out GameplayModListItem current))
        {
            focusPreferredMod(activeCategory);
            return;
        }

        Vector2 currentPosition = current.Position;
        bool horizontal = direction.X != 0;
        var candidates = visibleItems
            .Where(pair => pair.Key != detailMod)
            .Select(pair => new
            {
                pair.Key,
                Position = pair.Value.Position,
            })
            .Where(candidate =>
                horizontal
                    ? direction.X < 0
                        ? candidate.Position.X < currentPosition.X - 1
                        : candidate.Position.X > currentPosition.X + 1
                    : direction.Y < 0
                        ? candidate.Position.Y < currentPosition.Y - 1
                        : candidate.Position.Y > currentPosition.Y + 1)
            .OrderBy(candidate =>
                horizontal
                    ? MathF.Abs(candidate.Position.X - currentPosition.X)
                      + MathF.Abs(candidate.Position.Y - currentPosition.Y) * 4
                    : MathF.Abs(candidate.Position.Y - currentPosition.Y)
                      + MathF.Abs(candidate.Position.X - currentPosition.X) * 4)
            .ToArray();

        ManiaModId next;
        if (candidates.Length > 0)
        {
            next = candidates[0].Key;
        }
        else
        {
            next = visibleItems
                .Where(pair => pair.Key != detailMod)
                .OrderBy(pair =>
                    horizontal
                        ? MathF.Abs(pair.Value.Position.Y - currentPosition.Y)
                          + (direction.X < 0
                              ? -pair.Value.Position.X
                              : pair.Value.Position.X)
                        : MathF.Abs(pair.Value.Position.X - currentPosition.X)
                          + (direction.Y < 0
                              ? -pair.Value.Position.Y
                              : pair.Value.Position.Y))
                .Select(pair => pair.Key)
                .FirstOrDefault(detailMod);
        }

        detailMod = next;
        updateFocusVisual();
        selectDetail(detailMod);
        ManiaModDefinition definition =
            OsuManiaModParityCatalog.Get(detailMod);
        showInteractionHint(
            $"{definition.Acronym} · {definition.Name.ToUpperInvariant()}   SPACE · TOGGLE");
    }

    private void updateFocusVisual()
    {
        foreach ((ManiaModId mod, GameplayModListItem item) in visibleItems)
            item.SetFocused(mod == detailMod);
    }

    internal void PreviewFixedRateSpeedChange(double value)
    {
        if (!isFixedRateMod(detailMod))
            return;

        ManiaModId mod = detailMod;
        bool alreadyActive = selectedMods.FixedRateMod == mod;
        bool adjustPitch = alreadyActive
                           && selectedMods.FixedRateAdjustPitch;
        double nextValue = Math.Round(value, 2);
        if (alreadyActive
            && Math.Abs(
                selectedMods.FixedRateSpeedChange - nextValue)
            < 0.0001)
        {
            return;
        }

        selectedMods = selectedMods.WithFixedRate(
            mod,
            nextValue,
            adjustPitch);
        if (loadComplete)
            selectionDirty = true;

        fixedRateValue.Text = $"{nextValue:0.##}x";
        if (!alreadyActive)
        {
            foreach ((ManiaModId visibleMod,
                         GameplayModListItem item)
                     in visibleItems)
            {
                item.SetSelected(
                    selectedMods.Contains(visibleMod));
            }

            resetButton?.SetEnabled(true);
            rebuildActiveMods();
            selectDetail(detailMod);
        }
    }

    internal void CompleteFixedRateInteraction()
    {
        updateSelection();
        selectDetail(detailMod);
    }

    private bool adjustFocusedFixedRate(double delta)
    {
        if (!isFixedRateMod(detailMod))
            return false;

        double minimum = isSlowFixedRateMod(detailMod) ? 0.5 : 1.01;
        double maximum = isSlowFixedRateMod(detailMod) ? 0.99 : 2;
        double currentRate = selectedMods.FixedRateMod == detailMod
            ? selectedMods.FixedRateSpeedChange
            : fixedRateFor(detailMod);
        double rate = Math.Clamp(
            Math.Round(currentRate + delta, 2),
            minimum,
            maximum);
        SetFixedRateSpeedChange(rate);
        showInteractionHint(
            $"RATE · {rate:0.00}x   +/- · ADJUST   P · PITCH");
        return true;
    }

    private void showInteractionHint(string text)
    {
        if (interactionHint == null)
            return;

        interactionHint.ClearTransforms();
        interactionHint.Text = text;
        interactionHint
            .FadeIn(90, Easing.OutQuint)
            .Delay(1700)
            .FadeOut(240, Easing.OutQuint);
    }

    private bool isSelectable(ManiaModId mod) =>
        mod != ManiaModId.ScoreV2
        && (mod is not (>= ManiaModId.Key1 and <= ManiaModId.Key10)
            && mod != ManiaModId.DualStages
            || beatmap.ConversionSource is not null);

    private static bool isConfigurable(ManiaModId mod) =>
        mod is ManiaModId.Perfect
            or ManiaModId.AccuracyChallenge
            or ManiaModId.DifficultyAdjust
            or ManiaModId.Muted
            or ManiaModId.Cover
            or ManiaModId.Flashlight
            or ManiaModId.HalfTime
            or ManiaModId.Daycore
            or ManiaModId.DoubleTime
            or ManiaModId.Nightcore
            or ManiaModId.WindUp
            or ManiaModId.WindDown
            or ManiaModId.AdaptiveSpeed
            or ManiaModId.Random
            or ManiaModId.DualStages
        || mod is >= ManiaModId.Key1 and <= ManiaModId.Key10;

    private static bool isRateMod(ManiaModId mod) =>
        mod is ManiaModId.HalfTime
            or ManiaModId.Daycore
            or ManiaModId.DoubleTime
            or ManiaModId.Nightcore
            or ManiaModId.WindUp
            or ManiaModId.WindDown
            or ManiaModId.AdaptiveSpeed;

    private static bool isFixedRateMod(ManiaModId mod) =>
        mod is ManiaModId.HalfTime
            or ManiaModId.Daycore
            or ManiaModId.DoubleTime
            or ManiaModId.Nightcore;

    private static bool isSlowFixedRateMod(ManiaModId mod) =>
        mod is ManiaModId.HalfTime or ManiaModId.Daycore;

    private static bool isPitchAdjustableFixedRate(ManiaModId mod) =>
        mod is ManiaModId.HalfTime or ManiaModId.DoubleTime;

    private static double fixedRateFor(ManiaModId mod) =>
        mod switch
        {
            ManiaModId.HalfTime or ManiaModId.Daycore => 0.75,
            ManiaModId.DoubleTime or ManiaModId.Nightcore => 1.5,
            _ => 1,
        };

    private static IReadOnlyList<ManiaModDefinition> definitionsFor(
        ManiaModCategory category)
    {
        ManiaModDefinition[] definitions = OsuManiaModParityCatalog.All
            .Where(definition =>
                definition.Category == category
                && definition.Id != ManiaModId.ScoreV2)
            .ToArray();

        ManiaModId[] order = category switch
        {
            ManiaModCategory.DifficultyReduction =>
            [
                ManiaModId.Easy,
                ManiaModId.HalfTime,
                ManiaModId.NoRelease,
                ManiaModId.NoFail,
                ManiaModId.Daycore,
            ],
            ManiaModCategory.DifficultyIncrease =>
            [
                ManiaModId.HardRock,
                ManiaModId.Perfect,
                ManiaModId.Nightcore,
                ManiaModId.Hidden,
                ManiaModId.Flashlight,
                ManiaModId.SuddenDeath,
                ManiaModId.DoubleTime,
                ManiaModId.FadeIn,
                ManiaModId.Cover,
                ManiaModId.AccuracyChallenge,
            ],
            _ => [],
        };

        if (order.Length == 0)
            return definitions;

        var byId = definitions.ToDictionary(definition => definition.Id);
        return order
            .Where(byId.ContainsKey)
            .Select(id => byId[id])
            .Concat(definitions.Where(definition =>
                !order.Contains(definition.Id)))
            .ToArray();
    }

    private static string categoryLabel(ManiaModCategory category) =>
        category switch
        {
            ManiaModCategory.DifficultyReduction => "Difficulty Down",
            ManiaModCategory.DifficultyIncrease => "Difficulty Up",
            ManiaModCategory.Conversion => "Conversion",
            ManiaModCategory.Automation => "Automation",
            ManiaModCategory.Fun => "Fun",
            _ => category.ToString(),
        };

    private static IconUsage categoryIcon(ManiaModCategory category) =>
        category switch
        {
            ManiaModCategory.DifficultyReduction => FontAwesome.Solid.ChevronDown,
            ManiaModCategory.DifficultyIncrease => FontAwesome.Solid.ChevronUp,
            ManiaModCategory.Conversion => FontAwesome.Solid.LayerGroup,
            ManiaModCategory.Automation => FontAwesome.Solid.Cog,
            ManiaModCategory.Fun => FontAwesome.Solid.Star,
            _ => FontAwesome.Solid.SlidersH,
        };

    private static Color4 categoryAccent(ManiaModCategory category) =>
        category switch
        {
            ManiaModCategory.DifficultyReduction => HomeControlColours.Cyan,
            ManiaModCategory.DifficultyIncrease => HomeControlColours.Pink,
            ManiaModCategory.Conversion => HomeControlColours.Cyan,
            ManiaModCategory.Automation => HomeControlColours.Yellow,
            ManiaModCategory.Fun => HomeControlColours.Pink,
            _ => HomeControlColours.Cyan,
        };

    private static Color4 accentForMod(
        ManiaModId mod,
        ManiaModCategory category)
    {
        if (mod is ManiaModId.SuddenDeath
            or ManiaModId.FadeIn
            or ManiaModId.Flashlight)
        {
            return HomeControlColours.Yellow;
        }

        return categoryAccent(category);
    }
}
