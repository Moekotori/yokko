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
using osu.Framework.Screens;
using osu.Framework.Testing;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Yokko.Core.Beatmaps;
using Yokko.Core.Gameplay;
using Yokko.Core.Timing;
using Yokko.Game.Importing;
using Yokko.Game.Screens.SongSelect;
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
        private void load(ImportedChartLibrary chartLibrary)
        {
            seedDemoCharts(chartLibrary);
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

        private static void seedDemoCharts(ImportedChartLibrary library)
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
            }
        }
    }
}
