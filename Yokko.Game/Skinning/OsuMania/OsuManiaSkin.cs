using System;
using System.Collections.Generic;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Textures;

namespace Yokko.Game.Skinning.OsuMania;

internal sealed class OsuManiaSkin : IDisposable
{
    private readonly OsuManiaSkinSource source;
    private readonly TextureStore textureStore;
    private readonly Dictionary<string, Texture> textureCache = new(StringComparer.OrdinalIgnoreCase);

    private OsuManiaSkin(
        string sourcePath,
        OsuManiaSkinInfo info,
        OsuManiaSkinConfiguration configuration,
        OsuManiaSkinSource source,
        TextureStore textureStore)
    {
        SourcePath = sourcePath;
        Info = info;
        Configuration = configuration;
        this.source = source;
        this.textureStore = textureStore;
    }

    public string SourcePath { get; }

    public OsuManiaSkinInfo Info { get; }

    public OsuManiaSkinConfiguration Configuration { get; }

    public static OsuManiaSkin Load(string path, int keys, IRenderer renderer)
    {
        var source = new OsuManiaSkinSource(path);

        try
        {
            OsuManiaSkinInfo info = OsuManiaSkinIniDecoder.Decode(source.ReadSkinIni());
            var textureStore = new TextureStore(renderer, new TextureLoaderStore(source), scaleAdjust: 1);
            return new OsuManiaSkin(path, info, info.GetConfiguration(keys), source, textureStore);
        }
        catch
        {
            source.Dispose();
            throw;
        }
    }

    public Texture GetTexture(string assetName, bool repeatVertically = false)
    {
        if (string.IsNullOrWhiteSpace(assetName))
            return null;

        string cacheKey = repeatVertically ? assetName + "\0repeat-y" : assetName;

        if (textureCache.TryGetValue(cacheKey, out Texture cached))
            return cached;

        (string resolvedName, bool highResolution) = source.ResolveTextureName(assetName);

        if (resolvedName == null)
        {
            textureCache[cacheKey] = null;
            return null;
        }

        Texture texture = textureStore.Get(
            resolvedName,
            WrapMode.ClampToEdge,
            repeatVertically ? WrapMode.Repeat : WrapMode.ClampToEdge);

        if (texture != null)
            texture.ScaleAdjust = highResolution ? 2 : 1;

        textureCache[cacheKey] = texture;
        return texture;
    }

    public void Dispose()
    {
        textureStore.Dispose();
    }
}
