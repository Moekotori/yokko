using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.IO.Stores;
using osuTK;
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
        private readonly YokkoDisplaySettings displaySettings = new();

        protected YokkoGameBase()
        {
            // Ensure game and tests scale with window size and screen DPI.
            base.Content.Add(Content = scalingContainer = new DrawSizePreservingFillContainer
            {
                TargetDrawSize = displaySettings.TargetDrawSize,
                Strategy = DrawSizePreservationStrategy.Minimum,
            });
        }

        [BackgroundDependencyLoader]
        private void load(DependencyContainer dependencies)
        {
            Resources.AddStore(new DllResourceStore(typeof(YokkoResources).Assembly));
            dependencies.Cache(displaySettings);
            displaySettings.UiScale.BindValueChanged(_ => scalingContainer.TargetDrawSize = displaySettings.TargetDrawSize, true);
        }
    }
}
