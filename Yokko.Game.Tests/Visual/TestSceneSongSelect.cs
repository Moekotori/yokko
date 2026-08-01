using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osuTK;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Yokko.Core.Beatmaps;
using Yokko.Core.Gameplay;
using Yokko.Core.Mods;
using Yokko.Core.Scoring;
using Yokko.Core.Timing;
using Yokko.Game.Importing;
using Yokko.Game.Screens.SongSelect;
using Yokko.Game.Scoring;
using Yokko.Import;

namespace Yokko.Game.Tests.Visual
{
    [TestFixture]
    public partial class TestSceneSongSelect : YokkoTestScene
    {
        private ScreenStack screenStack;

        [Resolved]
        private IRenderer renderer { get; set; }

        [BackgroundDependencyLoader]
        private void load(
            ImportedChartLibrary chartLibrary,
            GameplayScoreStore scoreStore)
        {
            seedDemoCharts(chartLibrary, scoreStore);
            Add(screenStack = new ScreenStack(new SongSelectScreen()) { RelativeSizeAxes = Axes.Both });
        }

        [Test]
        public void TestSongSelectLayout()
        {
            AddUntilStep("song rows loaded", () =>
                (screenStack.CurrentScreen as Drawable)?
                .ChildrenOfType<SongSelectSongRow>()
                .Any() == true);
            AddStep("select middle row", () =>
            {
                ((SongSelectScreen)screenStack.CurrentScreen).SelectPrevious();
                ((SongSelectScreen)screenStack.CurrentScreen).SelectPrevious();
            });
            AddStep("capture screenshot", () => captureScreenshot());
        }

        [Test]
        public void TestSelectionTransitionDoesNotRebuildSongList()
        {
            int rebuildVersion = -1;
            int detailsTransitionVersion = -1;
            SongSelectEntry previousSelection = null;
            AddUntilStep("song rows loaded", () =>
                (screenStack.CurrentScreen as Drawable)?
                .ChildrenOfType<SongSelectSongRow>()
                .Any() == true);
            AddStep("remember list rebuild version", () =>
            {
                rebuildVersion = currentScreen.SongListRebuildVersion;
                detailsTransitionVersion =
                    currentScreen.DetailsTransitionVersion;
                previousSelection = currentScreen.SelectedEntry;
            });
            AddStep("select adjacent row", () =>
                currentScreen.SelectPrevious());
            AddAssert("selection changes", () =>
                !ReferenceEquals(
                    previousSelection,
                    currentScreen.SelectedEntry));
            AddAssert("details transition starts once", () =>
                currentScreen.DetailsTransitionVersion
                == detailsTransitionVersion + 1);
            AddAssert("selection keeps song list generation", () =>
                currentScreen.SongListRebuildVersion == rebuildVersion);
            AddStep("change selection rapidly", () =>
            {
                currentScreen.SelectPrevious();
                currentScreen.SelectPrevious();
            });
            AddAssert("rapid selection keeps one details paper", () =>
                currentScreen.DetailsLayerCount == 1);
            AddAssert("rapid selection keeps background covered", () =>
                currentScreen.BackgroundCoverageAlpha >= 0.99f);
            AddUntilStep("retire superseded detail layers", () =>
                currentScreen.DetailsLayerCount == 1);
            AddAssert("rapid selection still keeps list generation", () =>
                currentScreen.SongListRebuildVersion == rebuildVersion);
        }

