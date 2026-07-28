using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Platform;
using Yokko.Game.Configuration;
using Yokko.Game.Skinning.OsuMania;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class OsuManiaSkinLibraryTest
{
    private string testRoot;

    [SetUp]
    public void SetUp()
    {
        testRoot = Path.Combine(
            Path.GetTempPath(),
            $"yokko-skin-library-{Guid.NewGuid():N}");
        Directory.CreateDirectory(testRoot);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(testRoot))
            Directory.Delete(testRoot, true);
    }

    [Test]
    public void ImportOskAddsAndSelectsSkin()
    {
        string package = createSkinPackage("Arrow Test", "Skin Author", 4, "arrow.osk");
        var settings = new YokkoSkinSettings();
        var library = createLibrary(settings);

        SkinImportResult result = library.Import(package);

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(result.Skin?.Name, Is.EqualTo("Arrow Test"));
            Assert.That(result.Skin?.KeyModes, Is.EquivalentTo(new[] { 4 }));
            Assert.That(library.GetInstalledSkins(), Has.Count.EqualTo(1));
            Assert.That(library.IsSelected(result.Skin!.Id), Is.True);
            Assert.That(library.CurrentSkinPath, Is.EqualTo(result.Skin.FullPath));
            Assert.That(File.Exists(result.Skin.FullPath), Is.True);
        });
    }

    [Test]
    public void DuplicatePackagesAreKeptAsSeparateSkins()
    {
        string package = createSkinPackage("Throw Skin", "投皮测试", 7, "throw.osk");
        var library = createLibrary(new YokkoSkinSettings());

        SkinImportResult first = library.Import(package);
        SkinImportResult second = library.Import(package);

        Assert.Multiple(() =>
        {
            Assert.That(first.Success, Is.True);
            Assert.That(second.Success, Is.True);
            Assert.That(second.Skin!.Id, Is.Not.EqualTo(first.Skin!.Id));
            Assert.That(library.GetInstalledSkins(), Has.Count.EqualTo(2));
            Assert.That(library.IsSelected(second.Skin.Id), Is.True);
        });
    }

    [Test]
    public void SelectedSkinPersistsAndDeletingItClearsSelection()
    {
        string package = createSkinPackage("Persistent Skin", "Yokko", 4, "persistent.osk");
        string selectedId;

        using (var config = new YokkoConfigManager(new NativeStorage(testRoot)))
        {
            var settings = new YokkoSkinSettings();
            config.BindSkinSettings(settings);
            var library = createLibrary(settings);
            SkinImportResult imported = library.Import(package);
            Assert.That(imported.Success, Is.True, imported.Message);
            selectedId = imported.Skin!.Id;
        }

        using (var config = new YokkoConfigManager(new NativeStorage(testRoot)))
        {
            var settings = new YokkoSkinSettings();
            config.BindSkinSettings(settings);
            var library = createLibrary(settings);

            Assert.That(library.IsSelected(selectedId), Is.True);
            Assert.That(library.CurrentSkinPath, Is.Not.Null);
            Assert.That(library.Delete(selectedId), Is.True);
            Assert.That(settings.SelectedSkinId.Value, Is.Empty);
            Assert.That(library.CurrentSkinPath, Is.Null);
        }
    }

    [Test]
    public void FolderWithoutManiaSkinIsRejected()
    {
        string folder = Path.Combine(testRoot, "not-a-mania-skin");
        Directory.CreateDirectory(folder);
        File.WriteAllText(
            Path.Combine(folder, "skin.ini"),
            "[General]\nName: Standard only\n");
        var library = createLibrary(new YokkoSkinSettings());

        SkinImportResult result = library.Import(folder);

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Does.Contain("osu!mania"));
            Assert.That(library.GetInstalledSkins(), Is.Empty);
        });
    }

    [Test]
    [Category("Integration")]
    public void ImportsConfiguredRealSkinCorpus()
    {
        string corpus = Environment.GetEnvironmentVariable("YOKKO_OSU_MANIA_SKIN_CORPUS");

        if (string.IsNullOrWhiteSpace(corpus) || !Directory.Exists(corpus))
            Assert.Ignore("Set YOKKO_OSU_MANIA_SKIN_CORPUS to a directory containing real osu! skins.");

        string[] packages = Directory.EnumerateFiles(corpus, "*.osk", SearchOption.TopDirectoryOnly)
                                     .Concat(Directory.EnumerateDirectories(corpus))
                                     .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                                     .ToArray();
        var library = createLibrary(new YokkoSkinSettings());

        foreach (string package in packages)
        {
            SkinImportResult result = library.Import(package);
            Assert.That(result.Success, Is.True, $"{Path.GetFileName(package)}: {result.Message}");
        }

        Assert.That(library.GetInstalledSkins(), Has.Count.EqualTo(packages.Length));
        Assert.That(library.CurrentSkinPath, Is.Not.Null);
    }

    private OsuManiaSkinLibrary createLibrary(YokkoSkinSettings settings)
    {
        var library = new OsuManiaSkinLibrary();
        library.Initialise(new NativeStorage(testRoot), settings);
        return library;
    }

    private string createSkinPackage(
        string name,
        string author,
        int keys,
        string filename)
    {
        string sourceDirectory = Path.Combine(testRoot, "source");
        Directory.CreateDirectory(sourceDirectory);
        string package = Path.Combine(sourceDirectory, filename);

        using ZipArchive archive = ZipFile.Open(package, ZipArchiveMode.Create);
        ZipArchiveEntry ini = archive.CreateEntry("skin.ini");

        using StreamWriter writer = new(ini.Open());
        writer.Write(
            $"""
             [General]
             Name: {name}
             Author: {author}
             Version: 2.7

             [Mania]
             Keys: {keys}
             ColumnWidth: 30
             """);

        return package;
    }
}
