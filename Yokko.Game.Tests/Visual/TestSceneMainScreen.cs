using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Screens;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Tests.Visual
{
    [TestFixture]
    public partial class TestSceneMainScreen : YokkoTestScene
    {
        public TestSceneMainScreen()
        {
            Add(new ScreenStack(new MainScreen()) { RelativeSizeAxes = Axes.Both });
        }
    }
}
