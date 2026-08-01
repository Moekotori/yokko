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
            AddStep("set reported text", () => bubble.SetText("D F J K，出发！"));
            AddWaitStep("wait for fit", 5);
            AddAssert(
                "label fits sticker",
                () => bubble.StickerLabelDrawWidth <= 136
                      && bubble.StickerLabelDrawLeft >= 92
                      && bubble.StickerLabelDrawRight <= 232);
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

        [Test]
        public void TestSignalSnakeFollowsKeyTestInput()
        {
            HomeKeyTestPad pad = null;
            HomeSignalSnake snake = null;
            int steps = 0;
            float positionX = 0;
            float positionY = 0;

            AddStep("find home toys", () =>
            {
                pad = this.ChildrenOfType<HomeKeyTestPad>().Single();
                snake = this.ChildrenOfType<HomeSignalSnake>().Single();
                snake.SetAvailable(true);
                snake.Restart();
            });
            AddStep("record signal state", () =>
            {
                steps = snake.StepCount;
                positionX = snake.HeadPosition.X;
                positionY = snake.HeadPosition.Y;
            });
            AddStep("press right lane", () => pad.PressLane(3));
            AddAssert("signal advanced", () => snake.StepCount == steps + 1);
            AddAssert(
                "signal head moved",
                () => snake.HeadPosition.X != positionX
                      || snake.HeadPosition.Y != positionY);
        }

        [Test]
        public void TestSignalSnakeSupportsArrowKeys()
        {
            HomeSignalSnake snake = null;
            int steps = 0;
            bool releasedWhenHidden = false;

            AddStep("find signal snake", () =>
            {
                snake = this.ChildrenOfType<HomeSignalSnake>().Single();
                snake.SetAvailable(true);
                snake.Restart();
            });
            AddStep("record signal steps", () => steps = snake.StepCount);
            AddAssert("up arrow handled", () => snake.TryHandleArrowKey(Key.Up, false));
            AddAssert("signal advanced once", () => snake.StepCount == steps + 1);
            AddAssert("repeat consumed", () => snake.TryHandleArrowKey(Key.Up, true));
            AddAssert("repeat did not advance", () => snake.StepCount == steps + 1);
            AddAssert("letter ignored", () => !snake.TryHandleArrowKey(Key.Q, false));
            AddAssert("restart key handled", () => snake.TryHandleRestartKey(Key.R, false));
            AddAssert("restart returned to start", () => snake.StepCount == 0
                                                        && Math.Abs(snake.HeadPosition.X - 186) < 0.01f
                                                        && Math.Abs(snake.HeadPosition.Y - 88) < 0.01f);
            AddStep("hide signal snake", () =>
            {
                snake.SetAvailable(false);
                releasedWhenHidden = !snake.TryHandleArrowKey(Key.Left, false);
            });
            AddAssert("hidden snake releases arrows", () => releasedWhenHidden);
        }

        [Test]
        public void TestSignalSnakeStopsAtBoundaryAndDiesOnTailCollision()
        {
            HomeSignalSnake snake = null;
            int steps = 0;
            int deaths = 0;

            AddStep("find signal snake", () =>
            {
                snake = this.ChildrenOfType<HomeSignalSnake>().Single();
                snake.SetAvailable(true);
                snake.Restart();
                steps = snake.StepCount;
                deaths = snake.DeathCount;
            });
            AddStep("move against right boundary", () =>
            {
                for (int i = 0; i < 8; i++)
                    snake.HandleLane(3);
            });
            AddAssert("boundary stopped movement", () => snake.StepCount == steps + 3);
            AddAssert("head stayed at right edge", () => Math.Abs(snake.HeadPosition.X - 240) < 0.01f);
            AddAssert("boundary did not count as death", () => snake.DeathCount == deaths);
            AddStep("try to reverse into tail", () => snake.HandleLane(0));
            AddAssert("tail collision counted as death", () => snake.DeathCount == deaths + 1);
            AddAssert("death feedback is visible", () => snake.DeathFeedbackVisible);
            AddAssert("death did not advance movement", () => snake.StepCount == steps + 3);
            AddWaitStep("wait for respawn", 40);
            AddAssert("respawned at start", () => Math.Abs(snake.HeadPosition.X - 186) < 0.01f
                                                    && Math.Abs(snake.HeadPosition.Y - 88) < 0.01f);
            AddAssert("whole tail remains visible", () => snake.VisibleTrailDotCount == 9);
        }

        [Test]
        public void TestBubblePopGameStartsAndScores()
        {
            HomeBubblePopGame game = null;
            float targetX = 0;
            float targetY = 0;

            AddStep("find bubble pop game", () =>
            {
                game = this.ChildrenOfType<HomeBubblePopGame>().Single();
                game.SetAvailable(true);
                game.Restart();
            });
            AddAssert("waiting for start bubble", () => !game.IsRunning && game.Score == 0);
            AddAssert("start bubble has clickable size", () => game.TargetDrawWidth >= 40);
            AddStep("click special start bubble", () => game.ActivateTargetForTest());
            AddAssert("game started", () => game.IsRunning && game.Score == 0);
            AddAssert("target bubble has clickable size", () => game.TargetDrawWidth >= 30);
            AddStep("remember first target", () =>
            {
                targetX = game.TargetPosition.X;
                targetY = game.TargetPosition.Y;
            });
            AddStep("pop first target", () => game.ActivateTargetForTest());
            AddAssert("score increased", () => game.Score == 1);
            AddAssert(
                "new bubble spawned elsewhere",
                () => Math.Abs(game.TargetPosition.X - targetX) > 0.01f
                      || Math.Abs(game.TargetPosition.Y - targetY) > 0.01f);
            AddStep("pop second target", () => game.ActivateTargetForTest());
            AddAssert("score keeps increasing", () => game.Score == 2);
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
