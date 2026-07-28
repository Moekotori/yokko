using System.IO.Compression;

namespace Yokko.Import;

internal static class ChartArchive
{
    private const int maximumEntries = 10_000;
    private const long maximumExpandedBytes = 512L * 1024 * 1024;

    public static IReadOnlyList<string> ExtractCharts(string archivePath, string chartExtension)
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            "Yokko",
            "ChartImports",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string rootPrefix = root + Path.DirectorySeparatorChar;
        var charts = new List<string>();
        long expandedBytes = 0;

        try
        {
            using ZipArchive archive = ZipFile.OpenRead(archivePath);
            if (archive.Entries.Count > maximumEntries)
                throw new InvalidDataException($"Chart package contains more than {maximumEntries} entries.");

            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                expandedBytes += entry.Length;
                if (expandedBytes > maximumExpandedBytes)
                    throw new InvalidDataException("Chart package expands beyond the 512 MB safety limit.");

                string normalizedName = entry.FullName.Replace('/', Path.DirectorySeparatorChar);
                string destination = Path.GetFullPath(Path.Combine(root, normalizedName));
                if (!destination.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException($"Unsafe path in chart package: {entry.FullName}");

                if (string.IsNullOrEmpty(entry.Name))
                {
                    Directory.CreateDirectory(destination);
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                entry.ExtractToFile(destination, false);

                if (Path.GetExtension(destination).Equals(chartExtension, StringComparison.OrdinalIgnoreCase))
                    charts.Add(destination);
            }
        }
        catch
        {
            Directory.Delete(root, true);
            throw;
        }

        if (charts.Count == 0)
        {
            Directory.Delete(root, true);
            throw new InvalidDataException($"Chart package does not contain a {chartExtension} chart.");
        }

        charts.Sort(StringComparer.OrdinalIgnoreCase);
        return charts;
    }
}
