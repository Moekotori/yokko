using NUnit.Framework;
using Yokko.Core.Scoring;
using Yokko.Game.Screens.Gameplay;

namespace Yokko.Game.Tests.Core;

public class GameplayResultPresentationTest
{
    [Test]
    public void TimingSummaryUsesRealHitErrors()
    {
        GameplayTimingStatistics summary =
            GameplayTimingStatistics.FromHitErrors([-10, 0, 5])!;

        Assert.Multiple(() =>
        {
            Assert.That(summary.EarlyCount, Is.EqualTo(1));
            Assert.That(summary.OnTimeCount, Is.EqualTo(1));
            Assert.That(summary.LateCount, Is.EqualTo(1));
            Assert.That(summary.EarlyAverageMilliseconds, Is.EqualTo(-10));
            Assert.That(summary.LateAverageMilliseconds, Is.EqualTo(5));
            Assert.That(summary.MeanMilliseconds, Is.EqualTo(-5d / 3)
                .Within(0.0001));
            Assert.That(summary.UnstableRate, Is.GreaterThan(0));
        });
    }

    [Test]
    public void TimingSummaryKeepsIndependentLaneStatistics()
    {
        GameplayTimingStatistics summary =
            GameplayTimingStatistics.FromSamples(
            [
                new GameplayTimingSample(0, -12),
                new GameplayTimingSample(0, -8),
                new GameplayTimingSample(1, 4),
                new GameplayTimingSample(1, 8),
            ])!;

        Assert.Multiple(() =>
        {
            Assert.That(summary.SampleCount, Is.EqualTo(4));
            Assert.That(summary.Lanes, Has.Count.EqualTo(2));
            Assert.That(summary.Lanes[0].Lane, Is.EqualTo(0));
            Assert.That(summary.Lanes[0].MeanMilliseconds, Is.EqualTo(-10));
            Assert.That(summary.Lanes[1].Lane, Is.EqualTo(1));
            Assert.That(summary.Lanes[1].MeanMilliseconds, Is.EqualTo(6));
        });
    }

    [Test]
    public void TimingSummaryIsUnavailableWithoutSamples()
    {
        Assert.That(
            GameplayTimingStatistics.FromHitErrors([]),
            Is.Null);
    }

    [Test]
    public void TimingSummaryUsesDisplayPrecisionForOnTimeBoundary()
    {
        GameplayTimingStatistics summary =
            GameplayTimingStatistics.FromHitErrors(
                [-0.051, -0.05, 0.05, 0.051])!;

        Assert.Multiple(() =>
        {
            Assert.That(summary.EarlyCount, Is.EqualTo(1));
            Assert.That(summary.OnTimeCount, Is.EqualTo(2));
            Assert.That(summary.LateCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void TimingInputUsesPhysicalMillisecondsAndDirectInputsOnly()
    {
        var press = new JudgementEvent(
            0,
            0,
            1000,
            1015,
            15,
            JudgementRating.Perfect,
            JudgementPhase.Tap);
        var body = press with { Phase = JudgementPhase.HoldBody };

        Assert.Multiple(() =>
        {
            Assert.That(
                GameplayTimingStatistics.TryGetRealInputError(
                    press,
                    1.5,
                    out double realError),
                Is.True);
            Assert.That(realError, Is.EqualTo(10).Within(0.0001));
            Assert.That(
                GameplayTimingStatistics.TryGetRealInputError(
                    body,
                    1.5,
                    out _),
                Is.False);
        });
    }
}
