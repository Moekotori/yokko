using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Yokko.Audio;

namespace Yokko.Game.Audio;

internal enum AudioSettingsTestKind
{
    Music,
    HitSound,
}

/// <summary>
/// Exercises the configured output path with generated, licence-free test
/// signals. Music and hit sounds deliberately use their real native mix buses.
/// </summary>
internal sealed class AudioSettingsTestPlayer : IAsyncDisposable
{
    private enum TestMixBus
    {
        Music,
        HitSound,
        Calibration,
    }

    private const int sample_rate = 48000;
    private const double calibration_duration_seconds = 30;
    private const double calibration_lead_in_seconds = 1;
    private const double calibration_beat_seconds = 0.5;
    private readonly YokkoAudioSettings settings;
    private readonly Func<IAudioEngine> createEngine;
    private readonly Func<TimeSpan, CancellationToken, Task> delay;
    private readonly string testDirectory;
    private readonly SemaphoreSlim playbackGate = new(1, 1);
    private readonly CancellationTokenSource disposalCancellation = new();
    private readonly object mixLock = new();
    private IAudioMixControl activeMix;
    private TestMixBus activeMixBus;
    private bool filesReady;

    internal AudioSettingsTestPlayer(
        YokkoAudioSettings settings,
        Func<IAudioEngine> createEngine,
        string testDirectory,
        Func<TimeSpan, CancellationToken, Task> delay = null)
    {
        this.settings = settings
                        ?? throw new ArgumentNullException(nameof(settings));
        this.createEngine = createEngine
                            ?? throw new ArgumentNullException(
                                nameof(createEngine));
        this.testDirectory = string.IsNullOrWhiteSpace(testDirectory)
            ? throw new ArgumentException(
                "A test signal directory is required.",
                nameof(testDirectory))
            : Path.GetFullPath(testDirectory);
        this.delay = delay ?? Task.Delay;
        this.settings.MixChanged += onMixChanged;
    }

    internal async Task<AudioEngineStatus> PlayAsync(
        AudioSettingsTestKind kind,
        bool hitSoundsEnabled,
        CancellationToken cancellationToken = default)
    {
        using CancellationTokenSource linked =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                disposalCancellation.Token);
        CancellationToken token = linked.Token;
        await playbackGate.WaitAsync(token).ConfigureAwait(false);

        try
        {
            ensureTestSignals();
            await using IAudioEngine engine = createEngine();
            if (engine is not IAudioMixControl mix)
            {
                throw new InvalidOperationException(
                    "The active audio backend does not expose mix controls.");
            }

            TestMixBus bus = kind switch
            {
                AudioSettingsTestKind.Music => TestMixBus.Music,
                AudioSettingsTestKind.HitSound => TestMixBus.HitSound,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(kind),
                    kind,
                    null),
            };

