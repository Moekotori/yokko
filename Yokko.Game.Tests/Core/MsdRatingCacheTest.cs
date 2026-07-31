using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Yokko.Core.Beatmaps;
using Yokko.Core.Difficulty;
using Yokko.Core.Gameplay;
using Yokko.Game.Importing;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class MsdRatingCacheTest
{
    [Test]
    public void SuccessfulMsdSurvivesCacheReload()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"yokko-msd-cache-{Guid.NewGuid():N}");
        string path = Path.Combine(directory, "etterna-msd.json");

        try
        {
            YokkoBeatmap chart = createChart();
            var first = new MsdRatingCache();
            first.Initialise(path);
            ManiaMsdResult expected = first.GetOrCalculate(chart);
            first.SaveIfChanged();

            var second = new MsdRatingCache();
            second.Initialise(path);
            ManiaMsdResult actual = second.GetOrCalculate(chart);

            Assert.Multiple(() =>
            {
                Assert.That(expected.IsSuccess, Is.True);
                Assert.That(actual.Skillsets, Is.EqualTo(expected.Skillsets));
                Assert.That(second.HitCount, Is.EqualTo(1));
                Assert.That(second.Count, Is.EqualTo(1));
            });
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

    private static YokkoBeatmap createChart() => new(
        "Cache fixture",
        "Yokko",
        "Tests",
        "4K",
        KeyMode.FourKey,
        ChartSourceFormat.Etterna,
        [],
        null,
        Enumerable.Range(0, 64)
            .Select(index => new YokkoHitObject(
                index % 4,
                index * 150d,
                null,
                HitObjectKind.Tap))
            .ToArray());
}
