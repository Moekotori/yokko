using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using Yokko.Core.Beatmaps;
using Yokko.Core.Difficulty;
using Yokko.Core.Scoring;
using Yokko.Game.Screens.SongSelect;

namespace Yokko.Game.Tests.Visual;

[TestFixture]
public partial class TestSceneSongSelectVirtualisedList : YokkoTestScene
{
    private const int item_count = 10_000;
    private readonly SongSelectVirtualisedList list;
    private readonly SongSelectEntry[] entries;

    public TestSceneSongSelectVirtualisedList()
    {
        var beatmap = DemoBeatmaps.CreateFourKeyDemo();
        var ratings = new ManiaDifficultyRatings(
            ManiaMsdCalculator.CalculateResult(beatmap),
            ManiaStarRatingCalculator.CalculateResult(beatmap));
        entries = Enumerable.Range(0, item_count)
                            .Select(index => new SongSelectEntry(
                                beatmap,
                                string.Empty,
                                ratings.EtternaMsd,
                                ratings.RebirthStars,
                                TimeSpan.FromMinutes(2),
                                120,
                                0,
                                0,
                                [],
                                [],
                                "scale-package",
                                "Scale package",
                                true,
                                $"scale-{index}"))
                            .ToArray();

        Add(list = new SongSelectVirtualisedList(
            _ => ratings,
            _ => null,
            () => ManiaDifficultyRatingMode.EtternaMsd,
            null,
            _ => { },
            _ => { },
            _ => { })
        {
            RelativeSizeAxes = Axes.Both,
        });
    }

    [Test]
    public void TestTenThousandRowsKeepBoundedDrawableCount()
    {
        AddStep("index 10,000 lightweight rows", () => list.SetItems(
            entries.Select(entry => new SongSelectVirtualItem
            {
                Entry = entry,
                VisualHeight = 58,
            })));
        AddAssert("all logical rows indexed", () =>
            list.ItemCount == item_count);
        AddUntilStep("initial viewport actualised", () =>
            list.MaterialisedDrawableCount > 0);
        AddAssert("initial drawable count is bounded", () =>
            list.MaterialisedDrawableCount <= 40);
        AddStep("jump to last row", () =>
            list.ScrollEntryIntoView(entries[^1], false));
        AddUntilStep("last row actualised", () =>
            list.MaterialisedRows.Any(row =>
                ReferenceEquals(row.Entry, entries[^1])));
        AddAssert("far jump remains bounded", () =>
            list.MaterialisedDrawableCount <= 40);
    }
}
