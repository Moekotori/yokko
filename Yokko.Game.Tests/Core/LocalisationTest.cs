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
    public void SharedTypographyMaintainsReadableFontMetrics()
    {
        Assert.That(HomeTypography.Body(16).Family, Is.EqualTo("PlusJakartaSans"));
        Assert.That(HomeTypography.Display(16).Family, Is.EqualTo("PlusJakartaSans"));
        Assert.That(HomeTypography.SearchInput(16).Family, Is.EqualTo("PlusJakartaSans"));
        Assert.That(HomeTypography.Sticker(16).Family, Is.EqualTo("PlusJakartaSans"));
        Assert.That(HomeTypography.Display(16).Weight, Is.Null);
        Assert.That(HomeTypography.Display(6).Size, Is.EqualTo(14));
        Assert.That(HomeTypography.Body(16).Size, Is.EqualTo(20.8f)
            .Within(0.001f));
        Assert.That(HomeTypography.Display(22).Size, Is.EqualTo(27.1f)
            .Within(0.001f));
        Assert.That(HomeTypography.Hero(72).Size, Is.EqualTo(78));
    }

    [TestCase("Fonts/PlusJakartaSans/PlusJakartaSans.bin")]
    public void UiFontContainsLocalisationAndExternalText(string resourceName)
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
            "Regenerate Yokko's UI fonts with " +
            $"`python scripts/generate-localisation-font.py`; missing: {new string(missing)}");
    }

    [TestCase("Fonts/PlusJakartaSans/PlusJakartaSans.bin")]
    public void UiFontCoversRepresentativeImportedMetadata(string resourceName)
    {
        using var resources = new DllResourceStore(typeof(YokkoResources).Assembly);
        using Stream stream = resources.GetStream(resourceName);
        HashSet<int> glyphs = readGlyphCodepoints(stream);
        const string metadata =
            "中文輸入検索カタカナひらがな語り한국어제목РусскийΕλληνικα♪→★";

        Assert.That(
            metadata.Where(character => !glyphs.Contains(character)),
            Is.Empty,
            "UI and imported metadata must render without replacement glyphs.");
    }

    [TestCase("Fonts/PlusJakartaSans/PlusJakartaSans.bin")]
    public void UiFontKeepsCompleteEastAsianCoverage(string resourceName)
    {
        using var resources = new DllResourceStore(typeof(YokkoResources).Assembly);
        using Stream stream = resources.GetStream(resourceName);
        HashSet<int> glyphs = readGlyphCodepoints(stream);

        Assert.Multiple(() =>
        {
            Assert.That(countRange(glyphs, 0x3040, 0x309f), Is.GreaterThanOrEqualTo(90), "Hiragana");
            Assert.That(countRange(glyphs, 0x30a0, 0x30ff), Is.GreaterThanOrEqualTo(90), "Katakana");
            Assert.That(countRange(glyphs, 0x4e00, 0x9fff), Is.GreaterThanOrEqualTo(20_900), "CJK unified ideographs");
            Assert.That(countRange(glyphs, 0xac00, 0xd7a3), Is.EqualTo(11_172), "modern Hangul syllables");
        });
    }

    [Test]
    public void UiFontAtlasStaysMemoryBounded()
    {
        using var resources = new DllResourceStore(typeof(YokkoResources).Assembly);
        using Stream obsoleteBold = resources.GetStream(
            "Fonts/PlusJakartaSans/PlusJakartaSans-Bold.bin");

        Assert.That(
            obsoleteBold,
            Is.Null,
            "Do not embed a second complete CJK weight; it duplicates tens of " +
            "thousands of glyphs in memory.");

        using Stream descriptor = resources.GetStream(
            "Fonts/PlusJakartaSans/PlusJakartaSans.bin");
        FontAtlasSummary atlas = readFontAtlasSummary(descriptor);
        long maximumDecodedPageBytes =
            (long)atlas.Width * atlas.Height * 4;
        long compressedBytes = 0;
        int pageDigits = (atlas.PageCount - 1).ToString().Length;

        for (int page = 0; page < atlas.PageCount; page++)
        {
            string resourceName =
                $"Fonts/PlusJakartaSans/PlusJakartaSans_{page.ToString().PadLeft(pageDigits, '0')}.png";
            using Stream pageStream = resources.GetStream(resourceName);
            Assert.That(pageStream, Is.Not.Null, $"Missing font atlas page {resourceName}.");
            compressedBytes += pageStream.Length;
        }

        Assert.Multiple(() =>
        {
            Assert.That(atlas.SourceSize, Is.EqualTo(64));
            Assert.That(maximumDecodedPageBytes, Is.LessThanOrEqualTo(16L * 1024 * 1024));
            Assert.That(compressedBytes, Is.LessThanOrEqualTo(48L * 1024 * 1024));
        });
    }

    private static int countRange(HashSet<int> glyphs, int start, int end) =>
        glyphs.Count(codepoint => codepoint >= start && codepoint <= end);

    private static FontAtlasSummary readFontAtlasSummary(Stream stream)
    {
        using var reader = new BinaryReader(stream);
        Assert.That(new string(reader.ReadChars(3)), Is.EqualTo("BMF"));
        Assert.That(reader.ReadByte(), Is.EqualTo(3));

        short sourceSize = 0;
        ushort width = 0;
        ushort height = 0;
        ushort pages = 0;

        while (reader.BaseStream.Position < reader.BaseStream.Length)
        {
            byte blockType = reader.ReadByte();
            uint blockLength = reader.ReadUInt32();
            long nextBlock = reader.BaseStream.Position + blockLength;

            if (blockType == 1)
                sourceSize = reader.ReadInt16();
            else if (blockType == 2)
            {
                reader.ReadUInt16();
                reader.ReadUInt16();
                width = reader.ReadUInt16();
                height = reader.ReadUInt16();
                pages = reader.ReadUInt16();
            }

            reader.BaseStream.Position = nextBlock;
        }

        return new FontAtlasSummary(sourceSize, width, height, pages);
    }

    private readonly record struct FontAtlasSummary(
        short SourceSize,
        ushort Width,
        ushort Height,
        ushort PageCount);

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
