using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Yokko.Audio;
using Yokko.Game.Audio;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class AudioSettingsTestPlayerTest
{
    [Test]
    public async Task TestSignalsUseTheirRealMixBuses()
    {
        string directory = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            "audio-test-signals",
            Guid.NewGuid().ToString("N"));
        var engines = new List<RecordingAudioEngine>();
        var settings = new YokkoAudioSettings();
        bool calibrationStarted = false;
        settings.MasterVolume.Value = 0.8;
        settings.MusicVolume.Value = 0.5;
        settings.HitSoundVolume.Value = 0.75;

        try
        {
            await using var player = new AudioSettingsTestPlayer(
                settings,
                () =>
                {
                    var engine = new RecordingAudioEngine();
                    engines.Add(engine);
                    return engine;
                },
                directory,
                static (_, _) => Task.CompletedTask);

            AudioEngineStatus musicStatus =
                await player.PlayAsync(AudioSettingsTestKind.Music, true);
            AudioEngineStatus hitSoundStatus =
                await player.PlayAsync(AudioSettingsTestKind.HitSound, true);
            await player.PlayCalibrationAsync(
                () => calibrationStarted = true);

            Assert.That(engines, Has.Count.EqualTo(3));
            Assert.Multiple(() =>
            {
                Assert.That(
                    engines[0].MusicVolume,
                    Is.EqualTo(0.4).Within(0.0001));
                Assert.That(engines[0].HitSoundVolume, Is.Zero);
                Assert.That(engines[0].StartedPaths, Has.Count.EqualTo(1));
                Assert.That(
                    Path.GetFileName(engines[0].StartedPaths[0]),
                    Is.EqualTo("music-test.wav"));
                Assert.That(musicStatus, Is.EqualTo(engines[0].Status));

                Assert.That(engines[1].MusicVolume, Is.Zero);
                Assert.That(
                    engines[1].HitSoundVolume,
                    Is.EqualTo(0.6).Within(0.0001));
                Assert.That(engines[1].PreparedPaths, Has.Count.EqualTo(1));
                Assert.That(
                    engines[1].TriggeredPath,
                    Is.EqualTo(engines[1].PreparedPaths[0]));
                Assert.That(
                    Path.GetFileName(engines[1].StartedPaths[0]),
                    Is.EqualTo("silence-test.wav"));
                Assert.That(hitSoundStatus, Is.EqualTo(engines[1].Status));

                Assert.That(calibrationStarted, Is.True);
                Assert.That(
                    engines[2].MusicVolume,
                    Is.EqualTo(0.6).Within(0.0001));
                Assert.That(engines[2].HitSoundVolume, Is.Zero);
                Assert.That(
                    Path.GetFileName(engines[2].StartedPaths[0]),
                    Is.EqualTo("gameplay-calibration.wav"));
            });

            foreach (string path in Directory.GetFiles(directory, "*.wav"))
            {
                byte[] header = File.ReadAllBytes(path);
                Assert.That(header.Length, Is.GreaterThan(44));
                Assert.That(
                    System.Text.Encoding.ASCII.GetString(header, 0, 4),
                    Is.EqualTo("RIFF"));
            }
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

    [Test]
    public async Task DisabledHitSoundsCannotBeTested()
    {
        string directory = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            "disabled-audio-test-signals",
            Guid.NewGuid().ToString("N"));

        try
        {
            var settings = new YokkoAudioSettings();
            var player = new AudioSettingsTestPlayer(
                settings,
                static () => new RecordingAudioEngine(),
                directory,
                static (_, _) => Task.CompletedTask);

            Assert.ThrowsAsync<InvalidOperationException>(
                async () => await player.PlayAsync(
                    AudioSettingsTestKind.HitSound,
                    false));
            await player.DisposeAsync();
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

    [Test]
    public async Task InactiveFallbackCannotCountAsSuccessfulPlayback()
    {
        string directory = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            "inactive-audio-test-signals",
            Guid.NewGuid().ToString("N"));

        try
        {
            var settings = new YokkoAudioSettings();
            await using var player = new AudioSettingsTestPlayer(
                settings,
                static () => new NullAudioEngine(),
                directory,
                static (_, _) => Task.CompletedTask);

            Assert.ThrowsAsync<InvalidOperationException>(
                async () => await player.PlayAsync(
                    AudioSettingsTestKind.Music,
                    true));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

    [Test]
    public async Task GeneratedSignalsCanUseNativeOutput()
    {
        if (Environment.GetEnvironmentVariable(
                "YOKKO_RUN_AUDIO_SETTINGS_OUTPUT_TEST") != "1")
        {
            Assert.Ignore(
                "Set YOKKO_RUN_AUDIO_SETTINGS_OUTPUT_TEST=1 to run real output.");
        }

        if (!NativeAudioEngine.IsAvailable)
            Assert.Ignore("The Yokko native audio library is unavailable.");

        string directory = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            "native-audio-test-signals",
            Guid.NewGuid().ToString("N"));

        try
        {
            var settings = new YokkoAudioSettings();
            settings.PreferredBackend.Value = AudioBackendKind.SharedWasapi;
            settings.MasterVolume.Value = 0.35;
            await using var player = new AudioSettingsTestPlayer(
                settings,
                static () => new NativeAudioEngine(),
                directory);

            await player.PlayAsync(AudioSettingsTestKind.Music, true);
            await player.PlayAsync(AudioSettingsTestKind.HitSound, true);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

    private sealed class RecordingAudioEngine :
        IAudioEngine,
        IAudioMixControl,
        IAudioSamplePlayback
    {
        internal List<string> StartedPaths { get; } = new();
        internal List<string> PreparedPaths { get; } = new();
        internal string TriggeredPath { get; private set; }

        public double MusicVolume { get; private set; } = 1;
        public double HitSoundVolume { get; private set; } = 1;
        public double MetronomeVolume { get; private set; }
        public AudioEngineStatus Status { get; } = new(
            AudioBackendKind.SharedWasapi,
            null,
            48000,
            64,
            0,
            false,
            true,
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
        public double PlaybackTimeMilliseconds => 0;
        public double DurationMilliseconds => 1000;
        public IReadOnlyList<AudioBackendCapabilities> Backends { get; } = [];

        public ValueTask<IReadOnlyList<AudioDeviceInfo>> GetOutputDevicesAsync(
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<AudioDeviceInfo>>([]);

        public ValueTask StartAsync(
            AudioEngineStartRequest request,
            CancellationToken cancellationToken = default)
        {
            StartedPaths.Add(request.AudioPath);
            return ValueTask.CompletedTask;
        }

        public ValueTask PrepareSamplesAsync(
            IReadOnlyCollection<string> samplePaths,
            CancellationToken cancellationToken = default)
        {
            PreparedPaths.AddRange(samplePaths);
            return ValueTask.CompletedTask;
        }

        public bool TriggerSample(string samplePath)
        {
            TriggeredPath = samplePath;
            return true;
        }

        public void SetMixVolumes(
            double musicVolume,
            double hitSoundVolume,
            double metronomeVolume)
        {
            MusicVolume = musicVolume;
            HitSoundVolume = hitSoundVolume;
            MetronomeVolume = metronomeVolume;
        }

        public bool TriggerMetronome() => true;
        public ValueTask PauseAsync(CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;
        public ValueTask SeekAsync(
            double timeMilliseconds,
            CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;
        public ValueTask StopAsync(CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
