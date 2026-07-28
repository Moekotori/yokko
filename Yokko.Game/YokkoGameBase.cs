using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Configuration;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.IO.Stores;
using osu.Framework.Localisation;
using osu.Framework.Platform;
using osuTK;
using Yokko.Game.Audio;
using Yokko.Game.Configuration;
using Yokko.Game.Importing;
using Yokko.Game.Localisation;
using Yokko.Game.Presentation;
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
        private YokkoConfigManager yokkoConfig;

        protected YokkoGameBase()
        {
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
            base.SetHost(host);
            yokkoConfig ??= new YokkoConfigManager(host.Storage);
            yokkoConfig.BindAudioSettings(audioSettings);
            yokkoConfig.BindImportSettings(importSettings);
        }

        [BackgroundDependencyLoader]
        private void load(FrameworkConfigManager frameworkConfig)
        {
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
                yokkoConfig?.Dispose();

            base.Dispose(isDisposing);
        }
    }
}
