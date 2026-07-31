using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
    private const float ranking_top = 286;
    private const float ranking_height = 510;
    private const float browse_top = 222;
    private const float browse_width = 850;
    private const float browse_right = 24;

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
    private Sprite backgroundA;
    private Sprite backgroundB;
    private Sprite activeBackground;
    private Container detailsHost;
    private Container songBrowser;
    private Container footer;
    private SongSelectVirtualisedList songList;
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
    private SpriteText modInfoTitle;
    private SpriteText modInfoDescription;
    private ManiaModId? hoveredMod;
    private AnimatedGifSprite mascotAnimation;
    private SongSelectPreviewPlayer previewPlayer;
    private SongSelectBrowseToolButton sortButton;
    private SongSelectBrowseToolButton groupButton;

    private List<SongSelectEntry> visibleEntries;
    private List<SongSelectEntry> navigableEntries = [];
    private SongSelectEntry selectedEntry;
    private KeyMode? keyModeFilter;
    private string searchQuery = string.Empty;
    private SongSelectScoreView scoreView = SongSelectScoreView.GlobalRanking;
    private ManiaModSet selectedMods = ManiaModSet.Empty;
    private bool modPanelOpen;
    private bool sortByDifficulty;
    private bool packagesCollapsed;
    private bool previewActive;
    private bool transitionPending;
    private bool nextPreloadScheduled;
    private long libraryRevision = -1;
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
    }

    internal SongSelectEntry SelectedEntry => selectedEntry;
    internal int VisibleEntryCount => visibleEntries?.Count ?? 0;
    internal int VisibleRowCount => navigableEntries.Count;
    internal int MaterialisedSongListDrawableCount =>
        songList?.MaterialisedDrawableCount ?? 0;
    internal long LibraryRevision => libraryRevision;
    internal KeyMode? KeyModeFilter => keyModeFilter;
    internal string SearchQuery => searchQuery;
    internal Vector2 SearchBoxSize => searchBox?.Size ?? Vector2.Zero;
    internal Vector2 RankingPanelSize =>
        rankingPanel?.Size ?? Vector2.Zero;
    internal Vector2 RankingContentSize =>
        rankingPanel?.ContentSize ?? Vector2.Zero;
    internal SongSelectScoreView ScoreView => scoreView;
    internal ManiaModSet SelectedMods => selectedMods;
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

        // 展开/折叠只影响列表排布：不走 applyFilters（会把折叠进去的选中顶
        // 替换成第一首歌），也不重播整列表的入场动画、不把滚动条拽回选中行。
        // 只就地重建，并让被点的图包头保持在视野里。
        rebuildSongList(animate: false);

        Scheduler.AddDelayed(
            () => songList?.ScrollPackageToTop(packageId, false),
            80);
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
        ensurePlayableBeatmap(selectedEntry);
        rememberSelectedEntry();
        visibleEntries = entries.ToList();

        Texture firstWallpaper = textureFor(selectedEntry);
        Texture logo = textures.Get("home-logo-light");

        InternalChildren = new Drawable[]
        {
            backgroundA = createBackground(firstWallpaper),
            backgroundB = createBackground(firstWallpaper),
            createBackgroundIsolation(),
            createBackgroundMoodWash(),
            stage = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    createLibraryShade(),
                    createHeader(logo),
                    detailsHost = new Container
                    {
                        Position = new Vector2(details_left, details_top),
                        Size = new Vector2(
                            details_width,
                            designed_height - footer_height - details_top - 18),
                    },
                    createSongBrowser(),
                    createFooter(),
                    mascotAnimation = new AnimatedGifSprite(
                        "Textures/SongSelect/mascot-box.gif")
                    {
                        Anchor = Anchor.BottomLeft,
                        Origin = Anchor.BottomLeft,
                        Position = new Vector2(8, -1),
                        Size = new Vector2(130),
                    },
                },
            },
        };
        logLoadStage("static UI");

        backgroundB.Alpha = 0;
        activeBackground = backgroundA;
        rebuildDetails();
        logLoadStage("selected details");
        applyFilters();
        logLoadStage("song rows");
        updateFilters();
        displaySettings.DifficultyRatingMode.BindValueChanged(
            onDifficultyRatingModeChanged);

        stage.Alpha = 0;
        stage.Y = 14;

        Logger.Log(
            $"Song select construction: {loadStopwatch.Elapsed.TotalMilliseconds:0} ms "
            + $"({entries.Count} charts, {songList?.ItemCount ?? 0} indexed rows, "
            + $"{songList?.MaterialisedDrawableCount ?? 0} materialised).",
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

    public override void OnEntering(ScreenTransitionEvent e)
    {
        base.OnEntering(e);
        restoreRememberedSelection();
        previewActive = true;
        diagnostics.Trace(
            "SONG_SELECT",
            "entered",
            $"selected={selectedEntry?.Beatmap.Title ?? "none"}");
        playSelectedPreview();
        stage.FadeIn(260, Easing.OutQuint).MoveToY(0, 420, Easing.OutQuint);

        // Build the next short-lived screen once the first visible frame is
        // established. This keeps future Home -> Play transitions instant
        // without making the current click compete with UI construction.
        if (!nextPreloadScheduled && requestNextPreload != null)
        {
            nextPreloadScheduled = true;
            Scheduler.AddDelayed(requestNextPreload, 250);
        }
    }

    public override void OnResuming(ScreenTransitionEvent e)
    {
        base.OnResuming(e);
        modPanelOpen = false;
        modsToggleButton?.SetOpen(false);
        synchroniseImportedCharts();
        int selectedIndex = Math.Max(0, entries.IndexOf(selectedEntry));
        refreshSavedScores();
        selectedEntry = entries.Count == 0
            ? null
            : entries[Math.Min(selectedIndex, entries.Count - 1)];
        applyFilters();
        rebuildDetails();
        previewActive = true;
        diagnostics.Trace(
            "SONG_SELECT",
            "resumed",
            $"entries={entries.Count} | visible={visibleEntries.Count}"
            + $" | selected={selectedEntry?.Beatmap.Title ?? "none"}");
        playSelectedPreview();
        this.FadeIn(180, Easing.OutQuint);
    }

    public override void OnSuspending(ScreenTransitionEvent e)
    {
        base.OnSuspending(e);
        previewActive = false;
        diagnostics.Trace("SONG_SELECT", "suspended");
        if (!KeepsPreviewPlaying(e.Next))
            previewPlayer?.Stop();
        this.FadeTo(0.35f, 180, Easing.OutQuint);
    }

    public override bool OnExiting(ScreenExitEvent e)
    {
        previewActive = false;
        diagnostics.Trace("SONG_SELECT", "exiting");
        if (previewHost == null)
            previewPlayer?.Stop();
        else
        {
            previewHost.CompletePreviewHandoff(
                previewPlayer?.WaitForIdleAsync() ?? Task.CompletedTask);
            previewPlayer?.Detach();
        }
        this.FadeOut(180, Easing.OutQuint);
        return base.OnExiting(e);
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
            }

            if (previewPlayer != null)
                _ = previewPlayer.DisposeAsync();
        }

        base.Dispose(isDisposing);
    }

    protected override bool OnKeyDown(KeyDownEvent e)
    {
        if (HandlePlaybackRateShortcut(e.Key, e.AltPressed))
            return true;

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
                HandleEscape();
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
        diagnostics.Trace(
            "SONG_SELECT",
            "key-filter-changed",
            $"mode={mode?.ToString() ?? "all"}");
        updateFilters();
        applyFilters();
    }

    internal void SetSearchQuery(string query)
    {
        searchQuery = query ?? string.Empty;
        diagnostics.Trace(
            "SONG_SELECT",
            "search-changed",
            $"query={searchQuery}");
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
        if (!DismissSearch())
            stopPreviewThen(this.Exit);
    }

    internal void ToggleScoreView()
    {
        scoreView = scoreView == SongSelectScoreView.GlobalRanking
            ? SongSelectScoreView.Personal
            : SongSelectScoreView.GlobalRanking;
        rebuildDetails();
    }

    internal void ActivateRankingPanel() => rankingPanel?.TriggerClick();

    internal void PlaySelected()
    {
        if (selectedEntry == null || transitionPending)
            return;
        if (!ensurePlayableBeatmap(selectedEntry))
            return;

        ManiaModSet gameplayMods = selectedMods;
        YokkoBeatmap gameplayBeatmap = selectedEntry.Beatmap;
        string gameplayArtwork = selectedEntry.WallpaperTexture;
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
            + $" | mods={string.Join(',', gameplayMods.DisplayLabels)}",
            LogLevel.Important);
        previewActive = false;
        previewPlayer?.Stop();
        stage.FadeTo(0.84f, 90, Easing.OutQuint)
             .ScaleTo(0.997f, 90, Easing.OutQuint);

        // Large charts can spend a noticeable frame applying mods and
        // preparing gameplay bounds. Construct the still-unloaded screen off
        // the update thread while the preview engine shuts down, then hand it
        // back to the ScreenStack on the scheduler.
        Task<GameplaySessionScreen> gameplayTask = Task.Run(() =>
            new GameplaySessionScreen(new GameplayScreen(
                gameplayBeatmap,
                mods: gameplayMods,
                cinemaArtworkPath: gameplayArtwork)));
        _ = finishGameplayTransitionAsync(
            previewPlayer?.WaitForIdleAsync() ?? Task.CompletedTask,
            gameplayTask);
    }

    private async Task finishGameplayTransitionAsync(
        Task previewStopped,
        Task<GameplaySessionScreen> gameplayTask)
    {
        try
        {
            await Task.WhenAll(previewStopped, gameplayTask)
                      .ConfigureAwait(false);
            Scheduler.Add(() =>
            {
                transitionPending = false;
                diagnostics.Trace("SONG_SELECT", "gameplay-ready");
                this.Push(gameplayTask.Result);
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
                previewActive = true;
                stage.FadeTo(1, 120, Easing.OutQuint)
                     .ScaleTo(1, 120, Easing.OutQuint);
                playSelectedPreview();
            });
        }
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
            return true;

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
    }

    internal void ToggleModPanel()
    {
        if (modPanelOpen || selectedEntry == null)
            return;
        if (!ensurePlayableBeatmap(selectedEntry))
            return;

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

    private Drawable createHeader(Texture logo) => new Container
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
                Position = new Vector2(-312, 78),
                Size = new Vector2(564, 48),
            },
            new FillFlowContainer
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                Position = new Vector2(-browse_right, 78),
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(8, 0),
                Children =
                [
                    allFilter = new SongSelectFilterButton(
                        "ALL",
                        92,
                        () => SetKeyModeFilter(null),
                        accentDot: true),
                    fourKeyFilter = new SongSelectFilterButton(
                        "4K",
                        80,
                        () => SetKeyModeFilter(KeyMode.FourKey)),
                    sevenKeyFilter = new SongSelectFilterButton(
                        "7K",
                        80,
                        () => SetKeyModeFilter(KeyMode.SevenKey)),
                ],
            },
            createBrowseToolbar(),
        ],
    };

    private Drawable createTopNavigation(Texture logo) => new Container
    {
        RelativeSizeAxes = Axes.X,
        Height = 64,
        Children =
        [
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = SongSelectTheme.Navy,
            },
            new Sprite
            {
                Position = new Vector2(24, 13),
                Size = new Vector2(152, 39),
                Texture = logo,
                FillMode = FillMode.Fit,
            },
            createTopNavigationIcon(FontAwesome.Solid.Music, 214, true),
            createTopNavigationIcon(FontAwesome.Regular.Star, 272),
            createTopNavigationIcon(FontAwesome.Solid.PencilAlt, 330),
            createTopNavigationIcon(FontAwesome.Solid.Users, 388),
            createTopNavigationIcon(FontAwesome.Solid.Crosshairs, 446),
            createTopNavigationIcon(FontAwesome.Solid.Trophy, 1010),
            createTopNavigationIcon(FontAwesome.Regular.Comment, 1068),
            createTopNavigationIcon(FontAwesome.Solid.Globe, 1126),
            new SpriteText
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                X = -128,
                Text = "MOCHI",
                Font = HomeTypography.Display(15),
                Colour = Color4.White,
            },
            new Container
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                X = -68,
                Size = new Vector2(42),
                Masking = true,
                CornerRadius = 21,
                BorderThickness = 2,
                BorderColour = SongSelectTheme.Cyan,
                Child = new Sprite
                {
                    RelativeSizeAxes = Axes.Both,
                    Texture = textures.Get("yokko")?.Crop(
                        new RectangleF(270, 2200, 850, 850)),
                    FillMode = FillMode.Fill,
                },
            },
            new SpriteIcon
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                X = -24,
                Size = new Vector2(20),
                Icon = FontAwesome.Regular.Bell,
                Colour = Color4.White,
            },
        ],
    };

    private static Drawable createTopNavigationIcon(
        IconUsage icon,
        float x,
        bool selected = false) => new Container
    {
        Position = new Vector2(x, 0),
        Size = new Vector2(48, 64),
        Children =
        [
            new SpriteIcon
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Y = -2,
                Size = new Vector2(22),
                Icon = icon,
                Colour = selected
                    ? SongSelectTheme.Yellow
                    : Color4.White,
            },
            new Box
            {
                Anchor = Anchor.BottomCentre,
                Origin = Anchor.BottomCentre,
                Width = 30,
                Height = 4,
                Colour = SongSelectTheme.Cyan,
                Alpha = selected ? 1 : 0,
            },
        ],
    };

    private Drawable createBrowseToolbar() => new Container
    {
        Anchor = Anchor.TopRight,
        Origin = Anchor.TopRight,
        Position = new Vector2(-browse_right, 140),
        Size = new Vector2(browse_width, 68),
        Children =
        [
            createStarRangeSummary(),
            new SongSelectFilterButton(
                "SHOW CONVERTS",
                172,
                () => { })
            {
                X = 678,
                Height = 34,
            },
            sortButton = new SongSelectBrowseToolButton(
                "SORT",
                sortByDifficulty ? "DIFFICULTY" : "TITLE",
                267,
                FontAwesome.Solid.SortAmountDown,
                toggleSortMode)
            {
                Y = 38,
            },
            groupButton = new SongSelectBrowseToolButton(
                "GROUP",
                packagesCollapsed ? "COLLAPSED" : "BEATMAPS",
                267,
                FontAwesome.Solid.LayerGroup,
                togglePackageVisibility)
            {
                Position = new Vector2(275, 38),
            },
            new SongSelectBrowseToolButton(
                "COLLECTION",
                "ALL SONGS",
                300,
                FontAwesome.Solid.Archive,
                () => { },
                122)
            {
                Position = new Vector2(550, 38),
            },
        ],
    };

    private static Drawable createStarRangeSummary() => new Container
    {
        Size = new Vector2(670, 34),
        Masking = true,
        CornerRadius = 7,
        BorderThickness = 1,
        BorderColour = SongSelectSurface.Border(0.24f),
        Children =
        [
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = SongSelectSurface.Ivory(0.98f),
            },
            new SpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 12,
                Text = "STAR RATING",
                Font = HomeTypography.Display(8),
                Colour = SongSelectTheme.Navy,
            },
            new SpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 104,
                Text = "0.0",
                Font = HomeTypography.Display(11),
                Colour = SongSelectTheme.Cyan,
            },
            new Box
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 142,
                Width = 486,
                Height = 4,
                Colour = SongSelectTheme.Cyan,
            },
            new SpriteText
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                X = -12,
                Text = "∞",
                Font = HomeTypography.Display(13),
                Colour = SongSelectTheme.Navy,
            },
        ],
    };

    private Drawable createSongBrowser() => songBrowser = new Container
    {
        Anchor = Anchor.TopRight,
        Origin = Anchor.TopRight,
        Position = new Vector2(-browse_right, browse_top),
        Size = new Vector2(
            browse_width,
            designed_height - footer_height - browse_top - 16),
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

        var mods = modsToggleButton = new SongSelectModsToggleButton(
            ToggleModPanel,
            textures.Get("SongSelect/Cute/sticker-diamond"))
        {
            Position = new Vector2(878, 24),
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
                new SongSelectFooterBackButton(
                    () => stopPreviewThen(this.Exit),
                    textures.Get("SongSelect/Cute/sticker-diamond"))
                {
                    Position = new Vector2(168, 25),
                },
                createAccountCard(),
                modPanel,
                mods,
                new SongSelectFooterToolButton(
                    "RANDOM",
                    FontAwesome.Solid.Random,
                    SongSelectTheme.Cyan,
                    selectRandomEntry)
                {
                    Position = new Vector2(1020, 24),
                },
                new SongSelectFooterToolButton(
                    "OPTIONS",
                    FontAwesome.Solid.Cog,
                    SongSelectTheme.Pink,
                    () => { })
                {
                    Position = new Vector2(1162, 24),
                },
                new SongSelectPlayButton(
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

    private Drawable createAccountCard()
    {
        Texture avatar = textures.Get("yokko")?
                                 .Crop(new RectangleF(270, 2200, 850, 850));
        Container panel = SongSelectSurface.CreateCard(
            out _,
            SongSelectSurface.Ivory(0.98f),
            new Color4(
                SongSelectTheme.Cyan.R,
                SongSelectTheme.Cyan.G,
                SongSelectTheme.Cyan.B,
                0.48f),
            10,
            1.25f);

        return new Container
        {
            Position = new Vector2(390, 23),
            Size = new Vector2(470, 82),
            Children =
            [
                SongSelectSurface.CreateShadow(10, 0.18f, 3),
                panel,
                new Container
                {
                    Position = new Vector2(10, 8),
                    Size = new Vector2(64),
                    Masking = true,
                    CornerRadius = 32,
                    BorderThickness = 2,
                    BorderColour = SongSelectTheme.Cyan,
                    Child = new Sprite
                    {
                        RelativeSizeAxes = Axes.Both,
                        Texture = avatar,
                        FillMode = FillMode.Fill,
                    },
                },
                new Circle
                {
                    Position = new Vector2(60, 56),
                    Size = new Vector2(12),
                    BorderThickness = 2,
                    BorderColour = Color4.White,
                    Colour = new Color4(0.24f, 0.82f, 0.48f, 1f),
                },
                new SpriteText
                {
                    Position = new Vector2(88, 8),
                    Text = "MOCHI",
                    Font = HomeTypography.Display(17),
                    Colour = SongSelectTheme.Navy,
                },
                new SpriteText
                {
                    Position = new Vector2(170, 13),
                    Text = "●  ONLINE",
                    Font = HomeTypography.Display(8),
                    Colour = new Color4(0.22f, 0.72f, 0.46f, 1f),
                },
                accountMetric("7,272", "PP", 88),
                accountMetric("98.76%", "ACC", 164),
                accountMetric("#12,846", "GLOBAL", 254),
                new Box
                {
                    Position = new Vector2(88, 67),
                    Size = new Vector2(252, 5),
                    Colour = new Color4(
                        SongSelectTheme.Cyan.R,
                        SongSelectTheme.Cyan.G,
                        SongSelectTheme.Cyan.B,
                        0.24f),
                },
                new Box
                {
                    Position = new Vector2(88, 67),
                    Size = new Vector2(142, 5),
                    Colour = SongSelectTheme.Cyan,
                },
                new SpriteText
                {
                    Position = new Vector2(350, 58),
                    Text = "LV.45",
                    Font = HomeTypography.Display(10),
                    Colour = SongSelectTheme.Pink,
                },
            ],
        };
    }

    private static Drawable accountMetric(
        string value,
        string label,
        float x) => new Container
        {
            Position = new Vector2(x, 31),
            Size = new Vector2(64, 27),
            Children =
        [
            new SpriteText
            {
                Text = value,
                Font = HomeTypography.Display(11),
                Colour = SongSelectTheme.Navy,
            },
            new SpriteText
            {
                Y = 14,
                Text = label,
                Font = HomeTypography.Display(7),
                Colour = SongSelectTheme.Cyan,
            },
        ],
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

        rankingPanel = new SongSelectRankingPanel(selectedEntry, textures, newView => scoreView = newView)
        {
            Position = new Vector2(0, ranking_top),
        };
        rankingPanel.SetView(scoreView, textures);

        detailsHost.AddRange(new Drawable[]
        {
            createSongInfoPanel(),
            createSelectedArtwork(),
            new Sprite
            {
                Position = new Vector2(7, 19),
                Size = new Vector2(54, 28),
                Rotation = -12,
                Texture = textures.Get("SongSelect/Cute/tape-short"),
                FillMode = FillMode.Fit,
            },
            createDifficultyPill(appliedBeatmap),
            createAdaptiveDetailsTitle(selectedEntry.Beatmap.Title),
            new SpriteText
            {
                Position = new Vector2(252, 132),
                Width = 420,
                Truncate = true,
                Text = selectedEntry.Beatmap.Artist,
                Font = HomeTypography.Display(14),
                Colour = SongSelectTheme.Navy,
            },
            new SpriteText
            {
                Position = new Vector2(252, 157),
                Width = 420,
                Truncate = true,
                Text = $"mapped by {selectedEntry.Beatmap.Creator}",
                Font = HomeTypography.Body(10),
                Colour = SongSelectTheme.Cyan,
            },
            createStarRating(
                difficultyRatings,
                displaySettings.DifficultyRatingMode.Value),
            createSongStat(
                252,
                204,
                FontAwesome.Regular.Clock,
                "LENGTH",
                TimeSpan.FromMilliseconds(
                    displayedLengthMilliseconds).ToString(@"mm\:ss")),
            createSongStat(
                360,
                204,
                FontAwesome.Solid.WaveSquare,
                "BPM",
                bpmLabel),
            createSongStat(
                455,
                204,
                FontAwesome.Solid.Music,
                "NOTES",
                appliedBeatmap.HitObjects.Count.ToString("N0")),
            createBestScoreStat(586, 198),
            createBestAccuracyStat(710, 198),
            createPlaybackRateBadge(rateLabel),
            rankingPanel,
        });
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
        => new Container
        {
            Size = new Vector2(details_width, 255),
            Children =
            [
                SongSelectSurface.CreateShadow(14, 0.12f, 3),
                new Sprite
                {
                    RelativeSizeAxes = Axes.Both,
                    Texture = textures.Get("SongSelect/Cute/paper-song-info"),
                    Alpha = 0.96f,
                },
            ],
        };

    private static Drawable createAdaptiveDetailsTitle(string title)
    {
        string[] lines = SongSelectTextLayout.TwoLines(title, 30);
        var flow = new FillFlowContainer
        {
            Position = new Vector2(252, 49),
            Width = 420,
            AutoSizeAxes = Axes.Y,
            Direction = FillDirection.Vertical,
            Spacing = new Vector2(0, -2),
        };
        foreach (string line in lines)
        {
            flow.Add(new SpriteText
            {
                Width = 420,
                Truncate = true,
                Text = line,
                Font = HomeTypography.Display(
                    lines.Length == 1 ? 28 : 24),
                Colour = SongSelectTheme.Navy,
            });
        }

        return flow;
    }

    private Drawable createSelectedArtwork() => new Container
    {
        Position = new Vector2(25, 30),
        Size = new Vector2(210),
        Rotation = -1.2f,
        Masking = true,
        CornerRadius = 10,
        BorderThickness = 1.5f,
        BorderColour = new Color4(
            SongSelectTheme.Navy.R,
            SongSelectTheme.Navy.G,
            SongSelectTheme.Navy.B,
            0.28f),
        Children =
        [
            new Sprite
            {
                RelativeSizeAxes = Axes.Both,
                Texture = textureFor(selectedEntry),
                FillMode = FillMode.Fill,
            },
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

    private static Drawable createDifficultyPill(YokkoBeatmap beatmap) =>
        new Container
        {
            Position = new Vector2(24, 3),
            Size = new Vector2(190, 25),
            Masking = true,
            CornerRadius = 7,
            Children =
            [
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = SongSelectTheme.PaleCyan,
                },
                new SpriteText
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Width = 176,
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

    private static Drawable createStarRating(
        ManiaDifficultyRatings ratings,
        ManiaDifficultyRatingMode mode)
        => new Container
        {
            Position = new Vector2(685, 55),
            Size = new Vector2(142, 33),
            Masking = true,
            CornerRadius = 8,
            BorderThickness = 1,
            BorderColour = new Color4(
                SongSelectTheme.Cyan.R,
                SongSelectTheme.Cyan.G,
                SongSelectTheme.Cyan.B,
                0.66f),
            Children =
            [
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = SongSelectTheme.PaleCyan,
                },
                new SpriteText
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    X = 13,
                    Text = ManiaDifficultyPresentation.Unit(mode),
                    Font = HomeTypography.Display(9),
                    Colour = SongSelectTheme.Cyan,
                },
                new SpriteText
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    X = -13,
                    Text = ManiaDifficultyPresentation.FormatValue(
                        ratings,
                        mode),
                    Font = HomeTypography.Display(15),
                    Colour = SongSelectTheme.Navy,
                },
            ],
        };

    private Drawable createBestScoreStat(float x, float y) => new Container
    {
        Position = new Vector2(x, y),
        Size = new Vector2(120, 54),
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
                Width = 98,
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
            Size = new Vector2(120, 52),
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
                    Width = 98,
                    Truncate = true,
                    Text = selectedEntry.BestAccuracy > 0
                        ? $"{selectedEntry.BestAccuracy:P2}"
                        : "--",
                    Font = HomeTypography.Display(13),
                    Colour = SongSelectTheme.Navy,
                },
            ],
        };

    private static Drawable createSongStat(float x, float y, IconUsage icon, LocalisableString label, string value) => new Container
    {
        Position = new Vector2(x, y),
        Size = new Vector2(58, 52),
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

    private static Drawable createPlaybackRateBadge(
        string rateLabel) =>
        new Container
        {
            Position = new Vector2(697, 21),
            Size = new Vector2(76, 25),
            Masking = true,
            CornerRadius = 7,
            BorderThickness = 1,
            BorderColour = SongSelectTheme.Cyan,
            Children =
            [
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = SongSelectTheme.PaleCyan,
                },
                new SpriteText
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Text = rateLabel,
                    Font = HomeTypography.Display(9),
                    Colour = SongSelectTheme.Navy,
                },
            ],
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

    private void rebuildSongList(bool animate = true)
    {
        if (songList == null)
            return;

        navigableEntries = [];
        var virtualItems = new List<SongSelectVirtualItem>();

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
                virtualItems.Add(new SongSelectVirtualItem
                {
                    HeaderEntry = first,
                    PackageId = first.PackageId,
                    PackageName = first.PackageName,
                    SongCount = songCount,
                    ChartCount = groupEntries.Length,
                    Collapsed = collapsed,
                    VisualHeight = 84,
                });
            }

            if (collapsed)
                continue;

            foreach (SongSelectEntry entry in groupEntries)
            {
                virtualItems.Add(new SongSelectVirtualItem
                {
                    Entry = entry,
                    VisualHeight = entry.IsPackage ? 58 : 84,
                });
                navigableEntries.Add(entry);
            }
        }

        songList.SetItems(virtualItems);
        songList.UpdateSelection(selectedEntry);

        noResults.FadeTo(visibleEntries.Count == 0 ? 1 : 0, 140, Easing.OutQuint);

        // 展开/折叠重建时不把滚动条拽回选中行，由 TogglePackage 自行锚定图包头。
        if (!animate)
            return;

        if (selectedEntry != null)
        {
            Scheduler.AddDelayed(() =>
            {
                songList?.ScrollEntryIntoView(selectedEntry, false);
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
        diagnostics.Trace(
            "SONG_SELECT",
            "filter-applied",
            $"query={searchQuery} | mode={keyModeFilter?.ToString() ?? "all"}"
            + $" | visible={visibleEntries.Count}/{entries.Count}"
            + $" | difficulty-sort={sortByDifficulty}"
            + $" | packages-collapsed={packagesCollapsed}");

        if (sortByDifficulty)
        {
            visibleEntries = visibleEntries
                             .OrderBy(entry => entry.PackageName, StringComparer.OrdinalIgnoreCase)
                             .ThenBy(entry =>
                                 selectedDifficultyValue(entry)
                                 ?? double.MaxValue)
                             .ThenBy(entry => entry.Beatmap.Title, StringComparer.OrdinalIgnoreCase)
                             .ToList();
        }

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

        bool changed = selectedEntry != entry;
        selectedEntry = entry;
        ensurePlayableBeatmap(selectedEntry);
        rememberSelectedEntry();

        if (changed)
        {
            diagnostics.Trace(
                "SONG_SELECT",
                "selection-changed",
                $"title={entry.Beatmap.Title} | artist={entry.Beatmap.Artist}"
                + $" | difficulty={entry.Beatmap.DifficultyName}"
                + $" | keys={(int)entry.Beatmap.KeyMode}"
                + $" | format={entry.Beatmap.SourceFormat}");
            crossFadeBackground(textureFor(entry));
            rebuildDetails();
            modSettingsHost?.SetState(selectedMods, entry.Beatmap);
            playSelectedPreview();
        }

        if (rebuildList)
        {
            songList?.UpdateSelection(entry);
            songList?.ScrollEntryIntoView(entry, true);
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

    private void playSelectedPreview()
    {
        if (!previewActive)
            return;

        previewHost?.AdoptPreview(selectedEntry?.Beatmap);
        previewPlayer?.Play(selectedEntry?.Beatmap, selectedMods);
    }

    private bool ensurePlayableBeatmap(SongSelectEntry entry)
    {
        if (entry == null || !entry.IsReadOnly)
            return entry != null;

        try
        {
            YokkoBeatmap playable = importedChartLibrary.GetPlayableBeatmap(
                entry.ChartId);
            if (playable == null)
                return false;

            entry.Beatmap = playable;
            return true;
        }
        catch (Exception exception)
        {
            Logger.Error(
                exception,
                $"Could not materialise external chart '{entry.ChartId}'.",
                LoggingTarget.Runtime);
            return false;
        }
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

    private void refreshSavedScores()
    {
        for (int i = 0; i < entries.Count; i++)
        {
            SongSelectEntry entry = entries[i];
            StoredGameplayScore saved = scoreStore.GetBest(
                entry.Beatmap,
                selectedMods,
                gameplaySettings.GetJudgementConfiguration());
            IEnumerable<SongSelectScore> ranking = saved == null
                ? entry.Ranking
                : entry.Ranking.Where(score => !score.IsCurrentPlayer);

            if (saved != null)
            {
                ranking = ranking.Append(new SongSelectScore(
                                   0,
                                   "MOCHI",
                                   "yokko",
                                   saved.Rank,
                                   (int)Math.Min(int.MaxValue, saved.Score),
                                   saved.Accuracy,
                                   saved.MaxCombo,
                                   saved.ModLabels,
                                   true,
                                   saved.PlayedAt))
                                 .OrderByDescending(score => score.Score);
            }

            SongSelectScore[] ranked = ranking
                               .Select((score, rank) => score with
                               {
                                   Rank = rank + 1,
                               })
                               .ToArray();

            SongSelectScore[] history = scoreStore.GetHistory(
                                                   entry.Beatmap,
                                                   gameplaySettings.GetJudgementConfiguration(),
                                                   30)
                                               .Select((score, rank) =>
                                                   new SongSelectScore(
                                                       rank + 1,
                                                       "MOCHI",
                                                       "yokko",
                                                       score.Rank,
                                                       (int)Math.Min(int.MaxValue, score.Score),
                                                       score.Accuracy,
                                                       score.MaxCombo,
                                                       score.ModLabels,
                                                       true,
                                                       score.PlayedAt))
                                               .ToArray();

            SongSelectEntry refreshed = entry with
            {
                BestScore = saved == null
                    ? 0
                    : (int)Math.Min(int.MaxValue, saved.Score),
                BestAccuracy = saved?.Accuracy ?? 0,
                Ranking = ranked,
                History = history,
            };
            entries[i] = refreshed;
            if (entry.ChartId != null
                && importedEntries.TryGetValue(
                    entry.ChartId,
                    out SongSelectEntry tracked)
                && ReferenceEquals(tracked, entry))
            {
                importedEntries[entry.ChartId] = refreshed;
            }
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

    private void onDifficultyRatingModeChanged(
        ValueChangedEvent<ManiaDifficultyRatingMode> _)
    {
        applyFilters();
        rebuildDetails();
    }

    private void refreshSongListDifficulties()
    {
        if (sortByDifficulty)
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

    private void synchroniseImportedCharts(bool selectNewest = false)
    {
        (long revision, IReadOnlyList<ImportedChart> charts) =
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

        libraryRevision = revision;

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

    private void toggleSortMode()
    {
        sortByDifficulty = !sortByDifficulty;
        sortButton?.SetValue(sortByDifficulty ? "DIFFICULTY" : "TITLE");
        applyFilters();
    }

    private void togglePackageVisibility()
    {
        packagesCollapsed = !packagesCollapsed;
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

        rebuildSongList(animate: false);
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
        Width = 900,
        Colour = new Color4(1f, 0.995f, 0.972f, 0.18f),
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
            createDemoRanking(),
            [],
            imported.PackageId,
            imported.PackageName,
            imported.IsPackage,
            imported.Id,
            imported.IsReadOnly);
    }

    private static IReadOnlyList<SongSelectScore> createDemoRanking() =>
    [
        new(1, "RIN", "SongSelect/Avatars/rin", ScoreRank.S, 2_845_901, 0.9972, 1842, ["HD"]),
        new(2, "MIKA", "SongSelect/Avatars/mika", ScoreRank.S, 2_731_550, 0.9928, 1756, ["DT"]),
        new(3, "NANA", "SongSelect/Avatars/nana", ScoreRank.S, 2_698_234, 0.9891, 1689, ["HR"]),
        new(4, "LUNA", "SongSelect/Avatars/luna", ScoreRank.A, 2_554_700, 0.9764, 1542, []),
        new(5, "AOI", "SongSelect/Avatars/aoi", ScoreRank.A, 2_432_190, 0.9682, 1430, ["MR"]),
        new(6, "MOCHI", "yokko", ScoreRank.A, 2_398_420, 0.9621, 1388, ["HD"], true),
        new(7, "YUKI", "SongSelect/Avatars/aoi", ScoreRank.A, 2_287_110, 0.9568, 1321, []),
    ];

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

}
