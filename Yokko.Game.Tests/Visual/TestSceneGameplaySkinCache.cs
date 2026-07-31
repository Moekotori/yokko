using System;
using System.IO;
using System.IO.Compression;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Textures;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
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

    [TearDown]
    public void TearDown()
    {
        cache?.Dispose();
        if (skinPath != null && File.Exists(skinPath))
            File.Delete(skinPath);
    }

    private static void createSkin(string path)
    {
        using var image = new Image<Rgba32>(8, 8, new Rgba32(80, 220, 255));
        using var imageBytes = new MemoryStream();
        image.SaveAsPng(imageBytes);

        using ZipArchive archive = ZipFile.Open(path, ZipArchiveMode.Create);
        writeEntry(
            archive,
            "skin.ini",
            "[General]\nName: Gameplay cache test\n[Mania]\nKeys: 4\nNoteImage0: mania-note1");
        ZipArchiveEntry texture = archive.CreateEntry("mania-note1.png");
        using Stream textureStream = texture.Open();
        textureStream.Write(imageBytes.ToArray());
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
