using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Logging;
using osu.Framework.Platform;
using Yokko.Game.Importing;
using Yokko.Game.Skinning.OsuMania;

namespace Yokko.Game.Resources;

internal sealed record ResourceMigrationResult(
    bool Success,
    string Message,
    string RootPath,
    int LoadedChartCount = 0,
    bool PreviousDataRetained = false);

/// <summary>
/// Resolves Yokko's resource root and coordinates moving both beatmaps and
/// skins when the user changes it.
/// </summary>
internal sealed class YokkoResourceStorage
{
    private readonly SemaphoreSlim migrationLock = new(1, 1);
    private Storage storage;
    private YokkoResourceSettings settings;
    private YokkoImportSettings importSettings;
    private ImportedChartLibrary chartLibrary;
    private OsuManiaSkinLibrary skinLibrary;
    private YokkoSkinSettings skinSettings;
    private string defaultRootPath;

    public event Action LocationChanged;

    public string RootPath { get; private set; }
    public string BeatmapsPath => Path.Combine(RootPath, "Beatmaps");
    public string SkinsPath => Path.Combine(RootPath, "Skins");
    public bool UsesCustomPath => !string.IsNullOrWhiteSpace(settings?.RootPath.Value);

    public void Initialise(
        Storage hostStorage,
        YokkoResourceSettings resourceSettings,
        YokkoImportSettings yokkoImportSettings,
        ImportedChartLibrary importedChartLibrary,
        OsuManiaSkinLibrary osuManiaSkinLibrary,
        YokkoSkinSettings skinSettings)
    {
        storage = hostStorage ?? throw new ArgumentNullException(nameof(hostStorage));
        settings = resourceSettings ?? throw new ArgumentNullException(nameof(resourceSettings));
        importSettings = yokkoImportSettings ?? throw new ArgumentNullException(nameof(yokkoImportSettings));
        chartLibrary = importedChartLibrary ?? throw new ArgumentNullException(nameof(importedChartLibrary));
        skinLibrary = osuManiaSkinLibrary ?? throw new ArgumentNullException(nameof(osuManiaSkinLibrary));
        this.skinSettings = skinSettings ?? throw new ArgumentNullException(nameof(skinSettings));
        defaultRootPath = Path.GetFullPath(storage.GetFullPath(YokkoResourceDirectories.Root, true));

        try
        {
            RootPath = resolveConfiguredRoot(settings.RootPath.Value);
            ensureStructure(RootPath);
        }
        catch (Exception ex)
        {
            Logger.Log(
                $"Could not use configured resource directory: {ex.Message}. Falling back to Yokko storage.",
                LoggingTarget.Runtime,
                LogLevel.Error);
            settings.RootPath.Value = string.Empty;
            RootPath = defaultRootPath;
            ensureStructure(RootPath);
        }

        migrateLegacySkinDirectory();
        chartLibrary.Initialise(BeatmapsPath);
        skinLibrary.Initialise(SkinsPath, skinSettings);
    }

    public Task<ResourceMigrationResult> MigrateToDefaultAsync(
        CancellationToken cancellationToken = default) =>
        MigrateAsync(defaultRootPath, true, cancellationToken);

    public Task<ResourceMigrationResult> MigrateAsync(
        string targetRoot,
        CancellationToken cancellationToken = default) =>
        MigrateAsync(targetRoot, false, cancellationToken);

