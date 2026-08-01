using System;
using System.Collections.Generic;
using System.IO;

namespace Yokko.Game.Importing;

/// <summary>
/// Locates an osu!stable Songs directory without modifying the installation.
/// Discovery is intentionally bounded to known paths and stable config files.
/// </summary>
internal static class ExternalOsuSongsLocator
{
    internal static string Find(
        string configuredSongsPath = null,
        string localApplicationDataPath = null)
    {
        string configured = tryResolveExisting(configuredSongsPath);
        if (configured != null)
            return configured;

        string localAppData = string.IsNullOrWhiteSpace(
            localApplicationDataPath)
            ? Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData)
            : localApplicationDataPath;

        if (string.IsNullOrWhiteSpace(localAppData))
            return null;

        string osuRoot;
        try
        {
            osuRoot = Path.GetFullPath(Path.Combine(localAppData, "osu!"));
        }
        catch (Exception exception) when (exception is ArgumentException
                                          or NotSupportedException
                                          or PathTooLongException)
        {
            return null;
        }

        foreach (string configPath in enumerateStableConfigs(osuRoot))
        {
            string beatmapDirectory = readBeatmapDirectory(configPath);
            if (string.IsNullOrWhiteSpace(beatmapDirectory))
                continue;

            string candidate = Environment.ExpandEnvironmentVariables(
                beatmapDirectory.Trim().Trim('"'));
            if (!Path.IsPathFullyQualified(candidate))
                candidate = Path.Combine(osuRoot, candidate);

            string resolved = tryResolveExisting(candidate);
            if (resolved != null)
                return resolved;
        }

        return tryResolveExisting(Path.Combine(osuRoot, "Songs"));
    }

    private static IEnumerable<string> enumerateStableConfigs(string osuRoot)
    {
        try
        {
            string[] configs = Directory.GetFiles(
                osuRoot,
                "osu!.*.cfg",
                SearchOption.TopDirectoryOnly);
            Array.Sort(configs, (left, right) =>
                getLastWriteTimeUtc(right).CompareTo(
                    getLastWriteTimeUtc(left)));
            return configs;
        }
        catch (Exception exception) when (exception is IOException
                                          or UnauthorizedAccessException
                                          or ArgumentException
                                          or NotSupportedException)
        {
            return Array.Empty<string>();
        }
    }

    private static DateTime getLastWriteTimeUtc(string path)
    {
        try
        {
            return File.GetLastWriteTimeUtc(path);
        }
        catch (Exception exception) when (exception is IOException
                                          or UnauthorizedAccessException
                                          or ArgumentException
                                          or NotSupportedException)
        {
            return DateTime.MinValue;
        }
    }

    private static string readBeatmapDirectory(string configPath)
    {
        try
        {
            foreach (string line in File.ReadLines(configPath))
            {
                int separator = line.IndexOf('=');
                if (separator < 0
                    || !line[..separator].Trim().Equals(
                        "BeatmapDirectory",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return line[(separator + 1)..].Trim();
            }
        }
        catch (Exception exception) when (exception is IOException
                                          or UnauthorizedAccessException
                                          or ArgumentException
                                          or NotSupportedException)
        {
        }

        return null;
    }

    private static string tryResolveExisting(string candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
            return null;

        try
        {
            string resolved = ExternalOsuSongsIndex.ResolveSongsPath(candidate);
            return Directory.Exists(resolved) ? resolved : null;
        }
        catch (Exception exception) when (exception is IOException
                                          or UnauthorizedAccessException
                                          or ArgumentException
                                          or NotSupportedException)
        {
            return null;
        }
    }
}
