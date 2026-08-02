using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Yokko.Game.Importing;

internal sealed record ManagedChartFileSnapshot(
    string RelativePath,
    long Length,
    long LastWriteTimeUtcTicks);

internal sealed record ManagedChartDirectorySnapshot(
    string RelativePath,
    long LastWriteTimeUtcTicks);

internal sealed record ManagedChartIndexEntry(
    string SourceRelativePath,
    long SourceLength,
    long SourceLastWriteTimeUtcTicks,
    IReadOnlyList<ImportedChart> Charts,
    IReadOnlyList<ManagedChartFileSnapshot> Dependencies,
    IReadOnlyList<ManagedChartDirectorySnapshot> WatchedDirectories);

internal sealed record ManagedChartIndexDocument(
    int Version,
    string LibraryPath,
    bool PreferKeysounds,
    bool PreferSscSimfiles,
    bool EnableBmsScratch,
    IReadOnlyList<ManagedChartIndexEntry> Entries);

internal static class ManagedChartLibraryIndex
{
    internal const int CurrentVersion = 1;
    internal const string FileName = "managed-library-index.json";

    private static readonly JsonSerializerOptions jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    internal static ManagedChartIndexDocument Load(
        string cachePath,
        string libraryPath,
        bool preferKeysounds,
        bool preferSscSimfiles,
        bool enableBmsScratch)
    {
        if (!File.Exists(cachePath))
            return null;

        try
        {
            ManagedChartIndexDocument document =
                JsonSerializer.Deserialize<ManagedChartIndexDocument>(
                    File.ReadAllText(cachePath),
                    jsonOptions);
            if (document?.Version != CurrentVersion
                || !pathsEqual(document.LibraryPath, libraryPath)
                || document.PreferKeysounds != preferKeysounds
                || document.PreferSscSimfiles != preferSscSimfiles
                || document.EnableBmsScratch != enableBmsScratch
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
        string libraryPath,
        bool preferKeysounds,
        bool preferSscSimfiles,
        bool enableBmsScratch,
        IReadOnlyList<ManagedChartIndexEntry> entries)
    {
        string directory = Path.GetDirectoryName(cachePath)!;
        Directory.CreateDirectory(directory);
        string temporaryPath = cachePath + ".tmp";
        var document = new ManagedChartIndexDocument(
            CurrentVersion,
            Path.GetFullPath(libraryPath),
            preferKeysounds,
            preferSscSimfiles,
            enableBmsScratch,
            entries);

        File.WriteAllText(
            temporaryPath,
            JsonSerializer.Serialize(document, jsonOptions));
        File.Move(temporaryPath, cachePath, true);
    }

    internal static bool IsCurrent(
        ManagedChartIndexEntry entry,
        string libraryPath,
        FileInfo source)
    {
        if (entry.SourceLength != source.Length
            || entry.SourceLastWriteTimeUtcTicks != source.LastWriteTimeUtc.Ticks
            || entry.Charts == null
            || entry.Dependencies == null
            || entry.WatchedDirectories == null)
        {
            return false;
        }

        foreach (ManagedChartFileSnapshot dependency in entry.Dependencies)
        {
            string path = resolveInside(libraryPath, dependency.RelativePath);
            if (path == null || !File.Exists(path))
                return false;

            var info = new FileInfo(path);
            if (info.Length != dependency.Length
                || info.LastWriteTimeUtc.Ticks
                   != dependency.LastWriteTimeUtcTicks)
            {
                return false;
            }
        }

        foreach (ManagedChartDirectorySnapshot watched in entry.WatchedDirectories)
        {
            string path = resolveInside(libraryPath, watched.RelativePath);
            if (path == null || !Directory.Exists(path)
                || Directory.GetLastWriteTimeUtc(path).Ticks
                   != watched.LastWriteTimeUtcTicks)
            {
                return false;
            }
        }

        return true;
    }

    internal static string RelativePath(string libraryPath, string path) =>
        Path.GetRelativePath(
            Path.GetFullPath(libraryPath),
            Path.GetFullPath(path));

    private static string resolveInside(string libraryPath, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)
            || Path.IsPathRooted(relativePath))
        {
            return null;
        }

        string root = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(libraryPath));
        string path = Path.GetFullPath(Path.Combine(root, relativePath));
        string relative = Path.GetRelativePath(root, path);
        return relative.Equals("..", StringComparison.Ordinal)
               || relative.StartsWith(
                   $"..{Path.DirectorySeparatorChar}",
                   StringComparison.Ordinal)
            ? null
            : path;
    }

    private static bool pathsEqual(string first, string second) =>
        !string.IsNullOrWhiteSpace(first)
        && !string.IsNullOrWhiteSpace(second)
        && Path.GetFullPath(first).Equals(
            Path.GetFullPath(second),
            StringComparison.OrdinalIgnoreCase);
}
