using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
    private static readonly string[] ui_font_assets =
    {
        "Fonts/PlusJakartaSans/PlusJakartaSans",
        "Fonts/Noto/Noto-Basic",
        "Fonts/Noto/Noto-Bopomofo",
        "Fonts/Noto/Noto-CJK-Basic",
        "Fonts/Noto/Noto-CJK-Compatibility",
        "Fonts/Noto/Noto-Hangul",
        "Fonts/Noto/Noto-Thai",
    };

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
        Assert.That(HomeTypography.Body(16).Weight, Is.EqualTo("Regular"));
        Assert.That(HomeTypography.Display(16).Weight, Is.EqualTo("SemiBold"));
        Assert.That(HomeTypography.SearchInput(16).Weight, Is.Null);
        Assert.That(HomeTypography.Hero(72).Weight, Is.EqualTo("Bold"));
        Assert.That(HomeTypography.Sticker(16).Weight, Is.EqualTo("Bold"));
        Assert.That(HomeTypography.Control().Weight, Is.EqualTo("SemiBold"));
        Assert.That(HomeTypography.Control().Size, Is.EqualTo(20));
        Assert.That(HomeTypography.Control(14).Size, Is.EqualTo(18));
        Assert.That(HomeTypography.Display(6).Size, Is.EqualTo(18));
        Assert.That(HomeTypography.Body(16).Size, Is.EqualTo(20));
        Assert.That(HomeTypography.Display(11).Size, Is.EqualTo(18));
        Assert.That(HomeTypography.Display(12).Size, Is.EqualTo(18));
        Assert.That(HomeTypography.Display(14).Size, Is.EqualTo(18));
        Assert.That(HomeTypography.Display(18).Size, Is.EqualTo(22));
        Assert.That(HomeTypography.Display(22).Size, Is.EqualTo(28));
        Assert.That(HomeTypography.Brand(58).Size, Is.EqualTo(64));
        Assert.That(HomeTypography.Hero(72).Size, Is.EqualTo(80));
    }

    [Test]
    public async Task UiFontContainsLocalisationAndExternalText()
    {
        using var resources = new DllResourceStore(typeof(YokkoResources).Assembly);
        IGlyphStore[] glyphStores = await loadUiGlyphStores(resources);
        char[] missing = YokkoLocale.SUPPORTED
                                    .SelectMany(locale => YokkoStrings.ForLocale(locale).Values)
                                    .SelectMany(value => value)
                                    .Concat(YokkoStrings.ExternalTextGlyphs)
                                    .Where(character => !glyphStores.Any(store => store.HasGlyph(character)))
                                    .Distinct()
                                    .OrderBy(character => character)
                                    .ToArray();

        Assert.That(
            missing,
            Is.Empty,
            "Regenerate Yokko's UI fonts with " +
            $"`python scripts/generate-localisation-font.py`; missing: {new string(missing)}");
    }

    [Test]
    public async Task UiFontCoversRepresentativeImportedMetadata()
    {
        using var resources = new DllResourceStore(typeof(YokkoResources).Assembly);
        IGlyphStore[] glyphStores = await loadUiGlyphStores(resources);
        const string metadata =
            "中文輸入検索カタカナひらがな語り한국어제목РусскийΕλληνικα♪→★";

        Assert.That(
            metadata.Where(character => !glyphStores.Any(store => store.HasGlyph(character))),
            Is.Empty,
            "UI and imported metadata must render without replacement glyphs.");
    }

    [Test]
    public async Task UiFontKeepsCompleteEastAsianCoverage()
    {
        using var resources = new DllResourceStore(typeof(YokkoResources).Assembly);
        IGlyphStore[] glyphStores = await loadUiGlyphStores(resources);

        Assert.Multiple(() =>
        {
            Assert.That(countRange(glyphStores, 0x3040, 0x309f), Is.GreaterThanOrEqualTo(90), "Hiragana");
            Assert.That(countRange(glyphStores, 0x30a0, 0x30ff), Is.GreaterThanOrEqualTo(90), "Katakana");
            Assert.That(countRange(glyphStores, 0x4e00, 0x9fff), Is.GreaterThanOrEqualTo(20_900), "CJK unified ideographs");
            Assert.That(countRange(glyphStores, 0xac00, 0xd7a3), Is.EqualTo(11_172), "modern Hangul syllables");
        });
    }

    [Test]
    public async Task ChineseGlyphsUseLazerNotoStoresRatherThanLatinAtlas()
    {
        using var resources = new DllResourceStore(typeof(YokkoResources).Assembly);
        using Stream latinDescriptor = resources.GetStream(
            "Fonts/PlusJakartaSans/PlusJakartaSans.bin");
        HashSet<int> latinGlyphs = readGlyphCodepoints(latinDescriptor);
        IGlyphStore[] glyphStores = await loadUiGlyphStores(resources);
        const string representativeChinese = "中文游戏设置成绩谱面";

        Assert.Multiple(() =>
        {
            Assert.That(
                representativeChinese.All(character => !latinGlyphs.Contains(character)),
                Is.True,
                "The Plus Jakarta Sans atlas must stay Latin-only.");
            Assert.That(
                representativeChinese.All(character => glyphStores.Skip(1).Any(store => store.HasGlyph(character))),
                Is.True,
                "Chinese glyphs must come from the osu!lazer Noto stores.");
        });
    }

    [Test]
    public void UiFontAtlasStaysMemoryBounded()
    {
        using var resources = new DllResourceStore(typeof(YokkoResources).Assembly);
        long compressedBytes = 0;
        string[] fontNames =
        {
            "PlusJakartaSans-Regular",
            "PlusJakartaSans",
            "PlusJakartaSans-SemiBold",
            "PlusJakartaSans-Bold",
        };

        foreach (string fontName in fontNames)
        {
            using Stream descriptor = resources.GetStream(
                $"Fonts/PlusJakartaSans/{fontName}.bin");
            Assert.That(descriptor, Is.Not.Null, $"Missing {fontName} descriptor.");
            FontAtlasSummary atlas = readFontAtlasSummary(descriptor);
            string resourceName = $"Fonts/PlusJakartaSans/{fontName}_0.png";
            using Stream pageStream = resources.GetStream(resourceName);

            Assert.Multiple(() =>
            {
                Assert.That(atlas.SourceSize, Is.EqualTo(64), fontName);
                Assert.That(atlas.PageCount, Is.EqualTo(1), fontName);
                Assert.That(
                    (long)atlas.Width * atlas.Height * 4,
                    Is.LessThanOrEqualTo(16L * 1024 * 1024),
                    fontName);
                Assert.That(pageStream, Is.Not.Null, resourceName);
            });
            compressedBytes += pageStream.Length;
        }

        Assert.That(compressedBytes, Is.LessThanOrEqualTo(512L * 1024));
    }

    private static int countRange(
        IReadOnlyCollection<IGlyphStore> glyphStores,
        int start,
        int end) =>
        Enumerable.Range(start, end - start + 1)
                  .Count(codepoint => glyphStores.Any(store => store.HasGlyph((char)codepoint)));

    private static async Task<IGlyphStore[]> loadUiGlyphStores(
        DllResourceStore resources)
    {
        var aggregateResources = new ResourceStore<byte[]>(resources);
        IGlyphStore[] stores = ui_font_assets
            .Select(asset => (IGlyphStore)new GlyphStore(aggregateResources, asset))
            .ToArray();
        await Task.WhenAll(stores.Select(store => store.LoadFontAsync()));
        return stores;
    }

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
