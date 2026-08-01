using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Logging;
using osu.Framework.Platform;
using Yokko.Core.Beatmaps;
using Yokko.Core.Difficulty;
using Yokko.Core.Timing;
using Yokko.Game.Resources;
using Yokko.Import;

namespace Yokko.Game.Importing;

internal sealed record ImportedChart(
    string Id,
    string SourcePath,
    ChartImportResult Result,
    string ArtworkPath,
    ManiaMsdResult DifficultyRating,
    ManiaStarRatingResult StarRating,
    string PackageId,
    string PackageName,
    bool IsPackage,
    ImportedChartSourceKind SourceKind = ImportedChartSourceKind.Managed,
    bool IsReadOnly = false,
    double? LengthMilliseconds = null,
    double? Bpm = null,
    string BeatmapFingerprint = null);

internal enum ImportedChartSourceKind
{
    Managed,
    ExternalOsu,
}

internal sealed record ExternalOsuLibraryResult(
    bool Success,
    string SongsPath,
    int ChartCount,
    string Message,
    int ScannedFileCount = 0,
    int ContentReadCount = 0,
    double ElapsedMilliseconds = 0);

internal sealed record ManagedChartRemovalResult(
    int RemovedChartCount,
    string SourcePath);

internal sealed record FolderChartImportResult(
    int ImportedChartCount,
    int SourceFileCount,
    int FailedFileCount);

/// <summary>
/// Owns Yokko's persistent beatmap resource directory and notifies views which
/// present the playable chart library.
/// </summary>
internal sealed class ImportedChartLibrary : IDisposable
{
    private const string pending_difficulty_reason =
        "Pending background difficulty calculation.";
    private readonly List<ImportedChart> charts = [];
    private readonly object syncRoot = new();
    private readonly SemaphoreSlim importLock = new(1, 1);
    private readonly SemaphoreSlim externalDifficultyLock = new(1, 1);
    private readonly object externalWorkPauseLock = new();
    private readonly object externalOsuStateLock = new();
    private TaskCompletionSource<bool> externalWorkResume =
        completedResumeSource();
    private int externalDifficultyGeneration;
    private readonly MsdRatingCache msdRatingCache = new();
    private readonly StarRatingCache starRatingCache = new();
    private Task<int> startupLoadTask = Task.FromResult(0);
    private Task externalDifficultyTask = Task.CompletedTask;
    private bool startupLoadStarted;
    private int externalOsuConfigurationGeneration;
    private long revision;
    private YokkoExternalOsuSettings externalOsuSettings;
    private string externalOsuCachePath;
    private FileSystemWatcher externalWatcher;
    private Timer externalWatcherDebounce;
    private bool disposed;
    private readonly Dictionary<string, ChartImportResult> externalBeatmapCache =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly LinkedList<string> externalBeatmapLru = new();
    private const int externalBeatmapCacheCapacity = 64;

    public event Action LibraryChanged;

    public string LibraryPath { get; private set; }
    internal string ExternalOsuSongsPath =>
        externalOsuSettings?.SongsPath.Value ?? string.Empty;
    internal int ExternalOsuChartCount
    {
        get
        {
            lock (syncRoot)
            {
                return charts.Count(chart =>
                    chart.SourceKind == ImportedChartSourceKind.ExternalOsu);
            }
        }
    }

    internal long Revision
    {
        get
        {
            lock (syncRoot)
                return revision;
        }
    }

    internal Task<int> StartupLoadTask
    {
        get
        {
            lock (syncRoot)
                return startupLoadTask;
        }
    }

    internal Task ExternalDifficultyTask =>
        Volatile.Read(ref externalDifficultyTask);

    internal void SetExternalIndexingPaused(bool paused)
    {
        lock (externalWorkPauseLock)
        {
            if (paused)
            {
                if (externalWorkResume.Task.IsCompleted)
                {
                    externalWorkResume = new TaskCompletionSource<bool>(
                        TaskCreationOptions.RunContinuationsAsynchronously);
                }
            }
            else
            {
                externalWorkResume.TrySetResult(true);
            }
        }
    }

