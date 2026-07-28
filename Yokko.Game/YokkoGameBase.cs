using System.Collections.Generic;
using System.Threading.Tasks;
using osu.Framework.Allocation;
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
using Yokko.Game.Skinning.OsuMania;
using Yokko.Resources;

namespace Yokko.Game
{
    public partial class YokkoGameBase : osu.Framework.Game
    {
        // Anything in this class is shared between the test browser and the game implementation.
        // It allows for caching global dependencies that should be accessible to tests, or changing
        // the screen scaling for all components including the test browser and framework overlays.

        protected override Container<Drawable> Content { get; }

        private readonly DrawSizePreservingFillContainer scalingContainer;
        [Cached]
        private readonly YokkoDisplaySettings displaySettings = new();
        [Cached]
        private readonly YokkoAudioSettings audioSettings = new();
        [Cached]
        private readonly YokkoImportSettings importSettings = new();
        [Cached]
        private readonly YokkoGameplaySettings gameplaySettings = new();
        [Cached]
        private readonly YokkoSkinSettings skinSettings = new();
        [Cached]
        private readonly OsuManiaSkinLibrary skinLibrary = new();
        [Cached]
        private readonly KeyInputTimestampSource keyInputTimestamps;
        private SkinImportNotificationOverlay skinImportOverlay;
        [Cached]
        private YokkoConfigManager yokkoConfig;
        private IWindow window;

        protected YokkoGameBase(IKeyInputTimestampBackend keyInputTimestampBackend = null)
        {
            keyInputTimestamps = new KeyInputTimestampSource(keyInputTimestampBackend);

            // Ensure game and tests scale with window size and screen DPI.
            base.Content.Add(Content = scalingContainer = new DrawSizePreservingFillContainer
            {
                TargetDrawSize = displaySettings.TargetDrawSize,
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
            yokkoConfig.BindGameplaySettings(gameplaySettings);
            yokkoConfig.BindSkinSettings(skinSettings);
            skinLibrary.Initialise(host.Storage, skinSettings);

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
        private void load(FrameworkConfigManager frameworkConfig)
        {
            base.Content.Add(
                skinImportOverlay = new SkinImportNotificationOverlay());

            string configuredLocale = frameworkConfig.Get<string>(FrameworkSetting.Locale);
            string normalizedLocale = YokkoLocale.Normalize(configuredLocale);
            if (configuredLocale != normalizedLocale)
                frameworkConfig.SetValue(FrameworkSetting.Locale, normalizedLocale);

            var resources = new DllResourceStore(typeof(YokkoResources).Assembly);
            Resources.AddStore(resources);
            AddFont(Resources, @"Fonts/Yokko/Yokko");
            AddFont(Resources, @"Fonts/Yokko/Yokko-Bold");
            displaySettings.UiScale.BindValueChanged(_ => scalingContainer.TargetDrawSize = displaySettings.TargetDrawSize, true);
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                if (window != null)
                    window.DragDrop -= onFileDropped;

                yokkoConfig?.Dispose();
                keyInputTimestamps.Dispose();
            }

            base.Dispose(isDisposing);
        }

        private void onFileDropped(string path)
        {
            if (!OsuManiaSkinLibrary.IsSupportedDrop(path))
                return;

            Scheduler.Add(() => skinImportOverlay.ShowImporting(path));

            _ = Task.Run(() => skinLibrary.Import(path))
                    .ContinueWith(
                        task => Scheduler.Add(() =>
                        {
                            SkinImportResult result = task.IsCompletedSuccessfully
                                ? task.Result
                                : new SkinImportResult(
                                    false,
                                    task.Exception?.GetBaseException().Message ?? "Unknown import error.");
                            skinImportOverlay.ShowResult(result);
                        }),
                        TaskScheduler.Default);
        }
    }
}
