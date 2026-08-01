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

        lock (syncRoot)
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
                    new TextureLoaderStore(
                        new ConstrainedTextureResourceStore(
                            new ChartArtworkResourceStore(),
                            maximumDimension,
                            maximumPixelCount: Math.Min(
                                maximum_thumbnail_pixels,
                                (long)maximumDimension * maximumDimension))),
                    manualMipmaps: false);
            }
            else if (!ReferenceEquals(renderer, currentRenderer))
            {
                throw new InvalidOperationException(
                    "Song-select artwork cannot be shared across renderers.");
            }

            if (cachedTextures.TryGetValue(path, out CachedArtwork cached))
            {
                lru.Remove(cached.Node);
                lru.AddFirst(cached.Node);
                return textureStore.Get(path);
            }

            // LargeTextureStore returns independent reference-counted wrappers.
            // Keep one pin in the LRU and return a separate wrapper to the
            // Sprite. Evicting the pin is therefore safe while a visible
            // drawable still owns the same native texture.
            Texture pin = textureStore.Get(path);
            if (pin == null)
                return null;

            LinkedListNode<string> node = lru.AddFirst(path);
            cachedTextures[path] = new CachedArtwork(pin, node);
            trimToCapacity();
            return textureStore.Get(path);
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
