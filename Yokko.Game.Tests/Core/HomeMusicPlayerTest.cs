using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Yokko.Audio;
using Yokko.Core.Beatmaps;
using Yokko.Game.Audio;
using Yokko.Game.Importing;
using Yokko.Game.Screens.Main;
using Yokko.Game.Screens.SongSelect;
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

    [Test]
    public void AdoptingSongSelectPreviewDoesNotEnableHomeMusicPreference()
    {
        string audioPath = Path.GetFullPath("preview.ogg");
        var beatmap = DemoBeatmaps.CreateFourKeyDemo() with
        {
            AudioPath = audioPath,
        };
        var settings = new YokkoAudioSettings();
        settings.HomeMusicEnabled.Value = false;

        using var player = new HomeMusicPlayer();
        typeof(HomeMusicPlayer)
            .GetProperty(
                "audioSettings",
                BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(player, settings);
        typeof(HomeMusicPlayer)
            .GetField(
                "tracks",
                BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(
                player,
                new HomeMusicPlayer.ImportedHomeTrack[]
                {
                    new(audioPath, "Preview", "Artist", 180, 60_000),
                });
        var trackIndices = (System.Collections.Generic.Dictionary<string, int>)
            typeof(HomeMusicPlayer)
                .GetField(
                    "trackIndices",
                    BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(player)!;
        trackIndices[audioPath] = 0;

        ((ISongSelectPreviewHost)player).AdoptPreview(beatmap);

        Assert.That(settings.HomeMusicEnabled.Value, Is.False);
    }

    [Test]
    public void UpdateReadsEngineStateThroughSingleSnapshot()
    {
        var engine = new CountingAudioEngine();
        using var player = new HomeMusicPlayer();
        setPrivateField(player, "audioEngine", engine);
        setPrivateField(player, "screenActive", true);
        setPrivateField(player, "desiredPlaying", true);
        setPrivateField(
            player,
            "tracks",
            new HomeMusicPlayer.ImportedHomeTrack[]
            {
                new("song.ogg", "Song", "Artist", 180, 60_000),
            });

        typeof(HomeMusicPlayer)
            .GetMethod(
                "Update",
                BindingFlags.Instance
                | BindingFlags.NonPublic
                | BindingFlags.DeclaredOnly)!
            .Invoke(player, null);

        Assert.Multiple(() =>
        {
            Assert.That(engine.StatusReads, Is.EqualTo(1));
            Assert.That(engine.PlaybackTimeReads, Is.EqualTo(1));
        });
    }

    private static void setPrivateField(
        HomeMusicPlayer player,
        string name,
        object value) =>
        typeof(HomeMusicPlayer)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(player, value);

    /// <summary>
    /// 记录 Status 与播放时间被读取的次数；两者在真实引擎上各对应一次
    /// 原生状态查询，用来回归 Update 每帧只取一次快照。
    /// </summary>
    private sealed class CountingAudioEngine : IAudioEngine
    {
        public int StatusReads { get; private set; }

        public int PlaybackTimeReads { get; private set; }

        public AudioEngineStatus Status
        {
            get
            {
                StatusReads++;
                return default;
            }
        }

        public double PlaybackTimeMilliseconds
        {
            get
            {
                PlaybackTimeReads++;
                return 1_000;
            }
        }

        public double DurationMilliseconds => 60_000;

        public IReadOnlyList<AudioBackendCapabilities> Backends { get; } = [];

        public ValueTask<IReadOnlyList<AudioDeviceInfo>> GetOutputDevicesAsync(
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<AudioDeviceInfo>>([]);

        public ValueTask StartAsync(
            AudioEngineStartRequest request,
            CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        public ValueTask PauseAsync(
            CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        public ValueTask SeekAsync(
            double timeMilliseconds,
            CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        public ValueTask StopAsync(
            CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
