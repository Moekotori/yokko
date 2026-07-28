using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osuTK;
using osuTK.Input;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Yokko.Audio;
using Yokko.Core.Beatmaps;
using Yokko.Core.Gameplay;
using Yokko.Core.Timing;
using Yokko.Game.Gameplay;
using Yokko.Game.Screens.Gameplay;
using Yokko.Game.Skinning.OsuMania;

namespace Yokko.Game.Tests.Visual
{
    [TestFixture]
    public partial class TestSceneGameplayScreen : YokkoTestScene
    {
        private readonly ScreenStack screenStack;

        [Resolved]
        private YokkoGameplaySettings gameplaySettings { get; set; }

        public TestSceneGameplayScreen()
        {
            Add(screenStack = new ScreenStack(new GameplayScreen(DemoBeatmaps.CreateFourKeyDemo()))
            {
                RelativeSizeAxes = Axes.Both,
            });
        }

        [Test]
        public void TestGameplaySurfaceFillsAndPlayfieldIsCentred()
        {
            AddUntilStep("gameplay layout loaded", () =>
                (screenStack.CurrentScreen as Drawable)?
                .ChildrenOfType<GameplayPlayfield>()
                .SingleOrDefault() != null);
            AddAssert("surface fills the screen", () =>
            {
                Drawable current = screenStack.CurrentScreen as Drawable;
                return current?
                       .ChildrenOfType<Box>()
                       .Any(box =>
                           ReferenceEquals(box.Parent, current) &&
                           box.RelativeSizeAxes == Axes.Both) == true;
            });
            AddAssert("playfield is grounded and uniformly fills height", () =>
            {
                Drawable current = screenStack.CurrentScreen as Drawable;
                GameplayPlayfield playfield = (screenStack.CurrentScreen as Drawable)?
                                              .ChildrenOfType<GameplayPlayfield>()
                                              .SingleOrDefault();
                return current != null &&
                       playfield != null &&
                       playfield.Anchor == Anchor.BottomCentre &&
                       playfield.Origin == Anchor.BottomCentre &&
                       playfield.Position == Vector2.Zero &&
                       Math.Abs(playfield.Scale.X - playfield.Scale.Y) < 0.001 &&
                       Math.Abs(
                           playfield.Height * playfield.Scale.Y -
                           current.DrawHeight) < 0.5;
            });
            AddAssert("score hud is visible", () =>
            {
                Drawable current = screenStack.CurrentScreen as Drawable;
                return current?
                       .ChildrenOfType<GameplayHud>()
                       .SingleOrDefault() != null &&
                       current.ChildrenOfType<JudgementReadout>().Any() == false;
            });
        }

        [Test]
        public void TestAudioFailureStopsGameplayAndShowsMessage()
        {
            YokkoBeatmap beatmap = DemoBeatmaps.CreateFourKeyDemo() with
            {
                AudioPath = "missing-audio.wav",
            };

            AddStep("open gameplay with failing audio", () =>
                screenStack.Push(new GameplayScreen(
                    beatmap,
                    new FailingAudioEngine())));
            AddUntilStep("gameplay is blocked", () =>
                (screenStack.CurrentScreen as GameplayScreen)?.GameplayBlocked == true);
            AddAssert("audio failure is visible", () =>
                (screenStack.CurrentScreen as Drawable)?
                .ChildrenOfType<GameplayFailureOverlay>()
                .SingleOrDefault() != null);
        }

        [Test]
        public void TestCompletedPlayShowsResultOverlay()
        {
            YokkoBeatmap beatmap = DemoBeatmaps.CreateFourKeyDemo() with
            {
                Title = "Result Test",
                HitObjects =
                [
                    new YokkoHitObject(
                        0,
                        0,
                        null,
                        HitObjectKind.Tap),
                ],
            };

            AddStep("open short gameplay", () =>
                screenStack.Push(new GameplayScreen(beatmap)));
            AddUntilStep("gameplay completes", () =>
                (screenStack.CurrentScreen as GameplayScreen)?
                .GameplayCompleted == true);
            AddAssert("result overlay is visible", () =>
                (screenStack.CurrentScreen as Drawable)?
                .ChildrenOfType<GameplayResultOverlay>()
                .SingleOrDefault() != null);
            AddAssert("miss result was captured", () =>
                (screenStack.CurrentScreen as GameplayScreen)?
                .CompletedResult?.Miss == 1);
        }

