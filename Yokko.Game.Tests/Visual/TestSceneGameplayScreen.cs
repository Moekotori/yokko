using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Animations;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osuTK;
using osuTK.Input;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.PixelFormats;
using Yokko.Audio;
using Yokko.Core.Beatmaps;
using Yokko.Core.Gameplay;
using Yokko.Core.Mods;
using Yokko.Core.Scoring;
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

        [Resolved]
        private IRenderer renderer { get; set; }

        public TestSceneGameplayScreen()
        {
            Add(screenStack = new ScreenStack(new GameplayScreen(DemoBeatmaps.CreateFourKeyDemo()))
            {
                RelativeSizeAxes = Axes.Both,
            });
        }

        [Test]
        public void TestCustomNightcoreAudioPolicyMatchesLazer()
        {
            var audioEngine = new SeekTrackingAudioEngine();
            GameplayScreen gameplay = null;
            string audioPath = Path.Combine(
                TestContext.CurrentContext.WorkDirectory,
                $"{TestContext.CurrentContext.Test.ID}.wav");

            AddStep("open custom Nightcore gameplay", () =>
            {
                File.WriteAllBytes(audioPath, []);
                YokkoBeatmap beatmap =
                    DemoBeatmaps.CreateFourKeyDemo() with
                    {
                        AudioPath = audioPath,
                    };
                var replay = new GameplayReplay(
                    [],
                    ManiaModSet.Empty.WithFixedRate(
                        ManiaModId.Nightcore,
                        1.25));
                gameplay = new GameplayScreen(
                    beatmap,
                    audioEngine,
                    null,
                    null,
                    replay);
                screenStack.Push(gameplay);
            });
            AddUntilStep(
                "audio request started",
                () => audioEngine.LastStartRequest != null);
            AddAssert("NC keeps lazer frequency at custom speed", () =>
                audioEngine.LastStartRequest.PlaybackRate == 1.25
                && audioEngine.LastStartRequest.PitchMode
                   == AudioPitchMode.ScaleWithRate
                && audioEngine.LastStartRequest.FixedFrequencyScale
                   == 1.5
                && gameplay.Mods.FixedRateMod
                   == ManiaModId.Nightcore
                && gameplay.Mods.FixedRateSpeedChange == 1.25);
            AddStep("remove audio fixture", () =>
                File.Delete(audioPath));
        }

        [Test]
        public void TestCinemaHidesGameplayAndUsesAutoReplay()
        {
            GameplayScreen gameplay = null;
            AddStep("open Cinema gameplay", () =>
            {
                gameplay = new GameplayScreen(
                    DemoBeatmaps.CreateFourKeyDemo(),
                    mods: ManiaModSet.Empty.With(
                        ManiaModId.Cinema,
                        true));
                screenStack.Push(gameplay);
            });
            AddUntilStep("Cinema surface loaded", () =>
                gameplay?.ChildrenOfType<GameplayCinemaIndicator>().Any()
                == true);
            AddAssert("Cinema generates auto replay", () =>
                gameplay.ReplayMode && gameplay.AutoplayMode);
            AddAssert("Cinema hides playfield and HUD", () =>
                gameplay.ChildrenOfType<GameplayPlayfield>().Single().Alpha == 0
                && gameplay.ChildrenOfType<GameplayHud>().Single().Alpha == 0
                && gameplay.ChildrenOfType<JudgementReadout>().Single().Alpha == 0);
        }

        [Test]
        public void TestTenKeyGameplayBuildsAllLanes()
        {
            GameplayScreen gameplay = null;
            var beatmap = DemoBeatmaps.CreateFourKeyDemo() with
            {
                KeyMode = KeyMode.TenKey,
                DifficultyName = "10K Test",
                HitObjects = Enumerable.Range(0, 10)
                    .Select(lane => new YokkoHitObject(
                        lane,
                        1000 + lane * 20,
                        null,
                        HitObjectKind.Tap))
                    .ToArray(),
            };

            AddStep("open 10K gameplay", () =>
            {
                gameplay = new GameplayScreen(beatmap);
                screenStack.Push(gameplay);
            });
            AddUntilStep("10K playfield loaded", () =>
                gameplay?.ChildrenOfType<GameplayPlayfield>()
                    .SingleOrDefault()?.KeyCount == 10);
            AddAssert("all ten notes are represented", () =>
                gameplay.ChildrenOfType<GameplayPlayfield>()
                    .Single().ActiveDrawableNoteCount <= 10
                && gameplay.AppliedBeatmap.HitObjects.Count == 10);
        }

        [Test]
        public void TestDualSevenKeyGameplayBuildsTwoStages()
        {
            GameplayScreen gameplay = null;
            YokkoBeatmap beatmap = DemoBeatmaps.CreateSevenKeyDemo() with
            {
                SourceFormat = ChartSourceFormat.OsuStandard,
                ConversionSource = new ManiaConversionSource(
                    4,
                    8,
                    9,
                    6,
                    Enumerable.Range(0, 14)
                        .Select(index =>
                            new ManiaConversionHitObject(
                                index * (511d / 13),
                                1000 + index * 20,
                                1000 + index * 20,
                                ManiaConversionObjectKind.Circle))
                        .ToArray()),
            };
            ManiaModSet mods = ManiaModSet.Empty
                .With(ManiaModId.Key7, true)
                .With(ManiaModId.DualStages, true);

            AddStep("open dual 7K gameplay", () =>
            {
                gameplay = new GameplayScreen(
                    beatmap,
                    mods: mods);
                screenStack.Push(gameplay);
            });
            AddUntilStep("dual playfield loaded", () =>
                gameplay?.ChildrenOfType<GameplayPlayfield>()
                    .SingleOrDefault()?.KeyCount == 14);
            AddAssert("two stages retain their 7-key split", () =>
                gameplay.AppliedBeatmap.StageCount == 2
                && gameplay.AppliedBeatmap.KeysPerStage == 7);
        }

        [Test]
        public void TestClassicConvertedChartUsesLazerConvertWindows()
        {
            GameplayScreen gameplay = null;
            YokkoBeatmap beatmap = DemoBeatmaps.CreateFourKeyDemo() with
            {
                SourceFormat = ChartSourceFormat.OsuStandard,
                ConversionSource = new ManiaConversionSource(
                    4,
                    5,
                    5,
                    5,
                    [
                        new ManiaConversionHitObject(
                            256,
                            1600,
                            1600,
                            ManiaConversionObjectKind.Circle),
                    ]),
            };

            AddStep("open Classic converted gameplay", () =>
            {
                gameplay = new GameplayScreen(
                    beatmap,
                    mods: ManiaModSet.Empty.With(
                        ManiaModId.Classic,
                        true));
                screenStack.Push(gameplay);
            });
            AddAssert("lazer convert windows are selected", () =>
                gameplay.ActiveJudgementWindows.IsConvert
                && gameplay.ActiveJudgementWindows.PerfectMilliseconds == 16.5
                && gameplay.ActiveJudgementWindows.GreatMilliseconds == 34.5
                && gameplay.ActiveJudgementWindows.GoodMilliseconds == 67.5
                && gameplay.ActiveJudgementWindows.OkMilliseconds == 97.5
                && gameplay.ActiveJudgementWindows.MehMilliseconds == 121.5
                && gameplay.ActiveJudgementWindows.MissMilliseconds == 158.5);
        }

        [Test]
        public void TestWindUpAdvancesFrameClockAndHudRate()
        {
            GameplayScreen gameplay = null;
            var beatmap = DemoBeatmaps.CreateFourKeyDemo() with
            {
                HitObjects =
                [
                    new YokkoHitObject(
                        0,
                        0,
                        null,
                        HitObjectKind.Tap),
                    new YokkoHitObject(
                        1,
                        3000,
                        null,
                        HitObjectKind.Tap),
                ],
            };
            ManiaModSet mods = ManiaModSet.Empty.WithTimeRamp(
                ManiaModId.WindUp,
                1,
                1.5,
                true);

            AddStep("open Wind Up gameplay", () =>
            {
                gameplay = new GameplayScreen(
                    beatmap,
                    mods: mods);
                screenStack.Push(gameplay);
            });
            AddAssert("Wind Up keeps lazer Mania hit windows unchanged", () =>
                gameplay.ActiveJudgementWindows.SpeedMultiplier == 1);
            AddUntilStep("Wind Up rate begins increasing", () =>
                gameplay?.ChildrenOfType<GameplayHud>()
                    .SingleOrDefault()?.DisplayedDynamicRate
                    .Contains("1.1") == true);
            AddAssert("frame clock advances through ramp", () =>
                gameplay.CurrentGameplayTime > 0);
        }

        [Test]
        public void TestAdaptiveSpeedRespondsToMisses()
        {
            GameplayScreen gameplay = null;
            var beatmap = DemoBeatmaps.CreateFourKeyDemo() with
            {
                HitObjects = Enumerable.Range(0, 12)
                    .Select(index => new YokkoHitObject(
                        index % 4,
                        index * 100,
                        null,
                        HitObjectKind.Tap))
                    .ToArray(),
            };
            ManiaModSet mods = ManiaModSet.Empty
                .With(ManiaModId.NoFail, true)
                .WithAdaptiveSpeed(1, true);

            AddStep("open Adaptive Speed gameplay", () =>
            {
                gameplay = new GameplayScreen(
                    beatmap,
                    mods: mods);
                screenStack.Push(gameplay);
            });
            AddUntilStep("misses reduce Adaptive Speed", () =>
            {
                string rate = gameplay?
                    .ChildrenOfType<GameplayHud>()
                    .SingleOrDefault()?
                    .DisplayedDynamicRate;
                return rate?.Contains("LIVE RATE 0.") == true;
            });
            AddAssert("adaptive frame clock remains live", () =>
                gameplay.CurrentGameplayTime > 0);
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
                       current.ChildrenOfType<JudgementReadout>().SingleOrDefault() != null;
            });
        }

        [Test]
        public void TestRuntimeAudioTruthAndHitErrorAreVisible()
        {
            GameplayHud hud = null;
            JudgementReadout readout = null;

            AddUntilStep("gameplay feedback loaded", () =>
            {
                Drawable current = screenStack.CurrentScreen as Drawable;
                hud = current?
                      .ChildrenOfType<GameplayHud>()
                      .SingleOrDefault();
                readout = current?
                          .ChildrenOfType<JudgementReadout>()
                          .SingleOrDefault();
                return hud != null && readout != null;
            });
            AddStep("show shared fallback truth", () =>
                hud.UpdateAudioStatus(
                    createAudioStatus(
                        AudioBackendKind.SharedWasapi,
                        running: true,
                        bufferSize: 480,
                        latencyMilliseconds: 10),
                    AudioBackendKind.WasapiExclusive));
            AddAssert("fallback and actual latency are visible", () =>
                hud.DisplayedAudioStatus.Contains("WASAPI SHARED")
                && hud.DisplayedAudioStatus.Contains("480f")
                && hud.DisplayedAudioStatus.Contains("10.00 ms")
                && hud.DisplayedAudioStatus.Contains("FALLBACK"));
            AddStep("show signed hit error", () =>
                readout.Show(new JudgementEvent(
                    0,
                    0,
                    1000,
                    1012.5,
                    12.5,
                    JudgementRating.Perfect)));
            AddAssert("rating and milliseconds are visible", () =>
                readout.DisplayedRating == "PERFECT"
                && readout.DisplayedError == "+12.5 ms");
        }

        [Test]
        public void TestDenseChartOnlyUpdatesVisibleNotes()
        {
            const int totalNotes = 4000;
            int visibleNotes = totalNotes;
            int activeDrawableNotes = totalNotes;
            GameplayPlayfield playfield = null;
            BeatmapJudgementState state = null;

            AddStep("create dense chart", () =>
            {
                var beatmap = new YokkoBeatmap(
                    "Dense visibility fixture",
                    "Yokko",
                    "Codex",
                    "4K",
                    KeyMode.FourKey,
                    ChartSourceFormat.Yokko,
                    [YokkoTimingPoint.Default],
                    null,
                    Enumerable.Range(0, totalNotes)
                              .Select(index => new YokkoHitObject(
                                  index % 4,
                                  index * 10,
                                  null,
                                  HitObjectKind.Tap))
                              .ToArray());
                state = new BeatmapJudgementState(beatmap);
                playfield = new GameplayPlayfield(
                    beatmap,
                    KeyModeBindings.ForMode(KeyMode.FourKey));
                Add(playfield);
            });
            AddUntilStep("dense playfield loaded", () =>
                playfield?.IsLoaded == true);
            AddStep("update dense chart", () =>
            {
                playfield.UpdateGameplayTime(20_000, state);
                visibleNotes = playfield.VisibleNoteCount;
                activeDrawableNotes =
                    playfield.ActiveDrawableNoteCount;
            });
            AddAssert("only the visible window was updated", () =>
                visibleNotes is > 0 and < 300);
            AddAssert("scene graph only contains visible notes", () =>
                activeDrawableNotes == visibleNotes);
            AddStep("seek dense chart forward", () =>
            {
                playfield.UpdateGameplayTime(30_000, state);
                visibleNotes = playfield.VisibleNoteCount;
                activeDrawableNotes =
                    playfield.ActiveDrawableNoteCount;
            });
            AddAssert("forward seek replaces active notes", () =>
                visibleNotes is > 0 and < 300
                && activeDrawableNotes == visibleNotes);
            AddStep("rewind dense chart", () =>
            {
                playfield.UpdateGameplayTime(5_000, state);
                visibleNotes = playfield.VisibleNoteCount;
                activeDrawableNotes =
                    playfield.ActiveDrawableNoteCount;
            });
            AddAssert("rewind reuses preloaded notes", () =>
                visibleNotes is > 0 and < 300
                && activeDrawableNotes == visibleNotes);
            AddStep("remove dense playfield", () =>
                playfield.Expire());
        }

        [Test]
        public void TestDenseHoldChartUsesRangeIndex()
        {
            const int totalHolds = 4000;
            GameplayPlayfield playfield = null;
            BeatmapJudgementState state = null;

            AddStep("create dense hold chart", () =>
            {
                var beatmap = new YokkoBeatmap(
                    "Dense hold range fixture",
                    "Yokko",
                    "Codex",
                    "4K",
                    KeyMode.FourKey,
                    ChartSourceFormat.Yokko,
                    [YokkoTimingPoint.Default],
                    null,
                    Enumerable.Range(0, totalHolds)
                              .Select(index => new YokkoHitObject(
                                  index % 4,
                                  index * 10,
                                  index * 10 + 30,
                                  HitObjectKind.Hold))
                              .ToArray());
                state = new BeatmapJudgementState(beatmap);
                playfield = new GameplayPlayfield(
                    beatmap,
                    KeyModeBindings.ForMode(KeyMode.FourKey));
                Add(playfield);
            });
            AddUntilStep("dense hold playfield loaded", () =>
                playfield?.IsLoaded == true);
            AddStep("query middle of dense hold chart", () =>
                playfield.UpdateGameplayTime(20_000, state));
            AddAssert("hold query skips most chart ranges", () =>
                playfield.LastHoldRangeNodeVisits < totalHolds / 4);
            AddAssert("only visible holds stay active", () =>
                playfield.VisibleNoteCount is > 0 and < 300
                && playfield.ActiveDrawableNoteCount
                   == playfield.VisibleNoteCount);
            AddStep("remove dense hold playfield", () =>
                playfield.Expire());
        }

        [Test]
        public void TestLazerStyleVisibilityModOverlays()
        {
            GameplayPlayfield hiddenPlayfield = null;
            GameplayPlayfield flashlightPlayfield = null;

            AddStep("create Hidden playfield", () =>
            {
                YokkoBeatmap beatmap =
                    DemoBeatmaps.CreateFourKeyDemo();
                hiddenPlayfield = new GameplayPlayfield(
                    beatmap,
                    KeyModeBindings.ForMode(KeyMode.FourKey),
                    mods: new ManiaModSet([ManiaModId.Hidden]));
                Add(hiddenPlayfield);
                hiddenPlayfield.UpdateGameplayTime(
                    0,
                    new BeatmapJudgementState(beatmap));
            });
            AddUntilStep("Hidden cover loaded", () =>
                hiddenPlayfield?.IsLoaded == true);
            AddAssert("Hidden covers notes at receptor side", () =>
            {
                ManiaNoteVisibilityCover cover =
                    hiddenPlayfield
                    .ChildrenOfType<ManiaNoteVisibilityCover>()
                    .SingleOrDefault();
                return cover != null
                       && cover.CoversBottom
                       && Math.Abs(
                           cover.Coverage - 160d / 768) < 0.0001
                       && hiddenPlayfield.VisibilityPolicy.Mode
                          == ManiaVisibilityMode.Hidden;
            });
            AddStep("remove Hidden playfield", () =>
                hiddenPlayfield.Expire());

            AddStep("create Flashlight playfield", () =>
            {
                YokkoBeatmap beatmap =
                    DemoBeatmaps.CreateFourKeyDemo();
                flashlightPlayfield = new GameplayPlayfield(
                    beatmap,
                    KeyModeBindings.ForMode(KeyMode.FourKey),
                    mods: new ManiaModSet(
                    [
                        ManiaModId.Flashlight,
                    ]));
                Add(flashlightPlayfield);
                flashlightPlayfield.UpdateGameplayTime(
                    0,
                    new BeatmapJudgementState(beatmap));
            });
            AddUntilStep("Flashlight overlay loaded", () =>
                flashlightPlayfield?.IsLoaded == true);
            AddAssert("Flashlight uses full-width 50px window", () =>
            {
                ManiaFlashlightOverlay overlay =
                    flashlightPlayfield
                    .ChildrenOfType<ManiaFlashlightOverlay>()
                    .SingleOrDefault();
                return overlay != null
                       && Math.Abs(overlay.WindowSize - 50) < 0.001
                       && flashlightPlayfield.VisibilityPolicy.Mode
                          == ManiaVisibilityMode.Flashlight;
            });
            AddStep("remove Flashlight playfield", () =>
                flashlightPlayfield.Expire());
        }

        [Test]
        public void TestControlScrollResizesPlayfieldWidthOnly()
        {
            GameplayScreen gameplayScreen = null;
            float defaultWidth = 0;
            float defaultHeight = 0;
            float defaultScreenScale = 0;
            float defaultNoteWidth = 0;
            float defaultNoteHeight = 0;
            DrawableNote measuredNote = null;

            AddUntilStep("gameplay layout loaded", () =>
                (gameplayScreen = screenStack.CurrentScreen as GameplayScreen)?
                .ChildrenOfType<GameplayPlayfield>()
                .SingleOrDefault() != null);
            AddStep("capture default geometry", () =>
            {
                GameplayPlayfield playfield = gameplayScreen
                                              .ChildrenOfType<GameplayPlayfield>()
                                              .Single();
                measuredNote = playfield.GetDrawableNote(0);
                defaultWidth = playfield.Width;
                defaultHeight = playfield.Height;
                defaultScreenScale = playfield.Scale.X;
                defaultNoteWidth = measuredNote.Width;
                defaultNoteHeight = measuredNote.Height;
            });
            AddAssert("plain scroll is ignored", () =>
                gameplayScreen.HandlePlayfieldWidthScroll(1, false) == false);
            AddAssert("plain scroll keeps width", () =>
                Math.Abs(
                    gameplayScreen
                    .ChildrenOfType<GameplayPlayfield>()
                    .Single()
                    .Width - defaultWidth) < 0.001);
            AddAssert("control scroll up is handled", () =>
                gameplayScreen.HandlePlayfieldWidthScroll(1, true));
            AddAssert("only playfield width becomes larger", () =>
            {
                GameplayPlayfield playfield = gameplayScreen
                                              .ChildrenOfType<GameplayPlayfield>()
                                              .Single();
                return playfield.Width > defaultWidth
                       && Math.Abs(playfield.Height - defaultHeight) < 0.001
                       && Math.Abs(playfield.Scale.X - defaultScreenScale) < 0.001
                       && Math.Abs(playfield.Scale.X - playfield.Scale.Y) < 0.001;
            });
            AddAssert("note keeps its aspect ratio", () =>
            {
                return measuredNote.Width > defaultNoteWidth
                       && measuredNote.Height > defaultNoteHeight
                       && Math.Abs(
                           measuredNote.Width / defaultNoteWidth
                           - measuredNote.Height / defaultNoteHeight) < 0.001;
            });
            AddAssert("control scroll down is handled", () =>
                gameplayScreen.HandlePlayfieldWidthScroll(-1, true));
            AddAssert("playfield returns to default width", () =>
                Math.Abs(
                    gameplayScreen
                    .ChildrenOfType<GameplayPlayfield>()
                    .Single()
                    .Width - defaultWidth) < 0.001);
            AddStep("scroll repeatedly towards maximum", () =>
            {
                for (int i = 0; i < 100; i++)
                    gameplayScreen.HandlePlayfieldWidthScroll(1, true);
            });
            AddAssert("zoom stops at 250 percent", () =>
            {
                GameplayPlayfield playfield = gameplayScreen
                                              .ChildrenOfType<GameplayPlayfield>()
                                              .Single();
                float maximumWidth = playfield.Width;
                gameplayScreen.HandlePlayfieldWidthScroll(1, true);
                return Math.Abs(maximumWidth - defaultWidth * 2.5f) < 0.001
                       && Math.Abs(playfield.Width - maximumWidth) < 0.001
                       && Math.Abs(playfield.Height - defaultHeight) < 0.001
                       && Math.Abs(playfield.Scale.X - playfield.Scale.Y) < 0.001;
            });
            AddStep("scroll repeatedly towards minimum", () =>
            {
                for (int i = 0; i < 100; i++)
                    gameplayScreen.HandlePlayfieldWidthScroll(-1, true);
            });
            AddAssert("zoom stops safely at 20 percent", () =>
            {
                GameplayPlayfield playfield = gameplayScreen
                                              .ChildrenOfType<GameplayPlayfield>()
                                              .Single();
                float minimumWidth = playfield.Width;
                gameplayScreen.HandlePlayfieldWidthScroll(-1, true);
                return Math.Abs(minimumWidth - defaultWidth * 0.2f) < 0.001
                       && Math.Abs(playfield.Width - minimumWidth) < 0.001
                       && playfield.Width > 0
                       && Math.Abs(playfield.Height - defaultHeight) < 0.001
                       && Math.Abs(playfield.Scale.X - playfield.Scale.Y) < 0.001;
            });
            AddStep("restore default zoom", () =>
            {
                for (int i = 0; i < 8; i++)
                    gameplayScreen.HandlePlayfieldWidthScroll(1, true);
            });
            AddAssert("zoom returns exactly to default", () =>
                Math.Abs(
                    gameplayScreen
                    .ChildrenOfType<GameplayPlayfield>()
                    .Single()
                    .Width - defaultWidth) < 0.001);
        }

        [Test]
        public void TestControlScrollKeepsSkinnedGameplayFullHeight()
        {
            GameplayScreen gameplayScreen = null;
            float defaultPlayfieldWidth = 0;
            float defaultPlayfieldHeight = 0;
            float defaultJudgementPosition = 0;
            float defaultNoteWidth = 0;
            float defaultNoteHeight = 0;
            GameplayPlayfield playfield = null;
            DrawableNote measuredNote = null;

            AddStep("open skinned gameplay", () =>
            {
                gameplayScreen = new GameplayScreen(
                    DemoBeatmaps.CreateFourKeyDemo(),
                    skinPath: createTestSkin());
                screenStack.Push(gameplayScreen);
            });
            AddUntilStep("skinned geometry loaded", () =>
            {
                playfield = gameplayScreen
                            .ChildrenOfType<GameplayPlayfield>()
                            .SingleOrDefault();
                measuredNote = playfield?.GetDrawableNote(0);
                return measuredNote?.Width > 0;
            });
            AddStep("capture skinned geometry", () =>
            {
                defaultPlayfieldWidth = playfield.Width;
                defaultPlayfieldHeight = playfield.Height;
                defaultJudgementPosition = playfield.JudgementPosition;
                defaultNoteWidth = measuredNote.Width;
                defaultNoteHeight = measuredNote.Height;
            });
            AddStep("widen skinned playfield", () =>
                gameplayScreen.HandlePlayfieldWidthScroll(1, true));
            AddAssert("skinned gameplay remains full height", () =>
            {
                GameplayPlayfield playfield = gameplayScreen
                                              .ChildrenOfType<GameplayPlayfield>()
                                              .Single();
                Drawable current = gameplayScreen;
                return Math.Abs(playfield.Width - defaultPlayfieldWidth * 1.1f) < 0.001
                       && Math.Abs(playfield.Height - defaultPlayfieldHeight) < 0.001
                       && Math.Abs(playfield.JudgementPosition - defaultJudgementPosition) < 0.001
                       && Math.Abs(
                           playfield.Height * playfield.Scale.Y
                           - current.DrawHeight) < 0.5;
            });
            AddAssert("skinned note stays proportional", () =>
            {
                return Math.Abs(measuredNote.Width - defaultNoteWidth * 1.1f) < 0.001
                       && Math.Abs(measuredNote.Height - defaultNoteHeight * 1.1f) < 0.001;
            });
            AddStep("restore skinned width", () =>
                gameplayScreen.HandlePlayfieldWidthScroll(-1, true));
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
        public void TestRuntimeAudioFaultBlocksWithoutRewindingClock()
        {
            var audioEngine = new SeekTrackingAudioEngine();
            YokkoBeatmap beatmap = DemoBeatmaps.CreateFourKeyDemo() with
            {
                AudioPath = "runtime-fault-fixture.wav",
            };
            GameplayScreen gameplayScreen = null;
            double timeBeforeFault = 0;

            AddStep("open gameplay with runtime audio", () =>
            {
                gameplayScreen = new GameplayScreen(beatmap, audioEngine);
                screenStack.Push(gameplayScreen);
            });
            AddUntilStep("audio starts", () =>
                audioEngine.StartCount == 1);
            AddStep("advance audio clock", () =>
                audioEngine.SetPlaybackTime(1234));
            AddUntilStep("gameplay observes audio clock", () =>
                gameplayScreen.CurrentGameplayTime >= 1234);
            AddStep("capture clock and fault output", () =>
            {
                timeBeforeFault = gameplayScreen.CurrentGameplayTime;
                audioEngine.Fault(unchecked((int)0x88890004), 15);
            });
            AddUntilStep("runtime fault blocks gameplay", () =>
                gameplayScreen.GameplayBlocked);
            AddAssert("fault keeps last stable clock", () =>
                Math.Abs(
                    gameplayScreen.CurrentGameplayTime
                    - timeBeforeFault) < 0.01);
            AddAssert("runtime failure details are visible", () =>
                gameplayScreen
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
            AddUntilStep("result mascot GIF decoded", () =>
                (screenStack.CurrentScreen as Drawable)?
                .ChildrenOfType<GameplayResultOverlay>()
                .SingleOrDefault()?
                .MascotFrameCount == 15);
            AddAssert("result exposes three actions", () =>
                (screenStack.CurrentScreen as Drawable)?
                .ChildrenOfType<GameplayResultOverlay>()
                .SingleOrDefault()?
                .ActionCount == 3);
            AddStep("watch replay", () =>
                (screenStack.CurrentScreen as Drawable)?
                .ChildrenOfType<GameplayResultOverlay>()
                .Single()
                .TriggerReplay());
            AddUntilStep("recorded run is replaying", () =>
                (screenStack.CurrentScreen as GameplayScreen)?
                .ReplayMode == true);
        }

        [Test]
        public void TestCompletedLivePlayPersistsExactNativeReplay()
        {
            YokkoBeatmap beatmap = DemoBeatmaps.CreateFourKeyDemo() with
            {
                Title = "Native Replay Persistence Test",
                HitObjects =
                [
                    new YokkoHitObject(
                        0,
                        0,
                        null,
                        HitObjectKind.Tap),
                ],
            };
            ManiaModSet mods = ManiaModSet.Empty
                .WithRandomSeed(97531)
                .WithCover(
                    0.64,
                    ManiaCoverDirection.AgainstScroll)
                .WithDifficultyAdjust(10.5, -2, true)
                .WithMuted(true, false, 180, false)
                .WithFixedRate(
                    ManiaModId.HalfTime,
                    0.82,
                    true);
            GameplayScreen gameplay = null;

            AddStep("open configured live gameplay", () =>
                screenStack.Push(gameplay = new GameplayScreen(
                    beatmap,
                    mods: mods)));
            AddUntilStep("configured live play completes", () =>
                gameplay?.GameplayCompleted == true);
            AddAssert("native replay was persisted", () =>
                !string.IsNullOrWhiteSpace(gameplay.SavedReplayPath)
                && File.Exists(gameplay.SavedReplayPath));
            AddAssert("persisted replay restores exact session", () =>
            {
                YokkoReplayLoadResult restored =
                    YokkoReplayIO.ReadFromFile(
                        gameplay.SavedReplayPath);
                return restored.Replay.Mods.Equals(mods)
                       && restored.KeyCount == 4
                       && restored.BeatmapFingerprint
                       == YokkoBeatmapFingerprint.Compute(beatmap);
            });
            AddStep("remove native replay fixture", () =>
                File.Delete(gameplay.SavedReplayPath));
        }

        [Test]
        public void TestLazerModMultiplierIsAppliedToGameplayScore()
        {
            YokkoBeatmap beatmap = DemoBeatmaps.CreateFourKeyDemo() with
            {
                Title = "Mod Multiplier Result Test",
                HitObjects =
                [
                    new YokkoHitObject(
                        0,
                        0,
                        null,
                        HitObjectKind.Tap),
                ],
            };
            GameplayScreen gameplay = null;

            AddStep("open Easy autoplay gameplay", () =>
                screenStack.Push(gameplay = new GameplayScreen(
                    beatmap,
                    mods: new ManiaModSet(
                    [
                        ManiaModId.Easy,
                        ManiaModId.Autoplay,
                    ]))));
            AddUntilStep("gameplay completes", () =>
                gameplay?.GameplayCompleted == true);
            AddAssert("Easy applies lazer 0.5x score multiplier", () =>
                gameplay.CompletedResult.Score == 500_000
                && gameplay.CompletedResult.Perfect == 1);
        }

        [Test]
        public void TestPerfectFailureShowsDedicatedFailOverlay()
        {
            YokkoBeatmap beatmap = DemoBeatmaps.CreateFourKeyDemo() with
            {
                Title = "Perfect Failure Test",
                HitObjects =
                [
                    new YokkoHitObject(
                        0,
                        0,
                        null,
                        HitObjectKind.Tap),
                ],
            };

            AddStep("open Perfect gameplay", () =>
                screenStack.Push(new GameplayScreen(
                    beatmap,
                    mods: new ManiaModSet(
                    [
                        ManiaModId.Perfect,
                    ]))));
            AddUntilStep("Perfect run fails on miss", () =>
                (screenStack.CurrentScreen as GameplayScreen)?
                .GameplayFailed == true);
            AddAssert("result is not saved after failure", () =>
                (screenStack.CurrentScreen as GameplayScreen)?
                .GameplayCompleted == false);
            AddAssert("dedicated fail overlay identifies Perfect", () =>
                (screenStack.CurrentScreen as Drawable)?
                .ChildrenOfType<GameplayFailOverlay>()
                .SingleOrDefault()?
                .Reason == ManiaFailReason.PerfectBroken);
        }

        [Test]
        public void TestPerfectStrictSettingMatchesLazerGreatBoundary()
        {
            YokkoBeatmap beatmap = DemoBeatmaps.CreateFourKeyDemo() with
            {
                Title = "Perfect Strict Boundary Test",
                HitObjects =
                [
                    new YokkoHitObject(
                        0,
                        1000,
                        null,
                        HitObjectKind.Tap),
                ],
            };
            var replay = new GameplayReplay(
            [
                new GameplayReplayInput(0, true, 1020),
                new GameplayReplayInput(0, false, 1100),
            ]);
            GameplayScreen defaultPerfect = null;
            GameplayScreen strictPerfect = null;

            AddStep("play Great with default Perfect", () =>
                screenStack.Push(defaultPerfect =
                    new GameplayScreen(
                        beatmap,
                        null,
                        null,
                        ManiaModSet.Empty.WithPerfect(false),
                        replay)));
            AddUntilStep("default Perfect accepts Great", () =>
                defaultPerfect?.GameplayCompleted == true);
            AddAssert("default run recorded Great", () =>
                !defaultPerfect.GameplayFailed
                && defaultPerfect.CompletedResult.Great == 1);
            AddStep("play Great with strict Perfect", () =>
                screenStack.Push(strictPerfect =
                    new GameplayScreen(
                        beatmap,
                        null,
                        null,
                        ManiaModSet.Empty.WithPerfect(true),
                        replay)));
            AddUntilStep("strict Perfect rejects Great", () =>
                strictPerfect?.GameplayFailed == true);
            AddAssert("strict setting reaches fail overlay", () =>
                strictPerfect
                    .ChildrenOfType<GameplayFailOverlay>()
                    .SingleOrDefault()?
                    .Reason == ManiaFailReason.PerfectBroken);
        }

        [Test]
        public void TestHardRockAndAccuracyChallengeRuntime()
        {
            YokkoBeatmap beatmap = DemoBeatmaps.CreateFourKeyDemo() with
            {
                Title = "HR AC Runtime Test",
                HitObjects =
                [
                    new YokkoHitObject(
                        0,
                        0,
                        null,
                        HitObjectKind.Tap),
                ],
            };
            GameplayScreen gameplay = null;

            AddStep("open HR AC gameplay", () =>
                screenStack.Push(gameplay = new GameplayScreen(
                    beatmap,
                    mods: ManiaModSet.Empty
                                          .With(
                                              ManiaModId.HardRock,
                                              true)
                                          .WithAccuracyChallenge(
                                              0.9,
                                              ManiaAccuracyMode
                                                  .MaximumAchievable))));
            AddUntilStep("HR runtime is loaded", () =>
                gameplay?.HealthState != null
                && gameplay.ActiveJudgementWindows != null);
            AddAssert("HR caps HP drain and tightens windows", () =>
                gameplay.HealthState.EffectiveDrainRate
                == Math.Min(10, beatmap.DrainRate * 1.4)
                && gameplay.ActiveJudgementWindows
                           .DifficultyMultiplier == 1.4);
            AddUntilStep("AC fails when target is unrecoverable", () =>
                gameplay.GameplayFailed);
            AddAssert("AC failure reason is presented", () =>
                gameplay.ChildrenOfType<GameplayFailOverlay>()
                        .SingleOrDefault()?
                        .Reason
                == ManiaFailReason.AccuracyChallenge);
        }

        [Test]
        public void TestDifficultyAdjustAndConstantSpeedRuntime()
        {
            YokkoBeatmap beatmap = DemoBeatmaps.CreateFourKeyDemo() with
            {
                Title = "DA CS Runtime Test",
                InitialScrollVelocity = 2,
                ScrollVelocities =
                [
                    new YokkoScrollVelocity(500, 0.25),
                ],
                HitObjects =
                [
                    new YokkoHitObject(
                        0,
                        1000,
                        null,
                        HitObjectKind.Tap),
                ],
            };
            GameplayScreen gameplay = null;

            AddStep("open DA CS gameplay", () =>
                screenStack.Push(gameplay = new GameplayScreen(
                    beatmap,
                    mods: ManiaModSet.Empty
                                          .WithDifficultyAdjust(
                                              9.5,
                                              12,
                                              true)
                                          .With(
                                              ManiaModId.ConstantSpeed,
                                              true))));
            AddUntilStep("DA CS runtime is loaded", () =>
                gameplay?.HealthState != null
                && gameplay.ActiveJudgementWindows != null
                && gameplay.ChildrenOfType<GameplayPlayfield>()
                           .SingleOrDefault() != null);
            AddAssert("DA overrides HP and extended OD", () =>
                gameplay.HealthState.EffectiveDrainRate == 9.5
                && gameplay.ActiveJudgementWindows.OverallDifficulty
                == 12);
            AddAssert("CS uses linear chart-time positions", () =>
            {
                GameplayPlayfield playfield =
                    gameplay.ChildrenOfType<GameplayPlayfield>()
                            .Single();
                return playfield.ConstantSpeedEnabled
                       && playfield.GetNoteStartScrollPosition(0)
                       == 1000;
            });
            AddAssert("HUD presents both active rules", () =>
                gameplay.ChildrenOfType<GameplayHud>()
                        .Single()
                        .DisplayedRuleStatus
                        .Contains("DA HP 9.5")
                && gameplay.ChildrenOfType<GameplayHud>()
                           .Single()
                           .DisplayedRuleStatus
                           .Contains("CS"));
        }

        [Test]
        public void TestLoadsOsuManiaSkinTextures()
        {
            string skinPath = createTestSkin();
            GameplayScreen gameplay = null;

            AddStep("open skinned gameplay", () =>
                screenStack.Push(gameplay = new GameplayScreen(
                    DemoBeatmaps.CreateFourKeyDemo(),
                    skinPath: skinPath)));
            AddUntilStep("custom playfield width applied", () =>
                gameplay?.ChildrenOfType<GameplayPlayfield>().SingleOrDefault()?.Width == 160);
            AddUntilStep("skin sprites loaded", () =>
                gameplay?.ChildrenOfType<Sprite>().Any(sprite => sprite.Texture != null) == true);
            AddAssert("skin owns judgement feedback", () =>
                gameplay.ChildrenOfType<GameplayPlayfield>()
                        .Single()
                        .UsesSkinJudgementOverlay);
        }

        [Test]
        public void TestPreparesOsuManiaSkinHitSounds()
        {
            string skinPath = createTestSkin();
            string hitSoundPath = Path.Combine(
                skinPath,
                "normal-hitnormal.wav");
            File.WriteAllBytes(hitSoundPath, [1, 2, 3]);
            var audioEngine = new SampleTrackingAudioEngine();

            AddStep("open gameplay with skin hit sounds", () =>
                screenStack.Push(new GameplayScreen(
                    DemoBeatmaps.CreateFourKeyDemo(),
                    audioEngine,
                    skinPath)));
            AddUntilStep("skin hit sound is prepared", () =>
                audioEngine.PreparedSamples.Contains(hitSoundPath));
        }

        [Test]
        public void TestOversizedHoldBodyIsConstrainedBeforeUpload()
        {
            string skinPath = null;

            AddStep("create oversized hold body", () =>
            {
                skinPath = createTestSkin();
                string holdBodyPath = Path.Combine(skinPath, "hold-body.png");

                using var image = new Image<Rgba32>(
                    8,
                    Math.Max(renderer.MaxTextureSize + 128, 20_000),
                    new Rgba32(72, 208, 240, 255));
                image.Save(holdBodyPath, new TiffEncoder());
            });
            AddStep("open chart with oversized hold body", () =>
                screenStack.Push(new GameplayScreen(
                    createHoldDemo(KeyMode.FourKey),
                    skinPath: skinPath)));
            AddUntilStep("hold body is safe for renderer", () =>
            {
                DrawableNote hold = (screenStack.CurrentScreen as Drawable)?
                                    .ChildrenOfType<GameplayPlayfield>()
                                    .SingleOrDefault()?
                                    .GetDrawableNote(0);
                Texture body = hold?
                               .ChildrenOfType<Sprite>()
                               .Select(sprite => sprite.Texture)
                               .Where(texture => texture != null)
                               .OrderByDescending(texture => texture.DisplayHeight)
                               .FirstOrDefault();

                return body?.Available == true
                       && body.DisplayHeight <= renderer.MaxTextureSize
                       && body.DisplayHeight > 1000;
            });
        }

        [Test]
        public void TestLongHoldBodyConnectsBehindRoundEndpoints()
        {
            string skinPath = null;

            AddStep("create long hold body", () =>
            {
                skinPath = createTestSkin();

                using var image = new Image<Rgba32>(
                    8,
                    400,
                    new Rgba32(142, 136, 145, 255));
                image.SaveAsPng(Path.Combine(skinPath, "hold-body.png"));
            });
            AddStep("open chart with long hold body", () =>
                screenStack.Push(new GameplayScreen(
                    createHoldDemo(KeyMode.FourKey),
                    skinPath: skinPath)));
            AddUntilStep("long hold geometry loaded", () =>
            {
                DrawableNote hold = (screenStack.CurrentScreen as Drawable)?
                                    .ChildrenOfType<GameplayPlayfield>()
                                    .SingleOrDefault()?
                                    .GetDrawableNote(0);
                Sprite body = hold?
                              .ChildrenOfType<Sprite>()
                              .FirstOrDefault(sprite =>
                                  sprite.Texture?.DisplayHeight > 100);
                Sprite[] endpoints = hold?
                                     .ChildrenOfType<Sprite>()
                                     .Where(sprite =>
                                         sprite.Texture?.DisplayHeight == 8)
                                     .ToArray();

                if (hold == null || body?.Parent is not Container clip
                    || endpoints?.Length != 2)
                    return false;

                hold.UpdatePosition(
                    1000,
                    false,
                    false,
                    0,
                    460,
                    1800);

                float upperEndpointCentre = endpoints.Min(sprite => sprite.Y);
                float lowerEndpointCentre = endpoints.Max(sprite => sprite.Y);
                return clip.Y <= upperEndpointCentre + 0.01f
                       && clip.Y + clip.Height
                       >= lowerEndpointCentre - 0.01f;
            });
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
                DrawableNote firstNote = playfield?.GetDrawableNote(0);
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
                                         .ChildrenOfType<GameplayPlayfield>()
                                         .SingleOrDefault()?
                                         .GetDrawableNote(0);

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
                GameplayPlayfield playfield = (screenStack.CurrentScreen as Drawable)?
                                              .ChildrenOfType<GameplayPlayfield>()
                                              .SingleOrDefault();
                DrawableNote[] notes = playfield == null
                    ? null
                    : Enumerable.Range(0, 4)
                                .Select(playfield.GetDrawableNote)
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
                                    .ChildrenOfType<GameplayPlayfield>()
                                    .SingleOrDefault()?
                                    .GetDrawableNote(0);
                Sprite longBody = hold?
                                  .ChildrenOfType<Sprite>()
                                  .FirstOrDefault(sprite => sprite.Texture?.DisplayHeight > 1000);
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
                GameplayPlayfield playfield = current?
                                              .ChildrenOfType<GameplayPlayfield>()
                                              .SingleOrDefault();
                bool noteFlipped = playfield?
                                   .GetDrawableNote(0)
                                   .ChildrenOfType<Sprite>()
                                   .Any(sprite => sprite.Scale.Y < 0) == true;
                bool keyFlipped = current?
                                  .ChildrenOfType<LaneColumn>()
                                  .SelectMany(lane => lane.ReceptorLayer.ChildrenOfType<Sprite>())
                                  .Any(sprite => sprite.Scale.Y < 0) == true;
                return noteFlipped && keyFlipped;
            });
        }

        [Test]
        public void TestLegacySkinHitPositionAdjustsTimeRange()
        {
            double originalSpeed = OsuManiaScrollSpeed.Default;
            const double testSpeed = 34;
            GameplayPlayfield playfield = null;

            AddStep("open HitPosition 460 skin", () =>
            {
                originalSpeed = gameplaySettings.ScrollSpeed.Value;
                gameplaySettings.SetScrollSpeed(testSpeed);
                screenStack.Push(new GameplayScreen(
                    DemoBeatmaps.CreateFourKeyDemo(),
                    skinPath: createTestSkin("HitPosition: 460")));
            });
            AddUntilStep("osu skin time range applied", () =>
            {
                playfield = (screenStack.CurrentScreen as Drawable)?
                            .ChildrenOfType<GameplayPlayfield>()
                            .SingleOrDefault();
                return playfield != null
                       && Math.Abs(
                           playfield.ApproachTimeMilliseconds
                           - OsuManiaScrollSpeed.ComputeScrollTime(
                               testSpeed,
                               460)) < 0.001;
            });
            AddStep("press first lane", () =>
                playfield.SetLanePressed(0, true));
            AddAssert("skin lane light turns on", () =>
                playfield.ChildrenOfType<TextureAnimation>()
                         .Any(animation =>
                             animation.Name == "Lane light"
                             && animation.Alpha > 0.99f));
            AddStep("show perfect hit", () =>
                playfield.ApplyJudgement(new JudgementEvent(
                    0,
                    0,
                    1000,
                    1000,
                    0,
                    JudgementRating.Perfect)));
            AddUntilStep("skin hit explosion appears", () =>
                playfield.ChildrenOfType<TextureAnimation>()
                         .Any(animation =>
                             animation.Name == "Hit explosion"
                             && animation.Alpha > 0));
            AddStep("show overlapping hits", () =>
            {
                playfield.ApplyJudgement(new JudgementEvent(
                    0,
                    0,
                    1000,
                    1000,
                    0,
                    JudgementRating.Perfect));
                playfield.ApplyJudgement(new JudgementEvent(
                    0,
                    0,
                    1000,
                    1000,
                    0,
                    JudgementRating.Perfect));
            });
            AddAssert("rapid hits keep two explosions", () =>
                playfield.ChildrenOfType<TextureAnimation>()
                         .Count(animation =>
                             animation.Name == "Hit explosion"
                             && animation.Alpha > 0) >= 2);
            AddStep("release first lane", () =>
                playfield.SetLanePressed(0, false));
            AddStep("restore scroll speed", () =>
                gameplaySettings.SetScrollSpeed(originalSpeed));
        }

        [Test]
        public void TestOsuManiaScrollSpeedShortcuts()
        {
            double originalSpeed = OsuManiaScrollSpeed.Default;
            Key originalDecreaseKey = Key.F3;
            Key originalIncreaseKey = Key.F4;
            GameplayScreen gameplayScreen = null;

            AddStep("save and reset scroll speed", () =>
            {
                originalSpeed = gameplaySettings.ScrollSpeed.Value;
                originalDecreaseKey =
                    gameplaySettings.DecreaseScrollSpeedKey.Value;
                originalIncreaseKey =
                    gameplaySettings.IncreaseScrollSpeedKey.Value;
                gameplaySettings.ResetShortcutBindings();
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
            AddStep("customise Mania speed shortcuts", () =>
            {
                gameplaySettings.SetShortcutBinding(
                    ManiaShortcutAction.DecreaseScrollSpeed,
                    Key.F7);
                gameplaySettings.SetShortcutBinding(
                    ManiaShortcutAction.IncreaseScrollSpeed,
                    Key.F8);
            });
            AddStep("old F4 is ignored", () =>
                gameplayScreen.HandleScrollSpeedShortcut(
                    Key.F4,
                    false));
            AddAssert("old key keeps speed 8", () =>
                gameplaySettings.ScrollSpeed.Value == 8);
            AddStep("custom F8 increases speed", () =>
                gameplayScreen.HandleScrollSpeedShortcut(
                    Key.F8,
                    false));
            AddAssert("custom increase reaches 9", () =>
                gameplaySettings.ScrollSpeed.Value == 9);
            AddStep("custom F7 decreases speed", () =>
                gameplayScreen.HandleScrollSpeedShortcut(
                    Key.F7,
                    false));
            AddAssert("custom decrease restores 8", () =>
                gameplaySettings.ScrollSpeed.Value == 8);
            AddStep("restore scroll speed and shortcuts", () =>
            {
                gameplaySettings.SetScrollSpeed(originalSpeed);
                gameplaySettings.SetShortcutBinding(
                    ManiaShortcutAction.DecreaseScrollSpeed,
                    originalDecreaseKey);
                gameplaySettings.SetShortcutBinding(
                    ManiaShortcutAction.IncreaseScrollSpeed,
                    originalIncreaseKey);
            });
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
            AddAssert("second skip is rejected", () =>
                gameplayScreen.HandleIntroSkip() == false);
            AddAssert("intro is no longer skippable", () =>
                gameplayScreen.IntroSkipAvailable == false);
        }

        [Test]
        public void TestAudioStartsAfterGameplayLeadIn()
        {
            var audioEngine = new SeekTrackingAudioEngine();
            YokkoBeatmap beatmap = DemoBeatmaps.CreateFourKeyDemo() with
            {
                AudioPath = "lead-in-fixture.mp3",
            };

            AddStep("open gameplay with audio", () =>
                screenStack.Push(new GameplayScreen(beatmap, audioEngine)));
            AddAssert("audio does not start immediately", () =>
                audioEngine.StartCount == 0);
            AddUntilStep("audio starts after lead-in", () =>
                audioEngine.StartCount == 1);
        }

        [Test]
        public void TestPauseOverlayStopsAndResumesAudio()
        {
            var audioEngine = new SeekTrackingAudioEngine();
            YokkoBeatmap beatmap = DemoBeatmaps.CreateFourKeyDemo() with
            {
                AudioPath = "pause-fixture.mp3",
                Title = "Pulse Bloom",
                DifficultyName = "4K Normal",
            };
            GameplayScreen gameplayScreen = null;
            Key originalPauseKey = Key.Escape;
            Key originalMenuNextKey = Key.Down;

            AddStep("open gameplay with audio", () =>
            {
                originalPauseKey = gameplaySettings.PauseOrBackKey.Value;
                originalMenuNextKey = gameplaySettings.MenuNextKey.Value;
                gameplaySettings.SetShortcutBinding(
                    ManiaShortcutAction.PauseOrBack,
                    Key.F10);
                gameplaySettings.SetShortcutBinding(
                    ManiaShortcutAction.MenuNext,
                    Key.F11);
                gameplayScreen = new GameplayScreen(beatmap, audioEngine);
                screenStack.Push(gameplayScreen);
            });
            AddUntilStep("audio starts", () =>
                audioEngine.StartCount == 1);
            AddStep("pause gameplay", () =>
                gameplayScreen.TogglePause());
            AddUntilStep("pause completes", () =>
                gameplayScreen.IsPaused
                && !gameplayScreen.PauseTransitionInProgress
                && audioEngine.PauseCount == 1);
            AddAssert("gameplay remains current screen", () =>
                ReferenceEquals(screenStack.CurrentScreen, gameplayScreen));
            AddAssert("pause overlay is visible with resume selected", () =>
            {
                GameplayPauseOverlay overlay = gameplayScreen
                                               .ChildrenOfType<GameplayPauseOverlay>()
                                               .SingleOrDefault();
                return overlay?.ActionCount == 4
                       && overlay.SelectedAction == 0;
            });
            AddStep("capture pause screen when requested", () =>
            {
                string outputPath = Environment.GetEnvironmentVariable(
                    "YOKKO_PAUSE_SCREENSHOT");
                if (string.IsNullOrWhiteSpace(outputPath))
                    return;

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
            });
            AddStep("old menu key is ignored", () =>
                gameplayScreen
                    .ChildrenOfType<GameplayPauseOverlay>()
                    .Single()
                    .HandleKey(Key.Down));
            AddAssert("selection remains on resume", () =>
                gameplayScreen
                    .ChildrenOfType<GameplayPauseOverlay>()
                    .Single()
                    .SelectedAction == 0);
            AddStep("custom menu key moves selection", () =>
                gameplayScreen
                    .ChildrenOfType<GameplayPauseOverlay>()
                    .Single()
                    .HandleKey(Key.F11));
            AddAssert("restart becomes selected", () =>
                gameplayScreen
                    .ChildrenOfType<GameplayPauseOverlay>()
                    .Single()
                    .SelectedAction == 1);
            AddStep("old pause key is ignored", () =>
                gameplayScreen
                    .ChildrenOfType<GameplayPauseOverlay>()
                    .Single()
                    .HandleKey(Key.Escape));
            AddAssert("gameplay stays paused", () =>
                gameplayScreen.IsPaused);
            AddStep("custom pause key resumes gameplay", () =>
                gameplayScreen
                    .ChildrenOfType<GameplayPauseOverlay>()
                    .Single()
                    .HandleKey(Key.F10));
            AddUntilStep("resume completes", () =>
                !gameplayScreen.IsPaused
                && !gameplayScreen.PauseTransitionInProgress
                && audioEngine.SeekCount == 1);
            AddAssert("pause overlay is removed", () =>
                gameplayScreen
                    .ChildrenOfType<GameplayPauseOverlay>()
                    .Any() == false);
            AddAssert("audio is running again", () =>
                audioEngine.Status.IsRunning);
            AddStep("restore pause shortcuts", () =>
            {
                gameplaySettings.SetShortcutBinding(
                    ManiaShortcutAction.PauseOrBack,
                    originalPauseKey);
                gameplaySettings.SetShortcutBinding(
                    ManiaShortcutAction.MenuNext,
                    originalMenuNextKey);
            });
        }

        [Test]
        public void TestLosingHostFocusPausesGameplay()
        {
            var audioEngine = new SeekTrackingAudioEngine();
            YokkoBeatmap beatmap = DemoBeatmaps.CreateFourKeyDemo() with
            {
                AudioPath = "focus-loss-fixture.mp3",
            };
            GameplayScreen gameplayScreen = null;
            bool originalPauseWhenUnfocused = true;

            AddStep("open gameplay with audio", () =>
            {
                originalPauseWhenUnfocused =
                    gameplaySettings.PauseWhenUnfocused.Value;
                gameplaySettings.PauseWhenUnfocused.Value = false;
                gameplayScreen = new GameplayScreen(beatmap, audioEngine);
                screenStack.Push(gameplayScreen);
            });
            AddUntilStep("audio starts", () =>
                audioEngine.StartCount == 1);
            AddStep("deactivate host while disabled", () =>
                gameplayScreen.HandleHostDeactivated());
            AddAssert("disabled setting keeps gameplay running", () =>
                !gameplayScreen.IsPaused
                && audioEngine.PauseCount == 0);
            AddStep("enable pause when unfocused", () =>
                gameplaySettings.PauseWhenUnfocused.Value = true);
            AddStep("deactivate host", () =>
                gameplayScreen.HandleHostDeactivated());
            AddUntilStep("focus loss pauses safely", () =>
                gameplayScreen.IsPaused
                && !gameplayScreen.PauseTransitionInProgress
                && audioEngine.PauseCount == 1);
            AddAssert("repeated deactivation is idempotent", () =>
            {
                gameplayScreen.HandleHostDeactivated();
                return audioEngine.PauseCount == 1;
            });
            AddStep("restore pause preference", () =>
                gameplaySettings.PauseWhenUnfocused.Value =
                    originalPauseWhenUnfocused);
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
                         "mania-stage-light.png",
                         "lightingN.png",
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

            public double DurationMilliseconds => 0;

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

        private class SeekTrackingAudioEngine : IAudioEngine
        {
            private AudioEngineStatus status = createStatus(false);

            public AudioEngineStatus Status => status;

            public double PlaybackTimeMilliseconds { get; private set; }

            public double DurationMilliseconds => 0;

            public double LastSeekMilliseconds { get; private set; } = double.NaN;

            public int StartCount { get; private set; }

            public AudioEngineStartRequest LastStartRequest { get; private set; }

            public int PauseCount { get; private set; }

            public int SeekCount { get; private set; }

            public IReadOnlyList<AudioBackendCapabilities> Backends => [];

            public ValueTask<IReadOnlyList<AudioDeviceInfo>> GetOutputDevicesAsync(
                CancellationToken cancellationToken = default) =>
                ValueTask.FromResult<IReadOnlyList<AudioDeviceInfo>>([]);

            public ValueTask StartAsync(
                AudioEngineStartRequest request,
                CancellationToken cancellationToken = default)
            {
                StartCount++;
                LastStartRequest = request;
                status = createStatus(true);
                return ValueTask.CompletedTask;
            }

            public ValueTask PauseAsync(
                CancellationToken cancellationToken = default)
            {
                PauseCount++;
                status = createStatus(false);
                return ValueTask.CompletedTask;
            }

            public ValueTask SeekAsync(
                double timeMilliseconds,
                CancellationToken cancellationToken = default)
            {
                // Model an engine whose post-seek clock is relative to the
                // newly-created playback core. Gameplay must still treat the
                // intro skip as a one-shot action.
                SeekCount++;
                PlaybackTimeMilliseconds = 0;
                LastSeekMilliseconds = timeMilliseconds;
                status = createStatus(true);
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

            public void SetPlaybackTime(double milliseconds) =>
                PlaybackTimeMilliseconds = milliseconds;

            public void Fault(int backendError, uint backendErrorStage)
            {
                status = status with
                {
                    IsRunning = false,
                    IsFaulted = true,
                    BackendError = backendError,
                    BackendErrorStage = backendErrorStage,
                };
            }

            private static AudioEngineStatus createStatus(bool running) =>
                createAudioStatus(
                    AudioBackendKind.WasapiExclusive,
                    running,
                    144,
                    3);
        }

        private sealed class SampleTrackingAudioEngine
            : SeekTrackingAudioEngine, IAudioSamplePlayback
        {
            public HashSet<string> PreparedSamples { get; } =
                new(StringComparer.OrdinalIgnoreCase);

            public ValueTask PrepareSamplesAsync(
                IReadOnlyCollection<string> samplePaths,
                CancellationToken cancellationToken = default)
            {
                PreparedSamples.UnionWith(samplePaths);
                return ValueTask.CompletedTask;
            }

            public bool TriggerSample(string samplePath) => true;
        }

        private static AudioEngineStatus createAudioStatus(
            AudioBackendKind backend,
            bool running,
            int bufferSize,
            double latencyMilliseconds) =>
            new(
                backend,
                "Test output",
                48_000,
                bufferSize,
                latencyMilliseconds,
                backend == AudioBackendKind.WasapiExclusive,
                running,
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
    }
}
