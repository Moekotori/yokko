using System.IO.Compression;

namespace Yokko.Import;

internal sealed record ChartArchiveExtraction(
    string RootPath,
    IReadOnlyList<string> ChartPaths);

public static class ChartArchive
{
    private const int maximumEntries = 10_000;
    private const long maximumExpandedBytes = 512L * 1024 * 1024;

    private static string extractionsRoot => Path.Combine(
        Path.GetTempPath(),
        "Yokko",
        "ChartImports");

    internal static ChartArchiveExtraction ExtractCharts(
        string archivePath,
        params string[] chartExtensions)
    {
        if (chartExtensions.Length == 0)
            throw new ArgumentException("At least one chart extension is required.", nameof(chartExtensions));

        var supportedExtensions = chartExtensions.ToHashSet(StringComparer.OrdinalIgnoreCase);
        string root = Path.Combine(
            extractionsRoot,
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

                if (supportedExtensions.Contains(Path.GetExtension(destination)))
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
            throw new InvalidDataException(
                $"Chart package does not contain a supported chart ({string.Join(", ", chartExtensions)}).");
        }

        charts.Sort(StringComparer.OrdinalIgnoreCase);
        return new ChartArchiveExtraction(root, charts);
    }

    /// <summary>
    /// Best-effort 删除一个由 <see cref="ExtractCharts"/> 产生的解压目录。
    /// 只接受直接位于本模块解压根下的路径；文件被占用等 IO 失败会被忽略，
    /// 残留目录由 <see cref="CleanUpStaleExtractions"/> 在后续启动时回收。
    /// </summary>
    public static void TryDeleteExtraction(string? rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
            return;

        try
        {
            string fullPath = Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(rootPath));
            string expectedParent = Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(extractionsRoot));
            if (!string.Equals(
                    Path.GetDirectoryName(fullPath),
                    expectedParent,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (Directory.Exists(fullPath))
                Directory.Delete(fullPath, true);
        }
        catch (Exception exception) when (exception is IOException
                                          or UnauthorizedAccessException)
        {
        }
    }

    /// <summary>
    /// 删除解压根下最后写入时间早于 <paramref name="minimumAge"/> 的陈旧
    /// 解压目录。年龄阈值避免误删其他正在运行实例刚创建的目录。
    /// </summary>
    public static void CleanUpStaleExtractions(TimeSpan minimumAge)
    {
        string root = extractionsRoot;
        string[] directories;

        try
        {
            directories = Directory.Exists(root)
                ? Directory.GetDirectories(root)
                : [];
        }
        catch (Exception exception) when (exception is IOException
                                          or UnauthorizedAccessException)
        {
            return;
        }

        DateTime cutoffUtc = DateTime.UtcNow - minimumAge;

        foreach (string directory in directories)
        {
            try
            {
                if (Directory.GetLastWriteTimeUtc(directory) < cutoffUtc)
                    Directory.Delete(directory, true);
            }
            catch (Exception exception) when (exception is IOException
                                              or UnauthorizedAccessException)
            {
            }
        }
    }
}