            activateMix(mix, bus);
            try
            {
                return kind switch
                {
                    AudioSettingsTestKind.Music =>
                        await playMusicAsync(engine, token)
                            .ConfigureAwait(false),
                    AudioSettingsTestKind.HitSound =>
                        await playHitSoundAsync(
                                engine,
                                hitSoundsEnabled,
                                token)
                            .ConfigureAwait(false),
                    _ => throw new ArgumentOutOfRangeException(
                        nameof(kind),
                        kind,
                        null),
                };
            }
            finally
            {
                deactivateMix(mix);
            }
        }
        finally
        {
            playbackGate.Release();
        }
    }

    internal async Task PlayCalibrationAsync(
        Action playbackStarted,
        CancellationToken cancellationToken = default)
    {
        using CancellationTokenSource linked =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                disposalCancellation.Token);
        CancellationToken token = linked.Token;
        await playbackGate.WaitAsync(token).ConfigureAwait(false);

        try
        {
            ensureTestSignals();
            await using IAudioEngine engine = createEngine();
            if (engine is not IAudioMixControl mix)
            {
                throw new InvalidOperationException(
                    "The active audio backend does not expose mix controls.");
            }

            activateMix(mix, TestMixBus.Calibration);
            try
            {
                await engine.StartAsync(
                                settings.CreateStartRequest(calibrationPath),
                                token)
                            .ConfigureAwait(false);
                playbackStarted?.Invoke();
                await delay(
                          TimeSpan.FromSeconds(calibration_duration_seconds),
                          token)
                      .ConfigureAwait(false);
                await engine.StopAsync(token).ConfigureAwait(false);
            }
            finally
            {
                deactivateMix(mix);
            }
        }
        finally
        {
            playbackGate.Release();
        }
    }

    private async Task<AudioEngineStatus> playMusicAsync(
        IAudioEngine engine,
        CancellationToken cancellationToken)
    {
        await engine.StartAsync(
                        settings.CreateStartRequest(musicPath),
                        cancellationToken)
                    .ConfigureAwait(false);
        AudioEngineStatus opened = requireActiveOutput(engine);
        await delay(
                  TimeSpan.FromMilliseconds(760),
                  cancellationToken)
              .ConfigureAwait(false);
        await engine.StopAsync(cancellationToken).ConfigureAwait(false);
        return opened;
    }

    private async Task<AudioEngineStatus> playHitSoundAsync(
        IAudioEngine engine,
        bool hitSoundsEnabled,
        CancellationToken cancellationToken)
    {
        if (!hitSoundsEnabled)
        {
            throw new InvalidOperationException(
                "Hitsounds must be enabled before testing.");
        }

        if (engine is not IAudioSamplePlayback samples)
        {
            throw new InvalidOperationException(
                "The active audio backend cannot test hitsounds.");
        }

        await samples.PrepareSamplesAsync(
                         new[] { hitSoundPath },
                         cancellationToken)
                     .ConfigureAwait(false);
        await engine.StartAsync(
                        settings.CreateStartRequest(silencePath),
                        cancellationToken)
                    .ConfigureAwait(false);
        AudioEngineStatus opened = requireActiveOutput(engine);
        if (!samples.TriggerSample(hitSoundPath))
        {
            throw new InvalidOperationException(
                "The active audio backend rejected the hitsound test.");
        }

        await delay(
                  TimeSpan.FromMilliseconds(320),
                  cancellationToken)
              .ConfigureAwait(false);
        await engine.StopAsync(cancellationToken).ConfigureAwait(false);
        return opened;
    }

    private static AudioEngineStatus requireActiveOutput(
        IAudioEngine engine)
    {
        AudioEngineStatus opened = engine.Status;
        if (!opened.IsRunning
            || opened.ActiveBackend == AudioBackendKind.Fallback)
        {
            throw new InvalidOperationException(
                "The audio test did not open an active output stream.");
        }

        return opened;
    }

    private void activateMix(IAudioMixControl mix, TestMixBus bus)
    {
        lock (mixLock)
        {
            activeMix = mix;
            activeMixBus = bus;
            applyCurrentMix(mix, bus);
        }
    }

    private void deactivateMix(IAudioMixControl mix)
    {
        lock (mixLock)
        {
            if (ReferenceEquals(activeMix, mix))
                activeMix = null;
        }
    }

    private void onMixChanged()
    {
        lock (mixLock)
        {
            if (activeMix != null)
                applyCurrentMix(activeMix, activeMixBus);
        }
    }

    private void applyCurrentMix(IAudioMixControl mix, TestMixBus bus)
    {
        switch (bus)
        {
            case TestMixBus.Music:
                mix.SetMixVolumes(settings.EffectiveMusicVolume, 0, 0);
                break;

            case TestMixBus.HitSound:
                mix.SetMixVolumes(0, settings.EffectiveHitSoundVolume, 0);
                break;

            case TestMixBus.Calibration:
                mix.SetMixVolumes(settings.EffectiveHitSoundVolume, 0, 0);
                break;
        }
    }

    private string musicPath => Path.Combine(testDirectory, "music-test.wav");
    private string silencePath => Path.Combine(testDirectory, "silence-test.wav");
    private string hitSoundPath => Path.Combine(testDirectory, "hitsound-test.wav");
    private string calibrationPath =>
        Path.Combine(testDirectory, "gameplay-calibration.wav");

    private void ensureTestSignals()
    {
        if (filesReady
            && File.Exists(musicPath)
            && File.Exists(silencePath)
            && File.Exists(hitSoundPath)
            && File.Exists(calibrationPath))
        {
            return;
        }

        Directory.CreateDirectory(testDirectory);
        writeWave(
            musicPath,
            0.75,
            2,
            (frame, frames) =>
            {
                double time = frame / (double)sample_rate;
                double fade = envelope(frame, frames, 0.02, 0.06);
                return 0.16
                       * (Math.Sin(2 * Math.PI * 440 * time)
                          + 0.45 * Math.Sin(2 * Math.PI * 660 * time))
                       * fade;
            });
        writeWave(silencePath, 0.5, 2, static (_, _) => 0);
        writeWave(
            hitSoundPath,
            0.14,
            1,
            (frame, frames) =>
            {
                double time = frame / (double)sample_rate;
                double progress = frame / (double)Math.Max(1, frames - 1);
                double frequency = 1050 - 380 * progress;
                double fade = envelope(frame, frames, 0.002, 0.09);
                return 0.42
                       * Math.Sin(2 * Math.PI * frequency * time)
                       * fade;
            });
        writeWave(
            calibrationPath,
            calibration_duration_seconds,
            2,
            static (frame, _) =>
            {
                double time = frame / (double)sample_rate;
                if (time < calibration_lead_in_seconds)
                    return 0;

                double beatPosition =
                    (time - calibration_lead_in_seconds)
                    % calibration_beat_seconds;
                if (beatPosition >= 0.075)
                    return 0;

                double fade = Math.Exp(-beatPosition * 52);
                double accent =
                    Math.Abs(
                        (time - calibration_lead_in_seconds)
                        % (calibration_beat_seconds * 4))
                    < 1d / sample_rate
                        ? 1.2
                        : 1;
                return 0.34
                       * accent
                       * Math.Sin(2 * Math.PI * 1180 * beatPosition)
                       * fade;
            });
        filesReady = true;
    }

    private static double envelope(
        int frame,
        int frameCount,
        double attackSeconds,
        double releaseSeconds)
    {
        double attackFrames = Math.Max(1, attackSeconds * sample_rate);
        double releaseFrames = Math.Max(1, releaseSeconds * sample_rate);
        return Math.Min(
            1,
            Math.Min(
                frame / attackFrames,
                (frameCount - 1 - frame) / releaseFrames));
    }

    private static void writeWave(
        string path,
        double durationSeconds,
        short channels,
        Func<int, int, double> sampleAt)
    {
        const short bits_per_sample = 16;
        int frameCount = checked(
            (int)Math.Round(sample_rate * durationSeconds));
        int blockAlign = channels * bits_per_sample / 8;
        int dataLength = checked(frameCount * blockAlign);

        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);
        writer.Write("RIFF"u8);
        writer.Write(36 + dataLength);
        writer.Write("WAVE"u8);
        writer.Write("fmt "u8);
        writer.Write(16);
        writer.Write((short)1);
        writer.Write(channels);
        writer.Write(sample_rate);
        writer.Write(sample_rate * blockAlign);
        writer.Write((short)blockAlign);
        writer.Write(bits_per_sample);
        writer.Write("data"u8);
        writer.Write(dataLength);

        for (int frame = 0; frame < frameCount; frame++)
        {
            short value = (short)Math.Round(
                Math.Clamp(sampleAt(frame, frameCount), -1, 1)
                * short.MaxValue);
            for (int channel = 0; channel < channels; channel++)
                writer.Write(value);
        }
    }

    public async ValueTask DisposeAsync()
    {
        settings.MixChanged -= onMixChanged;
        disposalCancellation.Cancel();
        await playbackGate.WaitAsync().ConfigureAwait(false);
        playbackGate.Release();
        disposalCancellation.Dispose();
        playbackGate.Dispose();
    }
}
