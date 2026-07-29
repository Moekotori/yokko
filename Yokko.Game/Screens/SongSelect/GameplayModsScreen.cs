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

    private Container stage;
    private Container modList;
    private Container fixedRatePanel;
    private Container configurablePanel;
    private FillFlowContainer activeMods;
    private Box activeModsDivider;
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
    private SongSelectModSettingsHost settingsHost;
    private ManiaModCategory activeCategory =
        ManiaModCategory.DifficultyReduction;
    private ManiaModId detailMod = ManiaModId.HalfTime;
    private ManiaModSet selectedMods;
    private bool loadComplete;

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
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Size = new Vector2(designed_width, designed_height),
                Children = new Drawable[]
                {
                    createHeader(logo),
                    createCategoryRail(),
                    createModBrowser(),
                    createDetailPanel(),
                    createDecorations(),
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

    public override void OnEntering(ScreenTransitionEvent e)
    {
        base.OnEntering(e);
        stage.FadeIn(220, Easing.OutQuint)
             .MoveToY(0, 360, Easing.OutQuint);
    }

    public override bool OnExiting(ScreenExitEvent e)
    {
        stage.FadeOut(150, Easing.OutQuint)
             .MoveToY(8, 180, Easing.OutQuint);
        return base.OnExiting(e);
    }

    protected override bool OnKeyDown(KeyDownEvent e)
    {
        switch (e.Key)
        {
            case Key.Escape:
            case Key.M:
            case Key.Enter:
                this.Exit();
                return true;

            case Key.R:
                ResetMods();
                return true;

            case Key.Space:
                ToggleMod(detailMod);
                return true;

            case Key.P:
                if (isPitchAdjustableFixedRate(detailMod)
                    && selectedMods.FixedRateMod == detailMod)
                {
                    SetFixedRateAdjustPitch(
                        !selectedMods.FixedRateAdjustPitch);
                    return true;
                }
                break;

            case Key.H:
                ToggleMod(ManiaModId.HalfTime);
                return true;

            default:
                break;
        }

        return base.OnKeyDown(e);
    }

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
        selectedMods = mod == ManiaModId.Random && enabled
            ? selectedMods.WithRandomSeed(Random.Shared.Next())
            : selectedMods.With(mod, enabled);

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
        if (selectedMods.FixedRateMod is not ManiaModId mod)
            return;

        selectedMods = selectedMods.WithFixedRate(
            mod,
            value,
            selectedMods.FixedRateAdjustPitch);
        updateSelection();
        selectDetail(detailMod);
    }

    internal void SetFixedRateAdjustPitch(bool value)
    {
        if (selectedMods.FixedRateMod is not ManiaModId mod)
            return;

        selectedMods = selectedMods.WithFixedRate(
            mod,
            selectedMods.FixedRateSpeedChange,
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

    private Drawable createModBrowser() => new Container
    {
        Position = new Vector2(315, 119),
        Size = new Vector2(550, 472),
        Child = modList = new Container
        {
            RelativeSizeAxes = Axes.Both,
        },
    };

    private Drawable createDetailPanel()
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
            SetFixedRateSpeedChange,
            SetFixedRateAdjustPitch,
            SetTimeRampInitialRate,
            SetTimeRampFinalRate,
            SetTimeRampAdjustPitch,
            SetAdaptiveInitialRate,
            SetAdaptiveAdjustPitch,
            ToggleMod)
        {
            Position = new Vector2(20, 12),
            Scale = new Vector2(1.18f),
        };

        return new Container
        {
            Position = new Vector2(920, 118),
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
                new Container
                {
                    Position = new Vector2(5, 40),
                    Size = new Vector2(78),
                    Masking = true,
                    CornerRadius = 8,
                    BorderThickness = 1.5f,
                    BorderColour = HomeControlColours.Cyan,
                    Children = new Drawable[]
                    {
                        new Box
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
                SetFixedRateSpeedChange)
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

    private Drawable createDecorations() => new Container
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
                Position = new Vector2(1133, 19),
                Size = new Vector2(10),
                Icon = FontAwesome.Solid.Plus,
                Colour = HomeControlColours.Cyan,
            },
            new SpriteIcon
            {
                Position = new Vector2(1183, 50),
                Size = new Vector2(10),
                Icon = FontAwesome.Solid.Plus,
                Colour = HomeControlColours.Pink,
            },
            new SpriteIcon
            {
                Position = new Vector2(1233, 82),
                Size = new Vector2(10),
                Icon = FontAwesome.Solid.Plus,
                Colour = HomeControlColours.Pink,
            },
            new SpriteIcon
            {
                Position = new Vector2(15, 475),
                Size = new Vector2(10),
                Icon = FontAwesome.Regular.Heart,
                Colour = HomeControlColours.Pink,
            },
            new Box
            {
                Position = new Vector2(888, 110),
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
        Height = 110,
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
            new GameplayModsResetButton(ResetMods)
            {
                Position = new Vector2(542, 26),
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
                Position = new Vector2(907, 16),
            },
        },
    };

    private void rebuildModList()
    {
        if (modList == null)
            return;

        visibleItems.Clear();
        modList.Clear();

        if (activeCategory is ManiaModCategory.DifficultyReduction
            or ManiaModCategory.DifficultyIncrease)
        {
            addSection(ManiaModCategory.DifficultyReduction, 0);
            addSection(ManiaModCategory.DifficultyIncrease, 195);
        }
        else
        {
            addSection(activeCategory, 0);
        }
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
        modList.Add(new Box
        {
            Position = new Vector2(128, y + 9),
            Size = new Vector2(405, 1),
            Colour = accent,
        });

        float rowSpacing = category switch
        {
            ManiaModCategory.DifficultyReduction => 54,
            ManiaModCategory.DifficultyIncrease => 50,
            _ => 44,
        };
        for (int index = 0; index < definitions.Count; index++)
        {
            ManiaModDefinition definition = definitions[index];
            int rowCount = (definitions.Count + 1) / 2;
            int column = index / rowCount;
            int row = index % rowCount;
            var item = new GameplayModListItem(
                definition,
                accentForMod(definition.Id, category),
                isSelectable(definition.Id),
                () => ToggleMod(definition.Id),
                previewDetail)
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

    private void updateSelection()
    {
        foreach ((ManiaModId mod, GameplayModListItem item) in visibleItems)
            item.SetSelected(selectedMods.Contains(mod));

        settingsHost?.SetState(selectedMods, beatmap);
        rebuildActiveMods();
        activeModsHeader.Text = selectedMods.Mods.Count == 0
            ? "ACTIVE MODS"
            : $"ACTIVE MODS ({selectedMods.Mods.Count})";
        if (loadComplete)
            modsChanged?.Invoke(selectedMods);
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

    private void previewDetail(ManiaModId mod, bool hovered)
    {
        if (hovered)
            selectDetail(mod);
        else
            selectDetail(detailMod);
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

        bool fixedRateMod = isFixedRateMod(mod);
        bool configurable = isConfigurable(mod) && !fixedRateMod;
        settingsHeader.Y = fixedRateMod ? 170 : 127;
        settingsDivider.Y = fixedRateMod ? 179 : 136;
        fixedRatePanel.Y = fixedRateMod ? 200 : 153;
        configurablePanel.Alpha = configurable ? 1 : 0;
        fixedRatePanel.Alpha = configurable ? 0 : 1;
        activeModsHeader.Alpha = configurable ? 0 : 1;
        activeModsDivider.Alpha = configurable ? 0 : 1;
        activeMods.Alpha = configurable ? 0 : 1;
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
                fixedRateMod && enabledRateMod,
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
            or ManiaModId.HalfTime
            or ManiaModId.Daycore
            or ManiaModId.DoubleTime
            or ManiaModId.Nightcore
            or ManiaModId.WindUp
            or ManiaModId.WindDown
            or ManiaModId.AdaptiveSpeed
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
