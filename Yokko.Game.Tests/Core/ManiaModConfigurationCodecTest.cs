using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Yokko.Core.Mods;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class ManiaModConfigurationCodecTest
{
    [Test]
    public void NoPauseAllowanceRoundTrips()
    {
        ManiaModSet original = ManiaModSet.Empty.WithNoPause(3);

        ManiaModSet restored = ManiaModConfigurationCodec.Restore(
            ManiaModConfigurationCodec.Capture(original));

        Assert.That(restored, Is.EqualTo(original));
        Assert.That(restored.NoPauseAllowedPauses, Is.EqualTo(3));
    }

    public static IEnumerable<ManiaModSet> Configurations
    {
        get
        {
            yield return ManiaModSet.Empty;
            yield return ManiaModSet.Empty
                .WithRandomSeed(741852)
                .WithCover(0.63, ManiaCoverDirection.AgainstScroll)
                .WithDifficultyAdjust(10.5, -3, true)
                .WithMuted(true, false, 250, false)
                .WithPerfect(true)
                .WithFixedRate(ManiaModId.HalfTime, 0.83, true);
            yield return ManiaModSet.Empty
                .WithFlashlight(1.75, true)
                .WithAccuracyChallenge(
                    0.972,
                    ManiaAccuracyMode.Standard)
                .WithTimeRamp(
                    ManiaModId.WindUp,
                    0.75,
                    1.8,
                    false);
            yield return ManiaModSet.Empty.WithAdaptiveSpeed(1.35, false);
            yield return ManiaModSet.Empty.WithFixedRate(
                ManiaModId.Nightcore,
                1.72);
        }
    }

    [TestCaseSource(nameof(Configurations))]
    public void EveryConfigurableValueRoundTrips(ManiaModSet expected)
    {
        ManiaModConfigurationEnvelope envelope =
            ManiaModConfigurationCodec.Capture(expected);
        ManiaModSet actual =
            ManiaModConfigurationCodec.Restore(envelope);

        Assert.Multiple(() =>
        {
            Assert.That(
                envelope.SchemaVersion,
                Is.EqualTo(
                    ManiaModConfigurationEnvelope.CurrentSchemaVersion));
            Assert.That(actual, Is.EqualTo(expected));
            Assert.That(actual.Fingerprint, Is.EqualTo(expected.Fingerprint));
        });
    }

    [Test]
    public void UnknownModKeyFailsClosed()
    {
        var envelope = new ManiaModConfigurationEnvelope(
            ManiaModConfigurationEnvelope.CurrentSchemaVersion,
            [new ManiaModConfigurationEntry("future-unknown-mod")]);

        Assert.That(
            () => ManiaModConfigurationCodec.Restore(envelope),
            Throws.TypeOf<InvalidDataException>());
    }

    [Test]
    public void DuplicateModKeyFailsClosed()
    {
        var envelope = new ManiaModConfigurationEnvelope(
            ManiaModConfigurationEnvelope.CurrentSchemaVersion,
            [
                new ManiaModConfigurationEntry("hidden"),
                new ManiaModConfigurationEntry("hidden"),
            ]);

        Assert.That(
            () => ManiaModConfigurationCodec.Restore(envelope),
            Throws.TypeOf<InvalidDataException>());
    }

    [Test]
    public void UnknownSchemaIsNotSilentlyDowngraded()
    {
        var envelope = new ManiaModConfigurationEnvelope(999, []);

        Assert.That(
            () => ManiaModConfigurationCodec.Restore(envelope),
            Throws.TypeOf<NotSupportedException>());
    }
}
