using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Screens;
using Yokko.Game.Screens.Settings;

namespace Yokko.Game.Tests.Visual
{
    [TestFixture]
    public partial class TestSceneSettingsScreen : YokkoTestScene
    {
        private readonly ScreenStack screenStack;

        public TestSceneSettingsScreen()
        {
            Add(screenStack = new ScreenStack(new SettingsScreen()) { RelativeSizeAxes = Axes.Both });
        }

        [Test]
        public void TestSettingsScreen()
        {
            AddAssert("settings screen is current", () => screenStack.CurrentScreen is SettingsScreen);
        }
    }
}
