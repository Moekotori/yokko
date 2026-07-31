using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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
using Yokko.Game.Gameplay;
using Yokko.Game.Importing;
using Yokko.Game.Input;
using Yokko.Game.Localisation;
using Yokko.Game.Presentation;
using Yokko.Game.Resources;
using Yokko.Game.Skinning.OsuMania;
using Yokko.Game.Scoring;
using Yokko.Import;
using Yokko.Import.Osu;
using Yokko.Resources;
using Yokko.Core.Beatmaps;
using Yokko.Core.Mods;

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
        private readonly YokkoAudioSettings audioSettings = new();
        [Cached]
        private readonly YokkoImportSettings importSettings = new();
        [Cached]
        private readonly ImportedChartLibrary importedChartLibrary = new();
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
        private readonly OsuManiaSkinCache gameplaySkinCache = new();
        [Cached]
        private readonly KeyInputTimestampSource keyInputTimestamps;
        [Cached]
        private readonly GameplayScoreStore scoreStore = new();
        [Cached]
        private readonly GameplayReplayStore replayStore = new();
        [Cached]
        private readonly YokkoManiaModPreferences modPreferences = new();
        [Cached]
        private readonly YokkoFrameRateAdaptation frameRateAdaptation = new();
        private ImportNotificationOverlay importOverlay;
        [Cached]
        private YokkoConfigManager yokkoConfig;
        private YokkoFrameRateController frameRateController;
        private YokkoWindowSizeGuard windowSizeGuard;
        private IWindow window;

        protected YokkoDisplaySettings DisplaySettings => displaySettings;
        internal ImportedChartLibrary ImportedCharts =>
            importedChartLibrary;

        protected YokkoGameBase(IKeyInputTimestampBackend keyInputTimestampBackend = null)
        {
            keyInputTimestamps = new KeyInputTimestampSource(keyInputTimestampBackend);

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
                [FrameworkSetting.WindowMode] = WindowMode.Fullscreen,
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
            yokkoConfig ??= new YokkoConfigManager(host.Storage);
            yokkoConfig.BindAudioSettings(audioSettings);
            yokkoConfig.BindDisplaySettings(displaySettings);
            yokkoConfig.BindImportSettings(importSettings);
            yokkoConfig.BindResourceSettings(resourceSettings);
            yokkoConfig.BindGameplaySettings(gameplaySettings);
            yokkoConfig.BindModPreferences(modPreferences);
            yokkoConfig.BindSkinSettings(skinSettings);
            resourceStorage.Initialise(
                host.Storage,
                resourceSettings,
                importSettings,
                importedChartLibrary,
                skinLibrary,
                skinSettings);
            scoreStore.Initialise(host.Storage);
            replayStore.Initialise(host.Storage);

            window = host.Window;
            keyInputTimestamps.Attach(window);
            Logger.Log(
                "Input timestamp backend: "
                + (keyInputTimestamps.IsRawInputAvailable
                    ? "Windows Raw Input"
                    : "SDL window fallback"));

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
            windowSizeGuard = new YokkoWindowSizeGuard(
                windowedSize,
                currentDisplayMode,
                () => host.Window?.Scale ?? 1,
                (requested, corrected) => Logger.Log(
                    $"Repaired unsafe window size {requested.Width}x{requested.Height} "
                    + $"to {corrected.Width}x{corrected.Height}.",
                    LoggingTarget.Runtime,
                    LogLevel.Important));

            frameworkConfig.SetValue(
                FrameworkSetting.ExecutionMode,
                ExecutionMode.MultiThreaded);
            YokkoLatencyThreadPolicy.Apply(host);
            frameRateController = new YokkoFrameRateController(
                frameworkConfig,
                displaySettings.FrameLimit,
                currentDisplayMode,
                frameRateAdaptation);

            string configuredLocale = frameworkConfig.Get<string>(FrameworkSetting.Locale);
            string normalizedLocale = YokkoLocale.Normalize(configuredLocale);
            if (configuredLocale != normalizedLocale)
                frameworkConfig.SetValue(FrameworkSetting.Locale, normalizedLocale);

            var resources = new DllResourceStore(typeof(YokkoResources).Assembly);
            Resources.AddStore(resources);
            AddFont(Resources, @"Fonts/Yokko/Yokko");
            AddFont(Resources, @"Fonts/Yokko/Yokko-Bold");
            AddFont(Resources, @"Fonts/YokkoInput/YokkoInput");
            AddFont(Resources, @"Fonts/ArchivoBlack/ArchivoBlack");
            _ = Task.Run(() => importedChartLibrary.LoadFromDiskAsync(
                importSettings.PreferKeysounds.Value,
                importSettings.PreferSscSimfiles.Value,
                importSettings.EnableBmsScratch.Value))
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

                yokkoConfig?.Dispose();
                frameRateController?.Dispose();
                windowSizeGuard?.Dispose();
                keyInputTimestamps.Dispose();
            }

            base.Dispose(isDisposing);

            if (isDisposing)
                gameplaySkinCache.Dispose();
        }

        private void onFileDropped(string path) => OpenExternalPath(path);

        protected void OpenExternalPath(string path)
        {
            if (Path.GetExtension(path).Equals(
                    YokkoReplayIO.FileExtension,
                    StringComparison.OrdinalIgnoreCase))
            {
                importYokkoReplay(path);
                return;
            }

            if (Path.GetExtension(path).Equals(
                    ".osr",
                    StringComparison.OrdinalIgnoreCase))
            {
                importOsuReplay(path);
                return;
            }

            if (KnownChartImporters.CanImport(path))
            {
                importChart(path);
                return;
            }

            if (OsuManiaSkinLibrary.IsSupportedDrop(path))
                importSkin(path);
        }

        private void onWindowResized() => windowSizeGuard?.Repair();

        private void importOsuReplay(string path)
        {
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

                    return (
                        Beatmap: chart.Result.Beatmap,
                        Replay: replay,
                        PlayerName: osuReplay.PlayerName);
                })
                .ContinueWith(
                    task => Scheduler.Add(() =>
                    {
                        if (!task.IsCompletedSuccessfully)
                        {
                            importOverlay.ShowFailure(
                                YokkoStrings.Get("import.replay.failed"),
                                task.Exception?.GetBaseException().Message
                                ?? "Unknown replay import error.");
                            return;
                        }

                        importOverlay.ShowSuccess(
                            YokkoStrings.Get("import.replay.success"),
                            string.IsNullOrWhiteSpace(task.Result.PlayerName)
                                ? task.Result.Beatmap.Title
                                : $"{task.Result.Beatmap.Title} · {task.Result.PlayerName}");
                        OpenImportedReplay(
                            task.Result.Beatmap,
                            task.Result.Replay);
                    }),
                    TaskScheduler.Default);
        }

        private void importYokkoReplay(string path)
        {
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

                    return (
                        Beatmap: chart.Result.Beatmap,
                        loaded.Replay);
                })
                .ContinueWith(
                    task => Scheduler.Add(() =>
                    {
                        if (!task.IsCompletedSuccessfully)
                        {
                            importOverlay.ShowFailure(
                                YokkoStrings.Get("import.replay.failed"),
                                task.Exception?.GetBaseException().Message
                                ?? "Unknown replay import error.");
                            return;
                        }

                        importOverlay.ShowSuccess(
                            YokkoStrings.Get("import.replay.success"),
                            task.Result.Beatmap.Title);
                        OpenImportedReplay(
                            task.Result.Beatmap,
                            task.Result.Replay);
                    }),
                    TaskScheduler.Default);
        }

        private protected virtual void OpenImportedReplay(
            Yokko.Core.Beatmaps.YokkoBeatmap beatmap,
            GameplayReplay replay)
        {
        }

        private void importChart(string path)
        {
            var request = new ChartImportRequest(
                path,
                importSettings.PreferKeysounds.Value,
                importSettings.PreferSscSimfiles.Value,
                importSettings.EnableBmsScratch.Value);

            Scheduler.Add(() => importOverlay.ShowImporting(
                YokkoStrings.Get("import.chart.importing"),
                path));

            _ = Task.Run(async () => await importedChartLibrary.ImportAsync(request))
                    .ContinueWith(
                        task => Scheduler.Add(() =>
                        {
                            if (!task.IsCompletedSuccessfully)
                            {
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
                        }),
                        TaskScheduler.Default);
        }

        private void importSkin(string path)
        {
            Scheduler.Add(() => importOverlay.ShowImporting(
                YokkoStrings.Get("settings.skins.importing"),
                path));

            _ = Task.Run(() => skinLibrary.Import(path))
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
                                importOverlay.ShowSuccess(
                                    YokkoStrings.Get("settings.skins.import_success"),
                                    result.Skin?.Name ?? result.Message);
                            }
                            else
                            {
                                importOverlay.ShowFailure(
                                    YokkoStrings.Get("settings.skins.import_failed"),
                                    result.Message);
                            }
                        }),
                        TaskScheduler.Default);
        }

    }
}
