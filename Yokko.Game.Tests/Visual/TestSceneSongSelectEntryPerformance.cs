using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Screens;
using osu.Framework.Testing;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Yokko.Core.Beatmaps;
using Yokko.Core.Gameplay;
using Yokko.Core.Timing;
using Yokko.Game.Importing;
using Yokko.Game.Screens.Main;
using Yokko.Game.Screens.SongSelect;
using Yokko.Import;

namespace Yokko.Game.Tests.Visual;

[TestFixture]
public partial class TestSceneSongSelectEntryPerformance : YokkoTestScene
{
    private readonly ScreenStack screenStack;
    private readonly MainScreen mainScreen;
    private readonly List<string> artworkPaths = [];
    private string temporaryDirectory;
    private int cachedArtworkBeforeNavigation;
    private Stopwatch navigationStopwatch;
    private float stageAlphaAtNavigation;

    [Resolved]
    private ImportedChartLibrary importedChartLibrary { get; set; }

    [Resolved]
    private SongSelectArtworkTextureCache artworkTextureCache { get; set; }

    public TestSceneSongSelectEntryPerformance()
    {
        Add(screenStack = new ScreenStack(mainScreen = new MainScreen())
        {
            RelativeSizeAxes = Axes.Both,
        });
    }

    [Test]
    public void TestPreparedNavigationDoesNotDecodeArtworkOnFirstFrame()
    {
        AddStep("create six large standalone artworks", createLargeArtworks);
        AddStep("publish standalone charts", publishStandaloneCharts);
        AddUntilStep("latest song select preload is ready", () =>
            mainScreen.PreparedSongSelectEntryCount == artworkPaths.Count
            && mainScreen.IsPreparedSongSelectCurrent);
        AddAssert("all first-frame artwork is cached before navigation", () =>
            artworkPaths.All(artworkTextureCache.IsCached));
        AddStep("record artwork cache size", () =>
            cachedArtworkBeforeNavigation = artworkTextureCache.CachedArtworkCount);
        AddStep("open prepared song select", () =>
        {
            navigationStopwatch = Stopwatch.StartNew();
            mainScreen.ChildrenOfType<HomePrimaryAction>()
                      .Single()
                      .Action?.Invoke();
            stageAlphaAtNavigation =
                (screenStack.CurrentScreen as SongSelectScreen)?.StageAlpha
                ?? -1;
        });
        AddAssert("prepared screen is visible on navigation frame", () =>
            stageAlphaAtNavigation >= 0.99f);
        AddUntilStep("entry motion starts", () =>
            screenStack.CurrentScreen is SongSelectScreen songSelect
            && songSelect.EntryTransitionVersion > 0);
        AddUntilStep("song rows materialise", () =>
            screenStack.CurrentScreen is SongSelectScreen songSelect
            && songSelect.MaterialisedSongListDrawableCount > 0);
        AddUntilStep("entry motion settles", () =>
            screenStack.CurrentScreen is SongSelectScreen songSelect
            && !songSelect.EntryTransitionInProgress);
        AddAssert("prepared navigation stays below hitch threshold", () =>
            navigationStopwatch.ElapsedMilliseconds < 250);
        AddAssert("first frame has no artwork cache miss", () =>
            artworkTextureCache.CachedArtworkCount
            == cachedArtworkBeforeNavigation);
        AddStep("return to main", () => screenStack.CurrentScreen.Exit());
        AddUntilStep("main screen resumes", () =>
            ReferenceEquals(screenStack.CurrentScreen, mainScreen));
        AddUntilStep("next song select preload is ready", () =>
            mainScreen.PreparedSongSelectEntryCount == artworkPaths.Count
            && mainScreen.IsPreparedSongSelectCurrent);
        AddStep("open prepared song select again", () =>
        {
            stageAlphaAtNavigation = -1;
            mainScreen.ChildrenOfType<HomePrimaryAction>()
                      .Single()
                      .Action?.Invoke();
            stageAlphaAtNavigation =
                (screenStack.CurrentScreen as SongSelectScreen)?.StageAlpha
                ?? -1;
        });
        AddAssert("repeated entry is visible on navigation frame", () =>
            stageAlphaAtNavigation >= 0.99f);
        AddUntilStep("repeated entry starts motion", () =>
            screenStack.CurrentScreen is SongSelectScreen songSelect
            && songSelect.EntryTransitionVersion > 0);
        AddStep("return to main again", () =>
            screenStack.CurrentScreen.Exit());
        AddUntilStep("main screen resumes again", () =>
            ReferenceEquals(screenStack.CurrentScreen, mainScreen));
        AddStep("clear test library", () => importedChartLibrary.Clear());
    }

    private void createLargeArtworks()
    {
        temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            "Yokko-song-select-entry-performance",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryDirectory);

        for (int index = 0; index < 6; index++)
        {
            string path = Path.Combine(
                temporaryDirectory,
                $"artwork-{index}.png");
            using var image = new Image<Rgba32>(
                1920,
                1080,
                new Rgba32(
                    (byte)(32 + index * 25),
                    (byte)(80 + index * 17),
                    (byte)(160 + index * 11)));
            image.SaveAsPng(path);
            artworkPaths.Add(path);
        }
    }

    private void publishStandaloneCharts()
    {
        importedChartLibrary.Clear();
        for (int index = 0; index < artworkPaths.Count; index++)
        {
            var beatmap = new YokkoBeatmap(
                $"Entry performance {index}",
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
                new ChartImportResult(
                    beatmap,
                    [],
                    artworkPaths[index]),
                Path.Combine(temporaryDirectory, $"chart-{index}.osu"));
        }
    }

    protected override void Dispose(bool isDisposing)
    {
        base.Dispose(isDisposing);
        if (!isDisposing || string.IsNullOrWhiteSpace(temporaryDirectory))
            return;

        try
        {
            Directory.Delete(temporaryDirectory, true);
        }
        catch
        {
            // Test cleanup must not hide the assertion result.
        }
    }
}
