using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
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
using osu.Framework.Logging;
using osu.Framework.Screens;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;
using Yokko.Audio;
using Yokko.Core.Beatmaps;
using Yokko.Core.Difficulty;
using Yokko.Core.Gameplay;
using Yokko.Core.Mods;
using Yokko.Core.Scoring;
using Yokko.Game.Audio;
using Yokko.Game.Diagnostics;
using Yokko.Game.Gameplay;
using Yokko.Game.Importing;
using Yokko.Game.Localisation;
using Yokko.Game.Presentation;
using Yokko.Game.Screens.Gameplay;
using Yokko.Game.Screens.Main;
using Yokko.Game.Screens.Settings;
using Yokko.Game.Scoring;
using Yokko.Game.Skinning.OsuMania;

namespace Yokko.Game.Screens.SongSelect;

public partial class SongSelectScreen : Screen
{
    private const double playbackRateShortcutStep = 0.05;
    private const double minimumPlaybackRate = 0.5;
    private const double maximumPlaybackRate = 2;
    private const float designed_height = 1080;
    private const float footer_height = 130;
    private const float details_top = 82;
    private const float details_left = 36;
    private const float details_width = 850;
    private const float details_panel_height = 320;
    private const float details_artwork_size = 280;
    private const float selected_artwork_rotation = -1.25f;
    private const float details_content_left = 310;
    private const float details_content_width = 522;
    private const double details_title_units_per_line = 26;
    private const float ranking_top = 340;
    private const float ranking_height = 508;
    private const float browse_top = 184;
    private const float browse_width = 980;
    private const float browse_right = 24;
    private const float browse_height =
        designed_height - footer_height - browse_top - 16;
    private const int initial_artwork_preload_limit = 16;
    private const int page_navigation_step = 5;
    private const string demo_profile_name = "YOKKO DEMO";
    private const string demo_profile_level = "LV.114514";

    private readonly List<SongSelectEntry> entries = createEntries();
    private readonly IAudioEngine suppliedPreviewAudioEngine;
    private readonly Action requestNextPreload;
    private readonly SongSelectSelectionMemory selectionMemory;
    private readonly ISongSelectPreviewHost previewHost;
    private readonly Dictionary<string, SongSelectEntry> importedEntries =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ImportedChart> importedChartModels =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> collapsedPackages =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<
        SongSelectEntry,
        Dictionary<DifficultyCacheState, ManiaDifficultyRatings>>
        difficultyRatingsCache =
            new(ReferenceEqualityComparer.Instance);

    private TextureStore textures;
    private Container stage;
    private Container header;
    private Sprite backgroundA;
    private Sprite backgroundB;
    private Sprite activeBackground;
    private Container detailsHost;
    private Container activeDetailsContent;
    private Container selectedChartFactsRow;
    private Container selectedPerformanceRow;
    private Container songBrowser;
    private Container footer;
    private SongSelectFooterBackButton footerBackButton;
    private SongSelectAccountCard accountCard;
    private Container footerToolDock;
    private Container[] footerToolShadows;
    private Box[] footerToolDividers;
    private SongSelectFooterToolButton randomFooterButton;
    private SongSelectFooterToolButton optionsFooterButton;
    private SongSelectPlayButton playButton;
    private Container shortcutLegend;
    private SongSelectVirtualisedList songList;
    private SongSelectRankingPanel rankingPanel;
    private ScoreResultInputBlocker scoreResultHost;
    private GameplayResultOverlay scoreResultOverlay;
    private SongSelectScore activeScoreResult;
    private SongSelectNoResultsPanel noResults;
    private SongSelectKeyModeFilterButton keyModeFilterButton;
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
    private SongSelectSelectedModsButton selectedModsButton;
    private Container modPanel;
    private SongSelectModSettingsHost modSettingsHost;
    private SpriteText modInfoTitle;
    private SpriteText modInfoDescription;
    private ManiaModId? hoveredMod;
    private SongSelectPreviewPlayer previewPlayer;
    private SongSelectBrowseToolButton sortButton;
    private SongSelectSortPopover sortPopover;
    private SongSelectBrowseToolButton groupButton;
    private SongSelectBrowseToolButton convertsButton;
    private SongSelectBrowseToolButton filtersButton;
    private Container browseToolbar;
    private Container filtersPopover;
    private bool filtersPopoverOpen;
    private Container topNavigation;
    private Sprite topNavigationLogo;
    private Container topNavigationProfile;
    private SongSelectDifficultyFilterBar difficultyFilterBar;
    private GameplayPlaybackRateOverlay playbackRateOverlay;
    private GameplayScrollSpeedOverlay scrollSpeedOverlay;

    private List<SongSelectEntry> visibleEntries;
    private List<SongSelectEntry> navigableEntries = [];
    private SongSelectEntry selectedEntry;
    private CancellationTokenSource filterCancellation;
    private Task<SongSelectFilterResult> filterTask;
    private int filterGeneration;
    private bool filterDisposed;
    private bool filterPending;
    private readonly HashSet<string> materialisedExternalCharts =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Action<bool>> playableBeatmapCallbacks = [];
    private CancellationTokenSource playableBeatmapCancellation;
    private Task<YokkoBeatmap> playableBeatmapTask;
    private TaskCompletionSource<bool> playableBeatmapStartSignal;
    private string playableBeatmapChartId;
    private int playableBeatmapGeneration;
    private bool playableBeatmapDisposed;
    private KeyMode? keyModeFilter;
    private string searchQuery = string.Empty;
    private SongSelectScoreView scoreView = SongSelectScoreView.Personal;
    private ManiaModSet selectedMods = ManiaModSet.Empty;
    private bool modPanelOpen;
    private SongSelectSortMode sortMode = SongSelectSortMode.Title;
    private SongSelectSortDirection sortDirection =
        SongSelectSortDirection.Ascending;
    private bool packagesCollapsed;
    private bool focusedPackageExpansion = true;
    private bool showConverts = true;
    private double minimumMsdFilter;
    private double minimumStarFilter;
    private bool previewActive;
    private bool transitionPending;
    private int gameplayPreloadGeneration;
    private CancellationTokenSource gameplayPreloadScheduleCancellation;
    private CancellationTokenSource gameplayPreloadCancellation;
    private Task<GameplaySessionScreen> gameplayPreloadTask;
    private GameplayPreloadKey? gameplayPreloadKey;
    private bool nextPreloadScheduled;
    private int initialArtworkPrewarmCount;
    private bool entryTransitionInProgress;
    private int entryTransitionVersion;
    private readonly List<string> initialArtworkPrewarmPaths = [];
    private bool resumeFromGameplayMods;
    private bool detailsTransitionInProgress;
    private int songListRebuildVersion;
    private int detailsTransitionVersion;
    private long libraryRevision = -1;
    private long libraryStructureRevision = -1;
    private readonly object pendingLibraryChangeLock = new();
    private ImportedChartLibraryChange pendingLibraryChange;
    private volatile bool screenActive;
    private Stopwatch loadStopwatch;
    private double displayedPlaybackRate = 1;
    private string displayedBpm = "0";
    private ManiaMsdResult displayedMsdRating;
    private ManiaStarRatingResult displayedStarRating;
    [Resolved]
    private GameplayScoreStore scoreStore { get; set; }
    [Resolved]
    private ImportedChartLibrary importedChartLibrary { get; set; }
    [Resolved]
    private IRenderer renderer { get; set; }
    [Resolved]
    private YokkoAudioSettings audioSettings { get; set; }
    [Resolved]
    private YokkoGameplaySettings gameplaySettings { get; set; }
    [Resolved]
    private YokkoManiaModPreferences modPreferences { get; set; }
    [Resolved]
    private YokkoDisplaySettings displaySettings { get; set; }
    [Resolved]
    private YokkoDiagnostics diagnostics { get; set; }
    [Resolved]
    private SongSelectArtworkTextureCache artworkTextureCache { get; set; }

    private readonly record struct GameplayPreloadKey(
        int BeatmapIdentity,
        string ChartId,
        string ModsFingerprint,
        string ArtworkPath);

    private const double selection_preload_delay = 120;
    private const double settings_preload_delay = 350;
    private const int filter_debounce_milliseconds = 100;
    private const int playable_beatmap_debounce_milliseconds = 150;

    public SongSelectScreen(
        IAudioEngine previewAudioEngine = null,
        Action requestNextPreload = null)
        : this(previewAudioEngine, requestNextPreload, null)
    {
    }

    internal SongSelectScreen(
        IAudioEngine previewAudioEngine,
        Action requestNextPreload,
        SongSelectSelectionMemory selectionMemory,
        ISongSelectPreviewHost previewHost = null)
    {
        suppliedPreviewAudioEngine = previewAudioEngine;
        this.requestNextPreload = requestNextPreload;
        this.selectionMemory = selectionMemory;
        this.previewHost = previewHost;
        if (selectionMemory != null)
        {
            sortMode = selectionMemory.SortMode;
            sortDirection = selectionMemory.SortDirection;
        }
    }

    internal SongSelectEntry SelectedEntry => selectedEntry;
    internal int VisibleEntryCount => visibleEntries?.Count ?? 0;
    internal int VisibleRowCount => songList?.VisibleEntryCount ?? 0;
    internal int MaterialisedSongListDrawableCount =>
        songList?.MaterialisedDrawableCount ?? 0;
    internal IReadOnlyList<string> MaterialisedCompactPrimaryTexts =>
        songList?.MaterialisedRows
                 .Select(row => row.CompactPrimaryText)
                 .ToArray()
        ?? [];
    internal int SongListRebuildVersion => songListRebuildVersion;
    internal int DetailsTransitionVersion => detailsTransitionVersion;
    internal int DetailsLayerCount =>
        detailsHost?.Children.Count() ?? 0;
    internal float BackgroundCoverageAlpha => Math.Max(
        backgroundA?.Alpha ?? 0,
        backgroundB?.Alpha ?? 0);
    internal float StageAlpha => stage?.Alpha ?? 0;
    internal bool EntryTransitionInProgress => entryTransitionInProgress;
    internal int EntryTransitionVersion => entryTransitionVersion;
    internal bool GameplayPreloadReady =>
        gameplayPreloadTask?.IsCompletedSuccessfully == true
        && gameplayPreloadTask.Result.InitialPresentationReady;
    internal bool IsPreparedForNavigation =>
        (visibleEntries.Count == 0
         || songList?.MaterialisedDrawableCount > 0)
        && initialArtworkPrewarmPaths.All(
            artworkTextureCache.IsUploadComplete);
    internal Vector2 SelectedChartFactsPosition =>
        selectedChartFactsRow?.Position ?? Vector2.Zero;
    internal Vector2 SelectedChartFactsSize =>
        selectedChartFactsRow?.Size ?? Vector2.Zero;
    internal Vector2 SelectedPerformancePosition =>
        selectedPerformanceRow?.Position ?? Vector2.Zero;
    internal Vector2 SelectedPerformanceSize =>
        selectedPerformanceRow?.Size ?? Vector2.Zero;
    internal static Vector2 SelectedDetailsPanelSize =>
        new(details_width, details_panel_height);
    internal static Vector2 SelectedArtworkSize =>
        new(details_artwork_size);
    internal static float SelectedArtworkRotation =>
        selected_artwork_rotation;
    internal static float RankingTop => ranking_top;
    internal long LibraryRevision => libraryRevision;
    internal long LibraryStructureRevision => libraryStructureRevision;
    internal KeyMode? KeyModeFilter => keyModeFilter;
    internal string SearchQuery => searchQuery;
    internal bool NoResultsVisible => noResults?.Alpha > 0.5f;
    internal string NoResultsTitle => noResults?.Title ?? string.Empty;
    internal string NoResultsSummary => noResults?.Summary ?? string.Empty;
    internal bool NoResultsResetVisible =>
        noResults?.ClearButtonVisible ?? false;
    internal Vector2 SearchBoxSize => searchBox?.Size ?? Vector2.Zero;
    internal bool SearchHasFocus => searchBox?.HasFocus == true;
    internal Vector2 KeyFilterButtonSize =>
        keyModeFilterButton?.Size ?? Vector2.Zero;
    internal string KeyFilterButtonValue =>
        keyModeFilterButton?.DisplayedValue ?? string.Empty;
    internal Vector2 DifficultyFilterSize =>
        difficultyFilterBar?.Size ?? Vector2.Zero;
    internal Vector2 BrowseToolbarSize =>
        browseToolbar?.Size ?? Vector2.Zero;
    internal float TopNavigationHeight => topNavigation?.Height ?? 0;
    internal Vector2 TopNavigationLogoPosition =>
        topNavigationLogo?.Position ?? Vector2.Zero;
    internal Vector2 TopNavigationLogoSize =>
        topNavigationLogo?.Size ?? Vector2.Zero;
    internal Vector2 TopNavigationProfileSize =>
        topNavigationProfile?.Size ?? Vector2.Zero;
    internal float SongBrowserTop => songBrowser?.Y ?? 0;
    internal bool ShowConverts => showConverts;
    internal SongSelectSortMode SortMode => sortMode;
    internal SongSelectSortDirection SortDirection => sortDirection;
    internal bool SortPopoverOpen => sortPopover?.IsOpen == true;
    internal bool FiltersPopoverOpen => filtersPopoverOpen;
    internal string FiltersButtonValue =>
        filtersButton?.DisplayedValue ?? string.Empty;
    internal string PlayButtonEyebrow =>
        playButton?.EyebrowText ?? string.Empty;
    internal string PlayButtonAction =>
        playButton?.ActionText ?? string.Empty;
    internal Vector2 ShortcutLegendSize => shortcutLegend?.Size ?? Vector2.Zero;
    internal string SortButtonValue => sortButton?.DisplayedValue ?? string.Empty;
    internal IReadOnlyList<SongSelectEntry> VisibleEntries => visibleEntries;
    internal SongSelectEntry ImportedEntryForTest(string chartId) =>
        chartId != null
        && importedEntries.TryGetValue(chartId, out SongSelectEntry entry)
            ? entry
            : null;
    internal double MinimumDifficultyFilter =>
        displaySettings.DifficultyRatingMode.Value
            == ManiaDifficultyRatingMode.EtternaMsd
            ? minimumMsdFilter
            : minimumStarFilter;
    internal string DifficultyFilterUnit =>
        difficultyFilterBar?.DisplayedUnit ?? string.Empty;
    internal Vector2 FooterToolDockSize =>
        footerToolDock?.Size ?? Vector2.Zero;
    internal Vector2 FooterToolDockPosition =>
        footerToolDock?.Position ?? Vector2.Zero;
    internal Vector2 FooterBackPosition =>
        footerBackButton?.Position ?? Vector2.Zero;
    internal Vector2 AccountCardPosition =>
        accountCard?.Position ?? Vector2.Zero;
    internal Vector2 AccountCardSize =>
        accountCard?.Size ?? Vector2.Zero;
    internal string AccountDisplayName =>
        accountCard?.DisplayName ?? string.Empty;
    internal string AccountLevelText =>
        accountCard?.LevelText ?? string.Empty;
    internal IReadOnlyList<string> AccountMetricLabels =>
        accountCard?.MetricLabels ?? Array.Empty<string>();
    internal IReadOnlyList<string> AccountMetricValues =>
        accountCard?.MetricValues ?? Array.Empty<string>();
    internal int FooterToolShadowCount => footerToolShadows?.Length ?? 0;
    internal static Vector2 FooterToolDockSizeFor(YokkoUiScale scale) =>
        new(scale == YokkoUiScale.Large ? 378 : 462, 82);
    internal static float FooterToolButtonWidthFor(YokkoUiScale scale) =>
        scale == YokkoUiScale.Large ? 126 : 154;
    internal static float FooterToolButtonStepFor(YokkoUiScale scale) =>
        FooterToolButtonWidthFor(scale);
    internal Vector2 RankingPanelSize =>
        rankingPanel?.Size ?? Vector2.Zero;
    internal Vector2 RankingContentSize =>
        rankingPanel?.ContentSize ?? Vector2.Zero;
    internal Vector2 RankingPaperPosition =>
        rankingPanel?.PaperPosition ?? Vector2.Zero;
    internal Vector2 RankingPaperSize =>
        rankingPanel?.PaperSize ?? Vector2.Zero;
    internal int RankingContentLayerCount =>
        rankingPanel?.ContentLayerCount ?? 0;
    internal int RankingContentTransitionVersion =>
        rankingPanel?.ContentTransitionVersion ?? 0;
    internal bool RankingEmptyStateVisible =>
        rankingPanel?.EmptyStateVisible ?? false;
    internal SongSelectScoreView ScoreView => scoreView;
    internal ManiaModSet SelectedMods => selectedMods;
    internal int SelectedModsButtonCount =>
        selectedModsButton?.ActiveModCount ?? 0;
    internal string SelectedModsButtonSummary =>
        selectedModsButton?.Summary ?? string.Empty;
    internal Vector2 SelectedModsButtonPosition =>
        selectedModsButton?.Position ?? Vector2.Zero;
    internal Vector2 SelectedModsButtonSize =>
        selectedModsButton?.Size ?? Vector2.Zero;
    internal double DisplayedPlaybackRate => displayedPlaybackRate;
    internal string DisplayedBpm => displayedBpm;
    internal ManiaMsdResult DisplayedMsdRating =>
        displayedMsdRating;
    internal ManiaStarRatingResult DisplayedStarRating =>
        displayedStarRating;
    internal ManiaDifficultyRatingMode DisplayedDifficultyRatingMode =>
        displaySettings.DifficultyRatingMode.Value;
    internal SongSelectAccuracyChallengeSettings
        AccuracyChallengeSettings =>
            modSettingsHost?.AccuracySettings;
    internal SongSelectPerfectSettings PerfectSettings =>
        modSettingsHost?.PerfectSettings;
    internal SongSelectDifficultyAdjustSettings
        DifficultyAdjustSettings =>
            modSettingsHost?.DifficultySettings;
    internal SongSelectMutedSettings MutedSettings =>
        modSettingsHost?.MutedSettings;
    internal SongSelectFixedRateSettings FixedRateSettings =>
        modSettingsHost?.FixedRateSettings;
    internal SongSelectTimeRampSettings TimeRampSettings =>
        modSettingsHost?.TimeRampSettings;
    internal SongSelectAdaptiveSpeedSettings AdaptiveSpeedSettings =>
        modSettingsHost?.AdaptiveSettings;
    internal SongSelectKeyConversionSettings KeyConversionSettings =>
        modSettingsHost?.KeySettings;
    internal bool IsModPanelOpen => modPanelOpen;
    internal string ModInfoTitle =>
        modInfoTitle?.Text.ToString() ?? string.Empty;
    internal string ModInfoDescription =>
        modInfoDescription?.Text.ToString() ?? string.Empty;
    internal bool LegacyInlineModPanelMaterialised =>
        modSettingsHost != null;
    internal bool ScoreResultVisible => scoreResultHost != null;
    internal SongSelectScore ResultScore => activeScoreResult;
    internal bool ResultReplayAvailable =>
        scoreResultOverlay?.ReplayAvailable == true;
    internal void ActivateResultReplay() =>
        scoreResultOverlay?.TriggerReplay();
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
    internal int IndexedSongListItemCount => songList?.ItemCount ?? 0;
    internal int NavigableEntryCount => navigableEntries.Count;
    internal bool FilterPending => filterPending;
    internal bool UsesFocusedPackageExpansion => focusedPackageExpansion;

