using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Yokko.Audio.Native;

namespace Yokko.Game.Tests.Core
{
    [TestFixture]
    public sealed class NativeAudioInteropTest
    {
        [Test]
        [Platform(Include = "Win")]
        [NonParallelizable]
        public void ManagedBoundaryMatchesNativeAbiAndLifecycle()
        {
            string nativeLibraryPath =
                Environment.GetEnvironmentVariable("YOKKO_NATIVE_AUDIO_TEST_DLL")
                ?? findDefaultNativeLibraryPath();

            if (!File.Exists(nativeLibraryPath))
                Assert.Ignore($"Native audio library is not built: {nativeLibraryPath}");

            Environment.SetEnvironmentVariable(
                "YOKKO_NATIVE_AUDIO_TEST_DLL",
                nativeLibraryPath);

            using var core = new NativeAudioCore(
                sampleRate: 48000,
                channels: 2,
                ringCapacityFrames: 8,
                startupThresholdFrames: 4);

            Assert.That(core.GetStatus().State, Is.EqualTo(NativeAudioState.Idle));
            uint sampleId = core.RegisterSample(
                new float[] { 0.25f, -0.25f, 0.5f, -0.5f });
            Assert.That(sampleId, Is.GreaterThan(0));
            Assert.That(core.Submit(new float[8]), Is.EqualTo(4));
            Assert.That(core.GetStatus().State, Is.EqualTo(NativeAudioState.Primed));

            core.Start();
            Assert.That(core.GetStatus().State, Is.EqualTo(NativeAudioState.Running));
            core.ReportPresentedPosition(4800, 480, 0);
            NativeAudioStatus correlated = core.GetStatus();
            Assert.Multiple(() =>
            {
                Assert.That(correlated.AbiVersion, Is.EqualTo(11));
                Assert.That(correlated.HasPresentedPosition, Is.EqualTo(1));
                Assert.That(correlated.PresentedFramePosition, Is.EqualTo(4800));
                Assert.That(correlated.PositionObservationTime100ns, Is.Zero);
            });
            Assert.That(core.TriggerSample(sampleId), Is.True);
            uint loopId = core.StartLoopingSample(sampleId, 0.5f);
            Assert.That(loopId, Is.GreaterThan(0));
            Assert.That(core.StopLoopingSample(loopId), Is.True);

            core.Pause();
            Assert.That(core.GetStatus().State, Is.EqualTo(NativeAudioState.Paused));

            core.Stop();
            NativeAudioStatus stopped = core.GetStatus();
            Assert.That(stopped.State, Is.EqualTo(NativeAudioState.Idle));
            Assert.That(stopped.BufferedFrames, Is.Zero);
            Assert.That(stopped.UnderrunCount, Is.Zero);
        }

        [Test]
        [Platform(Include = "Win")]
        [NonParallelizable]
        public void AsioDiscoveryIsPassiveAndUsesBackendScopedIds()
        {
            string nativeLibraryPath =
                Environment.GetEnvironmentVariable(
                    "YOKKO_NATIVE_AUDIO_TEST_DLL")
                ?? findDefaultNativeLibraryPath();

            if (!File.Exists(nativeLibraryPath))
            {
                Assert.Ignore(
                    $"Native audio library is not built: {nativeLibraryPath}");
            }

            Environment.SetEnvironmentVariable(
                "YOKKO_NATIVE_AUDIO_TEST_DLL",
                nativeLibraryPath);
            NativeAudioResult result =
                NativeAudioInterop.GetAsioDeviceCount(
                    out uint reportedCount);
            if (result == NativeAudioResult.BackendUnavailable)
            {
                Assert.That(reportedCount, Is.Zero);
                return;
            }

            Assert.That(result, Is.EqualTo(NativeAudioResult.Ok));
            NativeAsioDevice[] devices =
                NativeAsioDevices.Enumerate().ToArray();
            Assert.Multiple(() =>
            {
                Assert.That(
                    devices,
                    Has.Length.EqualTo((int)reportedCount));
                Assert.That(
                    devices.Select(device => device.Id),
                    Is.Unique);
                Assert.That(
                    devices,
                    Has.All.Matches<NativeAsioDevice>(
                        device =>
                            device.Id.StartsWith(
                                "asio:{",
                                StringComparison.OrdinalIgnoreCase)
                            && !string.IsNullOrWhiteSpace(
                                device.Name)));
            });
        }

        private static string findDefaultNativeLibraryPath()
        {
            string repositoryRoot = Path.GetFullPath(
                Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", ".."));
            return Path.Combine(
                repositoryRoot,
                "artifacts",
                "native-audio",
                "Release",
                "yokko_audio_native.dll");
        }

    }
}
