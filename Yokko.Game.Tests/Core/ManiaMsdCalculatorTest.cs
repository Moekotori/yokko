using System;
using System.Linq;
using NUnit.Framework;
using Yokko.Core.Beatmaps;
using Yokko.Core.Difficulty;
using Yokko.Core.Gameplay;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class ManiaMsdCalculatorTest
{
    [Test]
    public void KnownChartProducesCompleteEtterna515Result()
    {
        ManiaMsdResult result =
            ManiaMsdCalculator.CalculateResult(createChart());

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(ManiaMsdStatus.Success));
            Assert.That(result.AlgorithmIdentifier, Does.Contain("v515"));
            Assert.That(result.Skillsets, Is.Not.Null);
            Assert.That(
                Enum.GetValues<EtternaMsdSkillset>()
                    .Select(skillset => result.Skillsets![skillset]),
                Is.All.GreaterThanOrEqualTo(0));
            Assert.That(
                result.Skillsets!.Overall,
                Is.EqualTo(8.408500671386719).Within(0.000001));
            Assert.That(
                result.Skillsets.Stream,
                Is.EqualTo(8.408500671386719).Within(0.000001));
            Assert.That(
                result.Skillsets.Jumpstream,
                Is.EqualTo(5.060000419616699).Within(0.000001));
            Assert.That(
                result.Skillsets.Handstream,
                Is.EqualTo(4.580000400543213).Within(0.000001));
            Assert.That(
                result.Skillsets.Stamina,
                Is.EqualTo(6.82586669921875).Within(0.000001));
            Assert.That(
                result.Skillsets.JackSpeed,
                Is.EqualTo(4.820000171661377).Within(0.000001));
            Assert.That(
                result.Skillsets.Chordjack,
                Is.EqualTo(6.019999980926514).Within(0.000001));
            Assert.That(
                result.Skillsets.Technical,
                Is.EqualTo(7.299999713897705).Within(0.000001));
        });
    }

    [Test]
    public void PlaybackRateChangesMsd()
    {
        YokkoBeatmap chart = createChart();
        ManiaMsdResult slow =
            ManiaMsdCalculator.CalculateResult(chart, 0.75);
        ManiaMsdResult normal =
            ManiaMsdCalculator.CalculateResult(chart);
        ManiaMsdResult fast =
            ManiaMsdCalculator.CalculateResult(chart, 1.5);

        Assert.Multiple(() =>
        {
            Assert.That(slow.IsSuccess, Is.True);
            Assert.That(normal.IsSuccess, Is.True);
            Assert.That(fast.IsSuccess, Is.True);
            Assert.That(slow.Value, Is.LessThan(normal.Value));
            Assert.That(normal.Value, Is.LessThan(fast.Value));
        });
    }

    [Test]
    public void HoldTailsMinesAndSamplesDoNotChangeMsdRows()
    {
        YokkoBeatmap chart = createChart();
        ManiaMsdResult baseline =
            ManiaMsdCalculator.CalculateResult(chart);
        ManiaMsdResult withExtras =
            ManiaMsdCalculator.CalculateResult(
                chart with
                {
                    HitObjects =
                    [
                        .. chart.HitObjects.Select(hitObject =>
                            hitObject.Kind == HitObjectKind.Hold
                                ? new YokkoHitObject(
                                    hitObject.Lane,
                                    hitObject.StartTimeMilliseconds,
                                    hitObject.EndTimeMilliseconds + 5000,
                                    HitObjectKind.Hold)
                                : hitObject),
                        new YokkoHitObject(
                            0,
                            350,
                            null,
                            HitObjectKind.Mine),
                        new YokkoHitObject(
                            1,
                            450,
                            null,
                            HitObjectKind.Sample),
                    ],
                });

        Assert.That(withExtras.Skillsets, Is.EqualTo(baseline.Skillsets));
    }

    [Test]
    public void InputOrderDoesNotChangeResultOrCacheKey()
    {
        YokkoBeatmap chart = createChart();
        YokkoBeatmap reversed = chart with
        {
            HitObjects = chart.HitObjects.Reverse().ToArray(),
        };

        Assert.Multiple(() =>
        {
            Assert.That(
                ManiaMsdCalculator.CalculateResult(reversed).Skillsets,
                Is.EqualTo(
                    ManiaMsdCalculator.CalculateResult(chart).Skillsets));
            Assert.That(
                ManiaMsdCalculator.CreateCacheKey(reversed),
                Is.EqualTo(
                    ManiaMsdCalculator.CreateCacheKey(chart)));
        });
    }

    [Test]
    public void InvalidInputFailsSoft()
    {
        YokkoBeatmap chart = createChart();
        ManiaMsdResult invalidLane =
            ManiaMsdCalculator.CalculateResult(
                chart with
                {
                    HitObjects =
                    [
                        .. chart.HitObjects,
                        new YokkoHitObject(
                            4,
                            9000,
                            null,
                            HitObjectKind.Tap),
                    ],
                });
        ManiaMsdResult invalidRate =
            ManiaMsdCalculator.CalculateResult(chart, 0);
        ManiaMsdResult tooShort =
            ManiaMsdCalculator.CalculateResult(
                chart with
                {
                    HitObjects =
                    [
                        new YokkoHitObject(
                            0,
                            0,
                            null,
                            HitObjectKind.Tap),
                    ],
                });

        Assert.Multiple(() =>
        {
            Assert.That(
                invalidLane.Status,
                Is.EqualTo(ManiaMsdStatus.InvalidLane));
            Assert.That(
                invalidRate.Status,
                Is.EqualTo(ManiaMsdStatus.InvalidRate));
            Assert.That(
                tooShort.Status,
                Is.EqualTo(ManiaMsdStatus.TooFewRows));
        });
    }

    [Test]
    public void PresentationNamesEtternaAndDominantSkillset()
    {
        var result = new ManiaMsdResult(
            ManiaMsdStatus.Success,
            new EtternaMsdValues(
                18.24,
                10,
                12,
                14,
                13,
                16,
                15,
                18),
            1,
            ManiaMsdCalculator.AlgorithmIdentifier);

        Assert.Multiple(() =>
        {
            Assert.That(
                ManiaMsdPresentation.FormatValue(result),
                Is.EqualTo("18.24"));
            Assert.That(
                ManiaMsdPresentation.Qualifier(result),
                Is.EqualTo("ETTERNA MSD · TECH"));
        });
    }

    private static YokkoBeatmap createChart()
    {
        YokkoHitObject[] notes = Enumerable.Range(0, 96)
            .Select(index =>
            {
                int lane = index % 4;
                double time = index * 125d;
                return index % 12 == 0
                    ? new YokkoHitObject(
                        lane,
                        time,
                        time + 500,
                        HitObjectKind.Hold)
                    : new YokkoHitObject(
                        lane,
                        time,
                        null,
                        HitObjectKind.Tap);
            })
            .ToArray();

        return new YokkoBeatmap(
            "Etterna MSD fixture",
            "Yokko",
            "Tests",
            "4K",
            KeyMode.FourKey,
            ChartSourceFormat.Etterna,
            [],
            null,
            notes);
    }
}
