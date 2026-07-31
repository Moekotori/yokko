using System;
using System.IO;
using NUnit.Framework;
using Yokko.Desktop;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class StartupFileArgumentsTest
{
    private string temporaryDirectory;

    [SetUp]
    public void SetUp()
    {
        temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            "YokkoStartupFileArgumentsTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryDirectory);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(temporaryDirectory))
            Directory.Delete(temporaryDirectory, true);
    }

    [Test]
    public void ResolvesExistingSupportedChartsAndRemovesDuplicates()
    {
        string beatmap = Path.Combine(temporaryDirectory, "chart.OSU");
        File.WriteAllText(beatmap, string.Empty);
        string unsupported = Path.Combine(temporaryDirectory, "notes.txt");
        File.WriteAllText(unsupported, string.Empty);

        string[] resolved = StartupFileArguments.Resolve(
        [
            $"\"{beatmap}\"",
            beatmap,
            unsupported,
            Path.Combine(temporaryDirectory, "missing.osu"),
            "",
        ]);

        Assert.That(resolved, Is.EqualTo(new[] { Path.GetFullPath(beatmap) }));
    }

    [Test]
    public void SupportsDedicatedArchiveExtensions()
    {
        foreach (string extension in new[] { ".osz", ".qp", ".mcz", ".smzip" })
            File.WriteAllText(
                Path.Combine(temporaryDirectory, $"chart{extension}"),
                string.Empty);

        string[] resolved = StartupFileArguments.Resolve(
            Directory.GetFiles(temporaryDirectory));

        Assert.That(resolved, Has.Length.EqualTo(4));
    }

    [Test]
    public void OpenWithRegistrationCoversBeatmapsButNotGenericZipArchives()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                WindowsFileAssociationRegistrar.AssociatedExtensions,
                Does.Contain(".osu"));
            Assert.That(
                WindowsFileAssociationRegistrar.AssociatedExtensions,
                Does.Contain(".osz"));
            Assert.That(
                WindowsFileAssociationRegistrar.AssociatedExtensions,
                Does.Contain(".qua"));
            Assert.That(
                WindowsFileAssociationRegistrar.AssociatedExtensions,
                Does.Contain(".sm"));
            Assert.That(
                WindowsFileAssociationRegistrar.AssociatedExtensions,
                Does.Contain(".bms"));
            Assert.That(
                WindowsFileAssociationRegistrar.AssociatedExtensions,
                Does.Not.Contain(".zip"));
        });
    }

    [Test]
    public void OpenCommandQuotesExecutableAndSelectedFile()
    {
        Assert.That(
            WindowsFileAssociationRegistrar.BuildOpenCommand(
                @"C:\Program Files\Yokko\Yokko.exe"),
            Is.EqualTo(
                "\"C:\\Program Files\\Yokko\\Yokko.exe\" \"%1\""));
    }
}
