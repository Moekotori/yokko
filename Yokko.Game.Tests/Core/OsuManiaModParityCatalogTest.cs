using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Yokko.Core.Mods;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public class OsuManiaModParityCatalogTest
{
    [Test]
    public void MatchesPinnedUpstreamCategoryShape()
    {
        IReadOnlyList<ManiaModDefinition> definitions =
            OsuManiaModParityCatalog.All;

        Assert.Multiple(() =>
        {
            Assert.That(definitions, Has.Count.EqualTo(41));
            Assert.That(
                definitions.Count(mod =>
                    mod.Category == ManiaModCategory.DifficultyReduction),
                Is.EqualTo(5));
            Assert.That(
                definitions.Count(mod =>
                    mod.Category == ManiaModCategory.DifficultyIncrease),
                Is.EqualTo(11));
            Assert.That(
                definitions.Count(mod =>
                    mod.Category == ManiaModCategory.Conversion),
                Is.EqualTo(18));
            Assert.That(
                definitions.Count(mod =>
                    mod.Category == ManiaModCategory.Automation),
                Is.EqualTo(2));
            Assert.That(
                definitions.Count(mod =>
                    mod.Category == ManiaModCategory.Fun),
                Is.EqualTo(4));
            Assert.That(
                definitions.Count(mod =>
                    mod.Category == ManiaModCategory.System),
                Is.EqualTo(1));
        });
    }

    [Test]
    public void IdentifiersAndAcronymsAreUnique()
    {
        IReadOnlyList<ManiaModDefinition> definitions =
            OsuManiaModParityCatalog.All;

        Assert.Multiple(() =>
        {
            Assert.That(
                definitions.Select(mod => mod.Id),
                Is.Unique);
            Assert.That(
                definitions.Select(mod => mod.Key),
                Is.Unique);
            Assert.That(
                definitions.Select(mod => mod.Acronym),
                Is.Unique);
            Assert.That(
                definitions.Select(mod => mod.Description),
                Has.None.Empty);
        });
    }

    [Test]
    public void LookupUsesStableCaseInsensitiveKey()
    {
        Assert.That(
            OsuManiaModParityCatalog.TryGet(
                "DOUBLE-TIME",
                out ManiaModDefinition definition),
            Is.True);
        Assert.That(definition?.Id, Is.EqualTo(ManiaModId.DoubleTime));
        Assert.That(definition?.Acronym, Is.EqualTo("DT"));
    }
}
