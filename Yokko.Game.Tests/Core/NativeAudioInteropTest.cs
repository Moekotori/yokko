using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using NUnit.Framework;
using Yokko.Audio.Native;

namespace Yokko.Game.Tests.Core
{
    [TestFixture]
    public sealed class NativeAudioInteropTest
    {
        private static readonly object resolverLock = new();
        private static bool resolverInstalled;

        [Test]
        [Platform(Include = "Win")]
        public void ManagedBoundaryMatchesNativeAbiAndLifecycle()
        {
            string nativeLibraryPath =
                Environment.GetEnvironmentVariable("YOKKO_NATIVE_AUDIO_TEST_DLL")
                ?? findDefaultNativeLibraryPath();

            if (!File.Exists(nativeLibraryPath))
                Assert.Ignore($"Native audio library is not built: {nativeLibraryPath}");

            installResolver(nativeLibraryPath);

            using var core = new NativeAudioCore(
                sampleRate: 48000,
                channels: 2,
                ringCapacityFrames: 8,
                startupThresholdFrames: 4);

            Assert.That(core.GetStatus().State, Is.EqualTo(NativeAudioState.Idle));
            Assert.That(core.Submit(new float[8]), Is.EqualTo(4));
            Assert.That(core.GetStatus().State, Is.EqualTo(NativeAudioState.Primed));

            core.Start();
            Assert.That(core.GetStatus().State, Is.EqualTo(NativeAudioState.Running));

            core.Pause();
            Assert.That(core.GetStatus().State, Is.EqualTo(NativeAudioState.Paused));

            core.Stop();
            NativeAudioStatus stopped = core.GetStatus();
            Assert.That(stopped.State, Is.EqualTo(NativeAudioState.Idle));
            Assert.That(stopped.BufferedFrames, Is.Zero);
            Assert.That(stopped.UnderrunCount, Is.Zero);
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

        private static void installResolver(string nativeLibraryPath)
        {
            lock (resolverLock)
            {
                if (resolverInstalled)
                    return;

                Assembly audioAssembly = typeof(NativeAudioInterop).Assembly;
                NativeLibrary.SetDllImportResolver(
                    audioAssembly,
                    (libraryName, _, _) =>
                        string.Equals(
                            libraryName,
                            NativeAudioInterop.LibraryName,
                            StringComparison.Ordinal)
                            ? NativeLibrary.Load(nativeLibraryPath)
                            : IntPtr.Zero);
                resolverInstalled = true;
            }
        }
    }
}
