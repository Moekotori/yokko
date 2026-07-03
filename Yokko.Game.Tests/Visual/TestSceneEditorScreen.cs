using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Screens;
using Yokko.Game.Screens.Editor;

namespace Yokko.Game.Tests.Visual
{
    [TestFixture]
    public partial class TestSceneEditorScreen : YokkoTestScene
    {
        public TestSceneEditorScreen()
        {
            Add(new ScreenStack(new EditorScreen()) { RelativeSizeAxes = Axes.Both });
        }
    }
}
