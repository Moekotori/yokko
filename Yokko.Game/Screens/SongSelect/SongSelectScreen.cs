using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Framework.Screens;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;
using Yokko.Core.Beatmaps;
using Yokko.Core.Difficulty;
using Yokko.Core.Gameplay;
using Yokko.Core.Mods;
using Yokko.Core.Scoring;
using Yokko.Game.Importing;
using Yokko.Game.Localisation;
using Yokko.Game.Presentation;
using Yokko.Game.Screens.Gameplay;
using Yokko.Game.Screens.Main;
using Yokko.Game.Scoring;
using Yokko.Game.Skinning.OsuMania;

namespace Yokko.Game.Screens.SongSelect;

public partial class SongSelectScreen : Screen
{
    private const double list_refresh_stagger = 28;
    private const int max_staggered_rows = 7;
    private const float designed_height = 900;
    private const float footer_height = 136;
    private const float details_top = 145;
    private const float ranking_top = 303;
    private const float ranking_height = 190;

    private readonly List<SongSelectEntry> entries = createEntries();
    private readonly Dictionary<string, SongSelectEntry> importedEntries =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly List<SongSelectSongRow> rows = new();
    private readonly HashSet<string> collapsedPackages =
        new(StringComparer.OrdinalIgnoreCase);

    private TextureStore textures;
    private TextureStore chartArtworkTextures;
    private Container stage;
    private Sprite backgroundA;
    private Sprite backgroundB;
    private Sprite activeBackground;
    private Container detailsHost;
    private Container songBrowser;
    private Container footer;
    private FillFlowContainer songList;
    private BasicScrollContainer songScroll;
    private SongSelectRankingPanel rankingPanel;
    private SpriteText noResults;
    private SongSelectFilterButton allFilter;
    private SongSelectFilterButton fourKeyFilter;
    private SongSelectFilterButton sevenKeyFilter;
    private SongSelectSearchBox searchBox;
    private SongSelectModButton doubleTimeMod;
    private SongSelectModButton nightcoreMod;
    private SongSelectModButton halfTimeMod;
    private SongSelectModButton daycoreMod;
    private SongSelectModButton easyMod;
    private SongSelectModButton noFailMod;
    private SongSelectModButton suddenDeathMod;
    private SongSelectModButton perfectMod;
    private SongSelectModButton hardRockMod;
    private SongSelectModButton accuracyChallengeMod;
    private SongSelectModButton mirrorMod;
    private SongSelectModButton randomMod;
    private SongSelectModButton holdOffMod;
    private SongSelectModButton noReleaseMod;
    private SongSelectModButton fadeInMod;
    private SongSelectModButton hiddenMod;
    private SongSelectModButton coverMod;
    private SongSelectModButton flashlightMod;
    private SongSelectModButton constantSpeedMod;
    private SongSelectModButton difficultyAdjustMod;
    private SongSelectModButton autoplayMod;
    private SongSelectModButton cinemaMod;
    private SongSelectModButton invertMod;
    private SongSelectModButton classicMod;
    private SongSelectModButton mutedMod;
    private SongSelectModButton windUpMod;
    private SongSelectModButton windDownMod;
    private SongSelectModButton adaptiveSpeedMod;
    private SongSelectModsToggleButton modsToggleButton;
    private Container modPanel;
    private SongSelectModSettingsHost modSettingsHost;
    private AnimatedGifSprite mascotAnimation;

    private List<SongSelectEntry> visibleEntries;
    private List<SongSelectEntry> navigableEntries = [];
    private SongSelectEntry selectedEntry;
    private KeyMode? keyModeFilter;
    private string searchQuery = string.Empty;
    private SongSelectScoreView scoreView = SongSelectScoreView.GlobalRanking;
    private ManiaModSet selectedMods = ManiaModSet.Empty;
    private bool modPanelOpen;
    [Resolved]
    private GameplayScoreStore scoreStore { get; set; }
    [Resolved]
    private ImportedChartLibrary importedChartLibrary { get; set; }
    [Resolved]
    private IRenderer renderer { get; set; }

    internal SongSelectEntry SelectedEntry => selectedEntry;
    internal int VisibleEntryCount => visibleEntries?.Count ?? 0;
    internal int VisibleRowCount => navigableEntries.Count;
    internal KeyMode? KeyModeFilter => keyModeFilter;
    internal string SearchQuery => searchQuery;
    internal SongSelectScoreView ScoreView => scoreView;
    internal ManiaModSet SelectedMods => selectedMods;
    internal SongSelectAccuracyChallengeSettings
        AccuracyChallengeSettings =>
            modSettingsHost?.AccuracySettings;
    internal SongSelectDifficultyAdjustSettings
        DifficultyAdjustSettings =>
            modSettingsHost?.DifficultySettings;
    internal SongSelectMutedSettings MutedSettings =>
        modSettingsHost?.MutedSettings;
    internal SongSelectTimeRampSettings TimeRampSettings =>
        modSettingsHost?.TimeRampSettings;
    internal SongSelectAdaptiveSpeedSettings AdaptiveSpeedSettings =>
        modSettingsHost?.AdaptiveSettings;
    internal SongSelectKeyConversionSettings KeyConversionSettings =>
        modSettingsHost?.KeySettings;
    internal bool IsModPanelOpen => modPanelOpen;
    internal int MascotFrameCount => mascotAnimation?.FrameCount ?? 0;
    internal static bool RankingFitsDesignedStage =>
        details_top + ranking_top + ranking_height
        <= designed_height - footer_height;
    internal bool RankingFitsAboveFooter =>
        rankingPanel != null
        && detailsHost != null
        && detailsHost.Y + rankingPanel.Y + rankingPanel.Height
        <= DrawHeight - footer_height + 0.5f;

    internal bool IsPackageCollapsed(string packageId) =>
        collapsedPackages.Contains(packageId);

    internal void TogglePackage(string packageId)
    {
        if (string.IsNullOrWhiteSpace(packageId))
            return;

        if (!collapsedPackages.Add(packageId))
            collapsedPackages.Remove(packageId);

        applyFilters();
    }

