using System.Reflection;
using System.Runtime.InteropServices;

namespace Yokko.Audio.Native;

internal static class NativeAudioLibrary
{
    private static readonly object sync = new();
    private static nint loadedHandle;
    private static bool resolverInstalled;

    internal static void EnsureLoaded()
    {
        if (loadedHandle != 0)
            return;

        lock (sync)
        {
            if (loadedHandle != 0)
                return;

            string? libraryPath = findLibraryPath();
            if (libraryPath == null)
                throw new NativeAudioException(
                    "Yokko native audio library was not found. Build it with scripts/test-native-audio.ps1.");

            loadedHandle = NativeLibrary.Load(libraryPath);
            if (!resolverInstalled)
            {
                NativeLibrary.SetDllImportResolver(
                    typeof(NativeAudioInterop).Assembly,
                    resolve);
                resolverInstalled = true;
            }
        }
    }

    internal static bool IsAvailable
    {
        get
        {
            if (!OperatingSystem.IsWindows())
                return false;

            try
            {
                EnsureLoaded();
                return NativeAudioInterop.GetAbiVersion()
                       == NativeAudioInterop.AbiVersion;
            }
            catch
            {
                return false;
            }
        }
    }

    private static nint resolve(
        string libraryName,
        Assembly assembly,
        DllImportSearchPath? searchPath)
        => string.Equals(
            libraryName,
            NativeAudioInterop.LibraryName,
            StringComparison.Ordinal)
            ? loadedHandle
            : 0;

    private static string? findLibraryPath()
    {
        string? configured =
            Environment.GetEnvironmentVariable("YOKKO_NATIVE_AUDIO_TEST_DLL");
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
            return Path.GetFullPath(configured);

        string outputPath = Path.Combine(
            AppContext.BaseDirectory,
            "yokko_audio_native.dll");
        if (File.Exists(outputPath))
            return outputPath;

        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory != null)
        {
            foreach (string configuration in new[] { "Debug", "Release" })
            {
                string candidate = Path.Combine(
                    directory.FullName,
                    "artifacts",
                    "native-audio",
                    configuration,
                    "yokko_audio_native.dll");
                if (File.Exists(candidate))
                    return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