        [Test]
        public void TestLoadsOsuManiaSkinTextures()
        {
            string skinPath = createTestSkin();

            AddStep("open skinned gameplay", () =>
                screenStack.Push(new GameplayScreen(DemoBeatmaps.CreateFourKeyDemo(), skinPath: skinPath)));
            AddUntilStep("custom playfield width applied", () =>
                (screenStack.CurrentScreen as Drawable)?.ChildrenOfType<GameplayPlayfield>().SingleOrDefault()?.Width == 160);
            AddUntilStep("skin sprites loaded", () =>
                (screenStack.CurrentScreen as Drawable)?.ChildrenOfType<Sprite>().Any(sprite => sprite.Texture != null) == true);
        }

        [Test]
        [Category("Integration")]
        public void TestLoadsRealOsuManiaSkinSample()
        {
            string skinPath = Environment.GetEnvironmentVariable("YOKKO_OSU_MANIA_SKIN_SAMPLE");

            if (string.IsNullOrWhiteSpace(skinPath) || (!File.Exists(skinPath) && !Directory.Exists(skinPath)))
                Assert.Ignore("Set YOKKO_OSU_MANIA_SKIN_SAMPLE to a real osu! skin package.");

            bool sevenKey = Environment.GetEnvironmentVariable("YOKKO_OSU_MANIA_SKIN_SAMPLE_KEYS") == "7";

            AddStep($"open real {(sevenKey ? 7 : 4)}K skin", () =>
                screenStack.Push(new GameplayScreen(
                    sevenKey ? DemoBeatmaps.CreateSevenKeyDemo() : DemoBeatmaps.CreateFourKeyDemo(),
                    skinPath: skinPath)));
            AddUntilStep("real skin playfield loaded", () =>
                (screenStack.CurrentScreen as Drawable)?.ChildrenOfType<GameplayPlayfield>().SingleOrDefault() != null);
            AddUntilStep("real skin textures decoded", () =>
                (screenStack.CurrentScreen as Drawable)?.ChildrenOfType<Sprite>().Any(sprite => sprite.Texture != null) == true);
        }

        [Test]
        [Category("Integration")]
        public void TestCircleSkinUsesLegacyGeometryWithoutExtraScaling()
        {
            string skinPath = Environment.GetEnvironmentVariable("YOKKO_OSU_MANIA_CIRCLE_SKIN_SAMPLE");

            if (string.IsNullOrWhiteSpace(skinPath) || !File.Exists(skinPath))
                Assert.Ignore("Set YOKKO_OSU_MANIA_CIRCLE_SKIN_SAMPLE to a real circle skin package.");

            YokkoBeatmap beatmap = DemoBeatmaps.CreateFourKeyDemo();

            AddStep("open real circle skin", () =>
                screenStack.Push(new GameplayScreen(beatmap, skinPath: skinPath)));
            AddUntilStep("circle skin geometry loaded", () =>
            {
                GameplayPlayfield playfield = (screenStack.CurrentScreen as Drawable)?
                                              .ChildrenOfType<GameplayPlayfield>()
                                              .SingleOrDefault();
                DrawableNote firstNote = (screenStack.CurrentScreen as Drawable)?
                                         .ChildrenOfType<DrawableNote>()
                                         .FirstOrDefault();
                LaneColumn firstLane = (screenStack.CurrentScreen as Drawable)?
                                       .ChildrenOfType<LaneColumn>()
                                       .FirstOrDefault();
                Sprite idleReceptor = firstLane?
                                      .ReceptorLayer
                                      .ChildrenOfType<Sprite>()
                                      .FirstOrDefault();

                return playfield != null &&
                       firstNote != null &&
                       idleReceptor != null &&
                       Math.Abs(playfield.Width - 286) < 0.01 &&
                       Math.Abs(playfield.Scale.X - playfield.Scale.Y) < 0.001 &&
                       Math.Abs(firstNote.Width - 70) < 0.01 &&
                       Math.Abs(firstNote.Height - 70 * 146f / 150f) < 0.01 &&
                       Math.Abs(idleReceptor.Width - 70) < 0.01 &&
                       Math.Abs(
                           idleReceptor.Height -
                           187.5f /
                           OsuManiaSkinConfiguration.LegacyPositionScaleFactor) < 0.01;
            });
            AddAssert("downscroll note bottom meets hit position", () =>
            {
                DrawableNote firstNote = (screenStack.CurrentScreen as Drawable)?
                                         .ChildrenOfType<DrawableNote>()
                                         .FirstOrDefault();

                if (firstNote == null)
                    return false;

                firstNote.UpdatePosition(
                    beatmap.HitObjects[0].StartTimeMilliseconds,
                    false,
                    false,
                    0,
                    460,
                    1800);
                return Math.Abs(firstNote.Y + firstNote.Height - 460) < 0.01;
            });
        }

