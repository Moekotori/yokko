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

        bool success = ManiaStarRatingCalculator.TryCalculate(
            chart,
            out double rating);

        Assert.Multiple(() =>
        {
            Assert.That(success, Is.False);
            Assert.That(rating, Is.Zero);
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

        Assert.That(
            ManiaStarRatingCalculator.TryCalculate(chart, out _),
            Is.False);
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