    internal void TogglePackage(string packageId)
    {
        if (string.IsNullOrWhiteSpace(packageId))
            return;

        bool expanding = collapsedPackages.Remove(packageId);
        if (!expanding)
            collapsedPackages.Add(packageId);
        else if (focusedPackageExpansion || packagesCollapsed)
        {
            enterFocusedPackageExpansion(packageId);
            SongSelectEntry packageSelection = selectedEntry?.IsPackage == true
                                               && string.Equals(
                                                   selectedEntry.PackageId,
                                                   packageId,
                                                   StringComparison.OrdinalIgnoreCase)
                ? selectedEntry
                : visibleEntries.FirstOrDefault(entry =>
                    entry.IsPackage
                    && string.Equals(
                        entry.PackageId,
                        packageId,
                        StringComparison.OrdinalIgnoreCase));
            if (packageSelection != null)
                select(packageSelection, false);
        }

        // 展开/折叠只影响列表排布：不走 applyFilters（会把折叠进去的选中顶
        // 替换成第一首歌），也不重播整列表的入场动画、不把滚动条拽回选中行。
        // 只就地重建，并让被点的图包头保持在视野里。
        rebuildSongList(
            animate: false,
            animateLayout: true,
            transitionPackageId: packageId);

        songList?.ScrollPackageToTop(packageId, true);
    }

