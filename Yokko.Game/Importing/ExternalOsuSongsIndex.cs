using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Yokko.Core.Difficulty;
using Yokko.Import;

namespace Yokko.Game.Importing;

internal sealed record ExternalOsuIndexEntry(
    string SourcePath,
    long Length,
    long LastWriteTimeUtcTicks,
    ChartImportResult Result,
    string ArtworkPath,
    ManiaMsdResult DifficultyRating,
    ManiaStarRatingResult StarRating,
    double LengthMilliseconds = 0,
    double Bpm = 0,
    string BeatmapFingerprint = null);

internal sealed record ExternalOsuIndexDocument(
    int Version,
    string SongsPath,
    IReadOnlyList<ExternalOsuIndexEntry> Entries);

internal sealed record ExternalOsuFileSnapshot(
    string Path,
    long Length,
    long LastWriteTimeUtcTicks);

internal static class ExternalOsuSongsIndex
{
    internal const int CurrentVersion = 2;

    private static readonly JsonSerializerOptions jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    internal static string ResolveSongsPath(string selectedPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(selectedPath);
        string fullPath = Path.GetFullPath(
            Environment.ExpandEnvironmentVariables(selectedPath.Trim()));

        if (Path.GetFileName(fullPath.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar))
            .Equals("Songs", StringComparison.OrdinalIgnoreCase))
        {
            return fullPath;
        }

        string child = Path.Combine(fullPath, "Songs");
        if (Directory.Exists(child))
            return Path.GetFullPath(child);

        throw new DirectoryNotFoundException(
            "Select an osu!stable Songs directory or its osu! installation directory.");
    }

    internal static ExternalOsuIndexDocument Load(
        string cachePath,
        string configuredSongsPath)
    {
        if (string.IsNullOrWhiteSpace(cachePath)
            || string.IsNullOrWhiteSpace(configuredSongsPath)
            || !File.Exists(cachePath))
        {
            return null;
        }

        try
        {
            ExternalOsuIndexDocument document =
                JsonSerializer.Deserialize<ExternalOsuIndexDocument>(
                    File.ReadAllText(cachePath),
                    jsonOptions);
            if (document?.Version != CurrentVersion
                || !pathsEqual(document.SongsPath, configuredSongsPath)
                || document.Entries == null)
            {
                return null;
            }

            return document;
        }
        catch (Exception exception) when (exception is IOException
                                          or UnauthorizedAccessException
                                          or ArgumentException
                                          or JsonException
                                          or NotSupportedException)
        {
            return null;
        }
    }

    internal static void Save(
        string cachePath,
        string songsPath,
        IReadOnlyList<ExternalOsuIndexEntry> entries)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cachePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(songsPath);
        ArgumentNullException.ThrowIfNull(entries);

        string directory = Path.GetDirectoryName(cachePath)!;
        Directory.CreateDirectory(directory);
        string temporaryPath = cachePath + ".tmp";
        var document = new ExternalOsuIndexDocument(
            CurrentVersion,
            Path.GetFullPath(songsPath),
            entries);

        File.WriteAllText(
            temporaryPath,
            JsonSerializer.Serialize(document, jsonOptions));
        File.Move(temporaryPath, cachePath, true);
    }

    internal static IReadOnlyList<ExternalOsuFileSnapshot> EnumerateFiles(
        string songsPath,
        out bool complete)
    {
        var files = new List<ExternalOsuFileSnapshot>();
        var pending = new Stack<string>();
        pending.Push(Path.GetFullPath(songsPath));
        complete = true;

        while (pending.Count > 0)
        {
            string directory = pending.Pop();
            string[] osuFiles;

            try
            {
                osuFiles = Directory.GetFiles(
                    directory,
                    "*.osu",
                    SearchOption.TopDirectoryOnly);
            }
            catch (Exception exception) when (exception is IOException
                                              or UnauthorizedAccessException)
            {
                complete = false;
                continue;
            }

            if (osuFiles.Length > 0)
            {
                foreach (string path in osuFiles)
                {
                    try
                    {
                        var info = new FileInfo(path);
                        files.Add(new ExternalOsuFileSnapshot(
                            info.FullName,
                            info.Length,
                            info.LastWriteTimeUtc.Ticks));
                    }
                    catch (Exception exception) when (exception is IOException
                                                      or UnauthorizedAccessException)
                    {
                        complete = false;
                    }
                }

                // Matches osu!stable's set-folder semantics. Once a folder
                // contains .osu files it is one set and asset subdirectories
                // must not be treated as additional sets.
                continue;
            }

            try
            {
                foreach (string child in Directory.GetDirectories(directory))
                    pending.Push(child);
            }
            catch (Exception exception) when (exception is IOException
                                              or UnauthorizedAccessException)
            {
                complete = false;
            }
        }

        return files.OrderBy(file => file.Path, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
    }

    internal static bool IsManiaFile(string path)
    {
        using FileStream stream = new(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            4096,
            FileOptions.SequentialScan);
        using var reader = new StreamReader(
            stream,
            Encoding.UTF8,
            true,
            4096);

        bool inGeneral = false;
        while (reader.ReadLine() is string rawLine)
        {
            string line = rawLine.Trim();
            if (line.Equals("[General]", StringComparison.OrdinalIgnoreCase))
            {
                inGeneral = true;
                continue;
            }

            if (!inGeneral)
                continue;

            if (line.StartsWith("[", StringComparison.Ordinal))
                return false;

            int separator = line.IndexOf(':');
            if (separator < 0
                || !line[..separator].Trim()
                    .Equals("Mode", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return line[(separator + 1)..].Trim()
                       .Equals("3", StringComparison.Ordinal);
        }

        return false;
    }

    internal static bool pathsEqual(string first, string second)
    {
        if (string.IsNullOrWhiteSpace(first)
            || string.IsNullOrWhiteSpace(second))
        {
            return false;
        }

        return Path.GetFullPath(first).TrimEnd(
                   Path.DirectorySeparatorChar,
                   Path.AltDirectorySeparatorChar)
               .Equals(
                   Path.GetFullPath(second).TrimEnd(
                       Path.DirectorySeparatorChar,
                       Path.AltDirectorySeparatorChar),
                   OperatingSystem.IsWindows()
                       ? StringComparison.OrdinalIgnoreCase
                       : StringComparison.Ordinal);
    }
}
