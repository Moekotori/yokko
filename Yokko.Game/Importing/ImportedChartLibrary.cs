using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Logging;
using osu.Framework.Platform;
using Yokko.Core.Difficulty;
using Yokko.Game.Resources;
using Yokko.Import;

namespace Yokko.Game.Importing;

internal sealed record ImportedChart(
    string Id,
    string SourcePath,
    ChartImportResult Result,
    string ArtworkPath,
    double? StarRating,
    string PackageId,
    string PackageName,
    bool IsPackage);

/// <summary>
/// Owns Yokko's persistent beatmap resource directory and notifies views which
/// present the playable chart library.
/// </summary>
internal sealed class ImportedChartLibrary
{
    private readonly List<ImportedChart> charts = [];
    private readonly object syncRoot = new();
    private readonly SemaphoreSlim importLock = new(1, 1);
    private string libraryPath;

    public event Action LibraryChanged;

    public string LibraryPath => libraryPath;

    public void Initialise(Storage storage)
    {
        ArgumentNullException.ThrowIfNull(storage);
        Initialise(storage.GetFullPath(YokkoResourceDirectories.Beatmaps, true));
    }

    public void Initialise(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        libraryPath = Path.GetFullPath(path);
        Directory.CreateDirectory(libraryPath);
    }

    public IReadOnlyList<ImportedChart> GetCharts()
    {
        lock (syncRoot)
            return charts.ToArray();
    }

    internal void Clear()
    {
        lock (syncRoot)
            charts.Clear();

        LibraryChanged?.Invoke();
    }

    public ImportedChart FindBySourceHash(string sourceHash)
    {
        if (string.IsNullOrWhiteSpace(sourceHash))
            return null;

        lock (syncRoot)
        {
            return charts.FirstOrDefault(chart =>
                string.Equals(
                    chart.Result.SourceHash,
                    sourceHash,
                    StringComparison.OrdinalIgnoreCase));
        }
    }

    public async Task<IReadOnlyList<ChartImportResult>> ImportAsync(
        ChartImportRequest request)
    {
        ensureInitialised();
        ArgumentNullException.ThrowIfNull(request);

        await importLock.WaitAsync(request.CancellationToken).ConfigureAwait(false);

        try
        {
            string sourcePath = Path.GetFullPath(request.Path);

            if (isManagedPath(sourcePath))
            {
                IReadOnlyList<ChartImportResult> managedResults =
                    await KnownChartImporters.ImportAllAsync(
                        request with { Path = sourcePath });
                AddOrReplace(managedResults, sourcePath);
                return managedResults;
            }

            IReadOnlyList<ChartImportResult> sourceResults =
                await KnownChartImporters.ImportAllAsync(
                    request with { Path = sourcePath });
            string destination = createImportDirectory(sourcePath);

            try
            {
                Directory.CreateDirectory(destination);
                string managedSourcePath = Path.Combine(
                    destination,
                    Path.GetFileName(sourcePath));
                File.Copy(sourcePath, managedSourcePath);
                copyReferencedAssets(sourcePath, destination, sourceResults);

                IReadOnlyList<ChartImportResult> managedResults =
                    await KnownChartImporters.ImportAllAsync(
                        request with { Path = managedSourcePath });
                AddOrReplace(managedResults, managedSourcePath);
                return managedResults;
            }
            catch
            {
                if (Directory.Exists(destination))
                    Directory.Delete(destination, true);

                throw;
            }
        }
        finally
        {
            importLock.Release();
        }
    }

