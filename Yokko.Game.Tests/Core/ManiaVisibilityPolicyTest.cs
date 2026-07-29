using System;
using NUnit.Framework;
using Yokko.Core.Mods;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class ManiaVisibilityPolicyTest
{
    [Test]
    public void HiddenCoverageGrowsWithComboAndCapsLikeLazer()
    {
        var mods = new ManiaModSet([ManiaModId.Hidden]);

        ManiaVisibilityPolicy start =
            ManiaVisibilityPolicyResolver.Resolve(mods, 0);
        ManiaVisibilityPolicy capped =
            ManiaVisibilityPolicyResolver.Resolve(mods, 1000);

        Assert.Multiple(() =>
        {
            Assert.That(
                start.Mode,
                Is.EqualTo(ManiaVisibilityMode.Hidden));
            Assert.That(
                start.CoverDirection,
                Is.EqualTo(ManiaCoverDirection.AgainstScroll));
            Assert.That(
                start.Coverage,
                Is.EqualTo(160d / 768).Within(1e-12));
            Assert.That(
                capped.Coverage,
                Is.EqualTo(400d / 768).Within(1e-12));
        });
    }

    [Test]
    public void FadeInUsesTheSameCoverInTheOppositeDirection()
    {
        ManiaVisibilityPolicy policy =
            ManiaVisibilityPolicyResolver.Resolve(
                new ManiaModSet([ManiaModId.FadeIn]),
                0);

        Assert.Multiple(() =>
        {
            Assert.That(
                policy.Mode,
                Is.EqualTo(ManiaVisibilityMode.FadeIn));
            Assert.That(
                policy.CoverDirection,
                Is.EqualTo(ManiaCoverDirection.AlongScroll));
            Assert.That(
                policy.Coverage,
                Is.EqualTo(160d / 768).Within(1e-12));
        });
    }

    [Test]
    public void CoverSettingsAreValidatedAndCanonical()
    {
        ManiaModSet mods = ManiaModSet.Empty.WithCover(
            0.7,
            ManiaCoverDirection.AgainstScroll);
        ManiaVisibilityPolicy policy =
            ManiaVisibilityPolicyResolver.Resolve(mods, 0);

        Assert.Multiple(() =>
        {
            Assert.That(policy.Coverage, Is.EqualTo(0.7));
            Assert.That(
                policy.CoverDirection,
                Is.EqualTo(ManiaCoverDirection.AgainstScroll));
            Assert.That(
                mods.Fingerprint,
                Is.EqualTo("cover:0.7:against"));
            Assert.That(
                () => ManiaModSet.Empty.WithCover(
                    0.1,
                    ManiaCoverDirection.AlongScroll),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        });
    }

    [TestCase(0, 50)]
    [TestCase(99, 50)]
    [TestCase(100, 40.625)]
    [TestCase(200, 31.25)]
    public void FlashlightCanShrinkAtLazerComboThresholds(
        int combo,
        double expectedSize)
    {
        ManiaModSet mods =
            ManiaModSet.Empty.WithFlashlight(1, true);
        ManiaVisibilityPolicy policy =
            ManiaVisibilityPolicyResolver.Resolve(mods, combo);

        Assert.That(policy.FlashlightSize, Is.EqualTo(expectedSize));
    }

    [Test]
    public void VisibilityModsReplaceEachOtherAndInvalidSetsFail()
    {
        ManiaModSet mods = ManiaModSet.Empty
                                       .With(ManiaModId.Hidden, true)
                                       .With(ManiaModId.Flashlight, true);

        Assert.Multiple(() =>
        {
            Assert.That(
                mods.Contains(ManiaModId.Hidden),
                Is.False);
            Assert.That(
                mods.Contains(ManiaModId.Flashlight),
                Is.True);
            Assert.That(
                () => new ManiaModSet(
                [
                    ManiaModId.Hidden,
                    ManiaModId.Flashlight,
                ]),
                Throws.TypeOf<ArgumentException>());
        });
    }
}
