using System;
using NUnit.Framework;
using Yokko.Core.Mods;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class ManiaModSetTest
{
    [Test]
    public void CanonicalSetIsStableAndDeduplicated()
    {
        var mods = new ManiaModSet(
        [
            ManiaModId.Autoplay,
            ManiaModId.DoubleTime,
            ManiaModId.Autoplay,
        ]);

        Assert.Multiple(() =>
        {
            Assert.That(
                mods.Mods,
                Is.EqualTo(new[]
                {
                    ManiaModId.DoubleTime,
                    ManiaModId.Autoplay,
                }));
            Assert.That(mods.Acronyms, Is.EqualTo(new[] { "DT", "AT" }));
            Assert.That(mods.Fingerprint, Is.EqualTo("double-time+autoplay"));
            Assert.That(mods.PlaybackRate, Is.EqualTo(1.5));
            Assert.That(mods.ChangesAudioPitch, Is.False);
            Assert.That(mods.IsAutomation, Is.True);
        });
    }

    [Test]
    public void EnablingNightcoreReplacesDoubleTime()
    {
        ManiaModSet mods = ManiaModSet.Empty
                                            .With(
                                                ManiaModId.DoubleTime,
                                                true)
                                            .With(
                                                ManiaModId.Nightcore,
                                                true);

        Assert.Multiple(() =>
        {
            Assert.That(mods.Contains(ManiaModId.DoubleTime), Is.False);
            Assert.That(mods.Contains(ManiaModId.Nightcore), Is.True);
            Assert.That(mods.PlaybackRate, Is.EqualTo(1.5));
            Assert.That(mods.ChangesAudioPitch, Is.True);
        });
    }

    [Test]
    public void InvalidRateCombinationFailsClosed()
    {
        Assert.That(
            () => new ManiaModSet(
            [
                ManiaModId.DoubleTime,
                ManiaModId.Nightcore,
            ]),
            Throws.TypeOf<ArgumentException>());
    }

    [Test]
    public void HalfTimeAndDaycoreUseTheSlowRateFamily()
    {
        ManiaModSet mods = ManiaModSet.Empty
                                       .With(ManiaModId.HalfTime, true);

        Assert.Multiple(() =>
        {
            Assert.That(mods.PlaybackRate, Is.EqualTo(0.75));
            Assert.That(mods.ChangesAudioPitch, Is.False);
        });

        mods = mods.With(ManiaModId.Daycore, true);
        Assert.Multiple(() =>
        {
            Assert.That(mods.Contains(ManiaModId.HalfTime), Is.False);
            Assert.That(mods.Contains(ManiaModId.Daycore), Is.True);
            Assert.That(mods.PlaybackRate, Is.EqualTo(0.75));
            Assert.That(mods.ChangesAudioPitch, Is.True);
        });
    }

    [Test]
    public void RandomSeedIsPartOfCanonicalIdentity()
    {
        ManiaModSet first =
            ManiaModSet.Empty.WithRandomSeed(123456);
        ManiaModSet same =
            ManiaModSet.Empty.WithRandomSeed(123456);
        ManiaModSet different =
            ManiaModSet.Empty.WithRandomSeed(654321);

        Assert.Multiple(() =>
        {
            Assert.That(first.RandomSeed, Is.EqualTo(123456));
            Assert.That(first.Fingerprint, Is.EqualTo("random:123456"));
            Assert.That(first, Is.EqualTo(same));
            Assert.That(first, Is.Not.EqualTo(different));
        });
    }

    [Test]
    public void NoReleaseAndHoldOffReplaceEachOther()
    {
        ManiaModSet mods = ManiaModSet.Empty
                                       .With(ManiaModId.NoRelease, true)
                                       .With(ManiaModId.HoldOff, true);

        Assert.Multiple(() =>
        {
            Assert.That(mods.Contains(ManiaModId.HoldOff), Is.True);
            Assert.That(mods.Contains(ManiaModId.NoRelease), Is.False);
            Assert.That(
                () => new ManiaModSet(
                [
                    ManiaModId.NoRelease,
                    ManiaModId.HoldOff,
                ]),
                Throws.TypeOf<ArgumentException>());
        });
    }
}
