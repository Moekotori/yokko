using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
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
    string BeatmapFingerprint = null,
    bool RequiresMaterialisation = false);

internal enum ImportedChartSourceKind
{
    Managed,
    ExternalOsu,
}

[Flags]
internal enum ImportedChartLibraryChangeKind
{
    None = 0,
    Structure = 1 << 0,
    DifficultyRatings = 1 << 1,
}

internal sealed record ImportedChartLibraryDelta(
    IReadOnlyList<ImportedChart> UpsertedCharts,
    IReadOnlyList<string> RemovedChartIds)
{
    internal ImportedChartLibraryDelta Merge(
        ImportedChartLibraryDelta newer)
    {
        ArgumentNullException.ThrowIfNull(newer);
        var upserted = new Dictionary<string, ImportedChart>(
            StringComparer.OrdinalIgnoreCase);
        var removed = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        apply(this);
        apply(newer);
        return new ImportedChartLibraryDelta(
            upserted.Values.ToArray(),
            removed.ToArray());

        void apply(ImportedChartLibraryDelta delta)
        {
            foreach (string id in delta.RemovedChartIds)
            {
                upserted.Remove(id);
                removed.Add(id);
            }

            foreach (ImportedChart chart in delta.UpsertedCharts)
            {
                removed.Remove(chart.Id);
                upserted[chart.Id] = chart;
            }
        }
    }
}

