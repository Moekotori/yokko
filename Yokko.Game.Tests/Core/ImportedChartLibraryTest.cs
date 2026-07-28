using System.Linq;
using NUnit.Framework;
using Yokko.Core.Beatmaps;
using Yokko.Game.Importing;
using Yokko.Import;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class ImportedChartLibraryTest
{
    [Test]
    public void ReimportingSamePathReplacesExistingChart()
    {
        var library = new ImportedChartLibrary();
        var first = new ChartImportResult(DemoBeatmaps.CreateFourKeyDemo(), []);
        var replacement = new ChartImportResult(
            DemoBeatmaps.CreateFourKeyDemo() with { Title = "Replacement" },
            []);

        library.AddOrReplace(first, @"C:\Charts\example.osu");
        library.AddOrReplace(replacement, @"c:\charts\EXAMPLE.osu");

        Assert.That(library.GetCharts(), Has.Count.EqualTo(1));
        Assert.That(library.GetCharts()[0].Result.Beatmap.Title, Is.EqualTo("Replacement"));
    }

    [Test]
    public void PackageAddsAllChartsAndRefreshesOnce()
    {
        var library = new ImportedChartLibrary();
        int refreshCount = 0;
        library.LibraryChanged += () => refreshCount++;

        library.AddOrReplace(
            [
                new ChartImportResult(DemoBeatmaps.CreateFourKeyDemo(), []),
                new ChartImportResult(DemoBeatmaps.CreateSevenKeyDemo(), []),
            ],
            @"C:\Charts\pack.osz");

        Assert.That(library.GetCharts(), Has.Count.EqualTo(2));
        Assert.That(library.GetCharts().Select(chart => chart.Id), Is.Unique);
        Assert.That(refreshCount, Is.EqualTo(1));
    }
}
