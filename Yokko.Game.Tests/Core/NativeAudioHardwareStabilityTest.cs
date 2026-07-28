using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Yokko.Audio;

namespace Yokko.Game.Tests.Core;

[TestFixture]
[Platform(Include = "Win")]
[NonParallelizable]
[Category("Hardware")]
public sealed class NativeAudioHardwareStabilityTest
{
    private static readonly int[] requestedBufferProfiles = { 64, 128, 256, 512 };
    private static readonly int[] sampleRates = { 44100, 48000, 96000 };

    [Test]
    public async Task ExclusiveProfilesFormatsAndStability()
    {
        if (Environment.GetEnvironmentVariable("YOKKO_RUN_AUDIO_STABILITY") != "1")
            Assert.Ignore("Set YOKKO_RUN_AUDIO_STABILITY=1 to run real-device audio tests.");
        if (!NativeAudioEngine.IsAvailable)
            Assert.Fail("The Yokko native audio library is unavailable.");

        int stabilitySeconds = int.TryParse(
            Environment.GetEnvironmentVariable("YOKKO_AUDIO_STABILITY_SECONDS"),
            out int configuredSeconds)
            ? Math.Clamp(configuredSeconds, 5, 120)
            : 12;

        await using var enumerator = new NativeAudioEngine();
        IReadOnlyList<AudioDeviceInfo> enumerated =
            await enumerator.GetOutputDevicesAsync();
        AudioDeviceInfo[] devices = enumerated
                                    .Where(device =>
                                        device.Backend
                                        == AudioBackendKind.WasapiExclusive)
                                    .GroupBy(
                                        device => device.Id,
                                        StringComparer.Ordinal)
                                    .Select(group => group.First())
                                    .ToArray();

        Assert.That(devices, Is.Not.Empty, "No active WASAPI output endpoints were found.");
        foreach (AudioDeviceInfo available in devices)
        {
            TestContext.Progress.WriteLine(
                $"Endpoint: {available.Name} | default={available.IsDefault} | "
                + available.Id);
        }

        string requestedDeviceId =
            Environment.GetEnvironmentVariable("YOKKO_AUDIO_TEST_DEVICE_ID");
        AudioDeviceInfo device = string.IsNullOrWhiteSpace(requestedDeviceId)
            ? devices.FirstOrDefault(candidate => candidate.IsDefault)
              ?? devices[0]
            : devices.Single(candidate => candidate.Id == requestedDeviceId);

        string directory = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            "native-audio-hardware",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            var audioFiles = new Dictionary<int, string>();
            foreach (int sampleRate in sampleRates)
            {
                string path = Path.Combine(
                    directory,
                    $"probe-{sampleRate}.wav");
                createProbeWave(
                    path,
                    sampleRate,
                    channels: 2,
                    durationSeconds: stabilitySeconds + 8);
                audioFiles[sampleRate] = path;
            }

            TestContext.Progress.WriteLine(
                $"Testing endpoint: {device.Name} ({device.Id})");

            var profileResults = new List<HardwareRunResult>();
            foreach (int requestedFrames in requestedBufferProfiles)
            {
                profileResults.Add(await runExclusiveAsync(
                    device,
                    audioFiles[48000],
                    48000,
                    requestedFrames,
                    TimeSpan.FromMilliseconds(1500)));
            }

            foreach (int sampleRate in sampleRates.Where(rate => rate != 48000))
            {
                await runExclusiveAsync(
                    device,
                    audioFiles[sampleRate],
                    sampleRate,
                    requestedBufferProfiles[0],
                    TimeSpan.FromMilliseconds(1500));
            }

            HardwareRunResult lowestLatency =
                profileResults.OrderBy(result => result.ReportedLatencyMilliseconds)
                              .ThenBy(result => result.AcceptedBufferFrames)
                              .First();
            HardwareRunResult stability = await runExclusiveAsync(
                device,
                audioFiles[48000],
                48000,
                lowestLatency.RequestedBufferFrames,
                TimeSpan.FromSeconds(stabilitySeconds));

            Assert.That(
                stability.CallbackDeadlineMissCount,
                Is.Zero,
                "A stable rhythm-game profile cannot miss audio callback deadlines.");
            Assert.That(
                stability.CallbackCadenceMissCount,
                Is.Zero,
                "A stable rhythm-game profile cannot receive late device callbacks.");
            Assert.That(
                stability.MaxCallbackDurationMilliseconds,
                Is.LessThan(stability.CallbackBudgetMilliseconds),
                "The callback's worst work duration exceeded the device period.");
            Assert.That(
                stability.ClockDriftMilliseconds,
                Is.LessThan(10),
                "The hardware playback clock drifted too far from monotonic time.");
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

    private static async Task<HardwareRunResult> runExclusiveAsync(
        AudioDeviceInfo device,
        string audioPath,
        int sampleRate,
        int requestedBufferFrames,
        TimeSpan duration)
    {
        await using var engine = new NativeAudioEngine();
        await engine.StartAsync(new AudioEngineStartRequest(
            audioPath,
            AudioBackendKind.WasapiExclusive,
            device.Id,
            sampleRate,
            requestedBufferFrames,
            0));

        AudioEngineStatus opened = engine.Status;
        Assert.That(
            opened.ActiveBackend,
            Is.EqualTo(AudioBackendKind.WasapiExclusive),
            "Shared fallback must never count as an Exclusive test pass.");
        Assert.That(opened.IsExclusive, Is.True);
        Assert.That(opened.IsRunning, Is.True);
        Assert.That(opened.SampleRate, Is.EqualTo(sampleRate));
        Assert.That(opened.BufferSize, Is.GreaterThan(0));
        Assert.That(
            opened.EstimatedOutputLatencyMilliseconds,
            Is.LessThanOrEqualTo(15),
            "The reported output stream latency is above the rhythm-game gate.");

        var startupStopwatch = Stopwatch.StartNew();
        AudioEngineStatus current = opened;
        while (current.CallbackCount < 5
               && startupStopwatch.Elapsed < TimeSpan.FromSeconds(2))
        {
            await Task.Delay(5);
            current = engine.Status;
            Assert.That(
                current.IsRunning,
                Is.True,
                $"Output faulted during startup: HRESULT "
                + $"0x{current.BackendError:X8}, stage "
                + $"{current.BackendErrorStage}.");
        }
        startupStopwatch.Stop();
        Assert.That(
            current.CallbackCount,
            Is.GreaterThanOrEqualTo(5),
            "The endpoint did not reach a stable event cadence within 2 seconds.");

        ulong firstCallbackCount = current.CallbackCount;
        double firstClock = engine.PlaybackTimeMilliseconds;
        double previousClock = firstClock;
        var stopwatch = Stopwatch.StartNew();

        while (stopwatch.Elapsed < duration)
        {
            await Task.Delay(25);
            current = engine.Status;
            double clock = engine.PlaybackTimeMilliseconds;

            Assert.That(
                current.IsRunning,
                Is.True,
                $"Output faulted: HRESULT 0x{current.BackendError:X8}, "
                + $"stage {current.BackendErrorStage}.");
            Assert.That(current.HasUnderrun, Is.False);
            Assert.That(
                current.BackendError,
                Is.Zero,
                $"Backend failure stage {current.BackendErrorStage}.");
            Assert.That(
                clock,
                Is.GreaterThanOrEqualTo(previousClock),
                "Hardware playback clock regressed.");
            previousClock = clock;
        }

        stopwatch.Stop();
        double clockElapsed = previousClock - firstClock;
        double clockDrift = Math.Abs(
            clockElapsed - stopwatch.Elapsed.TotalMilliseconds);
        double expectedCallbacks =
            stopwatch.Elapsed.TotalMilliseconds
            / current.CallbackBudgetMilliseconds;
        ulong observedCallbacks =
            current.CallbackCount - firstCallbackCount;

        var result = new HardwareRunResult(
            sampleRate,
            requestedBufferFrames,
            current.BufferSize,
            current.EstimatedOutputLatencyMilliseconds,
            current.CallbackCount,
            current.CallbackDeadlineMissCount,
            current.MaxCallbackDurationMilliseconds,
            current.CallbackBudgetMilliseconds,
            current.CallbackCadenceMissCount,
            current.MaxCallbackIntervalMilliseconds,
            startupStopwatch.Elapsed.TotalMilliseconds,
            clockElapsed,
            stopwatch.Elapsed.TotalMilliseconds,
            clockDrift);

        TestContext.Progress.WriteLine(
            $"{sampleRate} Hz | requested={requestedBufferFrames} frames | "
            + $"accepted={result.AcceptedBufferFrames} | "
            + $"latency={result.ReportedLatencyMilliseconds:F3} ms | "
            + $"callbacks={result.CallbackCount} | "
            + $"work misses={result.CallbackDeadlineMissCount} | "
            + $"cadence misses={result.CallbackCadenceMissCount} | "
            + $"max callback={result.MaxCallbackDurationMilliseconds:F3}/"
            + $"{result.CallbackBudgetMilliseconds:F3} ms | "
            + $"max interval={result.MaxCallbackIntervalMilliseconds:F3} ms | "
            + $"startup={result.StartupMilliseconds:F3} ms | "
            + $"clock={result.ClockElapsedMilliseconds:F3} ms / "
            + $"wall={result.WallElapsedMilliseconds:F3} ms | "
            + $"drift={result.ClockDriftMilliseconds:F3} ms");

        Assert.That(observedCallbacks, Is.GreaterThan(1));
        Assert.That(
            observedCallbacks,
            Is.GreaterThan(expectedCallbacks * 0.6),
            "The event stream produced too few callbacks for the device period.");
        Assert.That(
            current.CallbackDeadlineMissCount,
            Is.Zero,
            "Audio callback deadline miss detected.");
        Assert.That(
            current.CallbackCadenceMissCount,
            Is.Zero,
            "The output driver delivered an audio event too late.");
        Assert.That(
            current.MaxCallbackDurationMilliseconds,
            Is.LessThan(current.CallbackBudgetMilliseconds));
        Assert.That(
            clockDrift,
            Is.LessThan(15),
            "Hardware playback clock drift exceeded the short-run gate.");

        await engine.StopAsync();
        return result;
    }

    private static void createProbeWave(
        string path,
        int sampleRate,
        short channels,
        int durationSeconds)
    {
        const short bitsPerSample = 16;
        int frameCount = checked(sampleRate * durationSeconds);
        int dataLength = checked(
            frameCount * channels * bitsPerSample / 8);
        using var writer = new BinaryWriter(File.Create(path));
        writer.Write("RIFF"u8.ToArray());
        writer.Write(36 + dataLength);
        writer.Write("WAVE"u8.ToArray());
        writer.Write("fmt "u8.ToArray());
        writer.Write(16);
        writer.Write((short)1);
        writer.Write(channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * channels * bitsPerSample / 8);
        writer.Write((short)(channels * bitsPerSample / 8));
        writer.Write(bitsPerSample);
        writer.Write("data"u8.ToArray());
        writer.Write(dataLength);

        // Avoid driver-specific digital-silence power gating without producing
        // a meaningfully audible signal. Alternating one-LSB PCM16 samples are
        // approximately -90 dBFS and exercise the real render path.
        const int blockSamples = 4096;
        byte[] probeBlock = new byte[blockSamples * sizeof(short)];
        for (int sample = 0; sample < blockSamples; sample++)
        {
            short value = (short)((sample & 1) == 0 ? 1 : -1);
            probeBlock[sample * 2] = (byte)value;
            probeBlock[sample * 2 + 1] = (byte)(value >> 8);
        }

        int remaining = dataLength;
        while (remaining > 0)
        {
            int bytesToWrite = Math.Min(remaining, probeBlock.Length);
            writer.Write(probeBlock, 0, bytesToWrite);
            remaining -= bytesToWrite;
        }
    }

    private sealed record HardwareRunResult(
        int SampleRate,
        int RequestedBufferFrames,
        int AcceptedBufferFrames,
        double ReportedLatencyMilliseconds,
        ulong CallbackCount,
        ulong CallbackDeadlineMissCount,
        double MaxCallbackDurationMilliseconds,
        double CallbackBudgetMilliseconds,
        ulong CallbackCadenceMissCount,
        double MaxCallbackIntervalMilliseconds,
        double StartupMilliseconds,
        double ClockElapsedMilliseconds,
        double WallElapsedMilliseconds,
        double ClockDriftMilliseconds);
}
