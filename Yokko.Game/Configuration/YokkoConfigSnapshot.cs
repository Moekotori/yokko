using System;
using osu.Framework.Platform;

namespace Yokko.Game.Configuration;

/// <summary>
/// Export, import and reset persisted Yokko settings as portable ini text.
/// </summary>
internal static class YokkoConfigSnapshot
{
    public static string Export(YokkoConfigManager config)
    {
        config.Save();
        return config.ReadIniText();
    }

    public static bool TryImport(YokkoConfigManager config, string iniText)
    {
        if (string.IsNullOrWhiteSpace(iniText))
            return false;

        try
        {
            config.WriteIniText(iniText);
            config.Save();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static void ResetAll(YokkoConfigManager config) =>
        config.ResetPersistedFile();
}
