using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Win32;
using Yokko.Import;

namespace Yokko.Desktop;

internal static class WindowsFileAssociationRegistrar
{
    internal const string ProgId = "Yokko.Beatmap";

    internal static IReadOnlyList<string> AssociatedExtensions { get; } =
        KnownChartImporters.FileExtensions
                           // .zip is intentionally omitted because it is a
                           // general archive extension, not a beatmap format.
                           .Where(static extension =>
                               !extension.Equals(
                                   ".zip",
                                   StringComparison.OrdinalIgnoreCase))
                           .Order(StringComparer.OrdinalIgnoreCase)
                           .ToArray();

    [SupportedOSPlatform("windows")]
    public static bool TryRegister(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath)
            || !Path.GetExtension(executablePath).Equals(
                ".exe",
                StringComparison.OrdinalIgnoreCase)
            || !File.Exists(executablePath))
        {
            return false;
        }

        try
        {
            registerApplication(executablePath);
            registerProgId(executablePath);

            foreach (string extension in AssociatedExtensions)
                registerExtension(extension);

            return true;
        }
        catch (Exception exception) when (
            exception is UnauthorizedAccessException
            or System.Security.SecurityException
            or IOException)
        {
            // File association is a convenience. A locked-down registry must
            // never prevent Yokko itself from starting.
            return false;
        }
    }

    internal static string BuildOpenCommand(string executablePath) =>
        $"\"{executablePath}\" \"%1\"";

    [SupportedOSPlatform("windows")]
    private static void registerApplication(string executablePath)
    {
        using RegistryKey application = Registry.CurrentUser.CreateSubKey(
            @"Software\Classes\Applications\Yokko.exe");
        application.SetValue("FriendlyAppName", "Yokko");

        using RegistryKey icon = application.CreateSubKey("DefaultIcon");
        icon.SetValue(null, $"\"{executablePath}\",0");

        using RegistryKey command = application.CreateSubKey(
            @"shell\open\command");
        command.SetValue(null, BuildOpenCommand(executablePath));

        using RegistryKey supportedTypes = application.CreateSubKey(
            "SupportedTypes");
        foreach (string extension in AssociatedExtensions)
            supportedTypes.SetValue(extension, string.Empty);
    }

    [SupportedOSPlatform("windows")]
    private static void registerProgId(string executablePath)
    {
        using RegistryKey progId = Registry.CurrentUser.CreateSubKey(
            $@"Software\Classes\{ProgId}");
        progId.SetValue(null, "Yokko beatmap");

        using RegistryKey icon = progId.CreateSubKey("DefaultIcon");
        icon.SetValue(null, $"\"{executablePath}\",0");

        using RegistryKey command = progId.CreateSubKey(
            @"shell\open\command");
        command.SetValue(null, BuildOpenCommand(executablePath));
    }

    [SupportedOSPlatform("windows")]
    private static void registerExtension(string extension)
    {
        using RegistryKey openWithProgIds = Registry.CurrentUser.CreateSubKey(
            $@"Software\Classes\{extension}\OpenWithProgids");
        openWithProgIds.SetValue(
            ProgId,
            Array.Empty<byte>(),
            RegistryValueKind.None);

        using RegistryKey openWithList = Registry.CurrentUser.CreateSubKey(
            $@"Software\Classes\{extension}\OpenWithList\Yokko.exe");
    }
}
