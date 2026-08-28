using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Configuration;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.IO.Stores;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osuTK;
using Yokko.Game.Audio;
using Yokko.Game.Configuration;
using Yokko.Game.Diagnostics;
using Yokko.Game.Gameplay;
using Yokko.Game.Importing;
using Yokko.Game.Input;
using Yokko.Game.Localisation;
using Yokko.Game.Presentation;
using Yokko.Game.Resources;
using Yokko.Game.Screens.SongSelect;
using Yokko.Game.Skinning.OsuMania;
using Yokko.Game.Scoring;
using Yokko.Import;
using Yokko.Import.Malody;
using Yokko.Import.Osu;
using Yokko.Resources;
using Yokko.Core.Beatmaps;
using Yokko.Core.Mods;
using Yokko.Core.Scoring;

namespace Yokko.Game
{
    public partial class YokkoGameBase : osu.Framework.Game
    {
        // Anything in this class is shared between the test browser and the game implementation.
        // It allows for caching global dependencies that should be accessible to tests, or changing
        // the screen scaling for all components including the test browser and framework overlays.

        protected override Container<Drawable> Content { get; }

        [Cached]
        private readonly YokkoDisplaySettings displaySettings = new();
        [Cached]
        private readonly YokkoUiThemeStore uiTheme = new();
        [Cached]
        private readonly YokkoAudioSettings audioSettings = new();
        [Cached]
        private readonly YokkoImportSettings importSettings = new();
        [Cached]
        private readonly ImportedChartLibrary importedChartLibrary = new();
        [Cached]
        private readonly YokkoExternalOsuSettings externalOsuSettings = new();
        [Cached]
        private readonly YokkoResourceSettings resourceSettings = new();
        [Cached]
        private readonly YokkoResourceStorage resourceStorage = new();
        [Cached]
        private readonly YokkoGameplaySettings gameplaySettings = new();
        [Cached]
        private readonly YokkoSkinSettings skinSettings = new();
        [Cached]
        private readonly OsuManiaSkinLibrary skinLibrary = new();
        [Cached]
        private readonly SkinHudLayoutStore skinHudLayoutStore = new();
        [Cached]
        private readonly OsuManiaSkinCache gameplaySkinCache = new();
        [Cached]
        private readonly SongSelectArtworkTextureCache songSelectArtworkTextureCache = new();
        [Cached]
        private readonly KeyInputTimestampSource keyInputTimestamps;
        [Cached]
        private readonly GameplayScoreStore scoreStore = new();
        [Cached]
        private readonly GameplayReplayStore replayStore = new();
        [Cached]
        private readonly YokkoManiaModPreferences modPreferences = new();
        [Cached]
        private readonly YokkoAccessibilitySettings accessibilitySettings = new();
        [Cached]
        private readonly YokkoEditorSettings editorSettings = new();
        [Cached]
        private readonly YokkoPrivacySettings privacySettings = new();
        [Cached]
        private readonly YokkoStartupSettings startupSettings = new();
        [Cached]
        private readonly YokkoFrameRateAdaptation frameRateAdaptation = new();
        [Cached]
        private readonly YokkoDiagnostics diagnostics = new();
        [Cached(typeof(IResourceDirectoryPicker))]
        private readonly IResourceDirectoryPicker resourceDirectoryPicker;
        [Cached]
        private GameHost gameHost;
        [Cached(typeof(IDesktopDisplayModeController))]
        private readonly IDesktopDisplayModeController displayModeController;
        private ImportNotificationOverlay importOverlay;
        [Cached]
        private YokkoConfigManager yokkoConfig;
        private YokkoFrameRateController frameRateController;
        private YokkoDesktopBehaviourController desktopBehaviourController;
        private YokkoWindowSizeGuard windowSizeGuard;
        private IWindow window;

        protected YokkoDisplaySettings DisplaySettings => displaySettings;
        protected YokkoGameplaySettings GameplaySettings => gameplaySettings;
        protected YokkoAudioSettings AudioSettings => audioSettings;
        protected YokkoUiThemeStore UiThemeStore => uiTheme;
        internal YokkoDiagnostics Diagnostics => diagnostics;
        internal ImportedChartLibrary ImportedCharts =>
            importedChartLibrary;
        internal GameplayScoreStore ScoreStoreForTesting => scoreStore;
        internal GameplayReplayStore ReplayStoreForTesting => replayStore;

