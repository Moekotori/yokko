using System;
using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using Yokko.Core.Beatmaps;
using Yokko.Game.Importing;
using Yokko.Game.Screens.Main;
using Yokko.Import;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class HomeMusicPlayerTest
{
    [Test]
    public void PlaylistProjectionDeduplicatesAudioBeforeFilesystemWork()
    {
        string root = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            $"home-playlist-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        string audioPath = Path.Combine(root, "song.ogg");
        File.WriteAllBytes(audioPath, [1, 2, 3]);

        try
        {
            var firstBeatmap = DemoBeatmaps.CreateFourKeyDemo() with
            {
                Title = "First difficulty",
                AudioPath = audioPath,
            };
            var secondBeatmap = firstBeatmap with
            {
                Title = "Second difficulty",
            };
            ImportedChart[] charts =
            [
                new ImportedChart(
                    "first", "first.osu",
                    new ChartImportResult(firstBeatmap, []),
                    null, null, null, "set", "set", true,
                    LengthMilliseconds: 12_345,
                    Bpm: 180),
                new ImportedChart(
                    "second", "second.osu",
                    new ChartImportResult(secondBeatmap, []),
                    null, null, null, "set", "set", true),
                new ImportedChart(
                    "missing", "missing.osu",
                    new ChartImportResult(firstBeatmap with
                    {
                        AudioPath = Path.Combine(root, "missing.mp3"),
                    }, []),
                    null, null, null, "missing", "missing", false),
            ];

            HomeMusicPlayer.ImportedHomeTrack[] projected =
                HomeMusicPlayer.BuildPlaylistProjection(
                    charts,
                    CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(projected, Has.Length.EqualTo(1));
                Assert.That(projected[0].Title, Is.EqualTo("First difficulty"));
                Assert.That(projected[0].AudioPath, Is.EqualTo(audioPath));
                Assert.That(projected[0].Bpm, Is.EqualTo(180));
                Assert.That(projected[0].FallbackLength, Is.EqualTo(12_345));
            });
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Test]
    public void InitialTrackIsSelectedRandomlyWithinPlaylist()
    {
        const int trackCount = 8;
        var random = new Random(20260729);

        int[] selections = Enumerable.Range(0, 16)
                                     .Select(_ =>
                                         HomeMusicPlayer
                                             .ChooseInitialTrackIndex(
                                                 trackCount,
                                                 random))
                                     .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(
                selections,
                Is.All.InRange(0, trackCount - 1));
            Assert.That(
                selections.Distinct().Count(),
                Is.GreaterThan(1));
        });
    }
}
