using System.Linq;
using NUnit.Framework;
using Yokko.Game.Localisation;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public class LocalisationTest
{
    [Test]
    public void EnglishIsTheDefaultLocale()
    {
        Assert.That(YokkoLocale.English, Is.EqualTo("en"));
        Assert.That(YokkoLocale.SUPPORTED, Is.EqualTo(new[] { "en", "zh", "ja" }));
        Assert.That(YokkoLocale.Normalize(string.Empty), Is.EqualTo("en"));
        Assert.That(YokkoLocale.Normalize("unsupported"), Is.EqualTo("en"));
        Assert.That(YokkoLocale.Normalize("zh"), Is.EqualTo("zh"));
    }

    [TestCase(YokkoLocale.English)]
    [TestCase(YokkoLocale.Chinese)]
    [TestCase(YokkoLocale.Japanese)]
    public void EveryLocaleContainsEveryString(string locale)
    {
        var strings = YokkoStrings.ForLocale(locale);

        Assert.That(strings.Keys, Is.EquivalentTo(YokkoStrings.Keys));
        Assert.That(strings.Values.All(value => !string.IsNullOrWhiteSpace(value)), Is.True);
    }
}
