using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using NUnit.Framework;
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
