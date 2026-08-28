using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Yokko.Audio;
using Yokko.Core.Beatmaps;
using Yokko.Core.Mods;
using Yokko.Game.Audio;
using Yokko.Game.Screens.SongSelect;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class SongSelectPreviewPlayerTest
{
    [Test]
    public void PreviewStartUsesChartPointAndOsuFallback()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                SongSelectPreviewPlayer.CalculatePreviewStart(
                    25000,
                    100000),
                Is.EqualTo(25000));
            Assert.That(
                SongSelectPreviewPlayer.CalculatePreviewStart(
                    -1,
                    100000),
                Is.EqualTo(40000));
            Assert.That(
                SongSelectPreviewPlayer.CalculatePreviewStart(
                    120000,
                    100000),
                Is.EqualTo(40000));
        });
    }

    [Test]
    public async Task SelectionStartsAtPreviewAndSameSongDoesNotRestart()
    {
        string audioPath = createAudioPath();
        var engine = new TrackingAudioEngine
        {
            DurationMilliseconds = 100000,
        };

        try
        {
            await using var player = new SongSelectPreviewPlayer(
                engine,
                new YokkoAudioSettings());
            YokkoBeatmap first = DemoBeatmaps.CreateFourKeyDemo() with
            {
                AudioPath = audioPath,
                PreviewTimeMilliseconds = 25000,
            };
            YokkoBeatmap anotherDifficulty = first with
            {
                DifficultyName = "Another difficulty",
                PreviewTimeMilliseconds = 30000,
            };

            player.Play(first, ManiaModSet.Empty);
            await player.WaitForIdleAsync();
            player.Play(anotherDifficulty, ManiaModSet.Empty);
            await player.WaitForIdleAsync();

            Assert.Multiple(() =>
            {
                Assert.That(engine.Starts, Has.Count.EqualTo(1));
                Assert.That(engine.Seeks, Is.EqualTo(new[] { 25000 }));
                Assert.That(
                    player.CurrentAudioPath,
                    Is.EqualTo(Path.GetFullPath(audioPath)));
                Assert.That(player.IsPlaying, Is.True);
            });

            engine.FinishTrack(keepOutputRunning: true);
            player.EnsurePlaying();
            await player.WaitForIdleAsync();
            Assert.That(
                engine.Starts,
                Has.Count.EqualTo(2),
                "Finished previews should loop even while the native output remains running.");

            player.Stop();
            await player.WaitForIdleAsync();
            Assert.That(engine.StopCount, Is.EqualTo(1));
        }
        finally
        {
            File.Delete(audioPath);
        }
    }

    [Test]
    public async Task RapidSelectionOnlyStartsNewestSong()
    {
        string firstPath = createAudioPath();
        string secondPath = createAudioPath();
        var engine = new TrackingAudioEngine
        {
            DurationMilliseconds = 50000,
        };

        try
        {
            await using var player = new SongSelectPreviewPlayer(
                engine,
                new YokkoAudioSettings());
            YokkoBeatmap beatmap = DemoBeatmaps.CreateFourKeyDemo();

            player.Play(
                beatmap with { AudioPath = firstPath },
                ManiaModSet.Empty);
            player.Play(
                beatmap with { AudioPath = secondPath },
                ManiaModSet.Empty);
            await player.WaitForIdleAsync();

            Assert.That(engine.Starts, Has.Count.EqualTo(1));
            Assert.That(
                engine.Starts[0].AudioPath,
                Is.EqualTo(Path.GetFullPath(secondPath)));
        }
        finally
        {
            File.Delete(firstPath);
            File.Delete(secondPath);
        }
    }

    [Test]
    public async Task RateChangeUpdatesPlayingPreviewInPlace()
    {
        string audioPath = createAudioPath();
        var engine = new TrackingAudioEngine
        {
            DurationMilliseconds = 50000,
        };

        try
        {
            await using var player = new SongSelectPreviewPlayer(
                engine,
                new YokkoAudioSettings());
            YokkoBeatmap beatmap = DemoBeatmaps.CreateFourKeyDemo() with
            {
                AudioPath = audioPath,
            };

            player.Play(beatmap, ManiaModSet.Empty);
            await player.WaitForIdleAsync();
            int seekCount = engine.Seeks.Count;

            bool increased = player.TryUpdatePlaybackRate(
                beatmap,
                ManiaModSet.Empty.WithFixedRate(
                    ManiaModId.DoubleTime,
                    1.05));
            bool restored = player.TryUpdatePlaybackRate(
                beatmap,
                ManiaModSet.Empty);

            Assert.Multiple(() =>
            {
                Assert.That(increased, Is.True);
                Assert.That(restored, Is.True);
                Assert.That(engine.Starts, Has.Count.EqualTo(1));
                Assert.That(engine.Seeks, Has.Count.EqualTo(seekCount));
                Assert.That(
                    engine.Starts[0].DynamicPlaybackRate,
                    Is.True);
                Assert.That(
                    engine.RateChanges,
                    Is.EqualTo(new[] { 1.05, 1 }));
            });
        }
        finally
        {
            File.Delete(audioPath);
        }
    }

    [Test]
    public async Task AudioChangingModRestartsPreviewWithMatchingPolicy()
    {
        string audioPath = createAudioPath();
        var engine = new TrackingAudioEngine
        {
            DurationMilliseconds = 50000,
        };

        try
        {
            await using var player = new SongSelectPreviewPlayer(
                engine,
                new YokkoAudioSettings());
            YokkoBeatmap beatmap = DemoBeatmaps.CreateFourKeyDemo() with
            {
                AudioPath = audioPath,
            };

            player.Play(beatmap, ManiaModSet.Empty);
            await player.WaitForIdleAsync();
            player.Play(
                beatmap,
                ManiaModSet.Empty.WithFixedRate(
                    ManiaModId.Nightcore,
                    1.25));
            await player.WaitForIdleAsync();

            Assert.Multiple(() =>
            {
                Assert.That(engine.Starts, Has.Count.EqualTo(2));
                Assert.That(
                    engine.Starts[1].PlaybackRate,
                    Is.EqualTo(1.25));
                Assert.That(
                    engine.Starts[1].PitchMode,
                    Is.EqualTo(AudioPitchMode.ScaleWithRate));
                Assert.That(
                    engine.Starts[1].FixedFrequencyScale,
                    Is.EqualTo(1.5));
            });
        }
        finally
        {
            File.Delete(audioPath);
        }
    }

    private static string createAudioPath()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            $"yokko-preview-{Guid.NewGuid():N}.mp3");
        File.WriteAllBytes(path, []);
        return path;
    }

    private sealed class TrackingAudioEngine :
        IAudioEngine,
        IAudioRateControl
    {
        private static readonly AudioEngineStatus stoppedStatus = new(
            AudioBackendKind.SharedWasapi,
            "Test",
            48000,
            64,
            0,
            false,
            false,
            false,
            false,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0);

        public readonly List<AudioEngineStartRequest> Starts = [];
        public readonly List<double> Seeks = [];
        public readonly List<double> RateChanges = [];

        private AudioEngineStartRequest activeRequest;

        public AudioEngineStatus Status { get; private set; } =
            stoppedStatus;

        public double PlaybackTimeMilliseconds { get; private set; }

        public double DurationMilliseconds { get; init; }

        public double PlaybackRate { get; private set; } = 1;

        public IReadOnlyList<AudioBackendCapabilities> Backends => [];

        public int StopCount { get; private set; }

        public void FinishTrack(bool keepOutputRunning)
        {
            PlaybackTimeMilliseconds = DurationMilliseconds;
            Status = Status with { IsRunning = keepOutputRunning };
        }

        public ValueTask<IReadOnlyList<AudioDeviceInfo>>
            GetOutputDevicesAsync(
                CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<AudioDeviceInfo>>([]);

        public ValueTask StartAsync(
            AudioEngineStartRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Starts.Add(request);
            activeRequest = request;
            PlaybackRate = request.PlaybackRate;
            PlaybackTimeMilliseconds = 0;
            Status = stoppedStatus with { IsRunning = true };
            return ValueTask.CompletedTask;
        }

        public void SetPlaybackRate(double playbackRate)
        {
            if (activeRequest?.DynamicPlaybackRate != true)
                throw new InvalidOperationException();

            PlaybackRate = playbackRate;
            RateChanges.Add(playbackRate);
        }

        public ValueTask PauseAsync(
            CancellationToken cancellationToken = default,
            bool retainOutput = false)
        {
            Status = Status with { IsRunning = false };
            return ValueTask.CompletedTask;
        }

        public ValueTask ResumeAsync(
            CancellationToken cancellationToken = default)
        {
            Status = Status with { IsRunning = true };
            return ValueTask.CompletedTask;
        }

        public ValueTask SeekAsync(
            double timeMilliseconds,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Seeks.Add(timeMilliseconds);
            PlaybackTimeMilliseconds = timeMilliseconds;
            Status = Status with { IsRunning = true };
            return ValueTask.CompletedTask;
        }

        public ValueTask StopAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            StopCount++;
            PlaybackTimeMilliseconds = 0;
            Status = stoppedStatus;
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            Status = stoppedStatus;
            return ValueTask.CompletedTask;
        }
    }
}
