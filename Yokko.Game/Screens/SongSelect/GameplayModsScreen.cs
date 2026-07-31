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
using Yokko.Game.Localisation;
using Yokko.Game.Presentation;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Screens.SongSelect;

/// <summary>
/// Dedicated full-screen workspace for selecting and configuring gameplay
/// modifiers before starting a chart.
/// </summary>
internal partial class GameplayModsScreen : Screen
{
    private const float detail_panel_width = 448;
    private const float detail_panel_right_margin = 48;
    private const float browser_left = 48;
    private const float browser_detail_gap = 24;
    private const float loadout_top = 127;
    private const float browser_controls_top = 224;
    private const float mod_browser_top = 332;
    private const float footer_height = 107;
    private static readonly Vector2 designed_size =
        YokkoDisplaySettings.ReferenceLayoutSize;

    private enum ModsBrowseMode
    {
        All,
        Difficulty,
        Conversion,
        Automation,
        Fun,
    }

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
    private readonly Dictionary<ModsBrowseMode, GameplayModsCategoryChip>
        categoryButtons = new();
    private readonly Dictionary<ManiaModId, GameplayModListItem> visibleItems =
        new();
    private readonly List<Box> sectionDividers = new();

    private Container stage;
    private Drawable categoryRail;
    private Container loadoutBar;
    private Container modBrowser;
    private BasicScrollContainer modScroll;
    private Container modList;
    private Container detailPanel;
    private Container decorations;
    private Container fixedRatePanel;
    private Container configurablePanel;
    private Box configurablePanelBackground;
    private FillFlowContainer activeMods;
    private SpriteText activeModsEmpty;
    private SpriteText activeModsOverflow;
    private SpriteText scoreMultiplierValue;
    private SpriteText compatibilityValue;
    private GameplayModsSearchBox searchBox;
    private Container detailBadge;
    private Box detailBadgeBackground;
    private SpriteText detailAcronym;
    private SpriteText detailName;
    private SpriteText detailHint;
    private TextFlowContainer detailDescription;
    private SpriteText settingsHeader;
    private Box settingsDivider;
    private SpriteText fixedRateLabel;
    private SpriteText fixedRateValue;
    private SpriteText fixedRateMinimum;
    private SpriteText fixedRateMidpoint;
    private SpriteText fixedRateMaximum;
    private GameplayModsRateSlider fixedRateSlider;
    private GameplayModsPitchButton fixedRatePitch;
    private GameplayModsResetButton resetButton;
    private GameplayModsOrbitWorkspace orbitWorkspace;
    private SpriteText navigationHint;
    private SpriteText interactionHint;
    private SongSelectModSettingsHost settingsHost;
    private ManiaModCategory activeCategory =
        ManiaModCategory.DifficultyReduction;
    private ModsBrowseMode browseMode = ModsBrowseMode.All;
    private string searchQuery = string.Empty;
    private ManiaModId detailMod = ManiaModId.HalfTime;
    private ManiaModSet selectedMods;
    private bool loadComplete;
    private bool selectionDirty;
    private bool pageTransitioning;
    private double scrollGestureAccumulator;
    private double lastScrollGestureDirection;
    private double lastScrollNavigationTime = double.NegativeInfinity;
    private int modColumnCount = 2;
    private Vector2 lastResponsiveLayoutSize = new(-1);
    private float modBrowserRestingY = mod_browser_top;
    private float detailPanelRestingY = browser_controls_top;
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
    internal string DetailHintText =>
        detailHint?.Text.ToString() ?? string.Empty;
    internal string InteractionHintText =>
        interactionHint?.Text.ToString() ?? string.Empty;
    internal float SettingsHeaderY => settingsHeader?.Y ?? 0;
    internal float FixedRatePanelY => fixedRatePanel?.Y ?? 0;
    internal float FixedRateSliderHeight =>
        fixedRateSlider?.Height ?? 0;
    internal bool FixedRateSliderVisible =>
        fixedRateSlider?.Alpha > 0;
    internal bool FixedRateTicksVisible =>
        fixedRateMinimum?.Alpha > 0
        || fixedRateMidpoint?.Alpha > 0
        || fixedRateMaximum?.Alpha > 0;
    internal bool NavigationHintVisible =>
        navigationHint?.Alpha > 0;
    internal bool IsModVisible(ManiaModId mod) =>
        visibleItems.ContainsKey(mod);
    internal bool IsPageTransitioning => pageTransitioning;
    internal float OrbitContentX => orbitWorkspace?.OrbitContentX ?? 335;
    internal string SearchQuery => searchQuery;
    internal Color4 ConfigurablePanelColour =>
        configurablePanelBackground?.Colour ?? Color4.Transparent;
    internal bool ResetEnabled => resetButton?.IsEnabled ?? false;
    internal void SetPreviewRateVisual(double value) =>
        orbitWorkspace?.PreviewRate(value);
    internal static Vector2 CalculateResponsiveStageSize(Vector2 viewport) =>
        new(
            MathF.Max(viewport.X, designed_size.X),
            MathF.Max(viewport.Y, designed_size.Y));

    internal static int CalculateBrowserColumnCount(float browserWidth) =>
        browserWidth >= 1700 ? 4
        : browserWidth >= 1180 ? 3
        : 2;

