using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Screens;
using osu.Framework.Testing;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using osuTK.Input;
using Yokko.Core.Beatmaps;
using Yokko.Core.Gameplay;
using Yokko.Core.Timing;
using Yokko.Game.Gameplay;
using Yokko.Game.Importing;
using Yokko.Game.Screens.Main;
using Yokko.Import;

namespace Yokko.Game.Tests.Visual
{
    [TestFixture]
    public partial class TestSceneMainScreen : YokkoTestScene
    {
        [Resolved]
        private IRenderer renderer { get; set; }

        [Resolved]
        private YokkoGameplaySettings gameplaySettings { get; set; }

        [Resolved]
        private ImportedChartLibrary importedChartLibrary { get; set; }

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
        public void TestSongSelectPreloadRefreshesBeforeNavigation()
        {
            MainScreen screen = null;
            AddStep(
                "find main screen",
                () => screen = this.ChildrenOfType<MainScreen>().Single());
            AddStep("clear chart library", () => importedChartLibrary.Clear());
            AddUntilStep(
                "empty preload is current",
                () => screen.PreparedSongSelectEntryCount == 0
                      && screen.IsPreparedSongSelectCurrent);
            AddStep("publish chart while preload is inactive", () =>
            {
                var beatmap = new YokkoBeatmap(
                    "Preload regression",
                    "Yokko",
                    "Test",
                    "Normal",
                    KeyMode.FourKey,
                    ChartSourceFormat.OsuMania,
                    [YokkoTimingPoint.Default],
                    null,
                    [new YokkoHitObject(
                        0,
                        100,
                        null,
                        HitObjectKind.Tap)]);
                importedChartLibrary.AddOrReplace(
                    new ChartImportResult(beatmap, []),
                    @"C:\Charts\preload-regression.osu");
            });
            AddUntilStep(
                "preload rebuilt from latest library snapshot",
                () => screen.PreparedSongSelectEntryCount == 1
                      && screen.IsPreparedSongSelectCurrent);
            AddStep("clear test chart", () => importedChartLibrary.Clear());
        }

        [Test]
        public void TestKeyTestPadCountsKeyPresses()
        {
            HomeKeyTestPad pad = null;
            int baseline = 0;
            AddStep(
                "find key test pad",
                () => pad = this.ChildrenOfType<HomeKeyTestPad>().Single());
            AddStep("record baseline", () => baseline = pad.HitCount);
            AddStep("press D", () => pad.TryHandleKey(osuTK.Input.Key.D, true));
            AddStep("release D", () => pad.TryHandleKey(osuTK.Input.Key.D, false));
            AddStep("press K", () => pad.TryHandleKey(osuTK.Input.Key.K, true));
            AddStep("press unmapped key", () => pad.TryHandleKey(osuTK.Input.Key.Q, true));
            AddAssert("two hits counted", () => pad.HitCount == baseline + 2);
        }

        [Test]
        public void TestKeyTestPadBuildsCombo()
        {
            HomeKeyTestPad pad = null;
            AddStep(
                "find key test pad",
                () => pad = this.ChildrenOfType<HomeKeyTestPad>().Single());

            int baseline = 0;
            AddStep("press D once", () => pad.PressLane(0));
            AddStep("record combo baseline", () => baseline = pad.ComboCount);
            AddStep("press F three times", () =>
            {
                pad.PressLane(1);
                pad.PressLane(1);
                pad.PressLane(1);
            });
            AddAssert(
                "combo builds on quick hits",
                () => pad.ComboCount == baseline + 3);
            AddAssert(
                "combo hint shown",
                () => pad.ChildrenOfType<SpriteText>()
                         .Any(text => text.Text.ToString().StartsWith("COMBO x")));

            AddStep("press to milestone", () =>
            {
                int remaining = 10 - pad.ComboCount % 10;
                for (int i = 0; i < remaining; i++)
                    pad.PressLane(2);
            });
            AddAssert(
                "milestone combo reached",
                () => pad.ComboCount % 10 == 0);
        }

        [Test]
        public void TestBubbleLineAdvances()
        {
            MainScreen screen = null;
            AddStep(
                "find main screen",
                () => screen = this.ChildrenOfType<MainScreen>().Single());

            int startIndex = 0;
            AddStep(
                "remember current line",
                () => startIndex = screen.BubbleLineIndex);
            AddStep("advance line", () => screen.advanceBubbleLine());
            AddAssert(
                "line index advanced",
                () => screen.BubbleLineIndex
                      == (startIndex + 1) % screen.BubbleLineCount);
        }

        [Test]
        public void TestBubbleStickerLabelFitsInsideSticker()
        {
            HomeMascotBubble bubble = null;
            AddStep(
                "find bubble",
                () => bubble = this
                    .ChildrenOfType<HomeMascotBubble>()
                    .Single());
            AddStep("set long text", () => bubble.SetText("Again! Again!"));
            AddWaitStep("wait for fit", 5);
            AddAssert(
                "label fits sticker",
                () => bubble.StickerLabelDrawWidth <= 150);
        }

        [Test]
        public void TestKeyTestPadFollowsConfiguredBindings()
        {
            HomeKeyTestPad pad = null;
            Key original = Key.Unknown;
            int hits = 0;
            AddStep(
                "find key test pad",
                () => pad = this.ChildrenOfType<HomeKeyTestPad>().Single());
            AddStep(
                "remember original binding",
                () => original = gameplaySettings.FourKeyBindings[0].Value);
            AddStep(
                "rebind lane 1 to F12",
                () => gameplaySettings.SetBinding(KeyMode.FourKey, 0, Key.F12));
            AddUntilStep(
                "cap label updated",
                () => pad.ChildrenOfType<SpriteText>()
                         .Any(text => text.Text.ToString() == "F12"));
            AddStep("record hits", () => hits = pad.HitCount);
            AddStep("press F12", () => pad.TryHandleKey(Key.F12, true));
            AddAssert("rebound key hits lane", () => pad.HitCount == hits + 1);
            AddStep("press D", () => pad.TryHandleKey(Key.D, true));
            AddAssert(
                "old key no longer bound",
                () => pad.HitCount == hits + 1);
            AddStep(
                "restore binding",
                () => gameplaySettings.SetBinding(KeyMode.FourKey, 0, original));
        }

        [Test]
        public void TestKeyTestPadTracksKps()
        {
            HomeKeyTestPad pad = null;
            AddStep(
                "find key test pad",
                () => pad = this.ChildrenOfType<HomeKeyTestPad>().Single());
            AddStep("press lane rapidly", () =>
            {
                for (int i = 0; i < 6; i++)
                    pad.PressLane(0);
            });
            AddAssert("kps counted", () => pad.CurrentKps >= 6);
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
