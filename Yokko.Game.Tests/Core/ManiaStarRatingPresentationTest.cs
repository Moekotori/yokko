using NUnit.Framework;
using Yokko.Core.Difficulty;
using Yokko.Game.Presentation;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class ManiaStarRatingPresentationTest
{
    [Test]
    public void CompleteRatingKeepsPlainBetaValue()
    {
        var rating = new ManiaStarRatingResult(
            ManiaStarRatingStatus.Success,
            4.2,
            1,
            ManiaStarRatingCalculator.AlgorithmIdentifier);

        Assert.Multiple(() =>
        {
            Assert.That(
                ManiaStarRatingPresentation.FormatValue(rating),
                Is.EqualTo("4.20"));
            Assert.That(
                ManiaStarRatingPresentation.Qualifier(rating),
                Is.EqualTo("BETA"));
        });
    }

    [Test]
    public void PartialRatingNamesItsUnsupportedRules()
    {
        var rating = new ManiaStarRatingResult(
            ManiaStarRatingStatus.Success,
            4.2,
            1,
            ManiaStarRatingCalculator.AlgorithmIdentifier,
            Limitations:
                ManiaStarRatingLimitations.MinesExcluded
                | ManiaStarRatingLimitations.NoReleaseNotModelled
                | ManiaStarRatingLimitations.DynamicRateApproximation);

        Assert.Multiple(() =>
        {
            Assert.That(
                ManiaStarRatingPresentation.FormatValue(rating),
                Is.EqualTo("~4.20"));
            Assert.That(
                ManiaStarRatingPresentation.Qualifier(rating),
                Is.EqualTo("PARTIAL MINE/NR/RATE · BETA"));
        });
    }
}
