using System;
using System.Linq;
using NUnit.Framework;
using Yokko.Core.Beatmaps;
using Yokko.Game.Screens.SongSelect;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class SongSelectSortingTest
{
    [Test]
    public void TextAndNumericModesUseNaturalDefaultDirections()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                SongSelectSorting.DefaultDirection(SongSelectSortMode.Title),
                Is.EqualTo(SongSelectSortDirection.Ascending));
            Assert.That(
                SongSelectSorting.DefaultDirection(SongSelectSortMode.Artist),
                Is.EqualTo(SongSelectSortDirection.Ascending));
            Assert.That(
                SongSelectSorting.DefaultDirection(SongSelectSortMode.Creator),
                Is.EqualTo(SongSelectSortDirection.Ascending));
            Assert.That(
                SongSelectSorting.DefaultDirection(SongSelectSortMode.Difficulty),
                Is.EqualTo(SongSelectSortDirection.Descending));
            Assert.That(
                SongSelectSorting.DefaultDirection(SongSelectSortMode.LastPlayed),
                Is.EqualTo(SongSelectSortDirection.Descending));
        });
    }

    [Test]
    public void PackageChartsStayContiguousWhilePackageOrderUsesSortKey()
    {
        SongSelectEntry[] input =
        [
            entry("Zulu", "package-a", 120),
            entry("Beta", "package-b", 180),
            entry("Alpha", "package-a", 150),
        ];

        SongSelectEntry[] ascending = SongSelectSorting.Sort(
                input,
                SongSelectSortMode.Title,
                SongSelectSortDirection.Ascending,
                _ => null)
            .ToArray();
        SongSelectEntry[] descending = SongSelectSorting.Sort(
                input,
                SongSelectSortMode.Title,
                SongSelectSortDirection.Descending,
                _ => null)
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(
                ascending.Select(item => item.Beatmap.Title),
                Is.EqualTo(new[] { "Alpha", "Zulu", "Beta" }));
            Assert.That(
                descending.Select(item => item.Beatmap.Title),
                Is.EqualTo(new[] { "Zulu", "Alpha", "Beta" }));
            Assert.That(
                ascending.Take(2).Select(item => item.PackageId).Distinct().Count(),
                Is.EqualTo(1));
        });
    }

    [Test]
    public void UnknownNumericValuesRemainLastInBothDirections()
    {
        SongSelectEntry knownSlow = entry("Slow", "slow", 120);
        SongSelectEntry unknown = entry("Unknown", "unknown", 0);
        SongSelectEntry knownFast = entry("Fast", "fast", 200);
        SongSelectEntry[] input = [unknown, knownSlow, knownFast];

        Assert.Multiple(() =>
        {
            Assert.That(
                SongSelectSorting.Sort(
                                     input,
                                     SongSelectSortMode.Bpm,
                                     SongSelectSortDirection.Ascending,
                                     _ => null)
                                 .Select(item => item.Beatmap.Title),
                Is.EqualTo(new[] { "Slow", "Fast", "Unknown" }));
            Assert.That(
                SongSelectSorting.Sort(
                                     input,
                                     SongSelectSortMode.Bpm,
                                     SongSelectSortDirection.Descending,
                                     _ => null)
                                 .Select(item => item.Beatmap.Title),
                Is.EqualTo(new[] { "Fast", "Slow", "Unknown" }));
        });
    }

    private static SongSelectEntry entry(string title, string packageId, double bpm) =>
        new(
            DemoBeatmaps.CreateFourKeyDemo() with { Title = title },
            string.Empty,
            null,
            null,
            TimeSpan.FromMinutes(2),
            bpm,
            0,
            0,
            [],
            [],
            packageId,
            packageId,
            true,
            $"{packageId}-{title}");
}