    [BackgroundDependencyLoader]
    private void load(TextureStore textureStore)
    {
        loadStopwatch = Stopwatch.StartNew();
        textures = textureStore;
        selectedMods =
            modPreferences?.RestoreActiveMods() ?? ManiaModSet.Empty;
        previewPlayer = new SongSelectPreviewPlayer(
            previewHost?.AudioEngine
            ?? suppliedPreviewAudioEngine
            ?? AudioEngineFactory.CreateDefault(),
            audioSettings,
            ownsAudioEngine: previewHost == null);
        // Subscribe before taking the initial snapshot. The startup disk scan
        // runs in the background and can finish while this screen is loading;
        // reading first would leave a small window where its completion event
        // is missed and the first song-select screen stays empty.
        importedChartLibrary.LibraryChanged += onChartLibraryChanged;
        synchroniseImportedCharts();
        logLoadStage("library snapshot");
        refreshSavedScores();
        selectedEntry = rememberedEntryOrDefault();
        rememberSelectedEntry();
        visibleEntries = entries.ToList();
        focusPackageExpansion(selectedEntry?.IsPackage == true
            ? selectedEntry.PackageId
            : null);

        Texture firstWallpaper = textureFor(selectedEntry);
        Texture logo = textures.Get(
            "SongSelect/Ui/home-logo-light-512");

        InternalChildren = new Drawable[]
        {
            backgroundA = createBackground(firstWallpaper),
            backgroundB = createBackground(textureFor(selectedEntry)),
            createBackgroundIsolation(),
            createBackgroundMoodWash(),
            stage = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    createHeader(logo),
                    detailsHost = new Container
                    {
                        Position = new Vector2(details_left, details_top),
                        Size = new Vector2(
                            details_width,
                            designed_height - footer_height - details_top - 18),
                    },
                    createSongBrowser(),
                    createSortPopover(),
                    createFiltersPopover(),
                    createFooter(),
                    playbackRateOverlay = new GameplayPlaybackRateOverlay
                    {
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                        Y = 92,
                    },
                    scrollSpeedOverlay = new GameplayScrollSpeedOverlay
                    {
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                        Y = 92,
                    },
                },
            },
        };
        logLoadStage("static UI");

        backgroundB.Alpha = 0;
        activeBackground = backgroundA;
        rebuildDetails();
        logLoadStage("selected details");
        refreshDifficultyFilterBar();
        applyFilters(immediate: true);
        logLoadStage("song rows");
        prewarmInitialArtwork();
        logLoadStage("first-frame artwork");
        updateFilters();
        displaySettings.DifficultyRatingMode.BindValueChanged(
            onDifficultyRatingModeChanged);
        displaySettings.UiScale.BindValueChanged(
            onUiScaleChanged,
            true);

        stage.Alpha = 1;
        stage.Y = 0;

        Logger.Log(
            $"Song select construction: {loadStopwatch.Elapsed.TotalMilliseconds:0} ms "
            + $"({entries.Count} charts, {songList?.ItemCount ?? 0} indexed rows, "
            + $"{songList?.MaterialisedDrawableCount ?? 0} materialised, "
            + $"{initialArtworkPrewarmCount} artwork prewarmed).",
            LoggingTarget.Runtime,
            LogLevel.Important);
        diagnostics.Trace(
            "SONG_SELECT",
            "constructed",
            $"entries={entries.Count} | rows={songList?.ItemCount ?? 0}"
            + $" | selected={selectedEntry?.Beatmap.Title ?? "none"}"
            + $" | mods={string.Join(',', selectedMods.DisplayLabels)}");
    }

    private void logLoadStage(string stage) => Logger.Log(
        $"Song select stage {stage}: {loadStopwatch.Elapsed.TotalMilliseconds:0} ms.",
        LoggingTarget.Runtime,
        LogLevel.Important);

    internal void PrepareForNavigation()
    {
        songList.PrepareVisibleRangeForNavigation(browse_height);
        Logger.Log(
            $"Song select preload materialised {songList.MaterialisedDrawableCount} first-frame rows.",
            LoggingTarget.Runtime,
            LogLevel.Important);
    }

    public override void OnEntering(ScreenTransitionEvent e)
    {
        base.OnEntering(e);
        playButton?.SetReady();
        screenActive = true;
        applyPendingLibraryChange();
        restoreRememberedSelection();
        requestPlayableBeatmap(selectedEntry);
        previewActive = true;
        diagnostics.Trace(
            "SONG_SELECT",
            "entered",
            $"selected={selectedEntry?.Beatmap.Title ?? "none"}");
        playSelectedPreview();
        playEntryTransition();
        scheduleGameplayPreload();

        if (!nextPreloadScheduled && requestNextPreload != null)
        {
            nextPreloadScheduled = true;
            Scheduler.AddDelayed(requestNextPreload, 250);
        }
    }

    public override void OnResuming(ScreenTransitionEvent e)
    {
        base.OnResuming(e);
        playButton?.SetReady();
        screenActive = true;
        bool keepExistingSongSelectState = resumeFromGameplayMods;
        resumeFromGameplayMods = false;
        modPanelOpen = false;
        modsToggleButton?.SetOpen(false);
        if (!keepExistingSongSelectState)
        {
            synchroniseImportedCharts(refreshSongList: false);
            if (selectedEntry == null || !entries.Contains(selectedEntry))
                selectedEntry = entries.FirstOrDefault();
            refreshSavedScores(selectedEntry);
            requestPlayableBeatmap(selectedEntry);
            applyFilters();
            rebuildDetails();
        }
        applyPendingLibraryChange();
        bool scoreResultVisible = scoreResultHost != null;
        previewActive = !scoreResultVisible;
        diagnostics.Trace(
            "SONG_SELECT",
            "resumed",
            $"entries={entries.Count} | visible={visibleEntries.Count}"
            + $" | selected={selectedEntry?.Beatmap.Title ?? "none"}"
            + $" | preserved={keepExistingSongSelectState}");
        if (!scoreResultVisible)
            playSelectedPreview();
        scheduleGameplayPreload();
        this.FadeIn(180, Easing.OutQuint);
    }

    internal void RefreshImportedReplayScores(string chartId)
    {
        if (string.IsNullOrWhiteSpace(chartId))
            return;

        if (!importedEntries.TryGetValue(chartId, out SongSelectEntry target))
        {
            synchroniseImportedCharts(refreshSongList: false);
            if (!importedEntries.TryGetValue(chartId, out target))
                return;
        }

        refreshSavedScores(target);
        if (ReferenceEquals(target, selectedEntry))
        {
            requestPlayableBeatmap(selectedEntry);
            rebuildDetails();
        }
        applyFilters();
    }

    public override void OnSuspending(ScreenTransitionEvent e)
    {
        base.OnSuspending(e);
        screenActive = false;
        resumeFromGameplayMods = e.Next is GameplayModsScreen;
        previewActive = false;
        diagnostics.Trace("SONG_SELECT", "suspended");
        if (!KeepsPreviewPlaying(e.Next))
            previewPlayer?.Stop();
        this.FadeTo(0.35f, 180, Easing.OutQuint);
    }

    public override bool OnExiting(ScreenExitEvent e)
    {
        screenActive = false;
        previewActive = false;
        invalidatePlayableBeatmapRequest();
        diagnostics.Trace("SONG_SELECT", "exiting");
        if (previewHost == null)
            previewPlayer?.Stop();
        else
        {
            previewHost.CompletePreviewHandoff(
                previewPlayer?.WaitForIdleAsync() ?? Task.CompletedTask);
            previewPlayer?.Detach();
        }
        return base.OnExiting(e);
    }

    private void playEntryTransition()
    {
        // Match the motion language used by SettingsScreen: the page surface
        // is present on the navigation frame, primary content settles over
        // 180 ms, and the browser rail follows with the same 28 px / 520 ms
        // OutQuint movement as the settings sidebar. Keeping the surface
        // opaque means there is never an empty ScreenStack frame between pages.
        stage.ClearTransforms();
        stage.Alpha = 1;
        stage.Position = Vector2.Zero;
        entryTransitionInProgress = true;
        entryTransitionVersion++;
        Scheduler.AddDelayed(() => entryTransitionInProgress = false, 520);

        header.ClearTransforms();
        header.Y = -10;
        header.Alpha = 0;
        header.MoveToY(0, 180, Easing.OutQuint)
              .FadeIn(180, Easing.OutQuint);

        detailsHost.ClearTransforms();
        detailsHost.X = details_left + 10;
        detailsHost.Alpha = 0;
        detailsHost.MoveToX(details_left, 180, Easing.OutQuint)
                   .FadeIn(180, Easing.OutQuint);

        songBrowser.ClearTransforms();
        songBrowser.X = -browse_right + 28;
        songBrowser.Alpha = 0;
        songBrowser.MoveToX(-browse_right, 520, Easing.OutQuint)
                   .FadeIn(360, Easing.OutQuint);

        footer.ClearTransforms();
        footer.Y = 10;
        footer.Alpha = 0;
        footer.MoveToY(0, 180, Easing.OutQuint)
              .FadeIn(180, Easing.OutQuint);

    }

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
        {
            if (importedChartLibrary != null)
                importedChartLibrary.LibraryChanged -= onChartLibraryChanged;
            if (displaySettings != null)
            {
                displaySettings.DifficultyRatingMode.ValueChanged -=
                    onDifficultyRatingModeChanged;
                displaySettings.UiScale.ValueChanged -= onUiScaleChanged;
            }

            if (previewPlayer != null)
                _ = previewPlayer.DisposeAsync();

            playableBeatmapDisposed = true;
            invalidatePlayableBeatmapRequest();
            invalidateGameplayPreload();
            filterDisposed = true;
            invalidateFilterRequest();
        }

        base.Dispose(isDisposing);
    }

    protected override bool OnKeyDown(KeyDownEvent e)
    {
        if (scoreResultOverlay != null)
        {
            if (e.Key == Key.Escape)
                closeScoreResult();
            else if (e.Key is Key.Enter or Key.V)
                scoreResultOverlay.TriggerReplay();
            else if (e.Key == Key.R)
                retryScoreResult();

            return true;
        }

        if (HandlePlaybackRateShortcut(e.Key, e.AltPressed))
            return true;
        if (HandleScrollSpeedShortcut(e.Key, e.ControlPressed))
            return true;

        if (HandleBrowseKey(e.Key, e.ControlPressed))
            return true;

        return base.OnKeyDown(e);
    }

    internal bool HandleBrowseKey(Key key, bool controlPressed = false)
    {
        switch (key)
        {
            case Key.Up:
                SelectPrevious();
                return true;

            case Key.Down:
                SelectNext();
                return true;

            case Key.PageUp:
                selectOffset(-page_navigation_step, wrap: false);
                return true;

            case Key.PageDown:
                selectOffset(page_navigation_step, wrap: false);
                return true;

            case Key.Home:
                selectBoundary(first: true);
                return true;

            case Key.End:
                selectBoundary(first: false);
                return true;

            case Key.M:
                ToggleModPanel();
                return true;

            case Key.F when controlPressed:
                GetContainingFocusManager()?.ChangeFocus(searchBox);
                return true;

            case Key.F:
                toggleFiltersPopover();
                return true;

            case Key.Slash:
                GetContainingFocusManager()?.ChangeFocus(searchBox);
                return true;

            case Key.Enter:
                PlaySelected();
                return true;

            case Key.R:
                selectRandomEntry();
                return true;

            case Key.Escape:
                HandleEscape();
                return true;

            default:
                return false;
        }
    }

    internal void SelectNext() => selectOffset(1);

    internal void SelectPrevious() => selectOffset(-1);

    internal void SetKeyModeFilter(KeyMode? mode)
    {
        keyModeFilter = mode;
        diagnostics.Trace(
            "SONG_SELECT",
            "key-filter-changed",
            $"mode={mode?.ToString() ?? "all"}");
        updateFilters();
        applyFilters();
    }

    internal void SetMinimumDifficultyFilter(double value)
    {
        ManiaDifficultyRatingMode mode =
            displaySettings.DifficultyRatingMode.Value;
        double maximum = mode == ManiaDifficultyRatingMode.EtternaMsd
            ? 30
            : 10;
        double step = mode == ManiaDifficultyRatingMode.EtternaMsd
            ? 0.25
            : 0.1;
        double snapped = Math.Clamp(
            Math.Round(value / step) * step,
            0,
            maximum);

        if (mode == ManiaDifficultyRatingMode.EtternaMsd)
            minimumMsdFilter = snapped;
        else
            minimumStarFilter = snapped;

        difficultyFilterBar?.SetState(mode, snapped);
        diagnostics.Trace(
            "SONG_SELECT",
            "difficulty-filter-changed",
            $"mode={mode} | minimum={snapped:0.00}");
        applyFilters();
    }

    internal void SetSearchQuery(string query)
    {
        string nextQuery = query ?? string.Empty;
        if (searchQuery == nextQuery)
            return;

        searchQuery = nextQuery;
        if (searchBox != null && searchBox.Current.Value != searchQuery)
            searchBox.Current.Value = searchQuery;
        diagnostics.Trace(
            "SONG_SELECT",
            "search-changed",
            $"query={searchQuery}");
        updateFilters();
        applyFilters();
    }

    internal void ClearBrowseFilters()
    {
        searchQuery = string.Empty;
        keyModeFilter = null;
        minimumMsdFilter = 0;
        minimumStarFilter = 0;
        showConverts = true;

        if (searchBox != null && searchBox.Current.Value.Length > 0)
            searchBox.Current.Value = string.Empty;
        updateFilters();
        refreshDifficultyFilterBar();
        convertsButton?.SetValue("SHOWN");
        convertsButton?.SetActive(true);
        diagnostics.Trace(
            "SONG_SELECT",
            "browse-filters-cleared",
            "query= | mode=all | difficulty-min=0 | converts=true");
        applyFilters();
    }

    internal bool DismissSearch()
    {
        bool hasQuery = !string.IsNullOrEmpty(searchQuery)
                        || !string.IsNullOrEmpty(searchBox?.Current.Value);
        if (!hasQuery)
            return false;

        if (searchBox != null && searchBox.Current.Value.Length > 0)
            searchBox.Current.Value = string.Empty;
        else
            SetSearchQuery(string.Empty);

        return true;
    }

    internal void HandleEscape()
    {
        if (scoreResultOverlay != null)
            closeScoreResult();
        else if (sortPopover?.IsOpen == true)
        {
            sortPopover.Close();
            sortButton?.SetActive(false);
        }
        else if (filtersPopoverOpen)
            closeFiltersPopover();
        else if (!DismissSearch())
            stopPreviewThen(this.Exit);
    }

    internal void ToggleScoreView()
    {
        scoreView = SongSelectScoreView.Personal;
    }

    internal void ActivateRankingPanel()
    {
        SongSelectScore first = selectedEntry?.History.FirstOrDefault();
        if (first != null)
            ShowScoreResult(first);
    }

    internal void ActivateSelectedModsButton() =>
        selectedModsButton?.TriggerClick();

    internal void PlaySelected()
    {
        if (selectedEntry == null || transitionPending)
            return;
        if (!isPlayableBeatmapReady(selectedEntry))
        {
            playButton?.SetPreparing("PREPARING EXTERNAL CHART");
            SongSelectEntry requested = selectedEntry;
            requestPlayableBeatmap(requested, success =>
            {
                if (success && ReferenceEquals(selectedEntry, requested))
                {
                    playButton?.SetReady();
                    PlaySelected();
                }
                else if (ReferenceEquals(selectedEntry, requested))
                    playButton?.SetError();
            }, immediate: true);
            return;
        }

        ManiaModSet gameplayMods = selectedMods;
        YokkoBeatmap gameplayBeatmap = selectedEntry.Beatmap;
        string gameplayArtwork = selectedEntry.WallpaperTexture;
        Texture gameplayArtworkTexture = gameplayArtworkTextureFor(
            selectedEntry);
        GameplayPreloadKey gameplayKey = createGameplayPreloadKey(
            selectedEntry,
            gameplayMods);
        string preloadState = gameplayPreloadState(gameplayKey);
        selectedMods =
            YokkoManiaModPreferences.SelectPersistentActiveMods(
                gameplayMods);
        updateModSelection();
        if (hoveredMod == null)
            showModPanelSummary();

        transitionPending = true;
        diagnostics.Trace(
            "SONG_SELECT",
            "play-requested",
            $"title={gameplayBeatmap.Title} | difficulty={gameplayBeatmap.DifficultyName}"
            + $" | keys={(int)gameplayBeatmap.KeyMode}"
            + $" | mods={string.Join(',', gameplayMods.DisplayLabels)}"
            + $" | preload={preloadState}",
            LogLevel.Important);
        previewActive = false;
        previewPlayer?.Stop();
        stage.FadeTo(0.84f, 90, Easing.OutQuint)
             .ScaleTo(0.997f, 90, Easing.OutQuint);

        Task<GameplaySessionScreen> gameplayTask =
            getOrStartGameplayPreload(
                gameplayKey,
                gameplayBeatmap,
                gameplayMods,
                gameplayArtwork,
                gameplayArtworkTexture);
        _ = finishGameplayTransitionAsync(
            previewPlayer?.WaitForIdleAsync() ?? Task.CompletedTask,
            gameplayTask);
    }

    private async Task finishGameplayTransitionAsync(
        Task previewStopped,
        Task<GameplaySessionScreen> gameplayTask,
        Action<Exception> failure = null)
    {
        try
        {
            GameplaySessionScreen gameplay =
                await gameplayTask.ConfigureAwait(false);
            Task gameplayLoaded = gameplay.InitialGameplayPreloaded
                ? waitForInitialPresentationReadyAsync(
                    gameplay,
                    CancellationToken.None)
                : preloadGameplaySessionAsync(
                    gameplay,
                    CancellationToken.None);
            await Task.WhenAll(previewStopped, gameplayLoaded)
                      .ConfigureAwait(false);
            releaseGameplayPreloadOwnership(gameplayTask);
            Scheduler.Add(() =>
            {
                transitionPending = false;
                diagnostics.Trace("SONG_SELECT", "gameplay-ready");
                this.Push(gameplay);
            });
        }
        catch (Exception exception)
        {
            Logger.Error(
                exception,
                "Could not prepare gameplay from song select.",
                LoggingTarget.Runtime);
            diagnostics.Trace(
                "SONG_SELECT",
                "gameplay-prepare-failed",
                exception.ToString(),
                LogLevel.Error);
            Scheduler.Add(() =>
            {
                transitionPending = false;
                playButton?.SetError();
                bool scoreResultVisible = scoreResultHost != null;
                previewActive = !scoreResultVisible;
                stage.FadeTo(1, 120, Easing.OutQuint)
                     .ScaleTo(1, 120, Easing.OutQuint);
                if (!scoreResultVisible)
                    playSelectedPreview();
                failure?.Invoke(exception.GetBaseException());
            });
        }
    }

    private async Task preloadGameplaySessionAsync(
        GameplaySessionScreen gameplay,
        CancellationToken cancellationToken)
    {
        var loadScheduled = new TaskCompletionSource<Task>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using CancellationTokenRegistration registration =
            cancellationToken.Register(() =>
                loadScheduled.TrySetCanceled(cancellationToken));
        Scheduler.Add(() =>
        {
            try
            {
                loadScheduled.TrySetResult(
                    LoadComponentAsync(
                        gameplay,
                        _ => { },
                        cancellationToken));
            }
            catch (Exception exception)
            {
                loadScheduled.TrySetException(exception);
            }
        });

        Task loadTask = await loadScheduled.Task.ConfigureAwait(false);
        await loadTask.ConfigureAwait(false);
        await waitForInitialPresentationReadyAsync(
            gameplay,
            cancellationToken).ConfigureAwait(false);
    }

    private void scheduleGameplayPreload(
        double delayMilliseconds = selection_preload_delay)
    {
        if (transitionPending || selectedEntry == null)
            return;

        if (!isPlayableBeatmapReady(selectedEntry))
        {
            SongSelectEntry requested = selectedEntry;
            requestPlayableBeatmap(requested, success =>
            {
                if (success && ReferenceEquals(selectedEntry, requested))
                    scheduleGameplayPreload(delayMilliseconds);
            });
            return;
        }

        SongSelectEntry entry = selectedEntry;
        ManiaModSet mods = selectedMods;
        GameplayPreloadKey key = createGameplayPreloadKey(entry, mods);
        if (gameplayPreloadKey == key
            && gameplayPreloadTask is { IsCanceled: false, IsFaulted: false })
        {
            return;
        }

        invalidateGameplayPreload();
        int generation = gameplayPreloadGeneration;
        gameplayPreloadScheduleCancellation = new CancellationTokenSource();
        _ = startGameplayPreloadAfterDelayAsync(
            entry,
            mods,
            key,
            generation,
            delayMilliseconds,
            gameplayPreloadScheduleCancellation);
    }

    private async Task startGameplayPreloadAfterDelayAsync(
        SongSelectEntry entry,
        ManiaModSet mods,
        GameplayPreloadKey key,
        int generation,
        double delayMilliseconds,
        CancellationTokenSource scheduleCancellation)
    {
        try
        {
            await Task.Delay(
                TimeSpan.FromMilliseconds(delayMilliseconds),
                scheduleCancellation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        Scheduler.Add(() =>
        {
            if (scheduleCancellation.IsCancellationRequested
                || generation != gameplayPreloadGeneration
                || transitionPending
                || selectedEntry == null
                || createGameplayPreloadKey(selectedEntry, selectedMods) != key)
            {
                return;
            }

            if (ReferenceEquals(
                    gameplayPreloadScheduleCancellation,
                    scheduleCancellation))
            {
                gameplayPreloadScheduleCancellation = null;
            }
            scheduleCancellation.Dispose();
            _ = getOrStartGameplayPreload(
                key,
                entry.Beatmap,
                mods,
                entry.WallpaperTexture,
                gameplayArtworkTextureFor(entry));
        });
    }

    private Task<GameplaySessionScreen> getOrStartGameplayPreload(
        GameplayPreloadKey key,
        YokkoBeatmap beatmap,
        ManiaModSet mods,
        string artworkPath,
        Texture artworkTexture)
    {
        if (gameplayPreloadKey == key
            && gameplayPreloadTask is { IsCanceled: false, IsFaulted: false })
        {
            return gameplayPreloadTask;
        }

        invalidateGameplayPreload();
        gameplayPreloadKey = key;
        gameplayPreloadCancellation = new CancellationTokenSource();
        CancellationToken cancellationToken =
            gameplayPreloadCancellation.Token;
        gameplayPreloadTask = prepareGameplaySessionAsync(
            beatmap,
            mods,
            artworkPath,
            artworkTexture,
            cancellationToken);
        _ = observeGameplayPreloadAsync(gameplayPreloadTask, key);
        return gameplayPreloadTask;
    }

    private async Task<GameplaySessionScreen> prepareGameplaySessionAsync(
        YokkoBeatmap beatmap,
        ManiaModSet mods,
        string artworkPath,
        Texture artworkTexture,
        CancellationToken cancellationToken)
    {
        GameplaySessionScreen gameplay = null;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            gameplay = await Task.Run(
                () => new GameplaySessionScreen(new GameplayScreen(
                    beatmap,
                    mods: mods,
                    artworkPath: artworkPath,
                    preparedArtworkTexture: artworkTexture)),
                cancellationToken).ConfigureAwait(false);
            await preloadGameplaySessionAsync(
                gameplay,
                cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            diagnostics.Trace(
                "SONG_SELECT",
                "gameplay-preload-ready",
                $"title={beatmap.Title}"
                + $" | elapsed={stopwatch.Elapsed.TotalMilliseconds:0.###}ms"
                + $" | pending-textures={gameplay.PendingInitialTextureUploads}");
            return gameplay;
        }
        catch
        {
            gameplay?.Dispose();
            throw;
        }
    }

    private Task waitForInitialPresentationReadyAsync(
        GameplaySessionScreen gameplay,
        CancellationToken cancellationToken)
    {
        if (gameplay.InitialPresentationReady)
            return Task.CompletedTask;

        var completion = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationTokenRegistration registration =
            cancellationToken.Register(() =>
                completion.TrySetCanceled(cancellationToken));
        Action check = null;
        check = () =>
        {
            if (completion.Task.IsCompleted)
                return;

            if (gameplay.InitialPresentationReady)
            {
                completion.TrySetResult(true);
                return;
            }

            Scheduler.AddDelayed(check, 1);
        };
        Scheduler.Add(check);
        return completeAndDisposeRegistrationAsync(completion.Task, registration);
    }

    private static async Task completeAndDisposeRegistrationAsync(
        Task task,
        CancellationTokenRegistration registration)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        finally
        {
            registration.Dispose();
        }
    }

    private async Task observeGameplayPreloadAsync(
        Task<GameplaySessionScreen> preloadTask,
        GameplayPreloadKey key)
    {
        try
        {
            await preloadTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            diagnostics.Trace(
                "SONG_SELECT",
                "gameplay-preload-failed",
                $"chart={key.ChartId} | {exception.GetBaseException().Message}");
        }
    }

    private void invalidateGameplayPreload()
    {
        gameplayPreloadGeneration++;
        Task<GameplaySessionScreen> staleTask = gameplayPreloadTask;
        gameplayPreloadTask = null;
        gameplayPreloadKey = null;
        gameplayPreloadScheduleCancellation?.Cancel();
        gameplayPreloadScheduleCancellation?.Dispose();
        gameplayPreloadScheduleCancellation = null;
        gameplayPreloadCancellation?.Cancel();
        gameplayPreloadCancellation?.Dispose();
        gameplayPreloadCancellation = null;

        if (staleTask != null)
            _ = disposeStaleGameplayPreloadAsync(staleTask);
    }

    private async Task disposeStaleGameplayPreloadAsync(
        Task<GameplaySessionScreen> staleTask)
    {
        try
        {
            GameplaySessionScreen stale =
                await staleTask.ConfigureAwait(false);
            Scheduler.Add(stale.Dispose);
        }
        catch
        {
        }
    }

    private void releaseGameplayPreloadOwnership(
        Task<GameplaySessionScreen> gameplayTask)
    {
        if (!ReferenceEquals(gameplayPreloadTask, gameplayTask))
            return;

        gameplayPreloadGeneration++;
        gameplayPreloadTask = null;
        gameplayPreloadKey = null;
        gameplayPreloadCancellation?.Dispose();
        gameplayPreloadCancellation = null;
    }

    private static GameplayPreloadKey createGameplayPreloadKey(
        SongSelectEntry entry,
        ManiaModSet mods) =>
        new(
            RuntimeHelpers.GetHashCode(entry.Beatmap),
            entry.ChartId ?? string.Empty,
            mods.Fingerprint,
            entry.WallpaperTexture ?? string.Empty);

    private string gameplayPreloadState(GameplayPreloadKey key)
    {
        if (gameplayPreloadKey != key || gameplayPreloadTask == null)
            return "miss";
        if (gameplayPreloadTask.IsCompletedSuccessfully)
        {
            return gameplayPreloadTask.Result.InitialPresentationReady
                ? "ready"
                : "cpu-ready";
        }

        return gameplayPreloadTask.IsFaulted
            || gameplayPreloadTask.IsCanceled
                ? "miss"
                : "warming";
    }

    internal void ShowScoreResult(SongSelectScore score)
    {
        if (score == null || selectedEntry == null || transitionPending)
            return;

        closeScoreResultImmediately(restartPreview: false);
        var result = new ManiaScoreResult(
            score.Score,
            score.Accuracy,
            score.MaxCombo,
            score.Grade,
            score.Perfect,
            score.Great,
            score.Good,
            score.Ok,
            score.Meh,
            score.Miss,
            score.ComboBreaks,
            score.MaxMissCombo);
        bool replayAvailable =
            !string.IsNullOrWhiteSpace(score.ReplayPath)
            && File.Exists(score.ReplayPath);
        activeScoreResult = score;
        scoreResultOverlay = new GameplayResultOverlay(
            selectedEntry.Beatmap,
            result,
            score.ModSet ?? ManiaModSet.Empty,
            isNewBest: false,
            retry: retryScoreResult,
            watchReplay: () => watchScoreReplay(score),
            returnToSongSelect: closeScoreResult,
            practiceSession: false,
            judgementConfiguration: score.JudgementConfiguration
                ?? gameplaySettings.GetJudgementConfiguration(),
            replayAvailable: replayAvailable);
        scoreResultHost = new ScoreResultInputBlocker
        {
            RelativeSizeAxes = Axes.Both,
            Depth = float.MinValue,
            Child = scoreResultOverlay,
        };
        stage.Add(scoreResultHost);
        previewActive = false;
        previewPlayer?.Stop();
    }

    private void retryScoreResult()
    {
        if (activeScoreResult == null
            || selectedEntry == null
            || transitionPending)
        {
            return;
        }

        if (!isPlayableBeatmapReady(selectedEntry))
        {
            SongSelectEntry requested = selectedEntry;
            requestPlayableBeatmap(requested, success =>
            {
                if (success && ReferenceEquals(selectedEntry, requested))
                    retryScoreResult();
            }, immediate: true);
            return;
        }

        ManiaModSet retryMods = activeScoreResult.ModSet
                                ?? ManiaModSet.Empty;
        closeScoreResultImmediately(restartPreview: false);
        selectedMods = retryMods;
        updateModSelection();
        PlaySelected();
    }

    private void closeScoreResult()
    {
        ScoreResultInputBlocker host = scoreResultHost;
        if (host == null)
            return;

        scoreResultHost = null;
        scoreResultOverlay = null;
        activeScoreResult = null;
        host.ClearTransforms();
        host.FadeOut(140, Easing.InQuad);
        Scheduler.AddDelayed(() =>
        {
            if (host.Parent == stage)
                stage.Remove(host, true);
        }, 145);
        previewActive = true;
        playSelectedPreview();
    }

    private void closeScoreResultImmediately(bool restartPreview)
    {
        ScoreResultInputBlocker host = scoreResultHost;
        scoreResultHost = null;
        scoreResultOverlay = null;
        activeScoreResult = null;
        if (host?.Parent == stage)
            stage.Remove(host, true);
        if (!restartPreview)
            return;

        previewActive = true;
        playSelectedPreview();
    }

    private void watchScoreReplay(SongSelectScore score)
    {
        if (score == null
            || string.IsNullOrWhiteSpace(score.ReplayPath)
            || selectedEntry == null
            || transitionPending)
        {
            return;
        }

        YokkoBeatmap replayBeatmap = selectedEntry.Beatmap;
        string replayPath = score.ReplayPath;
        transitionPending = true;
        playButton?.SetPreparing("STARTING GAMEPLAY");
        previewActive = false;
        previewPlayer?.Stop();
        stage.FadeTo(0.84f, 90, Easing.OutQuint)
             .ScaleTo(0.997f, 90, Easing.OutQuint);

        Task<GameplaySessionScreen> gameplayTask = Task.Run(() =>
        {
            YokkoReplayLoadResult loaded =
                YokkoReplayIO.ReadFromFile(replayPath);
            string expectedFingerprint =
                YokkoBeatmapFingerprint.Compute(replayBeatmap);
            if (!string.Equals(
                    loaded.BeatmapFingerprint,
                    expectedFingerprint,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    "This replay belongs to a different chart.");
            }

            YokkoBeatmap applied = ManiaBeatmapModTransformer.Apply(
                replayBeatmap,
                loaded.Replay.Mods);
            if ((int)applied.KeyMode != loaded.KeyCount)
            {
                throw new InvalidDataException(
                    "The replay key count does not match this chart.");
            }

            return new GameplaySessionScreen(new GameplayScreen(
                replayBeatmap,
                null,
                null,
                null,
                loaded.Replay));
        });
        _ = finishGameplayTransitionAsync(
            previewPlayer?.WaitForIdleAsync() ?? Task.CompletedTask,
            gameplayTask,
            _ => scoreResultOverlay?.SetReplayAvailable(false));
    }

    private void stopPreviewThen(Action transition)
    {
        if (transitionPending)
            return;

        transitionPending = true;
        previewActive = false;
        previewPlayer?.Stop();
        _ = finishTransitionAsync(
            previewPlayer?.WaitForIdleAsync() ?? Task.CompletedTask,
            transition);
    }

    private async Task finishTransitionAsync(
        Task previewStopped,
        Action transition)
    {
        await previewStopped.ConfigureAwait(false);
        Scheduler.Add(() =>
        {
            transitionPending = false;
            transition();
        });
    }

    internal void ToggleMod(ManiaModId mod)
    {
        ManiaModSet previous = selectedMods;
        modPreferences?.Remember(previous);
        bool enabled = !selectedMods.Contains(mod);
        selectedMods = mod == ManiaModId.Random && enabled
            ? selectedMods.WithRandomSeed(Random.Shared.Next())
            : selectedMods.With(mod, enabled);
        if (enabled)
            selectedMods = modPreferences?.Apply(
                selectedMods,
                mod) ?? selectedMods;
        if (enabled
            && (mod is ManiaModId.AccuracyChallenge
                or ManiaModId.Perfect
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
                || mod is >= ManiaModId.Key1
                    and <= ManiaModId.Key10))
        {
            modSettingsHost?.Show(mod);
        }
        onSelectedModsChanged();
        showModFeedback(mod, enabled, previous);
    }

    private void showModFeedback(
        ManiaModId mod,
        bool enabled,
        ManiaModSet previous)
    {
        if (modInfoTitle == null || modInfoDescription == null)
            return;

        ManiaModDefinition definition =
            OsuManiaModParityCatalog.Get(mod);
        ManiaModId[] replaced = previous.Mods
            .Where(previousMod =>
                previousMod != mod
                && !selectedMods.Contains(previousMod))
            .ToArray();

        modInfoTitle.Text = enabled
            ? $"{definition.Acronym} · {definition.Name.ToUpperInvariant()} ACTIVE"
            : $"{definition.Acronym} · {definition.Name.ToUpperInvariant()} REMOVED";
        modInfoTitle.Colour = enabled
            ? SongSelectTheme.Yellow
            : SongSelectTheme.PaleCyan;
        modInfoDescription.Text = replaced.Length > 0
            ? $"Replaced {string.Join(", ", replaced.Select(replacedMod => OsuManiaModParityCatalog.Get(replacedMod).Acronym))} · {definition.Description}"
            : definition.Description;
    }

    private void onModHoverChanged(ManiaModId mod, bool hovered)
    {
        if (hovered)
        {
            hoveredMod = mod;
            ManiaModDefinition definition =
                OsuManiaModParityCatalog.Get(mod);
            modInfoTitle.Text =
                $"{definition.Acronym} · {definition.Name.ToUpperInvariant()}";
            modInfoTitle.Colour = SongSelectTheme.Ivory;
            modInfoDescription.Text = definition.Description;
        }
        else if (hoveredMod == mod)
        {
            hoveredMod = null;
            showModPanelSummary();
        }
    }

    private void showModPanelSummary()
    {
        if (modInfoTitle == null || modInfoDescription == null)
            return;

        int count = selectedMods.Mods.Count;
        modInfoTitle.Text = count == 0
            ? "GAMEPLAY MODS"
            : $"GAMEPLAY MODS · {count} ACTIVE";
        modInfoTitle.Colour = SongSelectTheme.Ivory;
        modInfoDescription.Text = count == 0
            ? "Hover a mod to see what it changes."
            : string.Join(
                "  ",
                selectedMods.DisplayLabels.Take(8));
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

    internal void SetPerfectRequirePerfectHits(bool value)
    {
        selectedMods = selectedMods.WithPerfect(value);
        onSelectedModsChanged();
    }

    internal void SetFixedRateSpeedChange(double value)
    {
        if (selectedMods.FixedRateMod is not ManiaModId mod)
            return;

        selectedMods = selectedMods.WithFixedRate(
            mod,
            value,
            selectedMods.FixedRateAdjustPitch);
        onSelectedModsChanged();
    }

    internal void SetFixedRateAdjustPitch(bool value)
    {
        if (selectedMods.FixedRateMod is not ManiaModId mod)
            return;

        selectedMods = selectedMods.WithFixedRate(
            mod,
            selectedMods.FixedRateSpeedChange,
            value);
        onSelectedModsChanged();
    }

    internal bool HandlePlaybackRateShortcut(
        Key key,
        bool altPressed)
    {
        if (!altPressed || selectedEntry == null)
            return false;

        double amount = key switch
        {
            Key.Plus or Key.KeypadPlus =>
                playbackRateShortcutStep,
            Key.Minus or Key.KeypadMinus =>
                -playbackRateShortcutStep,
            _ => 0,
        };
        if (amount == 0)
            return false;

        double currentRate = selectedMods.PlaybackRate;
        double nextRate = AdjustPlaybackRate(
            currentRate,
            amount);
        if (Math.Abs(nextRate - currentRate) < 0.000001)
        {
            showPlaybackRateHint();
            return true;
        }

        ManiaModId? currentMod = selectedMods.FixedRateMod;

        if (Math.Abs(nextRate - 1) < 0.000001)
        {
            if (currentMod.HasValue)
                selectedMods = selectedMods.With(
                    currentMod.Value,
                    false);
        }
        else
        {
            ManiaModId nextMod = fixedRateModFor(
                nextRate,
                currentMod);
            bool keepPitchAdjustment =
                currentMod == nextMod
                && selectedMods.FixedRateAdjustPitch;
            selectedMods = selectedMods.WithFixedRate(
                nextMod,
                nextRate,
                keepPitchAdjustment);
        }

        onPlaybackRateShortcutChanged();
        return true;
    }

    internal static double AdjustPlaybackRate(
        double playbackRate,
        double amount)
    {
        if (!double.IsFinite(playbackRate)
            || !double.IsFinite(amount))
        {
            throw new ArgumentOutOfRangeException(
                nameof(playbackRate));
        }

        return Math.Clamp(
            Math.Round(
                playbackRate + amount,
                2,
                MidpointRounding.AwayFromZero),
            minimumPlaybackRate,
            maximumPlaybackRate);
    }

    internal bool HandleScrollSpeedShortcut(
        Key key,
        bool controlPressed)
    {
        double direction = key switch
        {
            _ when key == gameplaySettings.IncreaseScrollSpeedKey.Value => 1,
            _ when key == gameplaySettings.DecreaseScrollSpeedKey.Value => -1,
            Key.Plus or Key.KeypadPlus when controlPressed => 1,
            Key.Minus or Key.KeypadMinus when controlPressed => -1,
            _ => 0,
        };
        if (direction == 0)
            return false;

        if (gameplaySettings.ScrollSpeedAdjustmentMode.Value
            == ScrollSpeedAdjustmentMode.Milliseconds)
        {
            gameplaySettings.AdjustScrollTimeMilliseconds(
                -direction
                * OsuManiaScrollSpeed.ScrollTimeStepMilliseconds);
        }
        else
        {
            gameplaySettings.AdjustScrollSpeed(
                direction * OsuManiaScrollSpeed.ShortcutStep);
        }

        showScrollSpeedHint();
        return true;
    }

    private static ManiaModId fixedRateModFor(
        double playbackRate,
        ManiaModId? currentMod)
    {
        bool slow = playbackRate < 1;
        bool sameFamily = slow
            ? currentMod is ManiaModId.HalfTime
                or ManiaModId.Daycore
            : currentMod is ManiaModId.DoubleTime
                or ManiaModId.Nightcore;
        if (sameFamily)
            return currentMod.Value;

        return slow
            ? ManiaModId.HalfTime
            : ManiaModId.DoubleTime;
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

    internal void SetCoverCoverage(double value)
    {
        selectedMods = selectedMods.WithCover(
            value,
            selectedMods.CoverDirection);
        onSelectedModsChanged();
    }

    internal void SetCoverDirection(ManiaCoverDirection value)
    {
        selectedMods = selectedMods.WithCover(
            selectedMods.CoverCoverage,
            value);
        onSelectedModsChanged();
    }

    internal void SetFlashlightSizeMultiplier(double value)
    {
        selectedMods = selectedMods.WithFlashlight(
            value,
            selectedMods.FlashlightComboBasedSize);
        onSelectedModsChanged();
    }

    internal void SetFlashlightComboBasedSize(bool value)
    {
        selectedMods = selectedMods.WithFlashlight(
            selectedMods.FlashlightSizeMultiplier,
            value);
        onSelectedModsChanged();
    }

    internal void SetRandomSeed(int value)
    {
        if (!selectedMods.Contains(ManiaModId.Random))
            return;

        selectedMods = selectedMods.WithRandomSeed(value);
        onSelectedModsChanged();
    }

    internal void SetNoPauseAllowedPauses(int value)
    {
        if (!selectedMods.Contains(ManiaModId.NoPause))
            return;

        selectedMods = selectedMods.WithNoPause(value);
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
        diagnostics.Trace(
            "SONG_SELECT",
            "mods-changed",
            $"mods={string.Join(',', selectedMods.DisplayLabels)}"
            + $" | rate={selectedMods.PlaybackRate:0.###}x");
        modPreferences?.Remember(selectedMods);
        modPreferences?.RememberActiveMods(selectedMods);
        updateModSelection();
        playSelectedPreview();
        if (hoveredMod == null)
            showModPanelSummary();
        refreshSavedScores();
        rebuildDetails();
        refreshSongListDifficulties();
        scheduleGameplayPreload(settings_preload_delay);
    }

    private void showPlaybackRateHint()
    {
        if (selectedEntry == null || playbackRateOverlay == null)
            return;

        scrollSpeedOverlay?.Hide();
        playbackRateOverlay.Show(
            selectedMods.PlaybackRate,
            selectedEntry.Bpm * selectedMods.PlaybackRate,
            difficultyRatingsFor(selectedEntry),
            displaySettings.DifficultyRatingMode.Value);
    }

    private void showScrollSpeedHint()
    {
        if (scrollSpeedOverlay == null)
            return;

        playbackRateOverlay?.Hide();
        double speed = gameplaySettings.ScrollSpeed.Value;
        scrollSpeedOverlay.Show(
            speed,
            (int)Math.Round(OsuManiaScrollSpeed.ComputeScrollTime(speed)),
            gameplaySettings.ScrollSpeedAdjustmentMode.Value
                == ScrollSpeedAdjustmentMode.Milliseconds);
    }

    private void onPlaybackRateShortcutChanged()
    {
        modPreferences?.Remember(selectedMods);
        modPreferences?.RememberActiveMods(selectedMods);
        updateModSelection();
        if (previewActive
            && previewPlayer?.TryUpdatePlaybackRate(
                selectedEntry?.Beatmap,
                selectedMods) != true)
        {
            playSelectedPreview();
        }
        if (hoveredMod == null)
            showModPanelSummary();
        rebuildDetails();
        refreshSongListDifficulties();
        showPlaybackRateHint();
        scheduleGameplayPreload(settings_preload_delay);
    }

    internal void ToggleModPanel()
    {
        if (modPanelOpen || selectedEntry == null)
            return;
        if (!isPlayableBeatmapReady(selectedEntry))
        {
            SongSelectEntry requested = selectedEntry;
            requestPlayableBeatmap(requested, success =>
            {
                if (success && ReferenceEquals(selectedEntry, requested))
                    ToggleModPanel();
            }, immediate: true);
            return;
        }

        modPanelOpen = true;
        modsToggleButton?.SetOpen(true);
        this.Push(new GameplayModsScreen(
            selectedEntry.Beatmap,
            selectedMods,
            applyModsFromPage));
    }

    internal static bool KeepsPreviewPlaying(IScreen next) =>
        next is GameplayModsScreen;

    private void applyModsFromPage(ManiaModSet mods)
    {
        selectedMods = mods ?? ManiaModSet.Empty;
        onSelectedModsChanged();
    }

    private Drawable createHeader(Texture logo) => header = new Container
    {
        RelativeSizeAxes = Axes.Both,
        Children =
        [
            createTopNavigation(logo),
            searchBox = new SongSelectSearchBox(
                SetSearchQuery,
                HandleEscape)
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                Position = new Vector2(-164, 82),
                Size = new Vector2(816, 48),
            },
            keyModeFilterButton = new SongSelectKeyModeFilterButton(
                cycleKeyModeFilter)
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                Position = new Vector2(-browse_right, 82),
            },
            difficultyFilterBar = new SongSelectDifficultyFilterBar(
                SetMinimumDifficultyFilter)
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                Position = new Vector2(-460, 136),
            },
            createBrowseToolbar(),
        ],
    };

    private Drawable createTopNavigation(Texture logo) => topNavigation = new Container
    {
        RelativeSizeAxes = Axes.X,
        Height = 72,
        Children =
        [
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = SongSelectTheme.Navy,
            },
            topNavigationLogo = new Sprite
            {
                Position = new Vector2(24, 7),
                Size = new Vector2(168, 57),
                Texture = logo,
                FillMode = FillMode.Fit,
            },
            createTopNavigationSeparator(204),
            createTopNavigationIcon(FontAwesome.Solid.Music, 220, true),
            createTopNavigationIcon(FontAwesome.Regular.Star, 274),
            createTopNavigationIcon(FontAwesome.Solid.PencilAlt, 328),
            createTopNavigationIcon(FontAwesome.Solid.Users, 382),
            createTopNavigationIcon(FontAwesome.Solid.Crosshairs, 436),
            createTopNavigationSeparator(802),
            createTopNavigationIcon(FontAwesome.Solid.Laptop, 824),
            createTopNavigationIcon(FontAwesome.Solid.Desktop, 876),
            createTopNavigationIcon(FontAwesome.Solid.Headphones, 928),
            createTopNavigationIcon(FontAwesome.Solid.Trophy, 980),
            createTopNavigationIcon(FontAwesome.Regular.Comment, 1032),
            createTopNavigationIcon(FontAwesome.Solid.Globe, 1084),
            createTopNavigationSeparator(1148),
            topNavigationProfile = createTopNavigationProfile(),
            createTopNavigationNotification(),
            new Box
            {
                Anchor = Anchor.BottomLeft,
                Origin = Anchor.BottomLeft,
                RelativeSizeAxes = Axes.X,
                Height = 1,
                Colour = SongSelectTheme.Cyan,
                Alpha = 0.42f,
            },
        ],
    };

    private static Drawable createTopNavigationSeparator(float x) => new Box
    {
        Position = new Vector2(x, 17),
        Size = new Vector2(1, 38),
        Colour = Color4.White,
        Alpha = 0.18f,
    };

    private Container createTopNavigationProfile() => new Container
    {
        Anchor = Anchor.CentreRight,
        Origin = Anchor.CentreRight,
        X = -72,
        Size = new Vector2(210, 46),
        Masking = true,
        CornerRadius = 23,
        BorderThickness = 1,
        BorderColour = new Color4(1f, 1f, 1f, 0.18f),
        Children =
        [
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = Color4.White,
                Alpha = 0.07f,
            },
            new Circle
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.Centre,
                X = 17,
                Size = new Vector2(6),
                Colour = SongSelectTheme.Cyan,
            },
            new SpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 29,
                Text = demo_profile_name,
                Font = HomeTypography.Display(14),
                Colour = Color4.White,
            },
            new Container
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                X = -4,
                Size = new Vector2(38),
                Masking = true,
                CornerRadius = 19,
                BorderThickness = 2,
                BorderColour = SongSelectTheme.Cyan,
                Child = new Sprite
                {
                    RelativeSizeAxes = Axes.Both,
                    Texture = textures.Get(
                        "SongSelect/Ui/yokko-avatar-256"),
                    FillMode = FillMode.Fill,
                },
            },
        ],
    };

    private static Drawable createTopNavigationNotification() => new Container
    {
        Anchor = Anchor.CentreRight,
        Origin = Anchor.CentreRight,
        X = -22,
        Size = new Vector2(40),
        Children =
        [
            new Circle
            {
                RelativeSizeAxes = Axes.Both,
                Colour = Color4.White,
                Alpha = 0.07f,
            },
            new SpriteIcon
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Size = new Vector2(18),
                Icon = FontAwesome.Regular.Bell,
                Colour = Color4.White,
            },
            new Circle
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                Position = new Vector2(-2, 2),
                Size = new Vector2(7),
                Colour = SongSelectTheme.Pink,
            },
        ],
    };

    private static Drawable createTopNavigationIcon(
        IconUsage icon,
        float x,
        bool selected = false) => new Container
        {
            Position = new Vector2(x, 0),
            Size = new Vector2(48, 72),
            Children =
        [
            new Circle
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Y = -2,
                Size = new Vector2(42),
                Colour = SongSelectTheme.Yellow,
                Alpha = selected ? 1 : 0,
            },
            new SpriteIcon
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Y = -2,
                Size = new Vector2(selected ? 20 : 22),
                Icon = icon,
                Colour = selected
                    ? SongSelectTheme.Navy
                    : Color4.White,
            },
            new Box
            {
                Anchor = Anchor.BottomCentre,
                Origin = Anchor.BottomCentre,
                Y = -5,
                Width = 26,
                Height = 3,
                Colour = SongSelectTheme.Pink,
                Alpha = selected ? 1 : 0,
            },
        ],
        };

    private Drawable createBrowseToolbar() => browseToolbar = new Container
    {
        Anchor = Anchor.TopRight,
        Origin = Anchor.TopRight,
        Position = new Vector2(-browse_right, 136),
        Size = new Vector2(browse_width, 34),
        Masking = false,
        Children =
        [
            filtersButton = new SongSelectBrowseToolButton(
                "FILTERS",
                "0 ACTIVE",
                200,
                FontAwesome.Solid.Filter,
                toggleFiltersPopover,
                82)
            {
                X = 570,
            },
            sortButton = new SongSelectBrowseToolButton(
                "SORT",
                sortButtonLabel(),
                200,
                FontAwesome.Solid.SortAmountDown,
                toggleSortPopover,
                68)
            {
                X = 780,
            },
        ],
    };

    private Drawable createFiltersPopover()
    {
        Container card = SongSelectSurface.CreateCard(
            out _,
            SongSelectSurface.Ivory(0.99f),
            SongSelectSurface.Border(0.24f),
            10,
            1);
        filtersPopover = new Container
        {
            Anchor = Anchor.TopRight,
            Origin = Anchor.TopRight,
            Position = new Vector2(-browse_right - 210, browse_top + 4),
            Size = new Vector2(410, 132),
            Alpha = 0,
            Children =
            [
                SongSelectSurface.CreateShadow(10, 0.12f, 3),
                card,
                new SpriteText
                {
                    Position = new Vector2(14, 12),
                    Text = "LIBRARY FILTERS",
                    Font = HomeTypography.Display(11),
                    Colour = SongSelectTheme.Navy,
                },
                groupButton = new SongSelectBrowseToolButton(
                    "GROUP",
                    packagesCollapsed ? "COLLAPSED" : "BEATMAPS",
                    184,
                    FontAwesome.Solid.LayerGroup,
                    togglePackageVisibility,
                    76)
                {
                    Position = new Vector2(14, 46),
                },
                createConvertsButton(),
                new YokkoButton(
                    "RESET",
                    FontAwesome.Solid.Undo,
                    ClearBrowseFilters,
                    108,
                    28,
                    YokkoButtonStyle.Quiet)
                {
                    Position = new Vector2(288, 8),
                },
                new SpriteText
                {
                    Position = new Vector2(14, 96),
                    Text = "F  CLOSE  ·  ESC  DISMISS",
                    Font = HomeTypography.Display(8),
                    Colour = new Color4(
                        SongSelectTheme.Navy.R,
                        SongSelectTheme.Navy.G,
                        SongSelectTheme.Navy.B,
                        0.56f),
                },
            ],
        };
        return filtersPopover;
    }

    private Drawable createConvertsButton()
    {
        convertsButton = new SongSelectBrowseToolButton(
            "CONVERTS",
            showConverts ? "SHOWN" : "HIDDEN",
            184,
            FontAwesome.Solid.ExchangeAlt,
            ToggleConvertedBeatmaps,
            84,
            showChevron: false)
        {
            Position = new Vector2(212, 46),
        };
        convertsButton.SetActive(showConverts);
        return convertsButton;
    }

    private Drawable createSortPopover()
    {
        sortPopover = new SongSelectSortPopover(
            SetSortMode,
            SetSortDirection)
        {
            Anchor = Anchor.TopRight,
            Origin = Anchor.TopRight,
            Position = new Vector2(-browse_right, browse_top + 4),
        };
        sortPopover.SetState(sortMode, sortDirection);
        return sortPopover;
    }

    private Drawable createSongBrowser() => songBrowser = new Container
    {
        Anchor = Anchor.TopRight,
        Origin = Anchor.TopRight,
        Position = new Vector2(-browse_right, browse_top),
        Size = new Vector2(
            browse_width,
            browse_height),
        Masking = true,
        Children = new Drawable[]
        {
            songList = new SongSelectVirtualisedList(
                difficultyRatingsFor,
                textureFor,
                () => displaySettings.DifficultyRatingMode.Value,
                textures.Get("SongSelect/Cute/sticker-star"),
                entry => select(entry),
                entry =>
                {
                    select(entry);
                    PlaySelected();
                },
                TogglePackage),
            noResults = new SongSelectNoResultsPanel(ClearBrowseFilters)
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
            },
        },
    };

    private Drawable createFooter()
    {
        // MODS now opens the dedicated GameplayModsScreen. Keep the retired
        // inline panel available only for explicit legacy previewing; eagerly
        // building its full button/settings tree here stalls every entry into
        // song select even though the hidden panel is never shown.
        modPanel = new Container { Alpha = 0 };
        if (Environment.GetEnvironmentVariable(
                "YOKKO_LEGACY_INLINE_MOD_PANEL") == "1")
        {
            doubleTimeMod = new SongSelectModButton(
                ManiaModId.DoubleTime,
                SongSelectTheme.Pink,
                () => ToggleMod(ManiaModId.DoubleTime),
                onModHoverChanged);
            nightcoreMod = new SongSelectModButton(
                ManiaModId.Nightcore,
                SongSelectTheme.Yellow,
                () => ToggleMod(ManiaModId.Nightcore),
                onModHoverChanged);
            halfTimeMod = new SongSelectModButton(
                ManiaModId.HalfTime,
                SongSelectTheme.Cyan,
                () => ToggleMod(ManiaModId.HalfTime),
                onModHoverChanged);
            daycoreMod = new SongSelectModButton(
                ManiaModId.Daycore,
                SongSelectTheme.Pink,
                () => ToggleMod(ManiaModId.Daycore),
                onModHoverChanged);
            easyMod = new SongSelectModButton(
                ManiaModId.Easy,
                SongSelectTheme.Cyan,
                () => ToggleMod(ManiaModId.Easy),
                onModHoverChanged);
            noFailMod = new SongSelectModButton(
                ManiaModId.NoFail,
                SongSelectTheme.Pink,
                () => ToggleMod(ManiaModId.NoFail),
                onModHoverChanged);
            suddenDeathMod = new SongSelectModButton(
                ManiaModId.SuddenDeath,
                SongSelectTheme.Yellow,
                () => ToggleMod(ManiaModId.SuddenDeath),
                onModHoverChanged);
            perfectMod = new SongSelectModButton(
                ManiaModId.Perfect,
                SongSelectTheme.Pink,
                () => ToggleMod(ManiaModId.Perfect),
                onModHoverChanged);
            hardRockMod = new SongSelectModButton(
                ManiaModId.HardRock,
                SongSelectTheme.Pink,
                () => ToggleMod(ManiaModId.HardRock),
                onModHoverChanged);
            accuracyChallengeMod = new SongSelectModButton(
                ManiaModId.AccuracyChallenge,
                SongSelectTheme.Yellow,
                () => ToggleMod(ManiaModId.AccuracyChallenge),
                onModHoverChanged);
            mirrorMod = new SongSelectModButton(
                ManiaModId.Mirror,
                SongSelectTheme.Cyan,
                () => ToggleMod(ManiaModId.Mirror),
                onModHoverChanged);
            randomMod = new SongSelectModButton(
                ManiaModId.Random,
                SongSelectTheme.Pink,
                () => ToggleMod(ManiaModId.Random),
                onModHoverChanged);
            holdOffMod = new SongSelectModButton(
                ManiaModId.HoldOff,
                SongSelectTheme.Yellow,
                () => ToggleMod(ManiaModId.HoldOff),
                onModHoverChanged);
            noReleaseMod = new SongSelectModButton(
                ManiaModId.NoRelease,
                SongSelectTheme.Cyan,
                () => ToggleMod(ManiaModId.NoRelease),
                onModHoverChanged);
            fadeInMod = new SongSelectModButton(
                ManiaModId.FadeIn,
                SongSelectTheme.Yellow,
                () => ToggleMod(ManiaModId.FadeIn),
                onModHoverChanged);
            hiddenMod = new SongSelectModButton(
                ManiaModId.Hidden,
                SongSelectTheme.Pink,
                () => ToggleMod(ManiaModId.Hidden),
                onModHoverChanged);
            coverMod = new SongSelectModButton(
                ManiaModId.Cover,
                SongSelectTheme.Cyan,
                () => ToggleMod(ManiaModId.Cover),
                onModHoverChanged);
            flashlightMod = new SongSelectModButton(
                ManiaModId.Flashlight,
                SongSelectTheme.Yellow,
                () => ToggleMod(ManiaModId.Flashlight),
                onModHoverChanged);
            constantSpeedMod = new SongSelectModButton(
                ManiaModId.ConstantSpeed,
                SongSelectTheme.Cyan,
                () => ToggleMod(ManiaModId.ConstantSpeed),
                onModHoverChanged);
            difficultyAdjustMod = new SongSelectModButton(
                ManiaModId.DifficultyAdjust,
                SongSelectTheme.Pink,
                () => ToggleMod(ManiaModId.DifficultyAdjust),
                onModHoverChanged);
            autoplayMod = new SongSelectModButton(
                ManiaModId.Autoplay,
                SongSelectTheme.Cyan,
                () => ToggleMod(ManiaModId.Autoplay),
                onModHoverChanged);
            cinemaMod = new SongSelectModButton(
                ManiaModId.Cinema,
                SongSelectTheme.Pink,
                () => ToggleMod(ManiaModId.Cinema),
                onModHoverChanged);
            invertMod = new SongSelectModButton(
                ManiaModId.Invert,
                SongSelectTheme.Yellow,
                () => ToggleMod(ManiaModId.Invert),
                onModHoverChanged);
            classicMod = new SongSelectModButton(
                ManiaModId.Classic,
                SongSelectTheme.Pink,
                () => ToggleMod(ManiaModId.Classic),
                onModHoverChanged);
            mutedMod = new SongSelectModButton(
                ManiaModId.Muted,
                SongSelectTheme.Cyan,
                () => ToggleMod(ManiaModId.Muted),
                onModHoverChanged);
            windUpMod = new SongSelectModButton(
                ManiaModId.WindUp,
                SongSelectTheme.Yellow,
                () => ToggleMod(ManiaModId.WindUp),
                onModHoverChanged);
            windDownMod = new SongSelectModButton(
                ManiaModId.WindDown,
                SongSelectTheme.Cyan,
                () => ToggleMod(ManiaModId.WindDown),
                onModHoverChanged);
            adaptiveSpeedMod = new SongSelectModButton(
                ManiaModId.AdaptiveSpeed,
                SongSelectTheme.Pink,
                () => ToggleMod(ManiaModId.AdaptiveSpeed),
                onModHoverChanged);
            modPanel = createModPanel();
        }

        var mods = modsToggleButton = new SongSelectModsToggleButton(
            ToggleModPanel,
            textures.Get("SongSelect/Cute/sticker-diamond"));
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
                    Colour = SongSelectSurface.Ivory(0.98f),
                },
                new Box
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 2,
                    Colour = new Color4(
                        SongSelectTheme.Cyan.R,
                        SongSelectTheme.Cyan.G,
                        SongSelectTheme.Cyan.B,
                        0.62f),
                },
                footerBackButton = new SongSelectFooterBackButton(
                    () => stopPreviewThen(this.Exit),
                    textures.Get("SongSelect/Cute/sticker-diamond"))
                {
                    Position = new Vector2(24, 24),
                },
                createAccountCard(),
                createShortcutLegend(),
                modPanel,
                createFooterToolDock(mods),
                playButton = new SongSelectPlayButton(
                    PlaySelected,
                    textures.Get("SongSelect/Cute/tape-long"))
                {
                    Anchor = Anchor.BottomRight,
                    Origin = Anchor.BottomRight,
                    Position = new Vector2(-24, -24),
                },
            },
        };
    }

    private Drawable createFooterToolDock(
        SongSelectModsToggleButton mods)
    {
        Container railShadow = createFooterToolShadow(Vector2.Zero);
        footerToolShadows = [railShadow];
        mods.Position = Vector2.Zero;
        Box firstDivider = createFooterToolDivider(154);
        Box secondDivider = createFooterToolDivider(308);
        footerToolDividers = [firstDivider, secondDivider];

        return footerToolDock = new Container
        {
            Anchor = Anchor.BottomRight,
            Origin = Anchor.BottomRight,
            Position = new Vector2(-436, -24),
            Size = FooterToolDockSizeFor(YokkoUiScale.Comfortable),
            Children =
            [
                railShadow,
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Masking = true,
                    CornerRadius = 11,
                    BorderThickness = 1.25f,
                    BorderColour = new Color4(
                        SongSelectTheme.Navy.R,
                        SongSelectTheme.Navy.G,
                        SongSelectTheme.Navy.B,
                        0.24f),
                    Children =
                    [
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = SongSelectSurface.Ivory(0.99f),
                        },
                        mods,
                        randomFooterButton = new SongSelectFooterToolButton(
                            "RANDOM",
                            FontAwesome.Solid.Random,
                            SongSelectTheme.Cyan,
                            selectRandomEntry)
                        {
                            Position = new Vector2(154, 0),
                        },
                        optionsFooterButton = new SongSelectFooterToolButton(
                            "OPTIONS",
                            FontAwesome.Solid.Cog,
                            SongSelectTheme.Pink,
                            OpenOptions)
                        {
                            Position = new Vector2(308, 0),
                        },
                        firstDivider,
                        secondDivider,
                    ],
                },
            ],
        };
    }

    private Drawable createShortcutLegend() => shortcutLegend = new Container
    {
        Position = new Vector2(786, 24),
        Size = new Vector2(220, 82),
        Masking = true,
        CornerRadius = 10,
        BorderThickness = 1,
        BorderColour = SongSelectSurface.Border(0.16f),
        Children =
        [
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = SongSelectSurface.Ivory(0.92f),
            },
            shortcutHint("U/D", "BROWSE", 12, 10),
            shortcutHint("PG", "JUMP", 116, 10),
            shortcutHint("/", "SEARCH", 12, 34),
            shortcutHint("F", "FILTERS", 116, 34),
            shortcutHint("R", "RANDOM", 12, 58),
            shortcutHint("ENT", "PLAY", 116, 58),
        ],
    };

    private static Drawable shortcutHint(
        string key,
        string label,
        float x,
        float y) => new Container
        {
            Position = new Vector2(x, y),
            Size = new Vector2(94, 16),
            Children =
            [
                new Container
                {
                    Size = new Vector2(27, 16),
                    Masking = true,
                    CornerRadius = 4,
                    Children =
                    [
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = new Color4(
                                SongSelectTheme.PaleCyan.R,
                                SongSelectTheme.PaleCyan.G,
                                SongSelectTheme.PaleCyan.B,
                                0.76f),
                        },
                        new SpriteText
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Text = key,
                            Font = HomeTypography.Display(8),
                            Colour = SongSelectTheme.Navy,
                        },
                    ],
                },
                new SpriteText
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    X = 34,
                    Text = label,
                    Font = HomeTypography.Display(8),
                    Colour = new Color4(
                        SongSelectTheme.Navy.R,
                        SongSelectTheme.Navy.G,
                        SongSelectTheme.Navy.B,
                        0.72f),
                },
            ],
        };

    private static Container createFooterToolShadow(Vector2 position) =>
        new()
        {
            Position = position,
            RelativeSizeAxes = Axes.Both,
            Child = SongSelectSurface.CreateShadow(11, 0.16f, 3),
        };

    private static Box createFooterToolDivider(float x) => new()
    {
        X = x,
        Y = 12,
        Width = 1,
        Height = 58,
        Colour = new Color4(
            SongSelectTheme.Navy.R,
            SongSelectTheme.Navy.G,
            SongSelectTheme.Navy.B,
            0.13f),
    };

    private void onUiScaleChanged(ValueChangedEvent<YokkoUiScale> change) =>
        applyFooterScaleLayout(change.NewValue);

    private void applyFooterScaleLayout(YokkoUiScale scale)
    {
        if (footerToolDock == null
            || footerToolShadows == null
            || footerToolDividers == null
            || modsToggleButton == null
            || randomFooterButton == null
            || optionsFooterButton == null)
            return;

        float buttonWidth = FooterToolButtonWidthFor(scale);
        float buttonStep = FooterToolButtonStepFor(scale);

        footerToolDock.Size = FooterToolDockSizeFor(scale);
        footerToolShadows[0].Position = Vector2.Zero;
        footerToolShadows[0].RelativeSizeAxes = Axes.Both;
        footerToolDividers[0].X = buttonStep;
        footerToolDividers[1].X = 2 * buttonStep;
        modsToggleButton.Size = new Vector2(buttonWidth, 82);
        randomFooterButton.Size = new Vector2(buttonWidth, 82);
        optionsFooterButton.Size = new Vector2(buttonWidth, 82);
        modsToggleButton.Position = Vector2.Zero;
        randomFooterButton.Position = new Vector2(buttonStep, 0);
        optionsFooterButton.Position = new Vector2(2 * buttonStep, 0);
    }

    internal void OpenOptions() => this.Push(new SettingsScreen());

    private Drawable createAccountCard() => accountCard =
        new SongSelectAccountCard(
            demo_profile_name,
            demo_profile_level,
            textures.Get("SongSelect/Ui/yokko-avatar-256"),
            textures.Get("SongSelect/Cute/sticker-star"))
        {
            Position = new Vector2(246, 24),
        };

    private Container createModPanel() => new Container
    {
        Anchor = Anchor.TopCentre,
        Origin = Anchor.BottomCentre,
        Position = new Vector2(-14, -10),
        Size = new Vector2(525, 342),
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
            modInfoTitle = new SpriteText
            {
                Position = new Vector2(42, 12),
                Text = "GAMEPLAY MODS",
                Font = HomeTypography.Display(13),
                Spacing = new Vector2(0.5f, 0),
                Colour = SongSelectTheme.Ivory,
            },
            modInfoDescription = new SpriteText
            {
                Position = new Vector2(18, 32),
                Text = "Hover a mod to see what it changes.",
                Font = HomeTypography.Display(9),
                Colour = SongSelectTheme.PaleCyan,
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
                Position = new Vector2(16, 57),
                Size = new Vector2(493, 1),
                Colour = new Color4(
                    SongSelectTheme.Cyan.R,
                    SongSelectTheme.Cyan.G,
                    SongSelectTheme.Cyan.B,
                    0.26f),
            },
            createModRow(
                64,
                easyMod,
                noFailMod,
                halfTimeMod,
                daycoreMod,
                noReleaseMod),
            createModRow(
                108,
                hardRockMod,
                suddenDeathMod,
                perfectMod,
                accuracyChallengeMod,
                doubleTimeMod),
            createModRow(
                152,
                nightcoreMod,
                fadeInMod,
                hiddenMod,
                coverMod,
                flashlightMod),
            createModRow(
                196,
                mirrorMod,
                randomMod,
                holdOffMod,
                invertMod,
                classicMod),
            createModRow(
                240,
                constantSpeedMod,
                difficultyAdjustMod,
                autoplayMod,
                cinemaMod,
                mutedMod),
            createModRow(
                284,
                windUpMod,
                windDownMod,
                adaptiveSpeedMod),
            new Box
            {
                Position = new Vector2(292, 64),
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
                    SetNoPauseAllowedPauses,
                    ToggleMod)
                {
                    Position = new Vector2(306, 62),
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

        if (previewActive)
            previewPlayer?.EnsurePlaying();

        if (songBrowser != null)
            songBrowser.Height = MathF.Max(
                320,
                DrawHeight - footer_height - songBrowser.Y - 12);
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

    private void rebuildDetails(
        bool animateSelection = false,
        float selectionDirection = 1)
    {
        if (detailsHost == null || selectedEntry == null)
            return;

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
        string bpmLabel = selectedMods.HasTimeRamp
            ? $"{selectedEntry.Bpm * selectedMods.TimeRampInitialRate:0}"
              + "→"
              + $"{selectedEntry.Bpm * selectedMods.TimeRampFinalRate:0}"
            : (selectedEntry.Bpm * selectedMods.PlaybackRate)
                .ToString("0");
        string rateLabel = selectedMods.HasTimeRamp
            ? $"{selectedMods.TimeRampInitialRate:0.00}"
              + "→"
              + $"{selectedMods.TimeRampFinalRate:0.00}×"
            : $"{selectedMods.PlaybackRate:0.00}×";
        ManiaDifficultyRatings difficultyRatings =
            difficultyRatingsFor(selectedEntry);
        displayedPlaybackRate = selectedMods.PlaybackRate;
        displayedBpm = bpmLabel;
        displayedMsdRating = difficultyRatings.EtternaMsd;
        displayedStarRating = difficultyRatings.RebirthStars;
        string[] detailsTitleLines = LayoutDetailsTitle(
            selectedEntry.Beatmap.Title);
        float artistY = detailsTitleLines.Length == 1 ? 102 : 111;
        float mapperY = detailsTitleLines.Length == 1 ? 126 : 134;

        rankingPanel = new SongSelectRankingPanel(
            selectedEntry,
            textures,
            ShowScoreResult)
        {
            Position = new Vector2(0, ranking_top),
        };
        rankingPanel.SetView(scoreView, textures);

        var nextDetailsContent = new Container
        {
            RelativeSizeAxes = Axes.Both,
            Children = new Drawable[]
            {
                createSongInfoPanel(),
                createSelectedArtwork(),
                createDifficultyPill(appliedBeatmap),
                createDifficultyValuePill(
                    difficultyRatings,
                    displaySettings.DifficultyRatingMode.Value),
                createAdaptiveDetailsTitle(detailsTitleLines),
                new SpriteText
                {
                    Position = new Vector2(details_content_left, artistY),
                    Width = details_content_width,
                    Truncate = true,
                    Text = selectedEntry.Beatmap.Artist,
                    Font = HomeTypography.Display(14),
                    Colour = SongSelectTheme.Navy,
                },
                new SpriteText
                {
                    Position = new Vector2(details_content_left, mapperY),
                    Width = details_content_width,
                    Truncate = true,
                    Text = $"mapped by {selectedEntry.Beatmap.Creator}",
                    Font = HomeTypography.Body(10),
                    Colour = SongSelectTheme.Cyan,
                },
                selectedChartFactsRow = createSelectedChartFactsRow(
                    displayedLengthMilliseconds,
                    bpmLabel,
                    appliedBeatmap.HitObjects.Count,
                    appliedBeatmap.OverallDifficulty,
                    appliedBeatmap.DrainRate),
                createSelectedDetailsDivider(),
                selectedPerformanceRow =
                    createSelectedPerformanceRow(rateLabel),
                rankingPanel,
                selectedModsButton = new SongSelectSelectedModsButton(
                    ToggleModPanel,
                    selectedMods)
                {
                    Position = new Vector2(696, ranking_top),
                },
            },
        };
        presentDetails(
            nextDetailsContent,
            animateSelection,
            selectionDirection);
    }

    private void presentDetails(
        Container next,
        bool animateSelection,
        float selectionDirection)
    {
        if (!animateSelection || activeDetailsContent == null)
        {
            detailsTransitionInProgress = false;
            detailsHost.Clear();
            next.Alpha = 1;
            next.Y = 0;
            detailsHost.Add(activeDetailsContent = next);
            return;
        }

        float direction = Math.Sign(selectionDirection);
        if (direction == 0)
            direction = 1;

        int transitionVersion = ++detailsTransitionVersion;
        if (detailsTransitionInProgress)
        {
            // A key-repeat or RANDOM jump can request several selections in
            // one update. Retire the superseded paper immediately so complete
            // ranking tables never stack in the same visible frame.
            detailsHost.Clear();
            next.Alpha = 1;
            next.Y = direction * 10;
            detailsHost.Add(activeDetailsContent = next);
            next.MoveToY(0, 170, Easing.OutQuint);
            Scheduler.AddDelayed(() =>
            {
                if (detailsTransitionVersion == transitionVersion
                    && ReferenceEquals(activeDetailsContent, next))
                {
                    detailsTransitionInProgress = false;
                }
            }, 190);
            return;
        }

        detailsTransitionInProgress = true;
        Container outgoing = activeDetailsContent;
        outgoing.ClearTransforms();
        outgoing.FadeOut(90, Easing.OutQuint);
        outgoing.MoveToY(-direction * 8, 170, Easing.OutQuint);

        // Keep the incoming paper and text opaque. Cross-fading two complete
        // ranking tables creates a cheap-looking double image; direction and
        // the existing wallpaper blend already communicate the selection.
        next.Alpha = 1;
        next.Y = direction * 10;
        detailsHost.Add(activeDetailsContent = next);
        next.MoveToY(0, 210, Easing.OutQuint);

        Scheduler.AddDelayed(() =>
        {
            if (outgoing.Parent == detailsHost)
                detailsHost.Remove(outgoing, true);
            if (detailsTransitionVersion == transitionVersion
                && ReferenceEquals(activeDetailsContent, next))
            {
                detailsTransitionInProgress = false;
            }
        }, 240);
    }

    private Drawable createPersonalPerformanceStrip()
    {
        SongSelectScore current = selectedEntry.Ranking
                                               .FirstOrDefault(score =>
                                                   score.IsCurrentPlayer);
        int rank = current?.Rank ?? 0;
        string accuracy = current == null
            ? "--"
            : $"{current.Accuracy:P2}";
        string combo = current == null
            ? "--"
            : $"{current.MaxCombo:N0}×";
        string mods = current == null || current.Mods.Count == 0
            ? "NM"
            : string.Join(" ", current.Mods);

        return new Container
        {
            Position = new Vector2(0, ranking_top + ranking_height + 12),
            Size = new Vector2(760, 88),
            Masking = true,
            CornerRadius = 11,
            BorderThickness = 1,
            BorderColour = new Color4(
                SongSelectTheme.Cyan.R,
                SongSelectTheme.Cyan.G,
                SongSelectTheme.Cyan.B,
                0.42f),
            Children =
            [
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new Color4(
                        SongSelectTheme.Surface.R,
                        SongSelectTheme.Surface.G,
                        SongSelectTheme.Surface.B,
                        0.90f),
                },
                new Box
                {
                    RelativeSizeAxes = Axes.Y,
                    Width = 7,
                    Colour = SongSelectTheme.Pink,
                },
                new SpriteText
                {
                    Position = new Vector2(20, 12),
                    Text = "YOUR POSITION",
                    Font = HomeTypography.Display(9),
                    Colour = SongSelectTheme.Cyan,
                },
                new SpriteText
                {
                    Position = new Vector2(20, 34),
                    Text = rank > 0 ? $"#{rank}" : "UNRANKED",
                    Font = HomeTypography.Display(28),
                    Colour = rank > 0
                        ? SongSelectTheme.Pink
                        : SongSelectTheme.PaleCyan,
                },
                personalMetric("BEST ACCURACY", accuracy, 166),
                personalMetric("MAX COMBO", combo, 318),
                personalMetric("SELECTED MODS", mods, 460),
                personalMetric(
                    "LOCAL PLAYS",
                    selectedEntry.History.Count.ToString(),
                    620),
            ],
        };
    }

    private static Drawable personalMetric(
        string label,
        string value,
        float x) => new Container
        {
            Position = new Vector2(x, 17),
            Size = new Vector2(126, 54),
            Children =
        [
            new SpriteText
            {
                Text = label,
                Font = HomeTypography.Display(8),
                Colour = new Color4(
                    SongSelectTheme.PaleCyan.R,
                    SongSelectTheme.PaleCyan.G,
                    SongSelectTheme.PaleCyan.B,
                    0.70f),
            },
            new SpriteText
            {
                Y = 22,
                Width = 124,
                Truncate = true,
                Text = value,
                Font = HomeTypography.Display(16),
                Colour = Color4.White,
            },
        ],
        };

    private Drawable createSongInfoPanel()
    {
        Container panel = SongSelectSurface.CreateCard(
            out _,
            SongSelectSurface.Ivory(0.88f),
            SongSelectSurface.Border(0.14f),
            14,
            1);

        return new Container
        {
            Size = new Vector2(details_width, details_panel_height),
            Children =
            [
                SongSelectSurface.CreateShadow(14, 0.06f, 2),
                panel,
            ],
        };
    }

    private static Drawable createAdaptiveDetailsTitle(string[] lines)
    {
        var flow = new FillFlowContainer
        {
            Position = new Vector2(details_content_left, 49),
            Width = details_content_width,
            AutoSizeAxes = Axes.Y,
            Direction = FillDirection.Vertical,
            Spacing = new Vector2(0, -2),
        };
        foreach (string line in lines)
        {
            flow.Add(new SpriteText
            {
                Width = details_content_width,
                Truncate = true,
                Text = line,
                Font = HomeTypography.Display(
                    lines.Length == 1 ? 28 : 21),
                Colour = SongSelectTheme.Navy,
            });
        }

        return flow;
    }

    internal static string[] LayoutDetailsTitle(string title) =>
        SongSelectTextLayout.BalancedTwoLines(
            title,
            details_title_units_per_line);

    private static Container createSelectedChartFactsRow(
        double lengthMilliseconds,
        string bpm,
        int noteCount,
        double overallDifficulty,
        double drainRate) => new()
        {
            Position = new Vector2(details_content_left, 210),
            Size = new Vector2(details_content_width, 34),
            Children =
            [
                createSongStat(
                    0,
                    0,
                    FontAwesome.Regular.Clock,
                    "LENGTH",
                    TimeSpan.FromMilliseconds(lengthMilliseconds)
                            .ToString(@"mm\:ss")),
                createDetailsVerticalDivider(110),
                createSongStat(
                    122,
                    0,
                    FontAwesome.Solid.WaveSquare,
                    "BPM",
                    bpm),
                createDetailsVerticalDivider(218),
                createSongStat(
                    230,
                    0,
                    FontAwesome.Solid.Music,
                    "NOTES",
                    noteCount.ToString("N0")),
                createDetailsVerticalDivider(332),
                createSongStat(
                    344,
                    0,
                    FontAwesome.Solid.Bullseye,
                    "OD",
                    overallDifficulty.ToString("0.0")),
                createDetailsVerticalDivider(434),
                createSongStat(
                    446,
                    0,
                    FontAwesome.Regular.Heart,
                    "HP",
                    drainRate.ToString("0.0")),
            ],
        };

    private static Drawable createSelectedDetailsDivider() => new Box
    {
        Position = new Vector2(details_content_left, 247),
        Size = new Vector2(details_content_width, 1),
        Colour = new Color4(
            SongSelectTheme.Navy.R,
            SongSelectTheme.Navy.G,
            SongSelectTheme.Navy.B,
            0.14f),
    };

    private Container createSelectedPerformanceRow(
        string rateLabel) => new()
        {
            Position = new Vector2(details_content_left, 255),
            Size = new Vector2(details_content_width, 35),
            Masking = true,
            CornerRadius = 8,
            Children =
        [
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = new Color4(
                    SongSelectTheme.PaleCyan.R,
                    SongSelectTheme.PaleCyan.G,
                    SongSelectTheme.PaleCyan.B,
                    0.24f),
            },
            createBestScoreStat(12, -3),
            createDetailsVerticalDivider(190, 27),
            createBestAccuracyStat(206, -3),
            createDetailsVerticalDivider(386, 27),
            createPlaybackRateStat(402, -3, rateLabel),
        ],
        };

    private static Drawable createDetailsVerticalDivider(
        float x,
        float height = 26) => new Box
        {
            Position = new Vector2(x, 3),
            Size = new Vector2(1, height),
            Colour = new Color4(
                SongSelectTheme.Navy.R,
                SongSelectTheme.Navy.G,
                SongSelectTheme.Navy.B,
                0.13f),
        };

    private Drawable createSelectedArtwork()
    {
        var artwork = new Container
        {
            RelativeSizeAxes = Axes.Both,
            Masking = true,
            CornerRadius = 12,
            BorderThickness = 1,
            BorderColour = new Color4(
                SongSelectTheme.Navy.R,
                SongSelectTheme.Navy.G,
                SongSelectTheme.Navy.B,
                0.28f),
            Children =
            [
                SongSelectArtworkCrop.Create(
                    textureFor(selectedEntry),
                    new Vector2(details_artwork_size)),
                new Box
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    RelativeSizeAxes = Axes.X,
                    Height = 42,
                    Colour = ColourInfo.GradientVertical(
                        new Color4(
                            SongSelectTheme.DeepNavy.R,
                            SongSelectTheme.DeepNavy.G,
                            SongSelectTheme.DeepNavy.B,
                            0),
                        new Color4(
                            SongSelectTheme.DeepNavy.R,
                            SongSelectTheme.DeepNavy.G,
                            SongSelectTheme.DeepNavy.B,
                            0.72f)),
                },
                new SpriteText
                {
                    Anchor = Anchor.BottomRight,
                    Origin = Anchor.BottomRight,
                    Position = new Vector2(-10, -8),
                    Text = "YOKKO",
                    Font = HomeTypography.Display(10),
                    Spacing = new Vector2(1.4f, 0),
                    Colour = Color4.White,
                },
            ],
        };

        return new Container
        {
            Origin = Anchor.Centre,
            Position = new Vector2(
                18 + details_artwork_size / 2),
            Size = new Vector2(details_artwork_size),
            Rotation = selected_artwork_rotation,
            Children =
            [
                SongSelectSurface.CreateShadow(12, 0.11f, 2),
                artwork,
                new Sprite
                {
                    Origin = Anchor.Centre,
                    Position = new Vector2(18, 3),
                    Size = new Vector2(52, 36),
                    Rotation = -8,
                    Texture = textures.Get(
                        "SongSelect/Cute/tape-short"),
                },
                new Sprite
                {
                    Origin = Anchor.Centre,
                    Position = new Vector2(268, 3),
                    Size = new Vector2(34, 36),
                    Rotation = 6,
                    Texture = textures.Get(
                        "SongSelect/Cute/sticker-star"),
                },
            ],
        };
    }

    private static Drawable createDifficultyPill(YokkoBeatmap beatmap) =>
        new Container
        {
            Position = new Vector2(details_content_left, 17),
            Size = new Vector2(210, 23),
            Masking = true,
            CornerRadius = 7,
            Children =
            [
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new Color4(
                        SongSelectTheme.PaleCyan.R,
                        SongSelectTheme.PaleCyan.G,
                        SongSelectTheme.PaleCyan.B,
                        0.78f),
                },
                new SpriteText
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Width = 192,
                    Truncate = true,
                    Text = beatmap.StageCount == 2
                        ? $"{beatmap.KeysPerStage}K + "
                          + $"{beatmap.KeysPerStage}K · "
                          + beatmap.DifficultyName
                        : $"{(int)beatmap.KeyMode}K · "
                          + beatmap.DifficultyName,
                    Font = HomeTypography.Display(9),
                    Colour = SongSelectTheme.Navy,
                },
            ],
        };

    private static Drawable createDifficultyValuePill(
        ManiaDifficultyRatings ratings,
        ManiaDifficultyRatingMode mode) => new Container
        {
            Anchor = Anchor.TopRight,
            Origin = Anchor.TopRight,
            Position = new Vector2(-18, 17),
            Size = new Vector2(122, 23),
            Masking = true,
            CornerRadius = 7,
            Children =
            [
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new Color4(
                        SongSelectTheme.Yellow.R,
                        SongSelectTheme.Yellow.G,
                        SongSelectTheme.Yellow.B,
                        0.42f),
                },
                new SpriteText
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Text = $"{ManiaDifficultyPresentation.Unit(mode)}  "
                           + ManiaDifficultyPresentation.FormatValue(
                               ratings,
                               mode),
                    Font = HomeTypography.Display(9),
                    Colour = SongSelectTheme.Navy,
                },
            ],
        };

    private Drawable createBestScoreStat(float x, float y) => new Container
    {
        Position = new Vector2(x, y),
        Size = new Vector2(164, 42),
        Children = new Drawable[]
        {
            new SpriteIcon
            {
                Position = new Vector2(0, 5),
                Size = new Vector2(13),
                Icon = FontAwesome.Solid.Trophy,
                Colour = SongSelectTheme.Yellow,
            },
            new SpriteText
            {
                Position = new Vector2(18, 0),
                Text = "BEST SCORE",
                Font = HomeTypography.Display(8),
                Colour = new Color4(
                    SongSelectTheme.Navy.R,
                    SongSelectTheme.Navy.G,
                    SongSelectTheme.Navy.B,
                    0.68f),
            },
            new SpriteText
            {
                Position = new Vector2(18, 18),
                Width = 142,
                Truncate = true,
                Text = selectedEntry.BestScore > 0
                    ? $"{selectedEntry.BestScore:N0}"
                    : "NO SCORE",
                Font = HomeTypography.Display(
                    selectedEntry.BestScore > 0 ? 13 : 9),
                Colour = SongSelectTheme.Navy,
            },
        },
    };

    private Drawable createBestAccuracyStat(float x, float y) =>
        new Container
        {
            Position = new Vector2(x, y),
            Size = new Vector2(164, 42),
            Children =
            [
                new SpriteIcon
                {
                    Position = new Vector2(0, 5),
                    Size = new Vector2(13),
                    Icon = FontAwesome.Solid.Bullseye,
                    Colour = SongSelectTheme.Cyan,
                },
                new SpriteText
                {
                    Position = new Vector2(18, 0),
                    Text = "BEST ACCURACY",
                    Font = HomeTypography.Display(7),
                    Colour = new Color4(
                        SongSelectTheme.Navy.R,
                        SongSelectTheme.Navy.G,
                        SongSelectTheme.Navy.B,
                        0.68f),
                },
                new SpriteText
                {
                    Position = new Vector2(18, 17),
                    Width = 142,
                    Truncate = true,
                    Text = selectedEntry.BestAccuracy > 0
                        ? $"{selectedEntry.BestAccuracy:P2}"
                        : "--",
                    Font = HomeTypography.Display(13),
                    Colour = SongSelectTheme.Navy,
                },
            ],
        };

    private static Drawable createPlaybackRateStat(
        float x,
        float y,
        string rateLabel) => new Container
        {
            Position = new Vector2(x, y),
            Size = new Vector2(164, 42),
            Children =
            [
                new SpriteIcon
                {
                    Position = new Vector2(0, 5),
                    Size = new Vector2(13),
                    Icon = FontAwesome.Solid.TachometerAlt,
                    Colour = SongSelectTheme.Pink,
                },
                new SpriteText
                {
                    Position = new Vector2(18, 0),
                    Text = "PLAYBACK RATE",
                    Font = HomeTypography.Display(7),
                    Colour = new Color4(
                        SongSelectTheme.Navy.R,
                        SongSelectTheme.Navy.G,
                        SongSelectTheme.Navy.B,
                        0.68f),
                },
                new SpriteText
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                    Position = new Vector2(-4, 0),
                    Text = "ALT +/-",
                    Font = HomeTypography.Display(7),
                    Colour = SongSelectTheme.Pink,
                },
                new SpriteText
                {
                    Position = new Vector2(18, 17),
                    Width = 142,
                    Truncate = true,
                    Text = rateLabel,
                    Font = HomeTypography.Display(13),
                    Colour = SongSelectTheme.Navy,
                },
            ],
        };

    private static Drawable createSongStat(float x, float y, IconUsage icon, LocalisableString label, string value) => new Container
    {
        Position = new Vector2(x, y),
        Size = new Vector2(112, 34),
        Children = new Drawable[]
        {
            new SpriteIcon
            {
                Position = new Vector2(0, 4),
                Size = new Vector2(11),
                Icon = icon,
                Colour = SongSelectTheme.Cyan,
            },
            new SpriteText
            {
                Position = new Vector2(15, 0),
                Text = label,
                Font = HomeTypography.Display(7),
                Colour = new Color4(
                    SongSelectTheme.Navy.R,
                    SongSelectTheme.Navy.G,
                    SongSelectTheme.Navy.B,
                    0.68f),
            },
            new SpriteText
            {
                Position = new Vector2(15, 14),
                Text = value,
                Font = HomeTypography.Display(12),
                Colour = SongSelectTheme.Navy,
            },
        },
    };

    private static Drawable createDifficultyRating(
        ManiaDifficultyRatings ratings,
        ManiaDifficultyRatingMode mode)
    {
        var flow = new FillFlowContainer
        {
            Position = new Vector2(252, 158),
            AutoSizeAxes = Axes.Both,
            Direction = FillDirection.Horizontal,
            Spacing = new Vector2(4, 0),
        };

        flow.Add(new SpriteText
        {
            Anchor = Anchor.CentreLeft,
            Origin = Anchor.CentreLeft,
            Text = ManiaDifficultyPresentation.Unit(mode),
            Font = HomeTypography.Display(9),
            Colour = SongSelectTheme.Cyan,
        });

        flow.Add(new SpriteText
        {
            Anchor = Anchor.CentreLeft,
            Origin = Anchor.CentreLeft,
            X = 6,
            Text = ManiaDifficultyPresentation.FormatValue(
                ratings,
                mode),
            Font = HomeTypography.Display(15),
            Colour = Color4.White,
        });
        flow.Add(new SpriteText
        {
            Anchor = Anchor.CentreLeft,
            Origin = Anchor.CentreLeft,
            X = 7,
            Text = ManiaDifficultyPresentation.Qualifier(
                ratings,
                mode),
            Font = HomeTypography.Display(8),
            Colour = SongSelectTheme.Cyan,
        });

        return flow;
    }

    private void rebuildSongList(
        bool animate = true,
        bool animateLayout = false,
        string transitionPackageId = null)
    {
        if (songList == null)
            return;

        songListRebuildVersion++;
        navigableEntries = visibleEntries.ToList();
        var virtualItems = new List<SongSelectVirtualItem>();

        foreach (IGrouping<string, SongSelectEntry> group in visibleEntries.GroupBy(
                     entry => entry.PackageId,
                     StringComparer.OrdinalIgnoreCase))
        {
            SongSelectEntry first = group.First();
            SongSelectEntry[] groupEntries = group.ToArray();
            int songCount = groupEntries
                           .Select(entry =>
                               $"{entry.Beatmap.Artist}\u001f{entry.Beatmap.Title}")
                           .Distinct(StringComparer.OrdinalIgnoreCase)
                           .Count();
            float sectionSpacing = virtualItems.Count == 0 ? 0 : 12;
            bool collapsed = first.IsPackage
                             && collapsedPackages.Contains(first.PackageId)
                             && string.IsNullOrWhiteSpace(searchQuery);

            if (first.IsPackage)
            {
                virtualItems.Add(new SongSelectVirtualItem
                {
                    HeaderEntry = first,
                    PackageId = first.PackageId,
                    PackageName = SongSelectTextLayout.DisplayPackageName(
                        first.PackageName,
                        first.PackageId),
                    SongCount = songCount,
                    ChartCount = groupEntries.Length,
                    Collapsed = collapsed,
                    VisualHeight = collapsed
                        ? SongSelectPackageHeader.CollapsedHeight
                        : SongSelectPackageHeader.ExpandedHeight,
                    SectionSpacingBefore = sectionSpacing,
                });
            }

            if (collapsed)
                continue;

            for (int entryIndex = 0;
                 entryIndex < groupEntries.Length;
                 entryIndex++)
            {
                SongSelectEntry entry = groupEntries[entryIndex];
                virtualItems.Add(new SongSelectVirtualItem
                {
                    Entry = entry,
                    CompactPrimaryText = first.IsPackage && songCount > 1
                        ? entry.Beatmap.Title
                        : entry.Beatmap.DifficultyName,
                    VisualHeight = entry.IsPackage
                        ? SongSelectSongRow.CompactHeight
                        : SongSelectSongRow.StandaloneHeight,
                    SectionSpacingBefore = !first.IsPackage
                                           && entryIndex == 0
                        ? sectionSpacing
                        : 0,
                });
            }
        }

        songList.SetItems(
            virtualItems,
            animateLayout,
            transitionPackageId);
        songList.UpdateSelection(selectedEntry);

        noResults.SetState(
            visibleEntries.Count == 0,
            entries.Count > 0,
            searchQuery,
            keyModeFilter,
            MinimumDifficultyFilter,
            DifficultyFilterUnit,
            showConverts);

        // 展开/折叠重建时不把滚动条拽回选中行，由 TogglePackage 自行锚定图包头。
        if (!animate)
            return;

        if (selectedEntry != null)
            songList.PrepareViewportFor(selectedEntry, browse_height);
    }

    private void applyFilters(bool immediate = false)
    {
        if (filterDisposed)
            return;

        int generation = ++filterGeneration;
        invalidateFilterRequest(incrementGeneration: false);
        bool needsDifficulty = sortMode == SongSelectSortMode.Difficulty
                               || MinimumDifficultyFilter > 0;
        var request = new SongSelectFilterRequest(
            generation,
            entries.Select(entry => new SongSelectSorting.EntrySnapshot(
                    entry,
                    entry.Beatmap,
                    needsDifficulty
                        ? selectedDifficultyValue(entry)
                        : null))
                .ToArray(),
            searchQuery,
            keyModeFilter,
            showConverts,
            MinimumDifficultyFilter,
            sortMode,
            sortDirection);

        if (immediate)
        {
            completeFilterRequest(
                request,
                runFilter(request, CancellationToken.None));
            return;
        }

        filterCancellation = new CancellationTokenSource();
        filterPending = true;
        CancellationToken cancellationToken = filterCancellation.Token;
        filterTask = Task.Run(async () =>
        {
            await Task.Delay(
                    filter_debounce_milliseconds,
                    cancellationToken)
                .ConfigureAwait(false);
            return runFilter(request, cancellationToken);
        }, cancellationToken);
        _ = observeFilterRequestAsync(request, filterTask);
    }

    private static SongSelectFilterResult runFilter(
        SongSelectFilterRequest request,
        CancellationToken cancellationToken)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        var filtered = new List<SongSelectSorting.EntrySnapshot>(
            request.Entries.Length);
        for (int index = 0; index < request.Entries.Length; index++)
        {
            if ((index & 0x7f) == 0)
                cancellationToken.ThrowIfCancellationRequested();

            SongSelectSorting.EntrySnapshot snapshot = request.Entries[index];
            YokkoBeatmap beatmap = snapshot.Beatmap;
            if (request.KeyModeFilter.HasValue
                && beatmap.KeyMode != request.KeyModeFilter)
            {
                continue;
            }

            if (!request.ShowConverts && beatmap.ConversionSource != null)
                continue;
            if (request.MinimumDifficulty > 0
                && (!snapshot.DifficultyValue.HasValue
                    || snapshot.DifficultyValue.Value
                    < request.MinimumDifficulty))
            {
                continue;
            }

            if (!matchesSearch(beatmap, request.SearchQuery))
                continue;

            filtered.Add(snapshot);
        }

        List<SongSelectEntry> sorted = SongSelectSorting.SortSnapshots(
            filtered,
            request.SortMode,
            request.SortDirection,
            cancellationToken);
        return new SongSelectFilterResult(sorted, stopwatch.Elapsed);
    }

    private static bool matchesSearch(YokkoBeatmap beatmap, string query) =>
        string.IsNullOrWhiteSpace(query)
        || beatmap.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
        || beatmap.RomanisedTitle.Contains(query, StringComparison.OrdinalIgnoreCase)
        || beatmap.Artist.Contains(query, StringComparison.OrdinalIgnoreCase)
        || beatmap.RomanisedArtist.Contains(query, StringComparison.OrdinalIgnoreCase)
        || beatmap.Creator.Contains(query, StringComparison.OrdinalIgnoreCase)
        || beatmap.DifficultyName.Contains(query, StringComparison.OrdinalIgnoreCase)
        || beatmap.Source.Contains(query, StringComparison.OrdinalIgnoreCase)
        || beatmap.Tags.Contains(query, StringComparison.OrdinalIgnoreCase);

    private async Task observeFilterRequestAsync(
        SongSelectFilterRequest request,
        Task<SongSelectFilterResult> task)
    {
        SongSelectFilterResult result;
        try
        {
            result = await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception exception)
        {
            Logger.Error(
                exception,
                "Could not filter the song-select library.",
                LoggingTarget.Runtime);
            return;
        }

        if (!filterDisposed)
            Scheduler.Add(() => completeFilterRequest(request, result));
    }

    private void completeFilterRequest(
        SongSelectFilterRequest request,
        SongSelectFilterResult result)
    {
        if (filterDisposed || request.Generation != filterGeneration)
            return;

        filterCancellation?.Dispose();
        filterCancellation = null;
        filterTask = null;
        filterPending = false;
        visibleEntries = result.Entries;
        diagnostics.Trace(
            "SONG_SELECT",
            "filter-applied",
            $"query={request.SearchQuery} | mode={request.KeyModeFilter?.ToString() ?? "all"}"
            + $" | visible={visibleEntries.Count}/{entries.Count}"
            + $" | sort={request.SortMode}:{request.SortDirection}"
            + $" | difficulty-min={request.MinimumDifficulty:0.00}"
            + $" | difficulty-unit={ManiaDifficultyPresentation.Unit(displaySettings.DifficultyRatingMode.Value)}"
            + $" | packages-collapsed={packagesCollapsed}"
            + $" | converts={request.ShowConverts}"
            + $" | elapsed-ms={result.Elapsed.TotalMilliseconds:0.0}");

        if (focusedPackageExpansion)
        {
            focusPackageExpansion(selectedEntry?.IsPackage == true
                ? selectedEntry.PackageId
                : null);
        }

        rebuildSongList();

        if (navigableEntries.Count > 0
            && !navigableEntries.Contains(selectedEntry))
            select(navigableEntries[0]);
    }

    private void invalidateFilterRequest(bool incrementGeneration = true)
    {
        if (incrementGeneration)
            filterGeneration++;
        filterCancellation?.Cancel();
        filterCancellation?.Dispose();
        filterCancellation = null;
        filterTask = null;
        filterPending = false;
    }

    private sealed record SongSelectFilterRequest(
        int Generation,
        SongSelectSorting.EntrySnapshot[] Entries,
        string SearchQuery,
        KeyMode? KeyModeFilter,
        bool ShowConverts,
        double MinimumDifficulty,
        SongSelectSortMode SortMode,
        SongSelectSortDirection SortDirection);

    private sealed record SongSelectFilterResult(
        List<SongSelectEntry> Entries,
        TimeSpan Elapsed);

    private void selectOffset(int direction, bool wrap = true)
    {
        if (navigableEntries.Count == 0)
            return;

        int index = navigableEntries.IndexOf(selectedEntry);
        if (index < 0)
            index = 0;
        else
            index = wrap
                ? (index + direction % navigableEntries.Count
                   + navigableEntries.Count) % navigableEntries.Count
                : Math.Clamp(index + direction, 0, navigableEntries.Count - 1);

        select(navigableEntries[index]);
    }

    private void selectBoundary(bool first)
    {
        if (navigableEntries.Count == 0)
            return;

        select(first ? navigableEntries[0] : navigableEntries[^1]);
    }

    private void selectRandomEntry()
    {
        if (navigableEntries.Count == 0)
            return;

        int currentIndex = navigableEntries.IndexOf(selectedEntry);
        int randomIndex = navigableEntries.Count == 1
            ? 0
            : Random.Shared.Next(navigableEntries.Count - 1);
        if (currentIndex >= 0 && randomIndex >= currentIndex)
            randomIndex++;

        select(navigableEntries[randomIndex]);
    }

    private void select(SongSelectEntry entry, bool rebuildList = true)
    {
        if (entry == null)
            return;

        int previousIndex = navigableEntries.IndexOf(selectedEntry);
        int nextIndex = navigableEntries.IndexOf(entry);
        float selectionDirection = previousIndex >= 0
                                   && nextIndex >= 0
                                   && nextIndex < previousIndex
            ? -1
            : 1;
        bool changed = selectedEntry != entry;
        bool packageChanged = changed
                              && entry.IsPackage
                              && !string.Equals(
                                  selectedEntry?.PackageId,
                                  entry.PackageId,
                                  StringComparison.OrdinalIgnoreCase);
        selectedEntry = entry;
        if (changed)
            requestPlayableBeatmap(selectedEntry);
        rememberSelectedEntry();

        if (changed)
        {
            playButton?.SetReady();
            diagnostics.Trace(
                "SONG_SELECT",
                "selection-changed",
                $"title={entry.Beatmap.Title} | artist={entry.Beatmap.Artist}"
                + $" | difficulty={entry.Beatmap.DifficultyName}"
                + $" | keys={(int)entry.Beatmap.KeyMode}"
                + $" | format={entry.Beatmap.SourceFormat}");
            crossFadeBackground(textureFor(entry));
            rebuildDetails(
                animateSelection: true,
                selectionDirection: selectionDirection);
            modSettingsHost?.SetState(selectedMods, entry.Beatmap);
            playSelectedPreview();
            scheduleGameplayPreload();
        }

        if (rebuildList)
        {
            bool selectedPackageHidden = entry.IsPackage
                                         && collapsedPackages.Contains(
                                             entry.PackageId);
            if (selectedPackageHidden
                || (focusedPackageExpansion && packageChanged))
            {
                enterFocusedPackageExpansion(entry.PackageId);
                rebuildSongList(
                    animate: false,
                    animateLayout: true,
                    transitionPackageId: entry.PackageId);
                songList?.ScrollEntryIntoView(entry, true);
                return;
            }

            songList?.UpdateSelection(entry);
            songList?.ScrollEntryIntoView(entry, true);
        }
    }

    private void crossFadeBackground(Texture texture)
    {
        Sprite outgoing = activeBackground;
        Sprite incoming = outgoing == backgroundA ? backgroundB : backgroundA;
        outgoing.ClearTransforms();
        incoming.ClearTransforms();
        // Restart an interrupted blend from a fully covered frame. Without
        // this, alternating the two sprites several times in one update can
        // leave both near zero alpha and expose the neutral stage beneath.
        outgoing.Alpha = 1;
        incoming.Texture = texture;
        incoming.Alpha = 0;
        incoming.FadeIn(220, Easing.OutQuint);
        outgoing.FadeOut(220, Easing.OutQuint);
        activeBackground = incoming;
    }

    private void playSelectedPreview()
    {
        if (!previewActive)
            return;

        previewHost?.AdoptPreview(selectedEntry?.Beatmap);
        previewPlayer?.Play(selectedEntry?.Beatmap, selectedMods);
    }

    private bool isPlayableBeatmapReady(SongSelectEntry entry) =>
        entry != null
        && (!entry.IsReadOnly
            || (entry.ChartId != null
                && materialisedExternalCharts.Contains(entry.ChartId)));

    private void requestPlayableBeatmap(
        SongSelectEntry entry,
        Action<bool> completed = null,
        bool immediate = false)
    {
        if (entry == null || playableBeatmapDisposed)
        {
            completed?.Invoke(false);
            return;
        }

        if (isPlayableBeatmapReady(entry))
        {
            if (playableBeatmapTask != null)
                invalidatePlayableBeatmapRequest();
            completed?.Invoke(true);
            return;
        }

        bool sameRequest = playableBeatmapTask is { IsCompleted: false }
                           && string.Equals(
                               playableBeatmapChartId,
                               entry.ChartId,
                               StringComparison.OrdinalIgnoreCase);
        if (sameRequest)
        {
            if (completed != null)
                playableBeatmapCallbacks.Add(completed);
            if (immediate)
                playableBeatmapStartSignal?.TrySetResult(true);
            return;
        }

        invalidatePlayableBeatmapRequest();
        if (completed != null)
            playableBeatmapCallbacks.Add(completed);
        playableBeatmapChartId = entry.ChartId;
        playableBeatmapCancellation = new CancellationTokenSource();
        playableBeatmapStartSignal = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        if (immediate)
            playableBeatmapStartSignal.TrySetResult(true);
        int generation = playableBeatmapGeneration;
        playableBeatmapTask = materialisePlayableBeatmapAfterSelectionSettlesAsync(
            entry.ChartId,
            playableBeatmapStartSignal.Task,
            playableBeatmapCancellation.Token);
        _ = observePlayableBeatmapAsync(
            playableBeatmapTask,
            entry,
            generation);
    }

    private async Task<YokkoBeatmap>
        materialisePlayableBeatmapAfterSelectionSettlesAsync(
            string chartId,
            Task immediateSignal,
            CancellationToken cancellationToken)
    {
        if (!immediateSignal.IsCompleted)
            await WaitForPlayableBeatmapStartAsync(
                    immediateSignal,
                    playable_beatmap_debounce_milliseconds,
                    cancellationToken)
                .ConfigureAwait(false);

        return await importedChartLibrary.GetPlayableBeatmapAsync(
                chartId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    internal static async Task WaitForPlayableBeatmapStartAsync(
        Task immediateSignal,
        int delayMilliseconds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(immediateSignal);
        cancellationToken.ThrowIfCancellationRequested();
        if (immediateSignal.IsCompleted)
            return;

        Task delay = Task.Delay(delayMilliseconds, cancellationToken);
        await Task.WhenAny(immediateSignal, delay).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private async Task observePlayableBeatmapAsync(
        Task<YokkoBeatmap> task,
        SongSelectEntry entry,
        int generation)
    {
        YokkoBeatmap playable;
        try
        {
            playable = await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception exception)
        {
            Logger.Error(
                exception,
                $"Could not materialise external chart '{entry.ChartId}'.",
                LoggingTarget.Runtime);
            Scheduler.Add(() => completePlayableBeatmapRequest(
                entry,
                generation,
                null));
            return;
        }

        Scheduler.Add(() => completePlayableBeatmapRequest(
            entry,
            generation,
            playable));
    }

    private void completePlayableBeatmapRequest(
        SongSelectEntry entry,
        int generation,
        YokkoBeatmap playable)
    {
        if (playableBeatmapDisposed
            || generation != playableBeatmapGeneration
            || !ReferenceEquals(selectedEntry, entry)
            || entry.ChartId == null
            || !importedEntries.TryGetValue(
                entry.ChartId,
                out SongSelectEntry tracked)
            || !ReferenceEquals(tracked, entry))
        {
            return;
        }

        playableBeatmapCancellation?.Dispose();
        playableBeatmapCancellation = null;
        playableBeatmapTask = null;
        playableBeatmapStartSignal = null;
        playableBeatmapChartId = null;
        Action<bool>[] callbacks = playableBeatmapCallbacks.ToArray();
        playableBeatmapCallbacks.Clear();
        bool success = playable != null;
        if (success)
        {
            entry.Beatmap = playable;
            materialisedExternalCharts.Add(entry.ChartId);
            difficultyRatingsCache.Remove(entry);
            rebuildDetails();
            refreshSongListDifficulties();
            modSettingsHost?.SetState(selectedMods, playable);
            scheduleGameplayPreload();
        }

        foreach (Action<bool> callback in callbacks)
            callback(success);
    }

    private void invalidatePlayableBeatmapRequest(bool clearCallbacks = true)
    {
        playableBeatmapGeneration++;
        playableBeatmapCancellation?.Cancel();
        playableBeatmapCancellation?.Dispose();
        playableBeatmapCancellation = null;
        playableBeatmapTask = null;
        playableBeatmapStartSignal = null;
        playableBeatmapChartId = null;
        if (clearCallbacks)
            playableBeatmapCallbacks.Clear();
    }

    private SongSelectEntry rememberedEntryOrDefault()
    {
        if (selectionMemory?.ChartId != null
            && importedEntries.TryGetValue(
                selectionMemory.ChartId,
                out SongSelectEntry remembered))
        {
            return remembered;
        }

        if (selectionMemory != null && entries.Count > 0)
        {
            return entries[
                SongSelectSelectionMemory.ChooseInitialEntryIndex(
                    entries.Count,
                    Random.Shared)];
        }

        return entries.LastOrDefault();
    }

    private void restoreRememberedSelection()
    {
        SongSelectEntry remembered = rememberedEntryOrDefault();
        if (remembered != null && remembered != selectedEntry)
            select(remembered);
    }

    private void rememberSelectedEntry()
    {
        if (selectionMemory == null || selectedEntry == null)
            return;

        selectionMemory.ChartId = importedEntries
                                  .Where(pair => ReferenceEquals(
                                      pair.Value,
                                      selectedEntry))
                                  .Select(pair => pair.Key)
                                  .FirstOrDefault();
    }

    private Texture textureFor(SongSelectEntry entry)
    {
        if (entry != null
            && Path.IsPathRooted(entry.WallpaperTexture)
            && File.Exists(entry.WallpaperTexture))
        {
            try
            {
                Texture artwork = artworkTextureCache.Get(
                    entry.WallpaperTexture,
                    renderer);
                if (artwork != null)
                    return artwork;
            }
            catch
            {
                // Invalid chart artwork falls back to Yokko's bundled image.
            }
        }

        return textures.Get(
                   SongSelectArtworkPolicy.Resolve(entry?.WallpaperTexture))
               ?? textures.Get(SongSelectArtworkPolicy.FallbackTexture);
    }

    // Song-select uses bounded 512 px thumbnails. Gameplay intentionally
    // loads its own 1920x1080-capped artwork instead of reusing a thumbnail.
    private static Texture gameplayArtworkTextureFor(SongSelectEntry entry) =>
        null;

    private void prewarmInitialArtwork()
    {
        initialArtworkPrewarmCount = 0;
        initialArtworkPrewarmPaths.Clear();
        foreach (SongSelectEntry entry in songList.GetArtworkPreloadCandidates(
                     selectedEntry,
                     browse_height,
                     initial_artwork_preload_limit))
        {
            if (!Path.IsPathRooted(entry.WallpaperTexture)
                || !File.Exists(entry.WallpaperTexture))
            {
                continue;
            }

            artworkTextureCache.Prewarm(
                entry.WallpaperTexture,
                renderer);
            initialArtworkPrewarmPaths.Add(entry.WallpaperTexture);
            initialArtworkPrewarmCount++;
        }
    }

    private void refreshSavedScores(
        SongSelectEntry entryToRefresh = null)
    {
        if (entryToRefresh != null)
        {
            refreshEntry(entryToRefresh);
            return;
        }

        foreach (SongSelectEntry entry in entries)
            refreshEntry(entry);

        void refreshEntry(SongSelectEntry entry)
        {
            StoredGameplayScore saved = scoreStore.GetBest(
                entry.Beatmap,
                selectedMods,
                gameplaySettings.GetJudgementConfiguration());
            JudgementConfiguration judgementConfiguration =
                gameplaySettings.GetJudgementConfiguration();
            SongSelectScore[] history = scoreStore.GetHistory(
                                                   entry.Beatmap,
                                                   judgementConfiguration,
                                                   30)
                                               .Select(toSongSelectScore)
                                               .ToArray();
            SongSelectScore[] ranking = scoreStore.GetRanking(
                                                   entry.Beatmap,
                                                   judgementConfiguration,
                                                   30)
                                               .Select(toSongSelectScore)
                                               .ToArray();

            entry.BestScore = saved == null
                ? 0
                : (int)Math.Min(int.MaxValue, saved.Score);
            entry.BestAccuracy = saved?.Accuracy ?? 0;
            entry.Ranking = ranking;
            entry.History = history;

            SongSelectScore toSongSelectScore(
                StoredGameplayScore score,
                int rank) => new(
                    rank + 1,
                    string.IsNullOrWhiteSpace(score.PlayerName)
                        ? demo_profile_name
                        : score.PlayerName,
                    "yokko",
                    score.Rank,
                    (int)Math.Min(int.MaxValue, score.Score),
                    score.Accuracy,
                    score.MaxCombo,
                    score.ModLabels,
                    score.IsCurrentPlayer != false,
                    score.PlayedAt,
                    score.Perfect,
                    score.Great,
                    score.Good,
                    score.Ok,
                    score.Meh,
                    score.Miss,
                    score.ComboBreaks,
                    score.MaxMissCombo,
                    score.ReplayPath,
                    score.ModSet,
                    score.JudgementConfiguration
                        ?? judgementConfiguration);
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

    private void onChartLibraryChanged(ImportedChartLibraryChange change)
    {
        lock (pendingLibraryChangeLock)
        {
            pendingLibraryChange = pendingLibraryChange == null
                ? change
                : pendingLibraryChange.Merge(change);
        }

        // Match lazer's collection update boundary: many source notifications
        // may arrive before the next update frame, but only the newest library
        // state is useful to a consumer. Inactive preloads keep the revision
        // dirty and apply it when they actually enter the screen stack.
        Scheduler.AddOnce(applyPendingLibraryChange);
    }

    private void applyPendingLibraryChange()
    {
        if (!screenActive)
            return;

        ImportedChartLibraryChange change;
        lock (pendingLibraryChangeLock)
        {
            change = pendingLibraryChange;
            pendingLibraryChange = null;
        }

        if (change == null || change.Revision <= libraryRevision)
            return;

        bool structureChanged =
            (change.Kind & ImportedChartLibraryChangeKind.Structure) != 0;
        if (!structureChanged
            && (change.Kind
                & ImportedChartLibraryChangeKind.DifficultyRatings) != 0
            && synchroniseImportedDifficultyRatings())
        {
            return;
        }

        synchroniseImportedCharts(selectNewest: structureChanged);
    }

    private bool synchroniseImportedDifficultyRatings()
    {
        (long revision, long structureRevision,
            IReadOnlyList<ImportedChart> charts) =
            importedChartLibrary.GetSnapshot();
        if (structureRevision != libraryStructureRevision
            || charts.Count != importedEntries.Count)
        {
            return false;
        }

        foreach (ImportedChart chart in charts)
        {
            if (!importedEntries.TryGetValue(
                    chart.Id,
                    out SongSelectEntry entry)
                || !importedChartModels.ContainsKey(chart.Id))
            {
                return false;
            }

            ImportedChart previous = importedChartModels[chart.Id];
            importedChartModels[chart.Id] = chart;
            if (ReferenceEquals(previous, chart))
                continue;

            entry.DifficultyRating = chart.DifficultyRating;
            entry.StarRating = chart.StarRating;
            difficultyRatingsCache.Remove(entry);
        }

        libraryRevision = revision;
        libraryStructureRevision = structureRevision;

        // Background base-rating completion does not alter search, grouping or
        // title ordering. Only rebuild the cheap virtual index when the current
        // view explicitly depends on difficulty values.
        if (sortMode == SongSelectSortMode.Difficulty
            || MinimumDifficultyFilter > 0)
            applyFilters();
        else
        {
            songList?.UpdateDifficulties();
            rebuildDetails();
        }

        return true;
    }

    private void onDifficultyRatingModeChanged(
        ValueChangedEvent<ManiaDifficultyRatingMode> _)
    {
        refreshDifficultyFilterBar();
        applyFilters();
        rebuildDetails();
    }

    private void refreshDifficultyFilterBar() =>
        difficultyFilterBar?.SetState(
            displaySettings.DifficultyRatingMode.Value,
            MinimumDifficultyFilter);

    private bool passesDifficultyFilter(SongSelectEntry entry)
    {
        double minimum = MinimumDifficultyFilter;
        if (minimum <= 0)
            return true;

        double? difficulty = selectedDifficultyValue(entry);
        return difficulty.HasValue && difficulty.Value >= minimum;
    }

    private void refreshSongListDifficulties()
    {
        if (sortMode == SongSelectSortMode.Difficulty
            || MinimumDifficultyFilter > 0)
        {
            applyFilters();
            return;
        }

        songList?.UpdateDifficulties();
    }

    private ManiaDifficultyRatings difficultyRatingsFor(
        SongSelectEntry entry)
    {
        JudgementConfiguration judgementConfiguration =
            entry.Beatmap.SourceFormat == ChartSourceFormat.Quaver
                ? JudgementConfiguration.QuaverDefault
                : gameplaySettings.GetJudgementConfiguration();
        bool minesEnabled = gameplaySettings.MinesEnabled.Value;
        var state = new DifficultyCacheState(
            selectedMods.Fingerprint,
            judgementConfiguration,
            minesEnabled);
        if (difficultyRatingsCache.TryGetValue(
                entry,
                out Dictionary<
                    DifficultyCacheState,
                    ManiaDifficultyRatings> entryCache)
            && entryCache.TryGetValue(
                state,
                out ManiaDifficultyRatings cached))
        {
            return cached;
        }

        JudgementConfiguration importedJudgement =
            entry.Beatmap.SourceFormat == ChartSourceFormat.Quaver
                ? JudgementConfiguration.QuaverDefault
                : JudgementConfiguration.YokkoDefault;
        ManiaDifficultyRatings ratings;
        if (selectedMods.IsEmpty
            && minesEnabled
            && judgementConfiguration == importedJudgement)
        {
            // ImportedChartLibrary already calculates and persistently caches
            // this exact no-mod result. Re-running both native MSD and Rebirth
            // for every visual row made the redesigned list take seconds.
            ratings = new ManiaDifficultyRatings(
                entry.DifficultyRating,
                entry.StarRating);
        }
        else
        {
            YokkoBeatmap appliedBeatmap =
                ManiaBeatmapModTransformer.Apply(
                    entry.Beatmap,
                    selectedMods);
            YokkoBeatmap difficultyBeatmap =
                ManiaTimeRampTimeline.TransformForDifficulty(
                    appliedBeatmap,
                    selectedMods);
            double timelineRate = selectedMods.HasTimeRamp
                ? 1
                : selectedMods.PlaybackRate;
            ManiaStarRatingContext context =
                ManiaStarRatingContext.ForGameplay(
                    difficultyBeatmap,
                    selectedMods,
                    judgementConfiguration,
                    minesEnabled,
                    timelineRate,
                    dynamicRatePretransformed:
                        selectedMods.HasTimeRamp);
            ratings = ManiaDifficultyCalculator.CalculateResult(
                difficultyBeatmap,
                context,
                timelineRate);
        }

        if (entryCache == null)
        {
            entryCache = [];
            difficultyRatingsCache[entry] = entryCache;
        }
        entryCache[state] = ratings;
        return ratings;
    }

    private double? selectedDifficultyValue(
        SongSelectEntry entry) =>
        difficultyRatingsFor(entry).Value(
            displaySettings.DifficultyRatingMode.Value);

    private void synchroniseImportedCharts(
        bool selectNewest = false,
        bool refreshSongList = true)
    {
        (long revision, long structureRevision,
            IReadOnlyList<ImportedChart> charts) =
            importedChartLibrary.GetSnapshot();
        string selectedImportedId = selectedEntry?.ChartId;
        var previousEntries = importedEntries.Values.ToHashSet(
            ReferenceEqualityComparer.Instance);
        var nextEntries = new Dictionary<string, SongSelectEntry>(
            StringComparer.OrdinalIgnoreCase);
        var nextModels = new Dictionary<string, ImportedChart>(
            StringComparer.OrdinalIgnoreCase);
        foreach (ImportedChart chart in charts)
        {
            SongSelectEntry entry;
            if (importedChartModels.TryGetValue(chart.Id, out ImportedChart previousModel)
                && ReferenceEquals(previousModel, chart)
                && importedEntries.TryGetValue(chart.Id, out SongSelectEntry previousEntry))
            {
                entry = previousEntry;
            }
            else
            {
                materialisedExternalCharts.Remove(chart.Id);
                if (string.Equals(
                        playableBeatmapChartId,
                        chart.Id,
                        StringComparison.OrdinalIgnoreCase))
                {
                    invalidatePlayableBeatmapRequest();
                }
                entry = createImportedEntry(chart);
                if (importedEntries.TryGetValue(chart.Id, out SongSelectEntry replaced))
                    difficultyRatingsCache.Remove(replaced);
            }

            nextEntries[chart.Id] = entry;
            nextModels[chart.Id] = chart;
        }

        var retainedEntries = nextEntries.Values.ToHashSet(
            ReferenceEqualityComparer.Instance);
        foreach (SongSelectEntry removed in previousEntries.Where(
                     entry => !retainedEntries.Contains(entry)))
        {
            difficultyRatingsCache.Remove(removed);
        }

        // Reorder references cheaply while retaining unchanged entry objects,
        // their calculated mod difficulties and the current selection.
        entries.RemoveAll(previousEntries.Contains);
        importedEntries.Clear();
        importedChartModels.Clear();
        foreach (ImportedChart chart in charts)
        {
            SongSelectEntry entry = nextEntries[chart.Id];
            importedEntries[chart.Id] = entry;
            importedChartModels[chart.Id] = nextModels[chart.Id];
            entries.Add(entry);
        }
        materialisedExternalCharts.IntersectWith(nextEntries.Keys);

        if (playableBeatmapChartId != null
            && !nextEntries.ContainsKey(playableBeatmapChartId))
        {
            invalidatePlayableBeatmapRequest();
        }

        libraryRevision = revision;
        libraryStructureRevision = structureRevision;

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

        if (refreshSongList)
            applyFilters();

        if (selectNewest && importedEntries.Count > 0)
            select(entries[^1]);
    }

    private void updateFilters()
    {
        keyModeFilterButton?.SetMode(keyModeFilter);
        int activeCount = (keyModeFilter.HasValue ? 1 : 0)
                          + (!string.IsNullOrWhiteSpace(searchQuery) ? 1 : 0)
                          + (MinimumDifficultyFilter > 0 ? 1 : 0)
                          + (!showConverts ? 1 : 0)
                          + (packagesCollapsed ? 1 : 0);
        filtersButton?.SetValue($"{activeCount} ACTIVE");
    }

    private void cycleKeyModeFilter() => SetKeyModeFilter(
        keyModeFilter switch
        {
            null => KeyMode.FourKey,
            KeyMode.FourKey => KeyMode.SevenKey,
            _ => null,
        });

    internal void SetSortMode(SongSelectSortMode mode)
    {
        if (sortMode == mode)
            return;

        sortMode = mode;
        sortDirection = SongSelectSorting.DefaultDirection(mode);
        rememberSortState();
        updateSortControls();
        applyFilters();
    }

    internal void SetSortDirection(SongSelectSortDirection direction)
    {
        if (sortDirection == direction)
            return;

        sortDirection = direction;
        rememberSortState();
        updateSortControls();
        applyFilters();
    }

    private void toggleSortPopover()
    {
        if (sortPopover?.IsOpen == true)
        {
            sortPopover.Close();
            sortButton?.SetActive(false);
            return;
        }

        closeFiltersPopover();
        sortPopover?.Open();
        sortButton?.SetActive(true);
    }

    private void toggleFiltersPopover()
    {
        if (filtersPopoverOpen)
        {
            closeFiltersPopover();
            return;
        }

        if (sortPopover?.IsOpen == true)
        {
            sortPopover.Close();
            sortButton?.SetActive(false);
        }

        filtersPopoverOpen = true;
        filtersPopover?.ClearTransforms();
        filtersPopover?.FadeIn(120, Easing.OutQuint);
        filtersButton?.SetActive(true);
    }

    private void closeFiltersPopover()
    {
        filtersPopoverOpen = false;
        filtersPopover?.ClearTransforms();
        filtersPopover?.FadeOut(90, Easing.OutQuint);
        filtersButton?.SetActive(false);
    }

    private void updateSortControls()
    {
        sortButton?.SetValue(sortButtonLabel());
        sortPopover?.SetState(sortMode, sortDirection);
    }

    private void rememberSortState()
    {
        if (selectionMemory == null)
            return;

        selectionMemory.SortMode = sortMode;
        selectionMemory.SortDirection = sortDirection;
    }

    private string sortButtonLabel() =>
        $"{SongSelectSorting.Label(sortMode)} "
        + (sortDirection == SongSelectSortDirection.Ascending ? "↑" : "↓");

    private void togglePackageVisibility()
    {
        packagesCollapsed = !packagesCollapsed;
        focusedPackageExpansion = false;
        groupButton?.SetValue(packagesCollapsed ? "COLLAPSED" : "BEATMAPS");
        if (packagesCollapsed)
        {
            foreach (string packageId in entries
                         .Where(entry => entry.IsPackage)
                         .Select(entry => entry.PackageId)
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                collapsedPackages.Add(packageId);
            }
        }
        else
        {
            collapsedPackages.Clear();
        }

        updateFilters();
        rebuildSongList(
            animate: false,
            animateLayout: true);
    }

    private void enterFocusedPackageExpansion(string packageId)
    {
        focusedPackageExpansion = true;
        packagesCollapsed = false;
        groupButton?.SetValue("BEATMAPS");
        updateFilters();
        focusPackageExpansion(packageId);
    }

    private void focusPackageExpansion(string packageId)
    {
        collapsedPackages.Clear();
        foreach (string candidate in entries
                     .Where(entry => entry.IsPackage)
                     .Select(entry => entry.PackageId)
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!string.Equals(
                    candidate,
                    packageId,
                    StringComparison.OrdinalIgnoreCase))
            {
                collapsedPackages.Add(candidate);
            }
        }
    }

    internal void ToggleConvertedBeatmaps()
    {
        showConverts = !showConverts;
        convertsButton?.SetValue(showConverts ? "SHOWN" : "HIDDEN");
        convertsButton?.SetActive(showConverts);
        updateFilters();
        applyFilters();
    }

    private static Sprite createBackground(Texture texture) => new()
    {
        RelativeSizeAxes = Axes.Both,
        Texture = texture,
        FillMode = FillMode.Fill,
    };

    private static Drawable createIvoryStage() => new Container
    {
        RelativeSizeAxes = Axes.Both,
        Children = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Y,
                Width = 426,
                Colour = SongSelectTheme.Ivory,
            },
            new Box
            {
                RelativeSizeAxes = Axes.Y,
                X = 426,
                Y = -12,
                Width = 92,
                Height = 1.08f,
                Rotation = 2.4f,
                Colour = SongSelectTheme.Ivory,
            },
        },
    };

    private static List<SongSelectEntry> createEntries() => [];

    private static SongSelectEntry createImportedEntry(ImportedChart imported)
    {
        YokkoBeatmap beatmap = imported.Result.Beatmap;
        double lengthMilliseconds = imported.LengthMilliseconds
            ?? (beatmap.HitObjects.Count == 0
            ? 0
            : beatmap.HitObjects.Max(hitObject =>
                hitObject.EndTimeMilliseconds ?? hitObject.StartTimeMilliseconds));
        double bpm = imported.Bpm
            ?? beatmap.TimingPoints
                            .Where(point => point.Uninherited && point.BeatsPerMinute > 0)
                            .Select(point => point.BeatsPerMinute)
                            .FirstOrDefault();

        return new SongSelectEntry(
            beatmap,
            SongSelectArtworkPolicy.Resolve(imported.ArtworkPath),
            imported.DifficultyRating,
            imported.StarRating,
            TimeSpan.FromMilliseconds(Math.Max(0, lengthMilliseconds)),
            bpm,
            0,
            0,
            [],
            [],
            imported.PackageId,
            imported.PackageName,
            imported.IsPackage,
            imported.Id,
            imported.IsReadOnly);
    }

    private static Drawable createBackgroundIsolation() => new Box
    {
        RelativeSizeAxes = Axes.Both,
        // This is deliberately neutral and constant. It protects the paper
        // UI from both blown-out and very busy chart artwork without sampling
        // or adapting to any specific beatmap's palette.
        Colour = new Color4(1f, 0.995f, 0.972f, 0.76f),
    };

    private static Drawable createBackgroundMoodWash() => new Container
    {
        RelativeSizeAxes = Axes.Both,
        Children =
        [
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = ColourInfo.GradientHorizontal(
                    new Color4(
                        SongSelectTheme.Cyan.R,
                        SongSelectTheme.Cyan.G,
                        SongSelectTheme.Cyan.B,
                        0.08f),
                    new Color4(
                        SongSelectTheme.Pink.R,
                        SongSelectTheme.Pink.G,
                        SongSelectTheme.Pink.B,
                        0.025f)),
            },
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = ColourInfo.GradientVertical(
                    new Color4(0f, 0f, 0f, 0f),
                    new Color4(
                        SongSelectTheme.Cyan.R,
                        SongSelectTheme.Cyan.G,
                        SongSelectTheme.Cyan.B,
                        0.05f)),
            },
        ],
    };

    private readonly record struct DifficultyCacheState(
        string ModFingerprint,
        JudgementConfiguration JudgementConfiguration,
        bool MinesEnabled);

    private partial class ScoreResultInputBlocker : Container
    {
        public override bool HandlePositionalInput => true;
    }

}
