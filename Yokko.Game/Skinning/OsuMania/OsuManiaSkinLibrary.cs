using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using osu.Framework.Platform;
using Yokko.Game.Resources;

namespace Yokko.Game.Skinning.OsuMania;

internal sealed record OsuManiaSkinEntry(
    string Id,
    string Name,
    string Author,
    string Version,
    IReadOnlyList<int> KeyModes,
    string FullPath);

internal sealed record SkinImportResult(
    bool Success,
    string Message,
    OsuManiaSkinEntry Skin = null);

/// <summary>
/// Owns Yokko's managed osu!mania skin directory and keeps filesystem paths
/// out of UI and gameplay state.
/// </summary>
internal sealed class OsuManiaSkinLibrary
{
    private const string legacy_skins_directory = "Skins";

    private Storage storage;
    private YokkoSkinSettings settings;
    private string libraryPath;
    private readonly object importLock = new();

    public event Action LibraryChanged;

    public string LibraryPath => libraryPath;

    public string CurrentSkinPath
    {
        get
        {
            if (string.IsNullOrWhiteSpace(settings?.SelectedSkinId.Value))
                return null;

            try
            {
                string path = getManagedPath(settings.SelectedSkinId.Value);
                return File.Exists(path) || Directory.Exists(path) ? path : null;
            }
            catch (ArgumentException)
            {
                return null;
            }
        }
    }

    public void Initialise(Storage hostStorage, YokkoSkinSettings skinSettings)
    {
        storage = hostStorage ?? throw new ArgumentNullException(nameof(hostStorage));
        Initialise(
            storage.GetFullPath(YokkoResourceDirectories.Skins, true),
            skinSettings);
        migrateLegacySkins();
    }

