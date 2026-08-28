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
using Yokko.Game.Audio;
using Yokko.Game.Gameplay;
using Yokko.Game.Presentation;
using Yokko.Game.Screens.Gameplay;
using Yokko.Game.Screens.Settings;
using Yokko.Game.Skinning.OsuMania;
using Yokko.Import.Osu;

namespace Yokko.Game.Tests.Visual
{
    [TestFixture]
    public partial class TestSceneGameplayScreen : YokkoTestScene
    {
        private readonly ScreenStack screenStack;

        [Resolved]
        private YokkoGameplaySettings gameplaySettings { get; set; }

        [Resolved]
        private YokkoAudioSettings audioSettings { get; set; }

        [Resolved]
        private YokkoDisplaySettings displaySettings { get; set; }

        [Resolved]
        private YokkoSkinSettings skinSettings { get; set; }

        [Resolved]
        private OsuManiaSkinLibrary skinLibrary { get; set; }

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
        public void TestBmsScratchLaneHasDistinctGameplayPresentation()
        {
            GameplayScreen gameplay = null;
            AddStep("open BMS scratch chart", () =>
            {
                var beatmap = new YokkoBeatmap(
                    "Scratch",
                    "Yokko",
                    "Yokko",
                    "7K + SCR",
                    KeyMode.EightKey,
                    ChartSourceFormat.Bms,
                    [YokkoTimingPoint.Default],
                    null,
                    [
                        new YokkoHitObject(
                            0,
                            1000,
                            null,
                            HitObjectKind.Tap),
                        new YokkoHitObject(
                            1,
                            1100,
                            null,
                            HitObjectKind.Tap),
                    ],
                    ScratchLane: 0);
                gameplay = new GameplayScreen(beatmap);
                screenStack.Push(gameplay);
            });
            AddUntilStep("scratch playfield loaded", () =>
                gameplay?.ChildrenOfType<GameplayPlayfield>()
                        .SingleOrDefault() is { } playfield
                && playfield.KeyCount == 8
                && playfield.GetLaneColumn(0).IsScratchLane
                && !playfield.GetLaneColumn(1).IsScratchLane
                && playfield.GetDrawableNote(0).IsScratchNote
                && !playfield.GetDrawableNote(1).IsScratchNote);
        }

        [Test]
        public void TestBmsDoublePlayHasTwoScratchLanes()
        {
            GameplayScreen gameplay = null;
            AddStep("open BMS DP scratch chart", () =>
            {
                var beatmap = new YokkoBeatmap(
                    "Double Scratch",
                    "Yokko",
                    "Yokko",
                    "5K + SCR DP",
                    KeyMode.TwelveKey,
                    ChartSourceFormat.Bms,
                    [YokkoTimingPoint.Default],
                    null,
                    [
                        new YokkoHitObject(
                            0,
                            1000,
                            null,
                            HitObjectKind.Tap),
                        new YokkoHitObject(
                            6,
                            1100,
                            null,
                            HitObjectKind.Tap),
                    ],
                    StageCount: 2);
                gameplay = new GameplayScreen(beatmap);
                screenStack.Push(gameplay);
            });
            AddUntilStep("both DP scratch lanes loaded", () =>
                gameplay?.ChildrenOfType<GameplayPlayfield>()
                        .SingleOrDefault() is { } playfield
                && playfield.KeyCount == 12
                && playfield.GetLaneColumn(0).IsScratchLane
                && playfield.GetLaneColumn(6).IsScratchLane
                && !playfield.GetLaneColumn(1).IsScratchLane
                && !playfield.GetLaneColumn(7).IsScratchLane
                && playfield.GetDrawableNote(0).IsScratchNote
                && playfield.GetDrawableNote(1).IsScratchNote);
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
                && gameplay.ChildrenOfType<GameplayTimingBar>().Single().Alpha == 0
                && gameplay
                   .ChildrenOfType<GameplayReplayControlsOverlay>()
                   .SingleOrDefault() == null);
        }

