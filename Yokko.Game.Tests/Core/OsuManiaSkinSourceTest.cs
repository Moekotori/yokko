using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using NUnit.Framework;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.PixelFormats;
using Yokko.Game.Skinning.OsuMania;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class OsuManiaSkinSourceTest
{
    [Test]
    public void ReadsNestedOskCaseInsensitively()
    {
        string archivePath = createPath("nested.osk");

        using (ZipArchive archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
        {
            writeEntry(archive, "Skin Folder/SKIN.INI", "[General]\nName: Nested");
            writeEntry(archive, "Skin Folder/Custom/NOTE.PNG", "image");
        }

        using var source = new OsuManiaSkinSource(archivePath);

        Assert.That(source.ReadSkinIni(), Does.Contain("Name: Nested"));
        Assert.That(source.Contains("custom/note.png"), Is.True);
        Assert.That(Encoding.UTF8.GetString(source.Get("CUSTOM/NOTE.PNG")), Is.EqualTo("image"));
    }

    [Test]
    public void RejectsUnsafeArchiveEntriesFromLookup()
    {
        string archivePath = createPath("unsafe.osk");

        using (ZipArchive archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
        {
            writeEntry(archive, "skin.ini", "[General]\nName: Safe");
            writeEntry(archive, "../outside.png", "unsafe");
        }

        using var source = new OsuManiaSkinSource(archivePath);

        Assert.That(source.Contains("../outside.png"), Is.False);
        Assert.That(source.Get("../outside.png"), Is.Null);
    }

    [Test]
    public void ResolvesJpegAnimationFrameAtHighResolution()
    {
        string archivePath = createPath("jpeg.osk");

        using (ZipArchive archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
        {
            writeEntry(archive, "skin.ini", "[General]\nName: JPEG");
            writeEntry(archive, "Custom/Note-0@2x.JPEG", "image");
        }

        using var source = new OsuManiaSkinSource(archivePath);

        (string name, bool highResolution) = source.ResolveTextureName(@"custom\note");

        Assert.That(name, Is.EqualTo("custom/note-0@2x.jpeg"));
        Assert.That(highResolution, Is.True);
        Assert.That(source.Get(name), Is.Not.Null);
    }

    [Test]
    public void ResolvesEveryContiguousAnimationFrame()
    {
        string archivePath = createPath("animation.osk");

        using (ZipArchive archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
        {
            writeEntry(archive, "skin.ini", "[General]\nName: Animation");
            writeEntry(archive, "lightingN.png", "static");
            writeEntry(archive, "lightingN-0@2x.png", "frame 0");
            writeEntry(archive, "lightingN-1@2x.png", "frame 1");
            writeEntry(archive, "lightingN-2@2x.png", "frame 2");
        }

        using var source = new OsuManiaSkinSource(archivePath);

        var frames = source.ResolveAnimationTextureNames("lightingN");

        Assert.That(
            frames.Select(frame => frame.Name),
            Is.EqualTo(new[]
            {
                "lightingN-0@2x.png",
                "lightingN-1@2x.png",
                "lightingN-2@2x.png",
            }));
        Assert.That(frames, Is.All.Matches<(string, bool)>(
            frame => frame.Item2));
    }

    [Test]
    public void ResolvesFolderHitSoundCaseInsensitively()
    {
        string directory = Path.GetDirectoryName(
            createPath("skin.ini"))!;
        string expected = Path.Combine(directory, "Soft-HitClap2.OGG");
        File.WriteAllText(Path.Combine(directory, "skin.ini"), "[General]");
        File.WriteAllBytes(expected, [1, 2, 3]);

        using var source = new OsuManiaSkinSource(directory);

        Assert.That(
            source.ResolveAudioPath("soft-hitclap2"),
            Is.EqualTo(expected));
    }

    [Test]
    public void MaterializesPackagedHitSoundForNativeAudio()
    {
        string archivePath = createPath("audio.osk");

        using (ZipArchive archive = ZipFile.Open(
                   archivePath,
                   ZipArchiveMode.Create))
        {
            writeEntry(archive, "skin.ini", "[General]\nName: Audio");
            writeEntry(archive, "Normal-HitNormal.WAV", "sample");
        }

        using var source = new OsuManiaSkinSource(archivePath);
        string resolved = source.ResolveAudioPath("normal-hitnormal");

        Assert.Multiple(() =>
        {
            Assert.That(resolved, Is.Not.Null);
            Assert.That(File.Exists(resolved), Is.True);
            Assert.That(File.ReadAllText(resolved), Is.EqualTo("sample"));
        });
    }

    [Test]
    public void ResizesMislabeledTextureBeyondRendererLimit()
    {
        string directory = Path.GetDirectoryName(
            createPath("hold-body.png"))!;
        string texturePath = Path.Combine(directory, "hold-body.png");

        using (var image = new Image<Rgba32>(10, 100))
            image.Save(texturePath, new TiffEncoder());

        using var store = new ConstrainedTextureResourceStore(
            new OsuManiaSkinSource(directory),
            64);

        byte[] constrained = store.Get("hold-body.png");
        ImageInfo info = Image.Identify(constrained);

        Assert.That(info.Width, Is.LessThanOrEqualTo(64));
        Assert.That(info.Height, Is.EqualTo(64));
        Assert.That(info.Width / (double)info.Height, Is.EqualTo(0.1).Within(0.02));
    }

    private static string createPath(string name)
    {
        string directory = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            "osu-skin",
            TestContext.CurrentContext.Test.ID,
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, name);
    }

    private static void writeEntry(ZipArchive archive, string path, string contents)
    {
        ZipArchiveEntry entry = archive.CreateEntry(path);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(contents);
    }
}