    public void Initialise(string path, YokkoSkinSettings skinSettings)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        settings = skinSettings ?? throw new ArgumentNullException(nameof(skinSettings));
        libraryPath = Path.GetFullPath(path);
        Directory.CreateDirectory(libraryPath);
    }

    public void ChangeLibraryPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        lock (importLock)
        {
            libraryPath = Path.GetFullPath(path);
            Directory.CreateDirectory(libraryPath);
        }

        LibraryChanged?.Invoke();
    }

    public static bool IsSupportedDrop(string path) =>
        !string.IsNullOrWhiteSpace(path) &&
        ((File.Exists(path) &&
          (Path.GetExtension(path).Equals(".osk", StringComparison.OrdinalIgnoreCase) ||
           Path.GetFileName(path).Equals("skin.ini", StringComparison.OrdinalIgnoreCase))) ||
         Directory.Exists(path));

    public IReadOnlyList<OsuManiaSkinEntry> GetInstalledSkins()
    {
        ensureInitialised();

        var skins = new List<OsuManiaSkinEntry>();

        foreach (string path in Directory.EnumerateFileSystemEntries(libraryPath)
                                         .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
        {
            if (File.Exists(path) &&
                !Path.GetExtension(path).Equals(".osk", StringComparison.OrdinalIgnoreCase))
                continue;

            if (tryReadEntry(path, out OsuManiaSkinEntry entry))
                skins.Add(entry);
        }

        return skins;
    }

    public SkinImportResult Import(string sourcePath)
    {
        lock (importLock)
            return import(sourcePath);
    }

    private SkinImportResult import(string sourcePath)
    {
        ensureInitialised();

        if (!IsSupportedDrop(sourcePath))
            return new SkinImportResult(false, "Only .osk packages and folders containing skin.ini are supported.");

        string importSource = resolveImportSource(sourcePath);
        string error = null;

        if (importSource == null || !tryReadInfo(importSource, out OsuManiaSkinInfo info, out error))
            return new SkinImportResult(false, error ?? "No usable osu!mania skin.ini was found.");

        string baseName = File.Exists(importSource)
            ? Path.GetFileNameWithoutExtension(importSource)
            : new DirectoryInfo(importSource).Name;
        string extension = File.Exists(importSource) ? ".osk" : string.Empty;
        string id = findAvailableId(sanitiseName(baseName), extension);
        string destination = getManagedPath(id);

        try
        {
            if (File.Exists(importSource))
                File.Copy(importSource, destination);
            else
                copyDirectory(importSource, destination);

            var entry = createEntry(id, destination, info);
            settings.SelectedSkinId.Value = id;
            LibraryChanged?.Invoke();
            return new SkinImportResult(true, $"Imported and enabled {entry.Name}.", entry);
        }
        catch (Exception ex)
        {
            cleanupIncompleteImport(destination);
            return new SkinImportResult(false, $"Could not import skin: {ex.Message}");
        }
    }

    public bool Select(string id)
    {
        ensureInitialised();
        string path = getManagedPath(id);

        if (!File.Exists(path) && !Directory.Exists(path))
            return false;

        settings.SelectedSkinId.Value = id;
        LibraryChanged?.Invoke();
        return true;
    }

    public bool Delete(string id)
    {
        ensureInitialised();
        string path = getManagedPath(id);

        if (File.Exists(path))
            File.Delete(path);
        else if (Directory.Exists(path))
            Directory.Delete(path, true);
        else
            return false;

        if (settings.SelectedSkinId.Value.Equals(id, StringComparison.OrdinalIgnoreCase))
            settings.SelectedSkinId.Value = string.Empty;

        LibraryChanged?.Invoke();
        return true;
    }

    public bool IsSelected(string id) =>
        settings?.SelectedSkinId.Value.Equals(id, StringComparison.OrdinalIgnoreCase) == true;

    private string getManagedPath(string id)
    {
        ensureInitialised();

        if (string.IsNullOrWhiteSpace(id) ||
            Path.IsPathRooted(id) ||
            id.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) >= 0 ||
            id is "." or "..")
            throw new ArgumentException("Invalid managed skin id.", nameof(id));

        string path = Path.GetFullPath(Path.Combine(libraryPath, id));
        string root = Path.GetFullPath(libraryPath) + Path.DirectorySeparatorChar;

        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Skin path escaped the managed library.", nameof(id));

        return path;
    }

    private bool tryReadEntry(string path, out OsuManiaSkinEntry entry)
    {
        entry = null;

        if (!tryReadInfo(path, out OsuManiaSkinInfo info, out _))
            return false;

        entry = createEntry(Path.GetFileName(path), path, info);
        return true;
    }

    private static OsuManiaSkinEntry createEntry(string id, string path, OsuManiaSkinInfo info) =>
        new(
            id,
            info.Name == "Unknown" ? Path.GetFileNameWithoutExtension(path) : info.Name,
            info.Author,
            info.Version,
            info.ManiaConfigurations.Keys.Order().ToArray(),
            path);

    private static bool tryReadInfo(
        string path,
        out OsuManiaSkinInfo info,
        out string error)
    {
        info = null;
        error = null;

        try
        {
            using var source = new OsuManiaSkinSource(path);
            string contents = source.ReadSkinIni();

            if (string.IsNullOrWhiteSpace(contents))
            {
                error = "The dropped item does not contain skin.ini.";
                return false;
            }

            info = OsuManiaSkinIniDecoder.Decode(contents);

            if (info.ManiaConfigurations.Count == 0)
            {
                error = "This skin does not contain an osu!mania section.";
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            error = $"Could not read skin: {ex.Message}";
            return false;
        }
    }

    private static string resolveImportSource(string path)
    {
        if (File.Exists(path))
        {
            if (Path.GetFileName(path).Equals("skin.ini", StringComparison.OrdinalIgnoreCase))
                return Path.GetDirectoryName(Path.GetFullPath(path));

            return Path.GetFullPath(path);
        }

        string skinIni = Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)
                                  .Where(candidate => Path.GetFileName(candidate)
                                                          .Equals("skin.ini", StringComparison.OrdinalIgnoreCase))
                                  .OrderBy(candidate => candidate.Count(character => character is '\\' or '/'))
                                  .FirstOrDefault();

        return skinIni == null ? null : Path.GetDirectoryName(skinIni);
    }

    private string findAvailableId(string baseName, string extension)
    {
        string id = baseName + extension;

        for (int suffix = 2; File.Exists(getManagedPath(id)) || Directory.Exists(getManagedPath(id)); suffix++)
            id = $"{baseName} ({suffix}){extension}";

        return id;
    }

    private static string sanitiseName(string value)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        string result = new(value.Select(character => invalid.Contains(character) ? '_' : character).ToArray());
        result = result.Trim().TrimEnd('.');
        return string.IsNullOrWhiteSpace(result) ? "Imported skin" : result;
    }

    private static void copyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);

        foreach (string directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(source, directory);
            Directory.CreateDirectory(Path.Combine(destination, relative));
        }

        foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(source, file);
            string target = Path.Combine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }

    private static void cleanupIncompleteImport(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
        else if (Directory.Exists(path))
            Directory.Delete(path, true);
    }

    private void migrateLegacySkins()
    {
        string legacyPath = storage.GetFullPath(legacy_skins_directory, false);

        if (!Directory.Exists(legacyPath)
            || Path.GetFullPath(legacyPath).Equals(
                Path.GetFullPath(libraryPath),
                StringComparison.OrdinalIgnoreCase))
            return;

        foreach (string source in Directory.EnumerateFileSystemEntries(legacyPath))
        {
            string destination = Path.Combine(libraryPath, Path.GetFileName(source));

            if (File.Exists(destination) || Directory.Exists(destination))
                continue;

            if (File.Exists(source))
                File.Move(source, destination);
            else
                Directory.Move(source, destination);
        }

        if (!Directory.EnumerateFileSystemEntries(legacyPath).Any())
            Directory.Delete(legacyPath);
    }

    private void ensureInitialised()
    {
        if (settings == null || libraryPath == null)
            throw new InvalidOperationException("The skin library has not been initialised.");
    }
}
