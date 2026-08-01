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
    private readonly object syncRoot = new();
    private readonly Dictionary<string, Texture> cachedTextures =
        new(StringComparer.OrdinalIgnoreCase);
    private TextureStore textureStore;
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
                textureStore = new TextureStore(
                    currentRenderer,
                    new TextureLoaderStore(
                        new ConstrainedTextureResourceStore(
                            new ChartArtworkResourceStore(),
                            currentRenderer.MaxTextureSize)),
                    scaleAdjust: 1);
            }
            else if (!ReferenceEquals(renderer, currentRenderer))
            {
                throw new InvalidOperationException(
                    "Song-select artwork cannot be shared across renderers.");
            }

            Texture texture = textureStore.Get(path);
            if (texture != null)
                cachedTextures[path] = texture;
            return texture;
        }
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
            return cachedTextures.TryGetValue(path, out Texture texture)
                   && texture.UploadComplete;
    }

    public void Dispose()
    {
        lock (syncRoot)
        {
            if (disposed)
                return;

            disposed = true;
            textureStore?.Dispose();
            textureStore = null;
            renderer = null;
            cachedTextures.Clear();
        }
    }
}