        [Test]
        [Category("Integration")]
        public void TestLoadsRealArrowSkin()
        {
            string skinPath = Environment.GetEnvironmentVariable("YOKKO_OSU_MANIA_ARROW_SKIN_SAMPLE");

            if (string.IsNullOrWhiteSpace(skinPath) || !File.Exists(skinPath))
                Assert.Ignore("Set YOKKO_OSU_MANIA_ARROW_SKIN_SAMPLE to a real arrow skin package.");

            AddStep("open real arrow skin", () =>
                screenStack.Push(new GameplayScreen(createHoldDemo(KeyMode.FourKey), skinPath: skinPath)));
            AddUntilStep("all arrow lanes use textures", () =>
            {
                DrawableNote[] notes = (screenStack.CurrentScreen as Drawable)?
                                       .ChildrenOfType<DrawableNote>()
                                       .Take(4)
                                       .ToArray();
                Sprite tiledBody = notes?
                                   .FirstOrDefault()?
                                   .ChildrenOfType<Sprite>()
                                   .FirstOrDefault(sprite => sprite.Texture?.WrapModeT == WrapMode.Repeat);
                return notes?.Length == 4 &&
                       notes.All(note => note.ChildrenOfType<Sprite>().Any(sprite => sprite.Texture != null)) &&
                       notes.SelectMany(note => note.ChildrenOfType<Sprite>())
                            .Select(sprite => sprite.Texture)
                            .Where(texture => texture != null)
                            .Distinct()
                            .Count() >= 4 &&
                       tiledBody?.TextureRelativeSizeAxes == Axes.None;
            });
        }

        [Test]
        [Category("Integration")]
        public void TestLoadsRealThrowStyleSkinBodyWithoutStretching()
        {
            string skinPath = Environment.GetEnvironmentVariable("YOKKO_OSU_MANIA_THROW_SKIN_SAMPLE");

            if (string.IsNullOrWhiteSpace(skinPath) || !File.Exists(skinPath))
                Assert.Ignore("Set YOKKO_OSU_MANIA_THROW_SKIN_SAMPLE to a real throw-style skin package.");

            AddStep("open real throw-style skin", () =>
                screenStack.Push(new GameplayScreen(createHoldDemo(KeyMode.SevenKey), skinPath: skinPath)));
            AddUntilStep("long body texture is cropped at natural scale", () =>
            {
                DrawableNote hold = (screenStack.CurrentScreen as Drawable)?
                                    .ChildrenOfType<DrawableNote>()
                                    .FirstOrDefault();
                Sprite longBody = hold?
                                  .ChildrenOfType<Sprite>()
                                  .FirstOrDefault(sprite => sprite.Texture?.DisplayHeight >= 30000);
                return longBody != null &&
                       longBody.Texture.Available &&
                       Math.Abs(longBody.Height - 40000f * 92 / 138) < 1 &&
                       longBody.Height > hold.Height * 10 &&
                       longBody.Parent is Container { Masking: true };
            });
        }

        [Test]
        public void TestUpsideDownSkinFlipsArrowElements()
        {
            string skinPath = createTestSkin("""
UpsideDown: 1
HitPosition: 400
""");

            AddStep("open upside-down skin", () =>
                screenStack.Push(new GameplayScreen(DemoBeatmaps.CreateFourKeyDemo(), skinPath: skinPath)));
            AddUntilStep("upscroll geometry applied", () =>
            {
                GameplayPlayfield playfield = (screenStack.CurrentScreen as Drawable)?
                                              .ChildrenOfType<GameplayPlayfield>()
                                              .SingleOrDefault();
                return playfield?.ScrollOrigin == 480 && playfield.JudgementPosition == 80;
            });
            AddUntilStep("notes and receptors flipped", () =>
            {
                Drawable current = screenStack.CurrentScreen as Drawable;
                bool noteFlipped = current?
                                   .ChildrenOfType<DrawableNote>()
                                   .SelectMany(note => note.ChildrenOfType<Sprite>())
                                   .Any(sprite => sprite.Scale.Y < 0) == true;
                bool keyFlipped = current?
                                  .ChildrenOfType<LaneColumn>()
                                  .SelectMany(lane => lane.ReceptorLayer.ChildrenOfType<Sprite>())
                                  .Any(sprite => sprite.Scale.Y < 0) == true;
                return noteFlipped && keyFlipped;
            });
        }