        protected YokkoGameBase(
            IKeyInputTimestampBackend keyInputTimestampBackend = null,
            IResourceDirectoryPicker resourceDirectoryPicker = null,
            IDesktopDisplayModeController displayModeController = null)
        {
            keyInputTimestamps = new KeyInputTimestampSource(keyInputTimestampBackend);
            this.resourceDirectoryPicker = resourceDirectoryPicker
                                           ?? new UnavailableResourceDirectoryPicker();
            this.displayModeController = displayModeController
                                         ?? new UnavailableDesktopDisplayModeController();

            // Ensure game and tests scale with window size and screen DPI.
            base.Content.Add(Content = new YokkoUiScalingContainer(
                displaySettings.UiScale));
        }

        protected override LocalisationManager CreateLocalisationManager(FrameworkConfigManager frameworkConfig) =>
            YokkoLocalisation.Create(frameworkConfig);

        protected override IDictionary<FrameworkSetting, object> GetFrameworkConfigDefaults() =>
            new Dictionary<FrameworkSetting, object>
            {
                // FrameworkConfigManager applies this only when no persisted
                // locale exists, so first launch follows the operating system
                // while every later launch respects the user's saved choice.
                [FrameworkSetting.Locale] =
                    YokkoLocale.FromSystemCulture(CultureInfo.CurrentUICulture),
                [FrameworkSetting.ExecutionMode] =
                    ExecutionMode.MultiThreaded,
                // Windowed mode starts at Yokko's normal 16:9 aspect ratio.
                // Existing installations continue to use their persisted size.
                [FrameworkSetting.WindowedSize] =
                    new System.Drawing.Size(1280, 720),
                [FrameworkSetting.WindowMode] = WindowMode.Fullscreen,
                // Keep exclusive fullscreen active while Windows switches
                // focus so Alt+Tab does not wait for an extra minimise/restore.
                [FrameworkSetting.MinimiseOnFocusLossInFullscreen] = false,
                [FrameworkSetting.FrameSync] =
                    YokkoFrameRateLimits.ToFrameworkFrameSync(
                        YokkoFrameRateLimits.LowLatencyDefault),
            };

        public override void SetHost(GameHost host)
        {
            if (window != null)
            {
                window.DragDrop -= onFileDropped;
                window.Resized -= onWindowResized;
            }

            base.SetHost(host);
            gameHost = host;
            yokkoConfig ??= new YokkoConfigManager(host.Storage);
            yokkoConfig.BindAudioSettings(audioSettings);
            yokkoConfig.BindDisplaySettings(displaySettings);
            yokkoConfig.BindDiagnosticSettings(diagnostics);
            yokkoConfig.BindImportSettings(importSettings);
            yokkoConfig.BindExternalOsuSettings(externalOsuSettings);
            yokkoConfig.BindResourceSettings(resourceSettings);
            yokkoConfig.BindGameplaySettings(gameplaySettings);
            yokkoConfig.BindModPreferences(modPreferences);
            yokkoConfig.BindSkinSettings(skinSettings);
            yokkoConfig.BindAccessibilitySettings(accessibilitySettings);
            yokkoConfig.BindEditorSettings(editorSettings);
            yokkoConfig.BindPrivacySettings(privacySettings);
            yokkoConfig.BindStartupSettings(startupSettings);
            resourceStorage.Initialise(
                host.Storage,
                resourceSettings,
                importSettings,
                importedChartLibrary,
                skinLibrary,
                skinSettings);
            skinHudLayoutStore.Initialise(
                host.Storage,
                gameplaySettings,
                skinSettings,
                skinLibrary);
            importedChartLibrary.ConfigureExternalOsu(
                host.Storage,
                externalOsuSettings);
            importedChartLibrary.ConfigureWatchFolder(importSettings);
            scoreStore.Initialise(host.Storage);
            replayStore.Initialise(host.Storage);
            diagnostics.Initialise(host);

            window = host.Window;
            keyInputTimestamps.Attach(window);
            Logger.Log(
                "Input timestamp backend: "
                + (keyInputTimestamps.IsRawInputAvailable
                    ? "Windows Raw Input"
                    : "SDL window fallback"));
            diagnostics.Trace(
                "HOST",
                "attached",
                $"host={host.GetType().Name} | window={window?.GetType().Name ?? "none"}"
                + $" | scale={window?.Scale ?? 1:0.###}"
                + $" | raw-input={keyInputTimestamps.IsRawInputAvailable}");

            if (window != null)
            {
                window.DragDrop += onFileDropped;
                window.Resized += onWindowResized;
            }
        }