    internal Task<int> BeginStartupLoad(
        bool preferKeysounds,
        bool preferSscSimfiles,
        bool enableBmsScratch = false)
    {
        lock (syncRoot)
        {
            if (startupLoadStarted)
                return startupLoadTask;

            startupLoadStarted = true;
            startupLoadTask = Task.Run(async () =>
            {
                await loadCachedExternalOsuAsync().ConfigureAwait(false);
                await LoadFromDiskAsync(
                    preferKeysounds,
                    preferSscSimfiles,
                    enableBmsScratch).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(ExternalOsuSongsPath))
                {
                    // The private index is already enough to populate song
                    // select. Validate file metadata in the background so a
                    // very large Songs folder never blocks startup.
                    _ = Task.Run(() => RefreshExternalOsuAsync())
                            .ContinueWith(task =>
                            {
                                if (task.Exception != null)
                                {
                                    Logger.Error(
                                        task.Exception.GetBaseException(),
                                        "Could not refresh the external osu! library in the background.");
                                }
                            }, TaskContinuationOptions.OnlyOnFaulted);
                }
                return GetCharts().Count;
            });
            return startupLoadTask;
        }
    }

    internal void ConfigureExternalOsu(
        Storage storage,
        YokkoExternalOsuSettings settings)
    {
        ArgumentNullException.ThrowIfNull(storage);
        externalOsuSettings = settings
                              ?? throw new ArgumentNullException(nameof(settings));
        string cacheDirectory = storage.GetFullPath(
            Path.Combine("cache", "external-osu"),
            true);
        externalOsuCachePath = Path.Combine(cacheDirectory, "library-index.json");
    }

    public void Initialise(Storage storage)
    {
        ArgumentNullException.ThrowIfNull(storage);
        Initialise(storage.GetFullPath(YokkoResourceDirectories.Beatmaps, true));
    }

    public void Initialise(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        LibraryPath = Path.GetFullPath(path);
        Directory.CreateDirectory(LibraryPath);
        initialiseDifficultyRatingCaches();
    }

    public IReadOnlyList<ImportedChart> GetCharts()
    {
        lock (syncRoot)
            return charts.ToArray();
    }

    internal (long Revision, IReadOnlyList<ImportedChart> Charts) GetSnapshot()
    {
        lock (syncRoot)
            return (revision, charts.ToArray());
    }

    internal void Clear()
    {
        lock (syncRoot)
        {
            charts.Clear();
            revision++;
        }

        LibraryChanged?.Invoke();
    }

    internal async Task<ManagedChartRemovalResult> RemoveManagedChartAsync(
        string chartId,
        CancellationToken cancellationToken = default)
    {
        ensureInitialised();
        ArgumentException.ThrowIfNullOrWhiteSpace(chartId);

        await importLock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            ImportedChart selected;
            lock (syncRoot)
                selected = charts.FirstOrDefault(chart => chart.Id == chartId);

            if (selected == null)
                return new ManagedChartRemovalResult(0, string.Empty);

            if (selected.SourceKind != ImportedChartSourceKind.Managed
                || selected.IsReadOnly)
            {
                throw new InvalidOperationException(
                    "External chart sources are read-only and cannot be removed by Yokko.");
            }

            string libraryRoot = Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(LibraryPath));
            string sourcePath = Path.GetFullPath(selected.SourcePath);
            string relativeSource = Path.GetRelativePath(libraryRoot, sourcePath);

            if (Path.IsPathRooted(relativeSource)
                || relativeSource.Equals("..", StringComparison.Ordinal)
                || relativeSource.StartsWith(
                    $"..{Path.DirectorySeparatorChar}",
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The selected chart is outside Yokko's managed beatmap directory.");
            }

            string firstSegment = relativeSource.Split(
                [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                2,
                StringSplitOptions.RemoveEmptyEntries)[0];
            bool sourceIsDirectChild = string.Equals(
                relativeSource,
                firstSegment,
                StringComparison.OrdinalIgnoreCase);
            string removalTarget = sourceIsDirectChild
                ? sourcePath
                : Path.Combine(libraryRoot, firstSegment);

            if (Directory.Exists(removalTarget))
                Directory.Delete(removalTarget, true);
            else if (File.Exists(removalTarget))
                File.Delete(removalTarget);

            int removed;
            lock (syncRoot)
            {
                removed = charts.RemoveAll(chart =>
                    chart.SourceKind == ImportedChartSourceKind.Managed
                    && (sourceIsDirectChild
                        ? chart.SourcePath.Equals(
                            sourcePath,
                            StringComparison.OrdinalIgnoreCase)
                        : isPathInside(
                            Path.GetFullPath(chart.SourcePath),
                            removalTarget)));

                if (removed > 0)
                    revision++;
            }

            if (removed > 0)
                LibraryChanged?.Invoke();

            return new ManagedChartRemovalResult(removed, sourcePath);
        }
        finally
        {
            importLock.Release();
        }
    }

    public ImportedChart FindBySourceHash(string sourceHash)
    {
        if (string.IsNullOrWhiteSpace(sourceHash))
            return null;

        ImportedChart matched;
        lock (syncRoot)
        {
            matched = charts.FirstOrDefault(chart =>
                string.Equals(
                    chart.Result.SourceHash,
                    sourceHash,
                    StringComparison.OrdinalIgnoreCase));
        }

        return materialiseExternalChart(matched);
    }

    public ImportedChart FindByBeatmapFingerprint(string fingerprint)
    {
        if (string.IsNullOrWhiteSpace(fingerprint))
            return null;

        ImportedChart matched;
        lock (syncRoot)
        {
            matched = charts.FirstOrDefault(chart =>
                string.Equals(
                    chart.BeatmapFingerprint
                    ?? YokkoBeatmapFingerprint.Compute(
                        chart.Result.Beatmap),
                    fingerprint,
                    StringComparison.OrdinalIgnoreCase));
        }

        return materialiseExternalChart(matched);
    }

    internal YokkoBeatmap GetPlayableBeatmap(string chartId)
    {
        if (string.IsNullOrWhiteSpace(chartId))
            return null;

        ImportedChart chart;
        lock (syncRoot)
            chart = charts.FirstOrDefault(candidate => candidate.Id == chartId);

        return materialiseExternalChart(chart)?.Result.Beatmap;
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
        bool enableBmsScratch = false,
        CancellationToken cancellationToken = default)
    {
        ensureInitialised();
        Stopwatch loadStopwatch = Stopwatch.StartNew();
        await importLock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var loaded = new List<ImportedChart>();
            string[] sources = Directory
                               .EnumerateFiles(
                                   LibraryPath,
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
                                enableBmsScratch,
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
                charts.RemoveAll(chart =>
                    chart.SourceKind == ImportedChartSourceKind.Managed);
                charts.AddRange(loaded);
                revision++;
            }

            msdRatingCache.SaveIfChanged();
            starRatingCache.SaveIfChanged();
            LibraryChanged?.Invoke();
            Logger.Log(
                $"Persistent beatmap scan loaded {loaded.Count} charts "
                + $"from {sources.Length} sources in "
                + $"{loadStopwatch.Elapsed.TotalMilliseconds:0} ms.",
                LoggingTarget.Runtime,
                LogLevel.Important);
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
        bool enableBmsScratch = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        await importLock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            LibraryPath = Path.GetFullPath(path);
            Directory.CreateDirectory(LibraryPath);
            initialiseDifficultyRatingCaches();
        }
        finally
        {
            importLock.Release();
        }

        return await LoadFromDiskAsync(
            preferKeysounds,
            preferSscSimfiles,
            enableBmsScratch,
            cancellationToken).ConfigureAwait(false);
    }

    internal async Task<ExternalOsuLibraryResult> SetExternalOsuSongsPathAsync(
        string selectedPath,
        CancellationToken cancellationToken = default)
    {
        ensureExternalConfigured();

        string songsPath;
        try
        {
            songsPath = ExternalOsuSongsIndex.ResolveSongsPath(selectedPath);
            if (!Directory.Exists(songsPath))
            {
                throw new DirectoryNotFoundException(
                    "The selected osu! Songs directory does not exist.");
            }
        }
        catch (Exception exception) when (exception is ArgumentException
                                          or IOException
                                          or UnauthorizedAccessException)
        {
            return new ExternalOsuLibraryResult(
                false,
                ExternalOsuSongsPath,
                ExternalOsuChartCount,
                exception.Message);
        }

        lock (externalOsuStateLock)
        {
            externalOsuConfigurationGeneration++;
            externalOsuSettings.SongsPath.Value = songsPath;
            disposeExternalWatcher();
        }
        await loadCachedExternalOsuAsync(cancellationToken)
            .ConfigureAwait(false);
        return await RefreshExternalOsuAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    internal void DisableExternalOsu()
    {
        ensureExternalConfigured();
        lock (externalOsuStateLock)
        {
            externalOsuConfigurationGeneration++;
            Interlocked.Increment(ref externalDifficultyGeneration);
            externalOsuSettings.SongsPath.Value = string.Empty;
            disposeExternalWatcher();
            replaceExternalCharts([]);
        }
    }

    public async Task<FolderChartImportResult> ImportFolderAsync(
        string path,
        bool preferKeysounds,
        bool preferSscSimfiles,
        bool enableBmsScratch = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string folderPath = Path.GetFullPath(path);
        if (!Directory.Exists(folderPath))
            throw new DirectoryNotFoundException($"Chart folder not found: {folderPath}");

        string[] sources = Directory
                           .EnumerateFiles(
                               folderPath,
                               "*",
                               new EnumerationOptions
                               {
                                   RecurseSubdirectories = true,
                                   IgnoreInaccessible = true,
                                   AttributesToSkip = FileAttributes.ReparsePoint,
                               })
                           .Where(KnownChartImporters.CanImport)
                           .Order(StringComparer.OrdinalIgnoreCase)
                           .ToArray();
        int importedCharts = 0;
        int failedFiles = 0;

        foreach (string source in sources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                IReadOnlyList<ChartImportResult> results = await ImportAsync(
                    new ChartImportRequest(
                        source,
                        preferKeysounds,
                        preferSscSimfiles,
                        enableBmsScratch,
                        cancellationToken)).ConfigureAwait(false);
                importedCharts += results.Count;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                failedFiles++;
                Logger.Error(
                    exception,
                    $"Could not import chart file '{source}' from folder '{folderPath}'.");
            }
        }

        return new FolderChartImportResult(
            importedCharts,
            sources.Length,
            failedFiles);
    }

    internal async Task<ExternalOsuLibraryResult> RefreshExternalOsuAsync(
        CancellationToken cancellationToken = default)
    {
        ensureExternalConfigured();
        string songsPath;
        int configurationGeneration;
        lock (externalOsuStateLock)
        {
            songsPath = ExternalOsuSongsPath;
            configurationGeneration = externalOsuConfigurationGeneration;
        }
        if (string.IsNullOrWhiteSpace(songsPath))
        {
            lock (externalOsuStateLock)
            {
                if (isExternalOsuConfigurationCurrent(
                        configurationGeneration,
                        songsPath))
                {
                    replaceExternalCharts([]);
                    disposeExternalWatcher();
                }
            }
            return new ExternalOsuLibraryResult(
                true,
                string.Empty,
                0,
                "External osu! library is disabled.");
        }

        if (!Directory.Exists(songsPath))
        {
            lock (externalOsuStateLock)
            {
                if (isExternalOsuConfigurationCurrent(
                        configurationGeneration,
                        songsPath))
                {
                    disposeExternalWatcher();
                }
            }
            return new ExternalOsuLibraryResult(
                false,
                songsPath,
                ExternalOsuChartCount,
                "The configured osu! Songs directory is unavailable. Cached charts were retained.");
        }

        Stopwatch stopwatch = Stopwatch.StartNew();
        await importLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        int difficultyGeneration = Interlocked.Increment(
            ref externalDifficultyGeneration);

        try
        {
            ExternalOsuIndexDocument cachedDocument =
                ExternalOsuSongsIndex.Load(externalOsuCachePath, songsPath);
            var cached = (cachedDocument?.Entries ?? [])
                         .GroupBy(entry => entry.SourcePath,
                             StringComparer.OrdinalIgnoreCase)
                         .ToDictionary(
                             group => group.Key,
                             group => group.Last(),
                             StringComparer.OrdinalIgnoreCase);
            IReadOnlyList<ExternalOsuFileSnapshot> snapshots =
                ExternalOsuSongsIndex.EnumerateFiles(
                    songsPath,
                    out bool enumerationComplete);
            var loaded = new ConcurrentBag<ExternalOsuIndexEntry>();
            var changed = new List<ExternalOsuFileSnapshot>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (ExternalOsuFileSnapshot snapshot in snapshots)
            {
                cancellationToken.ThrowIfCancellationRequested();
                seen.Add(snapshot.Path);

                if (cached.TryGetValue(
                        snapshot.Path,
                        out ExternalOsuIndexEntry existing)
                    && existing.Length == snapshot.Length
                    && existing.LastWriteTimeUtcTicks
                    == snapshot.LastWriteTimeUtcTicks)
                {
                    loaded.Add(existing);
                }
                else
                {
                    changed.Add(snapshot);
                }
            }

            await Parallel.ForEachAsync(
                changed,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = Math.Clamp(
                        Environment.ProcessorCount / 2,
                        2,
                        8),
                    CancellationToken = cancellationToken,
                },
                async (snapshot, token) =>
                {
                    try
                    {
                        await waitForExternalWorkAsync(token)
                            .ConfigureAwait(false);
                        if (!ExternalOsuSongsIndex.IsManiaFile(snapshot.Path))
                        {
                            loaded.Add(new ExternalOsuIndexEntry(
                                snapshot.Path,
                                snapshot.Length,
                                snapshot.LastWriteTimeUtcTicks,
                                null,
                                null,
                                null,
                                null));
                            return;
                        }

                        IReadOnlyList<ChartImportResult> results =
                            await KnownChartImporters.ImportAllAsync(
                                new ChartImportRequest(
                                    snapshot.Path,
                                    true,
                                    true,
                                    false,
                                    token));
                        if (results.Count == 1
                            && results[0].Beatmap.SourceFormat
                            == ChartSourceFormat.OsuMania)
                        {
                            ChartImportResult result = results[0];
                            double lengthMilliseconds =
                                chartLength(result.Beatmap);
                            double bpm = primaryBpm(result.Beatmap);
                            string fingerprint =
                                YokkoBeatmapFingerprint.Compute(result.Beatmap);
                            ChartImportResult summaryResult = result with
                            {
                                Beatmap = createExternalSummary(
                                    result.Beatmap,
                                    bpm),
                            };
                            loaded.Add(new ExternalOsuIndexEntry(
                                snapshot.Path,
                                snapshot.Length,
                                snapshot.LastWriteTimeUtcTicks,
                                summaryResult,
                                resolveArtworkPath(result, snapshot.Path),
                                pendingMsdResult(),
                                pendingStarResult(),
                                lengthMilliseconds,
                                bpm,
                                fingerprint));
                            return;
                        }

                        loaded.Add(new ExternalOsuIndexEntry(
                            snapshot.Path,
                            snapshot.Length,
                            snapshot.LastWriteTimeUtcTicks,
                            null,
                            null,
                            null,
                            null));
                    }
                    catch (Exception exception) when (exception
                                                       is not OperationCanceledException)
                    {
                        if (cached.TryGetValue(
                                snapshot.Path,
                                out ExternalOsuIndexEntry retained))
                        {
                            loaded.Add(retained);
                        }
                        Logger.Log(
                            $"Could not read external osu!mania beatmap '{snapshot.Path}': {exception.Message}",
                            LoggingTarget.Runtime,
                            LogLevel.Error);
                    }
                }).ConfigureAwait(false);

            if (!enumerationComplete)
            {
                foreach (ExternalOsuIndexEntry retained in cached.Values)
                {
                    if (!seen.Contains(retained.SourcePath))
                        loaded.Add(retained);
                }
            }

            ExternalOsuIndexEntry[] entries = loaded
                                               .OrderBy(
                                                   entry => entry.SourcePath,
                                                   StringComparer.OrdinalIgnoreCase)
                                               .ToArray();
            ImportedChart[] externalCharts = createExternalCharts(entries);
            msdRatingCache.SaveIfChanged();
            starRatingCache.SaveIfChanged();

            try
            {
                ExternalOsuSongsIndex.Save(
                    externalOsuCachePath,
                    songsPath,
                    entries);
            }
            catch (Exception exception) when (exception is IOException
                                              or UnauthorizedAccessException
                                              or NotSupportedException)
            {
                Logger.Error(
                    exception,
                    "Could not save the external osu! library index.");
            }

            lock (externalOsuStateLock)
            {
                if (!isExternalOsuConfigurationCurrent(
                        configurationGeneration,
                        songsPath))
                {
                    return supersededExternalOsuResult();
                }

                replaceExternalCharts(externalCharts);
                configureExternalWatcher(songsPath);
                startExternalDifficultyCompletion(
                    songsPath,
                    entries,
                    difficultyGeneration);
            }
            Logger.Log(
                $"External osu! scan loaded {externalCharts.Length} mania charts "
                + $"from {snapshots.Count} .osu files; {changed.Count} files "
                + $"required content reads ({stopwatch.Elapsed.TotalMilliseconds:0} ms).",
                LoggingTarget.Runtime,
                LogLevel.Important);
            return new ExternalOsuLibraryResult(
                true,
                songsPath,
                externalCharts.Length,
                enumerationComplete
                    ? "External osu!mania library is ready."
                    : "External osu!mania library loaded with inaccessible folders retained from cache.",
                snapshots.Count,
                changed.Count,
                stopwatch.Elapsed.TotalMilliseconds);
        }
        finally
        {
            importLock.Release();
        }
    }

    private async Task<int> loadCachedExternalOsuAsync(
        CancellationToken cancellationToken = default)
    {
        string songsPath;
        int configurationGeneration;
        lock (externalOsuStateLock)
        {
            songsPath = ExternalOsuSongsPath;
            configurationGeneration = externalOsuConfigurationGeneration;
        }

        if (externalOsuSettings == null
            || string.IsNullOrWhiteSpace(songsPath))
        {
            lock (externalOsuStateLock)
            {
                if (isExternalOsuConfigurationCurrent(
                        configurationGeneration,
                        songsPath))
                {
                    replaceExternalCharts([]);
                }
            }
            return 0;
        }

        await importLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ExternalOsuIndexDocument cached = ExternalOsuSongsIndex.Load(
                externalOsuCachePath,
                songsPath);
            if (cached == null)
            {
                lock (externalOsuStateLock)
                {
                    if (isExternalOsuConfigurationCurrent(
                            configurationGeneration,
                            songsPath))
                    {
                        replaceExternalCharts([]);
                    }
                }
                return 0;
            }

            ImportedChart[] restored = createExternalCharts(
                cached.Entries);
            lock (externalOsuStateLock)
            {
                if (!isExternalOsuConfigurationCurrent(
                        configurationGeneration,
                        songsPath))
                {
                    return 0;
                }

                replaceExternalCharts(restored);
            }
            Logger.Log(
                $"Restored {restored.Length} external osu!mania charts from Yokko's private index.",
                LoggingTarget.Runtime,
                LogLevel.Important);
            return restored.Length;
        }
        finally
        {
            importLock.Release();
        }
    }

    private static ImportedChart[] createExternalCharts(
        IReadOnlyList<ExternalOsuIndexEntry> entries)
    {
        var setSummaries = entries.GroupBy(
                                       entry => entry.Result == null
                                           ? null
                                           : Path.GetDirectoryName(
                                               entry.SourcePath) ?? string.Empty,
                                       StringComparer.OrdinalIgnoreCase)
                                  .Where(group => group.Key != null)
                                  .ToDictionary(
                                      group => group.Key,
                                      group => new
                                      {
                                          Count = group.Count(),
                                          Name = resolvePackageName(
                                              group.Select(entry => entry.Result)
                                                   .ToArray(),
                                              group.Key),
                                      },
                                      StringComparer.OrdinalIgnoreCase);

        return entries.Where(entry => entry.Result != null)
                      .Select(entry =>
                      {
                          string setPath = Path.GetDirectoryName(
                              entry.SourcePath) ?? entry.SourcePath;
                          var set = setSummaries[setPath];
                          return new ImportedChart(
                              $"external-osu\u001f{entry.SourcePath}",
                              entry.SourcePath,
                              entry.Result,
                              entry.ArtworkPath,
                              entry.DifficultyRating,
                              entry.StarRating,
                              $"external-osu-set\u001f{setPath}",
                              set.Name,
                              set.Count > 1,
                              ImportedChartSourceKind.ExternalOsu,
                              true,
                              entry.LengthMilliseconds,
                              entry.Bpm,
                              entry.BeatmapFingerprint);
                      })
                      .ToArray();
    }

    private void replaceExternalCharts(IReadOnlyList<ImportedChart> external)
    {
        bool changed;
        lock (syncRoot)
        {
            ImportedChart[] existing = charts
                                       .Where(chart => chart.SourceKind
                                                       == ImportedChartSourceKind.ExternalOsu)
                                       .ToArray();
            var existingById = existing.ToDictionary(
                chart => chart.Id,
                StringComparer.OrdinalIgnoreCase);
            var retainedIds = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            var merged = new ImportedChart[external.Count];

            for (int i = 0; i < external.Count; i++)
            {
                ImportedChart incoming = external[i];
                if (existingById.TryGetValue(incoming.Id, out ImportedChart current)
                    && externalChartEquivalent(current, incoming))
                {
                    // Preserve object identity for unchanged rows. This mirrors
                    // lazer's detached-store collection updates: consumers only
                    // receive actual additions, removals and replacements.
                    merged[i] = current;
                    retainedIds.Add(current.Id);
                }
                else
                {
                    merged[i] = incoming;
                }
            }

            changed = existing.Length != merged.Length
                      || existing.Where((chart, index) =>
                              index >= merged.Length
                              || !ReferenceEquals(chart, merged[index]))
                                 .Any();
            if (!changed)
                return;

            charts.RemoveAll(chart =>
                chart.SourceKind == ImportedChartSourceKind.ExternalOsu);
            charts.AddRange(merged);

            // Full beatmaps are loaded on demand. Keep LRU entries whose
            // source did not change instead of throwing away all 64 cached
            // decodes after an unrelated file-system event.
            LinkedListNode<string> node = externalBeatmapLru.First;
            while (node != null)
            {
                LinkedListNode<string> next = node.Next;
                if (!retainedIds.Contains(node.Value))
                {
                    externalBeatmapCache.Remove(node.Value);
                    externalBeatmapLru.Remove(node);
                }
                node = next;
            }
            revision++;
        }

        if (changed)
            LibraryChanged?.Invoke();
    }

    private static bool externalChartEquivalent(
        ImportedChart current,
        ImportedChart incoming) =>
        string.Equals(current.Id, incoming.Id, StringComparison.OrdinalIgnoreCase)
        && string.Equals(current.SourcePath, incoming.SourcePath,
            StringComparison.OrdinalIgnoreCase)
        && string.Equals(current.Result.SourceHash, incoming.Result.SourceHash,
            StringComparison.OrdinalIgnoreCase)
        && string.Equals(current.ArtworkPath, incoming.ArtworkPath,
            StringComparison.OrdinalIgnoreCase)
        && string.Equals(current.PackageId, incoming.PackageId,
            StringComparison.OrdinalIgnoreCase)
        && string.Equals(current.PackageName, incoming.PackageName,
            StringComparison.Ordinal)
        && current.IsPackage == incoming.IsPackage
        && current.IsReadOnly == incoming.IsReadOnly
        && current.SourceKind == incoming.SourceKind
        && Equals(current.DifficultyRating, incoming.DifficultyRating)
        && Equals(current.StarRating, incoming.StarRating);

    private async Task waitForExternalWorkAsync(
        CancellationToken cancellationToken)
    {
        Task resumeTask;
        lock (externalWorkPauseLock)
            resumeTask = externalWorkResume.Task;
        await resumeTask.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private void startExternalDifficultyCompletion(
        string songsPath,
        ExternalOsuIndexEntry[] entries,
        int generation)
    {
        if (disposed || !entries.Any(entry =>
                entry.Result != null && difficultyIsPending(entry)))
        {
            return;
        }

        Task completionTask = Task.Run(() => completeExternalDifficultiesAsync(
                songsPath,
                entries,
                generation));
        Volatile.Write(ref externalDifficultyTask, completionTask);
        _ = completionTask.ContinueWith(task =>
            {
                if (task.Exception != null)
                {
                    Logger.Error(
                        task.Exception.GetBaseException(),
                        "Could not complete external osu! difficulty ratings.");
                }
            }, TaskContinuationOptions.OnlyOnFaulted);
    }

    private async Task completeExternalDifficultiesAsync(
        string songsPath,
        ExternalOsuIndexEntry[] entries,
        int generation)
    {
        // Persist occasional rating-cache checkpoints without rewriting the
        // full external index or rebuilding every UI consumer. Publishing the
        // library every 64 charts turns 10,000 charts into roughly 157 full
        // list rebuilds.
        const int checkpointBatchSize = 2048;
        int completedSinceCheckpoint = 0;
        bool completedAny = false;

        for (int index = 0; index < entries.Length; index++)
        {
            if (disposed
                || generation != Volatile.Read(
                    ref externalDifficultyGeneration))
            {
                return;
            }

            ExternalOsuIndexEntry entry = entries[index];
            if (entry.Result == null || !difficultyIsPending(entry))
                continue;

            await waitForExternalWorkAsync(CancellationToken.None)
                .ConfigureAwait(false);
            if (!sourceSnapshotStillMatches(entry))
                continue;

            try
            {
                IReadOnlyList<ChartImportResult> results =
                    await KnownChartImporters.ImportAllAsync(
                        new ChartImportRequest(
                            entry.SourcePath,
                            true,
                            true,
                            false,
                            CancellationToken.None))
                        .ConfigureAwait(false);
                if (results.Count != 1
                    || results[0].Beatmap.SourceFormat
                    != ChartSourceFormat.OsuMania)
                {
                    continue;
                }

                // Ported from lazer's BeatmapDifficultyCache scheduling
                // policy: difficulty work is deliberately single-concurrency
                // because calculator fan-out causes visible frame stalls.
                await externalDifficultyLock.WaitAsync().ConfigureAwait(false);
                ManiaMsdResult msd;
                ManiaStarRatingResult star;
                try
                {
                    msd = msdRatingCache.GetOrCalculate(results[0].Beatmap);
                    star = starRatingCache.GetOrCalculate(results[0].Beatmap);
                }
                finally
                {
                    externalDifficultyLock.Release();
                }

                entries[index] = entry with
                {
                    DifficultyRating = msd,
                    StarRating = star,
                };
                completedAny = true;
                completedSinceCheckpoint++;
                if (completedSinceCheckpoint >= checkpointBatchSize)
                {
                    if (!await persistExternalDifficultyProgressAsync(
                            songsPath,
                            entries,
                            generation,
                            publishToLibrary: false).ConfigureAwait(false))
                    {
                        return;
                    }
                    completedSinceCheckpoint = 0;
                }
            }
            catch (Exception exception)
            {
                Logger.Log(
                    $"Could not calculate external osu! difficulty for '{entry.SourcePath}': {exception.Message}",
                    LoggingTarget.Runtime,
                    LogLevel.Error);
            }
        }

        if (completedAny)
        {
            await persistExternalDifficultyProgressAsync(
                    songsPath,
                    entries,
                    generation,
                    publishToLibrary: true)
                .ConfigureAwait(false);
        }
    }

    private async Task<bool> persistExternalDifficultyProgressAsync(
        string songsPath,
        ExternalOsuIndexEntry[] entries,
        int generation,
        bool publishToLibrary)
    {
        await importLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (disposed
                || generation != Volatile.Read(
                    ref externalDifficultyGeneration)
                || !string.Equals(
                    songsPath,
                    ExternalOsuSongsPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            msdRatingCache.SaveIfChanged();
            starRatingCache.SaveIfChanged();
            if (!publishToLibrary)
                return true;

            ExternalOsuSongsIndex.Save(
                externalOsuCachePath,
                songsPath,
                entries);

            lock (externalOsuStateLock)
            {
                if (disposed
                    || generation != Volatile.Read(
                        ref externalDifficultyGeneration)
                    || !string.Equals(
                        songsPath,
                        ExternalOsuSongsPath,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                replaceExternalCharts(createExternalCharts(entries));
            }
            return true;
        }
        catch (Exception exception) when (exception is IOException
                                          or UnauthorizedAccessException
                                          or NotSupportedException)
        {
            Logger.Error(
                exception,
                "Could not save completed external osu! difficulty ratings.");
            return false;
        }
        finally
        {
            importLock.Release();
        }
    }

    private static bool sourceSnapshotStillMatches(
        ExternalOsuIndexEntry entry)
    {
        var info = new FileInfo(entry.SourcePath);
        return info.Exists
               && info.Length == entry.Length
               && info.LastWriteTimeUtc.Ticks
               == entry.LastWriteTimeUtcTicks;
    }

    private static bool difficultyIsPending(
        ExternalOsuIndexEntry entry) =>
        entry.DifficultyRating == null
        || entry.StarRating == null
        || string.Equals(
            entry.DifficultyRating.FailureReason,
            pending_difficulty_reason,
            StringComparison.Ordinal)
        || string.Equals(
            entry.StarRating.FailureReason,
            pending_difficulty_reason,
            StringComparison.Ordinal);

    private static ManiaMsdResult pendingMsdResult() => new(
        ManiaMsdStatus.AlgorithmFailure,
        null,
        1,
        "Pending",
        pending_difficulty_reason);

    private static ManiaStarRatingResult pendingStarResult() => new(
        ManiaStarRatingStatus.AlgorithmFailure,
        null,
        1,
        "Pending",
        pending_difficulty_reason);

    private static TaskCompletionSource<bool> completedResumeSource()
    {
        var source = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        source.SetResult(true);
        return source;
    }

    private bool isExternalOsuConfigurationCurrent(
        int generation,
        string songsPath) =>
        generation == externalOsuConfigurationGeneration
        && string.Equals(
            songsPath,
            ExternalOsuSongsPath,
            StringComparison.OrdinalIgnoreCase);

    private ExternalOsuLibraryResult supersededExternalOsuResult() => new(
        true,
        ExternalOsuSongsPath,
        ExternalOsuChartCount,
        "External osu! scan was superseded by a configuration change.");

    private void configureExternalWatcher(string songsPath)
    {
        disposeExternalWatcher();
        if (disposed || !Directory.Exists(songsPath))
            return;

        externalWatcherDebounce = new Timer(
            _ => _ = RefreshExternalOsuAsync(),
            null,
            Timeout.Infinite,
            Timeout.Infinite);
        externalWatcher = new FileSystemWatcher(songsPath, "*.osu")
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName
                           | NotifyFilters.DirectoryName
                           | NotifyFilters.LastWrite
                           | NotifyFilters.Size,
            EnableRaisingEvents = true,
        };
        externalWatcher.Created += onExternalOsuChanged;
        externalWatcher.Changed += onExternalOsuChanged;
        externalWatcher.Deleted += onExternalOsuChanged;
        externalWatcher.Renamed += onExternalOsuRenamed;
        externalWatcher.Error += onExternalOsuWatcherError;
    }

    private void onExternalOsuChanged(object sender, FileSystemEventArgs e) =>
        externalWatcherDebounce?.Change(750, Timeout.Infinite);

    private void onExternalOsuRenamed(object sender, RenamedEventArgs e) =>
        externalWatcherDebounce?.Change(750, Timeout.Infinite);

    private void onExternalOsuWatcherError(object sender, ErrorEventArgs e) =>
        externalWatcherDebounce?.Change(750, Timeout.Infinite);

    private void disposeExternalWatcher()
    {
        if (externalWatcher != null)
        {
            externalWatcher.EnableRaisingEvents = false;
            externalWatcher.Created -= onExternalOsuChanged;
            externalWatcher.Changed -= onExternalOsuChanged;
            externalWatcher.Deleted -= onExternalOsuChanged;
            externalWatcher.Renamed -= onExternalOsuRenamed;
            externalWatcher.Error -= onExternalOsuWatcherError;
            externalWatcher.Dispose();
            externalWatcher = null;
        }

        externalWatcherDebounce?.Dispose();
        externalWatcherDebounce = null;
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
        msdRatingCache.SaveIfChanged();
        starRatingCache.SaveIfChanged();

        lock (syncRoot)
        {
            charts.RemoveAll(chart =>
                chart.SourceKind == ImportedChartSourceKind.Managed
                && chart.SourcePath.Equals(
                    sourcePath,
                    StringComparison.OrdinalIgnoreCase));
            charts.AddRange(imported);
            revision++;
        }

        LibraryChanged?.Invoke();
    }

    private ImportedChart[] createImportedCharts(
        IReadOnlyList<ChartImportResult> results,
        string sourcePath)
    {
        string packageName = resolvePackageName(results, sourcePath);
        // 只有真正包含多张谱面的合集才算图包；单曲 .osz/.zip 只是一首歌，
        // 不能因为扩展名就给它套上图包外壳。
        bool isPackage = results.Count > 1;

        return results.Select((result, index) =>
                      {
                          ManiaMsdResult msdRating =
                              msdRatingCache.GetOrCalculate(
                                  result.Beatmap);
                          ManiaStarRatingResult starRating =
                              starRatingCache.GetOrCalculate(
                                  result.Beatmap);

                          if (!msdRating.IsSuccess
                              && msdRating.Status
                                  != ManiaMsdStatus.TooFewRows)
                          {
                              Logger.Log(
                                  $"Could not calculate Etterna MSD for "
                                  + $"'{result.Beatmap.Title} "
                                  + $"[{result.Beatmap.DifficultyName}]': "
                                  + $"{msdRating.Status} — "
                                  + $"{msdRating.FailureReason}",
                                  LoggingTarget.Runtime,
                                  LogLevel.Error);
                          }

                          if (!starRating.IsSuccess
                              && starRating.Status
                                  != ManiaStarRatingStatus.TooFewNotes)
                          {
                              Logger.Log(
                                  $"Could not calculate Rebirth SR for "
                                  + $"'{result.Beatmap.Title} "
                                  + $"[{result.Beatmap.DifficultyName}]': "
                                  + $"{starRating.Status} — "
                                  + $"{starRating.FailureReason}",
                                  LoggingTarget.Runtime,
                                  LogLevel.Error);
                          }

                          return new ImportedChart(
                              $"{sourcePath}\u001f{index}",
                              sourcePath,
                              result,
                              resolveArtworkPath(result, sourcePath),
                              msdRating,
                              starRating,
                              sourcePath,
                              packageName,
                              isPackage,
                              LengthMilliseconds: chartLength(result.Beatmap),
                              Bpm: primaryBpm(result.Beatmap),
                              BeatmapFingerprint:
                                  YokkoBeatmapFingerprint.Compute(
                                      result.Beatmap));
                      })
                      .ToArray();
    }

    private ImportedChart materialiseExternalChart(ImportedChart chart)
    {
        if (chart == null
            || chart.SourceKind != ImportedChartSourceKind.ExternalOsu)
        {
            return chart;
        }

        ChartImportResult result;
        lock (syncRoot)
        {
            if (externalBeatmapCache.TryGetValue(chart.Id, out result))
            {
                externalBeatmapLru.Remove(chart.Id);
                externalBeatmapLru.AddFirst(chart.Id);
                return chart with { Result = result };
            }
        }

        if (!File.Exists(chart.SourcePath))
        {
            throw new FileNotFoundException(
                "The external osu!mania source file is unavailable.",
                chart.SourcePath);
        }
        if (!ExternalOsuSongsIndex.IsManiaFile(chart.SourcePath))
        {
            throw new InvalidDataException(
                "The external beatmap is no longer an osu!mania chart.");
        }

        IReadOnlyList<ChartImportResult> results =
            KnownChartImporters.ImportAllAsync(
                    new ChartImportRequest(
                        chart.SourcePath,
                        true,
                        true))
                .AsTask()
                .GetAwaiter()
                .GetResult();
        result = results.Count == 1
            ? results[0]
            : throw new InvalidDataException(
                "The external .osu file did not produce exactly one chart.");

        lock (syncRoot)
        {
            externalBeatmapCache[chart.Id] = result;
            externalBeatmapLru.Remove(chart.Id);
            externalBeatmapLru.AddFirst(chart.Id);
            while (externalBeatmapLru.Count > externalBeatmapCacheCapacity)
            {
                string evicted = externalBeatmapLru.Last!.Value;
                externalBeatmapLru.RemoveLast();
                externalBeatmapCache.Remove(evicted);
            }
        }

        return chart with { Result = result };
    }

    private static YokkoBeatmap createExternalSummary(
        YokkoBeatmap beatmap,
        double bpm)
    {
        YokkoTimingPoint summaryTiming = bpm > 0
            ? new YokkoTimingPoint(0, 60000 / bpm)
            : YokkoTimingPoint.Default;
        return beatmap with
        {
            TimingPoints = [summaryTiming],
            HitObjects = [],
            ScrollVelocities = [],
            ScrollSpeedFactors = [],
            ScrollProfiles = new Dictionary<string, YokkoScrollProfile>(),
            BreakPeriods = [],
            ScheduledSamples = [],
        };
    }

    private static double chartLength(YokkoBeatmap beatmap) =>
        beatmap.HitObjects.Count == 0
            ? 0
            : beatmap.HitObjects.Max(hitObject =>
                hitObject.EndTimeMilliseconds
                ?? hitObject.StartTimeMilliseconds);

    private static double primaryBpm(YokkoBeatmap beatmap) =>
        beatmap.TimingPoints
               .Where(point => point.Uninherited
                               && point.BeatsPerMinute > 0)
               .Select(point => point.BeatsPerMinute)
               .FirstOrDefault();

    private static string resolvePackageName(
        IReadOnlyList<ChartImportResult> results,
        string sourcePath)
    {
        string sourceName = Path.GetFileNameWithoutExtension(sourcePath);
        bool isOsuSet = results.All(result =>
            result.Beatmap.SourceFormat is ChartSourceFormat.OsuMania
                or ChartSourceFormat.OsuStandard);
        if (!isOsuSet
            && !sourceName.StartsWith(
                "beatmapset_",
                StringComparison.OrdinalIgnoreCase))
        {
            return sourceName;
        }

        string title = mostFrequentValue(results.Select(result =>
            result.Beatmap.Title));
        string artist = mostFrequentValue(results.Select(result =>
            result.Beatmap.Artist));

        if (string.IsNullOrWhiteSpace(title))
            return sourceName;

        return string.IsNullOrWhiteSpace(artist)
               || artist.Equals("Unknown Artist", StringComparison.OrdinalIgnoreCase)
            ? title
            : $"{artist} - {title}";
    }

    private static string mostFrequentValue(IEnumerable<string> values) =>
        values.Where(value => !string.IsNullOrWhiteSpace(value))
              .GroupBy(value => value.Trim(), StringComparer.OrdinalIgnoreCase)
              .OrderByDescending(group => group.Count())
              .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
              .Select(group => group.Key)
              .FirstOrDefault() ?? string.Empty;

    private void initialiseDifficultyRatingCaches()
    {
        string cacheDirectory = Path.Combine(
            LibraryPath,
            ".yokko-cache");
        msdRatingCache.Initialise(Path.Combine(
            cacheDirectory,
            "etterna-msd.json"));
        starRatingCache.Initialise(Path.Combine(
            cacheDirectory,
            "star-ratings.json"));
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

    private static bool isLegacyHitSampleFile(string path)
    {
        string extension = Path.GetExtension(path);
        if (!extension.Equals(".wav", StringComparison.OrdinalIgnoreCase)
            && !extension.Equals(".ogg", StringComparison.OrdinalIgnoreCase)
            && !extension.Equals(".mp3", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string stem = Path.GetFileNameWithoutExtension(path)
                          .ToLowerInvariant();
        return (stem.StartsWith("normal-", StringComparison.Ordinal)
                || stem.StartsWith("soft-", StringComparison.Ordinal)
                || stem.StartsWith("drum-", StringComparison.Ordinal))
               && (stem.Contains("-hit", StringComparison.Ordinal)
                   || stem.Contains("-slider", StringComparison.Ordinal));
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
        return Path.Combine(LibraryPath, id);
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
                                                 .Concat(results.SelectMany(
                                                     result =>
                                                         result.Beatmap.ScheduledSamples
                                                               .Select(sample =>
                                                                   sample.Path)))
                                                 .Concat(results.SelectMany(
                                                    result =>
                                                        result.Beatmap.HitObjects
                                                              .SelectMany(note =>
                                                                  note.Samples
                                                                      .Concat(
                                                                          note.NodeSamples
                                                                              .SelectMany(
                                                                                  static node =>
                                                                                      node))
                                                                      .Select(
                                                                          static sample =>
                                                                              sample.Filename))))
                                                 .Where(path =>
                                                     !string.IsNullOrWhiteSpace(
                                                         path))!
                                                 .Concat(Directory
                                                         .EnumerateFiles(sourceDirectory)
                                                         .Where(path =>
                                                             isArtworkFile(path)
                                                             || isLegacyHitSampleFile(path)));

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
        string root = Path.GetFullPath(LibraryPath)
                    + Path.DirectorySeparatorChar;
        return path.StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }

    private static bool isPathInside(string path, string directory)
    {
        string root = Path.TrimEndingDirectorySeparator(
                          Path.GetFullPath(directory))
                      + Path.DirectorySeparatorChar;
        return Path.GetFullPath(path).StartsWith(
            root,
            StringComparison.OrdinalIgnoreCase);
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
        if (string.IsNullOrWhiteSpace(LibraryPath))
        {
            throw new InvalidOperationException(
                "The imported chart library has not been initialised.");
        }
    }

    private void ensureExternalConfigured()
    {
        if (externalOsuSettings == null
            || string.IsNullOrWhiteSpace(externalOsuCachePath))
        {
            throw new InvalidOperationException(
                "The external osu! library has not been configured.");
        }
    }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        Interlocked.Increment(ref externalDifficultyGeneration);
        SetExternalIndexingPaused(false);
        disposeExternalWatcher();
    }
}