        [Test]
        public void TestOsuManiaScrollSpeedShortcuts()
        {
            double originalSpeed = OsuManiaScrollSpeed.Default;
            GameplayScreen gameplayScreen = null;

            AddStep("save and reset scroll speed", () =>
            {
                originalSpeed = gameplaySettings.ScrollSpeed.Value;
                gameplaySettings.SetScrollSpeed(8);
                gameplayScreen =
                    (GameplayScreen)screenStack.CurrentScreen;
            });
            AddStep("plain plus is ignored", () =>
                gameplayScreen.HandleScrollSpeedShortcut(
                    Key.Plus,
                    false));
            AddAssert("plain plus keeps speed 8", () =>
                gameplaySettings.ScrollSpeed.Value == 8);
            AddStep("ctrl plus increases speed", () =>
                gameplayScreen.HandleScrollSpeedShortcut(
                    Key.Plus,
                    true));
            AddAssert("speed is 9", () =>
                gameplaySettings.ScrollSpeed.Value == 9);
            AddAssert("playfield uses osu time range", () =>
                Math.Abs(
                    ((Drawable)screenStack.CurrentScreen)
                               .ChildrenOfType<GameplayPlayfield>()
                               .Single()
                               .ApproachTimeMilliseconds -
                    OsuManiaScrollSpeed.ComputeScrollTime(9)) < 0.001);
            AddStep("ctrl minus decreases speed", () =>
                gameplayScreen.HandleScrollSpeedShortcut(
                    Key.Minus,
                    true));
            AddAssert("speed is 8 again", () =>
                gameplaySettings.ScrollSpeed.Value == 8);
            AddStep("F4 matches osu gameplay shortcut", () =>
                gameplayScreen.HandleScrollSpeedShortcut(
                    Key.F4,
                    false));
            AddAssert("F4 speed is 9", () =>
                gameplaySettings.ScrollSpeed.Value == 9);
            AddStep("F3 restores speed 8", () =>
                gameplayScreen.HandleScrollSpeedShortcut(
                    Key.F3,
                    false));
            AddAssert("F3 speed is 8", () =>
                gameplaySettings.ScrollSpeed.Value == 8);
            AddStep("restore scroll speed", () =>
                gameplaySettings.SetScrollSpeed(originalSpeed));
        }

        [Test]
        public void TestSpaceSkipsLongIntro()
        {
            var audioEngine = new SeekTrackingAudioEngine();
            YokkoBeatmap beatmap = DemoBeatmaps.CreateFourKeyDemo() with
            {
                AudioPath = "intro-fixture.mp3",
                HitObjects =
                [
                    new YokkoHitObject(
                        0,
                        12_000,
                        null,
                        HitObjectKind.Tap),
                ],
            };
            GameplayScreen gameplayScreen = null;

            AddStep("open chart with long intro", () =>
            {
                gameplayScreen = new GameplayScreen(beatmap, audioEngine);
                screenStack.Push(gameplayScreen);
            });
            AddUntilStep("intro can be skipped", () =>
                gameplayScreen?.IntroSkipAvailable == true);
            AddStep("skip intro", () =>
                gameplayScreen.HandleIntroSkip());
            AddUntilStep("audio seeks near first note", () =>
                Math.Abs(
                    audioEngine.LastSeekMilliseconds
                    - gameplayScreen.IntroSkipTargetMilliseconds) < 0.01);
            AddAssert("intro is no longer skippable", () =>
                gameplayScreen.IntroSkipAvailable == false);
        }

        private static string createTestSkin()
            => createTestSkin(string.Empty);

        private static string createTestSkin(string extraManiaConfiguration)
        {
            string directory = Path.Combine(
                TestContext.CurrentContext.WorkDirectory,
                "osu-skin-visual",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "skin.ini"), """
[General]
Name: Yokko Visual Fixture
Version: 2.5

[Mania]
Keys: 4
ColumnWidth: 40,40,40,40
KeyImage0: key
KeyImage0D: key-down
NoteImage0: note
NoteImage0H: hold-head
NoteImage0L: hold-body
NoteImage0T: hold-tail
StageHint: stage-hint
""" + Environment.NewLine + extraManiaConfiguration);

