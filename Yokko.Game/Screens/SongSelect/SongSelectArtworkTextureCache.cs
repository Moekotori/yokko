using System;
using System.Collections.Generic;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Textures;
using Yokko.Game.Importing;
using Yokko.Game.Skinning.OsuMania;

namespace Yokko.Game.Screens.SongSelect;

/// <summary>
/// Owns decoded chart artwork for the application lifetime. Song-select
/// screens are short-lived, but their immutable source artwork is shared.
/// Decoding happens outside the cache lock so background loads never stall
/// update-thread lookups of already-cached artwork.
/// </summary>
internal sealed class SongSelectArtworkTextureCache : IDisposable
{
    internal const int Capacity = 24;
    internal const int MaximumThumbnailDimension = 512;
    private const long maximum_thumbnail_pixels =
        (long)MaximumThumbnailDimension * MaximumThumbnailDimension;
    private readonly object syncRoot = new();
    private readonly Dictionary<string, CachedArtwork> cachedTextures =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly LinkedList<string> lru = new();
    private LargeTextureStore textureStore;
    private IRenderer renderer;
    private bool disposed;

    internal Texture Get(string path, IRenderer currentRenderer)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(currentRenderer);

        LargeTextureStore store;

        lock (syncRoot)
        {
            store = ensureStore(currentRenderer);
            if (touchCached(path))
                return store.Get(path);
        }

        // The decode is the expensive part. LargeTextureStore serialises
        // concurrent lookups per key itself, so no cache lock is needed here
        // and cached lookups on other threads stay unblocked meanwhile.
        Texture pin = store.Get(path);
        if (pin == null)
            return null;

        lock (syncRoot)
        {
            if (disposed)
            {
                pin.Dispose();
                return null;
            }

            // LargeTextureStore returns independent reference-counted
            // wrappers. Keep one pin in the LRU and return a separate wrapper
            // to the Sprite. Evicting the pin is therefore safe while a
            // visible drawable still owns the same native texture.
            if (touchCached(path))
            {
                pin.Dispose();
            }
            else
            {
                LinkedListNode<string> node = lru.AddFirst(path);
                cachedTextures[path] = new CachedArtwork(pin, node);
                trimToCapacity();
            }

            return store.Get(path);
        }
    }

    /// <summary>
    /// Returns a wrapper for already-decoded artwork without ever decoding.
    /// Null means the caller should show a placeholder and load off-thread.
    /// </summary>
    internal Texture GetCached(string path, IRenderer currentRenderer)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(currentRenderer);

        lock (syncRoot)
        {
            LargeTextureStore store = ensureStore(currentRenderer);
            return touchCached(path) ? store.Get(path) : null;
        }
    }

    internal void Prewarm(string path, IRenderer currentRenderer)
    {
        Texture reference = Get(path, currentRenderer);
        reference?.Dispose();
    }

    internal int CachedArtworkCount
    {
        get
        {
            lock (syncRoot)
                return cachedTextures.Count;
        }
    }

    internal bool IsCached(string path)
    {
        lock (syncRoot)
            return cachedTextures.ContainsKey(path);
    }

    internal bool IsUploadComplete(string path)
    {
        lock (syncRoot)
            return cachedTextures.TryGetValue(path, out CachedArtwork cached)
                   && cached.Texture.UploadComplete;
    }

    public void Dispose()
    {
        lock (syncRoot)
        {
            if (disposed)
                return;

            disposed = true;
            foreach (CachedArtwork cached in cachedTextures.Values)
                cached.Texture.Dispose();
            textureStore?.Dispose();
            textureStore = null;
            renderer = null;
            cachedTextures.Clear();
            lru.Clear();
        }
    }

    private LargeTextureStore ensureStore(IRenderer currentRenderer)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (textureStore == null)
        {
            renderer = currentRenderer;
            int maximumDimension = Math.Min(
                MaximumThumbnailDimension,
                currentRenderer.MaxTextureSize);
            textureStore = new LargeTextureStore(
                currentRenderer,
                new ConstrainedTextureLoaderStore(
                    new TextureLoaderStore(new ChartArtworkResourceStore()),
                    maximumDimension,
                    maximumPixelCount: Math.Min(
                        maximum_thumbnail_pixels,
                        (long)maximumDimension * maximumDimension)),
                manualMipmaps: false);
        }
        else if (!ReferenceEquals(renderer, currentRenderer))
        {
            throw new InvalidOperationException(
                "Song-select artwork cannot be shared across renderers.");
        }

        return textureStore;
    }

    private bool touchCached(string path)
    {
        if (!cachedTextures.TryGetValue(path, out CachedArtwork cached))
            return false;

        lru.Remove(cached.Node);
        lru.AddFirst(cached.Node);
        return true;
    }

    private void trimToCapacity()
    {
        while (cachedTextures.Count > Capacity)
        {
            LinkedListNode<string> oldest = lru.Last!;
            lru.RemoveLast();
            if (cachedTextures.Remove(oldest.Value, out CachedArtwork cached))
                cached.Texture.Dispose();
        }
    }

    private sealed record CachedArtwork(
        Texture Texture,
        LinkedListNode<string> Node);
}
