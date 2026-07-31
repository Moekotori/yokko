using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NUnit.Framework;
using osu.Framework.IO.Stores;
using Yokko.Core.Mods;
using Yokko.Game.Localisation;
using Yokko.Game.Screens.Main;
using Yokko.Resources;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public class LocalisationTest
{
    [Test]
    public void UnsupportedLocalesFallBackToEnglish()
    {
        Assert.That(YokkoLocale.English, Is.EqualTo("en"));
        Assert.That(YokkoLocale.SUPPORTED, Is.EqualTo(new[] { "en", "zh", "ja" }));
        Assert.That(YokkoLocale.Normalize(string.Empty), Is.EqualTo("en"));
        Assert.That(YokkoLocale.Normalize("unsupported"), Is.EqualTo("en"));
        Assert.That(YokkoLocale.Normalize("zh"), Is.EqualTo("zh"));
        Assert.That(YokkoLocale.Normalize("ZH_cn"), Is.EqualTo("zh"));
    }

    [TestCase("zh-CN", YokkoLocale.Chinese)]
    [TestCase("zh-Hant-TW", YokkoLocale.Chinese)]
    [TestCase("ja-JP", YokkoLocale.Japanese)]
    [TestCase("en-US", YokkoLocale.English)]
    [TestCase("fr-FR", YokkoLocale.English)]
    public void FirstLaunchLocaleFollowsSupportedSystemLanguage(
        string cultureName,
        string expected)
    {
        Assert.That(
            YokkoLocale.FromSystemCulture(
                CultureInfo.GetCultureInfo(cultureName)),
            Is.EqualTo(expected));
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

    [Test]
    public void EveryGameplayModHasLocalisedNameAndDescription()
    {
        foreach (ManiaModDefinition definition in OsuManiaModParityCatalog.All)
        {
            Assert.That(
                YokkoStrings.ModName(definition).ToString(),
                Is.Not.Empty,
                $"{definition.Key} name");
            Assert.That(
                YokkoStrings.ModDescription(definition).ToString(),
                Is.Not.Empty,
                $"{definition.Key} description");
        }
    }

    [Test]
    public void LatinTypographyKeepsFrameworkFontMetrics()
    {
        Assert.That(HomeTypography.Body(16).Family, Is.EqualTo("Roboto"));
        Assert.That(HomeTypography.Display(16).Family, Is.EqualTo("Roboto"));
        Assert.That(HomeTypography.Body(16).Size, Is.EqualTo(19));
        Assert.That(HomeTypography.Display(22).Size, Is.EqualTo(25));
        Assert.That(HomeTypography.Hero(72).Size, Is.EqualTo(72));
    }

    [TestCase("Fonts/Yokko/Yokko.bin")]
    [TestCase("Fonts/Yokko/Yokko-Bold.bin")]
    public void LocalisationFontContainsEveryNonAsciiCharacter(string resourceName)
    {
        using var resources = new DllResourceStore(typeof(YokkoResources).Assembly);
        using Stream stream = resources.GetStream(resourceName);

        Assert.That(stream, Is.Not.Null, $"{resourceName} was not embedded.");

        HashSet<int> glyphs = readGlyphCodepoints(stream);
        char[] missing = YokkoLocale.SUPPORTED
                                    .SelectMany(locale => YokkoStrings.ForLocale(locale).Values)
                                    .SelectMany(value => value)
                                    .Concat(YokkoStrings.ExternalTextGlyphs)
                                    .Where(character => character >= 127 && !glyphs.Contains(character))
                                    .Distinct()
                                    .OrderBy(character => character)
                                    .ToArray();

        Assert.That(
            missing,
            Is.Empty,
            "Regenerate Yokko's localisation fonts with " +
            $"`python scripts/generate-localisation-font.py`; missing: {new string(missing)}");
    }

    private static HashSet<int> readGlyphCodepoints(Stream stream)
    {
        using var reader = new BinaryReader(stream);
        Assert.That(reader.ReadBytes(4), Is.EqualTo(new byte[] { (byte)'B', (byte)'M', (byte)'F', 3 }));

        var glyphs = new HashSet<int>();

        while (reader.BaseStream.Position < reader.BaseStream.Length)
        {
            byte blockType = reader.ReadByte();
            uint blockSize = reader.ReadUInt32();
            long blockEnd = reader.BaseStream.Position + blockSize;

            if (blockType == 4)
            {
                while (reader.BaseStream.Position < blockEnd)
                {
                    glyphs.Add((int)reader.ReadUInt32());
                    reader.BaseStream.Seek(16, SeekOrigin.Current);
                }
            }

            reader.BaseStream.Position = blockEnd;
        }

        return glyphs;
    }
}
