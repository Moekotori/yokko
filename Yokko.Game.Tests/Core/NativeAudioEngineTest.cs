using System;
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
            createSilentWave(audioPath, 48000, 2, 500);

            await using var engine = new NativeAudioEngine();
            var devices = await engine.GetOutputDevicesAsync();
            AudioDeviceInfo[] uniqueDevices = devices
                                              .Where(device => device.Backend == AudioBackendKind.WasapiExclusive)
                                              .OrderBy(device => device.IsDefault)
                                              .ToArray();
            bool opened = false;
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
                    opened = true;
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
            Assert.That(
                status.ActiveBackend,
                Is.AnyOf(
                    AudioBackendKind.WasapiExclusive,
                    AudioBackendKind.SharedWasapi));
            Assert.That(status.SampleRate, Is.EqualTo(48000));
            Assert.That(status.BufferSize, Is.GreaterThan(0));
            Assert.That(status.IsRunning, Is.True);
            Assert.That(status.HasUnderrun, Is.False);

            await engine.StopAsync();
            Assert.That(engine.Status.IsRunning, Is.False);
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
