using NUnit.Framework;
using Yokko.Game.Screens.Gameplay;

namespace Yokko.Game.Tests.Core;

public class GameplayResultPresentationTest
{
    [Test]
    public void TimingSummaryUsesRealHitErrors()
    {
        GameplayTimingSummary summary =
            GameplayTimingSummary.FromHitErrors([-10, 0, 5]);

        Assert.Multiple(() =>
        {
            Assert.That(summary.EarlyCount, Is.EqualTo(1));
            Assert.That(summary.OnTimeCount, Is.EqualTo(1));
            Assert.That(summary.LateCount, Is.EqualTo(1));
            Assert.That(summary.MeanMilliseconds, Is.EqualTo(-5d / 3)
                .Within(0.0001));
            Assert.That(summary.UnstableRate, Is.GreaterThan(0));
        });
    }

    [Test]
    public void TimingSummaryIsUnavailableWithoutSamples()
    {
        Assert.That(
            GameplayTimingSummary.FromHitErrors([]),
            Is.Null);
    }
}