        [BackgroundDependencyLoader]
        private void load(
            FrameworkConfigManager frameworkConfig,
            GameHost host)
        {
            base.Content.Add(
                importOverlay = new ImportNotificationOverlay());

            Bindable<System.Drawing.Size> windowedSize =
                frameworkConfig.GetBindable<System.Drawing.Size>(
                    FrameworkSetting.WindowedSize);
            IBindable<DisplayMode> currentDisplayMode =
                host.Window?.CurrentDisplayMode
                ?? new Bindable<DisplayMode>(new DisplayMode(
                    null,
                    windowedSize.Value,
                    0,
                    60,
                    0));
            YokkoLatencyThreadPolicy.Apply(host);
            frameRateController = new YokkoFrameRateController(
                frameworkConfig,
                displaySettings.FrameLimit,
                currentDisplayMode,
                frameRateAdaptation);

            if (YokkoPlatformCapabilities.SupportsWindowManagement)
            {
                windowSizeGuard = new YokkoWindowSizeGuard(
                    windowedSize,
                    currentDisplayMode,
                    () => host.Window?.Scale ?? 1,
                    (requested, corrected) => Logger.Log(
                        $"Repaired unsafe window size {requested.Width}x{requested.Height} "
                        + $"to {corrected.Width}x{corrected.Height}.",
                        LoggingTarget.Runtime,
                        LogLevel.Important));
                desktopBehaviourController = new YokkoDesktopBehaviourController(
                    host,
                    frameworkConfig,
                    displaySettings,
                    audioSettings,
                    displayModeController,
                    yokkoConfig);
            }

            string configuredLocale = frameworkConfig.Get<string>(FrameworkSetting.Locale);
            string normalizedLocale = YokkoLocale.Normalize(configuredLocale);
            if (configuredLocale != normalizedLocale)
                frameworkConfig.SetValue(FrameworkSetting.Locale, normalizedLocale);

            var resources = new DllResourceStore(typeof(YokkoResources).Assembly);
            Resources.AddStore(resources);
            AddFont(Resources, @"Fonts/PlusJakartaSans/PlusJakartaSans-Regular");
            AddFont(Resources, @"Fonts/PlusJakartaSans/PlusJakartaSans");
            AddFont(Resources, @"Fonts/PlusJakartaSans/PlusJakartaSans-SemiBold");
            AddFont(Resources, @"Fonts/PlusJakartaSans/PlusJakartaSans-Bold");
            AddFont(Resources, @"Fonts/Noto/Noto-Basic");
            AddFont(Resources, @"Fonts/Noto/Noto-Bopomofo");
            AddFont(Resources, @"Fonts/Noto/Noto-CJK-Basic");
            AddFont(Resources, @"Fonts/Noto/Noto-CJK-Compatibility");
            AddFont(Resources, @"Fonts/Noto/Noto-Hangul");
            AddFont(Resources, @"Fonts/Noto/Noto-Thai");
            _ = importedChartLibrary.BeginStartupLoad(
                importSettings.PreferKeysounds.Value,
                importSettings.PreferSscSimfiles.Value,
                importSettings.EnableBmsScratch.Value)
                    .ContinueWith(
                        task =>
                        {
                            if (!task.IsCompletedSuccessfully)
                            {
                                Logger.Log(
                                    $"Could not scan persistent beatmaps: {task.Exception?.GetBaseException().Message}",
                                    LoggingTarget.Runtime,
                                    LogLevel.Error);
                            }
                        },
                        TaskScheduler.Default);

            diagnostics.Trace(
                "CONFIG",
                "loaded",
                $"locale={normalizedLocale}"
                + $" | frame-limit={displaySettings.FrameLimit.Value}"
                + $" | ui-scale={displaySettings.UiScale.Value}"
                + $" | audio-backend={audioSettings.PreferredBackend.Value}"
                + $" | audio-buffer={audioSettings.PreferredBufferSize.Value}"
                + $" | keysounds={gameplaySettings.KeysoundsEnabled.Value}"
                + $" | skin={skinSettings.SelectedSkinId.Value}");
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            desktopBehaviourController?.RestoreWindowState();
            desktopBehaviourController?.EnsureWindowFrameVisible();
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                if (window != null)
                {
                    window.DragDrop -= onFileDropped;
                    window.Resized -= onWindowResized;
                }

                skinHudLayoutStore.Dispose();
                yokkoConfig?.Dispose();
                frameRateController?.Dispose();
                desktopBehaviourController?.Dispose();
                windowSizeGuard?.Dispose();
                keyInputTimestamps.Dispose();
                diagnostics.Dispose();
            }

