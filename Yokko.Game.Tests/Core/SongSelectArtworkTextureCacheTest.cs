using System;
using System.IO;
using NUnit.Framework;
using osu.Framework.Graphics.Rendering.Dummy;
using osu.Framework.Graphics.Textures;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Yokko.Game.Screens.SongSelect;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class SongSelectArtworkTextureCacheTest
{
    [Test]
    public void GetCachedNeverDecodes()
    {
        string path = createArtwork(64, 64);
        using var cache = new SongSelectArtworkTextureCache();
        var renderer = new DummyRenderer();

        Texture beforeDecode = cache.GetCached(path, renderer);

        Assert.Multiple(() =>
        {
            Assert.That(beforeDecode, Is.Null);
            Assert.That(cache.IsCached(path), Is.False);
            Assert.That(cache.CachedArtworkCount, Is.Zero);
        });

        using Texture decoded = cache.Get(path, renderer);
        using Texture afterDecode = cache.GetCached(path, renderer);

        Assert.Multiple(() =>
        {
            Assert.That(decoded, Is.Not.Null);
            Assert.That(afterDecode, Is.Not.Null);
            Assert.That(cache.IsCached(path), Is.True);
        });
    }

    [Test]
    public void OversizedArtworkIsBoundedToThumbnailSize()
    {
        string path = createArtwork(1920, 1080);
        using var cache = new SongSelectArtworkTextureCache();
        var renderer = new DummyRenderer();

        using Texture decoded = cache.Get(path, renderer);

        Assert.Multiple(() =>
        {
            Assert.That(decoded, Is.Not.Null);
            Assert.That(
                decoded.Width,
                Is.LessThanOrEqualTo(
                    SongSelectArtworkTextureCache.MaximumThumbnailDimension));
            Assert.That(
                decoded.Height,
                Is.LessThanOrEqualTo(
                    SongSelectArtworkTextureCache.MaximumThumbnailDimension));
        });
    }

    private static string createArtwork(int width, int height)
    {
        string directory = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            "artwork-cache",
            TestContext.CurrentContext.Test.ID,
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "artwork.png");
        using var image = new Image<Rgba32>(
            width,
            height,
            new Rgba32(40, 90, 170));
        image.SaveAsPng(path);
        return path;
    }
}