    [BackgroundDependencyLoader]
    private void load(TextureStore textureStore)
    {
        textures = textureStore;
        chartArtworkTextures = new TextureStore(
            renderer,
            new TextureLoaderStore(
                new ConstrainedTextureResourceStore(
                    new ChartArtworkResourceStore(),
                    renderer.MaxTextureSize)),
            scaleAdjust: 1);
        synchroniseImportedCharts();
        importedChartLibrary.LibraryChanged += onChartLibraryChanged;
        refreshSavedScores();
        selectedEntry = entries.LastOrDefault();
        visibleEntries = entries.ToList();

        Texture firstWallpaper = textureFor(selectedEntry);
        Texture logo = textures.Get("home-logo-hd");

        InternalChildren = new Drawable[]
        {
            backgroundA = createBackground(firstWallpaper),
            backgroundB = createBackground(firstWallpaper),
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = new Color4(0.01f, 0.03f, 0.16f, 0.22f),
            },
            createLibraryShade(),
            createIvoryStage(),
            stage = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    createHeader(logo),
                    detailsHost = new Container
                    {
                        Position = new Vector2(72, details_top),
                        Size = new Vector2(415, 495),
                    },
                    createSongBrowser(),
                    createFooter(),
                    mascotAnimation = new AnimatedGifSprite(
                        "Textures/SongSelect/mascot-box.gif")
                    {
                        Anchor = Anchor.BottomLeft,
                        Origin = Anchor.BottomLeft,
                        Position = new Vector2(39, -60),
                        Size = new Vector2(251),
                    },
                    createDecorations(),
                },
            },
        };

        backgroundB.Alpha = 0;
        activeBackground = backgroundA;
        rebuildDetails();
        rebuildSongList();
        updateFilters();

        stage.Alpha = 0;
        stage.Y = 14;
    }

    public override void OnEntering(ScreenTransitionEvent e)
    {
        base.OnEntering(e);
        stage.FadeIn(260, Easing.OutQuint).MoveToY(0, 420, Easing.OutQuint);
    }

    public override void OnResuming(ScreenTransitionEvent e)
    {
        base.OnResuming(e);
        synchroniseImportedCharts();
        int selectedIndex = Math.Max(0, entries.IndexOf(selectedEntry));
        refreshSavedScores();
        selectedEntry = entries.Count == 0
            ? null
            : entries[Math.Min(selectedIndex, entries.Count - 1)];
        applyFilters();
        rebuildDetails();
        this.FadeIn(180, Easing.OutQuint);
    }

    public override void OnSuspending(ScreenTransitionEvent e)
    {
        base.OnSuspending(e);
        this.FadeTo(0.35f, 180, Easing.OutQuint);
    }

    public override bool OnExiting(ScreenExitEvent e)
    {
        this.FadeOut(180, Easing.OutQuint);
        return base.OnExiting(e);
    }

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
        {
            if (importedChartLibrary != null)
                importedChartLibrary.LibraryChanged -= onChartLibraryChanged;

            chartArtworkTextures?.Dispose();
        }

        base.Dispose(isDisposing);
    }

    protected override bool OnKeyDown(KeyDownEvent e)
    {
        switch (e.Key)
        {
            case Key.Up:
                SelectPrevious();
                return true;

            case Key.Down:
                SelectNext();
                return true;

            case Key.M:
                ToggleModPanel();
                return true;

            case Key.Enter:
                PlaySelected();
                return true;

            case Key.Escape:
                this.Exit();
                return true;

            default:
                return base.OnKeyDown(e);
        }
    }

    internal void SelectNext() => selectOffset(1);

    internal void SelectPrevious() => selectOffset(-1);

    internal void SetKeyModeFilter(KeyMode? mode)
    {
        keyModeFilter = mode;
        updateFilters();
        applyFilters();
    }

    internal void SetSearchQuery(string query)
    {
        searchQuery = query ?? string.Empty;
        applyFilters();
    }

    internal void ToggleScoreView()
    {
        scoreView = scoreView == SongSelectScoreView.GlobalRanking
            ? SongSelectScoreView.Personal
            : SongSelectScoreView.GlobalRanking;
        rebuildDetails();
    }

    internal void PlaySelected()
    {
        if (selectedEntry != null)
        {
            this.Push(new GameplayScreen(
                selectedEntry.Beatmap,
                mods: selectedMods,
                cinemaArtworkPath: selectedEntry.WallpaperTexture));
        }
    }

    internal void ToggleMod(ManiaModId mod)
    {
        bool enabled = !selectedMods.Contains(mod);
        selectedMods = mod == ManiaModId.Random && enabled
            ? selectedMods.WithRandomSeed(Random.Shared.Next())
            : selectedMods.With(mod, enabled);
        if (enabled
            && (mod is ManiaModId.AccuracyChallenge
                or ManiaModId.DifficultyAdjust
                or ManiaModId.Muted
                or ManiaModId.WindUp
                or ManiaModId.WindDown
                or ManiaModId.AdaptiveSpeed
                or ManiaModId.DualStages
                || mod is >= ManiaModId.Key1
                    and <= ManiaModId.Key10))
        {
            modSettingsHost?.Show(mod);
        }
        onSelectedModsChanged();
    }

    internal void SetAccuracyChallengeMinimum(double value)
    {
        selectedMods = selectedMods.WithAccuracyChallenge(
            value,
            selectedMods.AccuracyChallengeMode);
        onSelectedModsChanged();
    }

    internal void SetAccuracyChallengeMode(ManiaAccuracyMode mode)
    {
        selectedMods = selectedMods.WithAccuracyChallenge(
            selectedMods.AccuracyChallengeMinimum,
            mode);
        onSelectedModsChanged();
    }

    internal void SetDifficultyAdjustDrainRate(double? value)
    {
        selectedMods = selectedMods.WithDifficultyAdjust(
            value,
            selectedMods.DifficultyAdjustOverallDifficulty,
            selectedMods.DifficultyAdjustExtendedLimits);
        onSelectedModsChanged();
    }

    internal void SetDifficultyAdjustOverallDifficulty(double? value)
    {
        selectedMods = selectedMods.WithDifficultyAdjust(
            selectedMods.DifficultyAdjustDrainRate,
            value,
            selectedMods.DifficultyAdjustExtendedLimits);
        onSelectedModsChanged();
    }

    internal void UseMapDifficultyValues()
    {
        selectedMods = selectedMods.WithDifficultyAdjust(
            null,
            null,
            selectedMods.DifficultyAdjustExtendedLimits);
        onSelectedModsChanged();
    }

    internal void SetDifficultyAdjustExtendedLimits(bool value)
    {
        selectedMods = selectedMods.WithDifficultyAdjust(
            selectedMods.DifficultyAdjustDrainRate,
            selectedMods.DifficultyAdjustOverallDifficulty,
            value);
        onSelectedModsChanged();
    }

    internal void SetMutedInverse(bool value)
    {
        selectedMods = selectedMods.WithMuted(
            value,
            selectedMods.MutedMetronome,
            selectedMods.MutedComboCount,
            selectedMods.MutedAffectsHitSounds);
        onSelectedModsChanged();
    }

    internal void SetMutedMetronome(bool value)
    {
        selectedMods = selectedMods.WithMuted(
            selectedMods.MutedInverse,
            value,
            selectedMods.MutedComboCount,
            selectedMods.MutedAffectsHitSounds);
        onSelectedModsChanged();
    }

    internal void SetMutedComboCount(int value)
    {
        selectedMods = selectedMods.WithMuted(
            selectedMods.MutedInverse,
            selectedMods.MutedMetronome,
            value,
            selectedMods.MutedAffectsHitSounds);
        onSelectedModsChanged();
    }

    internal void SetMutedAffectsHitSounds(bool value)
    {
        selectedMods = selectedMods.WithMuted(
            selectedMods.MutedInverse,
            selectedMods.MutedMetronome,
            selectedMods.MutedComboCount,
            value);
        onSelectedModsChanged();
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
        onSelectedModsChanged();
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
        onSelectedModsChanged();
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
        onSelectedModsChanged();
    }

    internal void SetAdaptiveInitialRate(double value)
    {
        if (!selectedMods.HasAdaptiveSpeed)
            return;
        selectedMods = selectedMods.WithAdaptiveSpeed(
            value,
            selectedMods.AdaptiveAdjustPitch);
        onSelectedModsChanged();
    }

    internal void SetAdaptiveAdjustPitch(bool value)
    {
        if (!selectedMods.HasAdaptiveSpeed)
            return;
        selectedMods = selectedMods.WithAdaptiveSpeed(
            selectedMods.AdaptiveInitialRate,
            value);
        onSelectedModsChanged();
    }

    private void onSelectedModsChanged()
    {
        updateModSelection();
        refreshSavedScores();
        rebuildDetails();
        rebuildSongList();
    }

    internal void ToggleModPanel()
    {
        modPanelOpen = !modPanelOpen;
        modsToggleButton?.SetOpen(modPanelOpen);

        if (modPanel == null)
            return;

        modPanel.ClearTransforms();
        if (modPanelOpen)
        {
            modPanel.Show();
            modPanel.Alpha = 0;
            modPanel.Y = -2;
            modPanel.FadeIn(150, Easing.OutQuint)
                    .MoveToY(-10, 190, Easing.OutQuint);
        }
        else
        {
            modPanel.FadeOut(110, Easing.OutQuint)
                    .MoveToY(-2, 140, Easing.OutQuint);
        }
    }

    private Drawable createHeader(Texture logo) => new Container
    {
        RelativeSizeAxes = Axes.Both,
        Children = new Drawable[]
        {
            new Sprite
            {
                Position = new Vector2(75, 24),
                Size = new Vector2(343, 105),
                Texture = logo,
            },
            searchBox = new SongSelectSearchBox(SetSearchQuery)
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                Position = new Vector2(-92, 23),
                Size = new Vector2(487, 40),
            },
            new FillFlowContainer
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                Position = new Vector2(-340, 72),
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(8, 0),
                Children = new Drawable[]
                {
                    allFilter = new SongSelectFilterButton("ALL SONGS", 116, () => SetKeyModeFilter(null), accentDot: true),
                    fourKeyFilter = new SongSelectFilterButton("4K", 54, () => SetKeyModeFilter(KeyMode.FourKey)),
                    sevenKeyFilter = new SongSelectFilterButton("7K", 54, () => SetKeyModeFilter(KeyMode.SevenKey)),
                },
            },
            new Container
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                Position = new Vector2(-44, 23),
                Size = new Vector2(40),
                Masking = true,
                CornerRadius = 4,
                BorderThickness = 1.2f,
                BorderColour = new Color4(1f, 1f, 1f, 0.78f),
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = new Color4(SongSelectTheme.DeepNavy.R, SongSelectTheme.DeepNavy.G, SongSelectTheme.DeepNavy.B, 0.72f),
                    },
                    new SpriteIcon
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Size = new Vector2(17),
                        Icon = FontAwesome.Solid.SlidersH,
                        Colour = SongSelectTheme.Cyan,
                    },
                },
            },
        },
    };

    private Drawable createSongBrowser() => songBrowser = new Container
    {
        Anchor = Anchor.TopRight,
        Origin = Anchor.TopRight,
        Position = new Vector2(0, 112),
        Size = new Vector2(668, 520),
        Children = new Drawable[]
        {
            songScroll = new BasicScrollContainer
            {
                RelativeSizeAxes = Axes.Both,
                ScrollbarVisible = false,
                Child = songList = new FillFlowContainer
                {
                    X = 24,
                    Width = 644,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = Vector2.Zero,
                },
            },
            noResults = new SpriteText
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Text = YokkoStrings.Get("song_select.no_results"),
                Font = HomeTypography.Display(24),
                Colour = SongSelectTheme.PaleCyan,
                Alpha = 0,
            },
        },
    };

    private Drawable createFooter()
    {
        doubleTimeMod = new SongSelectModButton(
            "DT",
            SongSelectTheme.Pink,
            () => ToggleMod(ManiaModId.DoubleTime));
        nightcoreMod = new SongSelectModButton(
            "NC",
            SongSelectTheme.Yellow,
            () => ToggleMod(ManiaModId.Nightcore));
        halfTimeMod = new SongSelectModButton(
            "HT",
            SongSelectTheme.Cyan,
            () => ToggleMod(ManiaModId.HalfTime));
        daycoreMod = new SongSelectModButton(
            "DC",
            SongSelectTheme.Pink,
            () => ToggleMod(ManiaModId.Daycore));
        easyMod = new SongSelectModButton(
            "EZ",
            SongSelectTheme.Cyan,
            () => ToggleMod(ManiaModId.Easy));
        noFailMod = new SongSelectModButton(
            "NF",
            SongSelectTheme.Pink,
            () => ToggleMod(ManiaModId.NoFail));
        suddenDeathMod = new SongSelectModButton(
            "SD",
            SongSelectTheme.Yellow,
            () => ToggleMod(ManiaModId.SuddenDeath));
        perfectMod = new SongSelectModButton(
            "PF",
            SongSelectTheme.Pink,
            () => ToggleMod(ManiaModId.Perfect));
        hardRockMod = new SongSelectModButton(
            "HR",
            SongSelectTheme.Pink,
            () => ToggleMod(ManiaModId.HardRock));
        accuracyChallengeMod = new SongSelectModButton(
            "AC",
            SongSelectTheme.Yellow,
            () => ToggleMod(ManiaModId.AccuracyChallenge));
        mirrorMod = new SongSelectModButton(
            "MR",
            SongSelectTheme.Cyan,
            () => ToggleMod(ManiaModId.Mirror));
        randomMod = new SongSelectModButton(
            "RD",
            SongSelectTheme.Pink,
            () => ToggleMod(ManiaModId.Random));
        holdOffMod = new SongSelectModButton(
            "HO",
            SongSelectTheme.Yellow,
            () => ToggleMod(ManiaModId.HoldOff));
        noReleaseMod = new SongSelectModButton(
            "NR",
            SongSelectTheme.Cyan,
            () => ToggleMod(ManiaModId.NoRelease));
        fadeInMod = new SongSelectModButton(
            "FI",
            SongSelectTheme.Yellow,
            () => ToggleMod(ManiaModId.FadeIn));
        hiddenMod = new SongSelectModButton(
            "HD",
            SongSelectTheme.Pink,
            () => ToggleMod(ManiaModId.Hidden));
        coverMod = new SongSelectModButton(
            "CO",
            SongSelectTheme.Cyan,
            () => ToggleMod(ManiaModId.Cover));
        flashlightMod = new SongSelectModButton(
            "FL",
            SongSelectTheme.Yellow,
            () => ToggleMod(ManiaModId.Flashlight));
        constantSpeedMod = new SongSelectModButton(
            "CS",
            SongSelectTheme.Cyan,
            () => ToggleMod(ManiaModId.ConstantSpeed));
        difficultyAdjustMod = new SongSelectModButton(
            "DA",
            SongSelectTheme.Pink,
            () => ToggleMod(ManiaModId.DifficultyAdjust));
        autoplayMod = new SongSelectModButton(
            "AT",
            SongSelectTheme.Cyan,
            () => ToggleMod(ManiaModId.Autoplay));
        cinemaMod = new SongSelectModButton(
            "CN",
            SongSelectTheme.Pink,
            () => ToggleMod(ManiaModId.Cinema));
        invertMod = new SongSelectModButton(
            "IN",
            SongSelectTheme.Yellow,
            () => ToggleMod(ManiaModId.Invert));
        classicMod = new SongSelectModButton(
            "CL",
            SongSelectTheme.Pink,
            () => ToggleMod(ManiaModId.Classic));
        mutedMod = new SongSelectModButton(
            "MU",
            SongSelectTheme.Cyan,
            () => ToggleMod(ManiaModId.Muted));
        windUpMod = new SongSelectModButton(
            "WU",
            SongSelectTheme.Yellow,
            () => ToggleMod(ManiaModId.WindUp));
        windDownMod = new SongSelectModButton(
            "WD",
            SongSelectTheme.Cyan,
            () => ToggleMod(ManiaModId.WindDown));
        adaptiveSpeedMod = new SongSelectModButton(
            "AS",
            SongSelectTheme.Pink,
            () => ToggleMod(ManiaModId.AdaptiveSpeed));

        var mods = modsToggleButton = new SongSelectModsToggleButton(
            ToggleModPanel)
        {
            Anchor = Anchor.TopCentre,
            Origin = Anchor.TopCentre,
            X = -14,
            Y = 8,
        };
        modPanel = createModPanel();
        updateModSelection();

        return footer = new Container
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
                    Colour = new Color4(0.278f, 0.81f, 0.949f, 1f),
                },
                new Box
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 2,
                    Colour = new Color4(1f, 1f, 1f, 0.72f),
                },
                new HomeDotField
                {
                    Position = new Vector2(10, 65),
                    Size = new Vector2(112, 34),
                    Colour = new Color4(1f, 1f, 1f, 0.38f),
                },
                new SpriteIcon
                {
                    Position = new Vector2(35, 17),
                    Size = new Vector2(13),
                    Icon = FontAwesome.Solid.Plus,
                    Colour = SongSelectTheme.Ivory,
                },
                new SpriteIcon
                {
                    Position = new Vector2(41, 61),
                    Size = new Vector2(9),
                    Icon = FontAwesome.Solid.Plus,
                    Colour = new Color4(1f, 1f, 1f, 0.84f),
                },
                new SpriteIcon
                {
                    Position = new Vector2(277, 73),
                    Size = new Vector2(9),
                    Icon = FontAwesome.Solid.Plus,
                    Colour = new Color4(1f, 1f, 1f, 0.9f),
                },
                new SpriteIcon
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Position = new Vector2(-194, 12),
                    Size = new Vector2(8),
                    Icon = FontAwesome.Solid.Plus,
                    Colour = new Color4(1f, 1f, 1f, 0.88f),
                },
                new HomeDotField
                {
                    Position = new Vector2(948, 43),
                    Size = new Vector2(44, 34),
                    Colour = new Color4(1f, 1f, 1f, 0.3f),
                },
                new SpriteIcon
                {
                    Position = new Vector2(1049, 72),
                    Size = new Vector2(9),
                    Icon = FontAwesome.Solid.Plus,
                    Colour = new Color4(1f, 1f, 1f, 0.9f),
                },
                new Box
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Position = new Vector2(-80, 36),
                    Size = new Vector2(1.4f, 43),
                    Rotation = 14,
                    Colour = new Color4(1f, 1f, 1f, 0.82f),
                },
                new SongSelectFooterBackButton(this.Exit)
                {
                    Position = new Vector2(272, 21.5f),
                },
                modPanel,
                mods,
                new HomePrimaryAction(
                    "PLAY",
                    "SONG SELECT",
                    FontAwesome.Solid.Play,
                    PlaySelected,
                    iconTileSize: 84,
                    iconTileX: 26,
                    iconSize: 35,
                    iconTileY: 4,
                    contentX: 140)
                {
                    Anchor = Anchor.BottomRight,
                    Origin = Anchor.BottomRight,
                    Position = new Vector2(-13, -17),
                    Scale = new Vector2(0.86f),
                },
            },
        };
    }

    private Container createModPanel() => new Container
    {
        Anchor = Anchor.TopCentre,
        Origin = Anchor.BottomCentre,
        Position = new Vector2(-14, -10),
        Size = new Vector2(525, 326),
        Alpha = 0,
        Depth = -5,
        Masking = true,
        CornerRadius = 8,
        BorderThickness = 1,
        BorderColour = SongSelectTheme.Cyan,
        Children = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = new Color4(
                    SongSelectTheme.DeepNavy.R,
                    SongSelectTheme.DeepNavy.G,
                    SongSelectTheme.DeepNavy.B,
                    0.97f),
            },
            new Box
            {
                RelativeSizeAxes = Axes.X,
                Height = 3,
                Colour = SongSelectTheme.Yellow,
            },
            new SpriteIcon
            {
                Position = new Vector2(18, 15),
                Size = new Vector2(14),
                Icon = FontAwesome.Solid.SlidersH,
                Colour = SongSelectTheme.Cyan,
            },
            new SpriteText
            {
                Position = new Vector2(42, 12),
                Text = "GAMEPLAY MODS",
                Font = HomeTypography.Display(13),
                Spacing = new Vector2(0.5f, 0),
                Colour = SongSelectTheme.Ivory,
            },
            new SpriteText
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                Position = new Vector2(-16, 13),
                Text = "M",
                Font = HomeTypography.Display(10),
                Colour = SongSelectTheme.Pink,
            },
            new Box
            {
                Position = new Vector2(16, 41),
                Size = new Vector2(493, 1),
                Colour = new Color4(
                    SongSelectTheme.Cyan.R,
                    SongSelectTheme.Cyan.G,
                    SongSelectTheme.Cyan.B,
                    0.26f),
            },
            createModRow(
                50,
                easyMod,
                noFailMod,
                halfTimeMod,
                daycoreMod,
                noReleaseMod),
            createModRow(
                94,
                hardRockMod,
                suddenDeathMod,
                perfectMod,
                accuracyChallengeMod,
                doubleTimeMod),
            createModRow(
                138,
                nightcoreMod,
                fadeInMod,
                hiddenMod,
                coverMod,
                flashlightMod),
            createModRow(
                182,
                mirrorMod,
                randomMod,
                holdOffMod,
                invertMod,
                classicMod),
            createModRow(
                226,
                constantSpeedMod,
                difficultyAdjustMod,
                autoplayMod,
                cinemaMod,
                mutedMod),
            createModRow(
                270,
                windUpMod,
                windDownMod,
                adaptiveSpeedMod),
            new Box
            {
                Position = new Vector2(292, 50),
                Size = new Vector2(1, 260),
                Colour = new Color4(
                    SongSelectTheme.Cyan.R,
                    SongSelectTheme.Cyan.G,
                    SongSelectTheme.Cyan.B,
                    0.3f),
            },
            modSettingsHost =
                new SongSelectModSettingsHost(
                    SetAccuracyChallengeMinimum,
                    SetAccuracyChallengeMode,
                    SetDifficultyAdjustDrainRate,
                    SetDifficultyAdjustOverallDifficulty,
                    UseMapDifficultyValues,
                    SetDifficultyAdjustExtendedLimits,
                    SetMutedInverse,
                    SetMutedMetronome,
                    SetMutedComboCount,
                    SetMutedAffectsHitSounds,
                    SetTimeRampInitialRate,
                    SetTimeRampFinalRate,
                    SetTimeRampAdjustPitch,
                    SetAdaptiveInitialRate,
                    SetAdaptiveAdjustPitch,
                    ToggleMod)
                {
                    Position = new Vector2(306, 48),
                },
        },
    };

    private static Drawable createModRow(
        float y,
        params SongSelectModButton[] buttons) =>
        new FillFlowContainer
        {
            Position = new Vector2(19, y),
            AutoSizeAxes = Axes.Both,
            Direction = FillDirection.Horizontal,
            Spacing = new Vector2(6, 0),
            Children = buttons,
        };

    protected override void Update()
    {
        base.Update();

        if (songBrowser != null)
            songBrowser.Height = MathF.Max(
                360,
                DrawHeight - footer_height - songBrowser.Y);
    }

    private static Drawable createDecorations() => new Container
    {
        RelativeSizeAxes = Axes.Both,
        Depth = 5,
        Children = new Drawable[]
        {
            new Box
            {
                Position = new Vector2(16, 18),
                Size = new Vector2(1, 54),
                Colour = new Color4(
                    SongSelectTheme.Navy.R,
                    SongSelectTheme.Navy.G,
                    SongSelectTheme.Navy.B,
                    0.62f),
            },
            new SpriteIcon
            {
                Position = new Vector2(12, 78),
                Size = new Vector2(9),
                Icon = FontAwesome.Solid.Plus,
                Colour = SongSelectTheme.Cyan,
            },
            new Box
            {
                Position = new Vector2(16, 96),
                Size = new Vector2(1, 252),
                Colour = new Color4(
                    SongSelectTheme.Navy.R,
                    SongSelectTheme.Navy.G,
                    SongSelectTheme.Navy.B,
                    0.62f),
            },
            new FillFlowContainer
            {
                Position = new Vector2(14, 360),
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 7),
                Children = Enumerable.Range(0, 7)
                                     .Select(_ => (Drawable)new SpriteIcon
                                     {
                                         Size = new Vector2(4),
                                         Icon = FontAwesome.Solid.Circle,
                                         Colour = SongSelectTheme.Cyan,
                                     })
                                     .ToArray(),
            },
            new SpriteIcon
            {
                Position = new Vector2(10, 538),
                Size = new Vector2(11),
                Icon = FontAwesome.Regular.Heart,
                Colour = SongSelectTheme.Pink,
            },
            new SpriteIcon
            {
                Position = new Vector2(33, 127),
                Size = new Vector2(13),
                Icon = FontAwesome.Solid.Plus,
                Colour = SongSelectTheme.Yellow,
            },
            new SpriteIcon
            {
                Position = new Vector2(358, 158),
                Size = new Vector2(12),
                Icon = FontAwesome.Solid.Plus,
                Colour = SongSelectTheme.Pink,
            },
            new SpriteIcon
            {
                Position = new Vector2(493, 414),
                Size = new Vector2(11),
                Icon = FontAwesome.Solid.Plus,
                Colour = SongSelectTheme.Cyan,
            },
        },
    };

    private void rebuildDetails()
    {
        if (detailsHost == null || selectedEntry == null)
            return;

        detailsHost.Clear();
        YokkoBeatmap appliedBeatmap =
            ManiaBeatmapModTransformer.Apply(
                selectedEntry.Beatmap,
                selectedMods);
        double appliedLengthMilliseconds =
            appliedBeatmap.HitObjects.Count == 0
                ? 0
                : appliedBeatmap.HitObjects.Max(hitObject =>
                    hitObject.EndTimeMilliseconds
                    ?? hitObject.StartTimeMilliseconds);
        YokkoBeatmap difficultyBeatmap =
            ManiaTimeRampTimeline.TransformForDifficulty(
                appliedBeatmap,
                selectedMods);
        double displayedLengthMilliseconds =
            selectedMods.HasTimeRamp
                ? difficultyBeatmap.HitObjects.Count == 0
                    ? 0
                    : difficultyBeatmap.HitObjects.Max(hitObject =>
                        hitObject.EndTimeMilliseconds
                        ?? hitObject.StartTimeMilliseconds)
                : appliedLengthMilliseconds
                  / selectedMods.PlaybackRate;
        string displayedBpm = selectedMods.HasTimeRamp
            ? $"{selectedEntry.Bpm * selectedMods.TimeRampInitialRate:0}"
              + "→"
              + $"{selectedEntry.Bpm * selectedMods.TimeRampFinalRate:0}"
            : (selectedEntry.Bpm * selectedMods.PlaybackRate)
                .ToString("0");

        rankingPanel = new SongSelectRankingPanel(selectedEntry, textures, newView => scoreView = newView)
        {
            Position = new Vector2(0, ranking_top),
        };
        rankingPanel.SetView(scoreView, textures);

        detailsHost.AddRange(new Drawable[]
        {
            new SpriteText
            {
                Text = "SONG SELECT",
                Font = HomeTypography.Display(10),
                Spacing = new Vector2(2.2f, 0),
                Colour = SongSelectTheme.Cyan,
            },
            new Box
            {
                Position = new Vector2(92, 8),
                Size = new Vector2(102, 1),
                Colour = new Color4(
                    SongSelectTheme.Cyan.R,
                    SongSelectTheme.Cyan.G,
                    SongSelectTheme.Cyan.B,
                    0.65f),
            },
            new SpriteIcon
            {
                Position = new Vector2(212, 2),
                Size = new Vector2(10),
                Icon = FontAwesome.Solid.Plus,
                Colour = SongSelectTheme.Pink,
            },
            new SpriteText
            {
                Position = new Vector2(0, 28),
                Width = 350,
                Truncate = true,
                Text = selectedEntry.Beatmap.Title,
                Font = HomeTypography.Display(50),
                Colour = SongSelectTheme.Navy,
            },
            new SpriteIcon
            {
                Position = new Vector2(306, 54),
                Size = new Vector2(23),
                Icon = FontAwesome.Solid.Heartbeat,
                Colour = SongSelectTheme.Cyan,
            },
            new SpriteText
            {
                Position = new Vector2(0, 83),
                Text = selectedEntry.Beatmap.Artist,
                Font = HomeTypography.Display(20),
                Colour = SongSelectTheme.Navy,
            },
            new SpriteText
            {
                Position = new Vector2(0, 107),
                Text = $"mapped by {selectedEntry.Beatmap.Creator}",
                Font = HomeTypography.Body(14),
                Colour = SongSelectTheme.Cyan,
            },
            new Container
            {
                Position = new Vector2(0, 137),
                AutoSizeAxes = Axes.Both,
                Masking = true,
                CornerRadius = 3,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = SongSelectTheme.Pink,
                    },
                    new SpriteText
                    {
                        Position = new Vector2(9, 3),
                        Text = appliedBeatmap.StageCount == 2
                            ? $"{appliedBeatmap.KeysPerStage}K + "
                              + $"{appliedBeatmap.KeysPerStage}K · "
                              + selectedEntry.Beatmap.DifficultyName
                            : $"{(int)appliedBeatmap.KeyMode}K · "
                              + selectedEntry.Beatmap.DifficultyName,
                        Font = HomeTypography.Display(13),
                        Colour = Color4.White,
                    },
                },
            },
            createStarRating(
                ManiaStarRatingCalculator.CalculateResult(
                    difficultyBeatmap,
                    selectedMods.HasTimeRamp
                        ? 1
                        : selectedMods.PlaybackRate)),
            new Box
            {
                Position = new Vector2(0, 216),
                Width = 340,
                Height = 1,
                Colour = new Color4(
                    SongSelectTheme.Cyan.R,
                    SongSelectTheme.Cyan.G,
                    SongSelectTheme.Cyan.B,
                    0.55f),
            },
            createSongStat(
                0,
                232,
                FontAwesome.Regular.Clock,
                "LENGTH",
                TimeSpan.FromMilliseconds(
                    displayedLengthMilliseconds).ToString(@"mm\:ss")),
            createSongStat(
                115,
                232,
                FontAwesome.Solid.WaveSquare,
                "BPM",
                displayedBpm),
            createBestScoreStat(225, 232),
            new Box
            {
                Position = new Vector2(0, 290),
                Width = 340,
                Height = 1,
                Colour = new Color4(
                    SongSelectTheme.Navy.R,
                    SongSelectTheme.Navy.G,
                    SongSelectTheme.Navy.B,
                    0.14f),
            },
            rankingPanel,
        });
    }

    private Drawable createBestScoreStat(float x, float y) => new Container
    {
        Position = new Vector2(x, y),
        Size = new Vector2(150, 58),
        Children = new Drawable[]
        {
            new SpriteIcon
            {
                Position = new Vector2(0, 4),
                Size = new Vector2(15),
                Icon = FontAwesome.Solid.Trophy,
                Colour = SongSelectTheme.Cyan,
            },
            new SpriteText
            {
                Position = new Vector2(20, 0),
                Text = "BEST SCORE",
                Font = HomeTypography.Display(9),
                Colour = new Color4(
                    SongSelectTheme.Navy.R,
                    SongSelectTheme.Navy.G,
                    SongSelectTheme.Navy.B,
                    0.72f),
            },
            new SpriteText
            {
                Position = new Vector2(20, 17),
                Text = selectedEntry.BestScore > 0 ? $"{selectedEntry.BestScore:N0}" : "NO SCORE YET",
                Font = HomeTypography.Display(selectedEntry.BestScore > 0 ? 18 : 12),
                Colour = SongSelectTheme.Navy,
            },
            new SpriteText
            {
                Position = new Vector2(20, 40),
                Text = selectedEntry.BestAccuracy > 0 ? $"ACC  {selectedEntry.BestAccuracy:P2}" : string.Empty,
                Font = HomeTypography.Display(14),
                Colour = SongSelectTheme.Pink,
            },
        },
    };

    private static Drawable createSongStat(float x, float y, IconUsage icon, LocalisableString label, string value) => new Container
    {
        Position = new Vector2(x, y),
        Size = new Vector2(104, 52),
        Children = new Drawable[]
        {
            new SpriteIcon
            {
                Position = new Vector2(0, 4),
                Size = new Vector2(15),
                Icon = icon,
                Colour = SongSelectTheme.Cyan,
            },
            new SpriteText
            {
                Position = new Vector2(20, 0),
                Text = label,
                Font = HomeTypography.Display(9),
                Colour = new Color4(
                    SongSelectTheme.Navy.R,
                    SongSelectTheme.Navy.G,
                    SongSelectTheme.Navy.B,
                    0.72f),
            },
            new SpriteText
            {
                Position = new Vector2(20, 17),
                Text = value,
                Font = HomeTypography.Display(20),
                Colour = SongSelectTheme.Navy,
            },
        },
    };

    private static Drawable createStarRating(
        ManiaStarRatingResult rating)
    {
        double value = rating.Value ?? 0;
        int filled = rating.IsSuccess ? (int)Math.Min(5, Math.Floor(value)) : 0;

        var flow = new FillFlowContainer
        {
            Position = new Vector2(0, 178),
            AutoSizeAxes = Axes.Both,
            Direction = FillDirection.Horizontal,
            Spacing = new Vector2(4, 0),
        };

        for (int i = 0; i < 5; i++)
        {
            flow.Add(new SpriteIcon
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                Size = new Vector2(21),
                Icon = i < filled ? FontAwesome.Solid.Star : FontAwesome.Regular.Star,
                Colour = rating.IsSuccess ? SongSelectTheme.Yellow : SongSelectTheme.PaleCyan,
            });
        }

        flow.Add(new SpriteText
        {
            Anchor = Anchor.CentreLeft,
            Origin = Anchor.CentreLeft,
            X = 6,
            Text = rating.Value?.ToString("0.00") ?? "--",
            Font = HomeTypography.Display(22),
            Colour = SongSelectTheme.Navy,
        });

        return flow;
    }

    private void rebuildSongList()
    {
        if (songList == null)
            return;

        songList.Clear();
        rows.Clear();
        navigableEntries = [];
        int drawableIndex = 0;

        foreach (IGrouping<string, SongSelectEntry> group in visibleEntries.GroupBy(
                     entry => entry.PackageId,
                     StringComparer.OrdinalIgnoreCase))
        {
            SongSelectEntry first = group.First();
            SongSelectEntry[] groupEntries = group.ToArray();
            bool collapsed = first.IsPackage
                             && collapsedPackages.Contains(first.PackageId)
                             && string.IsNullOrWhiteSpace(searchQuery);

            if (first.IsPackage)
            {
                int songCount = groupEntries
                                .Select(entry =>
                                    $"{entry.Beatmap.Artist}\u001f{entry.Beatmap.Title}")
                                .Distinct(StringComparer.OrdinalIgnoreCase)
                                .Count();
                var header = new SongSelectPackageHeader(
                    first.PackageName,
                    songCount,
                    groupEntries.Length,
                    collapsed,
                    () => TogglePackage(first.PackageId))
                {
                    Alpha = 0,
                    X = 12,
                };
                songList.Add(header);
                header.Delay(Math.Min(drawableIndex++, max_staggered_rows) * list_refresh_stagger)
                      .FadeIn(150, Easing.OutQuint)
                      .MoveToX(0, 210, Easing.OutQuint);
            }

            if (collapsed)
                continue;

            foreach (SongSelectEntry entry in groupEntries)
            {
                SongSelectSongRow row = new(
                    entry,
                    textureFor(entry),
                    () => select(entry),
                    () =>
                    {
                        select(entry);
                        PlaySelected();
                    });
                row.SetSelected(entry == selectedEntry);
                row.Alpha = 0;
                row.X = 24;
                rows.Add(row);
                navigableEntries.Add(entry);
                songList.Add(row);

                double delay = Math.Min(drawableIndex++, max_staggered_rows)
                               * list_refresh_stagger;
                row.Delay(delay)
                   .FadeIn(170, Easing.OutQuint)
                   .MoveToX(0, 240, Easing.OutQuint);
            }
        }

        noResults.FadeTo(visibleEntries.Count == 0 ? 1 : 0, 140, Easing.OutQuint);

        SongSelectSongRow selectedRow = rows.FirstOrDefault(row =>
            row.Entry == selectedEntry);
        if (selectedRow != null)
        {
            Scheduler.AddDelayed(() =>
            {
                songScroll?.ScrollIntoView(selectedRow, false);
                songScroll?.ScrollBy(-11, false);
            }, 260);
        }
    }

    private void applyFilters()
    {
        visibleEntries = entries.Where(entry =>
            (!keyModeFilter.HasValue || entry.Beatmap.KeyMode == keyModeFilter) &&
            (string.IsNullOrWhiteSpace(searchQuery) ||
             entry.Beatmap.Title.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) ||
             entry.Beatmap.Artist.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) ||
             entry.Beatmap.Creator.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) ||
             entry.Beatmap.DifficultyName.Contains(searchQuery, StringComparison.OrdinalIgnoreCase)))
                                .ToList();

        rebuildSongList();

        if (navigableEntries.Count > 0
            && !navigableEntries.Contains(selectedEntry))
            select(navigableEntries[0]);
    }

    private void selectOffset(int direction)
    {
        if (navigableEntries.Count == 0)
            return;

        int index = navigableEntries.IndexOf(selectedEntry);
        if (index < 0)
            index = 0;
        else
            index = (index + direction + navigableEntries.Count) % navigableEntries.Count;

        select(navigableEntries[index]);
    }

    private void select(SongSelectEntry entry, bool rebuildList = true)
    {
        if (entry == null)
            return;

        bool changed = selectedEntry != entry;
        selectedEntry = entry;

        if (changed)
        {
            crossFadeBackground(textureFor(entry));
            rebuildDetails();
            modSettingsHost?.SetState(selectedMods, entry.Beatmap);
        }

        if (rebuildList)
        {
            foreach (SongSelectSongRow row in rows)
                row.SetSelected(row.Entry == entry);

            SongSelectSongRow selectedRow = rows.FirstOrDefault(row => row.Entry == entry);
            if (selectedRow != null)
                songScroll?.ScrollIntoView(selectedRow, true);
        }
    }

    private void crossFadeBackground(Texture texture)
    {
        Sprite incoming = activeBackground == backgroundA ? backgroundB : backgroundA;
        incoming.Texture = texture;
        incoming.Alpha = 0;
        incoming.FadeIn(220, Easing.OutQuint);
        activeBackground.FadeOut(220, Easing.OutQuint);
        activeBackground = incoming;
    }

    private Texture textureFor(SongSelectEntry entry)
    {
        if (entry != null
            && Path.IsPathRooted(entry.WallpaperTexture)
            && File.Exists(entry.WallpaperTexture))
        {
            try
            {
                Texture artwork = chartArtworkTextures.Get(entry.WallpaperTexture);
                if (artwork != null)
                    return artwork;
            }
            catch
            {
                // Invalid chart artwork falls back to Yokko's bundled image.
            }
        }

        return textures.Get(entry?.WallpaperTexture ?? "SongSelect/blue-signal")
               ?? textures.Get("SongSelect/blue-signal");
    }

    private void refreshSavedScores()
    {
        for (int i = 0; i < entries.Count; i++)
        {
            SongSelectEntry entry = entries[i];
            StoredGameplayScore saved = scoreStore.GetBest(
                entry.Beatmap,
                selectedMods);
            IEnumerable<SongSelectScore> ranking = entry.Ranking
                .Where(score => !score.IsCurrentPlayer);

            if (saved != null)
            {
                ranking = ranking.Append(new SongSelectScore(
                                   0,
                                   "YOKKO",
                                   "yokko",
                                   saved.Rank,
                                   (int)Math.Min(int.MaxValue, saved.Score),
                                   saved.Accuracy,
                                   saved.Mods ?? [],
                                   true))
                                 .OrderByDescending(score => score.Score);
            }

            SongSelectScore[] ranked = ranking
                               .Select((score, rank) => score with
                               {
                                   Rank = rank + 1,
                               })
                               .ToArray();

            entries[i] = entry with
            {
                BestScore = saved == null
                    ? 0
                    : (int)Math.Min(int.MaxValue, saved.Score),
                BestAccuracy = saved?.Accuracy ?? 0,
                Ranking = ranked,
            };
        }
    }

    private void updateModSelection()
    {
        doubleTimeMod?.SetSelected(
            selectedMods.Contains(ManiaModId.DoubleTime));
        nightcoreMod?.SetSelected(
            selectedMods.Contains(ManiaModId.Nightcore));
        halfTimeMod?.SetSelected(
            selectedMods.Contains(ManiaModId.HalfTime));
        daycoreMod?.SetSelected(
            selectedMods.Contains(ManiaModId.Daycore));
        easyMod?.SetSelected(
            selectedMods.Contains(ManiaModId.Easy));
        noFailMod?.SetSelected(
            selectedMods.Contains(ManiaModId.NoFail));
        suddenDeathMod?.SetSelected(
            selectedMods.Contains(ManiaModId.SuddenDeath));
        perfectMod?.SetSelected(
            selectedMods.Contains(ManiaModId.Perfect));
        hardRockMod?.SetSelected(
            selectedMods.Contains(ManiaModId.HardRock));
        accuracyChallengeMod?.SetSelected(
            selectedMods.Contains(
                ManiaModId.AccuracyChallenge));
        mirrorMod?.SetSelected(
            selectedMods.Contains(ManiaModId.Mirror));
        randomMod?.SetSelected(
            selectedMods.Contains(ManiaModId.Random));
        holdOffMod?.SetSelected(
            selectedMods.Contains(ManiaModId.HoldOff));
        noReleaseMod?.SetSelected(
            selectedMods.Contains(ManiaModId.NoRelease));
        fadeInMod?.SetSelected(
            selectedMods.Contains(ManiaModId.FadeIn));
        hiddenMod?.SetSelected(
            selectedMods.Contains(ManiaModId.Hidden));
        coverMod?.SetSelected(
            selectedMods.Contains(ManiaModId.Cover));
        flashlightMod?.SetSelected(
            selectedMods.Contains(ManiaModId.Flashlight));
        constantSpeedMod?.SetSelected(
            selectedMods.Contains(ManiaModId.ConstantSpeed));
        difficultyAdjustMod?.SetSelected(
            selectedMods.Contains(ManiaModId.DifficultyAdjust));
        autoplayMod?.SetSelected(
            selectedMods.Contains(ManiaModId.Autoplay));
        cinemaMod?.SetSelected(
            selectedMods.Contains(ManiaModId.Cinema));
        invertMod?.SetSelected(
            selectedMods.Contains(ManiaModId.Invert));
        classicMod?.SetSelected(
            selectedMods.Contains(ManiaModId.Classic));
        mutedMod?.SetSelected(
            selectedMods.Contains(ManiaModId.Muted));
        windUpMod?.SetSelected(
            selectedMods.Contains(ManiaModId.WindUp));
        windDownMod?.SetSelected(
            selectedMods.Contains(ManiaModId.WindDown));
        adaptiveSpeedMod?.SetSelected(
            selectedMods.HasAdaptiveSpeed);
        if (selectedEntry?.Beatmap != null)
        {
            modSettingsHost?.SetState(
                selectedMods,
                selectedEntry.Beatmap);
        }

        modsToggleButton?.SetCount(selectedMods.Mods.Count);
    }

    private void onChartLibraryChanged() =>
        Scheduler.Add(() => synchroniseImportedCharts(true));

    private void synchroniseImportedCharts(bool selectNewest = false)
    {
        string selectedImportedId = importedEntries
                                    .Where(pair => ReferenceEquals(
                                        pair.Value.Beatmap,
                                        selectedEntry?.Beatmap))
                                    .Select(pair => pair.Key)
                                    .FirstOrDefault();

        foreach (SongSelectEntry existing in importedEntries.Values)
        {
            int existingIndex = entries.FindIndex(entry =>
                ReferenceEquals(entry.Beatmap, existing.Beatmap));
            if (existingIndex >= 0)
                entries.RemoveAt(existingIndex);
        }

        importedEntries.Clear();

        foreach (ImportedChart chart in importedChartLibrary.GetCharts())
        {
            SongSelectEntry entry = createImportedEntry(chart);
            importedEntries[chart.Id] = entry;
            entries.Add(entry);
        }

        if (!selectNewest
            && selectedImportedId != null
            && importedEntries.TryGetValue(
                selectedImportedId,
                out SongSelectEntry preservedSelection))
        {
            selectedEntry = preservedSelection;
        }

        if (songList == null)
            return;

        if (selectNewest)
        {
            keyModeFilter = null;
            searchQuery = string.Empty;
            if (searchBox?.Current.Value.Length > 0)
                searchBox.Current.Value = string.Empty;
            updateFilters();
        }

        applyFilters();

        if (selectNewest && importedEntries.Count > 0)
            select(entries[^1]);
    }

    private void updateFilters()
    {
        allFilter?.SetSelected(!keyModeFilter.HasValue);
        fourKeyFilter?.SetSelected(keyModeFilter == KeyMode.FourKey);
        sevenKeyFilter?.SetSelected(keyModeFilter == KeyMode.SevenKey);
    }

    private static Sprite createBackground(Texture texture) => new()
    {
        RelativeSizeAxes = Axes.Both,
        Texture = texture,
        FillMode = FillMode.Fill,
    };

    private static Drawable createLibraryShade() => new Box
    {
        RelativeSizeAxes = Axes.Y,
        Anchor = Anchor.TopRight,
        Origin = Anchor.TopRight,
        Width = 700,
        Colour = ColourInfo.GradientHorizontal(
            new Color4(
                SongSelectTheme.DeepNavy.R,
                SongSelectTheme.DeepNavy.G,
                SongSelectTheme.DeepNavy.B,
                0.08f),
            new Color4(
                SongSelectTheme.DeepNavy.R,
                SongSelectTheme.DeepNavy.G,
                SongSelectTheme.DeepNavy.B,
                0.94f)),
    };

    private static Drawable createIvoryStage() => new Container
    {
        RelativeSizeAxes = Axes.Both,
        Children = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Y,
                Width = 414,
                Colour = SongSelectTheme.Ivory,
            },
            new Box
            {
                RelativeSizeAxes = Axes.Y,
                X = 414,
                Y = -12,
                Width = 110,
                Height = 1.08f,
                Rotation = 3,
                Colour = SongSelectTheme.Ivory,
            },
        },
    };

    private static List<SongSelectEntry> createEntries() => [];

    private static SongSelectEntry createImportedEntry(ImportedChart imported)
    {
        YokkoBeatmap beatmap = imported.Result.Beatmap;
        double lengthMilliseconds = beatmap.HitObjects.Count == 0
            ? 0
            : beatmap.HitObjects.Max(hitObject =>
                hitObject.EndTimeMilliseconds ?? hitObject.StartTimeMilliseconds);
        double bpm = beatmap.TimingPoints
                            .Where(point => point.Uninherited && point.BeatsPerMinute > 0)
                            .Select(point => point.BeatsPerMinute)
                            .FirstOrDefault();

        return new SongSelectEntry(
            beatmap,
            imported.ArtworkPath ?? "SongSelect/blue-signal",
            imported.StarRating,
            TimeSpan.FromMilliseconds(Math.Max(0, lengthMilliseconds)),
            bpm,
            0,
            0,
            [],
            imported.PackageId,
            imported.PackageName,
            imported.IsPackage);
    }

}