            base.Dispose(isDisposing);

            if (isDisposing)
            {
                gameplaySkinCache.Dispose();
                songSelectArtworkTextureCache.Dispose();
                importedChartLibrary.Dispose();
            }
        }

        private void onFileDropped(string path)
        {
            diagnostics.Trace("FILES", "dropped", path);
            OpenExternalPath(path, playImportedReplay: false);
        }

        protected void OpenExternalPath(
            string path,
            bool playImportedReplay = true)
        {
            diagnostics.Trace(
                "FILES",
                "open-requested",
                $"path={path} | extension={Path.GetExtension(path)}");

            if (Path.GetExtension(path).Equals(
                    YokkoReplayIO.FileExtension,
                    StringComparison.OrdinalIgnoreCase))
            {
                importYokkoReplay(path, playImportedReplay);
                return;
            }

            if (Path.GetExtension(path).Equals(
                    ".osr",
                    StringComparison.OrdinalIgnoreCase))
            {
                importOsuReplay(path, playImportedReplay);
                return;
            }

            if (Path.GetExtension(path).Equals(
                    MalodyReplayIO.FileExtension,
                    StringComparison.OrdinalIgnoreCase))
            {
                importMalodyReplay(path, playImportedReplay);
                return;
            }

            if (KnownChartImporters.CanImport(path))
            {
                importChart(path);
                return;
            }

            if (OsuManiaSkinLibrary.IsSupportedDrop(path))
            {
                importSkin(path);
                return;
            }

            diagnostics.Trace("FILES", "unsupported", path, LogLevel.Important);
        }

        private void onWindowResized()
        {
            diagnostics.Trace("WINDOW", "resized", $"scale={window?.Scale ?? 1:0.###}");
            windowSizeGuard?.Repair();
        }

        private void importOsuReplay(
            string path,
            bool playImportedReplay)
        {
            diagnostics.Trace("IMPORT", "osu-replay-started", path);
            Scheduler.Add(() => importOverlay.ShowImporting(
                YokkoStrings.Get("import.replay.importing"),
                path));

            _ = Task.Run(() =>
                {
                    OsuReplay osuReplay = OsuReplayIO.ReadFromFile(path);
                    ImportedChart chart =
                        importedChartLibrary.FindBySourceHash(
                            osuReplay.BeatmapHash)
                        ?? throw new InvalidDataException(
                            "Import the matching osu!mania beatmap before its replay.");
                    GameplayReplay replay = GameplayReplay.FromOsuReplay(
                        osuReplay,
                        (int)chart.Result.Beatmap.KeyMode);
                    YokkoBeatmap applied = ManiaBeatmapModTransformer.Apply(
                        chart.Result.Beatmap,
                        replay.Mods);
                    DateTimeOffset playedAt =
                        osuReplay.PlayedAt ?? DateTimeOffset.UtcNow;
                    string replayPath = replayStore.Save(
                        chart.Result.Beatmap,
                        applied,
                        replay,
                        chart.Result.SourceHash,
                        playedAt);

                    return (
                        ChartId: chart.Id,
                        Beatmap: chart.Result.Beatmap,
                        Replay: replay,
                        PlayerName: osuReplay.PlayerName,
                        Score: ExternalReplayScoreConverter.FromOsu(osuReplay),
                        ReplayPath: replayPath,
                        PlayedAt: playedAt,
                        ExternalScoreId: "osu:" + computeFileHash(path));
                })
                .ContinueWith(
                    task => Scheduler.Add(() =>
                    {
                        if (!task.IsCompletedSuccessfully)
                        {
                            diagnostics.Trace(
                                "IMPORT",
                                "osu-replay-failed",
                                task.Exception?.GetBaseException().ToString(),
                                LogLevel.Error);
                            importOverlay.ShowFailure(
                                YokkoStrings.Get("import.replay.failed"),
                                task.Exception?.GetBaseException().Message
                                ?? "Unknown replay import error.");
                            return;
                        }

                        bool imported = scoreStore.ImportExternalScore(
                            task.Result.Beatmap,
                            task.Result.Replay.Mods,
                            JudgementConfiguration.YokkoDefault,
                            task.Result.Score,
                            task.Result.PlayerName,
                            task.Result.PlayerName,
                            "osu",
                            task.Result.ExternalScoreId,
                            task.Result.ReplayPath,
                            task.Result.PlayedAt);
                        importOverlay.ShowSuccess(
                            YokkoStrings.Get("import.replay.success"),
                            string.IsNullOrWhiteSpace(task.Result.PlayerName)
                                ? task.Result.Beatmap.Title
                                : $"{task.Result.Beatmap.Title} · {task.Result.PlayerName}");
                        diagnostics.Trace(
                            "IMPORT",
                            "osu-replay-completed",
                            $"title={task.Result.Beatmap.Title} | player={task.Result.PlayerName}"
                            + $" | score={task.Result.Score.Score} | added={imported}");
                        OnReplayImported(task.Result.ChartId);
                        if (playImportedReplay)
                        {
                            OpenImportedReplay(
                                task.Result.Beatmap,
                                task.Result.Replay);
                        }
                    }),
                    TaskScheduler.Default);
        }

