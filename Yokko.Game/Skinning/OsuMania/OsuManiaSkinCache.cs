using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using osu.Framework.Graphics.Rendering;

namespace Yokko.Game.Skinning.OsuMania;

/// <summary>
/// Retains immutable, renderer-backed skin resources between gameplay screens.
/// A gameplay screen only leases a skin; the cache owns its texture store.
/// </summary>
internal sealed class OsuManiaSkinCache : IDisposable
{
    private const int maximum_retained_skins = 8;

    private readonly object syncRoot = new();
    private readonly Dictionary<SkinCacheKey, CacheEntry> entries = [];
    private long useSequence;
    private bool disposed;

    internal OsuManiaSkinLease Acquire(
        string path,
        int keys,
        IRenderer renderer,
        int stageCount = 1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(renderer);

        SkinCacheKey key = createKey(
            path,
            keys,
            stageCount,
            renderer.MaxTextureSize);
        CacheEntry entry;

        lock (syncRoot)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            removeSupersededEntries(key);

            if (!entries.TryGetValue(key, out entry))
            {
                entry = new CacheEntry(new Lazy<OsuManiaSkin>(
                    () => OsuManiaSkin.Load(
                        path,
                        keys,
                        renderer,
                        stageCount),
                    LazyThreadSafetyMode.ExecutionAndPublication));
                entries.Add(key, entry);
            }

            entry.ReferenceCount++;
            entry.LastUse = ++useSequence;
        }

        try
        {
            return new OsuManiaSkinLease(
                entry.Skin.Value,
                () => release(entry));
        }
        catch
        {
            lock (syncRoot)
            {
                entry.ReferenceCount--;
                entries.Remove(key);
            }

            throw;
        }
    }

    internal int RetainedCount
    {
        get
        {
            lock (syncRoot)
                return entries.Count;
        }
    }

    public void Dispose()
    {
        CacheEntry[] retained;

        lock (syncRoot)
        {
            if (disposed)
                return;

            disposed = true;
            retained = entries.Values.ToArray();
            entries.Clear();
        }

        foreach (CacheEntry entry in retained)
        {
            if (entry.Skin.IsValueCreated)
                entry.Skin.Value.Dispose();
        }
    }

    private void release(CacheEntry entry)
    {
        List<OsuManiaSkin> evicted = [];

        lock (syncRoot)
        {
            if (entry.ReferenceCount > 0)
                entry.ReferenceCount--;

            while (entries.Count > maximum_retained_skins)
            {
                KeyValuePair<SkinCacheKey, CacheEntry> candidate = entries
                    .Where(pair => pair.Value.ReferenceCount == 0)
                    .OrderBy(pair => pair.Value.LastUse)
                    .FirstOrDefault();
                if (candidate.Value == null)
                    break;

                entries.Remove(candidate.Key);
                if (candidate.Value.Skin.IsValueCreated)
                    evicted.Add(candidate.Value.Skin.Value);
            }
        }

        foreach (OsuManiaSkin skin in evicted)
            skin.Dispose();
    }

    private void removeSupersededEntries(SkinCacheKey requested)
    {
        SkinCacheKey[] superseded = entries
            .Where(pair =>
                pair.Value.ReferenceCount == 0
                && pair.Key.Path == requested.Path
                && pair.Key.Keys == requested.Keys
                && pair.Key.StageCount == requested.StageCount
                && pair.Key.MaximumTextureSize == requested.MaximumTextureSize
                && pair.Key != requested)
            .Select(pair => pair.Key)
            .ToArray();

        foreach (SkinCacheKey key in superseded)
        {
            CacheEntry entry = entries[key];
            entries.Remove(key);
            if (entry.Skin.IsValueCreated)
                entry.Skin.Value.Dispose();
        }
    }

    private static SkinCacheKey createKey(
        string path,
        int keys,
        int stageCount,
        int maximumTextureSize)
    {
        string fullPath = Path.GetFullPath(path)
                              .TrimEnd(
                                  Path.DirectorySeparatorChar,
                                  Path.AltDirectorySeparatorChar)
                              .ToUpperInvariant();
        FileSystemInfo source = File.Exists(fullPath)
            ? new FileInfo(fullPath)
            : new DirectoryInfo(fullPath);
        long length = source is FileInfo file ? file.Length : 0;

        return new SkinCacheKey(
            fullPath,
            source.LastWriteTimeUtc.Ticks,
            length,
            keys,
            stageCount,
            maximumTextureSize);
    }

    private sealed class CacheEntry(Lazy<OsuManiaSkin> skin)
    {
        internal Lazy<OsuManiaSkin> Skin { get; } = skin;
        internal int ReferenceCount { get; set; }
        internal long LastUse { get; set; }
    }

    private readonly record struct SkinCacheKey(
        string Path,
        long LastWriteTicks,
        long Length,
        int Keys,
        int StageCount,
        int MaximumTextureSize);
}

internal sealed class OsuManiaSkinLease : IDisposable
{
    private Action release;

    internal OsuManiaSkinLease(OsuManiaSkin skin, Action release)
    {
        Skin = skin ?? throw new ArgumentNullException(nameof(skin));
        this.release = release ?? throw new ArgumentNullException(nameof(release));
    }

    internal OsuManiaSkin Skin { get; }

    public void Dispose() => Interlocked.Exchange(ref release, null)?.Invoke();
}
