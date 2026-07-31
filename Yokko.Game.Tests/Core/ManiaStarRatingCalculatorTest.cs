using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Yokko.Core.Beatmaps;
using Yokko.Core.Difficulty;
using Yokko.Core.Gameplay;
using Yokko.Core.Mods;
using Yokko.Core.Scoring;
using Yokko.Core.Timing;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class ManiaStarRatingCalculatorTest
{
    [Test]
    public void KnownChartProducesStableRating()
    {
        ManiaStarRatingResult result =
            ManiaStarRatingCalculator.CalculateResult(createChart());

        Assert.Multiple(() =>
        {
            Assert.That(
                result.Value,
                Is.EqualTo(1.3766484797801661).Within(0.000001));
            Assert.That(
                result.UpstreamValue,
                Is.EqualTo(0.84308937416632168).Within(0.000001));
            Assert.That(
                result.Adjustments,
                Is.EqualTo(
                    ManiaStarRatingAdjustments.LengthCalibration));
            Assert.That(result.LongNoteCalibrationFactor, Is.EqualTo(1));
            Assert.That(result.EffectiveActionCount, Is.EqualTo(37));
        });
    }

    [TestCase(20, 2)]
    [TestCase(60, 1.4142135623730951)]
    public void ShortChartsUseSofterActionCountCurve(
        int noteCount,
        double expectedGain)
    {
        ManiaStarRatingResult result =
            ManiaStarRatingCalculator.CalculateResult(
                createTapChart(noteCount));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(
                result.Value!.Value
                / result.UpstreamValue!.Value,
                Is.EqualTo(expectedGain).Within(0.000001));
            Assert.That(
                result.Adjustments.HasFlag(
                    ManiaStarRatingAdjustments.LengthCalibration),
                Is.True);
            Assert.That(
                result.EffectiveActionCount,
                Is.EqualTo(noteCount));
        });
    }

    [Test]
    public void LongChartsRemainCloseToUpstreamLengthScale()
    {
        ManiaStarRatingResult result =
            ManiaStarRatingCalculator.CalculateResult(
                createTapChart(600));

        Assert.That(
            result.Value!.Value / result.UpstreamValue!.Value,
            Is.InRange(1, 1.06));
    }

    [Test]
    public void MixedRiceAndLongNotesAreNotLnCalibrated()
    {
        ManiaStarRatingResult result =
            ManiaStarRatingCalculator.CalculateResult(createChart());

        Assert.Multiple(() =>
        {
            Assert.That(
                result.Adjustments.HasFlag(
                    ManiaStarRatingAdjustments
                        .LinkedLongNotesCalibrated),
                Is.False);
            Assert.That(
                result.Adjustments.HasFlag(
                    ManiaStarRatingAdjustments
                        .InvertLongNotesCalibrated),
                Is.False);
            Assert.That(result.LongNoteCalibrationFactor, Is.EqualTo(1));
        });
    }

    [Test]
    public void MostlyConnectedLongNotesReceiveSelectiveCalibration()
    {
        ManiaStarRatingResult result =
            ManiaStarRatingCalculator.CalculateResult(
                createConnectedLongNoteChart());

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(
                result.Adjustments.HasFlag(
                    ManiaStarRatingAdjustments
                        .LinkedLongNotesCalibrated),
                Is.True);
            Assert.That(
                result.LongNoteCalibrationFactor,
                Is.EqualTo(0.88).Within(0.000001));
            Assert.That(result.EffectiveActionCount, Is.EqualTo(80));
        });
    }

    [Test]
    public void InvertUsesItsOwnLongNoteCalibration()
    {
        YokkoBeatmap chart = createTapChart(32);
        var mods = new ManiaModSet([ManiaModId.Invert]);
        YokkoBeatmap inverted =
            ManiaBeatmapModTransformer.Apply(chart, mods);
        ManiaStarRatingContext context =
            ManiaStarRatingContext.ForGameplay(
                inverted,
                mods,
                JudgementConfiguration.YokkoDefault,
                minesEnabled: true,
                timelineRate: 1);

        ManiaStarRatingResult result =
            ManiaStarRatingCalculator.CalculateResult(
                inverted,
                context);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(
                result.Adjustments.HasFlag(
                    ManiaStarRatingAdjustments
                        .InvertLongNotesCalibrated),
                Is.True);
            Assert.That(
                result.LongNoteCalibrationFactor,
                Is.EqualTo(0.90).Within(0.000001));
            Assert.That(
                result.EffectiveActionCount,
                Is.EqualTo(inverted.HitObjects.Count * 2));
        });
    }

    [Test]
    public void SampleObjectsDoNotAffectRating()
    {
        YokkoBeatmap chart = createChart();
        double baseline = ManiaStarRatingCalculator.Calculate(chart);
        YokkoHitObject[] extras =
        [
            new YokkoHitObject(1, 2600, null, HitObjectKind.Sample),
        ];

        double withExtras = ManiaStarRatingCalculator.Calculate(chart with
        {
            HitObjects = chart.HitObjects.Concat(extras).ToArray(),
        });

        Assert.That(withExtras, Is.EqualTo(baseline));
    }

    [Test]
    public void MinesKeepBaseInputValueButMarkRatingPartial()
    {
        YokkoBeatmap chart = createChart();
        ManiaStarRatingResult baseline =
            ManiaStarRatingCalculator.CalculateResult(chart);
        YokkoBeatmap withMine = chart with
        {
            HitObjects = chart.HitObjects
                              .Append(new YokkoHitObject(
                                  0,
                                  2500,
                                  null,
                                  HitObjectKind.Mine))
                              .ToArray(),
        };

        ManiaStarRatingResult enabled =
            ManiaStarRatingCalculator.CalculateResult(withMine);
        ManiaStarRatingContext minesDisabled =
            ManiaStarRatingContext.ForGameplay(
                withMine,
                ManiaModSet.Empty,
                JudgementConfiguration.YokkoDefault,
                minesEnabled: false,
                timelineRate: 1);
        ManiaStarRatingResult disabled =
            ManiaStarRatingCalculator.CalculateResult(
                withMine,
                minesDisabled);

        Assert.Multiple(() =>
        {
            Assert.That(enabled.Value, Is.EqualTo(baseline.Value));
            Assert.That(enabled.IsPartial, Is.True);
            Assert.That(
                enabled.Limitations.HasFlag(
                    ManiaStarRatingLimitations.MinesExcluded),
                Is.True);
            Assert.That(disabled.Value, Is.EqualTo(baseline.Value));
            Assert.That(disabled.IsPartial, Is.False);
        });
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

    [Test]
    public void EffectiveJudgementWindowsAffectRating()
    {
        YokkoBeatmap chart = createChart();
        ManiaStarRatingResult normal = calculateWithMods(
            chart,
            ManiaModSet.Empty);
        ManiaStarRatingResult easy = calculateWithMods(
            chart,
            new ManiaModSet([ManiaModId.Easy]));
        ManiaStarRatingResult hardRock = calculateWithMods(
            chart,
            new ManiaModSet([ManiaModId.HardRock]));

        Assert.Multiple(() =>
        {
            Assert.That(easy.Value, Is.LessThan(normal.Value));
            Assert.That(hardRock.Value, Is.GreaterThan(normal.Value));
            Assert.That(
                easy.EffectiveOverallDifficulty,
                Is.LessThan(normal.EffectiveOverallDifficulty));
            Assert.That(
                hardRock.EffectiveOverallDifficulty,
                Is.GreaterThan(normal.EffectiveOverallDifficulty));
        });
    }

    [Test]
    public void NoReleaseIsExplicitlyMarkedPartial()
    {
        YokkoBeatmap chart = createChart();
        ManiaStarRatingResult normal = calculateWithMods(
            chart,
            ManiaModSet.Empty);
        ManiaStarRatingResult noRelease = calculateWithMods(
            chart,
            new ManiaModSet([ManiaModId.NoRelease]));

        Assert.Multiple(() =>
        {
            Assert.That(noRelease.Value, Is.LessThan(normal.Value));
            Assert.That(noRelease.IsPartial, Is.True);
            Assert.That(
                noRelease.Limitations.HasFlag(
                    ManiaStarRatingLimitations.NoReleaseNotModelled),
                Is.True);
        });
    }

    [Test]
    public void PretransformedTimeRampIsNotMarkedApproximate()
    {
        YokkoBeatmap chart = createChart();
        ManiaModSet windUp = ManiaModSet.Empty.WithTimeRamp(
            ManiaModId.WindUp,
            1,
            1.5,
            false);
        YokkoBeatmap transformed =
            ManiaTimeRampTimeline.TransformForDifficulty(chart, windUp);
        ManiaStarRatingContext context =
            ManiaStarRatingContext.ForGameplay(
                transformed,
                windUp,
                JudgementConfiguration.YokkoDefault,
                minesEnabled: true,
                timelineRate: 1,
                dynamicRatePretransformed: true);

        ManiaStarRatingResult result =
            ManiaStarRatingCalculator.CalculateResult(
                transformed,
                context);

        Assert.That(
            result.Limitations.HasFlag(
                ManiaStarRatingLimitations.DynamicRateApproximation),
            Is.False);
    }

    [Test]
    public void QuaverMapsItsFixedGreatWindowToEquivalentOd()
    {
        YokkoBeatmap chart = createChart() with
        {
            SourceFormat = ChartSourceFormat.Quaver,
        };

        ManiaStarRatingResult result =
            ManiaStarRatingCalculator.CalculateResult(chart);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(
                result.EffectiveOverallDifficulty,
                Is.EqualTo((64.5 - 43) / 3).Within(0.000001));
            Assert.That(
                result.Value,
                Is.Not.EqualTo(
                    ManiaStarRatingCalculator.Calculate(
                        createChart()))
                  .Within(0.000001));
        });
    }

    [Test]
    public void CacheKeyIncludesJudgementAndCoverageContext()
    {
        YokkoBeatmap chart = createChart();
        ManiaStarRatingContext easy =
            ManiaStarRatingContext.ForGameplay(
                chart,
                new ManiaModSet([ManiaModId.Easy]),
                JudgementConfiguration.YokkoDefault,
                minesEnabled: true,
                timelineRate: 1);
        ManiaStarRatingContext noRelease =
            ManiaStarRatingContext.ForGameplay(
                chart,
                new ManiaModSet([ManiaModId.NoRelease]),
                JudgementConfiguration.YokkoDefault,
                minesEnabled: true,
                timelineRate: 1);
        ManiaStarRatingContext normal =
            ManiaStarRatingContext.ForGameplay(
                chart,
                ManiaModSet.Empty,
                JudgementConfiguration.YokkoDefault,
                minesEnabled: true,
                timelineRate: 1);
        ManiaStarRatingContext invert =
            ManiaStarRatingContext.ForGameplay(
                chart,
                new ManiaModSet([ManiaModId.Invert]),
                JudgementConfiguration.YokkoDefault,
                minesEnabled: true,
                timelineRate: 1);

        Assert.Multiple(() =>
        {
            Assert.That(
                ManiaStarRatingCalculator.CreateCacheKey(chart, easy),
                Is.Not.EqualTo(
                    ManiaStarRatingCalculator.CreateCacheKey(
                        chart,
                        noRelease)));
            Assert.That(
                ManiaStarRatingCalculator.CreateCacheKey(chart, normal),
                Is.Not.EqualTo(
                    ManiaStarRatingCalculator.CreateCacheKey(
                        chart,
                        invert)));
        });
    }

    private static ManiaStarRatingResult calculateWithMods(
        YokkoBeatmap chart,
        ManiaModSet mods)
    {
        ManiaStarRatingContext context =
            ManiaStarRatingContext.ForGameplay(
                chart,
                mods,
                JudgementConfiguration.YokkoDefault,
                minesEnabled: true,
                timelineRate: mods.PlaybackRate);
        return ManiaStarRatingCalculator.CalculateResult(
            chart,
            context,
            mods.PlaybackRate);
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

    private static YokkoBeatmap createTapChart(int noteCount)
    {
        YokkoHitObject[] notes = Enumerable.Range(0, noteCount)
            .Select(index => new YokkoHitObject(
                index % 4,
                1000 + index * 180,
                null,
                HitObjectKind.Tap))
            .ToArray();
        return new YokkoBeatmap(
            "Tap fixture",
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

    private static YokkoBeatmap createConnectedLongNoteChart()
    {
        YokkoHitObject[] notes = Enumerable.Range(0, 40)
            .Select(index =>
            {
                double startTime = 1000 + index * 200;
                return new YokkoHitObject(
                    index % 4,
                    startTime,
                    startTime + 700,
                    HitObjectKind.Hold);
            })
            .ToArray();
        return new YokkoBeatmap(
            "Connected LN fixture",
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
