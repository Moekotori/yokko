using System;
using System.IO;
using NUnit.Framework;
using Yokko.Core.Beatmaps;
using Yokko.Game.Importing;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class StarRatingCacheTest
{
    [Test]
    public void SuccessfulRatingPersistsAcrossInstances()
    {
        string root = createRoot();
        string path = Path.Combine(root, "star-ratings.json");

        try
        {
            YokkoBeatmap chart = DemoBeatmaps.CreateFourKeyDemo();
            var first = new StarRatingCache();
            first.Initialise(path);
            double expected = first.GetOrCalculate(chart).Value!.Value;
            first.SaveIfChanged();

            var second = new StarRatingCache();
            second.Initialise(path);
            double actual = second.GetOrCalculate(chart).Value!.Value;

            Assert.Multiple(() =>
            {
                Assert.That(File.Exists(path), Is.True);
                Assert.That(second.Count, Is.EqualTo(1));
                Assert.That(second.HitCount, Is.EqualTo(1));
                Assert.That(actual, Is.EqualTo(expected));
            });
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [Test]
    public void CorruptCacheFallsBackToCalculation()
    {
        string root = createRoot();
        string path = Path.Combine(root, "star-ratings.json");

        try
        {
            File.WriteAllText(path, "not json");
            var cache = new StarRatingCache();

            Assert.DoesNotThrow(() => cache.Initialise(path));
            Assert.That(
                cache.GetOrCalculate(
                    DemoBeatmaps.CreateSevenKeyDemo()).IsSuccess,
                Is.True);
            Assert.That(cache.HitCount, Is.Zero);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    private static string createRoot()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            $"yokko-star-rating-cache-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return root;
    }
}
