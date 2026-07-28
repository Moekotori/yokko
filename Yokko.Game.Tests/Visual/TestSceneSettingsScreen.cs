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
        private readonly SettingsScreen settingsScreen;

        public TestSceneSettingsScreen()
        {
            Add(screenStack = new ScreenStack(settingsScreen = new SettingsScreen()) { RelativeSizeAxes = Axes.Both });
        }

        [Test]
        public void TestSettingsScreen()
        {
            AddAssert("settings screen is current", () => screenStack.CurrentScreen is SettingsScreen);
        }

        [Test]
        public void TestEverySettingsPageCanOpen()
        {
            foreach (SettingsPageKind page in System.Enum.GetValues<SettingsPageKind>())
            {
                SettingsPageKind capturedPage = page;
                AddStep($"open {page}", () => settingsScreen.OpenPage(capturedPage));
                AddAssert($"{page} is current", () => settingsScreen.CurrentPage == capturedPage);
            }
        }

        [Test]
        public void TestTransientInteractionsDismissInOrder()
        {
            SettingsPlaceholderPanel placeholder = null;
            DisplaySettingsPanel display = null;

            AddStep("open General", () => settingsScreen.OpenPage(SettingsPageKind.General));
            AddStep("capture placeholder", () => placeholder = (SettingsPlaceholderPanel)settingsScreen.ActivePanel);
            AddStep("expand first section", () => placeholder.ToggleSection(0));
            AddAssert("first section expanded", () => placeholder.ExpandedSectionIndex == 0);
            AddStep("expand second section", () => placeholder.ToggleSection(1));
            AddAssert("only second section expanded", () => placeholder.ExpandedSectionIndex == 1);
            AddAssert("Esc layer dismisses section", settingsScreen.DismissTransientUi);
            AddAssert("all sections collapsed", () => placeholder.ExpandedSectionIndex == -1);

            AddStep("open Display", () => settingsScreen.OpenPage(SettingsPageKind.Display));
            AddStep("capture display", () => display = (DisplaySettingsPanel)settingsScreen.ActivePanel);
            AddStep("open resolution menu", () => display.ToggleResolutionMenu());
            AddAssert("resolution menu open", () => display.IsResolutionMenuOpen);
            AddAssert("Esc layer dismisses menu", settingsScreen.DismissTransientUi);
            AddAssert("resolution menu closed", () => !display.IsResolutionMenuOpen);
        }
    }
}
