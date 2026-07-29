using System.IO;
using NUnit.Framework;
using Yokko.Core.Mods;
using Yokko.Import.Osu;

namespace Yokko.Game.Tests.Core;

/// <summary>
/// Mirrors osu!lazer's ManiaLegacyModConversionTest at
/// 9f227ed28b6c8ba46dfea1f000f778d8b2827ad0 (MIT).
/// </summary>
[TestFixture]
public sealed class OsuLegacyManiaModConverterTest
{
    [TestCase(OsuLegacyMods.NoFail, ManiaModId.NoFail)]
    [TestCase(OsuLegacyMods.Easy, ManiaModId.Easy)]
    [TestCase(OsuLegacyMods.Hidden, ManiaModId.Hidden)]
    [TestCase(OsuLegacyMods.HardRock, ManiaModId.HardRock)]
    [TestCase(OsuLegacyMods.SuddenDeath, ManiaModId.SuddenDeath)]
    [TestCase(OsuLegacyMods.DoubleTime, ManiaModId.DoubleTime)]
    [TestCase(OsuLegacyMods.HalfTime, ManiaModId.HalfTime)]
    [TestCase(OsuLegacyMods.Flashlight, ManiaModId.Flashlight)]
    [TestCase(OsuLegacyMods.Autoplay, ManiaModId.Autoplay)]
    [TestCase(OsuLegacyMods.Key4, ManiaModId.Key4)]
    [TestCase(OsuLegacyMods.Key5, ManiaModId.Key5)]
    [TestCase(OsuLegacyMods.Key6, ManiaModId.Key6)]
    [TestCase(OsuLegacyMods.Key7, ManiaModId.Key7)]
    [TestCase(OsuLegacyMods.Key8, ManiaModId.Key8)]
    [TestCase(OsuLegacyMods.FadeIn, ManiaModId.FadeIn)]
    [TestCase(OsuLegacyMods.Random, ManiaModId.Random)]
    [TestCase(OsuLegacyMods.Key9, ManiaModId.Key9)]
    [TestCase(OsuLegacyMods.KeyCoop, ManiaModId.DualStages)]
    [TestCase(OsuLegacyMods.Key1, ManiaModId.Key1)]
    [TestCase(OsuLegacyMods.Key3, ManiaModId.Key3)]
    [TestCase(OsuLegacyMods.Key2, ManiaModId.Key2)]
    [TestCase(OsuLegacyMods.Mirror, ManiaModId.Mirror)]
    [TestCase(OsuLegacyMods.ScoreV2, ManiaModId.ScoreV2)]
    public void SingleLegacyFlagMatchesLazer(
        OsuLegacyMods legacy,
        ManiaModId expected)
    {
        ManiaModSet mods = OsuLegacyManiaModConverter.Convert(legacy);

        Assert.That(mods.Mods, Is.EqualTo(new[] { expected }));
    }

    [TestCase(
        OsuLegacyMods.Nightcore | OsuLegacyMods.DoubleTime,
        ManiaModId.Nightcore)]
    [TestCase(
        OsuLegacyMods.Perfect | OsuLegacyMods.SuddenDeath,
        ManiaModId.Perfect)]
    [TestCase(
        OsuLegacyMods.Cinema | OsuLegacyMods.Autoplay,
        ManiaModId.Cinema)]
    public void LazerCompositeFlagPrecedenceIsPreserved(
        OsuLegacyMods legacy,
        ManiaModId expected)
    {
        ManiaModSet mods = OsuLegacyManiaModConverter.Convert(legacy);

        Assert.That(mods.Mods, Is.EqualTo(new[] { expected }));
    }

    [Test]
    public void CompatibleLegacyCombinationIsPreserved()
    {
        ManiaModSet mods = OsuLegacyManiaModConverter.Convert(
            OsuLegacyMods.HardRock
            | OsuLegacyMods.DoubleTime);

        Assert.That(
            mods.Mods,
            Is.EquivalentTo(new[]
            {
                ManiaModId.HardRock,
                ManiaModId.DoubleTime,
            }));
    }

    [Test]
    public void ImpossibleLegacyCombinationFailsClosed()
    {
        Assert.That(
            () => OsuLegacyManiaModConverter.Convert(
                OsuLegacyMods.Easy
                | OsuLegacyMods.HardRock),
            Throws.TypeOf<InvalidDataException>());
    }
}
