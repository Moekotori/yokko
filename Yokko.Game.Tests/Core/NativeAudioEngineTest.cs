using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Yokko.Audio;

namespace Yokko.Game.Tests.Core
{
    [TestFixture]
    public sealed class NativeAudioEngineTest
    {
        [Test]
        public async Task PreparedSampleHandlesAreVersionedAndValidated()
        {
            string directory = Path.Combine(
                TestContext.CurrentContext.WorkDirectory,
                "prepared-sample-handles",
                TestContext.CurrentContext.Test.ID);
            Directory.CreateDirectory(directory);
            string audioPath = Path.Combine(directory, "sample.wav");
            string corruptPath = Path.Combine(directory, "corrupt.wav");
            createSilentWave(audioPath, 48000, 2, 50);
            File.WriteAllBytes(corruptPath, [1, 2, 3]);

            await using var engine = new NativeAudioEngine();
            await using var foreignEngine = new NativeAudioEngine();

            Assert.That(
                engine.TryGetPreparedSampleHandle(audioPath, out _),
                Is.False);
            await engine.PrepareSamplesAsync(
                ["missing.wav", corruptPath, audioPath]);
            Assert.That(
                engine.TryGetPreparedSampleHandle(audioPath, out var first),
                Is.True);
            Assert.That(
                engine.TryGetPreparedSampleHandle(corruptPath, out _),
                Is.False);

            await engine.PrepareSamplesAsync([audioPath]);
            Assert.That(
                engine.TryGetPreparedSampleHandle(audioPath, out var second),
                Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(first.Generation, Is.Not.Zero);
                Assert.That(second.Generation, Is.Not.EqualTo(first.Generation));
                Assert.That(first.Slot, Is.Zero);
                Assert.That(second.Slot, Is.Zero);
                Assert.That(
                    engine.TriggerPreparedSample(default, 1),
                    Is.False);
                Assert.That(
                    engine.TriggerPreparedSample(second, double.NaN),
                    Is.False);
                Assert.That(
                    engine.StartLoopingPreparedSample(second, -1),
                    Is.Zero);
                Assert.That(
                    foreignEngine.TriggerPreparedSample(second, 1),
                    Is.False);
            });
        }

        [Test]
        public async Task SeekKeepsPreparedSampleHandlesValid()
        {
            string directory = Path.Combine(
                TestContext.CurrentContext.WorkDirectory,
                "seek-prepared-samples",
                TestContext.CurrentContext.Test.ID);
            Directory.CreateDirectory(directory);
            string audioPath = Path.Combine(directory, "sample.wav");
            createSilentWave(audioPath, 48000, 2, 50);

            await using var engine = new NativeAudioEngine();
            await engine.PrepareSamplesAsync([audioPath]);
            Assert.That(
                engine.TryGetPreparedSampleHandle(audioPath, out var before),
                Is.True);

            await engine.SeekAsync(1000);

            Assert.That(
                engine.TryGetPreparedSampleHandle(audioPath, out var after),
                Is.True,
                "Seeking must not invalidate the prepared sample set.");
            Assert.Multiple(() =>
            {
                Assert.That(after.Generation, Is.EqualTo(before.Generation));
                Assert.That(after.Slot, Is.EqualTo(before.Slot));
            });
        }

        [Test]
        public async Task PrepareSamplesDecodesInParallelPreservingSlotOrder()
        {
            string directory = Path.Combine(
                TestContext.CurrentContext.WorkDirectory,
                "parallel-prepared-samples",
                TestContext.CurrentContext.Test.ID);
            Directory.CreateDirectory(directory);
            var paths = new List<string>();
            for (int index = 0; index < 8; index++)
            {
                string path = Path.Combine(
                    directory,
                    $"keysound-{index}.wav");
                createSilentWave(path, 48000, 2, 30 + index * 10);
                paths.Add(path);
            }
            string corruptPath = Path.Combine(directory, "corrupt.wav");
            File.WriteAllBytes(corruptPath, [1, 2, 3]);
            paths.Insert(3, corruptPath);

            await using var engine = new NativeAudioEngine();
            await engine.PrepareSamplesAsync(paths);

            Assert.That(
                engine.TryGetPreparedSampleHandle(corruptPath, out _),
                Is.False,
                "A corrupt keysound must be skipped without failing the set.");
            int expectedSlot = 0;
            foreach (string path in paths)
            {
                if (path == corruptPath)
                    continue;

                Assert.That(
                    engine.TryGetPreparedSampleHandle(path, out var handle),
                    Is.True,
                    $"'{Path.GetFileName(path)}' must be prepared.");
                Assert.That(
                    handle.Slot,
                    Is.EqualTo(expectedSlot),
                    "Parallel decoding must keep slots in input order.");
                expectedSlot++;
            }
        }