            using var image = new Image<Rgba32>(8, 8, new Rgba32(72, 208, 240, 255));
            foreach (string name in new[]
                     {
                         "key.png",
                         "key-down.png",
                         "note.png",
                         "hold-head.png",
                         "hold-body.png",
                         "hold-tail.png",
                         "stage-hint.png",
                     })
                image.SaveAsPng(Path.Combine(directory, name));

            return directory;
        }

        private static YokkoBeatmap createHoldDemo(KeyMode keyMode)
        {
            int keys = (int)keyMode;
            var hitObjects = Enumerable.Range(0, keys)
                                       .Select(lane => lane == 0
                                           ? new YokkoHitObject(lane, 1600, 2800, HitObjectKind.Hold)
                                           : new YokkoHitObject(lane, 1600 + lane * 180, null, HitObjectKind.Tap))
                                       .ToArray();

            return new YokkoBeatmap(
                "Skin Compatibility",
                "Yokko",
                "Codex",
                $"{keys}K Special Skin",
                keyMode,
                ChartSourceFormat.Yokko,
                [YokkoTimingPoint.Default],
                null,
                hitObjects);
        }

        private sealed class FailingAudioEngine : IAudioEngine
        {
            public AudioEngineStatus Status { get; } = new(
                AudioBackendKind.Fallback,
                null,
                0,
                0,
                0,
                false,
                false,
                false,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0);

            public double PlaybackTimeMilliseconds => 0;

            public IReadOnlyList<AudioBackendCapabilities> Backends => [];

            public ValueTask<IReadOnlyList<AudioDeviceInfo>> GetOutputDevicesAsync(
                CancellationToken cancellationToken = default) =>
                ValueTask.FromResult<IReadOnlyList<AudioDeviceInfo>>([]);

            public ValueTask StartAsync(
                AudioEngineStartRequest request,
                CancellationToken cancellationToken = default) =>
                ValueTask.FromException(new InvalidOperationException("Audio fixture failed."));

            public ValueTask PauseAsync(CancellationToken cancellationToken = default) =>
                ValueTask.CompletedTask;

            public ValueTask SeekAsync(
                double timeMilliseconds,
                CancellationToken cancellationToken = default) =>
                ValueTask.CompletedTask;

            public ValueTask StopAsync(CancellationToken cancellationToken = default) =>
                ValueTask.CompletedTask;

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }

        private sealed class SeekTrackingAudioEngine : IAudioEngine
        {
            private AudioEngineStatus status = createStatus(false);

            public AudioEngineStatus Status => status;

            public double PlaybackTimeMilliseconds { get; private set; }

            public double LastSeekMilliseconds { get; private set; } = double.NaN;

            public IReadOnlyList<AudioBackendCapabilities> Backends => [];

            public ValueTask<IReadOnlyList<AudioDeviceInfo>> GetOutputDevicesAsync(
                CancellationToken cancellationToken = default) =>
                ValueTask.FromResult<IReadOnlyList<AudioDeviceInfo>>([]);

            public ValueTask StartAsync(
                AudioEngineStartRequest request,
                CancellationToken cancellationToken = default)
            {
                status = createStatus(true);
                return ValueTask.CompletedTask;
            }

            public ValueTask PauseAsync(
                CancellationToken cancellationToken = default) =>
                ValueTask.CompletedTask;

            public ValueTask SeekAsync(
                double timeMilliseconds,
                CancellationToken cancellationToken = default)
            {
                PlaybackTimeMilliseconds =
                    LastSeekMilliseconds = timeMilliseconds;
                return ValueTask.CompletedTask;
            }

            public ValueTask StopAsync(
                CancellationToken cancellationToken = default)
            {
                status = createStatus(false);
                return ValueTask.CompletedTask;
            }

            public ValueTask DisposeAsync() =>
                ValueTask.CompletedTask;

            private static AudioEngineStatus createStatus(bool running) =>
                new(
                    AudioBackendKind.Fallback,
                    null,
                    48_000,
                    128,
                    0,
                    false,
                    running,
                    false,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0);
        }
    }
}
