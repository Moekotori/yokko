using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Yokko.Core.Beatmaps;
using Yokko.Core.Difficulty;
using Yokko.Core.Gameplay;
using Yokko.Core.Timing;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class ManiaStarRatingCalculatorTest
{
    [Test]
    public void KnownChartProducesStableRating()
    {
        double rating = ManiaStarRatingCalculator.Calculate(createChart());

        Assert.That(rating, Is.EqualTo(0.84308937416632168).Within(0.000001));
    }

    [Test]
    public void NonPlayableObjectsDoNotAffectRating()
    {
        YokkoBeatmap chart = createChart();
        double baseline = ManiaStarRatingCalculator.Calculate(chart);
        YokkoHitObject[] extras =
        [
            new YokkoHitObject(0, 2500, null, HitObjectKind.Mine),
            new YokkoHitObject(1, 2600, null, HitObjectKind.Sample),
        ];

        double withExtras = ManiaStarRatingCalculator.Calculate(chart with
        {
            HitObjects = chart.HitObjects.Concat(extras).ToArray(),
        });

        Assert.That(withExtras, Is.EqualTo(baseline));
    }

    [Test]
    public void HoldTailsContributeToRating()
    {
        YokkoBeatmap chart = createChart();
        double withHolds = ManiaStarRatingCalculator.Calculate(chart);
        double tapsOnly = ManiaStarRatingCalculator.Calculate(chart with
        {
            HitObjects = chart.HitObjects
                              .Select(note => note.Kind == HitObjectKind.Hold
                                  ? new YokkoHitObject(
                                      note.Lane,
                                      note.StartTimeMilliseconds,
                                      null,
                                      HitObjectKind.Tap)
                                  : note)
                              .ToArray(),
        });

        Assert.That(withHolds, Is.Not.EqualTo(tapsOnly).Within(0.000001));
    }

    [Test]
    public void TooFewPlayableNotesFailSoftly()
    {
        YokkoBeatmap chart = createChart() with
        {
            HitObjects = createChart().HitObjects
                                      .Take(ManiaStarRatingCalculator.MinimumNoteCount - 1)
                                      .ToArray(),
        };

        ManiaStarRatingResult result =
            ManiaStarRatingCalculator.CalculateResult(chart);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(
                result.Status,
                Is.EqualTo(ManiaStarRatingStatus.TooFewNotes));
            Assert.That(result.Value, Is.Null);
            Assert.That(result.FailureReason, Does.Contain("at least 20"));
        });
    }

    [Test]
    public void InvalidLaneFailsSoftly()
    {
        YokkoBeatmap chart = createChart() with
        {
            HitObjects = createChart().HitObjects
                                      .Append(new YokkoHitObject(
                                          4,
                                          9000,
                                          null,
                                          HitObjectKind.Tap))
                                      .ToArray(),
        };

        ManiaStarRatingResult result =
            ManiaStarRatingCalculator.CalculateResult(chart);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(
                result.Status,
                Is.EqualTo(ManiaStarRatingStatus.InvalidLane));
        });
    }

    [Test]
    public void PlaybackRateChangesRating()
    {
        YokkoBeatmap chart = createChart();
        double halfTime = ManiaStarRatingCalculator.Calculate(chart, 0.75);
        double normal = ManiaStarRatingCalculator.Calculate(chart);
        double doubleTime = ManiaStarRatingCalculator.Calculate(chart, 1.5);

        Assert.That(halfTime, Is.LessThan(normal));
        Assert.That(doubleTime, Is.GreaterThan(normal));
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(double.NaN)]
    [TestCase(double.PositiveInfinity)]
    public void InvalidPlaybackRateIsReported(double playbackRate)
    {
        ManiaStarRatingResult result =
            ManiaStarRatingCalculator.CalculateResult(
                createChart(),
                playbackRate);

        Assert.That(
            result.Status,
            Is.EqualTo(ManiaStarRatingStatus.InvalidRate));
    }

    [Test]
    public void RatingAndCacheKeyIgnoreInputOrder()
    {
        YokkoBeatmap chart = createChart();
        YokkoBeatmap reversed = chart with
        {
            HitObjects = chart.HitObjects.Reverse().ToArray(),
        };

        Assert.Multiple(() =>
        {
            Assert.That(
                ManiaStarRatingCalculator.Calculate(reversed),
                Is.EqualTo(ManiaStarRatingCalculator.Calculate(chart)));
            Assert.That(
                ManiaStarRatingCalculator.CreateCacheKey(reversed),
                Is.EqualTo(
                    ManiaStarRatingCalculator.CreateCacheKey(chart)));
        });
    }

    [Test]
    public void MirroredLanesPreserveRating()
    {
        YokkoBeatmap chart = createChart();
        YokkoBeatmap mirrored = chart with
        {
            HitObjects = chart.HitObjects
                              .Select(note => new YokkoHitObject(
                                  3 - note.Lane,
                                  note.StartTimeMilliseconds,
                                  note.EndTimeMilliseconds,
                                  note.Kind,
                                  note.SampleKey,
                                  note.ScrollProfileId))
                              .ToArray(),
        };

        Assert.That(
            ManiaStarRatingCalculator.Calculate(mirrored),
            Is.EqualTo(ManiaStarRatingCalculator.Calculate(chart))
              .Within(0.000001));
    }

    [Test]
    public void CacheKeyIncludesPlaybackRate()
    {
        YokkoBeatmap chart = createChart();

        Assert.That(
            ManiaStarRatingCalculator.CreateCacheKey(chart, 1.5),
            Is.Not.EqualTo(
                ManiaStarRatingCalculator.CreateCacheKey(chart)));
    }

    private static YokkoBeatmap createChart()
    {
        var notes = new List<YokkoHitObject>();

        for (int i = 0; i < 32; i++)
        {
            double startTime = 1000 + i * 180;
            bool hold = i % 7 == 0;
            notes.Add(new YokkoHitObject(
                i % 4,
                startTime,
                hold ? startTime + 360 : null,
                hold ? HitObjectKind.Hold : HitObjectKind.Tap));
        }

        return new YokkoBeatmap(
            "Rating fixture",
            "Yokko",
            "Tests",
            "4K",
            KeyMode.FourKey,
            ChartSourceFormat.Yokko,
            [YokkoTimingPoint.Default],
            null,
            notes,
            OverallDifficulty: 7);
    }
}