        [Test]
        public async Task DisposeReleasesPreparedSamples()
        {
            string directory = Path.Combine(
                TestContext.CurrentContext.WorkDirectory,
                "prepared-sample-disposal",
                TestContext.CurrentContext.Test.ID);
            Directory.CreateDirectory(directory);
            string audioPath = Path.Combine(directory, "sample.wav");
            createSilentWave(audioPath, 48000, 2, 50);

            var engine = new NativeAudioEngine();
            await engine.PrepareSamplesAsync([audioPath]);
            Assert.That(
                engine.TryGetPreparedSampleHandle(audioPath, out _),
                Is.True);

            await engine.DisposeAsync();

            Assert.That(
                engine.TryGetPreparedSampleHandle(audioPath, out _),
                Is.False);
        }

        [Test]
        [Platform(Include = "Win")]
        [NonParallelizable]
        public async Task NativeEngineOpensARealAsioStream()
        {
            if (Environment.GetEnvironmentVariable(
                    "YOKKO_RUN_ASIO_TEST") != "1")
            {
                Assert.Ignore(
                    "Set YOKKO_RUN_ASIO_TEST=1 to run a real ASIO stream.");
            }

            if (!NativeAudioEngine.IsAvailable)
                Assert.Ignore("The Yokko native audio library is unavailable.");

            await using var engine = new NativeAudioEngine();
            AudioDeviceInfo[] devices =
                (await engine.GetOutputDevicesAsync())
                .Where(device =>
                    device.Backend == AudioBackendKind.Asio)
                .ToArray();
            if (devices.Length == 0)
                Assert.Ignore("No 64-bit ASIO drivers were registered.");

            string requestedDeviceId =
                Environment.GetEnvironmentVariable(
                    "YOKKO_ASIO_TEST_DEVICE_ID");
            AudioDeviceInfo device =
                string.IsNullOrWhiteSpace(requestedDeviceId)
                    ? devices[0]
                    : devices.Single(candidate =>
                        candidate.Id == requestedDeviceId);

            await engine.StartAsync(new AudioEngineStartRequest(
                string.Empty,
                AudioBackendKind.Asio,
                device.Id,
                48000,
                64,
                0));
            await Task.Delay(1000);
            AudioEngineStatus status = engine.Status;
            TestContext.Progress.WriteLine(
                $"{device.Name}: {status.SampleRate} Hz, "
                + $"{status.BufferSize} frames, "
                + $"{status.EstimatedOutputLatencyMilliseconds:F3} ms, "
                + $"callbacks={status.CallbackCount}, "
                + $"workMisses={status.CallbackDeadlineMissCount}, "
                + $"cadenceMisses={status.CallbackCadenceMissCount}, "
                + $"backendOverloads={status.BackendOverloadCount}, "
                + $"maxWork={status.MaxCallbackDurationMilliseconds:F3} ms, "
                + $"maxInterval={status.MaxCallbackIntervalMilliseconds:F3} ms");

            Assert.Multiple(() =>
            {
                Assert.That(
                    status.ActiveBackend,
                    Is.EqualTo(AudioBackendKind.Asio));
                Assert.That(status.DeviceName, Is.EqualTo(device.Name));
                Assert.That(status.IsExclusive, Is.True);
                Assert.That(status.IsRunning, Is.True);
                Assert.That(status.IsFaulted, Is.False);
                Assert.That(status.HasUnderrun, Is.False);
                Assert.That(status.SampleRate, Is.EqualTo(48000));
                Assert.That(status.BufferSize, Is.GreaterThan(0));
                Assert.That(status.CallbackCount, Is.GreaterThan(0));
                Assert.That(
                    status.CallbackDeadlineMissCount,
                    Is.Zero);
            });

            await engine.StopAsync();
        }

