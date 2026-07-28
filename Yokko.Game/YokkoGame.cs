using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Screens;
using Yokko.Game.Input;
using Yokko.Game.Presentation;
using Yokko.Game.Screens.Main;

namespace Yokko.Game
{
    public partial class YokkoGame : YokkoGameBase
    {
        private ScreenStack screenStack;
        private YokkoPerformanceReadout performanceReadout;
        private BindableBool showPerformanceReadout;

        public YokkoGame(IKeyInputTimestampBackend keyInputTimestampBackend = null)
            : base(keyInputTimestampBackend)
        {
        }

        [BackgroundDependencyLoader]
        private void load(YokkoDisplaySettings displaySettings)
        {
            Content.Children = new Drawable[]
            {
                screenStack = new ScreenStack
                {
                    RelativeSizeAxes = Axes.Both,
                },
                performanceReadout = new YokkoPerformanceReadout
                {
                    Anchor = Anchor.BottomRight,
                    Origin = Anchor.BottomRight,
                    Position = new osuTK.Vector2(-18, -14),
                    Depth = float.MinValue,
                },
            };

            showPerformanceReadout = displaySettings.ShowPerformanceReadout;
            showPerformanceReadout.BindValueChanged(
                onShowPerformanceReadoutChanged,
                true);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            screenStack.Push(new MainScreen(RequestExit));
        }

        private void onShowPerformanceReadoutChanged(
            ValueChangedEvent<bool> change)
        {
            performanceReadout.Alpha = change.NewValue ? 1 : 0;
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing && showPerformanceReadout != null)
                showPerformanceReadout.ValueChanged -=
                    onShowPerformanceReadoutChanged;

            base.Dispose(isDisposing);
        }
    }
}
