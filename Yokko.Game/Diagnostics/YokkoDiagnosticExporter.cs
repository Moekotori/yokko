using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using osu.Framework.Logging;

namespace Yokko.Game.Diagnostics;

internal static class YokkoDiagnosticExporter
{
    private static readonly UTF8Encoding utf8_without_bom = new(false);

    public static string Export(
        string exportDirectory,
        string logDirectory,
        string sessionLogPrefix,
        string liveLog,
        YokkoPerformanceSnapshot? performance)
    {
        if (string.IsNullOrWhiteSpace(exportDirectory))
            throw new InvalidOperationException("Diagnostics have not been initialised.");

        Directory.CreateDirectory(exportDirectory);
        Logger.Flush();

        string timestamp = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss-fff");
        string archivePath = Path.Combine(
            exportDirectory,
            $"Yokko-diagnostics-{timestamp}-{Guid.NewGuid():N}.zip");
        string temporaryPath = archivePath + ".tmp";
        var copiedLogs = new List<string>();
        var skippedLogs = new List<string>();

        try
        {
            using (var stream = new FileStream(
                       temporaryPath,
                       FileMode.CreateNew,
                       FileAccess.ReadWrite,
                       FileShare.None))
            using (var archive = new ZipArchive(
                       stream,
                       ZipArchiveMode.Create,
                       leaveOpen: false,
                       entryNameEncoding: utf8_without_bom))
            {
                writeTextEntry(
                    archive,
                    "diagnostics-live.log",
                    liveLog ?? string.Empty);

                foreach (string logPath in enumerateSessionLogs(
                             logDirectory,
                             sessionLogPrefix))
                {
                    try
                    {
                        string name = Path.GetFileName(logPath);
                        archive.CreateEntryFromFile(
                            logPath,
                            $"logs/{name}",
                            CompressionLevel.Optimal);
                        copiedLogs.Add(name);
                    }
                    catch (Exception exception)
                    {
                        skippedLogs.Add(
                            $"{Path.GetFileName(logPath)}: {exception.Message}");
                    }
                }

                writeTextEntry(
                    archive,
                    "manifest.txt",
                    createManifest(
                        copiedLogs,
                        skippedLogs,
                        liveLog,
                        performance));
            }

            File.Move(temporaryPath, archivePath);
            return archivePath;
        }
        catch
        {
            try
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
            catch
            {
            }

            throw;
        }
    }

    private static IEnumerable<string> enumerateSessionLogs(
        string logDirectory,
        string sessionLogPrefix)
    {
        if (string.IsNullOrWhiteSpace(logDirectory)
            || string.IsNullOrWhiteSpace(sessionLogPrefix)
            || !Directory.Exists(logDirectory))
            return [];

        return Directory
               .EnumerateFiles(
                   logDirectory,
                   $"{sessionLogPrefix}.*.log",
                   SearchOption.TopDirectoryOnly)
               .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
               .ToArray();
    }

    private static void writeTextEntry(
        ZipArchive archive,
        string name,
        string content)
    {
        ZipArchiveEntry entry = archive.CreateEntry(
            name,
            CompressionLevel.Optimal);
        using Stream entryStream = entry.Open();
        using var writer = new StreamWriter(
            entryStream,
            utf8_without_bom,
            bufferSize: 4096,
            leaveOpen: false);
        writer.Write(content);
    }

    private static string createManifest(
        IReadOnlyList<string> copiedLogs,
        IReadOnlyList<string> skippedLogs,
        string liveLog,
        YokkoPerformanceSnapshot? performance)
    {
        string version = Assembly.GetEntryAssembly()?
                                  .GetName().Version?.ToString()
                         ?? "unknown";
        var lines = new List<string>
        {
            "Yokko diagnostic export",
            $"ExportedUtc: {DateTimeOffset.UtcNow:O}",
            $"Version: {version}",
            $"Runtime: {RuntimeInformation.FrameworkDescription}",
            $"OS: {RuntimeInformation.OSDescription}",
            $"ProcessArchitecture: {RuntimeInformation.ProcessArchitecture}",
            $"ProcessorCount: {Environment.ProcessorCount}",
            $"LoggerLevel: {Logger.Level}",
            $"LiveEntriesTextLength: {liveLog?.Length ?? 0}",
            $"SessionLogFiles: {copiedLogs.Count}",
        };

        if (performance is { } snapshot)
            lines.Add($"LatestPerformance: {snapshot.ToLogDetails()}");

        foreach (string copiedLog in copiedLogs)
            lines.Add($"IncludedLog: {copiedLog}");

        foreach (string skippedLog in skippedLogs)
            lines.Add($"SkippedLog: {skippedLog}");

        return string.Join(Environment.NewLine, lines);
    }
}
