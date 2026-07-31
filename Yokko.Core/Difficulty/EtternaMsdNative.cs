using System.Reflection;
using System.Runtime.InteropServices;

namespace Yokko.Core.Difficulty;

internal static class EtternaMsdNativeLibrary
{
    private static readonly object sync = new();
    private static nint loadedHandle;
    private static bool resolverInstalled;

    internal static bool IsAvailable
    {
        get
        {
            try
            {
                EnsureLoaded();
                return EtternaMsdNative.GetAbiVersion()
                       == EtternaMsdNative.AbiVersion;
            }
            catch
            {
                return false;
            }
        }
    }

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
            {
                throw new DllNotFoundException(
                    "Yokko's Etterna MinaCalc library was not found. "
                    + "Build it with scripts/build-native-minacalc.ps1.");
            }

            loadedHandle = NativeLibrary.Load(libraryPath);
            if (!resolverInstalled)
            {
                NativeLibrary.SetDllImportResolver(
                    typeof(EtternaMsdNative).Assembly,
                    resolve);
                resolverInstalled = true;
            }
        }
    }

    private static nint resolve(
        string libraryName,
        Assembly assembly,
        DllImportSearchPath? searchPath)
        => string.Equals(
            libraryName,
            EtternaMsdNative.LibraryName,
            StringComparison.Ordinal)
            ? loadedHandle
            : 0;

    private static string? findLibraryPath()
    {
        string? configured =
            Environment.GetEnvironmentVariable("YOKKO_MINACALC_TEST_DLL");
        if (!string.IsNullOrWhiteSpace(configured)
            && File.Exists(configured))
        {
            return Path.GetFullPath(configured);
        }

        string libraryFileName = OperatingSystem.IsWindows()
            ? "yokko_minacalc_native.dll"
            : OperatingSystem.IsMacOS()
                ? "libyokko_minacalc_native.dylib"
                : "libyokko_minacalc_native.so";
        string outputPath = Path.Combine(
            AppContext.BaseDirectory,
            libraryFileName);
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
                    "native-minacalc",
                    configuration,
                    libraryFileName);
                if (File.Exists(candidate))
                    return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }
}

internal static unsafe partial class EtternaMsdNative
{
    internal const string LibraryName = "yokko_minacalc_native";
    internal const uint AbiVersion = 1;

    [LibraryImport(
        LibraryName,
        EntryPoint = "yokko_minacalc_get_abi_version")]
    internal static partial uint GetAbiVersion();

    [LibraryImport(
        LibraryName,
        EntryPoint = "yokko_minacalc_get_version")]
    internal static partial int GetVersion();

    [LibraryImport(
        LibraryName,
        EntryPoint = "yokko_minacalc_calculate")]
    internal static partial EtternaMsdNativeResult Calculate(
        EtternaMsdNativeNote* notes,
        nuint noteCount,
        uint keyCount,
        float musicRate,
        ref EtternaMsdNativeOutput output);
}

[StructLayout(LayoutKind.Sequential)]
internal readonly struct EtternaMsdNativeNote(uint notes, float rowTime)
{
    internal readonly uint Notes = notes;
    internal readonly float RowTime = rowTime;
}

[StructLayout(LayoutKind.Sequential)]
internal struct EtternaMsdNativeOutput
{
    internal uint StructSize;
    internal float Overall;
    internal float Stream;
    internal float Jumpstream;
    internal float Handstream;
    internal float Stamina;
    internal float JackSpeed;
    internal float Chordjack;
    internal float Technical;

    internal static EtternaMsdNativeOutput Create() => new()
    {
        StructSize = (uint)Marshal.SizeOf<EtternaMsdNativeOutput>(),
    };
}

internal enum EtternaMsdNativeResult
{
    Ok,
    InvalidArgument,
    InvalidChart,
    CalculationFailed,
}

