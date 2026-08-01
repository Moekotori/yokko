using System;
using System.IO;
using System.IO.Compression;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Textures;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Yokko.Game.Screens.Gameplay;
using Yokko.Game.Skinning.OsuMania;

namespace Yokko.Game.Tests.Visual;

[TestFixture]
public partial class TestSceneGameplaySkinCache : YokkoTestScene
{
    [Resolved]
    private IRenderer renderer { get; set; }

    private string skinPath;
    private OsuManiaSkinCache cache;
    private OsuManiaSkin firstSkin;
    private Texture firstTexture;
    private OsuManiaSkinLease fontLease;
    private OsuScoreFontText accuracyText;

    [Test]
    public void TestReusesSkinAndTextureAcrossGameplayLeases()
    {
        AddStep("create packaged skin", () =>
        {
            skinPath = Path.Combine(
                TestContext.CurrentContext.WorkDirectory,
                $"yokko-gameplay-cache-{Guid.NewGuid():N}.osk");
            createSkin(skinPath);
            cache = new OsuManiaSkinCache();
        });
        AddStep("load first gameplay lease", () =>
        {
            using OsuManiaSkinLease lease = cache.Acquire(
                skinPath,
                4,
                renderer);
            firstSkin = lease.Skin;
            firstTexture = lease.Skin.GetTexture("mania-note1");
        });
        AddAssert("first texture loaded", () => firstTexture != null);
        AddStep("load second gameplay lease", () =>
        {
            using OsuManiaSkinLease lease = cache.Acquire(
                skinPath,
                4,
                renderer);
            Assert.That(lease.Skin, Is.SameAs(firstSkin));
            Assert.That(
                lease.Skin.GetTexture("mania-note1"),
                Is.SameAs(firstTexture));
        });
        AddAssert("one skin retained", () => cache.RetainedCount == 1);
    }

    [Test]
    public void TestAccuracyUsesCompleteSkinScoreFont()
    {
        AddStep("create skin score font", () =>
        {
            skinPath = Path.Combine(
                TestContext.CurrentContext.WorkDirectory,
                $"yokko-accuracy-font-{Guid.NewGuid():N}.osk");
            createSkin(skinPath);
            cache = new OsuManiaSkinCache();
        });
        AddStep("load skin score font", () =>
        {
            fontLease = cache.Acquire(skinPath, 4, renderer);
            accuracyText = new OsuScoreFontText(fontLease.Skin);
            accuracyText.SetText("98.76%");
        });
        AddAssert("accuracy uses skin score font", () =>
            accuracyText.UsesSkinFont
            && accuracyText.DisplayedText == "98.76%");
    }

    [Test]
    public void TestAccuracyFallsBackFromMalformedSkinScoreFont()
    {
        AddStep("create malformed skin score font", () =>
        {
            skinPath = Path.Combine(
                TestContext.CurrentContext.WorkDirectory,
                $"yokko-malformed-accuracy-font-{Guid.NewGuid():N}.osk");
            createSkin(skinPath, malformedPercent: true);
            cache = new OsuManiaSkinCache();
        });
        AddStep("load malformed skin score font", () =>
        {
            fontLease = cache.Acquire(skinPath, 4, renderer);
            accuracyText = new OsuScoreFontText(fontLease.Skin);
            accuracyText.SetText("98.76%");
        });
        AddAssert("accuracy uses readable fallback", () =>
            !accuracyText.UsesSkinFont
            && accuracyText.DisplayedText == "98.76%");
    }

    [TearDown]
    public void TearDown()
    {
        fontLease?.Dispose();
        cache?.Dispose();
        if (skinPath != null && File.Exists(skinPath))
            File.Delete(skinPath);
    }

    private static void createSkin(
        string path,
        bool malformedPercent = false)
    {
        using var image = new Image<Rgba32>(8, 8, new Rgba32(80, 220, 255));
        using var imageBytes = new MemoryStream();
        image.SaveAsPng(imageBytes);

        using var malformedImage = new Image<Rgba32>(
            128,
            128,
            new Rgba32(80, 220, 255));
        using var malformedImageBytes = new MemoryStream();
        malformedImage.SaveAsPng(malformedImageBytes);

        using ZipArchive archive = ZipFile.Open(path, ZipArchiveMode.Create);
        writeEntry(
            archive,
            "skin.ini",
            "[General]\nName: Gameplay cache test"
            + "\n[Fonts]\nScorePrefix: custom/accuracy\nScoreOverlap: 2"
            + "\n[Mania]\nKeys: 4\nNoteImage0: mania-note1");
        byte[] textureBytes = imageBytes.ToArray();
        writeTextureEntry(archive, "mania-note1.png", textureBytes);
        for (int digit = 0; digit < 10; digit++)
        {
            writeTextureEntry(
                archive,
                $"custom/accuracy-{digit}.png",
                textureBytes);
        }

        writeTextureEntry(
            archive,
            "custom/accuracy-dot.png",
            textureBytes);
        writeTextureEntry(
            archive,
            "custom/accuracy-percent.png",
            malformedPercent
                ? malformedImageBytes.ToArray()
                : textureBytes);
    }

    private static void writeTextureEntry(
        ZipArchive archive,
        string name,
        byte[] contents)
    {
        ZipArchiveEntry texture = archive.CreateEntry(name);
        using Stream textureStream = texture.Open();
        textureStream.Write(contents);
    }

    private static void writeEntry(
        ZipArchive archive,
        string name,
        string contents)
    {
        ZipArchiveEntry entry = archive.CreateEntry(name);
        using var writer = new StreamWriter(entry.Open());
        writer.Write(contents);
    }
}