    public async Task<int> LoadFromDiskAsync(
        bool preferKeysounds,
        bool preferSscSimfiles,
        CancellationToken cancellationToken = default)
    {
        ensureInitialised();
        await importLock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var loaded = new List<ImportedChart>();
            string[] sources = Directory
                               .EnumerateFiles(
                                   libraryPath,
                                   "*",
                                   SearchOption.AllDirectories)
                               .Where(KnownChartImporters.CanImport)
                               .Order(StringComparer.OrdinalIgnoreCase)
                               .ToArray();

            foreach (string source in sources)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    IReadOnlyList<ChartImportResult> results =
                        await KnownChartImporters.ImportAllAsync(
                            new ChartImportRequest(
                                source,
                                preferKeysounds,
                                preferSscSimfiles,
                                cancellationToken));
                    loaded.AddRange(createImportedCharts(results, source));
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    Logger.Log(
                        $"Could not load persisted beatmap '{source}': {ex.Message}",
                        LoggingTarget.Runtime,
                        LogLevel.Error);
                }
            }

            lock (syncRoot)
            {
                charts.Clear();
                charts.AddRange(loaded);
            }

            LibraryChanged?.Invoke();
            return loaded.Count;
        }
        finally
        {
            importLock.Release();
        }
    }

    public async Task<int> ChangeLibraryPathAsync(
        string path,
        bool preferKeysounds,
        bool preferSscSimfiles,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        await importLock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            libraryPath = Path.GetFullPath(path);
            Directory.CreateDirectory(libraryPath);
        }
        finally
        {
            importLock.Release();
        }

        return await LoadFromDiskAsync(
            preferKeysounds,
            preferSscSimfiles,
            cancellationToken).ConfigureAwait(false);
    }

    public void AddOrReplace(ChartImportResult result, string sourcePath)
    {
        ArgumentNullException.ThrowIfNull(result);
        AddOrReplace([result], sourcePath);
    }

    public void AddOrReplace(
        IReadOnlyList<ChartImportResult> results,
        string sourcePath)
    {
        ArgumentNullException.ThrowIfNull(results);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        if (results.Count == 0)
            throw new ArgumentException("At least one imported chart is required.", nameof(results));

        ImportedChart[] imported = createImportedCharts(results, sourcePath);

        lock (syncRoot)
        {
            charts.RemoveAll(chart =>
                chart.SourcePath.Equals(sourcePath, StringComparison.OrdinalIgnoreCase));
            charts.AddRange(imported);
        }

        LibraryChanged?.Invoke();
    }

    private ImportedChart[] createImportedCharts(
        IReadOnlyList<ChartImportResult> results,
        string sourcePath)
    {
        string packageName = Path.GetFileNameWithoutExtension(sourcePath);
        bool isPackage = results.Count > 1 || isPackageExtension(sourcePath);

        return results.Select((result, index) =>
                      {
                          double? starRating =
                              ManiaStarRatingCalculator.TryCalculate(
                                  result.Beatmap,
                                  out double calculated)
                                  ? calculated
                                  : null;

                          return new ImportedChart(
                              $"{sourcePath}\u001f{index}",
                              sourcePath,
                              result,
                              resolveArtworkPath(result, sourcePath),
                              starRating,
                              sourcePath,
                              packageName,
                              isPackage);
                      })
                      .ToArray();
    }

    private static bool isPackageExtension(string path)
    {
        string extension = Path.GetExtension(path);
        return extension.Equals(".osz", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".mcz", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".zip", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".smzip", StringComparison.OrdinalIgnoreCase);
    }

    private static string resolveArtworkPath(
        ChartImportResult result,
        string sourcePath)
    {
        if (!string.IsNullOrWhiteSpace(result.ArtworkPath)
            && File.Exists(result.ArtworkPath))
            return Path.GetFullPath(result.ArtworkPath);

        string[] directories =
        [
            directoryOfExistingFile(result.Beatmap.AudioPath),
            directoryOfExistingFile(sourcePath),
        ];

        return directories.Where(directory => directory != null)
                          .Distinct(StringComparer.OrdinalIgnoreCase)
                          .SelectMany(findArtworkCandidates)
                          .OrderByDescending(candidate => candidate.NamePriority)
                          .ThenByDescending(candidate => candidate.Length)
                          .Select(candidate => candidate.Path)
                          .FirstOrDefault();
    }

    private static string directoryOfExistingFile(string path) =>
        !string.IsNullOrWhiteSpace(path) && File.Exists(path)
            ? Path.GetDirectoryName(Path.GetFullPath(path))
            : null;

    private static IEnumerable<ArtworkCandidate> findArtworkCandidates(
        string directory)
    {
        IEnumerable<string> files;

        try
        {
            files = Directory.EnumerateFiles(directory);
        }
        catch
        {
            yield break;
        }

        foreach (string path in files)
        {
            if (!isArtworkFile(path))
                continue;

            FileInfo file;
            try
            {
                file = new FileInfo(path);
            }
            catch
            {
                continue;
            }

            if (file.Length <= 0 || file.Length > 64L * 1024 * 1024)
                continue;

            string name = Path.GetFileNameWithoutExtension(path);
            int namePriority = name.Contains("background", StringComparison.OrdinalIgnoreCase)
                               || name.Equals("bg", StringComparison.OrdinalIgnoreCase)
                ? 3
                : name.Contains("cover", StringComparison.OrdinalIgnoreCase)
                  || name.Contains("jacket", StringComparison.OrdinalIgnoreCase)
                  || name.Contains("banner", StringComparison.OrdinalIgnoreCase)
                  || name.Contains("stage", StringComparison.OrdinalIgnoreCase)
                    ? 2
                    : 1;

            yield return new ArtworkCandidate(path, file.Length, namePriority);
        }
    }

    private static bool isArtworkFile(string path)
    {
        string extension = Path.GetExtension(path);
        return extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".webp", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".bmp", StringComparison.OrdinalIgnoreCase);
    }

    private readonly record struct ArtworkCandidate(
        string Path,
        long Length,
        int NamePriority);

    private string createImportDirectory(string sourcePath)
    {
        string baseName = sanitiseName(Path.GetFileNameWithoutExtension(sourcePath));
        string suffix = Guid.NewGuid().ToString("N")[..8];
        string id = $"{baseName}-{suffix}";
        return Path.Combine(libraryPath, id);
    }

    private static void copyReferencedAssets(
        string sourcePath,
        string destination,
        IEnumerable<ChartImportResult> results)
    {
        string sourceDirectory = Path.GetDirectoryName(sourcePath)!;
        string sourceRoot = Path.GetFullPath(sourceDirectory)
                          + Path.DirectorySeparatorChar;
        IEnumerable<string> assetPaths = results.Select(
                                                     result =>
                                                         result.Beatmap.AudioPath)
                                                .Concat(results.Select(
                                                    result =>
                                                        result.ArtworkPath))
                                                .Concat(results.SelectMany(
                                                    result =>
                                                        result.Beatmap.HitObjects
                                                              .Select(note =>
                                                                  note.SampleKey)))
                                                 .Where(path =>
                                                     !string.IsNullOrWhiteSpace(
                                                         path))!
                                                 .Concat(Directory
                                                         .EnumerateFiles(sourceDirectory)
                                                         .Where(isArtworkFile));

        foreach (string assetPath in assetPaths.Distinct(
                     StringComparer.OrdinalIgnoreCase))
        {
            string candidate = Path.IsPathRooted(assetPath)
                ? Path.GetFullPath(assetPath)
                : Path.GetFullPath(Path.Combine(sourceDirectory, assetPath));

            if (!candidate.StartsWith(
                    sourceRoot,
                    StringComparison.OrdinalIgnoreCase)
                || !File.Exists(candidate))
                continue;

            string relativePath = Path.GetRelativePath(sourceDirectory, candidate);
            string target = Path.Combine(destination, relativePath);

            if (File.Exists(target))
                continue;

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(candidate, target);
        }
    }

    private bool isManagedPath(string path)
    {
        string root = Path.GetFullPath(libraryPath)
                    + Path.DirectorySeparatorChar;
        return path.StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }

    private static string sanitiseName(string value)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        string result = new(value.Select(character =>
            invalid.Contains(character) ? '_' : character).ToArray());
        result = result.Trim().TrimEnd('.');
        return string.IsNullOrWhiteSpace(result) ? "Imported beatmap" : result;
    }

    private void ensureInitialised()
    {
        if (string.IsNullOrWhiteSpace(libraryPath))
        {
            throw new InvalidOperationException(
                "The imported chart library has not been initialised.");
        }
    }
}