    [BackgroundDependencyLoader]
    private void load(TextureStore textures)
    {
        Texture logo = textures.Get("Mods/home-logo-transparent");
        Texture paperTexture = textures.Get("Mods/ivory-paper");
        Texture waveformTexture = textures.Get("Mods/orbit-waveform");

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
                    new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Alpha = 0,
                        Children =
                        [
                            createHeader(logo),
                            loadoutBar = createLoadoutBar(),
                            categoryRail = createCategoryRail(),
                            modBrowser = createModBrowser(),
                            detailPanel = createDetailPanel(),
                            decorations = createDecorations(),
                            createFooter(),
                        ],
                    },
                    orbitWorkspace =
                        new GameplayModsOrbitWorkspace(
                            NavigateToCategoryPage,
                            ToggleMod,
                            FocusOrbitMod,
                            PreviewGlobalRate,
                            CompleteFixedRateInteraction,
                            ResetMods,
                            () => this.Exit(),
                            () =>
                            {
                                CommitSelection();
                                this.Exit();
                            },
                            category => definitionsFor(category),
                            isSelectable)
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                        },
                },
            },
        };

        orbitWorkspace.Build(logo, paperTexture, waveformTexture);
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
        Vector2 extra = stageSize - designed_size;
        float detailLeft = stageSize.X
                           - detail_panel_right_margin
                           - detail_panel_width;
        float browserWidth = MathF.Max(
            550,
            detailLeft - browser_left - browser_detail_gap);

        loadoutBar.Position = new Vector2(browser_left, loadout_top);
        loadoutBar.Width = stageSize.X - browser_left * 2;
        categoryRail.Y = browser_controls_top;
        categoryRail.Width = browserWidth;
        searchBox.SetLayoutWidth(MathF.Min(
            browserWidth,
            750 + MathF.Max(extra.X, 0) * 0.35f));
        modBrowserRestingY = mod_browser_top;
        detailPanelRestingY = browser_controls_top;
        if (!pageTransitioning)
        {
            modBrowser.Y = modBrowserRestingY;
            detailPanel.Y = detailPanelRestingY;
        }
        modBrowser.Width = browserWidth;
        modBrowser.Height = MathF.Max(
            stageSize.Y - footer_height - modBrowserRestingY - 14,
            260);
        detailPanel.Height = MathF.Max(
            stageSize.Y - footer_height - detailPanelRestingY - 14,
            390);

        int nextColumnCount =
            CalculateBrowserColumnCount(browserWidth);
        modColumnCount = nextColumnCount;
        rebuildModList();
        selectDetail(detailMod);
    }

    protected override bool OnKeyDown(KeyDownEvent e) =>
        HandleInteractionKey(e.Key, e.ShiftPressed)
        || base.OnKeyDown(e);

    protected override bool OnScroll(ScrollEvent e)
    {
        ProcessScrollGesture(e.ScrollDelta.Y, Time.Current);
        return true;
    }

    internal bool ProcessScrollGesture(double delta, double timestamp)
    {
        const double gesture_threshold = 0.45;
        const double gesture_lockout = 430;

        double direction = Math.Sign(delta);
        if (direction == 0)
            return false;

        if (direction != lastScrollGestureDirection)
        {
            scrollGestureAccumulator = 0;
            lastScrollGestureDirection = direction;
        }

        if (timestamp - lastScrollNavigationTime < gesture_lockout)
        {
            scrollGestureAccumulator = 0;
            return false;
        }

        scrollGestureAccumulator += delta;
        if (Math.Abs(scrollGestureAccumulator) < gesture_threshold)
            return false;

        double completedGesture = scrollGestureAccumulator;
        scrollGestureAccumulator = 0;
        if (!NavigatePageByScroll(completedGesture))
            return false;

        lastScrollNavigationTime = timestamp;
        showInteractionHint(
            completedGesture > 0
                ? "WHEEL · PREVIOUS PAGE"
                : "WHEEL · NEXT PAGE");
        return true;
    }

    internal bool HandleInteractionKey(
        Key key,
        bool shiftPressed = false)
    {
        switch (key)
        {
            case Key.Escape:
                if (searchQuery.Length > 0)
                {
                    searchBox.ClearQuery();
                    showInteractionHint("SEARCH CLEARED");
                    return true;
                }
                this.Exit();
                return true;

            case Key.M:
                this.Exit();
                return true;

            case Key.Slash:
                GetContainingFocusManager().ChangeFocus(searchBox);
                return true;

            case Key.R:
                ResetMods();
                showInteractionHint(
                    selectedMods.Mods.Count == 0
                        ? "ALL MODS CLEARED"
                        : "R · RESET MODS");
                return true;

            case Key.Tab:
                NavigateCategoryPage(shiftPressed ? -1 : 1, true);
                showInteractionHint(
                    shiftPressed
                        ? "SHIFT+TAB · PREVIOUS CATEGORY"
                        : "TAB · NEXT CATEGORY");
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

    internal bool NavigatePageByScroll(double delta)
    {
        if (pageTransitioning || Math.Abs(delta) < 0.001)
            return false;

        int offset = delta > 0 ? -1 : 1;
        int currentIndex = Array.IndexOf(
            visible_categories,
            activeCategory);
        int nextIndex = currentIndex + offset;
        if (nextIndex < 0 || nextIndex >= visible_categories.Length)
            return false;

        NavigateCategoryPage(offset, false);
        return true;
    }

    private void NavigateCategoryPage(int offset, bool wrap)
    {
        if (pageTransitioning || offset == 0)
            return;

        int currentIndex = Array.IndexOf(
            visible_categories,
            activeCategory);
        int nextIndex = currentIndex + Math.Sign(offset);
        if (wrap)
        {
            nextIndex = (nextIndex + visible_categories.Length)
                        % visible_categories.Length;
        }
        else
        {
            nextIndex = Math.Clamp(
                nextIndex,
                0,
                visible_categories.Length - 1);
        }

        if (nextIndex == currentIndex)
        {
            playPageEdgeFeedback(offset);
            return;
        }

        transitionToCategoryPage(
            visible_categories[nextIndex],
            Math.Sign(offset));
    }

    private void NavigateToCategoryPage(ManiaModCategory category)
    {
        int currentIndex = Array.IndexOf(
            visible_categories,
            activeCategory);
        int targetIndex = Array.IndexOf(
            visible_categories,
            category);
        if (targetIndex < 0 || targetIndex == currentIndex)
            return;

        transitionToCategoryPage(
            category,
            Math.Sign(targetIndex - currentIndex));
    }

    private void transitionToCategoryPage(
        ManiaModCategory category,
        int direction)
    {
        if (pageTransitioning)
            return;

        pageTransitioning = true;
        float travel = Math.Clamp(DrawHeight * 0.1f, 58, 92);
        float outgoingY = -direction * travel;
        orbitWorkspace?.TransitionOut(direction);

        modBrowser.ClearTransforms();
        detailPanel.ClearTransforms();
        modBrowser.MoveToY(
                      modBrowserRestingY + outgoingY,
                      125,
                      Easing.InCubic)
                  .FadeOut(95, Easing.OutQuint);
        detailPanel.Delay(18)
                   .MoveToY(
                       detailPanelRestingY + outgoingY,
                       125,
                       Easing.InCubic)
                   .FadeOut(95, Easing.OutQuint);

        Scheduler.AddDelayed(() =>
        {
            SetCategory(category);
            orbitWorkspace?.TransitionIn(direction);

            float incomingY = direction * travel;
            modBrowser.ClearTransforms();
            detailPanel.ClearTransforms();
            modBrowser.Y = modBrowserRestingY + incomingY;
            detailPanel.Y = detailPanelRestingY + incomingY;
            modBrowser.Alpha = 0;
            detailPanel.Alpha = 0;

            modBrowser.FadeIn(145, Easing.OutQuint)
                      .MoveToY(
                          modBrowserRestingY,
                          220,
                          Easing.OutQuint);
            detailPanel.Delay(22)
                       .FadeIn(145, Easing.OutQuint)
                       .MoveToY(
                           detailPanelRestingY,
                           220,
                           Easing.OutQuint);

            Scheduler.AddDelayed(
                () => pageTransitioning = false,
                225);
        }, 125);
    }

    private void playPageEdgeFeedback(int direction)
    {
        float offset = -Math.Sign(direction) * 8;
        modBrowser.ClearTransforms();
        detailPanel.ClearTransforms();
        modBrowser.MoveToY(
                      modBrowserRestingY + offset,
                      65,
                      Easing.OutQuint)
                  .Then()
                  .MoveToY(
                      modBrowserRestingY,
                      150,
                      Easing.OutBack);
        detailPanel.MoveToY(
                       detailPanelRestingY + offset,
                       65,
                       Easing.OutQuint)
                   .Then()
                   .MoveToY(
                       detailPanelRestingY,
                       150,
                       Easing.OutBack);
        showInteractionHint(
            direction < 0
                ? "FIRST PAGE"
                : "LAST PAGE");
    }

    internal void SetCategory(ManiaModCategory category)
    {
        if (!visible_categories.Contains(category))
            return;

        activeCategory = category;
        browseMode = browseModeFor(category);
        updateBrowseModeVisual();

        rebuildModList();
        focusPreferredMod(category);
        showInteractionHint(
            $"{categoryLabel(category).ToUpperInvariant()} · {visibleItems.Count} MODS");
    }

    private void setBrowseMode(ModsBrowseMode mode)
    {
        if (browseMode == mode)
            return;

        browseMode = mode;
        activeCategory = mode switch
        {
            ModsBrowseMode.Conversion => ManiaModCategory.Conversion,
            ModsBrowseMode.Automation => ManiaModCategory.Automation,
            ModsBrowseMode.Fun => ManiaModCategory.Fun,
            _ => ManiaModCategory.DifficultyReduction,
        };
        updateBrowseModeVisual();
        rebuildModList();
        focusPreferredMod(activeCategory);
        modScroll?.ScrollToStart();
        showInteractionHint(
            $"{mode.ToString().ToUpperInvariant()} · {visibleItems.Count} MODS");
    }

    private void updateBrowseModeVisual()
    {
        foreach ((ModsBrowseMode mode, GameplayModsCategoryChip button)
                 in categoryButtons)
        {
            button.SetSelected(mode == browseMode);
        }
    }

    private void setSearchQuery(string query)
    {
        searchQuery = query?.Trim() ?? string.Empty;
        rebuildModList();
        if (visibleItems.Count > 0
            && !visibleItems.ContainsKey(detailMod))
        {
            detailMod = visibleItems.Keys.First();
            updateFocusVisual();
            selectDetail(detailMod);
        }
        modScroll?.ScrollToStart();
    }

    internal void SetSearchQuery(string query) =>
        searchBox.SetQuery(query);

    internal void ToggleMod(ManiaModId mod)
    {
        detailMod = mod;
        if (!isSelectable(mod))
        {
            selectDetail(mod);
            ManiaModDefinition unavailable =
                OsuManiaModParityCatalog.Get(mod);
            showInteractionHint(
                $"{unavailable.Acronym} · REQUIRES OSU!STANDARD CHART");
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
        Height = 102,
        Children = new Drawable[]
        {
            new Sprite
            {
                Position = new Vector2(54, 12),
                Size = new Vector2(290, 98),
                Texture = logo,
            },
            new Box
            {
                Position = new Vector2(342, 18),
                Size = new Vector2(1.5f, 55),
                Colour = new Color4(
                    HomeControlColours.Navy.R,
                    HomeControlColours.Navy.G,
                    HomeControlColours.Navy.B,
                    0.46f),
            },
            new SpriteText
            {
                Position = new Vector2(370, 16),
                Text = "GAMEPLAY MODS",
                Font = HomeTypography.Hero(39),
                Scale = new Vector2(1.04f, 1),
                Colour = HomeControlColours.Navy,
            },
            new SpriteText
            {
                Position = new Vector2(372, 64),
                Text = "Customize your play experience.",
                Font = HomeTypography.Body(13),
                Colour = new Color4(
                    HomeControlColours.Navy.R,
                    HomeControlColours.Navy.G,
                    HomeControlColours.Navy.B,
                    0.66f),
            },
        },
    };

    private Container createLoadoutBar()
    {
        var bar = new Container
        {
            Position = new Vector2(browser_left, loadout_top),
            Size = new Vector2(
                designed_size.X - browser_left * 2,
                75),
            Masking = true,
            CornerRadius = 5,
            BorderThickness = 1.2f,
            BorderColour = new Color4(
                HomeControlColours.Cyan.R,
                HomeControlColours.Cyan.G,
                HomeControlColours.Cyan.B,
                0.48f),
        };

        bar.Children =
        [
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = new Color4(
                    HomeControlColours.PaleCyan.R,
                    HomeControlColours.PaleCyan.G,
                    HomeControlColours.PaleCyan.B,
                    0.42f),
            },
            new SpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 22,
                Text = "ACTIVE LOADOUT",
                Font = HomeTypography.Display(13),
                Spacing = new Vector2(1.1f, 0),
                Colour = HomeControlColours.Cyan,
            },
            activeMods = new FillFlowContainer
            {
                Position = new Vector2(258, 12),
                Width = 610,
                Height = 50,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(9, 0),
            },
            activeModsEmpty = new SpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 266,
                Text = "NO MODS ACTIVE",
                Font = HomeTypography.Display(10),
                Colour = new Color4(
                    HomeControlColours.Navy.R,
                    HomeControlColours.Navy.G,
                    HomeControlColours.Navy.B,
                    0.42f),
            },
            activeModsOverflow = new SpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 880,
                Font = HomeTypography.Display(9),
                Colour = HomeControlColours.Cyan,
                Alpha = 0,
            },
            new Box
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 870,
                Size = new Vector2(1, 38),
                Colour = new Color4(
                    HomeControlColours.Cyan.R,
                    HomeControlColours.Cyan.G,
                    HomeControlColours.Cyan.B,
                    0.62f),
            },
            new SpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.BottomLeft,
                Position = new Vector2(906, -2),
                Text = "SCORE MULTIPLIER",
                Font = HomeTypography.Display(8),
                Spacing = new Vector2(0.8f, 0),
                Colour = HomeControlColours.Cyan,
            },
            scoreMultiplierValue = new SpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.TopLeft,
                Position = new Vector2(906, 2),
                Font = HomeTypography.Display(16),
                Colour = HomeControlColours.Cyan,
            },
            new Box
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 1054,
                Size = new Vector2(1, 38),
                Colour = new Color4(
                    HomeControlColours.Cyan.R,
                    HomeControlColours.Cyan.G,
                    HomeControlColours.Cyan.B,
                    0.62f),
            },
            new SpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.BottomLeft,
                Position = new Vector2(1082, -2),
                Text = "COMPATIBILITY",
                Font = HomeTypography.Display(8),
                Spacing = new Vector2(0.8f, 0),
                Colour = HomeControlColours.Cyan,
            },
            compatibilityValue = new SpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.TopLeft,
                Position = new Vector2(1082, 2),
                Font = HomeTypography.Body(10),
                Colour = new Color4(0.22f, 0.64f, 0.34f, 1f),
            },
            new HomeMicroLine
            {
                Anchor = Anchor.BottomRight,
                Origin = Anchor.BottomRight,
                Position = new Vector2(-8, -1),
                Width = 84,
                Colour = HomeControlColours.Cyan,
            },
        ];
        return bar;
    }

    private Drawable createCategoryRail()
    {
        var container = new Container
        {
            Position = new Vector2(browser_left, browser_controls_top),
            Size = new Vector2(800, 85),
        };

        searchBox = new GameplayModsSearchBox(setSearchQuery);
        container.Add(searchBox);

        var flow = new FillFlowContainer
        {
            Position = new Vector2(0, 56),
            Width = 790,
            Height = 29,
            Direction = FillDirection.Horizontal,
            Spacing = new Vector2(10, 0),
        };
        container.Add(flow);

        (ModsBrowseMode Mode, string Label, IconUsage Icon, Color4 Accent, float Width)[] chips =
        [
            (ModsBrowseMode.All, "All", FontAwesome.Solid.ThLarge, HomeControlColours.Cyan, 84),
            (ModsBrowseMode.Difficulty, "Difficulty", FontAwesome.Solid.ChevronUp, HomeControlColours.Pink, 124),
            (ModsBrowseMode.Conversion, "Conversion", FontAwesome.Solid.LayerGroup, HomeControlColours.Cyan, 128),
            (ModsBrowseMode.Automation, "Automation", FontAwesome.Solid.Cog, HomeControlColours.Yellow, 132),
            (ModsBrowseMode.Fun, "Fun", FontAwesome.Solid.Star, HomeControlColours.Pink, 84),
        ];

        foreach (var chip in chips)
        {
            var button = new GameplayModsCategoryChip(
                chip.Label,
                chip.Icon,
                chip.Accent,
                chip.Width,
                () => setBrowseMode(chip.Mode));
            button.SetSelected(chip.Mode == browseMode);
            categoryButtons[chip.Mode] = button;
            flow.Add(button);
        }

        return container;
    }

    private Container createModBrowser() => new Container
    {
        Position = new Vector2(browser_left, mod_browser_top),
        Size = new Vector2(800, 454),
        Child = modScroll = new BasicScrollContainer(Direction.Vertical)
        {
            RelativeSizeAxes = Axes.Both,
            ScrollbarVisible = false,
            Child = modList = new Container
            {
                RelativeSizeAxes = Axes.X,
            },
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
            Position = new Vector2(16, 12),
            Scale = new Vector2(1.04f),
        };

        return new Container
        {
            Anchor = Anchor.TopRight,
            Origin = Anchor.TopRight,
            Position = new Vector2(
                -detail_panel_right_margin,
                browser_controls_top),
            Size = new Vector2(detail_panel_width, 562),
            Masking = true,
            CornerRadius = 7,
            BorderThickness = 1.2f,
            BorderColour = new Color4(
                HomeControlColours.Cyan.R,
                HomeControlColours.Cyan.G,
                HomeControlColours.Cyan.B,
                0.42f),
            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new Color4(1f, 1f, 1f, 0.94f),
                },
                new SpriteText
                {
                    Position = new Vector2(18, 17),
                    Text = "SELECTED MOD",
                    Font = HomeTypography.Display(10),
                    Spacing = new Vector2(1.8f, 0),
                    Colour = HomeControlColours.Cyan,
                    Alpha = 0,
                },
                new HomeMicroLine
                {
                    Position = new Vector2(129, 27),
                    Width = 317,
                    Alpha = 0,
                },
                detailBadge = new Container
                {
                    Position = new Vector2(18, 52),
                    Size = new Vector2(70),
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
                    Alpha = 0,
                },
                detailName = new SpriteText
                {
                    Position = new Vector2(32, 25),
                    Font = HomeTypography.Display(28),
                    Colour = HomeControlColours.Navy,
                },
                new SpriteIcon
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                    Position = new Vector2(-28, 27),
                    Size = new Vector2(18),
                    Icon = FontAwesome.Solid.ExternalLinkAlt,
                    Colour = HomeControlColours.Cyan,
                },
                detailDescription = new TextFlowContainer(text =>
                {
                    text.Font = HomeTypography.Body(14);
                    text.Colour = new Color4(
                        HomeControlColours.Navy.R,
                        HomeControlColours.Navy.G,
                        HomeControlColours.Navy.B,
                        0.7f);
                })
                {
                    Position = new Vector2(32, 65),
                    Width = 380,
                    AutoSizeAxes = Axes.Y,
                },
                detailHint = new SpriteText
                {
                    Position = new Vector2(32, 93),
                    Text = "SPACE TO TOGGLE",
                    Font = HomeTypography.Display(9),
                    Spacing = new Vector2(1.1f, 0),
                    Colour = HomeControlColours.Cyan,
                },
                new HomeDotField
                {
                    Position = new Vector2(423, 58),
                    Size = new Vector2(9, 58),
                    Colour = HomeControlColours.Cyan,
                },
                settingsHeader = new SpriteText
                {
                    Position = new Vector2(32, 116),
                    Text = "SETTINGS",
                    Font = HomeTypography.Display(10),
                    Spacing = new Vector2(1.8f, 0),
                    Colour = HomeControlColours.Cyan,
                },
                settingsDivider = new Box
                {
                    Position = new Vector2(109, 125),
                    Size = new Vector2(307, 1),
                    Colour = HomeControlColours.Cyan,
                },
                fixedRatePanel = createFixedRatePanel(),
                configurablePanel = new Container
                {
                    Position = new Vector2(18, 138),
                    Size = new Vector2(412, 198),
                    Masking = true,
                    CornerRadius = 8,
                    BorderThickness = 1.5f,
                    BorderColour = new Color4(
                        HomeControlColours.Cyan.R,
                        HomeControlColours.Cyan.G,
                        HomeControlColours.Cyan.B,
                        0.7f),
                    Alpha = 0,
                    Children = new Drawable[]
                    {
                        configurablePanelBackground = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = GameplayModSettingsTheme.Surface,
                        },
                        settingsHost,
                    },
                },
                new Container
                {
                    Position = new Vector2(18, 348),
                    Size = new Vector2(412, 78),
                    Masking = true,
                    CornerRadius = 5,
                    BorderThickness = 1,
                    BorderColour = new Color4(
                        HomeControlColours.Cyan.R,
                        HomeControlColours.Cyan.G,
                        HomeControlColours.Cyan.B,
                        0.24f),
                    Children =
                    [
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = new Color4(
                                HomeControlColours.PaleCyan.R,
                                HomeControlColours.PaleCyan.G,
                                HomeControlColours.PaleCyan.B,
                                0.26f),
                        },
                        new SpriteIcon
                        {
                            Position = new Vector2(14, 15),
                            Size = new Vector2(14),
                            Icon = FontAwesome.Solid.CheckCircle,
                            Colour = new Color4(0.38f, 0.68f, 0.22f, 1f),
                        },
                        new SpriteText
                        {
                            Position = new Vector2(38, 13),
                            Text = "COMPATIBILITY",
                            Font = HomeTypography.Display(10.5f),
                            Colour = new Color4(0.38f, 0.68f, 0.22f, 1f),
                        },
                        new SpriteText
                        {
                            Position = new Vector2(14, 38),
                            Text = "This mod works with your active loadout.",
                            Font = HomeTypography.Body(10.5f),
                            Colour = new Color4(
                                HomeControlColours.Navy.R,
                                HomeControlColours.Navy.G,
                                HomeControlColours.Navy.B,
                                0.58f),
                        },
                        new SpriteText
                        {
                            Position = new Vector2(14, 56),
                            Text = "No conflicts detected.",
                            Font = HomeTypography.Body(10.5f),
                            Colour = new Color4(
                                HomeControlColours.Navy.R,
                                HomeControlColours.Navy.G,
                                HomeControlColours.Navy.B,
                                0.58f),
                        },
                    ],
                },
                new SpriteText
                {
                    Position = new Vector2(32, 449),
                    Text = "SHORTCUTS",
                    Font = HomeTypography.Display(11),
                    Spacing = new Vector2(1.4f, 0),
                    Colour = HomeControlColours.Cyan,
                },
                createDetailShortcut(
                    "SPACE",
                    "Toggle",
                    new Vector2(32, 474),
                    58),
                createDetailShortcut(
                    "R",
                    "Reset setting",
                    new Vector2(230, 474),
                    34),
                createDetailShortcut(
                    "P",
                    "Toggle pitch",
                    new Vector2(32, 513),
                    34),
            },
        };
    }

    private static Drawable createDetailShortcut(
        string key,
        string label,
        Vector2 position,
        float keyWidth) =>
        new Container
        {
            Position = position,
            Size = new Vector2(186, 29),
            Children =
            [
                new Container
                {
                    Size = new Vector2(keyWidth, 29),
                    Masking = true,
                    CornerRadius = 4,
                    BorderThickness = 1,
                    BorderColour = new Color4(
                        HomeControlColours.Navy.R,
                        HomeControlColours.Navy.G,
                        HomeControlColours.Navy.B,
                        0.32f),
                    Children =
                    [
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = Color4.White,
                        },
                        new SpriteText
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Text = key,
                            Font = HomeTypography.Display(10),
                            Colour = HomeControlColours.Navy,
                        },
                    ],
                },
                new SpriteText
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    X = keyWidth + 12,
                    Text = label,
                    Font = HomeTypography.Body(12),
                    Colour = HomeControlColours.Navy,
                },
            ],
        };

    private Container createFixedRatePanel()
    {
        var panel = new Container
        {
            Position = new Vector2(32, 138),
            Size = new Vector2(284, 132),
            Scale = new Vector2(1.34f),
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
                Y = 65,
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
                Colour = Color4.White,
            },
            new Box
            {
                RelativeSizeAxes = Axes.X,
                Height = 2,
                Colour = HomeControlColours.Cyan,
            },
            new HomeDotField
            {
                Position = new Vector2(8, 34),
                Size = new Vector2(94, 38),
                Colour = new Color4(
                    HomeControlColours.Cyan.R,
                    HomeControlColours.Cyan.G,
                    HomeControlColours.Cyan.B,
                    0.22f),
            },
            new SongSelectFooterBackButton(this.Exit)
            {
                Position = new Vector2(110, 22),
                Scale = new Vector2(1.05f),
            },
            resetButton = new GameplayModsResetButton(ResetMods)
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Position = new Vector2(0, 18),
            },
            navigationHint = new SpriteText
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Y = 5,
                Text = "TAB · CATEGORIES    ARROWS · MODS    SPACE · TOGGLE",
                Font = HomeTypography.Display(8),
                Spacing = new Vector2(0.8f, 0),
                Colour = new Color4(
                    HomeControlColours.Navy.R,
                    HomeControlColours.Navy.G,
                    HomeControlColours.Navy.B,
                    0.58f),
            },
            interactionHint = new SpriteText
            {
                Anchor = Anchor.BottomCentre,
                Origin = Anchor.BottomCentre,
                Y = -5,
                Font = HomeTypography.Display(9),
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
                Position = new Vector2(320, 56),
                Size = new Vector2(8),
                Icon = FontAwesome.Solid.Plus,
                Colour = HomeControlColours.Cyan,
            },
            new HomeDotField
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                Position = new Vector2(-320, 0),
                Size = new Vector2(48, 28),
                Colour = new Color4(
                    HomeControlColours.Cyan.R,
                    HomeControlColours.Cyan.G,
                    HomeControlColours.Cyan.B,
                    0.2f),
            },
            new GameplayModsDoneButton(this.Exit)
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                Position = new Vector2(-19, 14),
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

        ManiaModCategory[] categories = searchQuery.Length > 0
            || browseMode == ModsBrowseMode.All
            ? visible_categories
            : browseMode == ModsBrowseMode.Difficulty
                ?
                [
                    ManiaModCategory.DifficultyReduction,
                    ManiaModCategory.DifficultyIncrease,
                ]
                : [activeCategory];

        float y;
        if (browseMode == ModsBrowseMode.All
            && searchQuery.Length == 0)
        {
            y = addFeaturedAllLayout();
        }
        else if (categories.Length > 1)
        {
            int sectionColumnCount = Math.Min(
                modColumnCount,
                categories.Length);
            const float sectionColumnGap = 38;
            float sectionColumnWidth = MathF.Max(
                (modBrowser.Width
                 - sectionColumnGap * (sectionColumnCount - 1))
                / sectionColumnCount,
                260);
            var sectionColumnHeights =
                new float[sectionColumnCount];

            foreach (ManiaModCategory category in categories)
            {
                int column = Array.IndexOf(
                    sectionColumnHeights,
                    sectionColumnHeights.Min());
                float height = addSection(
                    category,
                    column * (sectionColumnWidth + sectionColumnGap),
                    sectionColumnHeights[column],
                    sectionColumnWidth,
                    1);
                if (height <= 0)
                    continue;

                sectionColumnHeights[column] += height + 24;
            }

            y = sectionColumnHeights.Max();
        }
        else
        {
            y = addSection(
                    categories[0],
                    0,
                    0,
                    modBrowser.Width,
                    modColumnCount)
                + 18;
        }

        if (visibleItems.Count == 0)
        {
            modList.Add(new SpriteText
            {
                Position = new Vector2(4, 18),
                Text = "NO MODS MATCH YOUR SEARCH",
                Font = HomeTypography.Display(12),
                Colour = new Color4(
                    HomeControlColours.Navy.R,
                    HomeControlColours.Navy.G,
                    HomeControlColours.Navy.B,
                    0.46f),
            });
            y = 60;
        }

        modList.Height = MathF.Max(y, modBrowser?.Height ?? 0);
        updateFocusVisual();
    }

    private float addFeaturedAllLayout()
    {
        const float sectionColumnGap = 38;
        float sectionColumnWidth =
            (modBrowser.Width - sectionColumnGap) / 2;
        float rightX = sectionColumnWidth + sectionColumnGap;

        ManiaModId[] difficultyDown =
        [
            ManiaModId.HalfTime,
            ManiaModId.Easy,
            ManiaModId.NoFail,
            ManiaModId.Daycore,
        ];
        ManiaModId[] difficultyUp =
        [
            ManiaModId.HardRock,
            ManiaModId.SuddenDeath,
            ManiaModId.Perfect,
            ManiaModId.DoubleTime,
        ];
        ManiaModId[] funAndSpecial =
        [
            ManiaModId.Nightcore,
            ManiaModId.Hidden,
        ];
        ManiaModId[] challenge =
        [
            ManiaModId.Flashlight,
            ManiaModId.AccuracyChallenge,
        ];
        ManiaModId[] featured = difficultyDown
            .Concat(difficultyUp)
            .Concat(funAndSpecial)
            .Concat(challenge)
            .ToArray();
        ManiaModDefinition[] remaining =
            visible_categories
                .SelectMany(definitionsFor)
                .Where(definition => !featured.Contains(definition.Id))
                .ToArray();

        if (modColumnCount >= 3)
        {
            const float compactColumnGap = 34;
            float compactColumnWidth =
                (modBrowser.Width - compactColumnGap * 2) / 3;
            float secondX = compactColumnWidth + compactColumnGap;
            float thirdX = secondX
                           + compactColumnWidth
                           + compactColumnGap;

            addDefinitionSection(
                "DIFFICULTY DOWN",
                HomeControlColours.Cyan,
                definitionsForIds(difficultyDown),
                0,
                0,
                compactColumnWidth,
                1);
            addDefinitionSection(
                "DIFFICULTY UP",
                HomeControlColours.Pink,
                definitionsForIds(difficultyUp),
                secondX,
                0,
                compactColumnWidth,
                1);
            addDefinitionSection(
                "FUN & SPECIAL",
                new Color4(0.63f, 0.28f, 0.72f, 1f),
                definitionsForIds(funAndSpecial),
                thirdX,
                0,
                compactColumnWidth,
                1);
            addDefinitionSection(
                "CHALLENGE",
                HomeControlColours.Yellow,
                definitionsForIds(challenge),
                thirdX,
                154,
                compactColumnWidth,
                1);

            float compactRemainingHeight = addDefinitionSection(
                "MORE MODS",
                HomeControlColours.Cyan,
                remaining,
                0,
                312,
                modBrowser.Width,
                modColumnCount);
            return 312 + compactRemainingHeight + 18;
        }

        addDefinitionSection(
            "DIFFICULTY DOWN",
            HomeControlColours.Cyan,
            definitionsForIds(difficultyDown),
            0,
            0,
            sectionColumnWidth,
            1);
        addDefinitionSection(
            "DIFFICULTY UP",
            HomeControlColours.Pink,
            definitionsForIds(difficultyUp),
            rightX,
            0,
            sectionColumnWidth,
            1);
        addDefinitionSection(
            "FUN & SPECIAL",
            new Color4(0.63f, 0.28f, 0.72f, 1f),
            definitionsForIds(funAndSpecial),
            0,
            260,
            sectionColumnWidth,
            1);
        addDefinitionSection(
            "CHALLENGE",
            HomeControlColours.Yellow,
            definitionsForIds(challenge),
            rightX,
            260,
            sectionColumnWidth,
            1);

        float remainingHeight = addDefinitionSection(
            "MORE MODS",
            HomeControlColours.Cyan,
            remaining,
            0,
            470,
            modBrowser.Width,
            modColumnCount);
        return 470 + remainingHeight + 18;
    }

    private float addSection(
        ManiaModCategory category,
        float x,
        float y,
        float width,
        int itemColumnCount)
    {
        IReadOnlyList<ManiaModDefinition> definitions =
            filteredDefinitionsFor(category);
        return addDefinitionSection(
            categoryLabel(category).ToUpperInvariant(),
            categoryAccent(category),
            definitions,
            x,
            y,
            width,
            itemColumnCount);
    }

    private float addDefinitionSection(
        string label,
        Color4 accent,
        IReadOnlyList<ManiaModDefinition> definitions,
        float x,
        float y,
        float width,
        int itemColumnCount)
    {
        if (definitions.Count == 0)
            return 0;

        modList.Add(new SpriteText
        {
            Position = new Vector2(x, y),
            Text = label,
            Font = HomeTypography.Display(10),
            Spacing = new Vector2(1.7f, 0),
            Colour = accent,
        });
        var divider = new Box
        {
            Position = new Vector2(x + 128, y + 9),
            Size = new Vector2(
                MathF.Max(width - 145, 120),
                1),
            Colour = accent,
        };
        sectionDividers.Add(divider);
        modList.Add(divider);

        float rowSpacing = 54;
        float columnGap = 18;
        float columnWidth = MathF.Max(
            (width - columnGap * (itemColumnCount - 1))
            / itemColumnCount,
            220);
        for (int index = 0; index < definitions.Count; index++)
        {
            ManiaModDefinition definition = definitions[index];
            int rowCount =
                (definitions.Count + itemColumnCount - 1)
                / itemColumnCount;
            int column = index / rowCount;
            int row = index % rowCount;
            var item = new GameplayModListItem(
                definition,
                accentForMod(definition.Id, definition.Category),
                isSelectable(definition.Id),
                () => ToggleMod(definition.Id),
                null)
            {
                Position = new Vector2(
                    x + column * (columnWidth + columnGap),
                    y + 27 + row * rowSpacing),
            };
            item.SetLayoutWidth(columnWidth);
            item.SetSelected(selectedMods.Contains(definition.Id));
            visibleItems[definition.Id] = item;
            modList.Add(item);
        }

        return sectionHeight(definitions.Count, itemColumnCount);
    }

    private static float sectionHeight(
        int definitionCount,
        int itemColumnCount)
    {
        int rowCount =
            (definitionCount + itemColumnCount - 1)
            / itemColumnCount;
        const float rowSpacing = 54;

        return 27 + MathF.Max(rowCount - 1, 0) * rowSpacing + 42;
    }

    private void updateSelection()
    {
        foreach ((ManiaModId mod, GameplayModListItem item) in visibleItems)
            item.SetSelected(selectedMods.Contains(mod));

        settingsHost?.SetState(selectedMods, beatmap);
        rebuildActiveMods();
        orbitWorkspace?.SetState(
            activeCategory,
            detailMod,
            selectedMods);
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
        foreach (ManiaModId mod in selectedMods.Mods.Take(2))
        {
            ManiaModDefinition definition = OsuManiaModParityCatalog.Get(mod);
            string value = isRateMod(mod)
                ? $"{(selectedMods.FixedRateMod == mod
                    ? selectedMods.FixedRateSpeedChange
                    : selectedMods.PlaybackRate):0.##}x"
                : string.Empty;
            activeMods.Add(new GameplayActiveModChip(
                definition,
                value,
                accentForMod(definition.Id, definition.Category),
                () => ToggleMod(mod)));
        }

        activeModsEmpty.Alpha = selectedMods.Mods.Count == 0 ? 1 : 0;
        activeModsOverflow.Text = selectedMods.Mods.Count > 2
            ? $"+{selectedMods.Mods.Count - 2} MORE"
            : string.Empty;
        activeModsOverflow.Alpha = selectedMods.Mods.Count > 2 ? 1 : 0;
        scoreMultiplierValue.Text = $"{selectedMods.ScoreMultiplier:0.##}x";
        compatibilityValue.Text = "All good!";
    }

    private void selectDetail(ManiaModId mod)
    {
        ManiaModDefinition definition = OsuManiaModParityCatalog.Get(mod);
        orbitWorkspace?.SetState(
            activeCategory,
            mod,
            selectedMods);
        detailAcronym.Text = definition.Acronym;
        detailName.Text = YokkoStrings.ModName(definition);
        detailDescription.Clear();
        detailDescription.AddText(
            isSelectable(mod)
                ? YokkoStrings.ModDescription(definition)
                : YokkoStrings.Get("mods.standard_only"));

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
        detailName.Colour = HomeControlColours.Navy;

        bool fixedRateMod = isFixedRateMod(mod);
        bool configurable = isConfigurable(mod) && !fixedRateMod;
        settingsHeader.Text = configurable
            ? active
                ? "SETTINGS · ACTIVE"
                : "SETTINGS · PREVIEW"
            : "SETTINGS";
        settingsHeader.Y = 116;
        settingsDivider.Y = 125;
        settingsDivider.X = configurable ? 157 : 109;
        settingsDivider.Width = configurable ? 259 : 307;
        fixedRatePanel.Y = 138;
        configurablePanel.Alpha = configurable ? 1 : 0;
        fixedRatePanel.Alpha = configurable ? 0 : 1;
        detailHint.Alpha = configurable ? 0 : 1;
        detailHint.Text = fixedRateMod && !active
            ? "DRAG RATE OR SPACE · ENABLE"
            : mod == ManiaModId.HalfTime
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
                ? enabledRateMod
                    ? "SPEED MULTIPLIER"
                    : "SPEED MULTIPLIER · DRAG TO ENABLE"
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
            if (fixedRateMod)
            {
                fixedRateSlider.SetState(
                    true,
                    minimum,
                    maximum,
                    rate);
                fixedRateSlider.Alpha = 1;
            }
            else
            {
                fixedRateSlider.ClearTransforms();
                fixedRateSlider.Alpha = 0;
            }
            fixedRateMinimum.Alpha = fixedRateMod ? 1 : 0;
            fixedRateMidpoint.Alpha = fixedRateMod ? 1 : 0;
            fixedRateMaximum.Alpha = fixedRateMod ? 1 : 0;
            fixedRatePitch.SetState(
                fixedRateMod && enabledRateMod,
                isPitchAdjustableFixedRate(mod),
                enabledRateMod && selectedMods.FixedRateAdjustPitch);
            fixedRatePitch.Alpha = fixedRateMod ? 1 : 0;
        }
    }

    private void FocusOrbitMod(ManiaModId mod)
    {
        if (!isSelectable(mod))
            return;

        detailMod = mod;
        updateFocusVisual();
        selectDetail(mod);
    }

    private void PreviewGlobalRate(double value)
    {
        double nextValue = Math.Round(Math.Clamp(value, 0.5, 2), 2);
        bool adjustPitch = selectedMods.FixedRateAdjustPitch;
        ManiaModId? previousRateMod = selectedMods.FixedRateMod;

        if (Math.Abs(nextValue - 1) < 0.005)
        {
            if (selectedMods.FixedRateMod is ManiaModId currentRateMod)
                selectedMods = selectedMods.With(currentRateMod, false);
        }
        else
        {
            ManiaModId rateMod = nextValue < 1
                ? ManiaModId.HalfTime
                : ManiaModId.DoubleTime;
            double constrained = nextValue < 1
                ? Math.Min(nextValue, 0.99)
                : Math.Max(nextValue, 1.01);
            selectedMods = selectedMods.WithFixedRate(
                rateMod,
                constrained,
                adjustPitch);
        }

        selectionDirty = loadComplete;
        if (previousRateMod == selectedMods.FixedRateMod)
        {
            orbitWorkspace?.PreviewRate(nextValue);
        }
        else
        {
            orbitWorkspace?.SetState(
                activeCategory,
                detailMod,
                selectedMods);
        }
        settingsHost?.SetState(selectedMods, beatmap);
        resetButton?.SetEnabled(selectedMods.Mods.Count > 0);
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
        if (orbitWorkspace?.GetAdjacentMod(
                detailMod,
                direction.X < 0 || direction.Y < 0 ? -1 : 1)
            is ManiaModId orbitNext)
        {
            FocusOrbitMod(orbitNext);
            ManiaModDefinition orbitDefinition =
                OsuManiaModParityCatalog.Get(orbitNext);
            showInteractionHint(
                $"{orbitDefinition.Acronym} · {orbitDefinition.Name.ToUpperInvariant()}   SPACE · TOGGLE");
            return;
        }

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
        && (!isKeyConversionMod(mod)
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
        || isKeyConversionMod(mod);

    private static bool isKeyConversionMod(ManiaModId mod) =>
        mod is >= ManiaModId.Key1 and <= ManiaModId.Key10;

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
                && definition.Id != ManiaModId.ScoreV2
                && !isKeyConversionMod(definition.Id))
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

    private static IReadOnlyList<ManiaModDefinition> definitionsForIds(
        IReadOnlyList<ManiaModId> ids)
    {
        var definitions = OsuManiaModParityCatalog.All
            .ToDictionary(definition => definition.Id);
        return ids
            .Where(definitions.ContainsKey)
            .Select(id => definitions[id])
            .ToArray();
    }

    private IReadOnlyList<ManiaModDefinition> filteredDefinitionsFor(
        ManiaModCategory category)
    {
        IReadOnlyList<ManiaModDefinition> definitions =
            definitionsFor(category);
        if (searchQuery.Length == 0)
            return definitions;

        return definitions
            .Where(definition =>
                definition.Name.Contains(
                    searchQuery,
                    StringComparison.OrdinalIgnoreCase)
                || definition.Acronym.Contains(
                    searchQuery,
                    StringComparison.OrdinalIgnoreCase)
                || definition.Description.Contains(
                    searchQuery,
                    StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    private static ModsBrowseMode browseModeFor(
        ManiaModCategory category) =>
        category switch
        {
            ManiaModCategory.Conversion => ModsBrowseMode.Conversion,
            ManiaModCategory.Automation => ModsBrowseMode.Automation,
            ManiaModCategory.Fun => ModsBrowseMode.Fun,
            _ => ModsBrowseMode.Difficulty,
        };

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
