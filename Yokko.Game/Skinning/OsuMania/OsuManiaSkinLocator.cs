using System;
using System.IO;

namespace Yokko.Game.Skinning.OsuMania;

internal static class OsuManiaSkinLocator
{
    public const string EnvironmentVariable = "YOKKO_OSU_MANIA_SKIN";

    public static string FindConfiguredPath()
    {
        string environmentPath = Environment.GetEnvironmentVariable(EnvironmentVariable);

        if (!string.IsNullOrWhiteSpace(environmentPath) &&
            (Directory.Exists(environmentPath) || File.Exists(environmentPath)))
            return Path.GetFullPath(environmentPath);

        string currentDirectory = Environment.CurrentDirectory;
        string extractedSkin = Path.Combine(currentDirectory, "Skins", "Current");

        if (Directory.Exists(extractedSkin))
            return extractedSkin;

        string packagedSkin = Path.Combine(currentDirectory, "Skins", "current.osk");
        return File.Exists(packagedSkin) ? packagedSkin : null;
    }
}
