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
            Assert.That(
                library.LibraryPath,
                Does.EndWith(Path.Combine("Resources", "Skins")));
        });
    }

    [Test]
    public void ExistingTopLevelSkinIsMigratedIntoResourceDirectory()
    {
        string legacySkin = Path.Combine(testRoot, "Skins", "legacy");
        Directory.CreateDirectory(legacySkin);
        File.WriteAllText(
            Path.Combine(legacySkin, "skin.ini"),
            """
            [General]
            Name: Legacy

            [Mania]
            Keys: 4
            """);

        var library = createLibrary(new YokkoSkinSettings());

        Assert.Multiple(() =>
        {
            Assert.That(library.GetInstalledSkins().Single().Name, Is.EqualTo("Legacy"));
            Assert.That(Directory.Exists(legacySkin), Is.False);
            Assert.That(
                Directory.Exists(Path.Combine(library.LibraryPath, "legacy")),
                Is.True);
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
            settings.LongNoteCutAmount.Value = 1.3;
        }

        using (var config = new YokkoConfigManager(new NativeStorage(testRoot)))
        {
            var settings = new YokkoSkinSettings();
            config.BindSkinSettings(settings);
            var library = createLibrary(settings);

            Assert.That(library.IsSelected(selectedId), Is.True);
            Assert.That(library.CurrentSkinPath, Is.Not.Null);
            Assert.That(settings.LongNoteCutAmount.Value, Is.EqualTo(1.3));
            Assert.That(library.Delete(selectedId), Is.True);
            Assert.That(settings.SelectedSkinId.Value, Is.Empty);
            Assert.That(library.CurrentSkinPath, Is.Null);
        }
    }

    [Test]
    public void StandardOsuSkinWithoutManiaSectionIsAccepted()
    {
        string folder = Path.Combine(testRoot, "standard-osu-skin");
        Directory.CreateDirectory(folder);
        File.WriteAllText(
            Path.Combine(folder, "skin.ini"),
            "[General]\nName: Standard only\n");
        var library = createLibrary(new YokkoSkinSettings());

        SkinImportResult result = library.Import(folder);

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(result.Skin?.Name, Is.EqualTo("Standard only"));
            Assert.That(result.Skin?.KeyModes, Is.Empty);
            Assert.That(library.GetInstalledSkins(), Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void FolderWithoutSkinIniOrSupportedAssetsIsRejected()
    {
        string folder = Path.Combine(testRoot, "not-an-osu-skin");
        Directory.CreateDirectory(folder);
        File.WriteAllText(
            Path.Combine(folder, "README.txt"),
            "This directory has no usable skin resources.");
        var library = createLibrary(new YokkoSkinSettings());

        SkinImportResult result = library.Import(folder);

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.False);
            Assert.That(result.Message, Does.Contain("supported osu! skin assets"));
            Assert.That(library.GetInstalledSkins(), Is.Empty);
        });
    }

    [Test]
    public void AssetsOnlyManiaFolderIsAccepted()
    {
        string folder = Path.Combine(testRoot, "assets-only");
        Directory.CreateDirectory(folder);
        File.WriteAllText(
            Path.Combine(folder, "mania-note1.png"),
            "not decoded during library discovery");
        var library = createLibrary(new YokkoSkinSettings());

        SkinImportResult result = library.Import(folder);

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(result.Skin?.Version, Is.EqualTo("latest"));
            Assert.That(result.Skin?.KeyModes, Is.Empty);
            Assert.That(library.GetInstalledSkins(), Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void WrappedAssetsOnlyFolderResolvesTexturesFromItsContentRoot()
    {
        string folder = Path.Combine(testRoot, "wrapped-assets-only");
        string wrapper = Path.Combine(folder, "downloaded-skin");
        Directory.CreateDirectory(wrapper);
        File.WriteAllText(
            Path.Combine(folder, "README.txt"),
            "A top-level download note must not become the skin root.");
        File.WriteAllText(
            Path.Combine(wrapper, "mania-note1.png"),
            "not decoded during library discovery");
        var library = createLibrary(new YokkoSkinSettings());

        SkinImportResult result = library.Import(folder);

        using var source = new OsuManiaSkinSource(result.Skin!.FullPath);
        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(source.Contains("mania-note1.png"), Is.True);
            Assert.That(source.HasManiaAssets(), Is.True);
        });
    }

    [Test]
    public void FolderImportIgnoresMacMetadataSkinIni()
    {
        string folder = Path.Combine(testRoot, "mac-metadata-wrapper");
        string actual = Path.Combine(folder, "downloaded-skin");
        string metadata = Path.Combine(folder, "__MACOSX", "downloaded-skin");
        Directory.CreateDirectory(actual);
        Directory.CreateDirectory(metadata);
        File.WriteAllText(
            Path.Combine(actual, "skin.ini"),
            """
            [General]
            Name: Actual Skin

            [Mania]
            Keys: 4
            """);
        File.WriteAllText(
            Path.Combine(metadata, "skin.ini"),
            "[General]\nName: Metadata Artifact\n");
        var library = createLibrary(new YokkoSkinSettings());

        SkinImportResult result = library.Import(folder);

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(result.Skin?.Name, Is.EqualTo("Actual Skin"));
            Assert.That(result.Skin?.KeyModes, Is.EqualTo(new[] { 4 }));
        });
    }

    [Test]
    public void WrappedAssetsOnlyPackageResolvesTexturesFromItsContentRoot()
    {
        string package = Path.Combine(testRoot, "wrapped-assets-only.osk");

        using (ZipArchive archive = ZipFile.Open(package, ZipArchiveMode.Create))
        {
            archive.CreateEntry("README.txt");
            archive.CreateEntry("./downloaded-skin/mania-note1.png");
            archive.CreateEntry("__MACOSX/downloaded-skin/._mania-note1.png");
        }

        var library = createLibrary(new YokkoSkinSettings());
        SkinImportResult result = library.Import(package);

        using var source = new OsuManiaSkinSource(result.Skin!.FullPath);
        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(source.Contains("mania-note1.png"), Is.True);
            Assert.That(source.HasManiaAssets(), Is.True);
        });
    }

    [Test]
    public void WrappedScorebarOnlyPackageUsesItsContentRoot()
    {
        string package = Path.Combine(testRoot, "wrapped-scorebar-only.osk");

        using (ZipArchive archive = ZipFile.Open(package, ZipArchiveMode.Create))
        {
            archive.CreateEntry("README.txt");
            archive.CreateEntry("./interface/scorebar-bg.png");
            archive.CreateEntry("./interface/scorebar-colour-0.png");
        }

        var library = createLibrary(new YokkoSkinSettings());
        SkinImportResult result = library.Import(package);

        using var source = new OsuManiaSkinSource(result.Skin!.FullPath);
        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(source.Contains("scorebar-bg.png"), Is.True);
            Assert.That(source.Contains("scorebar-colour-0.png"), Is.True);
            Assert.That(source.HasManiaAssets(), Is.False);
            Assert.That(source.HasSupportedSkinAssets(), Is.True);
        });
    }

    [Test]
    public void UserFolderAlwaysUsesLatestSkinVersion()
    {
        string folder = Path.Combine(testRoot, "User");
        Directory.CreateDirectory(folder);
        File.WriteAllText(
            Path.Combine(folder, "skin.ini"),
            """
            [General]
            Name: User overrides
            Version: 1.0

            [Mania]
            Keys: 4
            """);
        var library = createLibrary(new YokkoSkinSettings());

        SkinImportResult result = library.Import(folder);

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(result.Skin?.Version, Is.EqualTo("latest"));
        });
    }

    [Test]
    public void EmptySkinIniUsesLegacyVersionWhileMissingFileUsesLatest()
    {
        string folder = Path.Combine(testRoot, "empty-skin-ini");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "skin.ini"), string.Empty);
        File.WriteAllText(
            Path.Combine(folder, "mania-note1.png"),
            "not decoded during library discovery");
        var library = createLibrary(new YokkoSkinSettings());

        SkinImportResult result = library.Import(folder);

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(result.Skin?.Version, Is.EqualTo("1.0"));
        });
    }

    [Test]
    public void StandardSkinIniWithManiaAssetsIsAccepted()
    {
        string folder = Path.Combine(testRoot, "standard-with-mania-assets");
        Directory.CreateDirectory(folder);
        File.WriteAllText(
            Path.Combine(folder, "skin.ini"),
            "[General]\nName: Inherited Mania\nVersion: 2.7\n");
        File.WriteAllText(
            Path.Combine(folder, "mania-key1.png"),
            "not decoded during library discovery");
        var library = createLibrary(new YokkoSkinSettings());

        SkinImportResult result = library.Import(folder);

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(result.Skin?.Name, Is.EqualTo("Inherited Mania"));
            Assert.That(result.Skin?.Version, Is.EqualTo("2.7"));
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
        int imported = 0;

        foreach (string package in packages)
        {
            SkinImportResult result = library.Import(package);

            if (!result.Success
                && Directory.Exists(package)
                && !Directory.EnumerateFileSystemEntries(
                    package,
                    "*",
                    SearchOption.AllDirectories).Any())
            {
                continue;
            }

            Assert.That(result.Success, Is.True, $"{Path.GetFileName(package)}: {result.Message}");
            imported++;
        }

        Assert.That(library.GetInstalledSkins(), Has.Count.EqualTo(imported));
        Assert.That(imported, Is.GreaterThan(0));
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
