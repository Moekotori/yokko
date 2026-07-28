using osu.Framework.Allocation;
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

        public YokkoGame(IKeyInputTimestampBackend keyInputTimestampBackend = null)
            : base(keyInputTimestampBackend)
        {
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Content.Children = new Drawable[]
            {
                screenStack = new ScreenStack
                {
                    RelativeSizeAxes = Axes.Both,
                },
                new YokkoPerformanceReadout
                {
                    Anchor = Anchor.BottomRight,
                    Origin = Anchor.BottomRight,
                    Position = new osuTK.Vector2(-18, -14),
                    Depth = float.MinValue,
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            screenStack.Push(new MainScreen(RequestExit));
        }
    }
}