        private void importMalodyReplay(
            string path,
            bool playImportedReplay)
        {
            diagnostics.Trace("IMPORT", "malody-replay-started", path);
            Scheduler.Add(() => importOverlay.ShowImporting(
                YokkoStrings.Get("import.replay.importing"),
                path));

            _ = Task.Run(() =>
                {
                    MalodyReplay malodyReplay = MalodyReplayIO.ReadFromFile(path);
                    ImportedChart chart =
                        importedChartLibrary.FindBySourceHash(
                            malodyReplay.BeatmapHash)
                        ?? throw new InvalidDataException(
                            "Import the matching Malody Key beatmap before its replay.");
                    GameplayReplay replay = GameplayReplay.FromMalodyReplay(
                        malodyReplay,
                        (int)chart.Result.Beatmap.KeyMode);
                    YokkoBeatmap applied = ManiaBeatmapModTransformer.Apply(
                        chart.Result.Beatmap,
                        replay.Mods);
                    DateTimeOffset playedAt =
                        malodyReplay.PlayedAt ?? DateTimeOffset.UtcNow;
                    string replayPath = replayStore.Save(
                        chart.Result.Beatmap,
                        applied,
                        replay,
                        chart.Result.SourceHash,
                        playedAt);

                    return (
                        ChartId: chart.Id,
                        Beatmap: chart.Result.Beatmap,
                        Replay: replay,
                        Score: ExternalReplayScoreConverter.FromMalody(malodyReplay),
                        ReplayPath: replayPath,
                        PlayedAt: playedAt,
                        ExternalScoreId: "malody:" + computeFileHash(path));
                })
                .ContinueWith(
                    task => Scheduler.Add(() =>
                    {
                        if (!task.IsCompletedSuccessfully)
                        {
                            diagnostics.Trace(
                                "IMPORT",
                                "malody-replay-failed",
                                task.Exception?.GetBaseException().ToString(),
                                LogLevel.Error);
                            importOverlay.ShowFailure(
                                YokkoStrings.Get("import.replay.failed"),
                                task.Exception?.GetBaseException().Message
                                ?? "Unknown replay import error.");
                            return;
                        }

                        const string playerName = "MALODY PLAYER";
                        bool imported = scoreStore.ImportExternalScore(
                            task.Result.Beatmap,
                            task.Result.Replay.Mods,
                            JudgementConfiguration.YokkoDefault,
                            task.Result.Score,
                            playerName,
                            null,
                            "malody",
                            task.Result.ExternalScoreId,
                            task.Result.ReplayPath,
                            task.Result.PlayedAt);
                        importOverlay.ShowSuccess(
                            YokkoStrings.Get("import.replay.success"),
                            $"{task.Result.Beatmap.Title} · {playerName}");
                        diagnostics.Trace(
                            "IMPORT",
                            "malody-replay-completed",
                            $"title={task.Result.Beatmap.Title}"
                            + $" | score={task.Result.Score.Score} | added={imported}");
                        OnReplayImported(task.Result.ChartId);
                        if (playImportedReplay)
                        {
                            OpenImportedReplay(
                                task.Result.Beatmap,
                                task.Result.Replay);
                        }
                    }),
                    TaskScheduler.Default);
        }

