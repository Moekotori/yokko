using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Screens;
using osu.Framework.Testing;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Tests.Visual
{
    [TestFixture]
    public partial class TestSceneMainScreen : YokkoTestScene
    {
        [Resolved]
        private IRenderer renderer { get; set; }

        private bool screenshotSaved;

        public TestSceneMainScreen()
        {
            Add(new ScreenStack(new MainScreen()) { RelativeSizeAxes = Axes.Both });
        }

        [Test]
        public void TestMainScreenLayout()
        {
            AddWaitStep("wait for entrance animation", 80);
            AddAssert(
                "player progress card is present",
                () => this.ChildrenOfType<HomePlayerProgressCard>().SingleOrDefault() != null);
            AddAssert(
                "player card layouts keep breathing room",
                () => MainScreen.PlayerCardLayoutsHaveBreathingRoom);
            AddStep("capture main screen", captureScreenshot);
            AddUntilStep("screenshot saved", () => screenshotSaved);
        }

        [Test]
        public void TestKeyTestPadCountsKeyPresses()
        {
            HomeKeyTestPad pad = null;
            AddStep(
                "find key test pad",
                () => pad = this.ChildrenOfType<HomeKeyTestPad>().Single());
            AddStep("press D", () => pad.TryHandleKey(osuTK.Input.Key.D, true));
            AddStep("release D", () => pad.TryHandleKey(osuTK.Input.Key.D, false));
            AddStep("press K", () => pad.TryHandleKey(osuTK.Input.Key.K, true));
            AddStep("press unmapped key", () => pad.TryHandleKey(osuTK.Input.Key.Q, true));
            AddAssert("two hits counted", () => pad.HitCount == 2);
        }

        private void captureScreenshot()
        {
            string outputPath = Environment.GetEnvironmentVariable(
                "YOKKO_MAIN_SCREENSHOT");

            if (string.IsNullOrWhiteSpace(outputPath))
            {
                screenshotSaved = true;
                return;
            }

            MethodInfo takeScreenshot = renderer.GetType().GetMethod(
                "TakeScreenshot",
                BindingFlags.Instance
                | BindingFlags.Public
                | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException(
                    "The active renderer does not expose screenshot capture.");
            using var screenshot = (Image<Rgba32>)takeScreenshot.Invoke(
                renderer,
                null);
            Directory.CreateDirectory(
                Path.GetDirectoryName(outputPath)
                ?? throw new InvalidOperationException(
                    "Screenshot path has no parent directory."));
            screenshot.SaveAsPng(outputPath);
            screenshotSaved = true;
        }
    }
}