        [Test]
        public void TestRankingUsesCompactGradeBadges()
        {
            AddAssert("ranking metrics use distinct horizontal columns", () =>
                SongSelectRankingPanel.MetricColumnRightEdges
                    == new Vector3(476, 586, 696));
            AddUntilStep("ranking badges loaded", () =>
                currentScreen.ChildrenOfType<SongSelectGradeBadge>()
                             .Count() == 7);
            AddAssert("badges stay inside row height", () =>
                currentScreen.ChildrenOfType<SongSelectGradeBadge>()
                             .All(badge =>
                                 Math.Abs(badge.Width - 36) < 0.01f
                                 && Math.Abs(badge.Height - 32) < 0.01f
                                 && Math.Abs(badge.X + 62) < 0.01f
                                 && Math.Abs(badge.BorderThickness) < 0.01f));
            AddAssert("grade labels do not use detached card surfaces", () =>
                currentScreen.ChildrenOfType<SongSelectGradeBadge>()
                             .All(badge =>
                                 !badge.ChildrenOfType<Box>().Any()));
            AddAssert("ranking keeps multiple grade states", () =>
            {
                ScoreRank[] grades = currentScreen
                                     .ChildrenOfType<SongSelectGradeBadge>()
                                     .Select(badge => badge.Grade)
                                     .ToArray();
                return grades.Contains(ScoreRank.S)
                       && grades.Contains(ScoreRank.A);
            });
            AddAssert("only current player badge is highlighted", () =>
                currentScreen.ChildrenOfType<SongSelectGradeBadge>()
                             .Count(badge => badge.Highlighted) == 1);
        }

        private SongSelectScreen currentScreen =>
            (SongSelectScreen)screenStack.CurrentScreen;

        private void captureScreenshot()
        {
            string outputPath = Environment.GetEnvironmentVariable("YOKKO_SONGSELECT_SCREENSHOT");
            if (string.IsNullOrWhiteSpace(outputPath))
                return;

            MethodInfo takeScreenshot = renderer.GetType().GetMethod(
                "TakeScreenshot",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException(
                    "The active renderer does not expose screenshot capture.");
            using var screenshot = (Image<Rgba32>)takeScreenshot.Invoke(renderer, null);
            Directory.CreateDirectory(
                Path.GetDirectoryName(outputPath)
                ?? throw new InvalidOperationException("Screenshot path has no parent directory."));
            screenshot.SaveAsPng(outputPath);
        }

        private static void seedDemoCharts(
            ImportedChartLibrary library,
            GameplayScoreStore scoreStore)
        {
            (string title, string artist, string creator, string difficulty, KeyMode mode, double bpm)[] demos =
            {
                ("Neon Pulse", "Synthion", "EchoRay", "Hyper", KeyMode.FourKey, 172),
                ("Afterimage", "Nixara", "Zero", "Insane", KeyMode.FourKey, 188),
                ("Blue Signal", "Asteria", "Yokko Team", "Hyper", KeyMode.FourKey, 178),
                ("Circuit Bloom", "Lunetia", "Mura", "Hyper", KeyMode.FourKey, 165),
                ("Parallel Hearts", "Koharu", "Rinstar", "Insane", KeyMode.SevenKey, 192),
            };

            foreach (var demo in demos)
            {
                YokkoBeatmap beatmap = (demo.mode == KeyMode.SevenKey
                    ? DemoBeatmaps.CreateSevenKeyDemo()
                    : DemoBeatmaps.CreateFourKeyDemo()) with
                {
                    Title = demo.title,
                    Artist = demo.artist,
                    Creator = demo.creator,
                    DifficultyName = demo.difficulty,
                    TimingPoints = [new YokkoTimingPoint(0, 60000 / demo.bpm)],
                };

                library.AddOrReplace(
                    new ChartImportResult(beatmap, []),
                    $"demo://song-select/{demo.title}");
                for (int attempt = 0; attempt < 7; attempt++)
                {
                    scoreStore.SaveBest(
                        beatmap,
                        ManiaModSet.Empty,
                        JudgementConfiguration.YokkoDefault,
                        new ManiaScoreResult(
                            970_000 - attempt * 25_000,
                            0.99 - attempt * 0.008,
                            600 - attempt * 40,
                            attempt < 3 ? ScoreRank.S : ScoreRank.A,
                            500 - attempt * 20,
                            80 + attempt * 10,
                            12 + attempt * 2,
                            4 + attempt,
                            attempt,
                            attempt / 2),
                        playedAt: DateTimeOffset.UtcNow.AddMinutes(-attempt));
                }
            }
        }
    }
}
