using System;
using System.Collections.Generic;
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
            createSilentWave(audioPath, 48000, 2, 2000);

            await using var engine = new NativeAudioEngine();
            await engine.PrepareSamplesAsync([audioPath]);
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
                status.CallbackCount,
                Is.GreaterThan(1),
                "The event-driven output callback must continue after startup.");
            Assert.That(status.CallbackBudgetMilliseconds, Is.GreaterThan(0));
            Assert.That(status.MaxCallbackDurationMilliseconds, Is.GreaterThanOrEqualTo(0));
            Assert.That(status.MaxCallbackIntervalMilliseconds, Is.GreaterThanOrEqualTo(0));

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

            await engine.StopAsync();
            Assert.That(engine.Status.IsRunning, Is.False);

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
