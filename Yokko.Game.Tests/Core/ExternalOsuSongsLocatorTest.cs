using System;
using System.IO;
using NUnit.Framework;
using Yokko.Game.Importing;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class ExternalOsuSongsLocatorTest
{
    [Test]
    public void FindsDefaultStableSongsDirectory()
    {
        string root = createTestRoot("default");
        string songs = Path.Combine(root, "osu!", "Songs");
        Directory.CreateDirectory(songs);

        try
        {
            Assert.That(
                ExternalOsuSongsLocator.Find(null, root),
                Is.EqualTo(Path.GetFullPath(songs)));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Test]
    public void ReadsRelativeBeatmapDirectoryFromStableConfig()
    {
        string root = createTestRoot("relative-config");
        string osuRoot = Path.Combine(root, "osu!");
        string songs = Path.Combine(osuRoot, "Custom Beatmaps");
        Directory.CreateDirectory(songs);
        File.WriteAllText(
            Path.Combine(osuRoot, "osu!.player.cfg"),
            "Username = player\nBeatmapDirectory = Custom Beatmaps\n");

        try
        {
            Assert.That(
                ExternalOsuSongsLocator.Find(null, root),
                Is.EqualTo(Path.GetFullPath(songs)));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Test]
    public void ReadsAbsoluteBeatmapDirectoryAndFallsBackFromMissingSavedPath()
    {
        string root = createTestRoot("absolute-config");
        string osuRoot = Path.Combine(root, "osu!");
        string songs = Path.Combine(root, "External Songs");
        Directory.CreateDirectory(osuRoot);
        Directory.CreateDirectory(songs);
        File.WriteAllText(
            Path.Combine(osuRoot, "osu!.player.cfg"),
            $"BeatmapDirectory = \"{songs}\"\n");

        try
        {
            Assert.That(
                ExternalOsuSongsLocator.Find(
                    Path.Combine(root, "Missing", "Songs"),
                    root),
                Is.EqualTo(Path.GetFullPath(songs)));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static string createTestRoot(string name) => Path.Combine(
        TestContext.CurrentContext.WorkDirectory,
        "external-osu-locator-tests",
        $"{name}-{Guid.NewGuid():N}");
}