        [Test]
        [Platform(Include = "Win")]
        [NonParallelizable]
        public async Task NativeEngineDecodesAndOpensARealWasapiStream()
        {
            if (!NativeAudioEngine.IsAvailable)
                Assert.Ignore("Native audio library is not built.");

            string directory = Path.Combine(
                TestContext.CurrentContext.WorkDirectory,
                "native-audio",
                TestContext.CurrentContext.Test.ID);
            Directory.CreateDirectory(directory);
            string audioPath = Path.Combine(directory, "silence.wav");
            createSilentWave(audioPath, 48000, 2, 5000);

            await using var engine = new NativeAudioEngine();
            await engine.PrepareSamplesAsync([audioPath]);
            Assert.That(
                engine.TryGetPreparedSampleHandle(audioPath, out var sampleHandle),
                Is.True);
            var devices = await engine.GetOutputDevicesAsync();
            AudioDeviceInfo[] uniqueDevices = devices
                                              .Where(device => device.Backend == AudioBackendKind.WasapiExclusive)
                                              .OrderBy(device => device.IsDefault)
                                              .ToArray();
            bool opened = false;
            AudioDeviceInfo openedDevice = null;
            foreach (AudioDeviceInfo device in uniqueDevices)
            {
                try
                {
                    await engine.StartAsync(new AudioEngineStartRequest(
                        audioPath,
                        AudioBackendKind.WasapiExclusive,
                        device.Id,
                        48000,
                        128,
                        0));
                    TestContext.Progress.WriteLine(
                        $"Opened {device.Name}: {engine.Status.ActiveBackend}, "
                        + $"{engine.Status.BufferSize} frames, "
                        + $"{engine.Status.EstimatedOutputLatencyMilliseconds:F2} ms");
                    await Task.Delay(250);
                    opened = true;
                    openedDevice = device;
                    break;
                }
                catch (Exception exception)
                    when (exception.Message.Contains(
                        "BackendUnavailable",
                        StringComparison.Ordinal))
                {
                    TestContext.Progress.WriteLine(
                        $"Busy or unsupported endpoint {device.Name}: {exception.Message}");
                }
            }

            if (!opened)
            {
                Assert.Ignore(
                    "No active WASAPI endpoint could be opened; another application may own them exclusively.");
            }

            AudioEngineStatus status = engine.Status;
            TestContext.Progress.WriteLine(
                $"Callbacks={status.CallbackCount}, "
                + $"deadline misses={status.CallbackDeadlineMissCount}, "
                + $"cadence misses={status.CallbackCadenceMissCount}, "
                + $"backend overloads={status.BackendOverloadCount}, "
                + $"max={status.MaxCallbackDurationMilliseconds:F3} ms / "
                + $"budget={status.CallbackBudgetMilliseconds:F3} ms, "
                + $"interval={status.MaxCallbackIntervalMilliseconds:F3} ms, "
                + $"backend error=0x{status.BackendError:X8} "
                + $"at stage {status.BackendErrorStage}");
            Assert.That(
                status.ActiveBackend,
                Is.AnyOf(
                    AudioBackendKind.WasapiExclusive,
                    AudioBackendKind.SharedWasapi));
            Assert.That(status.SampleRate, Is.EqualTo(48000));
            Assert.That(status.BufferSize, Is.GreaterThan(0));
            Assert.That(status.IsRunning, Is.True);
            Assert.That(status.HasUnderrun, Is.False);
            Assert.That(
                engine.TriggerSample(audioPath),
                Is.True,
                "A prepared keysound must enter the native callback queue.");
            Assert.That(
                engine.TriggerPreparedSample(sampleHandle, 0.5),
                Is.True,
                "A prepared handle must enter the same native callback queue.");
            uint loopId = engine.StartLoopingPreparedSample(sampleHandle, 0.5);
            Assert.That(loopId, Is.GreaterThan(0));
            Assert.That(engine.StopLoopingSample(loopId), Is.True);
            Assert.That(
                status.CallbackCount,
                Is.GreaterThan(1),
                "The event-driven output callback must continue after startup.");
            Assert.That(status.CallbackBudgetMilliseconds, Is.GreaterThan(0));
            Assert.That(status.MaxCallbackDurationMilliseconds, Is.GreaterThanOrEqualTo(0));
            Assert.That(status.MaxCallbackIntervalMilliseconds, Is.GreaterThanOrEqualTo(0));

            double normalStart = engine.PlaybackTimeMilliseconds;
            long normalWallStart = Stopwatch.GetTimestamp();
            await Task.Delay(800);
            double normalWallAdvance =
                Stopwatch.GetElapsedTime(normalWallStart).TotalMilliseconds;
            double normalAdvance =
                engine.PlaybackTimeMilliseconds - normalStart;
            double normalObservedRate = normalAdvance / normalWallAdvance;

            var clockSamples = new List<double>();
            for (int index = 0; index < 16; index++)
            {
                clockSamples.Add(engine.PlaybackTimeMilliseconds);
                await Task.Delay(1);
            }
            Assert.That(
                clockSamples.Zip(clockSamples.Skip(1), (left, right) => right >= left),
                Is.All.True,
                "The hardware playback clock must remain monotonic.");
            Assert.That(
                clockSamples.Distinct().Count(),
                Is.GreaterThan(8),
                "QPC interpolation should avoid a buffer-sized clock staircase.");

            await engine.SeekAsync(1000);
            double positionAfterSeek = engine.PlaybackTimeMilliseconds;
            Assert.That(
                positionAfterSeek,
                Is.InRange(950, 1250),
                "The public playback clock must retain the seek base position.");
            Assert.That(
                engine.TryGetPreparedSampleHandle(
                    audioPath,
                    out var afterSeekHandle),
                Is.True);
            Assert.That(
                afterSeekHandle.Generation,
                Is.EqualTo(sampleHandle.Generation),
                "An in-place seek restart must not rebuild the prepared sample set.");
            Assert.That(
                engine.TriggerPreparedSample(sampleHandle, 1),
                Is.True,
                "Prepared handles must survive an in-place seek restart.");

            await engine.PrepareSamplesAsync([audioPath]);
            Assert.That(
                engine.TryGetPreparedSampleHandle(audioPath, out var replacementHandle),
                Is.True);
            Assert.That(
                engine.TriggerPreparedSample(sampleHandle, 1),
                Is.False,
                "Preparing a new sample set must invalidate old handles.");
            await engine.SeekAsync(1000);
            Assert.Multiple(() =>
            {
                Assert.That(
                    engine.TriggerPreparedSample(replacementHandle, 1),
                    Is.True);
                Assert.That(
                    engine.TriggerPreparedSample(sampleHandle, 1),
                    Is.False);
            });

            await engine.StopAsync();
            Assert.That(engine.Status.IsRunning, Is.False);

            await engine.StartAsync(new AudioEngineStartRequest(
                audioPath,
                AudioBackendKind.WasapiExclusive,
                openedDevice.Id,
                48000,
                128,
                0,
                1.5,
                AudioPitchMode.Preserve));
            await Task.Delay(800);
            double ratedStart = engine.PlaybackTimeMilliseconds;
            long wallStart = Stopwatch.GetTimestamp();
            await Task.Delay(800);
            double wallAdvance = Stopwatch.GetElapsedTime(wallStart).TotalMilliseconds;
            double ratedAdvance =
                engine.PlaybackTimeMilliseconds - ratedStart;
            double observedRate = ratedAdvance / wallAdvance;
            double relativeRate = observedRate / normalObservedRate;
            Assert.That(
                relativeRate,
                Is.InRange(1.35, 1.65),
                "A real 1.5x output stream must advance the chart clock at "
                + "approximately 1.5x the same endpoint's normal stream. "
                + $"Observed normal={normalObservedRate:F3}x wall, "
                + $"rated={observedRate:F3}x wall, "
                + $"relative={relativeRate:F3}x.");
            await engine.StopAsync();

            await engine.StartAsync(new AudioEngineStartRequest(
                string.Empty,
                AudioBackendKind.WasapiExclusive,
                openedDevice.Id,
                48000,
                128,
                0));
            await Task.Delay(50);
            Assert.That(
                engine.PlaybackTimeMilliseconds,
                Is.GreaterThan(0),
                "Keysound-only charts need a native silence clock.");
            Assert.That(engine.TriggerSample(audioPath), Is.True);
            await engine.StopAsync();
        }

        private static void createSilentWave(
            string path,
            int sampleRate,
            short channels,
            int durationMilliseconds)
        {
            const short bitsPerSample = 16;
            int frameCount = sampleRate * durationMilliseconds / 1000;
            int dataLength = frameCount * channels * bitsPerSample / 8;
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
            writer.Write(new byte[dataLength]);
        }
    }
}
