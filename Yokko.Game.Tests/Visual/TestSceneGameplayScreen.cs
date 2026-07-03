using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Screens;
using Yokko.Core.Beatmaps;
using Yokko.Game.Screens.Gameplay;

namespace Yokko.Game.Tests.Visual
{
    [TestFixture]
    public partial class TestSceneGameplayScreen : YokkoTestScene
    {
        public TestSceneGameplayScreen()
        {
            Add(new ScreenStack(new GameplayScreen(DemoBeatmaps.CreateFourKeyDemo())) { RelativeSizeAxes = Axes.Both });
        }
    }
}