internal sealed record ImportedChartLibraryChange(
    long Revision,
    long StructureRevision,
    ImportedChartLibraryChangeKind Kind,
    int ChartCount,
    ImportedChartLibraryDelta Delta = null)
{
    internal ImportedChartLibraryChange Merge(
        ImportedChartLibraryChange newer)
    {
        ArgumentNullException.ThrowIfNull(newer);
        ImportedChartLibraryChange latest = newer.Revision >= Revision
            ? newer
            : this;
        return new ImportedChartLibraryChange(
            Math.Max(Revision, newer.Revision),
            Math.Max(StructureRevision, newer.StructureRevision),
            Kind | newer.Kind,
            latest.ChartCount,
            Delta == null || newer.Delta == null
                ? null
                : Delta.Merge(newer.Delta));
    }
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
    private sealed record ChartImportOperation(
        IReadOnlyList<ChartImportResult> Results,
        ImportedChartLibraryChange Change);

    private const string pending_difficulty_reason =
        "Pending background difficulty calculation.";
    private const int difficulty_progress_publish_interval_milliseconds = 750;
    private readonly List<ImportedChart> charts = [];
    private ImportedChart[] indexedExternalCharts = [];
    private readonly object syncRoot = new();
    private readonly SemaphoreSlim importLock = new(1, 1);
    private readonly SemaphoreSlim externalDifficultyLock = new(1, 1);
    private readonly object externalWorkPauseLock = new();
    private readonly object externalOsuStateLock = new();
    private CancellationTokenSource externalOsuRefreshCancellation;
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
    private long structureRevision;
    private YokkoExternalOsuSettings externalOsuSettings;
    private string externalOsuCachePath;
    private string managedIndexPath;
    private bool managedPreferKeysounds = true;
    private bool managedPreferSscSimfiles = true;
    private bool managedEnableBmsScratch;
    private FileSystemWatcher externalWatcher;
    private Timer externalWatcherDebounce;
    private Timer externalAvailabilityTimer;
    private string observedExternalOsuPath;
    private bool? observedExternalOsuAvailable;
    private bool disposed;
    private readonly Dictionary<string, ChartImportResult> externalBeatmapCache =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly LinkedList<string> externalBeatmapLru = new();
    private const int externalBeatmapCacheCapacity = 64;

    internal int LastManagedScannedFileCount { get; private set; }
    internal int LastManagedContentReadCount { get; private set; }
    internal int LastManagedCacheHitCount { get; private set; }

    public event Action<ImportedChartLibraryChange> LibraryChanged;

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

    internal long StructureRevision
    {
        get
        {
            lock (syncRoot)
                return structureRevision;
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
        configureExternalAvailabilityMonitor(ExternalOsuSongsPath);
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
        initialiseManagedIndex();
    }

    public IReadOnlyList<ImportedChart> GetCharts()
    {
        lock (syncRoot)
            return charts.ToArray();
    }

    internal (
        long Revision,
        long StructureRevision,
        IReadOnlyList<ImportedChart> Charts) GetSnapshot()
    {
        lock (syncRoot)
            return (revision, structureRevision, charts.ToArray());
    }

    internal int PruneUnavailableExternalCharts()
    {
        ImportedChartLibraryChange change = null;
        int removed;
        lock (syncRoot)
        {
            string[] unavailableIds = indexedExternalCharts
                                     .Where(chart => !File.Exists(chart.SourcePath))
                                     .Select(chart => chart.Id)
                                     .Distinct(StringComparer.OrdinalIgnoreCase)
                                     .ToArray();
            removed = unavailableIds.Length;
            if (removed == 0)
                return 0;

            var unavailable = new HashSet<string>(
                unavailableIds,
                StringComparer.OrdinalIgnoreCase);
            indexedExternalCharts = indexedExternalCharts
                                   .Where(chart => !unavailable.Contains(chart.Id))
                                   .ToArray();
            refreshVisibleExternalChartsAfterManagedChange();
            evictMaterialisedCharts(unavailable.Contains);
            change = advanceRevision(ImportedChartLibraryChangeKind.Structure);
        }

        LibraryChanged?.Invoke(change);
        return removed;
    }

    internal void Clear()
    {
        ImportedChartLibraryChange change;
        lock (syncRoot)
        {
            charts.Clear();
            indexedExternalCharts = [];
            change = advanceRevision(ImportedChartLibraryChangeKind.Structure);
        }

        LibraryChanged?.Invoke(change);
    }

    private int clearManagedChartsForMissingLibrary(
        bool preferKeysounds,
        bool preferSscSimfiles,
        bool enableBmsScratch)
    {
        ImportedChartLibraryChange change = null;
        int removed;
        lock (syncRoot)
        {
            string[] managedIds = charts
                                  .Where(chart => chart.SourceKind
                                                  == ImportedChartSourceKind.Managed)
                                  .Select(chart => chart.Id)
                                  .ToArray();
            removed = managedIds.Length;
            if (removed > 0)
            {
                var unavailable = new HashSet<string>(
                    managedIds,
                    StringComparer.OrdinalIgnoreCase);
                charts.RemoveAll(chart => unavailable.Contains(chart.Id));
                refreshVisibleExternalChartsAfterManagedChange();
                evictMaterialisedCharts(unavailable.Contains);
                change = advanceRevision(
                    ImportedChartLibraryChangeKind.Structure);
            }
        }

        managedPreferKeysounds = preferKeysounds;
        managedPreferSscSimfiles = preferSscSimfiles;
        managedEnableBmsScratch = enableBmsScratch;
        LastManagedScannedFileCount = 0;
        LastManagedContentReadCount = 0;
        LastManagedCacheHitCount = 0;

        if (change != null)
            LibraryChanged?.Invoke(change);

        Logger.Log(
            $"Managed beatmap directory '{LibraryPath}' is unavailable; "
            + $"removed {removed} stale charts from the active library.",
            LoggingTarget.Runtime,
            LogLevel.Important);
        return 0;
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
            ImportedChartLibraryChange change = null;
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
                {
                    refreshVisibleExternalChartsAfterManagedChange();
                    change = advanceRevision(
                        ImportedChartLibraryChangeKind.Structure);
                }
            }

            if (removed > 0)
            {
                saveManagedIndexFromCurrentCharts();
                LibraryChanged?.Invoke(change);
            }

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

        return materialiseChart(matched);
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

        return materialiseChart(matched);
    }

    internal YokkoBeatmap GetPlayableBeatmap(string chartId)
    {
        if (string.IsNullOrWhiteSpace(chartId))
            return null;

        ImportedChart chart;
        lock (syncRoot)
            chart = charts.FirstOrDefault(candidate => candidate.Id == chartId);

        return materialiseChart(chart)?.Result.Beatmap;
    }

    internal async Task<YokkoBeatmap> GetPlayableBeatmapAsync(
        string chartId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(chartId))
            return null;

        ImportedChart chart;
        lock (syncRoot)
            chart = charts.FirstOrDefault(candidate => candidate.Id == chartId);

        ImportedChart materialised = await materialiseChartAsync(
            chart,
            cancellationToken).ConfigureAwait(false);
        return materialised?.Result.Beatmap;
    }

    internal async Task<ManiaDifficultyRatings> GetBaseDifficultyRatingsAsync(
        string chartId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(chartId))
            return null;

        ImportedChart chart;
        lock (syncRoot)
            chart = charts.FirstOrDefault(candidate => candidate.Id == chartId);
        if (chart == null)
            return null;
        if (chart.DifficultyRating?.IsSuccess == true
            && chart.StarRating?.IsSuccess == true)
        {
            return new ManiaDifficultyRatings(
                chart.DifficultyRating,
                chart.StarRating);
        }

        ImportedChart materialised = await materialiseChartAsync(
                chart,
                cancellationToken)
            .ConfigureAwait(false);
        if (materialised == null)
            return null;

        await externalDifficultyLock.WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        ManiaDifficultyRatings ratings;
        try
        {
            ratings = new ManiaDifficultyRatings(
                msdRatingCache.GetOrCalculate(materialised.Result.Beatmap),
                starRatingCache.GetOrCalculate(materialised.Result.Beatmap));
        }
        finally
        {
            externalDifficultyLock.Release();
        }

        publishRequestedDifficultyRatings(materialised, ratings);
        return ratings;
    }

    private void publishRequestedDifficultyRatings(
        ImportedChart materialised,
        ManiaDifficultyRatings ratings)
    {
        ImportedChartLibraryChange change = null;
        lock (syncRoot)
        {
            int index = charts.FindIndex(candidate => candidate.Id.Equals(
                materialised.Id,
                StringComparison.OrdinalIgnoreCase));
            if (index < 0)
                return;

            ImportedChart current = charts[index];
            if (!externalChartEquivalentIgnoringRatings(current, materialised)
                || Equals(current.DifficultyRating, ratings.EtternaMsd)
                   && Equals(current.StarRating, ratings.RebirthStars))
            {
                return;
            }

            ImportedChart replacement = current with
            {
                DifficultyRating = ratings.EtternaMsd,
                StarRating = ratings.RebirthStars,
            };
            charts[index] = replacement;
            updateIndexedExternalChart(replacement);
            change = advanceRevision(
                ImportedChartLibraryChangeKind.DifficultyRatings);
        }

        LibraryChanged?.Invoke(change);
    }

    public async Task<IReadOnlyList<ChartImportResult>> ImportAsync(
        ChartImportRequest request)
    {
        ensureInitialised();
        ArgumentNullException.ThrowIfNull(request);

        await importLock.WaitAsync(request.CancellationToken).ConfigureAwait(false);

        try
        {
            ChartImportOperation operation = await importAsyncLocked(request)
                .ConfigureAwait(false);
            managedPreferKeysounds = request.PreferKeysounds;
            managedPreferSscSimfiles = request.PreferSscSimfiles;
            managedEnableBmsScratch = request.EnableBmsScratch;
            saveManagedIndexFromCurrentCharts();
            LibraryChanged?.Invoke(operation.Change);
            return operation.Results;
        }
        finally
        {
            importLock.Release();
        }
    }

    private async Task<ChartImportOperation> importAsyncLocked(
        ChartImportRequest request)
    {
        string sourcePath = Path.GetFullPath(request.Path);

        if (isManagedPath(sourcePath))
        {
            IReadOnlyList<ChartImportResult> managedResults =
                await KnownChartImporters.ImportAllAsync(
                    request with { Path = sourcePath }).ConfigureAwait(false);
            return new ChartImportOperation(
                managedResults,
                addOrReplaceCore(managedResults, sourcePath));
        }

        IReadOnlyList<ChartImportResult> sourceResults =
            await KnownChartImporters.ImportAllAsync(
                request with { Path = sourcePath }).ConfigureAwait(false);
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
                    request with { Path = managedSourcePath })
                    .ConfigureAwait(false);
            return new ChartImportOperation(
                managedResults,
                addOrReplaceCore(managedResults, managedSourcePath));
        }
        catch
        {
            if (Directory.Exists(destination))
                Directory.Delete(destination, true);

            throw;
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
            if (!Directory.Exists(LibraryPath))
            {
                return clearManagedChartsForMissingLibrary(
                    preferKeysounds,
                    preferSscSimfiles,
                    enableBmsScratch);
            }

            var loaded = new List<ImportedChart>();
            var updatedEntries = new List<ManagedChartIndexEntry>();
            ManagedChartIndexDocument index = ManagedChartLibraryIndex.Load(
                managedIndexPath,
                LibraryPath,
                preferKeysounds,
                preferSscSimfiles,
                enableBmsScratch);
            Dictionary<string, ManagedChartIndexEntry> cached = index?.Entries
                .Where(entry => !string.IsNullOrWhiteSpace(entry.SourceRelativePath))
                .GroupBy(
                    entry => entry.SourceRelativePath,
                    StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group.First(),
                    StringComparer.OrdinalIgnoreCase)
                ?? new Dictionary<string, ManagedChartIndexEntry>(
                    StringComparer.OrdinalIgnoreCase);
            string[] sources;
            try
            {
                sources = Directory
                          .EnumerateFiles(
                              LibraryPath,
                              "*",
                              SearchOption.AllDirectories)
                          .Where(KnownChartImporters.CanImport)
                          .Order(StringComparer.OrdinalIgnoreCase)
                          .ToArray();
            }
            catch (DirectoryNotFoundException)
            {
                return clearManagedChartsForMissingLibrary(
                    preferKeysounds,
                    preferSscSimfiles,
                    enableBmsScratch);
            }
            int contentReadCount = 0;
            int cacheHitCount = 0;

            foreach (string source in sources)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var sourceInfo = new FileInfo(source);
                    string relativeSource = ManagedChartLibraryIndex.RelativePath(
                        LibraryPath,
                        source);
                    if (cached.TryGetValue(relativeSource, out ManagedChartIndexEntry entry)
                        && ManagedChartLibraryIndex.IsCurrent(
                            entry,
                            LibraryPath,
                            sourceInfo))
                    {
                        loaded.AddRange(entry.Charts);
                        updatedEntries.Add(entry);
                        cacheHitCount++;
                        continue;
                    }

                    contentReadCount++;
                    IReadOnlyList<ChartImportResult> results =
                        await KnownChartImporters.ImportAllAsync(
                            new ChartImportRequest(
                                source,
                                preferKeysounds,
                                preferSscSimfiles,
                                enableBmsScratch,
                                cancellationToken));
                    ImportedChart[] imported = createImportedCharts(results, source);
                    loaded.AddRange(imported);
                    updatedEntries.Add(createManagedIndexEntry(
                        sourceInfo,
                        imported));
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    Logger.Log(
                        $"Could not load persisted beatmap '{source}': {ex.Message}",
                        LoggingTarget.Runtime,
                        LogLevel.Error);
                }
            }

            ImportedChartLibraryChange change;
            lock (syncRoot)
            {
                evictMaterialisedCharts(id =>
                    !id.StartsWith("external-osu\u001f", StringComparison.Ordinal));
                charts.RemoveAll(chart =>
                    chart.SourceKind == ImportedChartSourceKind.Managed);
                charts.AddRange(loaded);
                refreshVisibleExternalChartsAfterManagedChange();
                change = advanceRevision(
                    ImportedChartLibraryChangeKind.Structure);
            }

            trySaveManagedIndex(
                preferKeysounds,
                preferSscSimfiles,
                enableBmsScratch,
                updatedEntries);
            managedPreferKeysounds = preferKeysounds;
            managedPreferSscSimfiles = preferSscSimfiles;
            managedEnableBmsScratch = enableBmsScratch;
            LastManagedScannedFileCount = sources.Length;
            LastManagedContentReadCount = contentReadCount;
            LastManagedCacheHitCount = cacheHitCount;

            msdRatingCache.SaveIfChanged();
            starRatingCache.SaveIfChanged();
            LibraryChanged?.Invoke(change);
            Logger.Log(
                $"Persistent beatmap scan loaded {loaded.Count} charts "
                + $"from {sources.Length} sources in "
                + $"{loadStopwatch.Elapsed.TotalMilliseconds:0} ms "
                + $"({cacheHitCount} cached, {contentReadCount} parsed).",
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
            initialiseManagedIndex();
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

        int configurationGeneration;
        lock (externalOsuStateLock)
        {
            externalOsuConfigurationGeneration++;
            configurationGeneration = externalOsuConfigurationGeneration;
            externalOsuSettings.SongsPath.Value = songsPath;
            disposeExternalWatcher();
            configureExternalAvailabilityMonitor(songsPath);
        }
        cancelExternalOsuRefresh();
        await loadCachedExternalOsuAsync(cancellationToken)
            .ConfigureAwait(false);
        lock (externalOsuStateLock)
        {
            if (!isExternalOsuConfigurationCurrent(
                    configurationGeneration,
                    songsPath))
            {
                return supersededExternalOsuResult();
            }
        }
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
            disposeExternalAvailabilityMonitor();
            replaceExternalCharts([]);
        }
        cancelExternalOsuRefresh();
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
        ImportedChartLibraryChange combinedChange = null;
        await importLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            foreach (string source in sources)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    ChartImportOperation operation = await importAsyncLocked(
                        new ChartImportRequest(
                            source,
                            preferKeysounds,
                            preferSscSimfiles,
                            enableBmsScratch,
                            cancellationToken)).ConfigureAwait(false);
                    importedCharts += operation.Results.Count;
                    combinedChange = combinedChange == null
                        ? operation.Change
                        : combinedChange.Merge(operation.Change);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    failedFiles++;
                    Logger.Error(
                        exception,
                        $"Could not import chart file '{source}' from folder '{folderPath}'.");
                }
            }

            if (combinedChange != null)
            {
                managedPreferKeysounds = preferKeysounds;
                managedPreferSscSimfiles = preferSscSimfiles;
                managedEnableBmsScratch = enableBmsScratch;
                saveManagedIndexFromCurrentCharts();
            }
        }
        finally
        {
            importLock.Release();
            if (combinedChange != null)
                LibraryChanged?.Invoke(combinedChange);
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
        using ExternalOsuRefreshLease refresh = beginExternalOsuRefresh(
            cancellationToken,
            out string songsPath,
            out int configurationGeneration);
        CancellationToken refreshToken = refresh.Token;
        int difficultyGeneration = Interlocked.Increment(
            ref externalDifficultyGeneration);
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
                    replaceExternalCharts([]);
                    disposeExternalWatcher();
                }
            }
            return new ExternalOsuLibraryResult(
                false,
                songsPath,
                0,
                "The configured osu! Songs directory is unavailable. External charts were hidden.");
        }

        Stopwatch stopwatch = Stopwatch.StartNew();
        try
        {
            await importLock.WaitAsync(refreshToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            return supersededExternalOsuResult();
        }

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
                    out bool enumerationComplete,
                    refreshToken);
            var loaded = new ConcurrentBag<ExternalOsuIndexEntry>();
            var changed = new List<ExternalOsuFileSnapshot>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (ExternalOsuFileSnapshot snapshot in snapshots)
            {
                refreshToken.ThrowIfCancellationRequested();
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
                    CancellationToken = refreshToken,
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
                    if (!seen.Contains(retained.SourcePath)
                        && File.Exists(retained.SourcePath))
                        loaded.Add(retained);
                }
            }

            ExternalOsuIndexEntry[] entries = loaded
                                               .OrderBy(
                                                   entry => entry.SourcePath,
                                                   StringComparer.OrdinalIgnoreCase)
                                               .ToArray();
            refreshToken.ThrowIfCancellationRequested();
            ImportedChart[] externalCharts = createExternalCharts(entries);
            bool indexChanged = changed.Count > 0
                                || cachedDocument == null
                                || cached.Count != entries.Length;
            msdRatingCache.SaveIfChanged();
            starRatingCache.SaveIfChanged();

            try
            {
                refreshToken.ThrowIfCancellationRequested();
                if (indexChanged)
                {
                    ExternalOsuSongsIndex.Save(
                        externalOsuCachePath,
                        songsPath,
                        entries);
                }
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
                refreshToken.ThrowIfCancellationRequested();
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
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            return supersededExternalOsuResult();
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

        if (!Directory.Exists(songsPath))
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
                              entry.BeatmapFingerprint,
                              true);
                      })
                      .ToArray();
    }

    private void replaceExternalCharts(
        IReadOnlyList<ImportedChart> external,
        ImportedChartLibraryChangeKind requestedChangeKind =
            ImportedChartLibraryChangeKind.Structure)
    {
        bool changed;
        ImportedChartLibraryChange change = null;
        lock (syncRoot)
        {
            indexedExternalCharts = external.ToArray();
            external = visibleExternalCharts(indexedExternalCharts);
            ImportedChart[] existing = charts
                                       .Where(chart => chart.SourceKind
                                                       == ImportedChartSourceKind.ExternalOsu)
                                       .ToArray();
            var existingById = existing.ToDictionary(
                chart => chart.Id,
                StringComparer.OrdinalIgnoreCase);
            var retainedIds = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            var incomingIds = new HashSet<string>(
                external.Select(chart => chart.Id),
                StringComparer.OrdinalIgnoreCase);
            var upserted = new List<ImportedChart>();
            var merged = new ImportedChart[external.Count];
            bool orderChanged = false;
            ImportedChartLibraryChangeKind actualChangeKind =
                requestedChangeKind;

            if (existing.Length != external.Count)
                actualChangeKind |= ImportedChartLibraryChangeKind.Structure;

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
                    upserted.Add(incoming);

                    if (current != null
                        && externalChartEquivalentIgnoringRatings(
                            current,
                            incoming))
                    {
                        retainedIds.Add(current.Id);
                    }
                    else
                    {
                        actualChangeKind |=
                            ImportedChartLibraryChangeKind.Structure;
                    }
                }

                if (i >= existing.Length
                    || !string.Equals(
                        existing[i].Id,
                        incoming.Id,
                        StringComparison.OrdinalIgnoreCase))
                {
                    actualChangeKind |= ImportedChartLibraryChangeKind.Structure;
                    orderChanged = true;
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
            var mergedById = merged.ToDictionary(
                chart => chart.Id,
                StringComparer.OrdinalIgnoreCase);
            indexedExternalCharts = indexedExternalCharts
                                   .Select(chart => mergedById.TryGetValue(
                                       chart.Id,
                                       out ImportedChart retained)
                                           ? retained
                                           : chart)
                                   .ToArray();
            string[] removedIds = existing
                                  .Where(chart => !incomingIds.Contains(chart.Id))
                                  .Select(chart => chart.Id)
                                  .ToArray();

            // Full beatmaps are loaded on demand. Keep LRU entries whose
            // source did not change instead of throwing away all 64 cached
            // decodes after an unrelated file-system event.
            LinkedListNode<string> node = externalBeatmapLru.First;
            while (node != null)
            {
                LinkedListNode<string> next = node.Next;
                if (node.Value.StartsWith(
                        "external-osu\u001f",
                        StringComparison.Ordinal)
                    && !retainedIds.Contains(node.Value))
                {
                    externalBeatmapCache.Remove(node.Value);
                    externalBeatmapLru.Remove(node);
                }
                node = next;
            }
            change = advanceRevision(
                actualChangeKind,
                orderChanged
                && upserted.Count == 0
                && removedIds.Length == 0
                    ? null
                    : new ImportedChartLibraryDelta(
                        upserted.ToArray(),
                        removedIds));
        }

        if (changed)
            LibraryChanged?.Invoke(change);
    }

    private ImportedChart[] visibleExternalCharts(
        IReadOnlyList<ImportedChart> external)
    {
        Debug.Assert(Monitor.IsEntered(syncRoot));

        var managedSourceHashes = new HashSet<string>(
            charts.Where(chart => chart.SourceKind
                                  == ImportedChartSourceKind.Managed)
                  .Select(osuSourceHash)
                  .Where(hash => hash != null),
            StringComparer.OrdinalIgnoreCase);
        var seenSourceHashes = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visible = new List<ImportedChart>(external.Count);

        foreach (ImportedChart chart in external)
        {
            if (!seenIds.Add(chart.Id))
                continue;

            string sourceHash = osuSourceHash(chart);
            if (sourceHash != null
                && (managedSourceHashes.Contains(sourceHash)
                    || !seenSourceHashes.Add(sourceHash)))
            {
                continue;
            }

            visible.Add(chart);
        }

        return visible.ToArray();
    }

    private void refreshVisibleExternalChartsAfterManagedChange()
    {
        Debug.Assert(Monitor.IsEntered(syncRoot));

        ImportedChart[] existing = charts
                                   .Where(chart => chart.SourceKind
                                                   == ImportedChartSourceKind.ExternalOsu)
                                   .ToArray();
        Dictionary<string, ImportedChart> existingById = existing
            .GroupBy(chart => chart.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.First(),
                StringComparer.OrdinalIgnoreCase);
        ImportedChart[] visible = visibleExternalCharts(indexedExternalCharts)
            .Select(chart => existingById.TryGetValue(
                             chart.Id,
                                 out ImportedChart current)
                             && externalChartEquivalentIgnoringRatings(
                                 current,
                                 chart)
                ? current
                : chart)
            .ToArray();
        var visibleIds = new HashSet<string>(
            visible.Select(chart => chart.Id),
            StringComparer.OrdinalIgnoreCase);

        charts.RemoveAll(chart =>
            chart.SourceKind == ImportedChartSourceKind.ExternalOsu);
        charts.AddRange(visible);
        evictMaterialisedCharts(id =>
            id.StartsWith("external-osu\u001f", StringComparison.Ordinal)
            && !visibleIds.Contains(id));
    }

    private static string osuSourceHash(ImportedChart chart)
    {
        string sourceHash = chart.Result.SourceHash;
        return chart.Result.Beatmap.SourceFormat == ChartSourceFormat.OsuMania
               && !string.IsNullOrWhiteSpace(sourceHash)
            ? sourceHash.Trim()
            : null;
    }

    private void updateIndexedExternalChart(ImportedChart replacement)
    {
        Debug.Assert(Monitor.IsEntered(syncRoot));
        if (replacement.SourceKind != ImportedChartSourceKind.ExternalOsu)
            return;

        for (int index = 0; index < indexedExternalCharts.Length; index++)
        {
            if (indexedExternalCharts[index].Id.Equals(
                    replacement.Id,
                    StringComparison.OrdinalIgnoreCase))
            {
                indexedExternalCharts[index] = replacement;
            }
        }
    }

    private static bool externalChartEquivalent(
        ImportedChart current,
        ImportedChart incoming) =>
        externalChartEquivalentIgnoringRatings(current, incoming)
        && Equals(current.DifficultyRating, incoming.DifficultyRating)
        && Equals(current.StarRating, incoming.StarRating);

    private static bool externalChartEquivalentIgnoringRatings(
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
        && current.SourceKind == incoming.SourceKind;

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
        // full external index. Separately publish completed values to active
        // consumers on a time throttle so visible rows do not remain pending
        // until a large library's entire difficulty pass has completed.
        const int checkpointBatchSize = 2048;
        const int progressPublishBatchSize = 128;
        int completedSinceCheckpoint = 0;
        bool completedAny = false;
        Stopwatch progressPublishStopwatch = Stopwatch.StartNew();
        var completedForPublish = new List<ExternalOsuIndexEntry>(
            progressPublishBatchSize);

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
                completedForPublish.Add(entries[index]);
                if (completedForPublish.Count >= progressPublishBatchSize
                    || progressPublishStopwatch.ElapsedMilliseconds
                    >= difficulty_progress_publish_interval_milliseconds)
                {
                    if (!publishExternalDifficultyProgress(
                            songsPath,
                            completedForPublish,
                            generation))
                    {
                        return;
                    }
                    completedForPublish.Clear();
                    progressPublishStopwatch.Restart();
                }
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

    private bool publishExternalDifficultyProgress(
        string songsPath,
        IReadOnlyList<ExternalOsuIndexEntry> completed,
        int generation)
    {
        ImportedChartLibraryChange change = null;
        bool changed = false;
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

            lock (syncRoot)
            {
                var chartIndices = charts
                                  .Select((chart, index) => (chart.Id, index))
                                  .ToDictionary(
                                      item => item.Id,
                                      item => item.index,
                                      StringComparer.OrdinalIgnoreCase);
                var updated = new List<ImportedChart>(completed.Count);
                foreach (ExternalOsuIndexEntry entry in completed)
                {
                    string id = $"external-osu\u001f{entry.SourcePath}";
                    if (!chartIndices.TryGetValue(id, out int index))
                        continue;

                    ImportedChart current = charts[index];
                    if (Equals(current.DifficultyRating, entry.DifficultyRating)
                        && Equals(current.StarRating, entry.StarRating))
                    {
                        continue;
                    }

                    ImportedChart replacement = current with
                    {
                        DifficultyRating = entry.DifficultyRating,
                        StarRating = entry.StarRating,
                    };
                    charts[index] = replacement;
                    updateIndexedExternalChart(replacement);
                    updated.Add(replacement);
                    changed = true;
                }

                if (changed)
                {
                    change = advanceRevision(
                        ImportedChartLibraryChangeKind.DifficultyRatings,
                        new ImportedChartLibraryDelta(updated.ToArray(), []));
                }
            }
        }

        if (change != null)
            LibraryChanged?.Invoke(change);
        return true;
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

                replaceExternalCharts(
                    createExternalCharts(entries),
                    ImportedChartLibraryChangeKind.DifficultyRatings);
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

    private void configureExternalAvailabilityMonitor(string songsPath)
    {
        disposeExternalAvailabilityMonitor();
        if (disposed || string.IsNullOrWhiteSpace(songsPath))
            return;

        observedExternalOsuPath = songsPath;
        observedExternalOsuAvailable = Directory.Exists(songsPath);
        externalAvailabilityTimer = new Timer(
            _ => checkExternalOsuAvailability(),
            null,
            1000,
            1000);
    }

    private void checkExternalOsuAvailability()
    {
        lock (externalOsuStateLock)
        {
            if (disposed
                || string.IsNullOrWhiteSpace(observedExternalOsuPath)
                || !string.Equals(
                    observedExternalOsuPath,
                    ExternalOsuSongsPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            bool available = Directory.Exists(observedExternalOsuPath);
            if (observedExternalOsuAvailable == available)
                return;

            observedExternalOsuAvailable = available;
        }

        _ = RefreshExternalOsuAsync().ContinueWith(task =>
        {
            if (task.Exception != null)
            {
                Logger.Error(
                    task.Exception.GetBaseException(),
                    "Could not refresh the external osu! library after its path availability changed.");
            }
        }, TaskContinuationOptions.OnlyOnFaulted);
    }

    private void disposeExternalAvailabilityMonitor()
    {
        externalAvailabilityTimer?.Dispose();
        externalAvailabilityTimer = null;
        observedExternalOsuPath = null;
        observedExternalOsuAvailable = null;
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
        ImportedChartLibraryChange change = addOrReplaceCore(
            results,
            sourcePath);
        LibraryChanged?.Invoke(change);
    }

    private ImportedChartLibraryChange addOrReplaceCore(
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

        ImportedChartLibraryChange change;
        lock (syncRoot)
        {
            evictMaterialisedCharts(id => id.StartsWith(
                sourcePath + "\u001f",
                StringComparison.OrdinalIgnoreCase));
            charts.RemoveAll(chart =>
                chart.SourceKind == ImportedChartSourceKind.Managed
                && chart.SourcePath.Equals(
                    sourcePath,
                    StringComparison.OrdinalIgnoreCase));
            charts.AddRange(imported);
            refreshVisibleExternalChartsAfterManagedChange();
            change = advanceRevision(
                ImportedChartLibraryChangeKind.Structure);
        }
        return change;
    }

    private ImportedChartLibraryChange advanceRevision(
        ImportedChartLibraryChangeKind kind,
        ImportedChartLibraryDelta delta = null)
    {
        Debug.Assert(Monitor.IsEntered(syncRoot));
        revision++;
        if ((kind & ImportedChartLibraryChangeKind.Structure) != 0)
            structureRevision++;

        return new ImportedChartLibraryChange(
            revision,
            structureRevision,
            kind,
            charts.Count,
            delta);
    }

    private void evictMaterialisedCharts(Func<string, bool> predicate)
    {
        Debug.Assert(Monitor.IsEntered(syncRoot));
        LinkedListNode<string> node = externalBeatmapLru.First;
        while (node != null)
        {
            LinkedListNode<string> next = node.Next;
            if (predicate(node.Value))
            {
                externalBeatmapCache.Remove(node.Value);
                externalBeatmapLru.Remove(node);
            }
            node = next;
        }
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

    private ImportedChart materialiseChart(ImportedChart chart)
    {
        if (chart == null
            || !chart.RequiresMaterialisation
               && chart.SourceKind != ImportedChartSourceKind.ExternalOsu)
        {
            return chart;
        }

        if (!File.Exists(chart.SourcePath))
        {
            throw new FileNotFoundException(
                "The chart source file is unavailable.",
                chart.SourcePath);
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

        if (chart.SourceKind == ImportedChartSourceKind.ExternalOsu
            && !ExternalOsuSongsIndex.IsManiaFile(chart.SourcePath))
        {
            throw new InvalidDataException(
                "The external beatmap is no longer an osu!mania chart.");
        }

        IReadOnlyList<ChartImportResult> results =
            KnownChartImporters.ImportAllAsync(
                    new ChartImportRequest(
                        chart.SourcePath,
                        chart.SourceKind == ImportedChartSourceKind.ExternalOsu
                            || managedPreferKeysounds,
                        chart.SourceKind == ImportedChartSourceKind.ExternalOsu
                            || managedPreferSscSimfiles,
                        chart.SourceKind == ImportedChartSourceKind.Managed
                            && managedEnableBmsScratch))
                .AsTask()
                .GetAwaiter()
                .GetResult();
        result = selectMaterialisedResult(chart, results);

        lock (syncRoot)
        {
            ImportedChart current = charts.FirstOrDefault(candidate =>
                candidate.Id.Equals(
                    chart.Id,
                    StringComparison.OrdinalIgnoreCase));
            if (current == null
                || !externalChartEquivalentIgnoringRatings(current, chart))
            {
                throw new InvalidDataException(
                    "The beatmap changed while it was loading.");
            }

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

    private async Task<ImportedChart> materialiseChartAsync(
        ImportedChart chart,
        CancellationToken cancellationToken)
    {
        if (chart == null
            || !chart.RequiresMaterialisation
               && chart.SourceKind != ImportedChartSourceKind.ExternalOsu)
        {
            return chart;
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (!File.Exists(chart.SourcePath))
        {
            throw new FileNotFoundException(
                "The chart source file is unavailable.",
                chart.SourcePath);
        }

        lock (syncRoot)
        {
            if (externalBeatmapCache.TryGetValue(chart.Id, out ChartImportResult cached))
            {
                externalBeatmapLru.Remove(chart.Id);
                externalBeatmapLru.AddFirst(chart.Id);
                return chart with { Result = cached };
            }
        }

        ChartImportResult result = await Task.Run(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (chart.SourceKind == ImportedChartSourceKind.ExternalOsu
                && !ExternalOsuSongsIndex.IsManiaFile(chart.SourcePath))
            {
                throw new InvalidDataException(
                    "The external beatmap is no longer an osu!mania chart.");
            }

            IReadOnlyList<ChartImportResult> results =
                await KnownChartImporters.ImportAllAsync(
                    new ChartImportRequest(
                        chart.SourcePath,
                        chart.SourceKind == ImportedChartSourceKind.ExternalOsu
                            || managedPreferKeysounds,
                        chart.SourceKind == ImportedChartSourceKind.ExternalOsu
                            || managedPreferSscSimfiles,
                        chart.SourceKind == ImportedChartSourceKind.Managed
                            && managedEnableBmsScratch,
                        cancellationToken)).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return selectMaterialisedResult(chart, results);
        }, cancellationToken).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();
        lock (syncRoot)
        {
            ImportedChart current = charts.FirstOrDefault(candidate =>
                candidate.Id.Equals(
                    chart.Id,
                    StringComparison.OrdinalIgnoreCase));
            if (current == null
                || !externalChartEquivalentIgnoringRatings(current, chart))
            {
                throw new InvalidDataException(
                    "The beatmap changed while it was loading.");
            }

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

    private static ChartImportResult selectMaterialisedResult(
        ImportedChart chart,
        IReadOnlyList<ChartImportResult> results)
    {
        if (chart.SourceKind == ImportedChartSourceKind.ExternalOsu)
        {
            return results.Count == 1
                ? results[0]
                : throw new InvalidDataException(
                    "The external .osu file did not produce exactly one chart.");
        }

        int separator = chart.Id.LastIndexOf('\u001f');
        if (separator >= 0
            && int.TryParse(chart.Id.AsSpan(separator + 1), out int ordinal)
            && ordinal >= 0
            && ordinal < results.Count)
        {
            ChartImportResult candidate = results[ordinal];
            if (fingerprintMatches(chart, candidate))
                return candidate;

            ChartImportResult[] exactMatches = results.Where(result =>
                fingerprintMatches(chart, result)).ToArray();
            if (exactMatches.Length == 1)
                return exactMatches[0];
        }

        throw new InvalidDataException(
            "The chart source no longer produces the indexed difficulty.");
    }

    private static bool fingerprintMatches(
        ImportedChart chart,
        ChartImportResult result) =>
        !string.IsNullOrWhiteSpace(chart.BeatmapFingerprint)
        && string.Equals(
            YokkoBeatmapFingerprint.Compute(result.Beatmap),
            chart.BeatmapFingerprint,
            StringComparison.OrdinalIgnoreCase);

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

    private ManagedChartIndexEntry createManagedIndexEntry(
        FileInfo source,
        IReadOnlyList<ImportedChart> imported)
    {
        ImportedChart[] summaries = imported.Select(chart => chart with
        {
            Result = chart.Result with
            {
                Beatmap = createExternalSummary(
                    chart.Result.Beatmap,
                    chart.Bpm ?? primaryBpm(chart.Result.Beatmap)),
            },
            RequiresMaterialisation = true,
        }).ToArray();
        var dependencyPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var watchedDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            source.DirectoryName!,
        };

        foreach (ImportedChart chart in imported)
        {
            addDependency(chart.Result.Beatmap.AudioPath);
            addDependency(chart.ArtworkPath);
            addDependency(chart.Result.ArtworkPath);
            foreach (YokkoScheduledSample sample in chart.Result.Beatmap.ScheduledSamples)
                addDependency(sample.Path);
            foreach (YokkoHitObject hitObject in chart.Result.Beatmap.HitObjects)
            {
                foreach (YokkoHitSample sample in hitObject.Samples)
                    addDependency(sample.Filename);
                foreach (IReadOnlyList<YokkoHitSample> node in hitObject.NodeSamples)
                {
                    foreach (YokkoHitSample sample in node)
                        addDependency(sample.Filename);
                }
            }
        }

        ManagedChartFileSnapshot[] dependencies = dependencyPaths
            .Where(File.Exists)
            .Select(path => new FileInfo(path))
            .Select(info => new ManagedChartFileSnapshot(
                ManagedChartLibraryIndex.RelativePath(LibraryPath, info.FullName),
                info.Length,
                info.LastWriteTimeUtc.Ticks))
            .OrderBy(snapshot => snapshot.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        ManagedChartDirectorySnapshot[] directories = watchedDirectories
            .Where(Directory.Exists)
            .Select(path => new ManagedChartDirectorySnapshot(
                ManagedChartLibraryIndex.RelativePath(LibraryPath, path),
                Directory.GetLastWriteTimeUtc(path).Ticks))
            .OrderBy(snapshot => snapshot.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new ManagedChartIndexEntry(
            ManagedChartLibraryIndex.RelativePath(LibraryPath, source.FullName),
            source.Length,
            source.LastWriteTimeUtc.Ticks,
            summaries,
            dependencies,
            directories);

        void addDependency(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            string candidate = Path.IsPathRooted(path)
                ? Path.GetFullPath(path)
                : Path.GetFullPath(Path.Combine(source.DirectoryName!, path));
            string relative = Path.GetRelativePath(LibraryPath, candidate);
            if (relative.Equals("..", StringComparison.Ordinal)
                || relative.StartsWith(
                    $"..{Path.DirectorySeparatorChar}",
                    StringComparison.Ordinal))
            {
                return;
            }

            if (!candidate.Equals(source.FullName, StringComparison.OrdinalIgnoreCase))
                dependencyPaths.Add(candidate);
            string directory = Path.GetDirectoryName(candidate);
            if (!string.IsNullOrWhiteSpace(directory))
                watchedDirectories.Add(directory);
        }
    }

    private void saveManagedIndexFromCurrentCharts()
    {
        ImportedChart[] managed;
        lock (syncRoot)
        {
            managed = charts.Where(chart =>
                chart.SourceKind == ImportedChartSourceKind.Managed).ToArray();
        }

        ManagedChartIndexDocument existing = ManagedChartLibraryIndex.Load(
            managedIndexPath,
            LibraryPath,
            managedPreferKeysounds,
            managedPreferSscSimfiles,
            managedEnableBmsScratch);
        Dictionary<string, ManagedChartIndexEntry> cached = existing?.Entries
            .ToDictionary(
                entry => entry.SourceRelativePath,
                StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, ManagedChartIndexEntry>(
                StringComparer.OrdinalIgnoreCase);
        var entries = new List<ManagedChartIndexEntry>();

        foreach (IGrouping<string, ImportedChart> group in managed.GroupBy(
                     chart => chart.SourcePath,
                     StringComparer.OrdinalIgnoreCase))
        {
            if (!File.Exists(group.Key))
                continue;

            var source = new FileInfo(group.Key);
            string relative = ManagedChartLibraryIndex.RelativePath(
                LibraryPath,
                source.FullName);
            if (cached.TryGetValue(relative, out ManagedChartIndexEntry entry)
                && ManagedChartLibraryIndex.IsCurrent(entry, LibraryPath, source)
                && group.All(chart => chart.RequiresMaterialisation))
            {
                entries.Add(entry);
            }
            else
            {
                entries.Add(createManagedIndexEntry(source, group.ToArray()));
            }
        }

        trySaveManagedIndex(
            managedPreferKeysounds,
            managedPreferSscSimfiles,
            managedEnableBmsScratch,
            entries);
    }

    private void trySaveManagedIndex(
        bool preferKeysounds,
        bool preferSscSimfiles,
        bool enableBmsScratch,
        IReadOnlyList<ManagedChartIndexEntry> entries)
    {
        try
        {
            ManagedChartLibraryIndex.Save(
                managedIndexPath,
                LibraryPath,
                preferKeysounds,
                preferSscSimfiles,
                enableBmsScratch,
                entries);
        }
        catch (Exception exception) when (exception is IOException
                                          or UnauthorizedAccessException
                                          or ArgumentException
                                          or JsonException
                                          or NotSupportedException)
        {
            Logger.Error(exception, "Could not save the managed beatmap library index.");
        }
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

    private void initialiseManagedIndex()
    {
        managedIndexPath = Path.Combine(
            LibraryPath,
            ".yokko-cache",
            ManagedChartLibraryIndex.FileName);
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

    private ExternalOsuRefreshLease beginExternalOsuRefresh(
        CancellationToken cancellationToken,
        out string songsPath,
        out int configurationGeneration)
    {
        CancellationTokenSource previous;
        CancellationTokenSource current =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        lock (externalOsuStateLock)
        {
            songsPath = ExternalOsuSongsPath;
            configurationGeneration = externalOsuConfigurationGeneration;
            previous = externalOsuRefreshCancellation;
            externalOsuRefreshCancellation = current;
        }

        // Cancel outside the state lock. Cancellation continuations are free
        // to complete the previous refresh without re-entering this lock.
        previous?.Cancel();
        return new ExternalOsuRefreshLease(this, current);
    }

    private void cancelExternalOsuRefresh()
    {
        CancellationTokenSource cancellation;
        lock (externalOsuStateLock)
        {
            cancellation = externalOsuRefreshCancellation;
            externalOsuRefreshCancellation = null;
        }

        cancellation?.Cancel();
    }

    private sealed class ExternalOsuRefreshLease : IDisposable
    {
        private readonly ImportedChartLibrary owner;
        private CancellationTokenSource cancellation;

        internal ExternalOsuRefreshLease(
            ImportedChartLibrary owner,
            CancellationTokenSource cancellation)
        {
            this.owner = owner;
            this.cancellation = cancellation;
        }

        internal CancellationToken Token => cancellation.Token;

        public void Dispose()
        {
            CancellationTokenSource source = Interlocked.Exchange(
                ref cancellation,
                null);
            if (source == null)
                return;

            lock (owner.externalOsuStateLock)
            {
                if (ReferenceEquals(
                        owner.externalOsuRefreshCancellation,
                        source))
                {
                    owner.externalOsuRefreshCancellation = null;
                }
            }

            source.Dispose();
        }
    }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        Interlocked.Increment(ref externalDifficultyGeneration);
        cancelExternalOsuRefresh();
        SetExternalIndexingPaused(false);
        disposeExternalWatcher();
        disposeExternalAvailabilityMonitor();
    }
}
