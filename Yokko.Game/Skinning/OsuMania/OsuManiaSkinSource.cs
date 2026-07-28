using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.IO.Stores;

namespace Yokko.Game.Skinning.OsuMania;

internal sealed class OsuManiaSkinSource : IResourceStore<byte[]>
{
    private const long max_resource_bytes = 64 * 1024 * 1024;

    private readonly Dictionary<string, string> filePaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ZipArchiveEntry> archiveEntries = new(StringComparer.OrdinalIgnoreCase);
    private readonly FileStream archiveStream;
    private readonly ZipArchive archive;
    private readonly object archiveLock = new();

    public OsuManiaSkinSource(string path)
    {
        if (Directory.Exists(path))
        {
            string root = findSkinRoot(path);

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

    public (string Name, bool HighResolution) ResolveTextureName(string assetName)
    {
        if (string.IsNullOrWhiteSpace(assetName))
            return (null, false);

        string normalized = normalize(assetName.Trim());
        string extension = Path.GetExtension(normalized);
        string withoutExtension = extension.Length > 0 ? normalized[..^extension.Length] : normalized;
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

    private static string findSkinRoot(string path)
    {
        string nestedSkinIni = Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)
                                        .Where(candidate => Path.GetFileName(candidate)
                                                                .Equals("skin.ini", StringComparison.OrdinalIgnoreCase))
                                        .OrderBy(candidate => candidate.Count(character => character is '\\' or '/'))
                                        .FirstOrDefault();

        return nestedSkinIni == null
            ? Path.GetFullPath(path)
            : Path.GetDirectoryName(nestedSkinIni);
    }

    private static string findArchivePrefix(IEnumerable<ZipArchiveEntry> entries)
    {
        string skinIni = entries.Where(entry => normalize(entry.FullName).EndsWith("skin.ini", StringComparison.OrdinalIgnoreCase))
                                .OrderBy(entry => entry.FullName.Count(character => character is '\\' or '/'))
                                .Select(entry => normalize(entry.FullName))
                                .FirstOrDefault();

        if (skinIni == null)
            return string.Empty;

        int slash = skinIni.LastIndexOf('/');
        return slash < 0 ? string.Empty : skinIni[..(slash + 1)];
    }

    private static string normalize(string path) => (path ?? string.Empty)
                                                     .Replace('\\', '/')
                                                     .TrimStart('/');

    private static bool isSafe(string path) =>
        path.Length > 0 &&
        !Path.IsPathRooted(path) &&
        path.Split('/').All(segment => segment is not "." and not "..");
}
