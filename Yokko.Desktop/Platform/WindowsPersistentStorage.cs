using System;
using System.IO;

namespace Yokko.Desktop.Platform;

internal static class WindowsPersistentStorage
{
    public static string RootPath
    {
        get
        {
            string applicationData = Environment.GetFolderPath(
                Environment.SpecialFolder.ApplicationData,
                Environment.SpecialFolderOption.Create);
            return string.IsNullOrWhiteSpace(applicationData)
                ? string.Empty
                : Path.Combine(applicationData, "Yokko");
        }
    }
}
