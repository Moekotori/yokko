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

    [Test]
    public void UnifiedPresentationFollowsSelectedMode()
    {
        var ratings = new ManiaDifficultyRatings(
            new ManiaMsdResult(
                ManiaMsdStatus.Success,
                new EtternaMsdValues(
                    12.5,
                    9,
                    10,
                    11,
                    8,
                    7,
                    6,
                    12),
                1,
                ManiaMsdCalculator.AlgorithmIdentifier),
            new ManiaStarRatingResult(
                ManiaStarRatingStatus.Success,
                4.2,
                1,
                ManiaStarRatingCalculator
                    .AlgorithmIdentifier));

        Assert.Multiple(() =>
        {
            Assert.That(
                ManiaDifficultyPresentation.FormatInline(
                    ratings,
                    ManiaDifficultyRatingMode.EtternaMsd),
                Is.EqualTo("12.50 MSD"));
            Assert.That(
                ManiaDifficultyPresentation.FormatInline(
                    ratings,
                    ManiaDifficultyRatingMode.RebirthStars),
                Is.EqualTo("4.20 STAR"));
        });
    }
}
