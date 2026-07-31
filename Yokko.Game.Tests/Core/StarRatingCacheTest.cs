using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Yokko.Core.Beatmaps;
using Yokko.Core.Difficulty;
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
            YokkoBeatmap chart = DemoBeatmaps.CreateFourKeyDemo() with
            {
                HitObjects = DemoBeatmaps.CreateFourKeyDemo()
                    .HitObjects
                    .Append(new YokkoHitObject(
                        0,
                        2500,
                        null,
                        HitObjectKind.Mine))
                    .ToArray(),
            };
            var first = new StarRatingCache();
            first.Initialise(path);
            ManiaStarRatingResult expected =
                first.GetOrCalculate(chart);
            first.SaveIfChanged();

            var second = new StarRatingCache();
            second.Initialise(path);
            ManiaStarRatingResult actual =
                second.GetOrCalculate(chart);

            Assert.Multiple(() =>
            {
                Assert.That(File.Exists(path), Is.True);
                Assert.That(second.Count, Is.EqualTo(1));
                Assert.That(second.HitCount, Is.EqualTo(1));
                Assert.That(actual.Value, Is.EqualTo(expected.Value));
                Assert.That(actual.IsPartial, Is.True);
                Assert.That(
                    actual.Limitations,
                    Is.EqualTo(expected.Limitations));
                Assert.That(
                    actual.EffectiveOverallDifficulty,
                    Is.EqualTo(
                        expected.EffectiveOverallDifficulty));
                Assert.That(
                    actual.Adjustments,
                    Is.EqualTo(expected.Adjustments));
                Assert.That(
                    actual.UpstreamValue,
                    Is.EqualTo(expected.UpstreamValue));
                Assert.That(
                    actual.LongNoteCalibrationFactor,
                    Is.EqualTo(
                        expected.LongNoteCalibrationFactor));
                Assert.That(
                    actual.EffectiveActionCount,
                    Is.EqualTo(expected.EffectiveActionCount));
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
