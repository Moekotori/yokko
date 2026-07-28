using System.Collections.Generic;
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
using Yokko.Resources;

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
        private readonly KeyInputTimestampSource keyInputTimestamps;
        [Cached]
        private readonly GameplayScoreStore scoreStore = new();
        private ImportNotificationOverlay importOverlay;
        [Cached]
        private YokkoConfigManager yokkoConfig;
        private YokkoFrameRateController frameRateController;
        private IWindow window;

        protected YokkoGameBase(IKeyInputTimestampBackend keyInputTimestampBackend = null)
        {
            keyInputTimestamps = new KeyInputTimestampSource(keyInputTimestampBackend);

            // Ensure game and tests scale with window size and screen DPI.
            base.Content.Add(Content = new DrawSizePreservingFillContainer
            {
                TargetDrawSize = YokkoDisplaySettings.TargetDrawSize,
                Strategy = DrawSizePreservationStrategy.Minimum,
            });
        }

        protected override LocalisationManager CreateLocalisationManager(FrameworkConfigManager frameworkConfig) =>
            YokkoLocalisation.Create(frameworkConfig);

        protected override IDictionary<FrameworkSetting, object> GetFrameworkConfigDefaults() =>
            new Dictionary<FrameworkSetting, object>
            {
                [FrameworkSetting.Locale] = YokkoLocale.English,
            };

        public override void SetHost(GameHost host)
        {
            if (window != null)
                window.DragDrop -= onFileDropped;

            base.SetHost(host);
            yokkoConfig ??= new YokkoConfigManager(host.Storage);
            yokkoConfig.BindAudioSettings(audioSettings);
            yokkoConfig.BindDisplaySettings(displaySettings);
            yokkoConfig.BindImportSettings(importSettings);
            yokkoConfig.BindResourceSettings(resourceSettings);
            yokkoConfig.BindGameplaySettings(gameplaySettings);
            yokkoConfig.BindSkinSettings(skinSettings);
            resourceStorage.Initialise(
                host.Storage,
                resourceSettings,
                importSettings,
                importedChartLibrary,
                skinLibrary,
                skinSettings);
            scoreStore.Initialise(host.Storage);

            window = host.Window;
            keyInputTimestamps.Attach(window);
            Logger.Log(
                "Input timestamp backend: "
                + (keyInputTimestamps.IsRawInputAvailable
                    ? "Windows Raw Input"
                    : "SDL window fallback"));

            if (window != null)
                window.DragDrop += onFileDropped;
        }

        [BackgroundDependencyLoader]
        private void load(
            FrameworkConfigManager frameworkConfig,
            GameHost host)
        {
            base.Content.Add(
                importOverlay = new ImportNotificationOverlay());

            // The framework's first FrameSync mode is VSync, not a true
            // refresh-rate cap. Keep it disabled and apply Yokko's explicit
            // draw/update limits so a missed present cannot fall to a fraction
            // of the display refresh rate.
            frameworkConfig.SetValue(
                FrameworkSetting.FrameSync,
                FrameSync.Unlimited);
            frameRateController = new YokkoFrameRateController(
                host,
                displaySettings.FrameLimit,
                host.Window?.CurrentDisplayMode
                ?? new Bindable<DisplayMode>(new DisplayMode(
                    null,
                    new System.Drawing.Size(0, 0),
                    0,
                    60,
                    0)));

            string configuredLocale = frameworkConfig.Get<string>(FrameworkSetting.Locale);
            string normalizedLocale = YokkoLocale.Normalize(configuredLocale);
            if (configuredLocale != normalizedLocale)
                frameworkConfig.SetValue(FrameworkSetting.Locale, normalizedLocale);

            var resources = new DllResourceStore(typeof(YokkoResources).Assembly);
            Resources.AddStore(resources);
            AddFont(Resources, @"Fonts/Yokko/Yokko");
            AddFont(Resources, @"Fonts/Yokko/Yokko-Bold");

            _ = Task.Run(() => importedChartLibrary.LoadFromDiskAsync(
                importSettings.PreferKeysounds.Value,
                importSettings.PreferSscSimfiles.Value))
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
                    window.DragDrop -= onFileDropped;

                yokkoConfig?.Dispose();
                frameRateController?.Dispose();
                keyInputTimestamps.Dispose();
            }

            base.Dispose(isDisposing);
        }

        private void onFileDropped(string path)
        {
            if (KnownChartImporters.CanImport(path))
            {
                importChart(path);
                return;
            }

            if (OsuManiaSkinLibrary.IsSupportedDrop(path))
                importSkin(path);
        }

        private void importChart(string path)
        {
            var request = new ChartImportRequest(
                path,
                importSettings.PreferKeysounds.Value,
                importSettings.PreferSscSimfiles.Value);

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
