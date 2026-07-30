using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.IO.Stores;

namespace Yokko.Game.Skinning.OsuMania;

internal sealed class OsuManiaSkinSource : IResourceStore<byte[]>
{
    private const long max_resource_bytes = 64 * 1024 * 1024;
    private static readonly object audioCacheLock = new();

    private readonly Dictionary<string, string> filePaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ZipArchiveEntry> archiveEntries = new(StringComparer.OrdinalIgnoreCase);
    private readonly FileStream archiveStream;
    private readonly ZipArchive archive;
    private readonly string audioCacheRoot;
    private readonly object archiveLock = new();

    public bool UsesLatestVersion { get; }

    public OsuManiaSkinSource(string path)
    {
        if (Directory.Exists(path))
        {
            string root = findSkinRoot(path);
            UsesLatestVersion = Path.GetFileName(
                    Path.TrimEndingDirectorySeparator(root))
                .Equals("User", StringComparison.OrdinalIgnoreCase);

            foreach (string file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                string relativePath = normalize(Path.GetRelativePath(root, file));

                if (isSafe(relativePath))
                    filePaths[relativePath] = file;
            }

            return;
        }

        if (!File.Exists(path))
            throw new FileNotFoundException("The osu! skin path does not exist.", path);

        if (Path.GetFileName(path).Equals("skin.ini", StringComparison.OrdinalIgnoreCase))
        {
            string root = Path.GetDirectoryName(Path.GetFullPath(path));
            UsesLatestVersion = Path.GetFileName(
                    Path.TrimEndingDirectorySeparator(root))
                .Equals("User", StringComparison.OrdinalIgnoreCase);

            foreach (string file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                string relativePath = normalize(Path.GetRelativePath(root, file));

                if (isSafe(relativePath))
                    filePaths[relativePath] = file;
            }

            return;
        }

        archiveStream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        archive = new ZipArchive(archiveStream, ZipArchiveMode.Read);
        audioCacheRoot = createAudioCacheRoot(path);
        string prefix = findArchivePrefix(archive.Entries);

        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name))
                continue;

            string fullName = normalize(entry.FullName);

            if (prefix.Length > 0)
            {
                if (!fullName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    continue;

                fullName = fullName[prefix.Length..];
            }

            if (isSafe(fullName))
                archiveEntries[fullName] = entry;
        }
    }

    public string ReadSkinIni()
    {
        using Stream stream = GetStream("skin.ini");

        if (stream == null)
            return string.Empty;

        using var reader = new StreamReader(stream, Encoding.UTF8, true);
        return reader.ReadToEnd();
    }

    public bool Contains(string name)
    {
        string normalized = normalize(name);
        return filePaths.ContainsKey(normalized) || archiveEntries.ContainsKey(normalized);
    }

    public bool HasManiaAssets() =>
        GetAvailableResources().Any(isManiaResource);

    public bool HasSupportedSkinAssets() =>
        GetAvailableResources().Any(isSupportedSkinResource);

    public (string Name, bool HighResolution) ResolveTextureName(string assetName)
    {
        if (string.IsNullOrWhiteSpace(assetName))
            return (null, false);

        string normalized = normalize(assetName.Trim());
        string extension = Path.GetExtension(normalized);
        string withoutExtension = extension.Length > 0 ? normalized[..^extension.Length] : normalized;
        withoutExtension = stripHighResolutionSuffix(withoutExtension);
        string[] extensions = extension.Length > 0 ? [extension] : [".png", ".jpg", ".jpeg"];

        foreach (string candidateExtension in extensions)
        {
            string highResolution = withoutExtension + "@2x" + candidateExtension;

            if (Contains(highResolution))
                return (highResolution, true);

            string animatedHighResolution = withoutExtension + "-0@2x" + candidateExtension;

            if (Contains(animatedHighResolution))
                return (animatedHighResolution, true);

            string standard = withoutExtension + candidateExtension;

            if (Contains(standard))
                return (standard, false);

            string animatedStandard = withoutExtension + "-0" + candidateExtension;

            if (Contains(animatedStandard))
                return (animatedStandard, false);
        }

        return (null, false);
    }

    public IReadOnlyList<(string Name, bool HighResolution)> ResolveAnimationTextureNames(
        string assetName)
    {
        if (string.IsNullOrWhiteSpace(assetName))
            return Array.Empty<(string, bool)>();

        string normalized = normalize(assetName.Trim());
        string extension = Path.GetExtension(normalized);
        string withoutExtension = extension.Length > 0
            ? normalized[..^extension.Length]
            : normalized;
        withoutExtension = stripHighResolutionSuffix(withoutExtension);
        string[] extensions = extension.Length > 0
            ? [extension]
            : [".png", ".jpg", ".jpeg"];

        foreach (string candidateExtension in extensions)
        {
            IReadOnlyList<(string, bool)> frames = animationFrames(
                withoutExtension,
                candidateExtension);

            if (frames.Count > 0)
                return frames;
        }

        (string name, bool highResolutionFallback) = ResolveTextureName(assetName);
        return name == null
            ? Array.Empty<(string, bool)>()
            : [(name, highResolutionFallback)];
    }

    public string ResolveAudioPath(string assetName)
    {
        if (string.IsNullOrWhiteSpace(assetName))
            return null;

        string normalized = normalize(assetName.Trim());
        string extension = Path.GetExtension(normalized);
        string withoutExtension = extension.Length > 0
            ? normalized[..^extension.Length]
            : normalized;
        string[] extensions = extension.Length > 0
            ? [extension]
            : [".wav", ".ogg", ".mp3"];

        foreach (string candidateExtension in extensions)
        {
            string candidate = withoutExtension + candidateExtension;

            if (filePaths.TryGetValue(candidate, out string filePath))
                return filePath;

            if (!archiveEntries.ContainsKey(candidate))
                continue;

            return materializeAudio(candidate);
        }

        return null;
    }

    public byte[] Get(string name)
    {
        using Stream stream = GetStream(name);

        if (stream == null)
            return null;

        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    public Task<byte[]> GetAsync(string name, CancellationToken cancellationToken = default) =>
        Task.Run(() => Get(name), cancellationToken);

    public Stream GetStream(string name)
    {
        string normalized = normalize(name);

        if (filePaths.TryGetValue(normalized, out string filePath))
        {
            var info = new FileInfo(filePath);
            return info.Length <= max_resource_bytes
                ? File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read)
                : null;
        }

        if (!archiveEntries.TryGetValue(normalized, out ZipArchiveEntry entry) || entry.Length > max_resource_bytes)
            return null;

        lock (archiveLock)
        {
            using Stream input = entry.Open();
            var memory = new MemoryStream((int)entry.Length);
            input.CopyTo(memory);
            memory.Position = 0;
            return memory;
        }
    }

    public IEnumerable<string> GetAvailableResources() => filePaths.Keys.Concat(archiveEntries.Keys).ToArray();

    public void Dispose()
    {
        archive?.Dispose();
        archiveStream?.Dispose();
    }

    private string materializeAudio(string name)
    {
        if (string.IsNullOrWhiteSpace(audioCacheRoot))
            return null;

        string destination = Path.Combine(
            audioCacheRoot,
            name.Replace('/', Path.DirectorySeparatorChar));

        lock (audioCacheLock)
        {
            if (File.Exists(destination))
                return destination;

            byte[] contents = Get(name);
            if (contents == null)
                return null;

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.WriteAllBytes(destination, contents);
                return destination;
            }
            catch
            {
                return null;
            }
        }
    }

    private static string createAudioCacheRoot(string archivePath)
    {
        var info = new FileInfo(Path.GetFullPath(archivePath));
        string identity =
            $"{info.FullName}|{info.Length}|{info.LastWriteTimeUtc.Ticks}";
        string fingerprint = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
        return Path.Combine(
            Path.GetTempPath(),
            "Yokko",
            "skin-hitsounds-v1",
            fingerprint);
    }

    private static string findSkinRoot(string path)
    {
        string nestedSkinIni = Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)
                                        .Where(candidate => Path.GetFileName(candidate)
                                                                .Equals("skin.ini", StringComparison.OrdinalIgnoreCase)
                                                            && !isIgnorableArchiveArtifact(
                                                                normalize(Path.GetRelativePath(path, candidate))))
                                        .OrderBy(candidate => candidate.Count(character => character is '\\' or '/'))
                                        .FirstOrDefault();

        if (nestedSkinIni != null)
            return Path.GetDirectoryName(nestedSkinIni);

        string[] files = Directory.EnumerateFiles(
                                      path,
                                      "*",
                                      SearchOption.AllDirectories)
                                  .Where(file => !isIgnorableArchiveArtifact(
                                      normalize(Path.GetRelativePath(path, file))))
                                  .ToArray();
        if (files.Length == 0)
            return Path.GetFullPath(path);

        string[] skinFiles = files.Where(file =>
                                      isSupportedSkinResource(
                                          normalize(Path.GetRelativePath(path, file))))
                                  .ToArray();
        string[] rootCandidates =
            skinFiles.Length > 0 ? skinFiles : files;
        string commonRoot =
            Path.GetDirectoryName(Path.GetFullPath(rootCandidates[0]));

        foreach (string file in rootCandidates.Skip(1))
        {
            string fullPath = Path.GetFullPath(file);

            while (!isWithinDirectory(fullPath, commonRoot))
            {
                string parent = Path.GetDirectoryName(commonRoot);
                if (parent == null)
                    return Path.GetFullPath(path);

                commonRoot = parent;
            }
        }

        string requestedRoot = Path.GetFullPath(path);
        return isWithinDirectory(commonRoot, requestedRoot)
            ? commonRoot
            : requestedRoot;
    }

    private static string findArchivePrefix(IEnumerable<ZipArchiveEntry> entries)
    {
        string[] resourceNames = entries.Where(entry => !string.IsNullOrEmpty(entry.Name))
                                        .Select(entry => normalize(entry.FullName))
                                        .Where(name => !isIgnorableArchiveArtifact(name))
                                        .ToArray();
        string skinIni = resourceNames.Where(name => name.EndsWith(
                                                   "skin.ini",
                                                   StringComparison.OrdinalIgnoreCase))
                                      .OrderBy(name => name.Count(
                                          character => character == '/'))
                                      .FirstOrDefault();

        if (skinIni != null)
        {
            int slash = skinIni.LastIndexOf('/');
            return slash < 0 ? string.Empty : skinIni[..(slash + 1)];
        }

        if (resourceNames.Length == 0)
            return string.Empty;

        string[] skinResources =
            resourceNames.Where(isSupportedSkinResource).ToArray();
        string[] rootCandidates =
            skinResources.Length > 0 ? skinResources : resourceNames;
        string[][] directories = rootCandidates.Select(name =>
                                             {
                                                 string[] segments = name.Split('/');
                                                 return segments.Length <= 1
                                                     ? Array.Empty<string>()
                                                     : segments[..^1];
                                             })
                                             .ToArray();
        int commonLength = directories.Min(parts => parts.Length);

        for (int index = 0; index < commonLength; index++)
        {
            string segment = directories[0][index];

            if (directories.Any(parts => !parts[index].Equals(
                    segment,
                    StringComparison.OrdinalIgnoreCase)))
            {
                commonLength = index;
                break;
            }
        }

        return commonLength == 0
            ? string.Empty
            : string.Join('/', directories[0][..commonLength]) + "/";
    }

    private static string normalize(string path)
    {
        string[] segments = (path ?? string.Empty)
                            .Replace('\\', '/')
                            .Split(
                                '/',
                                StringSplitOptions.RemoveEmptyEntries);
        return string.Join(
            '/',
            segments.Where(segment => segment != "."));
    }

    private IReadOnlyList<(string Name, bool HighResolution)> animationFrames(
        string baseName,
        string extension)
    {
        var frames = new List<(string, bool)>();

        for (int index = 0; index < 1024; index++)
        {
            string highResolution = $"{baseName}-{index}@2x{extension}";
            string standard = $"{baseName}-{index}{extension}";

            if (Contains(highResolution))
                frames.Add((highResolution, true));
            else if (Contains(standard))
                frames.Add((standard, false));
            else
                break;
        }

        return frames;
    }

    private static string stripHighResolutionSuffix(string path) =>
        path.EndsWith("@2x", StringComparison.OrdinalIgnoreCase)
            ? path[..^3]
            : path;

    private static bool isManiaResource(string resource)
    {
        string fileName = Path.GetFileNameWithoutExtension(resource);
        fileName = stripHighResolutionSuffix(fileName);
        int animationSuffix = fileName.LastIndexOf('-');

        if (animationSuffix >= 0
            && int.TryParse(
                fileName[(animationSuffix + 1)..],
                out _))
        {
            fileName = fileName[..animationSuffix];
        }

        return fileName.StartsWith(
                   "mania-key",
                   StringComparison.OrdinalIgnoreCase)
               || fileName.StartsWith(
                   "mania-note",
                   StringComparison.OrdinalIgnoreCase)
               || fileName.StartsWith(
                   "mania-stage-",
                   StringComparison.OrdinalIgnoreCase)
               || fileName.StartsWith(
                   "mania-hit",
                   StringComparison.OrdinalIgnoreCase)
               || fileName.Equals(
                   "lightingL",
                   StringComparison.OrdinalIgnoreCase)
               || fileName.Equals(
                   "lightingN",
                   StringComparison.OrdinalIgnoreCase);
    }

    private static bool isSupportedSkinResource(string resource)
    {
        if (isManiaResource(resource))
            return true;

        string extension = Path.GetExtension(resource);
        string fileName = Path.GetFileNameWithoutExtension(resource);
        fileName = stripHighResolutionSuffix(fileName);
        int animationSuffix = fileName.LastIndexOf('-');

        if (animationSuffix >= 0
            && int.TryParse(fileName[(animationSuffix + 1)..], out _))
        {
            fileName = fileName[..animationSuffix];
        }

        if (fileName.Equals("scorebar-bg", StringComparison.OrdinalIgnoreCase)
            || fileName.Equals("scorebar-colour", StringComparison.OrdinalIgnoreCase)
            || fileName.Equals("scorebar-marker", StringComparison.OrdinalIgnoreCase)
            || fileName.Equals("comboburst-mania", StringComparison.OrdinalIgnoreCase)
            || fileName.Equals("score", StringComparison.OrdinalIgnoreCase))
        {
            return extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
                   || extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
                   || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase);
        }

        if (!extension.Equals(".wav", StringComparison.OrdinalIgnoreCase)
            && !extension.Equals(".ogg", StringComparison.OrdinalIgnoreCase)
            && !extension.Equals(".mp3", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return fileName.StartsWith("normal-hit", StringComparison.OrdinalIgnoreCase)
               || fileName.StartsWith("soft-hit", StringComparison.OrdinalIgnoreCase)
               || fileName.StartsWith("drum-hit", StringComparison.OrdinalIgnoreCase)
               || fileName.StartsWith("normal-slider", StringComparison.OrdinalIgnoreCase)
               || fileName.StartsWith("soft-slider", StringComparison.OrdinalIgnoreCase)
               || fileName.StartsWith("drum-slider", StringComparison.OrdinalIgnoreCase)
               || fileName.StartsWith("hitnormal", StringComparison.OrdinalIgnoreCase)
               || fileName.StartsWith("hitwhistle", StringComparison.OrdinalIgnoreCase)
               || fileName.StartsWith("hitfinish", StringComparison.OrdinalIgnoreCase)
               || fileName.StartsWith("hitclap", StringComparison.OrdinalIgnoreCase)
               || fileName.StartsWith("sliderslide", StringComparison.OrdinalIgnoreCase)
               || fileName.StartsWith("sliderwhistle", StringComparison.OrdinalIgnoreCase);
    }

    private static bool isWithinDirectory(string path, string directory)
    {
        string relative = Path.GetRelativePath(directory, path);
        return relative != ".."
               && !relative.StartsWith(
                   ".." + Path.DirectorySeparatorChar,
                   StringComparison.Ordinal)
               && !Path.IsPathRooted(relative);
    }

    private static bool isIgnorableArchiveArtifact(string path)
    {
        string normalized = normalize(path);
        string fileName = Path.GetFileName(normalized);
        return normalized.Split('/').Any(segment => segment.Equals(
                   "__MACOSX",
                   StringComparison.OrdinalIgnoreCase))
               || fileName.StartsWith("._", StringComparison.OrdinalIgnoreCase)
               || fileName.Equals(".DS_Store", StringComparison.OrdinalIgnoreCase)
               || fileName.Equals("Thumbs.db", StringComparison.OrdinalIgnoreCase);
    }

    private static bool isSafe(string path) =>
        path.Length > 0 &&
        !Path.IsPathRooted(path) &&
        path.Split('/').All(segment => segment is not "." and not "..");
}