        private static string computeFileHash(string path)
        {
            using FileStream stream = File.OpenRead(path);
            return Convert.ToHexString(SHA256.HashData(stream))
                          .ToLowerInvariant();
        }

        private void importYokkoReplay(
            string path,
            bool playImportedReplay)
        {
            diagnostics.Trace("IMPORT", "yokko-replay-started", path);
            Scheduler.Add(() => importOverlay.ShowImporting(
                YokkoStrings.Get("import.replay.importing"),
                path));

            _ = Task.Run(() =>
                {
                    YokkoReplayLoadResult loaded =
                        YokkoReplayIO.ReadFromFile(path);
                    ImportedChart chart =
                        !string.IsNullOrWhiteSpace(loaded.SourceHash)
                            ? importedChartLibrary.FindBySourceHash(
                                loaded.SourceHash)
                            : null;
                    chart ??=
                        importedChartLibrary.FindByBeatmapFingerprint(
                            loaded.BeatmapFingerprint)
                        ?? throw new InvalidDataException(
                            "Import the exact matching beatmap before its replay.");

                    string actualFingerprint =
                        YokkoBeatmapFingerprint.Compute(
                            chart.Result.Beatmap);
                    if (!string.Equals(
                            actualFingerprint,
                            loaded.BeatmapFingerprint,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidDataException(
                            "The imported beatmap does not exactly match this replay.");
                    }

                    YokkoBeatmap applied =
                        ManiaBeatmapModTransformer.Apply(
                            chart.Result.Beatmap,
                            loaded.Replay.Mods);
                    if ((int)applied.KeyMode != loaded.KeyCount)
                    {
                        throw new InvalidDataException(
                            "The replay key count does not match its restored Mod configuration.");
                    }

                    string replayPath = playImportedReplay
                        ? path
                        : saveDroppedYokkoReplay(
                            path,
                            chart.Result.Beatmap,
                            applied,
                            loaded);

                    return (
                        ChartId: chart.Id,
                        Beatmap: chart.Result.Beatmap,
                        loaded.Replay,
                        ReplayPath: replayPath);
                })
                .ContinueWith(
                    task => Scheduler.Add(() =>
                    {
                        if (!task.IsCompletedSuccessfully)
                        {
                            diagnostics.Trace(
                                "IMPORT",
                                "yokko-replay-failed",
                                task.Exception?.GetBaseException().ToString(),
                                LogLevel.Error);
                            importOverlay.ShowFailure(
                                YokkoStrings.Get("import.replay.failed"),
                                task.Exception?.GetBaseException().Message
                                ?? "Unknown replay import error.");
                            return;
                        }

                        importOverlay.ShowSuccess(
                            YokkoStrings.Get("import.replay.success"),
                            task.Result.Beatmap.Title);
                        diagnostics.Trace(
                            "IMPORT",
                            "yokko-replay-completed",
                            $"title={task.Result.Beatmap.Title} | inputs={task.Result.Replay.Inputs.Count}"
                            + $" | stored={task.Result.ReplayPath}");
                        OnReplayImported(task.Result.ChartId);
                        if (playImportedReplay)
                        {
                            OpenImportedReplay(
                                task.Result.Beatmap,
                                task.Result.Replay);
                        }
                    }),
                    TaskScheduler.Default);
        }

        private string saveDroppedYokkoReplay(
            string sourcePath,
            YokkoBeatmap beatmap,
            YokkoBeatmap appliedBeatmap,
            YokkoReplayLoadResult loaded)
        {
            string fullSourcePath = Path.GetFullPath(sourcePath);
            string replayDirectory = Path.GetFullPath(
                replayStore.ReplayDirectory);
            if (string.Equals(
                    Path.GetDirectoryName(fullSourcePath),
                    replayDirectory,
                    StringComparison.OrdinalIgnoreCase))
            {
                return fullSourcePath;
            }

            return replayStore.Save(
                beatmap,
                appliedBeatmap,
                loaded.Replay,
                loaded.SourceHash,
                loaded.RecordedAt);
        }

        private protected virtual void OpenImportedReplay(
            Yokko.Core.Beatmaps.YokkoBeatmap beatmap,
            GameplayReplay replay)
        {
        }

        private protected virtual void OnReplayImported(
            string chartId)
        {
        }

        private void importChart(string path)
        {
            var request = new ChartImportRequest(
                path,
                importSettings.PreferKeysounds.Value,
                importSettings.PreferSscSimfiles.Value,
                importSettings.EnableBmsScratch.Value);
            diagnostics.Trace(
                "IMPORT",
                "chart-started",
                $"path={path} | keysounds={request.PreferKeysounds}"
                + $" | prefer-ssc={request.PreferSscSimfiles}"
                + $" | bms-scratch={request.EnableBmsScratch}");

            Scheduler.Add(() => importOverlay.ShowImporting(
                YokkoStrings.Get("import.chart.importing"),
                path));

            _ = Task.Run(async () => await importedChartLibrary.ImportAsync(request))
                    .ContinueWith(
                        task => Scheduler.Add(() =>
                        {
                            if (!task.IsCompletedSuccessfully)
                            {
                                diagnostics.Trace(
                                    "IMPORT",
                                    "chart-failed",
                                    task.Exception?.GetBaseException().ToString(),
                                    LogLevel.Error);
                                importOverlay.ShowFailure(
                                    YokkoStrings.Get("import.chart.failed"),
                                    task.Exception?.GetBaseException().Message ?? "Unknown import error.");
                                return;
                            }

                            IReadOnlyList<ChartImportResult> results = task.Result;
                            LocalisableString detail;

                            if (results.Count > 1)
                            {
                                detail = YokkoStrings.Get(
                                    "import.chart.success_count",
                                    results.Count);
                            }
                            else
                            {
                                ChartImportResult result = results[0];
                                detail = result.Warnings.Count > 0
                                    && importSettings.ShowCompatibilityWarnings.Value
                                    ? $"{result.Beatmap.Title} · {result.Warnings[0]}"
                                    : result.Beatmap.Title;
                            }

                            importOverlay.ShowSuccess(
                                YokkoStrings.Get("import.chart.success"),
                                detail);
                            diagnostics.Trace(
                                "IMPORT",
                                "chart-completed",
                                $"charts={results.Count}"
                                + $" | warnings={results.Sum(result => result.Warnings.Count)}");
                        }),
                        TaskScheduler.Default);
        }

        private void importSkin(string path)
        {
            diagnostics.Trace("IMPORT", "skin-started", path);
            Scheduler.Add(() => importOverlay.ShowImporting(
                YokkoStrings.Get("settings.skins.importing"),
                path));

            _ = Task.Run(() => skinLibrary.Import(
                        path,
                        selectImportedSkin: false))
                    .ContinueWith(
                        task => Scheduler.Add(() =>
                        {
                            SkinImportResult result = task.IsCompletedSuccessfully
                                ? task.Result
                                : new SkinImportResult(
                                    false,
                                    task.Exception?.GetBaseException().Message ?? "Unknown import error.");

                            if (result.Success)
                            {
                                if (result.Skin != null)
                                    skinLibrary.Select(result.Skin.Id);
                                importOverlay.ShowSuccess(
                                    YokkoStrings.Get("settings.skins.import_success"),
                                    result.Skin?.Name ?? result.Message);
                                diagnostics.Trace(
                                    "IMPORT",
                                    "skin-completed",
                                    $"name={result.Skin?.Name ?? "unknown"}");
                            }
                            else
                            {
                                importOverlay.ShowFailure(
                                    YokkoStrings.Get("settings.skins.import_failed"),
                                    result.Message);
                                diagnostics.Trace(
                                    "IMPORT",
                                    "skin-failed",
                                    result.Message,
                                    LogLevel.Error);
                            }
                        }),
                        TaskScheduler.Default);
        }

    }
}
