using System;
using NUnit.Framework;
using Yokko.Audio;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class HomeWaveformAnalysisCacheTest
{
    [Test]
    public void EvictsLeastRecentlyUsedEntryBeyondCapacity()
    {
        var cache = new HomeWaveformAnalysisCache(3);
        cache.Store("a.ogg", createAnalysis());
        cache.Store("b.ogg", createAnalysis());
        cache.Store("c.ogg", createAnalysis());

        // 触碰 a 让它变成最近使用，b 成为最久未用。
        Assert.That(cache.TryGet("a.ogg", out _), Is.True);

        cache.Store("d.ogg", createAnalysis());

        Assert.Multiple(() =>
        {
            Assert.That(cache.Count, Is.EqualTo(3));
            Assert.That(cache.Contains("b.ogg"), Is.False);
            Assert.That(cache.Contains("a.ogg"), Is.True);
            Assert.That(cache.Contains("c.ogg"), Is.True);
            Assert.That(cache.Contains("d.ogg"), Is.True);
        });
    }

    [Test]
    public void StoringExistingPathUpdatesValueAndRefreshesRecency()
    {
        var cache = new HomeWaveformAnalysisCache(2);
        AudioWaveformAnalysis replacement = createAnalysis(2_000);
        cache.Store("a.ogg", createAnalysis());
        cache.Store("b.ogg", createAnalysis());

        cache.Store("a.ogg", replacement);
        cache.Store("c.ogg", createAnalysis());

        Assert.Multiple(() =>
        {
            Assert.That(cache.Count, Is.EqualTo(2));
            Assert.That(cache.Contains("b.ogg"), Is.False);
            Assert.That(cache.TryGet("a.ogg", out var analysis), Is.True);
            Assert.That(analysis, Is.SameAs(replacement));
        });
    }

    [Test]
    public void PathLookupIgnoresCaseLikeTrackIndices()
    {
        var cache = new HomeWaveformAnalysisCache(2);
        AudioWaveformAnalysis stored = createAnalysis();
        cache.Store("Song.OGG", stored);

        cache.Store("song.ogg", createAnalysis(2_000));

        Assert.Multiple(() =>
        {
            Assert.That(cache.Count, Is.EqualTo(1));
            Assert.That(cache.TryGet("SONG.ogg", out var analysis), Is.True);
            Assert.That(analysis.DurationMilliseconds, Is.EqualTo(2_000));
        });
    }

    [Test]
    public void RejectsNonPositiveCapacity() =>
        Assert.Throws<ArgumentOutOfRangeException>(
            () => _ = new HomeWaveformAnalysisCache(0));

    private static AudioWaveformAnalysis createAnalysis(
        double durationMilliseconds = 1_000) =>
        new(durationMilliseconds, [], [], [], []);
}