        [Test]
        public void TestGameplayUsesArtworkBackground()
        {
            GameplayScreen gameplay = null;
            string artworkPath = Path.Combine(
                TestContext.CurrentContext.WorkDirectory,
                $"{TestContext.CurrentContext.Test.ID}-background.png");

            AddStep("open gameplay with artwork", () =>
            {
                using (var image = new Image<Rgba32>(32, 18, new Rgba32(36, 92, 150)))
                    image.SaveAsPng(artworkPath);

                gameplay = new GameplayScreen(
                    DemoBeatmaps.CreateFourKeyDemo(),
                    artworkPath: artworkPath);
                screenStack.Push(gameplay);
            });
            AddUntilStep(
                "artwork background loads",
                () => gameplay?.HasArtworkBackground == true);
            AddStep("close gameplay", () => gameplay.Exit());
            AddUntilStep(
                "gameplay closes",
                () => !ReferenceEquals(screenStack.CurrentScreen, gameplay));
            AddStep("remove artwork fixture", () => File.Delete(artworkPath));
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
        public void TestJudgementPreferenceChangesApplyNextPlay()
        {
            GameplayScreen gameplay = null;
            JudgementMode originalMode = JudgementMode.Yokko;
            double originalJustice =
                JudgementConfiguration.DefaultEtternaJustice;

            AddStep("start play with Yokko judgement", () =>
            {
                originalMode = gameplaySettings.JudgementMode.Value;
                originalJustice = gameplaySettings.EtternaJustice.Value;
                gameplaySettings.JudgementMode.Value = JudgementMode.Yokko;
                gameplaySettings.SetEtternaJustice(
                    JudgementConfiguration.DefaultEtternaJustice);
                screenStack.Push(gameplay = new GameplayScreen(
                    DemoBeatmaps.CreateFourKeyDemo()));
            });
            AddUntilStep("starting judgement is loaded", () =>
                gameplay?.ActiveJudgementWindows != null);
            AddStep("change saved judgement during play", () =>
            {
                gameplaySettings.JudgementMode.Value = JudgementMode.Etterna;
                gameplaySettings.SetEtternaJustice(8);
            });
            AddAssert("current play keeps starting judgement", () =>
                gameplay.ActiveJudgementConfiguration.Mode
                    == JudgementMode.Yokko
                && gameplay.ActiveJudgementWindows.Configuration.Mode
                    == JudgementMode.Yokko);
            AddStep("restore judgement preference", () =>
            {
                gameplaySettings.JudgementMode.Value = originalMode;
                gameplaySettings.SetEtternaJustice(originalJustice);
            });
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
                && timingBar.Y == 8);
            AddAssert("fast and slow limits flank the timing bar", () =>
            {
                SpriteText early = timingBar
                                   .ChildrenOfType<SpriteText>()
                                   .Single(text =>
                                       text.Name == "Timing early limit");
                SpriteText late = timingBar
                                  .ChildrenOfType<SpriteText>()
                                  .Single(text =>
                                      text.Name == "Timing late limit");
                return early.Anchor == Anchor.TopLeft
                       && early.Origin == Anchor.CentreRight
                       && early.X < 0
                       && late.Anchor == Anchor.TopRight
                       && late.Origin == Anchor.CentreLeft
                       && late.X > 0;
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
            AddStep("show early timing marker", () =>
                timingBar.Show(new JudgementInputEvent(
                    0,
                    0,
                    1000,
                    987.5,
                    -12.5,
                    JudgementRating.Perfect,
                    JudgementPhase.Tap)));
            AddAssert("early timing is left of centre", () =>
                timingBar.RecordedMarkerCount == 1
                && timingBar.DisplayedDirectionKey
                == "gameplay.timing.early"
                && timingBar.LatestMarkerPosition
                < timingBar.CentreMarkerPosition
                && timingBar.LatestHitErrorMilliseconds == -12.5
                && timingBar.PressTrendMilliseconds == -12.5);
            AddStep("show late timing marker", () =>
                timingBar.Show(new JudgementInputEvent(
                    1,
                    1,
                    1200,
                    1225,
                    25,
                    JudgementRating.Great,
                    JudgementPhase.Tap)));
            AddAssert("late timing is right of centre", () =>
                timingBar.RecordedMarkerCount == 2
                && timingBar.DisplayedDirectionKey
                == "gameplay.timing.late"
                && timingBar.LatestMarkerPosition
                > timingBar.CentreMarkerPosition
                && timingBar.LatestHitErrorMilliseconds == 25
                && timingBar.PressTrendMilliseconds == -6.875);
            AddStep("show manually pressed miss", () =>
                timingBar.Show(new JudgementInputEvent(
                    2,
                    2,
                    1400,
                    1550,
                    150,
                    JudgementRating.Miss,
                    JudgementPhase.Tap)));
            AddAssert("manual miss uses the full miss axis", () =>
                timingBar.RecordedMarkerCount == 3
                && timingBar.LatestMarkerPosition
                > timingBar.CentreMarkerPosition
                && timingBar.LatestMarkerPosition
                < timingBar.MaximumMarkerPosition);
            AddStep("show late hold release", () =>
                timingBar.Show(new JudgementInputEvent(
                    3,
                    3,
                    1600,
                    1660,
                    60,
                    JudgementRating.Great,
                    JudgementPhase.HoldTail,
                    BeatmapJudgementState.HoldReleaseWindowLenience)));
            AddAssert("hold release has an independent trend", () =>
                timingBar.RecordedMarkerCount == 4
                && timingBar.LatestPhase == JudgementPhase.HoldTail
                && timingBar.ReleaseTrendMilliseconds == 60
                && timingBar.LatestMarkerPosition
                > timingBar.CentreMarkerPosition);
            AddStep("show stable and BMS LN release input", () =>
                timingBar.Show(new JudgementInputEvent(
                    4,
                    0,
                    1800,
                    1782,
                    -18,
                    JudgementRating.Great,
                    JudgementPhase.HoldTail)));
            AddAssert("stable and BMS LN release is recorded independently", () =>
                timingBar.RecordedMarkerCount == 5
                && timingBar.LatestPhase == JudgementPhase.HoldTail
                && Math.Abs(
                    timingBar.ReleaseTrendMilliseconds!.Value - 48.3) < 0.001
                && Math.Abs(
                    timingBar.PressTrendMilliseconds!.Value - 16.65625) < 0.001
                && timingBar.LatestMarkerPosition
                < timingBar.CentreMarkerPosition);
            AddStep("show stable and BMS LN head input", () =>
                timingBar.Show(new JudgementInputEvent(
                    5,
                    1,
                    2000,
                    2010,
                    10,
                    JudgementRating.Perfect,
                    JudgementPhase.HoldHead)));
            AddAssert("stable and BMS LN head is recorded independently", () =>
                timingBar.RecordedMarkerCount == 6
                && timingBar.LatestPhase == JudgementPhase.HoldHead
                && Math.Abs(
                    timingBar.PressTrendMilliseconds!.Value - 15.6578125) < 0.001
                && Math.Abs(
                    timingBar.ReleaseTrendMilliseconds!.Value - 48.3) < 0.001
                && timingBar.LatestMarkerPosition
                > timingBar.CentreMarkerPosition);
            AddStep("fill timing marker history", () =>
            {
                for (int i = 0; i < 60; i++)
                {
                    double error = i % 2 == 0 ? -30 : 30;
                    timingBar.Show(new JudgementInputEvent(
                        7 + i,
                        i % 4,
                        2400 + i,
                        2400 + i + error,
                        error,
                        JudgementRating.Great,
                        JudgementPhase.Tap));
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
        public void TestEarlierNotesDrawOnTopLikeOsuMania()
        {
            GameplayPlayfield playfield = null;
            BeatmapJudgementState state = null;
            int coveredTapIndex = -1;
            int coveringHoldIndex = -1;
            int laterNoteIndex = -1;

            AddStep("create hidden-note fixture", () =>
            {
                // The corpus keeps the covered tap before the earlier hold in
                // file order. osu!mania still draws the hold on top, including
                // while inherited SV changes are active.
                YokkoBeatmap beatmap =
                    OsuManiaBeatmapIO.ReadBeatmapFromFile(
                        Path.Combine(
                            TestContext.CurrentContext.TestDirectory,
                            "Resources",
                            "Testing",
                            "Beatmaps",
                            "SvGimmicks",
                            "hidden-note-long-note-cover.osu"));
                coveredTapIndex = beatmap.HitObjects
                                           .Select((hitObject, index) =>
                                               (hitObject, index))
                                           .Single(item =>
                                               item.hitObject
                                                   .StartTimeMilliseconds
                                               == 3000)
                                           .index;
                coveringHoldIndex = beatmap.HitObjects
                                             .Select((hitObject, index) =>
                                                 (hitObject, index))
                                             .Single(item =>
                                                 item.hitObject
                                                     .StartTimeMilliseconds
                                                 == 1000)
                                             .index;
                laterNoteIndex = beatmap.HitObjects
                                        .Select((hitObject, index) =>
                                            (hitObject, index))
                                        .Single(item =>
                                            item.hitObject
                                                .StartTimeMilliseconds
                                            == 7000)
                                        .index;
                state = new BeatmapJudgementState(beatmap);
                playfield = new GameplayPlayfield(
                    beatmap,
                    KeyModeBindings.ForMode(KeyMode.FourKey));
                Add(playfield);
            });
            AddUntilStep("hidden-note playfield loaded", () =>
                playfield?.IsLoaded == true);
            AddStep("update to middle of hold", () =>
                playfield.UpdateGameplayTime(3000, state));
            AddAssert("covered note sits behind the hold", () =>
                playfield.GetDrawableNote(coveredTapIndex).Depth
                > playfield.GetDrawableNote(coveringHoldIndex).Depth);
            AddAssert("later note sits behind earlier notes", () =>
                playfield.GetDrawableNote(laterNoteIndex).Depth
                > playfield.GetDrawableNote(coveredTapIndex).Depth);
            AddStep("remove hidden-note playfield", () =>
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
        public void TestGameplayLayoutEditorPausesAndShowsFullPagePreview()
        {
            GameplayScreen gameplayScreen = null;
            GameplayLayoutEditorOverlay layoutEditor = null;
            GameplayTimingBar timingBar = null;
            GameplayHud gameplayHud = null;
            GameplayComboReadout comboReadout = null;
            JudgementReadout judgementReadout = null;
            GameplayPlayfield gameplayPlayfield = null;
            ManiaScoreResult autoplayBaselineResult = null;
            double autoplayBaselineTime = 0;
            int autoplayBaselinePauses = 0;
            int autoplayBaselineHitErrors = 0;

            AddStep("open gameplay layout fixture", () =>
            {
                gameplaySettings.ResetGameplayLayout();
                gameplayScreen = new GameplayScreen(
                    DemoBeatmaps.CreateFourKeyDemo());
                screenStack.Push(gameplayScreen);
            });
            AddUntilStep("gameplay layout loaded", () =>
                gameplayScreen?
                    .ChildrenOfType<GameplayLayoutEditorOverlay>()
                    .SingleOrDefault() != null);
            AddStep("pause gameplay", () => gameplayScreen.TogglePause());
            AddUntilStep("pause menu is ready", () =>
                gameplayScreen.IsPaused
                && gameplayScreen
                    .ChildrenOfType<GameplayPauseOverlay>()
                    .SingleOrDefault() != null);
            AddStep("open layout editor from pause menu", () =>
            {
                GameplayPauseOverlay pauseOverlay = gameplayScreen
                    .ChildrenOfType<GameplayPauseOverlay>()
                    .Single();
                pauseOverlay.SelectNext();
                pauseOverlay.SelectNext();
                pauseOverlay.TriggerSelected();
            });
            AddUntilStep("layout editor is active", () =>
                gameplayScreen.IsLayoutEditing
                && (layoutEditor = gameplayScreen
                    .ChildrenOfType<GameplayLayoutEditorOverlay>()
                    .SingleOrDefault()) != null
                && (timingBar = gameplayScreen
                    .ChildrenOfType<GameplayTimingBar>()
                    .SingleOrDefault()) != null
                && (gameplayHud = gameplayScreen
                    .ChildrenOfType<GameplayHud>()
                    .SingleOrDefault()) != null
                && (comboReadout = gameplayScreen
                    .ChildrenOfType<GameplayComboReadout>()
                    .SingleOrDefault()) != null
                && (judgementReadout = gameplayScreen
                    .ChildrenOfType<JudgementReadout>()
                    .SingleOrDefault()) != null
                && (gameplayPlayfield = gameplayScreen
                    .ChildrenOfType<GameplayPlayfield>()
                    .SingleOrDefault()) != null);
            AddAssert("gameplay is paused while arranging", () =>
                gameplayScreen.IsPaused);
            AddAssert("six draggable tool windows and controller are visible", () =>
                layoutEditor.ToolWindowCountForTest == 6
                && layoutEditor.VisibleToolWindowCountForTest == 6
                && layoutEditor.ToolWindowControllerVisibleForTest);
            AddStep("drag every tool window and controller to a viewport edge", () =>
            {
                foreach (GameplayLayoutEditorToolWindow kind
                         in Enum.GetValues<GameplayLayoutEditorToolWindow>())
                {
                    layoutEditor.MoveToolWindowForTest(
                        kind,
                        new Vector2(-10_000));
                }

                layoutEditor.MoveToolWindowControllerForTest(
                    new Vector2(10_000));
            });
            AddAssert("all tool windows stay recoverable inside the viewport", () =>
                Enum.GetValues<GameplayLayoutEditorToolWindow>()
                    .All(layoutEditor.IsToolWindowInsideViewportForTest)
                && layoutEditor.IsToolWindowControllerInsideViewportForTest);
            AddStep("restore tool window positions", () =>
                layoutEditor.ResetToolWindowPositionsForTest());
            AddStep("hide feedback window from controller", () =>
                layoutEditor.ToggleToolWindowForTest(
                    GameplayLayoutEditorToolWindow.Feedback));
            AddUntilStep("feedback window hides without hiding controller", () =>
                !layoutEditor.IsToolWindowVisibleForTest(
                    GameplayLayoutEditorToolWindow.Feedback)
                && layoutEditor.VisibleToolWindowCountForTest == 5
                && layoutEditor.ToolWindowControllerVisibleForTest);
            AddStep("show feedback window from controller", () =>
                layoutEditor.ToggleToolWindowForTest(
                    GameplayLayoutEditorToolWindow.Feedback));
            AddUntilStep("feedback window is restored", () =>
                layoutEditor.IsToolWindowVisibleForTest(
                    GameplayLayoutEditorToolWindow.Feedback)
                && layoutEditor.VisibleToolWindowCountForTest == 6);
            AddAssert("eight elements expose only applicable resize handles", () =>
                layoutEditor.TransformTargetCount == 8
                && layoutEditor.ResizeHandleCount == 28);
            AddAssert("combo and judgement show editor previews", () =>
                comboReadout.Alpha > 0
                && judgementReadout.Alpha > 0
                && judgementReadout.DisplayedRating == "GREAT");
            AddAssert("overview keeps full page aspect ratio", () =>
                Math.Abs(
                    gameplayScreen.LayoutOverviewAspectRatio
                    - 16f / 9f) < 0.001f);
            AddAssert("playfield starts selected", () =>
                layoutEditor.SelectedElementForTest == "Playfield");
            AddAssert("performance target has a visible real preview", () =>
                layoutEditor.PerformanceReadoutPreviewAlphaForTest > 0.99f);
            AddAssert("inactive blocker handles stay inside the canvas", () =>
                layoutEditor.TopCoverHandleTopForTest >= -0.01f
                && layoutEditor.BottomCoverHandleBottomForTest
                    <= layoutEditor.DrawHeight + 0.01f);
            AddStep("Tab selects the next editable element", () =>
                layoutEditor.SelectNextElementForTest(false));
            AddAssert("Tab moves selection to accuracy", () =>
                layoutEditor.SelectedElementForTest == "Accuracy");
            AddStep("Shift Tab selects the previous element", () =>
                layoutEditor.SelectNextElementForTest(true));
            AddAssert("reverse selection returns to playfield", () =>
                layoutEditor.SelectedElementForTest == "Playfield");
            AddStep("hide accuracy and select next", () =>
            {
                layoutEditor.SetAccuracyHiddenForTest(true);
                layoutEditor.SelectNextElementForTest(false);
            });
            AddAssert("Tab skips hidden elements", () =>
                layoutEditor.SelectedElementForTest == "Progress");
            AddStep("restore accuracy", () =>
                layoutEditor.SetAccuracyHiddenForTest(false));
            AddStep("move performance readout", () =>
                layoutEditor.MovePerformanceReadoutForTest(
                    new Vector2(-120, -80)));
            AddAssert("performance readout position is editable", () =>
                gameplaySettings.LayoutPerformanceReadoutOffsetX.Value < 0
                && gameplaySettings.LayoutPerformanceReadoutOffsetY.Value < 0);
            AddStep("add top and bottom blockers", () =>
            {
                layoutEditor.SetTopCoverEnabledForTest(true);
                layoutEditor.SetBottomCoverEnabledForTest(true);
            });
            AddUntilStep("new blockers use a useful default height", () =>
                layoutEditor.TopCoverEnabledForTest
                && layoutEditor.BottomCoverEnabledForTest
                && Math.Abs(layoutEditor.TopCoverHeightForTest - 120) < 2
                && Math.Abs(layoutEditor.BottomCoverHeightForTest - 120) < 2);
            AddStep("set exact blocker heights", () =>
            {
                layoutEditor.SetTopCoverHeightForTest(210);
                layoutEditor.SetBottomCoverHeightForTest(84);
            });
            AddUntilStep("exact blocker heights are applied", () =>
                Math.Abs(layoutEditor.TopCoverHeightForTest - 210) < 2
                && Math.Abs(layoutEditor.BottomCoverHeightForTest - 84) < 2);
            float baseJudgementPosition = 0;
            double baseJudgementApproachTime = 0;
            AddStep("set exact judgement line position", () =>
            {
                baseJudgementPosition =
                    layoutEditor.JudgementLinePositionForTest;
                baseJudgementApproachTime =
                    gameplayScreen.PlayfieldApproachTimeForTest;
                layoutEditor.SetJudgementLinePositionForTest(
                    baseJudgementPosition - 80);
            });
            AddUntilStep("judgement region moves as one unit", () =>
                Math.Abs(
                    layoutEditor.JudgementLinePositionForTest
                    - (baseJudgementPosition - 80)) < 0.1
                && gameplayScreen.PlayfieldJudgementRegionAlignedForTest
                && gameplayScreen.PlayfieldApproachTimeForTest
                    < baseJudgementApproachTime);
            AddStep("drag judgement line to another exact position", () =>
                layoutEditor.DragJudgementLineForTest(
                    baseJudgementPosition - 32));
            AddUntilStep("drag uses the same judgement geometry path", () =>
                Math.Abs(
                    layoutEditor.JudgementLinePositionForTest
                    - (baseJudgementPosition - 32)) < 0.1
                && gameplayScreen.PlayfieldJudgementRegionAlignedForTest);
            AddStep("reset judgement line to skin baseline", () =>
                layoutEditor.SetJudgementLinePositionForTest(
                    baseJudgementPosition));
            AddUntilStep("judgement line baseline restores", () =>
                Math.Abs(
                    layoutEditor.JudgementLinePositionForTest
                    - baseJudgementPosition) < 0.1
                && Math.Abs(
                    gameplayScreen.PlayfieldApproachTimeForTest
                    - baseJudgementApproachTime) < 0.1);
            AddStep("bottom blocker can fill the whole playfield", () =>
                layoutEditor.SetBottomCoverHeightForTest(10000));
            AddUntilStep("bottom blocker has no artificial half-height cap", () =>
                Math.Abs(
                    gameplaySettings.LayoutBottomCoverRatio.Value
                    - YokkoGameplaySettings.MaximumBottomCoverRatio) < 0.0001
                && gameplaySettings.LayoutBottomCoverRatio.Value > 0.99);
            AddStep("restore exact bottom blocker height", () =>
                layoutEditor.SetBottomCoverHeightForTest(84));
            AddStep("drag top blocker resize bar", () =>
                layoutEditor.DragTopCoverResizeForTest(160));
            AddUntilStep("drag resize changes blocker height", () =>
                Math.Abs(layoutEditor.TopCoverHeightForTest - 160) < 2);
            AddStep("remove top blocker", () =>
                layoutEditor.SetTopCoverEnabledForTest(false));
            AddAssert("blockers can be removed independently", () =>
                !layoutEditor.TopCoverEnabledForTest
                && layoutEditor.BottomCoverEnabledForTest
                && gameplaySettings.LayoutTopCoverRatio.Value == 0
                && gameplaySettings.LayoutBottomCoverRatio.Value > 0);
            AddStep("restore layout after blocker checks", () =>
                gameplaySettings.ResetGameplayLayout());
            double timingBarOffsetX = 0;
            double timingBarOffsetY = 0;
            AddStep("nudge timing bar with arrow keys", () =>
            {
                timingBarOffsetX =
                    gameplaySettings.LayoutTimingBarOffsetX.Value;
                timingBarOffsetY =
                    gameplaySettings.LayoutTimingBarOffsetY.Value;
                layoutEditor.NudgeTimingBarForTest(
                    Key.Right,
                    false);
                layoutEditor.NudgeTimingBarForTest(
                    Key.Up,
                    true);
            });
            AddAssert("arrow key nudges use normal and accelerated steps", () =>
                gameplaySettings.LayoutTimingBarOffsetX.Value
                    > timingBarOffsetX
                && gameplaySettings.LayoutTimingBarOffsetY.Value
                    < timingBarOffsetY);
            float keyboardResizeStart = 0;
            AddStep("Ctrl arrow resizes timing bar", () =>
            {
                keyboardResizeStart =
                    layoutEditor.TimingBarEditorWidthForTest;
                layoutEditor.ResizeTimingBarWithKeyboardForTest(
                    Key.Right,
                    false);
            });
            AddUntilStep("keyboard resize is applied", () =>
                layoutEditor.TimingBarEditorWidthForTest
                    > keyboardResizeStart);
            AddStep("restore layout after keyboard transforms", () =>
                gameplaySettings.ResetGameplayLayout());
            AddStep("lock timing bar", () =>
                layoutEditor.SetTimingBarLockedForTest(true));
            AddAssert("locked timing bar rejects keyboard nudge", () =>
            {
                double before =
                    gameplaySettings.LayoutTimingBarOffsetX.Value;
                bool handled = layoutEditor.NudgeTimingBarForTest(
                    Key.Right,
                    false);
                return layoutEditor.TimingBarLockedForTest
                       && !handled
                       && Math.Abs(
                           gameplaySettings.LayoutTimingBarOffsetX.Value
                           - before) < 0.000001;
            });
            AddStep("unlock timing bar", () =>
                layoutEditor.SetTimingBarLockedForTest(false));
            AddStep("hide lower information layer in editor", () =>
                layoutEditor.SetHudHiddenForTest(true));
            AddAssert("hidden information remains recoverable independently", () =>
                layoutEditor.HudHiddenForTest
                && gameplayHud.InformationLayoutDrawable.Alpha == 0
                && gameplayHud.AccuracyLayoutDrawable.Alpha > 0
                && gameplayHud.ProgressLayoutDrawable.Alpha > 0);
            AddStep("show lower information layer again", () =>
                layoutEditor.SetHudHiddenForTest(false));
            AddAssert("information visibility is restored", () =>
                !layoutEditor.HudHiddenForTest
                && gameplayHud.InformationLayoutDrawable.Alpha > 0);
            double accuracyOffsetX = 0;
            double progressOffsetY = 0;
            double informationOffsetX = 0;
            AddStep("move the three HUD sections independently", () =>
            {
                accuracyOffsetX = gameplaySettings.LayoutAccuracyOffsetX.Value;
                progressOffsetY = gameplaySettings.LayoutProgressOffsetY.Value;
                informationOffsetX = gameplaySettings.LayoutHudOffsetX.Value;
                layoutEditor.MoveAccuracyForTest(new Vector2(60, 0));
                layoutEditor.MoveProgressForTest(new Vector2(0, 50));
                layoutEditor.MoveInformationForTest(new Vector2(-70, 0));
            });
            AddAssert("HUD section positions remain independent", () =>
                gameplaySettings.LayoutAccuracyOffsetX.Value > accuracyOffsetX
                && gameplaySettings.LayoutAccuracyOffsetY.Value == 0
                && gameplaySettings.LayoutProgressOffsetX.Value == 0
                && gameplaySettings.LayoutProgressOffsetY.Value > progressOffsetY
                && gameplaySettings.LayoutHudOffsetX.Value < informationOffsetX
                && gameplaySettings.LayoutHudOffsetY.Value == 0);
            AddStep("restore layout after HUD section transforms", () =>
                gameplaySettings.ResetGameplayLayout());
            double judgementOffsetX = 0;
            AddStep("slow pointer drag escapes centre snap", () =>
            {
                judgementOffsetX =
                    gameplaySettings.LayoutJudgementOffsetX.Value;
                layoutEditor.DragJudgementPointerIncrementallyForTest(
                    new Vector2(24, 0),
                    24);
            });
            AddAssert("incremental drag moves judgement", () =>
                gameplaySettings.LayoutJudgementOffsetX.Value
                    > judgementOffsetX + 0.005);
            AddStep("shrink judgement to minimum scale", () =>
            {
                gameplaySettings.LayoutJudgementScaleX.Value = 0.25;
                gameplaySettings.LayoutJudgementScaleY.Value = 0.25;
            });
            AddUntilStep("small judgement keeps a movable centre", () =>
                layoutEditor.JudgementCentreAllowsMoveDragForTest);
            AddStep("restore layout after pointer regressions", () =>
                gameplaySettings.ResetGameplayLayout());
            AddAssert("zero drag does not move an outlying target", () =>
                layoutEditor.SnapTimingBarMoveForTest(Vector2.Zero, true)
                    .LengthSquared < 0.000001f);
            float comboCentreX = 0;
            float judgementWidth = 0;
            AddStep("move combo and resize judgement display", () =>
            {
                comboCentreX = layoutEditor.ComboEditorCentreXForTest;
                judgementWidth =
                    layoutEditor.JudgementEditorWidthForTest;
                layoutEditor.MoveComboForTest(new Vector2(90, 44));
                layoutEditor.ResizeJudgementForTest(
                    new Vector2(70, 18));
            });
            AddUntilStep("combo and judgement transforms apply", () =>
                layoutEditor.ComboEditorCentreXForTest
                    > comboCentreX + 70
                && layoutEditor.JudgementEditorWidthForTest
                    > judgementWidth + 30
                && gameplaySettings.LayoutComboOffsetX.Value > 0
                && gameplaySettings.LayoutComboOffsetY.Value > 0
                && gameplaySettings.LayoutJudgementScaleX.Value > 1);
            AddStep("restore layout after readout transforms", () =>
                gameplaySettings.ResetGameplayLayout());
            AddStep("keep combo recoverable when moved off canvas", () =>
                layoutEditor.MoveComboSafelyForTest(
                    new Vector2(-10000, 10000)));
            AddUntilStep("combo stays inside a recoverable area", () =>
                layoutEditor.ComboEditorLeftForTest >= -0.01f
                && layoutEditor.ComboEditorRightForTest
                    <= layoutEditor.DrawWidth + 0.01f);
            AddStep("centre combo with the Home action", () =>
            {
                layoutEditor.SelectComboForTest();
                layoutEditor.CentreSelectedForTest();
            });
            AddUntilStep("Home centres both axes", () =>
                Math.Abs(
                    layoutEditor.ComboEditorCentreXForTest
                    - layoutEditor.DrawWidth / 2) < 1);
            AddStep("restore layout after safe movement checks", () =>
                gameplaySettings.ResetGameplayLayout());
            double originalBackgroundDim = 0;
            AddStep("adjust live background dim", () =>
            {
                originalBackgroundDim =
                    gameplayScreen.LayoutEditorBackgroundDimForTest;
                gameplayScreen.SetLayoutEditorBackgroundDimForTest(0.8);
            });
            AddUntilStep("background dim updates live", () =>
                Math.Abs(
                    gameplayScreen.LayoutEditorBackgroundDimForTest
                    - 0.8) < 0.001
                && Math.Abs(
                    gameplayScreen.DisplayedBackgroundDimForTest
                    - 0.8f) < 0.001f);
            AddStep("restore background dim", () =>
                gameplayScreen.SetLayoutEditorBackgroundDimForTest(
                    originalBackgroundDim));
            double originalScrollSpeed = 0;
            ManiaScrollDirection originalScrollDirection =
                ManiaScrollDirection.Downscroll;
            AddStep("change live scroll settings", () =>
            {
                originalScrollSpeed = gameplaySettings.ScrollSpeed.Value;
                originalScrollDirection =
                    gameplaySettings.ScrollDirection.Value;
                gameplayScreen.SetLayoutEditorScrollSpeedForTest(12);
                gameplayScreen.SetLayoutEditorScrollDirectionForTest(
                    ManiaScrollDirection.Upscroll);
            });
            AddUntilStep("scroll settings update inside editor", () =>
                Math.Abs(
                    gameplayScreen.AppliedScrollSpeedForTest - 12) < 0.001
                && gameplayScreen.LayoutEditorScrollDirectionForTest
                == ManiaScrollDirection.Upscroll);
            AddStep("restore live scroll settings", () =>
            {
                gameplayScreen.SetLayoutEditorScrollSpeedForTest(
                    originalScrollSpeed);
                gameplayScreen.SetLayoutEditorScrollDirectionForTest(
                    originalScrollDirection);
            });
            AddUntilStep("live scroll settings restore cleanly", () =>
                Math.Abs(
                    gameplayScreen.AppliedScrollSpeedForTest
                    - originalScrollSpeed) < 0.001
                && gameplayScreen.LayoutEditorScrollDirectionForTest
                    == originalScrollDirection);
            bool originalLongNoteCutEnabled = false;
            double originalLongNoteCutAmount = 0;
            AddStep("change live LN cut settings", () =>
            {
                originalLongNoteCutEnabled =
                    gameplayScreen.LayoutEditorLongNoteCutEnabledForTest;
                originalLongNoteCutAmount =
                    gameplayScreen.LayoutEditorLongNoteCutAmountForTest;
                gameplayScreen.SetLayoutEditorLongNoteCutEnabledForTest(true);
                gameplayScreen.SetLayoutEditorLongNoteCutAmountForTest(1.2);
            });
            AddUntilStep("LN cut updates inside editor", () =>
                gameplayScreen.LayoutEditorLongNoteCutEnabledForTest
                && Math.Abs(
                    gameplayScreen.LayoutEditorLongNoteCutAmountForTest
                    - 1.2) < 0.001
                && Math.Abs(
                    gameplayScreen.AppliedLongNoteCutAmountForTest
                    - 1.2) < 0.001);
            AddStep("restore live LN cut settings", () =>
            {
                gameplayScreen.SetLayoutEditorLongNoteCutAmountForTest(
                    originalLongNoteCutAmount);
                gameplayScreen.SetLayoutEditorLongNoteCutEnabledForTest(
                    originalLongNoteCutEnabled);
            });
            double originalJudgementDuration = 0;
            double originalJudgementOpacity = 0;
            double originalHitErrorScale = 0;
            bool originalShowHitError = false;
            bool originalShowTimingBar = false;
            AddStep("change live judgement feedback settings", () =>
            {
                originalJudgementDuration = gameplayScreen
                    .LayoutEditorJudgementDurationForTest;
                originalJudgementOpacity = gameplayScreen
                    .LayoutEditorJudgementOpacityForTest;
                originalHitErrorScale = gameplayScreen
                    .LayoutEditorHitErrorScaleForTest;
                originalShowHitError = gameplayScreen
                    .LayoutEditorShowsHitErrorForTest;
                originalShowTimingBar =
                    gameplaySettings.ShowTimingBar.Value;
                gameplayScreen.SetLayoutEditorJudgementDurationForTest(
                    900);
                gameplayScreen.SetLayoutEditorJudgementOpacityForTest(
                    0.6);
                gameplayScreen.SetLayoutEditorHitErrorScaleForTest(1.6);
                gameplayScreen.SetLayoutEditorShowHitErrorForTest(false);
                gameplayScreen.SetLayoutEditorShowTimingBarForTest(false);
            });
            AddUntilStep("judgement feedback updates inside editor", () =>
                Math.Abs(
                    gameplayScreen.LayoutEditorJudgementDurationForTest
                    - 900) < 0.001
                && Math.Abs(
                    gameplayScreen.LayoutEditorJudgementOpacityForTest
                    - 0.6f) < 0.001f
                && Math.Abs(
                    gameplayScreen.LayoutEditorHitErrorScaleForTest
                    - 1.6f) < 0.001f
                && !gameplayScreen.LayoutEditorShowsHitErrorForTest
                && timingBar.Alpha < 0.01f);
            AddStep("restore live judgement feedback settings", () =>
            {
                gameplayScreen.SetLayoutEditorJudgementDurationForTest(
                    originalJudgementDuration);
                gameplayScreen.SetLayoutEditorJudgementOpacityForTest(
                    originalJudgementOpacity);
                gameplayScreen.SetLayoutEditorHitErrorScaleForTest(
                    originalHitErrorScale);
                gameplayScreen.SetLayoutEditorShowHitErrorForTest(
                    originalShowHitError);
                gameplayScreen.SetLayoutEditorShowTimingBarForTest(
                    originalShowTimingBar);
            });
            AddUntilStep("judgement feedback settings restore cleanly", () =>
                Math.Abs(
                    gameplayScreen.LayoutEditorJudgementDurationForTest
                    - originalJudgementDuration) < 0.001
                && Math.Abs(
                    gameplayScreen.LayoutEditorJudgementOpacityForTest
                    - originalJudgementOpacity) < 0.001
                && Math.Abs(
                    gameplayScreen.LayoutEditorHitErrorScaleForTest
                    - originalHitErrorScale) < 0.001
                && gameplayScreen.LayoutEditorShowsHitErrorForTest
                == originalShowHitError
                && (originalShowTimingBar
                    ? timingBar.Alpha > 0.99f
                    : timingBar.Alpha < 0.01f));
            AddUntilStep("rebuilt HUD is ready", () =>
            {
                GameplayHud[] huds = gameplayScreen
                    .ChildrenOfType<GameplayHud>()
                    .ToArray();
                GameplayPlayfield[] playfields = gameplayScreen
                    .ChildrenOfType<GameplayPlayfield>()
                    .ToArray();
                if (huds.Length != 1 || playfields.Length != 1)
                    return false;

                gameplayHud = huds[0];
                gameplayPlayfield = playfields[0];
                return gameplayHud.IsLoaded;
            });
            AddStep("customise editor UI toggle shortcut", () =>
                gameplaySettings.SetShortcutBinding(
                    ManiaShortcutAction.ToggleLayoutEditorUi,
                    Key.H));
            AddStep("press custom shortcut to hide editor UI", () =>
                gameplayScreen.HandleKeyDownInput(
                    Key.H,
                    false,
                    false,
                    false));
            AddUntilStep("editor UI hides without leaving the session", () =>
                !layoutEditor.IsChromeVisibleForTest
                && layoutEditor.ChromeAlphaForTest < 0.01f
                && layoutEditor.IsEditing
                && gameplayScreen.IsLayoutEditing
                && gameplayScreen.IsPaused);
            AddStep("press custom shortcut again to restore editor UI", () =>
                gameplayScreen.HandleKeyDownInput(
                    Key.H,
                    false,
                    false,
                    false));
            AddUntilStep("editor UI restores in the same session", () =>
                layoutEditor.IsChromeVisibleForTest
                && layoutEditor.ChromeAlphaForTest > 0.99f
                && layoutEditor.IsEditing
                && gameplayScreen.IsLayoutEditing
                && gameplayScreen.IsPaused);
            AddStep("restore editor shortcut default", () =>
                gameplaySettings.ResetShortcutBinding(
                    ManiaShortcutAction.ToggleLayoutEditorUi));
            float requestedSnapDelta = 0;
            Vector2 snappedDelta = Vector2.Zero;
            Vector2 bypassedDelta = Vector2.Zero;
            AddStep("calculate centre snap", () =>
            {
                requestedSnapDelta =
                    layoutEditor.DrawWidth / 2
                    - layoutEditor.TimingBarEditorCentreXForTest
                    + 6;
                snappedDelta = layoutEditor.SnapTimingBarMoveForTest(
                    new Vector2(requestedSnapDelta, 0),
                    false);
                bypassedDelta = layoutEditor.SnapTimingBarMoveForTest(
                    new Vector2(requestedSnapDelta, 0),
                    true);
            });
            AddAssert("snap aligns centre while Alt bypass keeps free delta", () =>
                Math.Abs(
                    layoutEditor.TimingBarEditorCentreXForTest
                    + snappedDelta.X
                    - layoutEditor.DrawWidth / 2) < 0.01f
                && Math.Abs(
                    bypassedDelta.X
                    - requestedSnapDelta) < 0.01f);
            float timingBarEditorWidth = 0;
            AddStep("set precise timing bar width", () =>
            {
                timingBarEditorWidth =
                    layoutEditor.TimingBarEditorWidthForTest;
                layoutEditor.SetTimingBarWidthForTest(
                    timingBarEditorWidth + 48);
            });
            AddUntilStep("precise width is applied", () =>
                layoutEditor.TimingBarEditorWidthForTest
                    > timingBarEditorWidth + 24);
            AddStep("restore layout after inspector checks", () =>
                gameplaySettings.ResetGameplayLayout());
            AddStep("move and resize timing bar", () =>
            {
                layoutEditor.MoveTimingBarForTest(new Vector2(120, -80));
                layoutEditor.ResizeTimingBarForTest(new Vector2(72, 30));
            });
            AddUntilStep("timing bar transform is applied", () =>
                timingBar.Position.X > 100
                && timingBar.Position.Y < -30
                && timingBar.Scale.X > 1.15f
                && timingBar.Scale.Y > 1.2f);
            AddStep("start autoplay layout demo", () =>
            {
                autoplayBaselineResult = gameplayScreen.CurrentResultForTest;
                autoplayBaselineTime = gameplayScreen.CurrentGameplayTime;
                autoplayBaselinePauses = gameplayScreen.PausesUsed;
                autoplayBaselineHitErrors =
                    gameplayScreen.ResultHitErrorCountForTest;
                gameplayScreen.ResumeCountdownMillisecondsOverride = 0;
                gameplayScreen.SetLayoutEditorLongNoteCutEnabledForTest(true);
                gameplayScreen.SetLayoutEditorLongNoteCutAmountForTest(0.4);
                gameplayScreen.BeginLayoutAutoplayDemoForTest();
            });
            AddUntilStep("autoplay demo keeps editor UI visible", () =>
                gameplayScreen.IsLayoutAutoplayPlaying
                && gameplayScreen.AutoplayMode
                && !gameplayScreen.IsPaused
                && gameplayHud.Alpha > 0
                && layoutEditor.AutoplayControlVisibleForTest
                && layoutEditor.ChromeAlphaForTest > 0.9f);
            AddUntilStep("autoplay demo shows real long notes", () =>
                gameplayScreen.LayoutAutoplayDemoLongNoteCountForTest > 0
                && gameplayScreen.VisibleLayoutAutoplayDemoLongNoteCountForTest
                    > 0
                && !gameplayPlayfield.RegularNoteLayerVisible
                && gameplayScreen.LayoutAutoplayDemoLongNoteCutDistanceForTest
                    > 0);
            float autoplayCutDistance = 0;
            AddStep("increase LN cut during autoplay demo", () =>
            {
                autoplayCutDistance = gameplayScreen
                    .LayoutAutoplayDemoLongNoteCutDistanceForTest;
                gameplayScreen.SetLayoutEditorLongNoteCutAmountForTest(1.6);
            });
            AddUntilStep("autoplay long notes update cut in real time", () =>
                gameplayScreen.LayoutAutoplayDemoLongNoteCutDistanceForTest
                    > autoplayCutDistance + 20);
            AddUntilStep("autoplay demo hits notes", () =>
                comboReadout.DisplayedCombo > 0
                && judgementReadout.Alpha > 0);
            AddStep("exit autoplay demo", () =>
                layoutEditor.ExitAutoplayDemoForTest());
            AddUntilStep("autoplay demo returns to editor paused", () =>
                gameplayScreen.IsPaused
                && gameplayScreen.IsLayoutEditing
                && !gameplayScreen.IsLayoutAutoplayPlaying
                && !gameplayScreen.AutoplayMode
                && layoutEditor.IsEditing
                && gameplayPlayfield.RegularNoteLayerVisible
                && !layoutEditor.AutoplayControlVisibleForTest
                && layoutEditor.ChromeAlphaForTest > 0.9f);
            AddAssert("autoplay demo restores the real run exactly", () =>
                gameplayScreen.CurrentResultForTest == autoplayBaselineResult
                && Math.Abs(
                    gameplayScreen.CurrentGameplayTime
                    - autoplayBaselineTime) < 0.01
                && gameplayScreen.PausesUsed == autoplayBaselinePauses
                && gameplayScreen.ResultHitErrorCountForTest
                    == autoplayBaselineHitErrors);
            AddStep("restore LN cut after autoplay demo", () =>
            {
                gameplayScreen.SetLayoutEditorLongNoteCutAmountForTest(
                    originalLongNoteCutAmount);
                gameplayScreen.SetLayoutEditorLongNoteCutEnabledForTest(
                    originalLongNoteCutEnabled);
            });
            double testPlayOffset = 0;
            AddStep("prepare unsaved layout test play", () =>
            {
                gameplayScreen.ResumeCountdownMillisecondsOverride = 0;
                layoutEditor.MoveTimingBarForTest(new Vector2(40, 0));
                testPlayOffset =
                    gameplaySettings.LayoutTimingBarOffsetX.Value;
                gameplayScreen.BeginLayoutTestPlayForTest();
            });
            AddUntilStep("test play resumes without editor chrome", () =>
                gameplayScreen.IsLayoutTestPlaying
                && !gameplayScreen.IsPaused
                && !layoutEditor.IsEditing);
            AddStep("Escape returns to layout editor", () =>
                gameplayScreen.HandleKeyDownInput(
                    Key.Escape,
                    false,
                    false,
                    false));
            AddUntilStep("layout editor returns paused", () =>
                gameplayScreen.IsPaused
                && gameplayScreen.IsLayoutEditing
                && !gameplayScreen.IsLayoutTestPlaying
                && layoutEditor.IsEditing);
            AddAssert("test play keeps unsaved layout", () =>
                Math.Abs(
                    gameplaySettings.LayoutTimingBarOffsetX.Value
                    - testPlayOffset) < 0.000001);
            AddAssert("editor reports unsaved changes", () =>
                layoutEditor.HasUnsavedChangesForTest);
            AddStep("change live settings before cancelling", () =>
            {
                gameplayScreen.SetLayoutEditorBackgroundDimForTest(
                    Math.Min(0.9, originalBackgroundDim + 0.2));
                gameplayScreen.SetLayoutEditorJudgementDurationForTest(
                    originalJudgementDuration + 200);
                gameplayScreen.SetLayoutEditorHitErrorScaleForTest(
                    Math.Min(2.5, originalHitErrorScale + 0.4));
                gameplayScreen.SetLayoutEditorShowTimingBarForTest(
                    !originalShowTimingBar);
            });
            AddStep("press Escape once", () =>
                gameplayScreen.HandleKeyDownInput(
                    Key.Escape,
                    false,
                    false,
                    false));
            AddAssert("first Escape asks before discarding", () =>
                gameplayScreen.IsLayoutEditing
                && layoutEditor.IsCancelConfirmationPendingForTest);
            AddStep("make another change after discard warning", () =>
                layoutEditor.MoveTimingBarForTest(new Vector2(8, 0)));
            AddStep("stale confirmation does not discard", () =>
                gameplayScreen.HandleKeyDownInput(
                    Key.Escape,
                    false,
                    false,
                    false));
            AddAssert("new change requires a fresh confirmation", () =>
                gameplayScreen.IsLayoutEditing
                && layoutEditor.IsCancelConfirmationPendingForTest);
            AddStep("confirm discard with Escape", () =>
                gameplayScreen.HandleKeyDownInput(
                    Key.Escape,
                    false,
                    false,
                    false));
            AddUntilStep("layout editor closes", () =>
                !gameplayScreen.IsLayoutEditing);
            AddAssert("discard restores layout and live settings", () =>
                Math.Abs(
                    gameplaySettings.LayoutTimingBarOffsetX.Value) < 0.000001
                && Math.Abs(
                    gameplayScreen.LayoutEditorBackgroundDimForTest
                    - originalBackgroundDim) < 0.001
                && Math.Abs(
                    gameplayScreen.LayoutEditorJudgementDurationForTest
                    - originalJudgementDuration) < 0.001
                && Math.Abs(
                    gameplayScreen.LayoutEditorHitErrorScaleForTest
                    - originalHitErrorScale) < 0.001
                && gameplaySettings.ShowTimingBar.Value
                    == originalShowTimingBar);
            AddAssert("pause menu remains available", () =>
                gameplayScreen.IsPaused);
        }

        [Test]
        public void TestLayoutEditorUsesSkinJudgementPreview()
        {
            string skinPath = createTestSkin();
            using (var image = new Image<Rgba32>(
                       96,
                       40,
                       new Rgba32(255, 90, 150, 255)))
            {
                image.SaveAsPng(Path.Combine(
                    skinPath,
                    "mania-hit300.png"));
            }

            GameplayScreen gameplay = null;
            GameplayPlayfield playfield = null;
            GameplayLayoutEditorOverlay layoutEditor = null;
            float originalComboCentre = 0;
            float originalJudgementWidth = 0;

            AddStep("open gameplay with judgement skin", () =>
            {
                gameplaySettings.ResetGameplayLayout();
                screenStack.Push(gameplay = new GameplayScreen(
                    DemoBeatmaps.CreateFourKeyDemo(),
                    skinPath: skinPath));
            });
            AddUntilStep("skinned gameplay loaded", () =>
                (playfield = gameplay?
                    .ChildrenOfType<GameplayPlayfield>()
                    .SingleOrDefault())?.UsesSkinJudgementOverlay == true);
            AddStep("pause skinned gameplay", () =>
                gameplay.TogglePause());
            AddUntilStep("pause menu is ready", () =>
                gameplay.ChildrenOfType<GameplayPauseOverlay>()
                        .SingleOrDefault() != null);
            AddStep("open layout editor", () =>
            {
                GameplayPauseOverlay pauseOverlay = gameplay
                    .ChildrenOfType<GameplayPauseOverlay>()
                    .Single();
                pauseOverlay.SelectNext();
                pauseOverlay.SelectNext();
                pauseOverlay.TriggerSelected();
                layoutEditor = gameplay
                    .ChildrenOfType<GameplayLayoutEditorOverlay>()
                    .Single();
            });
            AddUntilStep("skin feedback assets are previewed", () =>
                gameplay.IsLayoutEditing
                && playfield.SkinJudgementEditorPreviewUsesTexture
                && playfield.SkinComboEditorPreviewVisible);
            AddAssert("built-in feedback stays hidden", () =>
                gameplay.ChildrenOfType<GameplayComboReadout>()
                        .Single()
                        .Alpha == 0
                && gameplay.ChildrenOfType<JudgementReadout>()
                        .Single()
                        .Alpha == 0);
            AddStep("move skin combo and resize skin judgement", () =>
            {
                originalComboCentre =
                    layoutEditor.ComboEditorCentreXForTest;
                originalJudgementWidth =
                    layoutEditor.JudgementEditorWidthForTest;
                layoutEditor.MoveComboForTest(new Vector2(90, 44));
                layoutEditor.ResizeJudgementForTest(
                    new Vector2(48, 20));
            });
            AddUntilStep("editor transforms real skin feedback", () =>
                layoutEditor.ComboEditorCentreXForTest
                    > originalComboCentre + 60
                && layoutEditor.JudgementEditorWidthForTest
                    > originalJudgementWidth + 24);
            AddStep("enable a top blocker", () =>
                gameplaySettings.LayoutTopCoverRatio.Value = 0.4);
            AddUntilStep("skin feedback stays above the blocker", () =>
                playfield.LayoutTopCoverHeightForTest > 1
                && playfield.SkinFeedbackRendersAboveLayoutCovers);
            AddStep("hide skin feedback and start autoplay", () =>
            {
                layoutEditor.SetComboHiddenForTest(true);
                layoutEditor.SetJudgementHiddenForTest(true);
                layoutEditor.SetHitEffectsHiddenForTest(true);
                gameplay.ResumeCountdownMillisecondsOverride = 0;
                gameplay.BeginLayoutAutoplayDemoForTest();
            });
            AddUntilStep("hidden skin feedback stays hidden in autoplay", () =>
                gameplay.IsLayoutAutoplayPlaying
                && !playfield.SkinComboVisibleForTest
                && !playfield.SkinJudgementVisibleForTest
                && !playfield.HitEffectsVisibleForTest);
            AddStep("exit autoplay", () =>
                layoutEditor.ExitAutoplayDemoForTest());
            AddUntilStep("editor returns with feedback still hidden", () =>
                gameplay.IsLayoutEditing
                && layoutEditor.IsEditing
                && !playfield.SkinComboVisibleForTest
                && !playfield.SkinJudgementVisibleForTest
                && layoutEditor.HitEffectsHiddenForTest
                && !playfield.HitEffectsVisibleForTest);
            AddStep("restore feedback", () =>
            {
                layoutEditor.SetComboHiddenForTest(false);
                layoutEditor.SetJudgementHiddenForTest(false);
                layoutEditor.SetHitEffectsHiddenForTest(false);
            });
            AddUntilStep("skin feedback is visible again", () =>
                gameplay.IsLayoutEditing
                && playfield.SkinComboVisibleForTest
                && playfield.SkinJudgementVisibleForTest
                && playfield.HitEffectsVisibleForTest);
            AddStep("restore blocker", () =>
                gameplaySettings.ResetGameplayLayout());
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
            AddUntilStep("result overlay is visible", () =>
                (screenStack.CurrentScreen as Drawable)?
                .ChildrenOfType<GameplayResultOverlay>()
                .SingleOrDefault() != null);
            AddAssert("miss result was captured", () =>
                (screenStack.CurrentScreen as GameplayScreen)?
                .CompletedResult?.Miss == 1);
            AddUntilStep("result mascot texture loaded", () =>
                (screenStack.CurrentScreen as Drawable)?
                .ChildrenOfType<GameplayResultOverlay>()
                .SingleOrDefault()?
                .MascotReady == true);
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
        public void TestCompletionFadesMusicBeforeStoppingAudio()
        {
            var audioEngine = new CompletionTrackingAudioEngine();
            GameplayScreen gameplay = null;
            double startingMusicVolume = 0;
            double startingHitSoundVolume = 0;
            int fadeHistoryStart = 0;
            int hitSoundHistoryStart = 0;
            YokkoBeatmap beatmap = DemoBeatmaps.CreateFourKeyDemo() with
            {
                Title = "Completion Transition Test",
                AudioPath = "completion-transition-fixture.wav",
                HitObjects =
                [
                    new YokkoHitObject(
                        0,
                        0,
                        null,
                        HitObjectKind.Tap),
                ],
            };

            AddStep("open transition gameplay", () =>
                screenStack.Push(gameplay = new GameplayScreen(
                    beatmap,
                    audioEngine)));
            AddUntilStep("transition audio starts", () =>
                audioEngine.StartCount == 1);
            AddStep("pass final judgement window", () =>
            {
                audioEngine.SetMixVolumes(
                    audioEngine.MusicVolume,
                    0.8,
                    audioEngine.MetronomeVolume);
                startingMusicVolume = audioEngine.MusicVolume;
                startingHitSoundVolume = audioEngine.HitSoundVolume;
                fadeHistoryStart = audioEngine.MusicVolumeHistory.Count;
                hitSoundHistoryStart =
                    audioEngine.HitSoundVolumeHistory.Count;
                audioEngine.SetPlaybackTime(1000);
            });
            AddUntilStep("completion transition starts", () =>
                gameplay?.GameplayCompleted == true
                && gameplay.CompletionTransitionActive);
            AddAssert("audio is not stopped on completion frame", () =>
                audioEngine.StopCount == 0);
            AddUntilStep("music begins fading before stop", () =>
                audioEngine.MusicVolume < startingMusicVolume
                && audioEngine.StopCount == 0);
            AddAssert("hit-sound tail holds then fades smoothly", () =>
                Math.Abs(
                    GameplayScreen.CalculateCompletionTailFadeRemaining(520)
                    - 1) < 0.000001
                && Math.Abs(
                    GameplayScreen.CalculateCompletionTailFadeRemaining(630)
                    - 0.5) < 0.000001
                && GameplayScreen.CalculateCompletionTailFadeRemaining(740)
                   <= 0.000001);
            AddUntilStep("completion transition finishes", () =>
                gameplay?.CompletionTransitionActive == false
                && audioEngine.StopCount == 1
                && gameplay
                   .ChildrenOfType<GameplayResultOverlay>()
                   .SingleOrDefault() != null);
            AddAssert("music fade is monotonic and reaches silence", () =>
            {
                double[] fade = audioEngine.MusicVolumeHistory
                                           .Skip(fadeHistoryStart)
                                           .ToArray();
                return fade.Length > 2
                       && fade[^1] <= 0.000001
                       && fade.Zip(
                               fade.Skip(1),
                               (previous, current) =>
                                   current <= previous + 0.000001)
                              .All(static monotonic => monotonic);
            });
            AddAssert("final hit-sound tail fades smoothly to silence", () =>
            {
                double[] fade = audioEngine.HitSoundVolumeHistory
                                           .Skip(hitSoundHistoryStart)
                                           .ToArray();
                return fade.Length > 2
                       && fade[^1] <= 0.000001
                       && fade.All(volume =>
                           volume <= startingHitSoundVolume + 0.000001)
                       && fade.Zip(
                               fade.Skip(1),
                               (previous, current) =>
                                   current <= previous + 0.000001)
                              .All(static monotonic => monotonic);
            });
        }

        [Test]
        public void TestMutedCompletionRestoresOnlyAfterAudioStops()
        {
            var stopCompletion = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var audioEngine = new CompletionTrackingAudioEngine
            {
                StopCompletion = stopCompletion,
            };
            GameplayScreen gameplay = null;
            YokkoBeatmap beatmap = DemoBeatmaps.CreateFourKeyDemo() with
            {
                Title = "Muted Completion Transition Test",
                AudioPath = "muted-completion-transition-fixture.wav",
                HitObjects =
                [
                    new YokkoHitObject(
                        0,
                        0,
                        null,
                        HitObjectKind.Tap),
                ],
            };
            ManiaModSet mods = ManiaModSet.Empty.WithMuted(
                inverse: true,
                metronome: false,
                comboCount: 1,
                affectsHitSounds: false);

            AddStep("open inverse Muted transition", () =>
                screenStack.Push(gameplay = new GameplayScreen(
                    beatmap,
                    audioEngine,
                    mods: mods)));
            AddUntilStep("Muted transition audio starts", () =>
                audioEngine.StartCount == 1);
            AddAssert("inverse Muted begins silent", () =>
                audioEngine.MusicVolume <= 0.000001);
            AddStep("complete inverse Muted gameplay", () =>
                audioEngine.SetPlaybackTime(1000));
            AddUntilStep("Muted completion requests stop", () =>
                gameplay?.CompletionTransitionActive == false
                && audioEngine.StopCount == 1);
            AddAssert("Muted mix stays silent while stop is pending", () =>
                !stopCompletion.Task.IsCompleted
                && audioEngine.MusicVolume <= 0.000001);
            AddStep("release Muted audio stop", () =>
                stopCompletion.SetResult(true));
            AddUntilStep("Muted mix restores after audio stops", () =>
                audioEngine.MusicVolume > 0.5);
        }

        [Test]
        public void TestCompletionTransitionCanBeSkippedAfterSettle()
        {
            var audioEngine = new CompletionTrackingAudioEngine();
            GameplayScreen gameplay = null;
            YokkoBeatmap beatmap = DemoBeatmaps.CreateFourKeyDemo() with
            {
                Title = "Skippable Completion Transition Test",
                AudioPath = "skippable-completion-transition-fixture.wav",
                HitObjects =
                [
                    new YokkoHitObject(
                        0,
                        0,
                        null,
                        HitObjectKind.Tap),
                ],
            };

            AddStep("open skippable transition", () =>
                screenStack.Push(gameplay = new GameplayScreen(
                    beatmap,
                    audioEngine)));
            AddUntilStep("skippable transition audio starts", () =>
                audioEngine.StartCount == 1);
            AddStep("complete skippable gameplay", () =>
                audioEngine.SetPlaybackTime(1000));
            AddUntilStep("completion skip becomes available", () =>
                gameplay?.CompletionTransitionActive == true
                && gameplay.CompletionTransitionElapsedMilliseconds >= 320);
            AddStep("skip completion transition", () =>
                gameplay.HandleKeyDownInput(
                    Key.Enter,
                    false,
                    false,
                    false));
            AddAssert("skip reveals results and stops audio", () =>
                !gameplay.CompletionTransitionActive
                && audioEngine.StopCount == 1
                && gameplay
                   .ChildrenOfType<GameplayResultOverlay>()
                   .SingleOrDefault() != null);
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
        public void TestDeveloperAutoplayPersistsReplayAndScore()
        {
            YokkoBeatmap beatmap = DemoBeatmaps.CreateFourKeyDemo() with
            {
                Title =
                    $"Developer Autoplay {TestContext.CurrentContext.Test.ID}",
                HitObjects =
                [
                    new YokkoHitObject(
                        0,
                        0,
                        null,
                        HitObjectKind.Tap),
                ],
            };
            ManiaModSet mods = ManiaModSet.Empty.With(
                ManiaModId.Autoplay,
                true);
            GameplayScreen gameplay = null;

            AddStep("open developer autoplay", () =>
                screenStack.Push(gameplay = new GameplayScreen(
                    beatmap,
                    mods: mods)));
            AddUntilStep("developer autoplay completes", () =>
                gameplay?.GameplayCompleted == true);
            AddAssert("AD run owns generated autoplay", () =>
                gameplay.DeveloperAutoplayRun
                && gameplay.CompletedResult.Perfect == 1);
            AddAssert("AD replay and score were persisted", () =>
                gameplay.BestScoreSaved
                && !string.IsNullOrWhiteSpace(gameplay.SavedReplayPath)
                && File.Exists(gameplay.SavedReplayPath));
            AddAssert("saved replay restores AD inputs", () =>
            {
                YokkoReplayLoadResult restored =
                    YokkoReplayIO.ReadFromFile(gameplay.SavedReplayPath);
                return restored.Replay.Mods.Contains(ManiaModId.Autoplay)
                       && restored.Replay.Frames.Count > 0;
            });
            AddStep("remove AD replay fixture", () =>
                File.Delete(gameplay.SavedReplayPath));
        }

        [Test]
        public void TestReplayPlaybackControlsPauseAndAdjustRate()
        {
            YokkoBeatmap beatmap = DemoBeatmaps.CreateFourKeyDemo() with
            {
                Title = "Replay Controls Test",
                HitObjects =
                [
                    new YokkoHitObject(
                        0,
                        5000,
                        null,
                        HitObjectKind.Tap),
                ],
            };
            var replay = new GameplayReplay(
            [
                new GameplayReplayInput(0, true, 5000),
                new GameplayReplayInput(0, false, 5050),
            ]);
            GameplayScreen gameplay = null;
            GameplayReplayControlsOverlay controls = null;
            GameplayPlayfield initialPlayfield = null;

            AddStep("open replay player", () =>
                screenStack.Push(gameplay = new GameplayScreen(
                    beatmap,
                    null,
                    null,
                    null,
                    replay)));
            AddUntilStep("replay controls are visible", () =>
                (controls = gameplay?
                    .ChildrenOfType<GameplayReplayControlsOverlay>()
                    .SingleOrDefault()) != null
                && (initialPlayfield = gameplay
                    .ChildrenOfType<GameplayPlayfield>()
                    .SingleOrDefault()) != null);
            AddStep("pause from replay rail", () =>
                controls.ActivateTogglePause());
            AddUntilStep("replay pauses without pause menu", () =>
                gameplay.IsPaused
                && controls.ShowsPausedState
                && gameplay
                   .ChildrenOfType<GameplayPauseOverlay>()
                   .SingleOrDefault() == null);
            AddStep("increase replay speed", () =>
                controls.ActivateIncreaseRate());
            AddAssert("replay rail shows adjusted rate", () =>
                Math.Abs(gameplay.CurrentPlaybackRate - 1.05) < 0.001
                && controls.RateText == "1.05x");
            AddStep("seek forward from replay rail", () =>
                controls.ActivateSeekForward());
            AddUntilStep("paused replay seeks forward", () =>
                !gameplay.ReplaySeekInProgress
                && gameplay.IsPaused
                && gameplay.CurrentGameplayTime > 4000
                && gameplay.CurrentGameplayTime <= 5050
                && !gameplay.IsLanePressed(0));
            AddStep("seek backward with Left", () =>
                gameplay.HandleKeyDownInput(
                    Key.Left,
                    false,
                    false,
                    false));
            AddUntilStep("rewind restores the earlier state", () =>
                !gameplay.ReplaySeekInProgress
                && gameplay.IsPaused
                && gameplay.CurrentGameplayTime < 0.001
                && !gameplay.IsLanePressed(0));
            AddStep("preview replay progress midpoint", () =>
                controls.PreviewProgressForTest(0.5));
            AddAssert("progress preview updates without seeking", () =>
                Math.Abs(controls.DisplayedProgressMilliseconds - 2525)
                < 0.001
                && gameplay.CurrentGameplayTime < 0.001
                && controls.TimeText == "00:02 / 00:05");
            AddStep("cancel progress drag on focus loss", () =>
                gameplay.HandleHostDeactivated());
            AddAssert("cancelled progress preview returns to playback", () =>
                controls.DisplayedProgressMilliseconds < 0.001);
            AddStep("preview replay progress midpoint again", () =>
                controls.PreviewProgressForTest(0.5));
            AddStep("commit replay progress midpoint", () =>
                controls.CommitProgressForTest());
            AddUntilStep("progress commit seeks once and stays paused", () =>
                !gameplay.ReplaySeekInProgress
                && gameplay.IsPaused
                && Math.Abs(gameplay.CurrentGameplayTime - 2525) < 0.001
                && Math.Abs(controls.DisplayedProgressMilliseconds - 2525)
                   < 0.001
                && ReferenceEquals(
                    initialPlayfield,
                    gameplay.ChildrenOfType<GameplayPlayfield>()
                        .SingleOrDefault()));
            AddStep("resume replay with Space", () =>
                gameplay.HandleKeyDownInput(
                    Key.Space,
                    false,
                    false,
                    false));
            AddUntilStep("replay resumes directly", () =>
                !gameplay.IsPaused
                && !controls.ShowsPausedState);
            AddStep("leave replay controls test", () => gameplay.Exit());
        }

        [Test]
        public void TestReplaySeekDuringLeadInIsAppliedAfterAudioStarts()
        {
            var audioEngine = new SeekTrackingAudioEngine();
            YokkoBeatmap beatmap = DemoBeatmaps.CreateFourKeyDemo() with
            {
                Title = "Replay Lead-In Seek Test",
                AudioPath = "replay-lead-in-seek-fixture.mp3",
                HitObjects =
                [
                    new YokkoHitObject(
                        0,
                        5000,
                        null,
                        HitObjectKind.Tap),
                ],
            };
            var replay = new GameplayReplay(
            [
                new GameplayReplayInput(0, true, 5000),
                new GameplayReplayInput(0, false, 5050),
            ]);
            GameplayScreen gameplay = null;
            GameplayReplayControlsOverlay controls = null;

            AddStep("open replay during audio lead-in", () =>
                screenStack.Push(gameplay = new GameplayScreen(
                    beatmap,
                    audioEngine,
                    null,
                    null,
                    replay)));
            AddUntilStep("lead-in replay controls are visible", () =>
                (controls = gameplay?
                    .ChildrenOfType<GameplayReplayControlsOverlay>()
                    .SingleOrDefault()) != null);
            AddAssert("audio has not started yet", () =>
                audioEngine.StartCount == 0);
            AddStep("commit midpoint before audio starts", () =>
            {
                controls.PreviewProgressForTest(0.5);
                controls.CommitProgressForTest();
            });
            AddAssert("seek waits for audio start", () =>
                audioEngine.SeekCount == 0
                && !gameplay.ReplaySeekInProgress);
            AddUntilStep("queued replay seek is applied", () =>
                audioEngine.StartCount == 1
                && audioEngine.SeekCount == 1
                && !gameplay.ReplaySeekInProgress);
            AddAssert("audio starts at committed replay target", () =>
                Math.Abs(audioEngine.LastSeekMilliseconds - 2500) < 0.001);
            AddStep("leave replay lead-in seek test", () => gameplay.Exit());
        }

        [Test]
        public void TestReplaySeekStaysPausedWhenFocusIsLost()
        {
            var seekCompletion = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var audioEngine = new SeekTrackingAudioEngine
            {
                SeekCompletion = seekCompletion,
            };
            YokkoBeatmap beatmap = DemoBeatmaps.CreateFourKeyDemo() with
            {
                Title = "Replay Seek Focus Test",
                AudioPath = "replay-seek-focus-fixture.mp3",
                HitObjects =
                [
                    new YokkoHitObject(
                        0,
                        5000,
                        null,
                        HitObjectKind.Tap),
                ],
            };
            var replay = new GameplayReplay(
            [
                new GameplayReplayInput(0, true, 5000),
                new GameplayReplayInput(0, false, 5050),
            ]);
            GameplayScreen gameplay = null;
            GameplayReplayControlsOverlay controls = null;

            AddStep("open replay with deferred audio seek", () =>
                screenStack.Push(gameplay = new GameplayScreen(
                    beatmap,
                    audioEngine,
                    null,
                    null,
                    replay)));
            AddUntilStep("replay audio and controls are ready", () =>
                audioEngine.StartCount == 1
                && (controls = gameplay?
                    .ChildrenOfType<GameplayReplayControlsOverlay>()
                    .SingleOrDefault()) != null);
            AddStep("begin deferred replay seek", () =>
            {
                controls.PreviewProgressForTest(0.5);
                controls.CommitProgressForTest();
            });
            AddUntilStep("audio seek is waiting", () =>
                audioEngine.SeekCount == 1
                && gameplay.ReplaySeekInProgress);
            AddStep("lose focus while seek is waiting", () =>
                gameplay.HandleHostDeactivated());
            AddStep("complete audio seek", () =>
                seekCompletion.TrySetResult(true));
            AddUntilStep("seek finishes paused", () =>
                !gameplay.ReplaySeekInProgress
                && gameplay.IsPaused
                && audioEngine.PauseCount >= 2);
            AddStep("leave replay focus seek test", () => gameplay.Exit());
        }

        [Test]
        public void TestPausedReplaySeekRestoresSlidingSampleOnResume()
        {
            var audioEngine = new SampleTrackingAudioEngine();
            bool originalKeysoundsEnabled = false;
            YokkoBeatmap beatmap = DemoBeatmaps.CreateFourKeyDemo() with
            {
                Title = "Replay Sliding Seek Test",
                HitObjects =
                [
                    new YokkoHitObject(
                        0,
                        1000,
                        5000,
                        HitObjectKind.Hold,
                        SamplePayload: new YokkoHitSamplePayload(
                            [new YokkoHitSample(YokkoHitSample.HitNormal)],
                            PlaySlidingSamples: true)),
                ],
            };
            var replay = new GameplayReplay(
            [
                new GameplayReplayInput(0, true, 1000),
                new GameplayReplayInput(0, false, 5000),
            ]);
            GameplayScreen gameplay = null;
            GameplayReplayControlsOverlay controls = null;

            AddStep("enable replay sliding samples", () =>
            {
                originalKeysoundsEnabled =
                    gameplaySettings.KeysoundsEnabled.Value;
                gameplaySettings.KeysoundsEnabled.Value = true;
            });
            AddStep("open hold replay", () =>
                screenStack.Push(gameplay = new GameplayScreen(
                    beatmap,
                    audioEngine,
                    null,
                    null,
                    replay)));
            AddUntilStep("hold replay loop is active", () =>
                gameplay?.CurrentGameplayTime > 1500
                && audioEngine.ActiveLoopCount == 1
                && (controls = gameplay
                    .ChildrenOfType<GameplayReplayControlsOverlay>()
                    .SingleOrDefault()) != null);
            AddStep("pause hold replay", () =>
                controls.ActivateTogglePause());
            AddUntilStep("pause stops sliding loop", () =>
                gameplay.IsPaused
                && audioEngine.ActiveLoopCount == 0);
            AddStep("seek into held note while paused", () =>
            {
                controls.PreviewProgressForTest(0.5);
                controls.CommitProgressForTest();
            });
            AddUntilStep("paused hold seek completes silently", () =>
                !gameplay.ReplaySeekInProgress
                && gameplay.IsPaused
                && gameplay.IsLanePressed(0)
                && audioEngine.ActiveLoopCount == 0);
            AddStep("resume held replay", () =>
                controls.ActivateTogglePause());
            AddUntilStep("resume restarts sliding loop", () =>
                !gameplay.IsPaused
                && audioEngine.ActiveLoopCount == 1);
            AddStep("restore replay keysound setting", () =>
            {
                gameplaySettings.KeysoundsEnabled.Value =
                    originalKeysoundsEnabled;
                gameplay.Exit();
            });
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
        public void TestShiftTabTogglesFocusMode()
        {
            GameplayScreen gameplay = null;
            GameplayHud hud = null;
            GameplayPlayfield playfield = null;

            AddStep("open gameplay", () =>
                screenStack.Push(gameplay = new GameplayScreen(
                    DemoBeatmaps.CreateFourKeyDemo())));
            AddUntilStep("gameplay presentation is loaded", () =>
            {
                hud = gameplay?.ChildrenOfType<GameplayHud>()
                               .SingleOrDefault();
                playfield = gameplay?.ChildrenOfType<GameplayPlayfield>()
                                     .SingleOrDefault();
                return hud?.IsLoaded == true && playfield?.IsLoaded == true;
            });
            AddStep("plain Tab does not toggle focus mode", () =>
                gameplay.HandleKeyDownInput(
                    Key.Tab,
                    false,
                    false,
                    false));
            AddAssert("focus mode remains off", () =>
                !gameplay.FocusModeActive);
            AddStep("press Shift Tab", () =>
                gameplay.HandleKeyDownInput(
                    Key.Tab,
                    false,
                    false,
                    false,
                    true));
            AddUntilStep("informational presentation is hidden", () =>
                gameplay.FocusModeActive
                && hud.Alpha == 0
                && gameplay.ChildrenOfType<GameplayComboReadout>()
                           .Single().Alpha == 0
                && gameplay.ChildrenOfType<JudgementReadout>()
                           .Single().Alpha == 0
                && gameplay.ChildrenOfType<GameplayTimingBar>()
                           .Single().Alpha == 0
                && gameplay.ChildrenOfType<GameplayScrollSpeedOverlay>()
                           .Single().Alpha == 0
                && gameplay.ChildrenOfType<GameplayPlaybackRateOverlay>()
                           .Single().Alpha == 0
                && playfield.Alpha > 0);
            AddStep("press Shift Tab again", () =>
                gameplay.HandleKeyDownInput(
                    Key.Tab,
                    false,
                    false,
                    false,
                    true));
            AddUntilStep("normal presentation returns", () =>
                !gameplay.FocusModeActive && hud.Alpha > 0);
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
            AddUntilStep("legacy column start positions the stage", () =>
            {
                GameplayPlayfield playfield =
                    gameplay?
                        .ChildrenOfType<GameplayPlayfield>()
                        .SingleOrDefault();
                return playfield != null
                       && playfield.Anchor == Anchor.BottomLeft
                       && playfield.Origin == Anchor.BottomLeft
                       && Math.Abs(
                           playfield.X / playfield.Scale.X
                           - 136) < 0.01f;
            });
            AddUntilStep("skin sprites loaded", () =>
                gameplay?.ChildrenOfType<Sprite>().Any(sprite => sprite.Texture != null) == true);
            AddAssert("skin owns judgement feedback", () =>
                gameplay.ChildrenOfType<GameplayPlayfield>()
                        .Single()
                        .UsesSkinJudgementOverlay);
        }

        [Test]
        public void TestLegacyColumnRightFitsOversizedStage()
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
            AddUntilStep("ColumnRight fit is applied", () =>
            {
                GameplayPlayfield playfield = gameplay?
                                              .ChildrenOfType<GameplayPlayfield>()
                                              .SingleOrDefault();
                if (playfield == null
                    || gameplay.DrawWidth <= 0
                    || playfield.Scale.Y <= 0)
                    return false;

                float rightMargin =
                    50 * playfield.Scale.Y;
                float rightEdge =
                    playfield.X
                    + playfield.Width * playfield.Scale.X;
                return playfield.Scale.X < playfield.Scale.Y
                       && rightEdge + rightMargin
                          <= gameplay.DrawWidth + 0.01f;
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
            GameplayScreen gameplay = null;

            AddStep("enable skin hit sounds", () =>
            {
                originalKeysoundsEnabled =
                    gameplaySettings.KeysoundsEnabled.Value;
                gameplaySettings.KeysoundsEnabled.Value = true;
            });
            AddStep("open gameplay with skin hit sounds", () =>
                screenStack.Push(gameplay = new GameplayScreen(
                    DemoBeatmaps.CreateFourKeyDemo(),
                    audioEngine,
                    skinPath)));
            AddUntilStep("skin hit sound is prepared", () =>
                audioEngine.PreparedSamples.Contains(hitSoundPath));
            AddStep("leave prepared gameplay", () => gameplay.Exit());
            AddUntilStep("keysound preparation lifetime is cancelled", () =>
                audioEngine.PreparationCancellationRequested);
            AddStep("restore skin hit sound setting", () =>
                gameplaySettings.KeysoundsEnabled.Value =
                    originalKeysoundsEnabled);
        }

        [Test]
        public void TestAudioStartWaitsForLatestKeysoundPreparation()
        {
            string skinPath = createTestSkin();
            File.WriteAllBytes(
                Path.Combine(skinPath, "normal-hitnormal.wav"),
                [1, 2, 3]);
            var audioEngine = new ControlledSamplePreparationAudioEngine();
            GameplayScreen gameplay = null;
            bool originalKeysoundsEnabled = false;

            AddStep("enable controlled skin hit sounds", () =>
            {
                originalKeysoundsEnabled =
                    gameplaySettings.KeysoundsEnabled.Value;
                gameplaySettings.KeysoundsEnabled.Value = true;
            });
            AddStep("open gameplay with blocked sample preparation", () =>
                screenStack.Push(gameplay = new GameplayScreen(
                    DemoBeatmaps.CreateFourKeyDemo() with
                    {
                        AudioPath = "controlled-preparation.mp3",
                    },
                    audioEngine,
                    skinPath)));
            AddUntilStep("initial sample preparation is blocked", () =>
                audioEngine.PreparationCount == 1);
            AddStep("restart sample preparation", () =>
                gameplay.RestartKeysoundPreparationForTest());
            AddUntilStep("replacement preparation starts", () =>
                audioEngine.PreparationCount == 2);
            AddWaitStep("pass audio lead in", 70);
            AddAssert("audio waits for latest preparation", () =>
                audioEngine.StartCount == 0);
            AddStep("complete latest preparation", () =>
                audioEngine.CompleteLatestPreparation());
            AddUntilStep("audio starts after latest preparation", () =>
                audioEngine.StartCount == 1);
            AddStep("leave controlled gameplay", () => gameplay.Exit());
            AddUntilStep("controlled audio engine is disposed", () =>
                audioEngine.DisposeCount == 1);
            AddAssert("preparation exits before engine disposal", () =>
                !audioEngine.DisposedDuringPreparation);
            AddStep("restore controlled keysound setting", () =>
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
        public void TestExtremelyLongHoldBodyUsesBoundedDrawableGeometry()
        {
            string skinPath = null;
            GameplayScreen gameplay = null;
            YokkoBeatmap beatmap = createHoldDemo(KeyMode.FourKey) with
            {
                HitObjects =
                [
                    new YokkoHitObject(
                        0,
                        1600,
                        3_601_600,
                        HitObjectKind.Hold),
                ],
            };

            AddStep("create long-hold skin", () =>
                skinPath = createTestSkin("NoteBodyStyle0: 0"));
            AddStep("open extremely long hold", () =>
                screenStack.Push(gameplay = new GameplayScreen(
                    beatmap,
                    skinPath: skinPath)));
            AddUntilStep("long hold stays connected with bounded geometry", () =>
            {
                DrawableNote hold = gameplay?
                                    .ChildrenOfType<GameplayPlayfield>()
                                    .SingleOrDefault()?
                                    .GetDrawableNote(0);
                Sprite body = hold?
                              .ChildrenOfType<Sprite>()
                              .FirstOrDefault(sprite =>
                                  sprite.Parent is Container { Masking: true });

                if (hold == null || body == null)
                    return false;

                hold.UpdatePosition(
                    1600,
                    false,
                    false,
                    0,
                    460,
                    1800);

                int visibleEndpointCount = hold
                                           .ChildrenOfType<Sprite>()
                                           .Count(sprite =>
                                               !ReferenceEquals(sprite, body)
                                               && sprite.Alpha > 0);
                return hold.Height is > 400 and < 800
                       && body.Height is > 400 and < 800
                       && float.IsFinite(body.TextureRectangle.Y)
                       && float.IsFinite(body.TextureRectangle.Height)
                       && visibleEndpointCount == 1;
            });
        }

        [Test]
        public void TestAdditionalLongNoteCutPreservesBakedPercyTexture()
        {
            string skinPath = null;
            bool originalCutEnabled = false;
            double originalCutAmount = 0;

            AddStep("create baked Percy skin", () =>
            {
                originalCutEnabled = skinSettings.LongNoteCutEnabled.Value;
                originalCutAmount = skinSettings.LongNoteCutAmount.Value;
                skinSettings.LongNoteCutEnabled.Value = true;
                skinSettings.LongNoteCutAmount.Value = 0.6;
                skinPath = createTestSkin();

                using var body = new Image<Rgba32>(8, 400);
                for (int y = 320; y < body.Height; y++)
                for (int x = 0; x < body.Width; x++)
                    body[x, y] = new Rgba32(142, 136, 145, 255);
                body.SaveAsPng(Path.Combine(skinPath, "hold-body.png"));

                using var tail = new Image<Rgba32>(8, 8);
                tail.SaveAsPng(Path.Combine(skinPath, "hold-tail.png"));
            });
            AddStep("open chart with extra LN cut", () =>
                screenStack.Push(new GameplayScreen(
                    createHoldDemo(KeyMode.FourKey),
                    skinPath: skinPath)));
            AddUntilStep("baked texture and extra cut are both active", () =>
            {
                DrawableNote hold = (screenStack.CurrentScreen as Drawable)?
                                    .ChildrenOfType<GameplayPlayfield>()
                                    .SingleOrDefault()?
                                    .GetDrawableNote(0);
                Texture bodyTexture = hold?
                                      .ChildrenOfType<Sprite>()
                                      .Select(sprite => sprite.Texture)
                                      .FirstOrDefault(texture =>
                                          texture?.DisplayHeight == 400);

                return bodyTexture?.Available == true
                       && Math.Abs(
                           hold.AppliedLongNoteCutDistance - 24) < 0.01;
            });
            AddStep("restore extra LN cut", () =>
            {
                skinSettings.LongNoteCutEnabled.Value =
                    originalCutEnabled;
                skinSettings.LongNoteCutAmount.Value =
                    originalCutAmount;
            });
        }

        [Test]
        public void TestDisabledAdditionalLongNoteCutDoesNotAffectGameplay()
        {
            bool originalCutEnabled = false;
            double originalCutAmount = 0;

            AddStep("store disabled extra LN cut", () =>
            {
                originalCutEnabled = skinSettings.LongNoteCutEnabled.Value;
                originalCutAmount = skinSettings.LongNoteCutAmount.Value;
                skinSettings.LongNoteCutEnabled.Value = false;
                skinSettings.LongNoteCutAmount.Value = 1.4;
            });
            AddStep("open hold chart", () =>
                screenStack.Push(new GameplayScreen(
                    createHoldDemo(KeyMode.FourKey))));
            AddUntilStep("disabled cut keeps original geometry", () =>
            {
                DrawableNote hold = (screenStack.CurrentScreen as Drawable)?
                                    .ChildrenOfType<GameplayPlayfield>()
                                    .SingleOrDefault()?
                                    .GetDrawableNote(0);

                return hold != null
                       && Math.Abs(hold.AppliedLongNoteCutDistance) < 0.01;
            });
            AddStep("restore extra LN cut", () =>
            {
                skinSettings.LongNoteCutEnabled.Value =
                    originalCutEnabled;
                skinSettings.LongNoteCutAmount.Value =
                    originalCutAmount;
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
                           body.TextureRectangle.Width
                           - body.Width) < 0.01f
                       && Math.Abs(
                           body.TextureRectangle.Height
                           - body.Texture.DisplayHeight
                           * body.Width
                           / body.Texture.DisplayWidth) < 0.01f
                       && body.Texture.WrapModeT == WrapMode.Repeat;
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
                            Sprite body = bodyClip?
                                .ChildrenOfType<Sprite>()
                                .SingleOrDefault();
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

                            if (body?.TextureRelativeSizeAxes == Axes.None
                                && (Math.Abs(
                                        body.TextureRectangle.X) >= 0.01f
                                    || Math.Abs(
                                        body.TextureRectangle.Width
                                        - body.Width) >= 0.01f))
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
            AddAssert("burst hugs the left stage edge", () =>
            {
                // The fixture texture is 120px wide, displayed at 75 units.
                // The burst must start off-stage left and rest with only a
                // small overlap onto the stage, never sweeping across it.
                const float spriteWidth = 120
                                          / OsuManiaSkinConfiguration
                                              .LegacyPositionScaleFactor;
                return playfield.LastComboBurstStartX < 0
                       && playfield.LastComboBurstRestX >= 0
                       && playfield.LastComboBurstRestX
                       <= spriteWidth * 0.25f;
            });
            AddStep("remove combo burst fixture", () =>
            {
                playfield.Expire();
                skin.Dispose();
            });
        }

        [Test]
        public void TestLegacyManiaComboBurstDisabledBySetting()
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
                "Combo burst disabled fixture",
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

            AddStep("load combo burst skin with bursts disabled", () =>
            {
                skin = OsuManiaSkin.Load(
                    skinPath,
                    4,
                    renderer);
                Add(playfield = new GameplayPlayfield(
                    beatmap,
                    KeyModeBindings.ForMode(KeyMode.FourKey),
                    skin,
                    showComboBursts: false));
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
            AddAssert("no combo burst is emitted", () =>
                state.Combo == 100
                && playfield.ComboBurstCount == 0);
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
        public void TestUpscrollSettingFlipsArrowElements()
        {
            string skinPath = createTestSkin("""
UpsideDown: 0
HitPosition: 400
""");
            ManiaScrollDirection originalDirection =
                ManiaScrollDirection.Downscroll;
            GameplayPlayfield upscrollPlayfield = null;

            AddStep("select upscroll", () =>
            {
                originalDirection =
                    gameplaySettings.ScrollDirection.Value;
                gameplaySettings.ScrollDirection.Value =
                    ManiaScrollDirection.Upscroll;
            });
            AddStep("open skin in upscroll", () =>
                screenStack.Push(new GameplayScreen(
                    DemoBeatmaps.CreateFourKeyDemo(),
                    skinPath: skinPath)));
            AddUntilStep("upscroll geometry applied", () =>
            {
                upscrollPlayfield = (screenStack.CurrentScreen as Drawable)?
                                    .ChildrenOfType<GameplayPlayfield>()
                                    .SingleOrDefault();
                return upscrollPlayfield?.ScrollOrigin == 480
                       && upscrollPlayfield.JudgementPosition == 80;
            });
            double upscrollBaseApproachTime = 0;
            AddStep("move upscroll judgement region down", () =>
            {
                upscrollBaseApproachTime =
                    upscrollPlayfield.ApproachTimeMilliseconds;
                gameplaySettings.LayoutJudgementLineOffsetY.Value =
                    40.0 / 480;
                upscrollPlayfield.SetJudgementLineOffset(40.0 / 480);
            });
            AddAssert("upscroll geometry and speed stay aligned", () =>
                Math.Abs(upscrollPlayfield.JudgementPosition - 120) < 0.1
                && upscrollPlayfield.JudgementRegionAlignedForTest
                && Math.Abs(
                    upscrollPlayfield.ApproachTimeMilliseconds
                    - upscrollBaseApproachTime * 360 / 400) < 0.1);
            AddStep("restore upscroll judgement region", () =>
            {
                gameplaySettings.LayoutJudgementLineOffsetY.Value = 0;
                upscrollPlayfield.SetJudgementLineOffset(0);
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
                       && timingBar.Y == 8;
            });
            AddStep("restore scroll direction", () =>
                gameplaySettings.ScrollDirection.Value =
                    originalDirection);
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
                && timingBar.Y == 8);
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
            double skinBaseApproachTime = 0;
            AddStep("move skin judgement region", () =>
            {
                skinBaseApproachTime = playfield.ApproachTimeMilliseconds;
                gameplaySettings.LayoutJudgementLineOffsetY.Value =
                    -60.0 / 480;
                playfield.SetJudgementLineOffset(-60.0 / 480);
            });
            AddAssert("skin line receptor and speed remain aligned", () =>
                Math.Abs(playfield.JudgementPosition - 400) < 0.1
                && playfield.JudgementRegionAlignedForTest
                && Math.Abs(
                    playfield.ApproachTimeMilliseconds
                    - skinBaseApproachTime * 400 / 460) < 0.1);
            AddStep("restore skin judgement region", () =>
            {
                gameplaySettings.LayoutJudgementLineOffsetY.Value = 0;
                playfield.SetJudgementLineOffset(0);
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
            AddStep("hide all lane hit effects", () =>
            {
                gameplaySettings.LayoutHitEffectsVisible.Value = 0;
                playfield.SetHitEffectsVisible(false);
            });
            AddAssert("hit effects reject new emissions immediately", () =>
                !playfield.HitEffectsVisibleForTest);
            AddAssert("hit effect layers hide immediately", () =>
                playfield.HitEffectLayersHiddenForTest);
            AddAssert("active lane lights are cleared", () =>
                playfield.ChildrenOfType<TextureAnimation>()
                         .Where(animation => animation.Name == "Lane light")
                         .All(animation => animation.Alpha == 0));
            AddStep("trigger feedback while effects are hidden", () =>
            {
                playfield.SetLanePressed(0, true);
                playfield.ApplyJudgement(new JudgementEvent(
                    0,
                    0,
                    1000,
                    1000,
                    0,
                    JudgementRating.Perfect));
            });
            AddAssert("future hit effects stay hidden", () =>
                playfield.HitEffectsHiddenForTest
                && playfield.ChildrenOfType<TextureAnimation>()
                            .Where(animation => animation.Name == "Lane light")
                            .All(animation => animation.Alpha == 0));
            AddStep("restore lane hit effects", () =>
            {
                gameplaySettings.LayoutHitEffectsVisible.Value = 1;
                playfield.SetHitEffectsVisible(true);
            });
            AddAssert("restored effects resume immediately", () =>
                playfield.HitEffectsVisibleForTest
                && playfield.ChildrenOfType<TextureAnimation>()
                            .Any(animation =>
                                animation.Name == "Lane light"
                                && animation.Alpha > 0.99f));
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
            AddStep("show stable hold result", () =>
            {
                foreach (LaneColumn lane in
                         playfield.ChildrenOfType<LaneColumn>())
                {
                    lane.ClearTransientFeedback();
                }
                playfield.ApplyJudgement(new JudgementEvent(
                    0,
                    0,
                    1000,
                    1000,
                    0,
                    JudgementRating.Perfect,
                    JudgementPhase.Hold));
            });
            AddAssert("stable hold result adds release explosion", () =>
                playfield.ChildrenOfType<TextureAnimation>()
                         .Any(animation =>
                             animation.Name == "Hit explosion"
                             && animation.Alpha > 0));
            AddStep("release first lane", () =>
                playfield.SetLanePressed(0, false));
            AddStep("restore scroll speed", () =>
                gameplaySettings.SetScrollSpeed(originalSpeed));
        }

        [Test]
        public void TestOsuManiaScrollSpeedShortcuts()
        {
            double originalSpeed = OsuManiaScrollSpeed.Default;
            ScrollSpeedAdjustmentMode originalAdjustmentMode =
                ScrollSpeedAdjustmentMode.OsuManiaScale;
            Key originalDecreaseKey = Key.F3;
            Key originalIncreaseKey = Key.F4;
            GameplayScreen gameplayScreen = null;
            GameplayScrollSpeedOverlay speedOverlay = null;

            AddStep("save and open fresh gameplay", () =>
            {
                originalSpeed = gameplaySettings.ScrollSpeed.Value;
                originalAdjustmentMode =
                    gameplaySettings.ScrollSpeedAdjustmentMode.Value;
                originalDecreaseKey =
                    gameplaySettings.DecreaseScrollSpeedKey.Value;
                originalIncreaseKey =
                    gameplaySettings.IncreaseScrollSpeedKey.Value;
                gameplaySettings.ResetShortcutBindings();
                gameplaySettings.SetScrollSpeed(8);
                gameplaySettings.ScrollSpeedAdjustmentMode.Value =
                    ScrollSpeedAdjustmentMode.OsuManiaScale;
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
            AddStep("ctrl plus uses original scale", () =>
                gameplayScreen.HandleScrollSpeedShortcut(
                    Key.Plus,
                    true));
            AddAssert("original scale reaches speed 9", () =>
                gameplaySettings.ScrollSpeed.Value == 9);
            AddAssert("speed overlay keeps original scale display", () =>
                speedOverlay.DisplayedSpeed == 9
                && speedOverlay.DisplayedTimeRangeMilliseconds
                   == (int)Math.Round(
                       OsuManiaScrollSpeed.ComputeScrollTime(9))
                && !speedOverlay.IsLocked
                && speedOverlay.DisplayedLabel == "SCROLL SPEED"
                && speedOverlay.DisplayedDetail
                   == $"{(int)Math.Round(OsuManiaScrollSpeed.ComputeScrollTime(9))} ms"
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
                    OsuManiaScrollSpeed.ComputeScrollTime(
                        gameplaySettings.ScrollSpeed.Value)) < 0.001);
            AddStep("ctrl minus restores original scale", () =>
                gameplayScreen.HandleScrollSpeedShortcut(
                    Key.Minus,
                    true));
            AddAssert("original scale returns to 8", () =>
                gameplaySettings.ScrollSpeed.Value == 8);
            AddStep("switch to advanced millisecond mode", () =>
                gameplaySettings.ScrollSpeedAdjustmentMode.Value =
                    ScrollSpeedAdjustmentMode.Milliseconds);
            AddStep("F4 shortens by one millisecond", () =>
                gameplayScreen.HandleScrollSpeedShortcut(
                    Key.F4,
                    false));
            AddAssert("F4 shortens to 1435 ms", () =>
                Math.Round(OsuManiaScrollSpeed.ComputeScrollTime(
                    gameplaySettings.ScrollSpeed.Value)) == 1435);
            AddAssert("advanced overlay leads with milliseconds", () =>
                speedOverlay.DisplayedTimeRangeMilliseconds == 1435
                && speedOverlay.DisplayedLabel == "SCROLL TIME"
                && speedOverlay.DisplayedDetail
                   == $"ms · {gameplaySettings.ScrollSpeed.Value:0.000}");
            AddStep("F3 restores 1436 ms", () =>
                gameplayScreen.HandleScrollSpeedShortcut(
                    Key.F3,
                    false));
            AddAssert("F3 time is 1436 ms", () =>
                Math.Round(OsuManiaScrollSpeed.ComputeScrollTime(
                    gameplaySettings.ScrollSpeed.Value)) == 1436);
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
            AddAssert("old key keeps 1436 ms", () =>
                Math.Round(OsuManiaScrollSpeed.ComputeScrollTime(
                    gameplaySettings.ScrollSpeed.Value)) == 1436);
            AddStep("custom F8 increases speed", () =>
                gameplayScreen.HandleScrollSpeedShortcut(
                    Key.F8,
                    false));
            AddAssert("custom increase reaches 1435 ms", () =>
                Math.Round(OsuManiaScrollSpeed.ComputeScrollTime(
                    gameplaySettings.ScrollSpeed.Value)) == 1435);
            AddStep("custom F7 decreases speed", () =>
                gameplayScreen.HandleScrollSpeedShortcut(
                    Key.F7,
                    false));
            AddAssert("custom decrease restores 1436 ms", () =>
                Math.Round(OsuManiaScrollSpeed.ComputeScrollTime(
                    gameplaySettings.ScrollSpeed.Value)) == 1436);
            AddStep("attempt late gameplay adjustment", () =>
                gameplayScreen.HandleScrollSpeedShortcut(
                    Key.F8,
                    false,
                    11000));
            AddAssert("late adjustment is locked", () =>
                Math.Round(OsuManiaScrollSpeed.ComputeScrollTime(
                    gameplaySettings.ScrollSpeed.Value)) == 1436
                && speedOverlay.IsLocked
                && speedOverlay.DisplayedSpeed
                   == gameplaySettings.ScrollSpeed.Value
                && speedOverlay.DisplayedLabel == "SPEED LOCKED"
                && speedOverlay.DisplayedDetail == "INTRO / BREAK");
            AddUntilStep("speed overlay exits smoothly", () =>
                speedOverlay.Alpha <= 0.01f);
            AddStep("restore scroll speed and shortcuts", () =>
            {
                gameplaySettings.SetScrollSpeed(originalSpeed);
                gameplaySettings.ScrollSpeedAdjustmentMode.Value =
                    originalAdjustmentMode;
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
            ManiaDifficultyRatingMode originalDifficultyMode =
                ManiaDifficultyRatingMode.EtternaMsd;
            var audioEngine = new RateTrackingAudioEngine();
            YokkoBeatmap beatmap =
                DemoBeatmaps.CreateFourKeyDemo() with
                {
                    AudioPath = "rate-adjustment-fixture.mp3",
                };
            GameplayScreen gameplayScreen = null;
            GameplayPlaybackRateOverlay rateOverlay = null;
            GameplayHud hud = null;
            ManiaMsdResult expectedDifficulty =
                ManiaMsdCalculator.CalculateResult(
                    beatmap,
                    1.05);

            AddStep("open rate-adjustable gameplay", () =>
            {
                originalDifficultyMode =
                    gameplaySettings.DifficultyRatingMode.Value;
                gameplaySettings.DifficultyRatingMode.Value =
                    ManiaDifficultyRatingMode.EtternaMsd;
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
                && rateOverlay.DisplayedDetail.Contains("MSD")
                && rateOverlay.Alpha > 0);
            AddUntilStep("hud keeps live rate stats visible", () =>
                hud.DisplayedDynamicRate.Contains("LIVE RATE 1.05×")
                && hud.DisplayedDynamicRate.Contains("126 BPM")
                && hud.DisplayedDynamicRate.Contains("MSD")
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
            AddStep("switch live readout to Rebirth stars", () =>
            {
                gameplaySettings.DifficultyRatingMode.Value =
                    ManiaDifficultyRatingMode.RebirthStars;
                gameplayScreen.HandlePlaybackRateShortcut(
                    Key.Plus,
                    true);
            });
            AddUntilStep("gameplay readouts follow star mode", () =>
                rateOverlay.DisplayedDetail.Contains("STAR")
                && hud.DisplayedDynamicRate.Contains("STAR"));
            AddStep("restore difficulty display mode", () =>
                gameplaySettings.DifficultyRatingMode.Value =
                    originalDifficultyMode);
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
            AddUntilStep("practice result appears", () =>
                gameplay
                    ?.ChildrenOfType<GameplayResultOverlay>()
                     .SingleOrDefault() != null);
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
        public void TestPreloadedGameplayWaitsForScreenEntryBeforeStarting()
        {
            var audioEngine = new SeekTrackingAudioEngine();
            GameplaySessionScreen session = null;
            GameplayScreen gameplay = null;

            AddStep("preload gameplay session", () =>
                LoadComponent(session = new GameplaySessionScreen(
                    gameplay = new GameplayScreen(
                        DemoBeatmaps.CreateFourKeyDemo() with
                        {
                            AudioPath = "preload-fixture.mp3",
                        },
                        audioEngine))));
            AddAssert("gameplay is preloaded", () =>
                session.InitialGameplayPreloaded);
            AddAssert("preload does not start audio", () =>
                audioEngine.StartCount == 0);
            AddStep("enter preloaded gameplay", () =>
                screenStack.Push(session));
            AddUntilStep("preloaded gameplay becomes current", () =>
                ReferenceEquals(session.CurrentGameplay, gameplay));
            AddUntilStep("audio starts after entry lead-in", () =>
                audioEngine.StartCount == 1);
            AddStep("exit gameplay", () => gameplay.Exit());
            AddUntilStep("gameplay session exits", () =>
                !ReferenceEquals(screenStack.CurrentScreen, session));
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
                && ReferenceEquals(session.CurrentGameplay, gameplay)
                && session.RetryTransitionActive);
            AddUntilStep("retry status is immediately visible", () =>
                session.RetryStatusVisible);
            AddUntilStep("replacement preloads during audio release", () =>
                session.PendingReplacementLoaded
                && ReferenceEquals(session.CurrentGameplay, gameplay));
            AddStep("release old audio session", () =>
                audioEngine.StopCompletion.SetResult(true));
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
        public void TestInitialGameplaySessionRevealsOnlyAfterGameplayIsReady()
        {
            GameplaySessionScreen session = null;
            GameplayScreen gameplay = null;

            AddStep("open gameplay session", () =>
                screenStack.Push(session = new GameplaySessionScreen(
                    gameplay = new GameplayScreen(
                        DemoBeatmaps.CreateFourKeyDemo()))));
            AddUntilStep("loaded gameplay begins reveal", () =>
                ReferenceEquals(screenStack.CurrentScreen, session)
                && ReferenceEquals(session?.CurrentGameplay, gameplay)
                && gameplay?.IsLoaded == true
                && session.InitialRevealStarted);
            AddUntilStep("gameplay reveal completes", () =>
                session?.InitialRevealAnimationComplete == true);
            AddStep("exit gameplay", () => gameplay.Exit());
            AddUntilStep("gameplay session exits", () =>
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
            bool originalCountdownEnabled = true;
            double originalCountdownDuration = 0;
            double originalMasterVolume = 1;
            double originalBackgroundDim = 0.5;

            AddStep("open gameplay with audio", () =>
            {
                originalPauseKey = gameplaySettings.PauseOrBackKey.Value;
                originalMenuNextKey = gameplaySettings.MenuNextKey.Value;
                originalCountdownEnabled =
                    gameplaySettings.ResumeCountdownEnabled.Value;
                originalCountdownDuration =
                    gameplaySettings.ResumeCountdownMilliseconds.Value;
                originalMasterVolume = audioSettings.MasterVolume.Value;
                originalBackgroundDim = gameplaySettings.BackgroundDim.Value;
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
                return overlay?.ActionCount == 5
                       && overlay.SelectedAction == 0
                       && overlay.DisplayedScore == 0
                       && Math.Abs(overlay.DisplayedAccuracy - 1) < 0.0001
                       && overlay.DisplayedCombo == 0
                       && overlay.DisplayedMaxCombo == 0
                       && overlay.DisplayedPauseCount == 1
                       && !string.IsNullOrWhiteSpace(
                           overlay.DisplayedRank)
                       && GameplayPauseOverlay.ReferenceSize
                          == new Vector2(1600, 900);
            });
            AddStep("open pause settings", () =>
            {
                gameplaySettings.ResumeCountdownEnabled.Value = true;
                gameplaySettings.ResumeCountdownMilliseconds.Value = 1000;
                audioSettings.MasterVolume.Value = 1;
                gameplaySettings.BackgroundDim.Value = 0.5;
                gameplayScreen
                    .ChildrenOfType<GameplayPauseOverlay>()
                    .Single()
                    .TogglePauseSettings();
            });
            AddAssert("pause countdown setting is expanded", () =>
                gameplayScreen
                    .ChildrenOfType<GameplayPauseOverlay>()
                    .Single()
                    .PauseSettingsExpanded);
            AddStep("retry shortcut is contained by pause settings", () =>
                gameplayScreen
                    .ChildrenOfType<GameplayPauseOverlay>()
                    .Single()
                    .HandleKey(gameplaySettings.GetShortcutBinding(
                        ManiaShortcutAction.Retry)));
            AddAssert("pause settings remains open after retry shortcut", () =>
                gameplayScreen.IsPaused
                && gameplayScreen
                    .ChildrenOfType<GameplayPauseOverlay>()
                    .Single()
                    .PauseSettingsExpanded);
            AddStep("confirm closes pause settings only", () =>
                gameplayScreen
                    .ChildrenOfType<GameplayPauseOverlay>()
                    .Single()
                    .HandleKey(gameplaySettings.GetShortcutBinding(
                        ManiaShortcutAction.Confirm)));
            AddAssert("confirm does not resume gameplay", () =>
                gameplayScreen.IsPaused
                && !gameplayScreen
                    .ChildrenOfType<GameplayPauseOverlay>()
                    .Single()
                    .PauseSettingsExpanded);
            AddStep("reopen pause settings", () =>
                gameplayScreen
                    .ChildrenOfType<GameplayPauseOverlay>()
                    .Single()
                    .TogglePauseSettings());
            AddStep("increase pause countdown", () =>
                gameplayScreen
                    .ChildrenOfType<GameplayPauseOverlay>()
                    .Single()
                    .AdjustResumeCountdown(1));
            AddAssert("pause countdown updates live", () =>
            {
                GameplayPauseOverlay overlay = gameplayScreen
                                               .ChildrenOfType<GameplayPauseOverlay>()
                                               .Single();
                return overlay.ResumeCountdownEnabled
                       && Math.Abs(overlay.ResumeCountdownMilliseconds - 2000)
                          < 0.001;
            });
            AddStep("adjust pause volume and background dim", () =>
            {
                GameplayPauseOverlay overlay = gameplayScreen
                                               .ChildrenOfType<GameplayPauseOverlay>()
                                               .Single();
                overlay.AdjustPauseVolume(-1);
                overlay.AdjustBackgroundDim(1);
            });
            AddAssert("pause quick settings update live", () =>
            {
                GameplayPauseOverlay overlay = gameplayScreen
                                               .ChildrenOfType<GameplayPauseOverlay>()
                                               .Single();
                return Math.Abs(overlay.MasterVolume - 0.95) < 0.001
                       && Math.Abs(overlay.BackgroundDim - 0.55) < 0.001
                       && Math.Abs(audioSettings.MasterVolume.Value - 0.95) < 0.001
                       && Math.Abs(gameplaySettings.BackgroundDim.Value - 0.55) < 0.001;
            });
            AddStep("close pause settings and restore value", () =>
            {
                GameplayPauseOverlay overlay = gameplayScreen
                                               .ChildrenOfType<GameplayPauseOverlay>()
                                               .Single();
                overlay.TogglePauseSettings();
                gameplaySettings.ResumeCountdownEnabled.Value =
                    originalCountdownEnabled;
                gameplaySettings.ResumeCountdownMilliseconds.Value =
                    originalCountdownDuration;
                audioSettings.MasterVolume.Value = originalMasterVolume;
                gameplaySettings.BackgroundDim.Value = originalBackgroundDim;
            });
            AddAssert("pause settings closes", () =>
                !gameplayScreen
                    .ChildrenOfType<GameplayPauseOverlay>()
                    .Single()
                    .PauseSettingsExpanded);
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
        public void TestSkinSelectionUpdatesExistingPausedGameplay()
        {
            GameplayScreen gameplay = null;
            SettingsScreen settings = null;
            SkinImportResult importedSkin = null;
            string originalSkinId = string.Empty;

            AddStep("open gameplay with built-in skin", () =>
            {
                originalSkinId = skinSettings.SelectedSkinId.Value;
                skinSettings.SelectedSkinId.Value = string.Empty;
                screenStack.Push(gameplay = new GameplayScreen(
                    DemoBeatmaps.CreateFourKeyDemo()));
            });
            AddUntilStep("built-in playfield is ready", () =>
                gameplay?.ChildrenOfType<GameplayPlayfield>()
                         .Any(playfield =>
                             !playfield.UsesSkinJudgementOverlay) == true);
            AddStep("pause gameplay", () => gameplay.TogglePause());
            AddUntilStep("gameplay is paused", () =>
                gameplay.IsPaused && !gameplay.PauseTransitionInProgress);
            AddStep("open settings over paused gameplay", () =>
                gameplay.Push(settings = new SettingsScreen()));
            AddUntilStep("settings is current", () =>
                ReferenceEquals(screenStack.CurrentScreen, settings));
            AddStep("select imported skin in settings", () =>
            {
                importedSkin = skinLibrary.Import(createTestSkin());
                Assert.That(importedSkin.Success, Is.True, importedSkin.Message);
            });
            AddStep("return to paused gameplay", () => settings.Exit());
            AddUntilStep("same paused gameplay uses selected skin", () =>
                ReferenceEquals(screenStack.CurrentScreen, gameplay)
                && gameplay.IsPaused
                && gameplay.ChildrenOfType<GameplayPlayfield>()
                           .Any(playfield =>
                               playfield.UsesSkinJudgementOverlay));
            AddStep("clean up imported skin", () =>
            {
                gameplay.Exit();
                if (importedSkin?.Skin != null)
                    skinLibrary.Delete(importedSkin.Skin.Id);
                skinSettings.SelectedSkinId.Value = originalSkinId;
            });
        }

        [Test]
        public void TestKeyRepeatDoesNotRetriggerOneShotActions()
        {
            var audioEngine = new SeekTrackingAudioEngine();
            YokkoBeatmap beatmap = DemoBeatmaps.CreateFourKeyDemo() with
            {
                AudioPath = "repeat-fixture.mp3",
            };
            GameplayScreen gameplayScreen = null;
            double originalSpeed = 0;
            ScrollSpeedAdjustmentMode originalAdjustmentMode =
                ScrollSpeedAdjustmentMode.OsuManiaScale;

            AddStep("open gameplay with audio", () =>
            {
                originalSpeed = gameplaySettings.ScrollSpeed.Value;
                originalAdjustmentMode =
                    gameplaySettings.ScrollSpeedAdjustmentMode.Value;
                gameplaySettings.ResetShortcutBindings();
                gameplaySettings.SetScrollSpeed(8);
                gameplaySettings.ScrollSpeedAdjustmentMode.Value =
                    ScrollSpeedAdjustmentMode.OsuManiaScale;
                gameplayScreen = new GameplayScreen(beatmap, audioEngine);
                screenStack.Push(gameplayScreen);
            });
            AddUntilStep("audio starts", () =>
                audioEngine.StartCount == 1);
            AddStep("hold escape into auto-repeat", () =>
            {
                gameplayScreen.HandleKeyDownInput(
                    Key.Escape,
                    true,
                    false,
                    false);
                gameplayScreen.HandleKeyDownInput(
                    Key.Escape,
                    true,
                    false,
                    false);
            });
            AddAssert("escape repeat never pauses", () =>
                !gameplayScreen.IsPaused
                && !gameplayScreen.PauseTransitionInProgress
                && audioEngine.PauseCount == 0);
            AddStep("press escape once", () =>
                gameplayScreen.HandleKeyDownInput(
                    Key.Escape,
                    false,
                    false,
                    false));
            AddUntilStep("pause completes", () =>
                gameplayScreen.IsPaused
                && !gameplayScreen.PauseTransitionInProgress
                && audioEngine.PauseCount == 1);
            AddStep("escape keeps auto-repeating", () =>
            {
                gameplayScreen.HandleKeyDownInput(
                    Key.Escape,
                    true,
                    false,
                    false);
                gameplayScreen.HandleKeyDownInput(
                    Key.Escape,
                    true,
                    false,
                    false);
            });
            AddAssert("repeat never resumes", () =>
                gameplayScreen.IsPaused
                && audioEngine.SeekCount == 0);
            AddStep("press escape again", () =>
                gameplayScreen.HandleKeyDownInput(
                    Key.Escape,
                    false,
                    false,
                    false));
            AddUntilStep("resume completes", () =>
                !gameplayScreen.IsPaused
                && !gameplayScreen.PauseTransitionInProgress
                && audioEngine.SeekCount == 1);
            AddStep("hold ctrl plus into auto-repeat", () =>
            {
                gameplayScreen.HandleKeyDownInput(
                    Key.Plus,
                    true,
                    false,
                    true);
                gameplayScreen.HandleKeyDownInput(
                    Key.Plus,
                    true,
                    false,
                    true);
            });
            AddAssert("original scroll scale adjustment repeats", () =>
                gameplaySettings.ScrollSpeed.Value == 10);
            AddStep("restore scroll speed", () =>
            {
                gameplaySettings.SetScrollSpeed(originalSpeed);
                gameplaySettings.ScrollSpeedAdjustmentMode.Value =
                    originalAdjustmentMode;
            });
        }

        [Test]
        public void TestNoPauseAllowanceIsConsumed()
        {
            var audioEngine = new SeekTrackingAudioEngine();
            YokkoBeatmap beatmap = DemoBeatmaps.CreateFourKeyDemo() with
            {
                AudioPath = "no-pause-fixture.mp3",
            };
            GameplayScreen gameplayScreen = null;

            AddStep("open gameplay with one pause", () =>
            {
                gameplaySettings.ResetShortcutBindings();
                gameplayScreen = new GameplayScreen(
                    beatmap,
                    audioEngine,
                    mods: ManiaModSet.Empty.WithNoPause(1));
                gameplayScreen.ResumeCountdownMillisecondsOverride = 0;
                screenStack.Push(gameplayScreen);
            });
            AddUntilStep("audio starts", () => audioEngine.StartCount == 1);
            AddStep("use allowed pause", () => gameplayScreen.TogglePause());
            AddUntilStep("first pause completes", () =>
                gameplayScreen.IsPaused
                && gameplayScreen.PausesUsed == 1
                && gameplayScreen.PausesRemaining == 0);
            AddStep("resume", () => gameplayScreen.TogglePause());
            AddUntilStep("resume completes", () => !gameplayScreen.IsPaused);
            AddStep("try another pause", () => gameplayScreen.TogglePause());
            AddWaitStep("allow blocked request to settle", 2);
            AddAssert("second pause is blocked", () =>
                !gameplayScreen.IsPaused
                && gameplayScreen.PausesUsed == 1
                && audioEngine.PauseCount == 1);
        }

        [Test]
        public void TestResumeCountdownBuffersAndCancels()
        {
            var audioEngine = new SeekTrackingAudioEngine();
            YokkoBeatmap beatmap = DemoBeatmaps.CreateFourKeyDemo() with
            {
                AudioPath = "resume-countdown-fixture.mp3",
            };
            GameplayScreen gameplayScreen = null;

            AddStep("open gameplay with audio", () =>
            {
                gameplaySettings.ResetShortcutBindings();
                gameplayScreen = new GameplayScreen(beatmap, audioEngine);
                screenStack.Push(gameplayScreen);
                gameplayScreen.ResumeCountdownMillisecondsOverride = 400;
            });
            AddUntilStep("audio starts", () =>
                audioEngine.StartCount == 1);
            AddStep("pause gameplay", () =>
                gameplayScreen.TogglePause());
            AddUntilStep("pause completes", () =>
                gameplayScreen.IsPaused
                && !gameplayScreen.PauseTransitionInProgress
                && audioEngine.PauseCount == 1);
            AddStep("request resume", () =>
                gameplayScreen.TogglePause());
            AddAssert("countdown buffers the resume", () =>
                gameplayScreen.ResumeCountdownInProgress
                && gameplayScreen.IsPaused
                && audioEngine.SeekCount == 0);
            AddStep("cancel resume with escape", () =>
                gameplayScreen.HandleKeyDownInput(
                    Key.Escape,
                    false,
                    false,
                    false));
            AddAssert("cancel returns to the pause menu", () =>
                !gameplayScreen.ResumeCountdownInProgress
                && gameplayScreen.IsPaused
                && gameplayScreen
                    .ChildrenOfType<GameplayPauseOverlay>()
                    .Any()
                && audioEngine.SeekCount == 0);
            AddStep("resume for real", () =>
                gameplayScreen.TogglePause());
            AddUntilStep("countdown completes and resumes", () =>
                !gameplayScreen.ResumeCountdownInProgress
                && !gameplayScreen.IsPaused
                && audioEngine.SeekCount == 1);
            AddStep("restore countdown", () =>
                gameplayScreen.ResumeCountdownMillisecondsOverride = null);
        }

        [Test]
        public void TestResumeCountdownCanBeDisabled()
        {
            var audioEngine = new SeekTrackingAudioEngine();
            YokkoBeatmap beatmap = DemoBeatmaps.CreateFourKeyDemo() with
            {
                AudioPath = "resume-countdown-off-fixture.mp3",
            };
            GameplayScreen gameplayScreen = null;
            bool originalEnabled = true;

            AddStep("open gameplay with countdown disabled", () =>
            {
                gameplaySettings.ResetShortcutBindings();
                originalEnabled =
                    gameplaySettings.ResumeCountdownEnabled.Value;
                gameplaySettings.ResumeCountdownEnabled.Value = false;
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
            AddStep("request resume", () =>
                gameplayScreen.TogglePause());
            AddUntilStep("resume is immediate", () =>
                !gameplayScreen.IsPaused
                && audioEngine.SeekCount == 1);
            AddAssert("countdown never engaged", () =>
                !gameplayScreen.ResumeCountdownInProgress
                && gameplayScreen
                    .ChildrenOfType<GameplayResumeCountdown>()
                    .Any() == false);
            AddStep("restore countdown setting", () =>
                gameplaySettings.ResumeCountdownEnabled.Value =
                    originalEnabled);
        }

        [Test]
        public void TestQuickRetryRequiresShortHoldToPreventAccidents()
        {
            var audioEngine = new SeekTrackingAudioEngine();
            YokkoBeatmap beatmap = DemoBeatmaps.CreateFourKeyDemo() with
            {
                AudioPath = "quick-retry-fixture.mp3",
            };
            GameplayScreen gameplayScreen = null;
            double originalHold = 0;

            AddStep("open gameplay with audio", () =>
            {
                gameplaySettings.ResetShortcutBindings();
                gameplayScreen = new GameplayScreen(beatmap, audioEngine);
                screenStack.Push(gameplayScreen);
                originalHold = gameplayScreen.QuickRetryHoldMilliseconds;
            });
            AddUntilStep("audio starts", () =>
                audioEngine.StartCount == 1);
            AddStep("tap quick retry", () =>
            {
                gameplayScreen.QuickRetryHoldMilliseconds = 10000;
                gameplayScreen.HandleKeyDownInput(
                    Key.Tilde,
                    false,
                    false,
                    false);
                gameplayScreen.HandleKeyUpInput(Key.Tilde);
            });
            AddAssert("tap does not retry", () =>
                audioEngine.StopCount == 0
                && !gameplayScreen.QuickRetryHoldActive);
            AddStep("hold quick retry", () =>
            {
                gameplayScreen.QuickRetryHoldMilliseconds = 30;
                gameplayScreen.HandleKeyDownInput(
                    Key.Tilde,
                    false,
                    false,
                    false);
            });
            AddUntilStep("short hold retries", () =>
                audioEngine.StopCount == 1);
            AddStep("restore hold duration", () =>
                gameplayScreen.QuickRetryHoldMilliseconds = originalHold);
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
            AddStep("press lane before disabled focus loss", () =>
                gameplayScreen.HandleKeyDownInput(
                    Key.D,
                    false,
                    false,
                    false));
            AddAssert("lane starts pressed", () =>
                gameplayScreen.IsLanePressed(0));
            AddStep("deactivate host while disabled", () =>
                gameplayScreen.HandleHostDeactivated());
            AddAssert("disabled setting keeps gameplay running", () =>
                !gameplayScreen.IsPaused
                && audioEngine.PauseCount == 0);
            AddAssert("disabled focus loss safely releases lane", () =>
                !gameplayScreen.IsLanePressed(0));
            AddStep("first press after focus reset is accepted", () =>
                gameplayScreen.HandleKeyDownInput(
                    Key.D,
                    false,
                    false,
                    false));
            AddAssert("lane presses after focus reset", () =>
                gameplayScreen.IsLanePressed(0));
            AddStep("release lane after focus reset", () =>
                gameplayScreen.HandleKeyUpInput(Key.D));
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

            public TaskCompletionSource<bool> SeekCompletion { get; init; }

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
                return SeekCompletion == null
                    ? ValueTask.CompletedTask
                    : new ValueTask(SeekCompletion.Task);
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

            public virtual ValueTask DisposeAsync() =>
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

        private sealed class CompletionTrackingAudioEngine
            : SeekTrackingAudioEngine, IAudioMixControl
        {
            private readonly List<double> musicVolumeHistory = new();
            private readonly List<double> hitSoundVolumeHistory = new();

            public double MusicVolume { get; private set; } = 1;

            public double HitSoundVolume { get; private set; } = 1;

            public double MetronomeVolume { get; private set; }

            public IReadOnlyList<double> MusicVolumeHistory =>
                musicVolumeHistory;

            public IReadOnlyList<double> HitSoundVolumeHistory =>
                hitSoundVolumeHistory;

            public void SetMixVolumes(
                double musicVolume,
                double hitSoundVolume,
                double metronomeVolume)
            {
                MusicVolume = musicVolume;
                HitSoundVolume = hitSoundVolume;
                MetronomeVolume = metronomeVolume;
                musicVolumeHistory.Add(musicVolume);
                hitSoundVolumeHistory.Add(hitSoundVolume);
            }

            public bool TriggerMetronome() => false;
        }

        private sealed class RateTrackingAudioEngine
            : SeekTrackingAudioEngine, IAudioRateControl
        {
            public double PlaybackRate { get; private set; } = 1;

            public void SetPlaybackRate(double playbackRate) =>
                PlaybackRate = playbackRate;
        }

        private sealed class SampleTrackingAudioEngine
            : SeekTrackingAudioEngine, IAudioLoopingSamplePlayback
        {
            private readonly HashSet<uint> activeLoops = [];
            private uint nextLoopId;

            public HashSet<string> PreparedSamples { get; } =
                new(StringComparer.OrdinalIgnoreCase);

            public int ActiveLoopCount => activeLoops.Count;

            public CancellationToken LastPreparationToken { get; private set; }

            public bool PreparationCancellationRequested =>
                LastPreparationToken.IsCancellationRequested;

            public ValueTask PrepareSamplesAsync(
                IReadOnlyCollection<string> samplePaths,
                CancellationToken cancellationToken = default)
            {
                LastPreparationToken = cancellationToken;
                PreparedSamples.UnionWith(samplePaths);
                return ValueTask.CompletedTask;
            }

            public bool TriggerSample(string samplePath) => true;

            public bool TriggerSample(string samplePath, double gain) => true;

            public uint StartLoopingSample(string samplePath, double gain)
            {
                uint loopId = ++nextLoopId;
                activeLoops.Add(loopId);
                return loopId;
            }

            public bool StopLoopingSample(uint loopId) =>
                activeLoops.Remove(loopId);
        }

        private sealed class ControlledSamplePreparationAudioEngine
            : SeekTrackingAudioEngine, IAudioSamplePlayback
        {
            private readonly object preparationLock = new();
            private readonly List<TaskCompletionSource<bool>> preparations = [];
            private int activePreparations;

            public int PreparationCount
            {
                get
                {
                    lock (preparationLock)
                        return preparations.Count;
                }
            }

            public int DisposeCount { get; private set; }

            public bool DisposedDuringPreparation { get; private set; }

            public async ValueTask PrepareSamplesAsync(
                IReadOnlyCollection<string> samplePaths,
                CancellationToken cancellationToken = default)
            {
                var completion = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                lock (preparationLock)
                    preparations.Add(completion);
                Interlocked.Increment(ref activePreparations);
                try
                {
                    using CancellationTokenRegistration registration =
                        cancellationToken.Register(() =>
                            completion.TrySetCanceled(cancellationToken));
                    await completion.Task.ConfigureAwait(false);
                }
                finally
                {
                    Interlocked.Decrement(ref activePreparations);
                }
            }

            public bool TriggerSample(string samplePath) => true;

            public void CompleteLatestPreparation()
            {
                TaskCompletionSource<bool> completion;
                lock (preparationLock)
                    completion = preparations[^1];
                completion.TrySetResult(true);
            }

            public override ValueTask DisposeAsync()
            {
                DisposeCount++;
                DisposedDuringPreparation =
                    Volatile.Read(ref activePreparations) > 0;
                return ValueTask.CompletedTask;
            }
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
