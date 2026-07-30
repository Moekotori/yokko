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
using osuTK.Graphics;
using osuTK.Input;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.PixelFormats;
using Yokko.Audio;
using Yokko.Core.Beatmaps;
using Yokko.Core.Difficulty;
using Yokko.Core.Gameplay;
using Yokko.Core.Mods;
using Yokko.Core.Scoring;
using Yokko.Core.Timing;
using Yokko.Game.Gameplay;
using Yokko.Game.Presentation;
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
                && gameplay.ChildrenOfType<JudgementReadout>().Single().Alpha == 0
                && gameplay.ChildrenOfType<GameplayTimingBar>().Single().Alpha == 0);
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
            GameplayScreen gameplay = null;
            GameplayPlayfield playfield = null;
            GameplayHud hud = null;
            JudgementReadout readout = null;
            GameplayTimingBar timingBar = null;

            AddStep("open gameplay feedback fixture", () =>
            {
                gameplay = new GameplayScreen(
                    DemoBeatmaps.CreateFourKeyDemo());
                screenStack.Push(gameplay);
            });
            AddUntilStep("gameplay feedback loaded", () =>
            {
                playfield = gameplay?
                            .ChildrenOfType<GameplayPlayfield>()
                            .SingleOrDefault();
                hud = gameplay?
                      .ChildrenOfType<GameplayHud>()
                      .SingleOrDefault();
                readout = gameplay?
                          .ChildrenOfType<JudgementReadout>()
                          .SingleOrDefault();
                timingBar = gameplay?
                            .ChildrenOfType<GameplayTimingBar>()
                            .SingleOrDefault();
                return playfield != null
                       && hud != null
                       && readout != null
                       && timingBar != null;
            });
            AddAssert("timing bar is fixed to the screen bottom", () =>
                timingBar.Anchor == Anchor.BottomCentre
                && timingBar.Origin == Anchor.BottomCentre
                && timingBar.Y == 28);
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
            AddStep("show early timing marker", () =>
                timingBar.Show(new JudgementEvent(
                    0,
                    0,
                    1000,
                    987.5,
                    -12.5,
                    JudgementRating.Perfect)));
            AddAssert("early timing is left of centre", () =>
                timingBar.RecordedMarkerCount == 1
                && timingBar.DisplayedDirectionKey
                == "gameplay.timing.early"
                && timingBar.LatestMarkerPosition
                < timingBar.CentreMarkerPosition
                && timingBar.LatestHitErrorMilliseconds == -12.5
                && timingBar.PressTrendMilliseconds == -12.5);
            AddStep("show late timing marker", () =>
                timingBar.Show(new JudgementEvent(
                    1,
                    1,
                    1200,
                    1225,
                    25,
                    JudgementRating.Great)));
            AddAssert("late timing is right of centre", () =>
                timingBar.RecordedMarkerCount == 2
                && timingBar.DisplayedDirectionKey
                == "gameplay.timing.late"
                && timingBar.LatestMarkerPosition
                > timingBar.CentreMarkerPosition
                && timingBar.LatestHitErrorMilliseconds == 25
                && timingBar.PressTrendMilliseconds == -6.875);
            AddStep("show manually pressed miss", () =>
                timingBar.Show(new JudgementEvent(
                    2,
                    2,
                    1400,
                    1550,
                    150,
                    JudgementRating.Miss)));
            AddAssert("manual miss uses the full miss axis", () =>
                timingBar.RecordedMarkerCount == 3
                && timingBar.LatestMarkerPosition
                > timingBar.CentreMarkerPosition
                && timingBar.LatestMarkerPosition
                < timingBar.MaximumMarkerPosition);
            AddStep("show late hold release", () =>
                timingBar.Show(new JudgementEvent(
                    3,
                    3,
                    1600,
                    1660,
                    60,
                    JudgementRating.Great,
                    JudgementPhase.HoldTail)));
            AddAssert("hold release has an independent trend", () =>
                timingBar.RecordedMarkerCount == 4
                && timingBar.LatestPhase == JudgementPhase.HoldTail
                && timingBar.ReleaseTrendMilliseconds == 60
                && timingBar.LatestMarkerPosition
                > timingBar.CentreMarkerPosition);
            AddStep("ignore automatic miss without input time", () =>
                timingBar.Show(new JudgementEvent(
                    4,
                    0,
                    1800,
                    null,
                    190,
                    JudgementRating.Miss)));
            AddAssert("automatic miss adds no timing marker", () =>
                timingBar.RecordedMarkerCount == 4);
            AddStep("fill timing marker history", () =>
            {
                for (int i = 0; i < 60; i++)
                {
                    double error = i % 2 == 0 ? -30 : 30;
                    timingBar.Show(new JudgementEvent(
                        5 + i,
                        i % 4,
                        2000 + i,
                        2000 + i + error,
                        error,
                        JudgementRating.Great));
                }
            });
            AddUntilStep("visible marker history stays capped", () =>
                timingBar.ActiveMarkerCount <= 50);
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
            AddUntilStep("legacy stage remains centred", () =>
            {
                GameplayPlayfield playfield =
                    gameplay?
                        .ChildrenOfType<GameplayPlayfield>()
                        .SingleOrDefault();
                return playfield != null
                       && playfield.Anchor == Anchor.BottomCentre
                       && playfield.Origin == Anchor.BottomCentre
                       && Math.Abs(playfield.X) < 0.01f
                       && Math.Abs(playfield.Scale.X - playfield.Scale.Y)
                       < 0.001f;
            });
            AddUntilStep("skin sprites loaded", () =>
                gameplay?.ChildrenOfType<Sprite>().Any(sprite => sprite.Texture != null) == true);
            AddAssert("skin owns judgement feedback", () =>
                gameplay.ChildrenOfType<GameplayPlayfield>()
                        .Single()
                        .UsesSkinJudgementOverlay);
        }

        [Test]
        public void TestLegacyColumnOffsetsCannotMoveOversizedStage()
        {
            string skinPath = createTestSkin("""
ColumnWidth: 100,100,100,100
ColumnStart: 500
ColumnRight: 50
""");
            GameplayScreen gameplay = null;

            AddStep("open oversized legacy stage", () =>
                screenStack.Push(gameplay = new GameplayScreen(
                    DemoBeatmaps.CreateFourKeyDemo(),
                    skinPath: skinPath)));
            AddUntilStep("oversized legacy stage is centred and fitted", () =>
            {
                GameplayPlayfield playfield = gameplay?
                                              .ChildrenOfType<GameplayPlayfield>()
                                              .SingleOrDefault();
                if (playfield == null
                    || gameplay.DrawWidth <= 0
                    || playfield.Scale.Y <= 0)
                    return false;

                float displayedWidth =
                    playfield.Width * playfield.Scale.X;
                return playfield.Anchor == Anchor.BottomCentre
                       && playfield.Origin == Anchor.BottomCentre
                       && Math.Abs(playfield.X) < 0.01f
                       && Math.Abs(playfield.Scale.X - playfield.Scale.Y)
                       < 0.001f
                       && displayedWidth
                       <= gameplay.DrawWidth * 0.94f + 0.01f;
            });
        }

        [Test]
        public void TestLegacySkinShowsKeyBindingReminder()
        {
            string skinPath = createTestSkin(
                "ColourKeyWarning: 10,20,30");
            GameplayScreen gameplay = null;

            AddStep("open key warning skin", () =>
                screenStack.Push(gameplay = new GameplayScreen(
                    DemoBeatmaps.CreateFourKeyDemo(),
                    skinPath: skinPath)));
            AddUntilStep("skinned receptors show binding reminder", () =>
            {
                SpriteText reminder = gameplay?
                                      .ChildrenOfType<LaneColumn>()
                                      .FirstOrDefault()?
                                      .SkinKeyWarning;
                return reminder != null
                       && !string.IsNullOrWhiteSpace(
                           reminder.Text.ToString())
                       && reminder.Colour
                          == new Color4(10, 20, 30, 255);
            });
        }

        [Test]
        public void TestMatchesLegacyCircleSkinGeometryAndStageFlags()
        {
            string skinPath = createTestSkin("""
JudgementLine: 0
LightingNWidth: 20,20,20,20
LightingLWidth: 20,20,20,20
""");
            GameplayScreen gameplay = null;

            using (var key = new Image<Rgba32>(
                       100,
                       160,
                       new Rgba32(255, 255, 255, 255)))
            {
                key.SaveAsPng(Path.Combine(skinPath, "key.png"));
                key.SaveAsPng(Path.Combine(skinPath, "key-down.png"));
                key.SaveAsPng(Path.Combine(
                    skinPath,
                    "mania-stage-right.png"));
            }

            AddStep("open circle skin", () =>
                screenStack.Push(gameplay = new GameplayScreen(
                    DemoBeatmaps.CreateFourKeyDemo(),
                    skinPath: skinPath)));
            AddUntilStep("legacy key converts 768-space height", () =>
                Math.Abs(
                    (gameplay?
                         .ChildrenOfType<LaneColumn>()
                         .FirstOrDefault()?
                         .IdleKeyHeight
                     ?? 0)
                    - 100) < 0.01f);
            AddAssert("hold lighting and stage foreground loaded", () =>
            {
                GameplayPlayfield playfield =
                    gameplay.ChildrenOfType<GameplayPlayfield>().Single();
                LaneColumn firstLane =
                    gameplay.ChildrenOfType<LaneColumn>().First();
                LegacyManiaAnimatedSprite firstNote =
                    playfield.GetDrawableNote(0)
                             .ChildrenOfType<LegacyManiaAnimatedSprite>()
                             .Single();
                return firstLane.HasHoldLight
                       && playfield.HasSkinStageBottom
                       && !playfield.ShowsSkinJudgementLine
                       && firstNote.RelativeSizeAxes == Axes.Both
                       && firstNote.Size == Vector2.One
                       && Math.Abs(
                           playfield.SkinStageHintHeight
                           - 8
                           / OsuManiaSkinConfiguration
                               .LegacyPositionScaleFactor
                           * 0.9f
                           * 1.6025f) < 0.01f
                       && gameplay.ChildrenOfType<Sprite>().Any(sprite =>
                           sprite.Texture?.DisplayWidth == 100
                           && sprite.Texture.DisplayHeight == 160
                           && Math.Abs(sprite.Width - 62.5f) < 0.01f
                           && Math.Abs(sprite.Height - 480) < 0.01f);
            });
        }

        [Test]
        public void TestLoadsAnimatedLegacyManiaPiecesAndBarLines()
        {
            string skinPath = createTestSkin();
            GameplayScreen gameplay = null;

            using (var first = new Image<Rgba32>(
                       12,
                       8,
                       new Rgba32(255, 80, 80, 255)))
            using (var second = new Image<Rgba32>(
                       12,
                       10,
                       new Rgba32(80, 255, 80, 255)))
            {
                first.SaveAsPng(Path.Combine(skinPath, "note-0.png"));
                second.SaveAsPng(Path.Combine(skinPath, "note-1.png"));
                first.SaveAsPng(Path.Combine(
                    skinPath,
                    "mania-stage-bottom-0.png"));
                second.SaveAsPng(Path.Combine(
                    skinPath,
                    "mania-stage-bottom-1.png"));
            }

            AddStep("open animated legacy skin", () =>
                screenStack.Push(gameplay = new GameplayScreen(
                    DemoBeatmaps.CreateFourKeyDemo(),
                    skinPath: skinPath)));
            AddUntilStep("animated notes and stage foreground loaded", () =>
            {
                GameplayPlayfield playfield = gameplay?
                    .ChildrenOfType<GameplayPlayfield>()
                    .SingleOrDefault();
                if (playfield == null)
                    return false;

                bool animatedNoteLoaded =
                    playfield.GetDrawableNote(0)
                             .ChildrenOfType<LegacyManiaAnimatedSprite>()
                             .Any(animation => animation.FrameCount == 2);
                bool animatedStageForegroundLoaded =
                    gameplay.ChildrenOfType<LegacyManiaAnimatedSprite>()
                            .Any(animation => animation.FrameCount == 2);
                return animatedNoteLoaded && animatedStageForegroundLoaded;
            });
            AddAssert("measure barlines are generated", () =>
                gameplay.ChildrenOfType<GameplayPlayfield>()
                        .Single()
                        .SkinBarLineCount > 0);
        }

        [Test]
        public void TestSplitLegacySkinRepeatsPerStageDecorations()
        {
            string skinPath = createTestSkin();
            GameplayScreen gameplay = null;
            YokkoBeatmap beatmap = createHoldDemo(KeyMode.EightKey);
            beatmap = beatmap with
            {
                HitObjects = beatmap.HitObjects
                                    .Select(hitObject => new YokkoHitObject(
                                        hitObject.Lane,
                                        hitObject.StartTimeMilliseconds + 6000,
                                        hitObject.EndTimeMilliseconds is double endTime
                                            ? endTime + 6000
                                            : null,
                                        hitObject.Kind,
                                        hitObject.SampleKey,
                                        hitObject.ScrollProfileId,
                                        hitObject.SamplePayload))
                                    .ToArray(),
            };
            File.WriteAllText(Path.Combine(skinPath, "skin.ini"), """
            [General]
            Name: Split Stage Fixture
            Version: 2.5

            [Mania]
            Keys: 8
            ColumnWidth: 40,40,40,40,40,40,40,40
            SplitStages: 1
            StageSeparation: 24
            StageHint: stage-hint
            WarningArrow: stage-hint
            """);

            AddStep("open forced split-stage skin", () =>
                screenStack.Push(gameplay = new GameplayScreen(
                    beatmap,
                    skinPath: skinPath)));
            AddUntilStep("split-stage playfield loaded", () =>
                gameplay?
                    .ChildrenOfType<GameplayPlayfield>()
                    .SingleOrDefault() is { } playfield
                && playfield.SkinStageBottomCount == 2
                && playfield.SkinStageHintCount == 2
                && playfield.SkinJudgementLineCount == 2
                && playfield.SkinWarningArrowCount == 2
                && Math.Abs(
                    playfield.SkinWarningArrowStartTime - 2000)
                < 0.001);
        }

        [Test]
        public void TestLegacyWarningArrowRequiresThreePriorMeasures()
        {
            string skinPath = createTestSkin();
            GameplayScreen gameplay = null;
            File.WriteAllText(Path.Combine(skinPath, "skin.ini"), """
            [General]
            Name: Warning Arrow Fixture
            Version: 2.5

            [Mania]
            Keys: 4
            WarningArrow: stage-hint
            """);

            AddStep("open short lead-in skin", () =>
                screenStack.Push(gameplay = new GameplayScreen(
                    DemoBeatmaps.CreateFourKeyDemo(),
                    skinPath: skinPath)));
            AddUntilStep("warning arrow omitted without three measures", () =>
                gameplay?
                    .ChildrenOfType<GameplayPlayfield>()
                    .SingleOrDefault() is { } playfield
                && playfield.SkinWarningArrowCount == 0);
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
            bool originalKeysoundsEnabled = false;

            AddStep("enable skin hit sounds", () =>
            {
                originalKeysoundsEnabled =
                    gameplaySettings.KeysoundsEnabled.Value;
                gameplaySettings.KeysoundsEnabled.Value = true;
            });
            AddStep("open gameplay with skin hit sounds", () =>
                screenStack.Push(new GameplayScreen(
                    DemoBeatmaps.CreateFourKeyDemo(),
                    audioEngine,
                    skinPath)));
            AddUntilStep("skin hit sound is prepared", () =>
                audioEngine.PreparedSamples.Contains(hitSoundPath));
            AddStep("restore skin hit sound setting", () =>
                gameplaySettings.KeysoundsEnabled.Value =
                    originalKeysoundsEnabled);
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
                       && body.DisplayWidth == 8
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
        public void TestLegacyRepeatBottomPreservesBodyStart()
        {
            string skinPath = null;

            AddStep("create bottom-repeat hold body", () =>
            {
                skinPath = createTestSkin("NoteBodyStyle0: 3");

                using var image = new Image<Rgba32>(
                    8,
                    12,
                    new Rgba32(142, 136, 145, 255));
                image.SaveAsPng(Path.Combine(skinPath, "hold-body.png"));
            });
            AddStep("open bottom-repeat hold", () =>
                screenStack.Push(new GameplayScreen(
                    createHoldDemo(KeyMode.FourKey),
                    skinPath: skinPath)));
            AddUntilStep("body starts at the texture start", () =>
            {
                DrawableNote hold = (screenStack.CurrentScreen as Drawable)?
                                    .ChildrenOfType<GameplayPlayfield>()
                                    .SingleOrDefault()?
                                    .GetDrawableNote(0);
                Sprite body = hold?
                              .ChildrenOfType<Sprite>()
                              .FirstOrDefault(sprite =>
                                  sprite.Texture?.DisplayHeight == 12);

                if (hold == null || body?.Parent is not Container clip)
                    return false;

                hold.UpdatePosition(
                    1000,
                    false,
                    false,
                    0,
                    460,
                    1800);

                return body.TextureRelativeSizeAxes == Axes.None
                       && Math.Abs(body.TextureRectangle.Y) < 0.01f
                       && Math.Abs(
                           body.TextureRectangle.Height
                           - clip.Height) < 0.01f
                       && body.Texture.WrapModeT == WrapMode.ClampToEdge;
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
        public void TestRendersConfiguredRealSkinCorpusLongNotes()
        {
            string root = Environment.GetEnvironmentVariable(
                "YOKKO_OSU_MANIA_SKIN_CORPUS");

            if (string.IsNullOrWhiteSpace(root)
                || !Directory.Exists(root))
            {
                Assert.Ignore(
                    "Set YOKKO_OSU_MANIA_SKIN_CORPUS to a directory containing real osu! skins.");
            }

            string[] packages = Directory
                                .EnumerateFiles(root, "*", SearchOption.TopDirectoryOnly)
                                .Where(path => Path.GetExtension(path).Equals(
                                    ".osk",
                                    StringComparison.OrdinalIgnoreCase)
                                    || Path.GetExtension(path).Equals(
                                        ".zip",
                                        StringComparison.OrdinalIgnoreCase))
                                .Concat(Directory.EnumerateDirectories(root))
                                .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                                .ToArray();
            int renderedConfigurations = 0;

            foreach (string package in packages)
            {
                OsuManiaSkinInfo info;

                using (var source = new OsuManiaSkinSource(package))
                {
                    string skinIni = source.ReadSkinIni();
                    info = OsuManiaSkinIniDecoder.Decode(
                        skinIni,
                        source.Contains("skin.ini"),
                        source.UsesLatestVersion);
                }

                foreach (int keys in info.ManiaConfigurations.Keys.Order())
                {
                    if (!Enum.IsDefined(typeof(KeyMode), keys))
                        continue;

                    var keyMode = (KeyMode)keys;
                    string caseName =
                        $"{Path.GetFileName(package)} {keys}K";
                    GameplayScreen gameplay = null;

                    AddStep($"open {caseName}", () =>
                        screenStack.Push(gameplay = new GameplayScreen(
                            createAllHoldDemo(keyMode),
                            skinPath: package)));
                    AddUntilStep($"render {caseName} LN geometry", () =>
                    {
                        GameplayPlayfield playfield = gameplay?
                            .ChildrenOfType<GameplayPlayfield>()
                            .SingleOrDefault();
                        if (playfield?.KeyCount != keys
                            || playfield.ActiveDrawableNoteCount < keys)
                        {
                            return false;
                        }

                        if (playfield.Anchor != Anchor.BottomCentre
                            || playfield.Origin != Anchor.BottomCentre
                            || Math.Abs(playfield.X) >= 0.01f
                            || Math.Abs(
                                playfield.Scale.X
                                - playfield.Scale.Y) >= 0.001f)
                        {
                            return false;
                        }

                        for (int lane = 0; lane < keys; lane++)
                        {
                            DrawableNote note = playfield.GetDrawableNote(lane);
                            if (note == null)
                                return false;

                            note.UpdatePosition(
                                1000,
                                false,
                                false,
                                0,
                                460,
                                1800);

                            Container bodyClip = note
                                .ChildrenOfType<Container>()
                                .FirstOrDefault(container =>
                                    container.Masking);
                            Box fallbackBody = note
                                .ChildrenOfType<Box>()
                                .FirstOrDefault(box => box.Height > 0);
                            bool hasVisual = note
                                                 .ChildrenOfType<Sprite>()
                                                 .Any(sprite =>
                                                     sprite.Texture?.Available
                                                     == true)
                                             || fallbackBody != null;
                            float bodyHeight =
                                bodyClip?.Height
                                ?? fallbackBody?.Height
                                ?? 0;
                            if (!hasVisual
                                || bodyHeight <= 0
                                || !float.IsFinite(bodyHeight)
                                || note.Width <= 0
                                || note.Height <= 0
                                || !float.IsFinite(note.Width)
                                || !float.IsFinite(note.Height)
                                || !float.IsFinite(note.Y))
                            {
                                return false;
                            }
                        }

                        return true;
                    });
                    AddStep($"close {caseName}", () => gameplay.Exit());
                    AddUntilStep($"closed {caseName}", () =>
                        !ReferenceEquals(screenStack.CurrentScreen, gameplay));
                    renderedConfigurations++;
                }
            }

            AddAssert(
                "rendered every configured key mode",
                () => renderedConfigurations > 0);
        }

        [Test]
        public void TestOsuScorebarReplacesDefaultHealthBar()
        {
            string skinPath = createTestSkin();
            using (var background = new Image<Rgba32>(
                       200,
                       40,
                       new Rgba32(25, 30, 45, 255)))
            using (var fill = new Image<Rgba32>(
                       160,
                       20,
                       new Rgba32(72, 208, 240, 255)))
            using (var alternateFill = new Image<Rgba32>(
                       160,
                       20,
                       new Rgba32(240, 120, 180, 255)))
            using (var marker = new Image<Rgba32>(
                       1,
                       1,
                       new Rgba32(255, 255, 255, 255)))
            {
                background.SaveAsPng(
                    Path.Combine(skinPath, "scorebar-bg.png"));
                fill.SaveAsPng(
                    Path.Combine(skinPath, "scorebar-colour-0.png"));
                alternateFill.SaveAsPng(
                    Path.Combine(skinPath, "scorebar-colour-1.png"));
                marker.SaveAsPng(
                    Path.Combine(skinPath, "scorebar-marker.png"));
            }

            GameplayScreen gameplay = null;
            LegacyManiaHealthBar scorebar = null;

            AddStep("open scorebar skin", () =>
            {
                gameplay = new GameplayScreen(
                    DemoBeatmaps.CreateFourKeyDemo(),
                    skinPath: skinPath);
                screenStack.Push(gameplay);
            });
            AddUntilStep("legacy scorebar loaded", () =>
            {
                scorebar = gameplay?
                           .ChildrenOfType<LegacyManiaHealthBar>()
                           .SingleOrDefault();
                return scorebar?.IsAvailable == true;
            });
            AddAssert("mania scorebar geometry matches stable", () =>
            {
                GameplayPlayfield playfield = gameplay
                                              .ChildrenOfType<GameplayPlayfield>()
                                              .Single();
                return scorebar.Rotation == -90
                       && Math.Abs(scorebar.X - playfield.Width) < 0.01
                       && scorebar.Y == 480
                       && Math.Abs(scorebar.Scale.X - 0.7f) < 0.001
                       && scorebar.AnimationFrameCount == 2
                       && scorebar.UsesMarkerStyle
                       && Vector2.Distance(
                           scorebar.FillPosition,
                           new Vector2(12, 12)
                           / OsuManiaSkinConfiguration
                               .LegacyPositionScaleFactor) < 0.001f;
            });
            AddAssert("default health bar is replaced", () =>
                gameplay.ChildrenOfType<GameplayHud>()
                        .Single()
                        .UsesLegacySkinHealthBar);
            double expectedHealth = 1;
            AddStep("change actual mania health", () =>
            {
                gameplay.HealthState.Apply(new JudgementEvent(
                    0,
                    0,
                    1000,
                    1000,
                    0,
                    JudgementRating.Miss));
                expectedHealth = gameplay.HealthState.Health;
            });
            AddUntilStep("skin scorebar follows actual health", () =>
                Math.Abs(
                    scorebar.TargetFillFraction - expectedHealth) < 0.001);
            AddAssert("skin fill targets actual health", () =>
            {
                scorebar.SetHealth(0.25);
                return Math.Abs(
                           scorebar.TargetFillFraction - 0.25f) < 0.001f
                       && Math.Abs(scorebar.FillColour.R - 0.5f) < 0.001f
                       && Math.Abs(scorebar.FillColour.G - 0.5f) < 0.001f
                       && Math.Abs(scorebar.FillColour.B - 0.5f) < 0.001f;
            });
        }

        [Test]
        public void TestStandardOsuSkinWithoutManiaSectionStillAppliesInterface()
        {
            string skinPath = Path.Combine(
                TestContext.CurrentContext.WorkDirectory,
                "osu-standard-skin-visual",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(skinPath);
            File.WriteAllText(Path.Combine(skinPath, "skin.ini"), """
            [General]
            Name: Standard osu skin
            Version: latest
            """);
            using (var background = new Image<Rgba32>(
                       200,
                       40,
                       new Rgba32(25, 30, 45, 255)))
            using (var fill = new Image<Rgba32>(
                       160,
                       20,
                       new Rgba32(72, 208, 240, 255)))
            {
                background.SaveAsPng(
                    Path.Combine(skinPath, "scorebar-bg.png"));
                fill.SaveAsPng(
                    Path.Combine(skinPath, "scorebar-colour.png"));
            }

            GameplayScreen gameplay = null;

            AddStep("open standard skin in mania", () =>
                screenStack.Push(gameplay = new GameplayScreen(
                    DemoBeatmaps.CreateFourKeyDemo(),
                    skinPath: skinPath)));
            AddUntilStep("default mania stage and skinned scorebar coexist", () =>
            {
                GameplayPlayfield playfield = gameplay?
                                              .ChildrenOfType<GameplayPlayfield>()
                                              .SingleOrDefault();
                LegacyManiaHealthBar scorebar = gameplay?
                                                .ChildrenOfType<LegacyManiaHealthBar>()
                                                .SingleOrDefault();
                return playfield?.KeyCount == 4
                       && playfield.HasSkinHealthBar
                       && playfield.GetDrawableNote(0)
                                   .ChildrenOfType<Box>()
                                   .Any()
                       && scorebar is
                       {
                           IsAvailable: true,
                           UsesMarkerStyle: false,
                       }
                       && Vector2.Distance(
                           scorebar.FillPosition,
                           new Vector2(5, 16)
                           / OsuManiaSkinConfiguration
                               .LegacyPositionScaleFactor) < 0.001f
                       && gameplay.ChildrenOfType<GameplayHud>()
                                  .Single()
                                  .UsesLegacySkinHealthBar;
            });
        }

        [Test]
        public void TestLegacyManiaComboBurstAtHundredCombo()
        {
            string skinPath = createTestSkin(
                "ComboBurstStyle: Left");
            using (var image = new Image<Rgba32>(
                       120,
                       240,
                       new Rgba32(240, 120, 180, 255)))
            {
                image.SaveAsPng(
                    Path.Combine(skinPath, "comboburst-mania.png"));
            }

            YokkoBeatmap beatmap = new(
                "Combo burst fixture",
                "Yokko",
                "Codex",
                "100 combo",
                KeyMode.FourKey,
                ChartSourceFormat.Yokko,
                [YokkoTimingPoint.Default],
                null,
                Enumerable.Range(0, 100)
                          .Select(index => new YokkoHitObject(
                              index % 4,
                              1000 + index * 20,
                              null,
                              HitObjectKind.Tap))
                          .ToArray());
            var state = new BeatmapJudgementState(beatmap);
            OsuManiaSkin skin = null;
            GameplayPlayfield playfield = null;

            AddStep("load combo burst skin", () =>
            {
                skin = OsuManiaSkin.Load(
                    skinPath,
                    4,
                    renderer);
                Add(playfield = new GameplayPlayfield(
                    beatmap,
                    KeyModeBindings.ForMode(KeyMode.FourKey),
                    skin));
            });
            AddUntilStep("combo burst playfield loaded", () =>
                playfield?.IsLoaded == true);
            AddStep("reach 100 combo", () =>
            {
                for (int index = 0; index < beatmap.HitObjects.Count; index++)
                {
                    YokkoHitObject note = beatmap.HitObjects[index];
                    if (state.TryJudgeLanePress(
                            note.Lane,
                            note.StartTimeMilliseconds) == null)
                    {
                        throw new InvalidOperationException(
                            $"Could not judge combo fixture note {index}.");
                    }
                }

                playfield.UpdateGameplayTime(3000, state);
            });
            AddAssert("left combo burst is emitted once", () =>
                state.Combo == 100
                && playfield.ComboBurstCount == 1
                && playfield.LastComboBurstRightSide == false);
            AddStep("remove combo burst fixture", () =>
            {
                playfield.Expire();
                skin.Dispose();
            });
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
                Sprite legacyBody = notes?
                                    .FirstOrDefault()?
                                    .ChildrenOfType<Sprite>()
                                    .FirstOrDefault(sprite =>
                                        sprite.Texture?.WrapModeT
                                        == WrapMode.Repeat);
                bool bodyUsesLegacyGeometry =
                    legacyBody?.Parent is Container bodyClip
                    && (legacyBody.TextureRelativeSizeAxes == Axes.None
                        || legacyBody.Height > bodyClip.Height);
                return notes?.Length == 4 &&
                       notes.All(note => note.ChildrenOfType<Sprite>().Any(sprite => sprite.Texture != null)) &&
                       notes.SelectMany(note => note.ChildrenOfType<Sprite>())
                            .Select(sprite => sprite.Texture)
                             .Where(texture => texture != null)
                             .Distinct()
                             .Count() >= 4 &&
                       bodyUsesLegacyGeometry;
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
            AddAssert("timing bar stays at the screen bottom", () =>
            {
                Drawable current = screenStack.CurrentScreen as Drawable;
                GameplayTimingBar timingBar = current?
                                              .ChildrenOfType<GameplayTimingBar>()
                                              .SingleOrDefault();
                return timingBar?.Anchor == Anchor.BottomCentre
                       && timingBar.Origin == Anchor.BottomCentre
                       && timingBar.Y == 28;
            });
        }

        [Test]
        public void TestTimingBarStaysAtBottomWithTallSkinReceptors()
        {
            string skinPath = createTestSkin();
            using (var keyImage = new Image<Rgba32>(
                       64,
                       240,
                       new Rgba32(72, 208, 240, 255)))
            {
                keyImage.SaveAsPng(Path.Combine(skinPath, "key.png"));
                keyImage.SaveAsPng(Path.Combine(skinPath, "key-down.png"));
            }

            GameplayScreen gameplay = null;
            GameplayTimingBar timingBar = null;

            AddStep("open tall receptor skin", () =>
            {
                gameplay = new GameplayScreen(
                    DemoBeatmaps.CreateFourKeyDemo(),
                    skinPath: skinPath);
                screenStack.Push(gameplay);
            });
            AddUntilStep("tall receptor feedback loaded", () =>
            {
                timingBar = gameplay?
                            .ChildrenOfType<GameplayTimingBar>()
                            .SingleOrDefault();
                return timingBar != null;
            });
            AddAssert("tall receptors do not move the timing bar", () =>
                timingBar.Anchor == Anchor.BottomCentre
                && timingBar.Origin == Anchor.BottomCentre
                && timingBar.Y == 28);
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
            GameplayScrollSpeedOverlay speedOverlay = null;

            AddStep("save and open fresh gameplay", () =>
            {
                originalSpeed = gameplaySettings.ScrollSpeed.Value;
                originalDecreaseKey =
                    gameplaySettings.DecreaseScrollSpeedKey.Value;
                originalIncreaseKey =
                    gameplaySettings.IncreaseScrollSpeedKey.Value;
                gameplaySettings.ResetShortcutBindings();
                gameplaySettings.SetScrollSpeed(8);
                gameplayScreen = new GameplayScreen(
                    DemoBeatmaps.CreateFourKeyDemo());
                screenStack.Push(gameplayScreen);
            });
            AddUntilStep("speed overlay loaded", () =>
            {
                speedOverlay = gameplayScreen?
                    .ChildrenOfType<GameplayScrollSpeedOverlay>()
                    .SingleOrDefault();
                return speedOverlay != null;
            });
            AddAssert("speed overlay uses compact upper-left ticket", () =>
                speedOverlay.Size
                    == GameplayScrollSpeedOverlay.ReferenceSize
                && speedOverlay.Anchor == Anchor.TopLeft
                && speedOverlay.Origin == Anchor.TopLeft
                && speedOverlay.Y
                   == GameplayScrollSpeedOverlay.TopOffset
                && speedOverlay.X
                   <= GameplayScrollSpeedOverlay.PreferredLeft);
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
            AddAssert("speed overlay shows value and time range", () =>
                speedOverlay.DisplayedSpeed == 9
                && speedOverlay.DisplayedTimeRangeMilliseconds
                   == (int)OsuManiaScrollSpeed.ComputeScrollTime(9)
                && !speedOverlay.IsLocked
                && speedOverlay.DisplayedLabel == "SCROLL SPEED"
                && speedOverlay.DisplayedDetail
                   == $"{(int)OsuManiaScrollSpeed.ComputeScrollTime(9)} ms"
                && speedOverlay.Alpha > 0);
            AddWaitStep("let speed ticket settle", 8);
            AddStep("capture speed ticket when requested", () =>
            {
                string outputPath = Environment.GetEnvironmentVariable(
                    "YOKKO_SCROLL_SPEED_SCREENSHOT");
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
            AddStep("attempt late gameplay adjustment", () =>
                gameplayScreen.HandleScrollSpeedShortcut(
                    Key.F8,
                    false,
                    11000));
            AddAssert("late adjustment is locked", () =>
                gameplaySettings.ScrollSpeed.Value == 8
                && speedOverlay.IsLocked
                && speedOverlay.DisplayedSpeed == 8
                && speedOverlay.DisplayedLabel == "SPEED LOCKED"
                && speedOverlay.DisplayedDetail == "INTRO / BREAK");
            AddUntilStep("speed overlay exits smoothly", () =>
                speedOverlay.Alpha <= 0.01f);
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
        public void TestAltPlusMinusAdjustsPlaybackRateAndLiveStats()
        {
            var audioEngine = new RateTrackingAudioEngine();
            YokkoBeatmap beatmap =
                DemoBeatmaps.CreateFourKeyDemo() with
                {
                    AudioPath = "rate-adjustment-fixture.mp3",
                };
            GameplayScreen gameplayScreen = null;
            GameplayPlaybackRateOverlay rateOverlay = null;
            GameplayHud hud = null;
            ManiaStarRatingResult expectedDifficulty =
                ManiaStarRatingCalculator.CalculateResult(
                    beatmap,
                    1.05);

            AddStep("open rate-adjustable gameplay", () =>
            {
                gameplayScreen = new GameplayScreen(
                    beatmap,
                    audioEngine);
                screenStack.Push(gameplayScreen);
            });
            AddUntilStep("rate overlay and hud loaded", () =>
            {
                rateOverlay = gameplayScreen?
                    .ChildrenOfType<GameplayPlaybackRateOverlay>()
                    .SingleOrDefault();
                hud = gameplayScreen?
                    .ChildrenOfType<GameplayHud>()
                    .SingleOrDefault();
                return rateOverlay != null && hud != null;
            });
            AddAssert("plain plus is not a rate shortcut", () =>
                !gameplayScreen.HandlePlaybackRateShortcut(
                    Key.Plus,
                    false)
                && gameplayScreen.CurrentPlaybackRate == 1);
            AddUntilStep(
                "audio starts with dynamic rate enabled",
                () => audioEngine.LastStartRequest?
                          .DynamicPlaybackRate == true);
            AddStep("alt plus raises playback rate", () =>
                gameplayScreen.HandlePlaybackRateShortcut(
                    Key.Plus,
                    true));
            AddUntilStep("audio receives 1.05x", () =>
                Math.Abs(audioEngine.PlaybackRate - 1.05) < 0.000001);
            AddUntilStep("overlay shows rate bpm and difficulty", () =>
                Math.Abs(rateOverlay.DisplayedRate - 1.05) < 0.000001
                && Math.Abs(rateOverlay.DisplayedBpm - 126) < 0.000001
                && Math.Abs(
                    rateOverlay.DisplayedDifficulty.GetValueOrDefault()
                    - expectedDifficulty.Value.GetValueOrDefault())
                   < 0.000001
                && rateOverlay.DisplayedDetail.Contains("126 BPM")
                && rateOverlay.DisplayedDetail.Contains("STAR")
                && rateOverlay.Alpha > 0);
            AddUntilStep("hud keeps live rate stats visible", () =>
                hud.DisplayedDynamicRate.Contains("LIVE RATE 1.05×")
                && hud.DisplayedDynamicRate.Contains("126 BPM")
                && hud.DisplayedDynamicRate.Contains("STAR")
                && hud.DisplayedDynamicRate.Contains("PRACTICE")
                && gameplayScreen.ManualPlaybackRateUsed);
            AddStep("alt keypad minus restores normal rate", () =>
                gameplayScreen.HandlePlaybackRateShortcut(
                    Key.KeypadMinus,
                    true));
            AddUntilStep("audio returns to 1x", () =>
                Math.Abs(audioEngine.PlaybackRate - 1) < 0.000001);
            AddAssert("restored rate is shown immediately", () =>
                Math.Abs(rateOverlay.DisplayedRate - 1) < 0.000001
                && Math.Abs(rateOverlay.DisplayedBpm - 120) < 0.000001);
            AddAssert("restored rate keeps practice status", () =>
                gameplayScreen.ManualPlaybackRateUsed
                && hud.DisplayedDynamicRate.Contains(
                    "LIVE RATE 1.00×")
                && hud.DisplayedDynamicRate.Contains("PRACTICE"));
        }

        [Test]
        public void TestRetryKeepsManualPlaybackRate()
        {
            GameplayScreen original = null;
            GameplayScreen replacement = null;

            AddStep("open gameplay for rate-preserving retry", () =>
            {
                original = new GameplayScreen(
                    DemoBeatmaps.CreateFourKeyDemo());
                screenStack.Push(original);
            });
            AddUntilStep(
                "gameplay loaded",
                () => original?.IsLoaded == true);
            AddStep("set 1.05x before retry", () =>
                original.HandlePlaybackRateShortcut(
                    Key.Plus,
                    true));
            AddAssert("original uses 1.05x", () =>
                Math.Abs(original.CurrentPlaybackRate - 1.05) < 0.000001);
            AddStep("retry gameplay", () =>
                original.RetryGameplay());
            AddUntilStep("replacement gameplay is active", () =>
            {
                replacement =
                    screenStack.CurrentScreen as GameplayScreen;
                return replacement != null
                       && !ReferenceEquals(replacement, original)
                       && replacement.IsLoaded;
            });
            AddAssert("replacement keeps 1.05x", () =>
                Math.Abs(replacement.CurrentPlaybackRate - 1.05)
                < 0.000001
                && replacement.ManualPlaybackRateUsed);
        }

        [Test]
        public void TestManualPlaybackRateCompletesAsPractice()
        {
            YokkoBeatmap beatmap =
                DemoBeatmaps.CreateFourKeyDemo() with
                {
                    Title =
                        $"Rate Practice {TestContext.CurrentContext.Test.ID}",
                    HitObjects =
                    [
                        new YokkoHitObject(
                            0,
                            250,
                            null,
                            HitObjectKind.Tap),
                    ],
                };
            GameplayScreen gameplay = null;

            AddStep("open short rate practice", () =>
            {
                gameplay = new GameplayScreen(beatmap);
                screenStack.Push(gameplay);
            });
            AddUntilStep(
                "rate practice loaded",
                () => gameplay?.IsLoaded == true);
            AddStep("adjust rate before completion", () =>
                gameplay.HandlePlaybackRateShortcut(
                    Key.Plus,
                    true));
            AddUntilStep(
                "rate practice completes",
                () => gameplay?.GameplayCompleted == true);
            AddAssert("practice does not replace best score", () =>
                !gameplay.BestScoreSaved);
            AddAssert("result identifies practice session", () =>
            {
                GameplayResultOverlay result = gameplay
                    .ChildrenOfType<GameplayResultOverlay>()
                    .SingleOrDefault();
                return result?.PracticeSession == true
                       && result.DisplayedMods.Contains("PRACTICE");
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
        public void TestRetryWaitsForAudioReleaseAndReplacesGameplay()
        {
            var audioEngine = new SeekTrackingAudioEngine
            {
                StopCompletion = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously),
            };
            GameplaySessionScreen session = null;
            GameplayScreen gameplay = null;
            GameplayScreen replacement = null;

            AddStep("open gameplay session", () =>
                screenStack.Push(session = new GameplaySessionScreen(
                    gameplay = new GameplayScreen(
                        DemoBeatmaps.CreateFourKeyDemo(),
                        audioEngine))));
            AddUntilStep("gameplay is current", () =>
                ReferenceEquals(screenStack.CurrentScreen, session)
                && ReferenceEquals(session?.CurrentGameplay, gameplay)
                && gameplay?.IsLoaded == true);
            AddStep("request retry twice", () =>
            {
                gameplay.RetryGameplay();
                gameplay.RetryGameplay();
            });
            AddAssert("retry waits and coalesces", () =>
                audioEngine.StopCount == 1
                && ReferenceEquals(screenStack.CurrentScreen, session)
                && ReferenceEquals(session.CurrentGameplay, gameplay));
            AddStep("release old audio session", () =>
                audioEngine.StopCompletion.SetResult(true));
            AddUntilStep("retry animation starts", () =>
                session.RetryTransitionActive);
            AddUntilStep("replacement gameplay is current", () =>
            {
                replacement = session?.CurrentGameplay;
                return replacement != null
                       && replacement.IsLoaded
                       && !ReferenceEquals(replacement, gameplay);
            });
            AddAssert("replacement stays inside gameplay session", () =>
                ReferenceEquals(screenStack.CurrentScreen, session)
                && replacement.GetParentScreen()
                   is GameplaySessionRootScreen
                && !gameplay.ValidForResume);
            AddUntilStep("retry reveal animation completes", () =>
                !session.RetryTransitionActive);
            AddStep("exit replacement gameplay", () =>
                replacement.Exit());
            AddUntilStep("gameplay session exits with its gameplay", () =>
                !ReferenceEquals(screenStack.CurrentScreen, session));
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
                       && overlay.SelectedAction == 0
                       && overlay.DisplayedScore == 0
                       && Math.Abs(overlay.DisplayedAccuracy - 1) < 0.0001
                       && overlay.DisplayedCombo == 0
                       && overlay.DisplayedMaxCombo == 0
                       && !string.IsNullOrWhiteSpace(
                           overlay.DisplayedRank)
                       && GameplayPauseOverlay.ReferenceSize
                          == YokkoDisplaySettings.ReferenceLayoutSize;
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
                          "lightingL.png",
                          "mania-stage-bottom.png",
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

        private static YokkoBeatmap createAllHoldDemo(KeyMode keyMode)
        {
            int keys = (int)keyMode;
            YokkoHitObject[] hitObjects = Enumerable
                                          .Range(0, keys)
                                          .Select(lane =>
                                              new YokkoHitObject(
                                                  lane,
                                                  1600,
                                                  2800,
                                                  HitObjectKind.Hold))
                                          .ToArray();

            return new YokkoBeatmap(
                "Skin LN Corpus",
                "Yokko",
                "Codex",
                $"{keys}K Long Notes",
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

            public int StopCount { get; private set; }

            public TaskCompletionSource<bool> StopCompletion { get; init; }

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
                StopCount++;
                status = createStatus(false);
                return StopCompletion == null
                    ? ValueTask.CompletedTask
                    : new ValueTask(StopCompletion.Task);
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

        private sealed class RateTrackingAudioEngine
            : SeekTrackingAudioEngine, IAudioRateControl
        {
            public double PlaybackRate { get; private set; } = 1;

            public void SetPlaybackRate(double playbackRate) =>
                PlaybackRate = playbackRate;
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