    private async Task<ResourceMigrationResult> MigrateAsync(
        string targetRoot,
        bool useDefaultSetting,
        CancellationToken cancellationToken)
    {
        ensureInitialised();
        await migrationLock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            string destinationRoot;

            try
            {
                destinationRoot = normaliseRoot(targetRoot);
                ensureValidMigrationTarget(destinationRoot);
            }
            catch (Exception ex)
            {
                return new ResourceMigrationResult(false, ex.Message, RootPath);
            }

            if (destinationRoot.Equals(RootPath, StringComparison.OrdinalIgnoreCase))
            {
                settings.RootPath.Value = useDefaultSetting
                    ? string.Empty
                    : destinationRoot;
                return new ResourceMigrationResult(
                    true,
                    "Resource directory is already in use.",
                    RootPath,
                    chartLibrary.GetCharts().Count);
            }

            string previousRoot = RootPath;

            try
            {
                await Task.Run(
                    () =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        ensureStructure(destinationRoot);
                        migrateCategory(
                            Path.Combine(previousRoot, "Beatmaps"),
                            Path.Combine(destinationRoot, "Beatmaps"),
                            keepLooseFilesTogether: true);
                        IReadOnlyDictionary<string, string> migratedSkins =
                            migrateCategory(
                                Path.Combine(previousRoot, "Skins"),
                                Path.Combine(destinationRoot, "Skins"),
                                keepLooseFilesTogether: false);

                        if (migratedSkins.TryGetValue(
                                this.skinSettings.SelectedSkinId.Value,
                                out string migratedSelectedId))
                        {
                            this.skinSettings.SelectedSkinId.Value =
                                migratedSelectedId;
                        }
                    },
                    cancellationToken).ConfigureAwait(false);

                RootPath = destinationRoot;
                settings.RootPath.Value = useDefaultSetting
                    ? string.Empty
                    : destinationRoot;
                skinLibrary.ChangeLibraryPath(SkinsPath);
                int loadedCharts = await chartLibrary.ChangeLibraryPathAsync(
                    BeatmapsPath,
                    importSettings.PreferKeysounds.Value,
                    importSettings.PreferSscSimfiles.Value,
                    importSettings.EnableBmsScratch.Value,
                    cancellationToken).ConfigureAwait(false);

                bool previousDataRetained = !tryDeleteOldResources(previousRoot);
                LocationChanged?.Invoke();
                return new ResourceMigrationResult(
                    true,
                    previousDataRetained
                        ? "Resources migrated. Some old files are still in use and were retained."
                        : "Resources migrated successfully.",
                    RootPath,
                    loadedCharts,
                    previousDataRetained);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return new ResourceMigrationResult(
                    false,
                    $"Could not migrate resources: {ex.Message}",
                    RootPath);
            }
        }
        finally
        {
            migrationLock.Release();
        }
    }

    private string resolveConfiguredRoot(string configuredPath) =>
        string.IsNullOrWhiteSpace(configuredPath)
            ? defaultRootPath
            : normaliseRoot(configuredPath);

    private static string normaliseRoot(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Path.GetFullPath(Environment.ExpandEnvironmentVariables(path.Trim()));
    }

    private void ensureValidMigrationTarget(string destinationRoot)
    {
        string[] sourceCategories = [BeatmapsPath, SkinsPath];
        string[] targetCategories =
        [
            Path.Combine(destinationRoot, "Beatmaps"),
            Path.Combine(destinationRoot, "Skins"),
        ];

        for (int i = 0; i < sourceCategories.Length; i++)
        {
            if (pathsOverlap(sourceCategories[i], targetCategories[i])
                && !Path.GetFullPath(sourceCategories[i]).Equals(
                    Path.GetFullPath(targetCategories[i]),
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "The new resource directory cannot be inside the current Beatmaps or Skins directory.");
            }
        }
    }

    private static bool pathsOverlap(string first, string second)
    {
        string firstPath = appendSeparator(Path.GetFullPath(first));
        string secondPath = appendSeparator(Path.GetFullPath(second));
        return firstPath.StartsWith(secondPath, StringComparison.OrdinalIgnoreCase)
               || secondPath.StartsWith(firstPath, StringComparison.OrdinalIgnoreCase);
    }

    private static string appendSeparator(string path) =>
        path.EndsWith(Path.DirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;

    private static void ensureStructure(string root)
    {
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(Path.Combine(root, "Beatmaps"));
        Directory.CreateDirectory(Path.Combine(root, "Skins"));

        string probe = Path.Combine(root, $".yokko-write-{Guid.NewGuid():N}.tmp");
        File.WriteAllBytes(probe, []);
        File.Delete(probe);
    }

    private static IReadOnlyDictionary<string, string> migrateCategory(
        string source,
        string destination,
        bool keepLooseFilesTogether)
    {
        var migratedNames = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);

        if (!Directory.Exists(source))
            return migratedNames;

        Directory.CreateDirectory(destination);
        string[] looseFiles = Directory.EnumerateFiles(source).ToArray();

        if (looseFiles.Length > 0)
        {
            string looseDestination = keepLooseFilesTogether
                ? findAvailablePath(destination, "Migrated files", string.Empty)
                : destination;

            Directory.CreateDirectory(looseDestination);
            foreach (string file in looseFiles)
            {
                string target = copyFileToAvailablePath(file, looseDestination);
                migratedNames[Path.GetFileName(file)] = Path.GetFileName(target);
            }
        }

        foreach (string directory in Directory.EnumerateDirectories(source))
        {
            string target = findAvailablePath(
                destination,
                Path.GetFileName(directory),
                string.Empty);
            copyDirectory(directory, target);
            migratedNames[Path.GetFileName(directory)] = Path.GetFileName(target);
        }

        return migratedNames;
    }

    private static string findAvailablePath(
        string directory,
        string baseName,
        string extension)
    {
        string candidate = Path.Combine(directory, baseName + extension);

        for (int suffix = 2;
             File.Exists(candidate) || Directory.Exists(candidate);
             suffix++)
        {
            candidate = Path.Combine(
                directory,
                $"{baseName} (migrated {suffix}){extension}");
        }

        return candidate;
    }

    private static string copyFileToAvailablePath(string source, string destination)
    {
        string extension = Path.GetExtension(source);
        string baseName = Path.GetFileNameWithoutExtension(source);
        string target = findAvailablePath(destination, baseName, extension);
        File.Copy(source, target);
        return target;
    }

    private static void copyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);

        foreach (string directory in Directory.EnumerateDirectories(
                     source,
                     "*",
                     SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(
                destination,
                Path.GetRelativePath(source, directory)));
        }

        foreach (string file in Directory.EnumerateFiles(
                     source,
                     "*",
                     SearchOption.AllDirectories))
        {
            string target = Path.Combine(
                destination,
                Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }

    private bool tryDeleteOldResources(string previousRoot)
    {
        try
        {
            deleteIfExists(Path.Combine(previousRoot, "Beatmaps"));
            deleteIfExists(Path.Combine(previousRoot, "Skins"));

            if (Directory.Exists(previousRoot)
                && !Directory.EnumerateFileSystemEntries(previousRoot).Any())
                Directory.Delete(previousRoot);

            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static void deleteIfExists(string path)
    {
        if (Directory.Exists(path))
            Directory.Delete(path, true);
    }

    private void migrateLegacySkinDirectory()
    {
        string legacy = storage.GetFullPath("Skins", false);
        IReadOnlyDictionary<string, string> migrated =
            migrateCategory(legacy, SkinsPath, keepLooseFilesTogether: false);

        if (migrated.TryGetValue(
                skinSettings.SelectedSkinId.Value,
                out string selectedId))
            skinSettings.SelectedSkinId.Value = selectedId;

        if (Directory.Exists(legacy))
            deleteIfExists(legacy);
    }

    private void ensureInitialised()
    {
        if (storage == null
            || settings == null
            || chartLibrary == null
            || skinLibrary == null
            || skinSettings == null
            || string.IsNullOrWhiteSpace(RootPath))
        {
            throw new InvalidOperationException(
                "Resource storage has not been initialised.");
        }
    }
}
