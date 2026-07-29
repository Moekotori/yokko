using System;
using System.Collections.Generic;
using System.Linq;
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
            OsuManiaSkinConfiguration configuration = info.GetConfiguration(keys);
            string[] holdBodyTextureNames = configuration.HoldBodyImages
                                                         .Select(assetName =>
                                                             source.ResolveTextureName(assetName).Name)
                                                         .Where(name => name != null)
                                                         .Distinct(StringComparer.OrdinalIgnoreCase)
                                                         .ToArray();
            var constrainedSource = new ConstrainedTextureResourceStore(
                source,
                renderer.MaxTextureSize,
                holdBodyTextureNames);
            var textureStore = new TextureStore(
                renderer,
                new TextureLoaderStore(constrainedSource),
                scaleAdjust: 1);
            return new OsuManiaSkin(path, info, configuration, source, textureStore);
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

        Texture texture = getResolvedTexture(
            resolvedName,
            highResolution,
            repeatVertically);

        textureCache[cacheKey] = texture;
        return texture;
    }

    public IReadOnlyList<Texture> GetAnimationFrames(string assetName)
    {
        IReadOnlyList<(string Name, bool HighResolution)> resolvedFrames =
            source.ResolveAnimationTextureNames(assetName);
        var frames = new List<Texture>(resolvedFrames.Count);

        foreach ((string name, bool highResolution) in resolvedFrames)
        {
            Texture texture = getResolvedTexture(name, highResolution, false);

            if (texture != null)
                frames.Add(texture);
        }

        return frames;
    }

    public string GetHitSamplePath(string lookupName) =>
        source.ResolveAudioPath(lookupName);

    public void Dispose()
    {
        textureStore.Dispose();
    }

    private Texture getResolvedTexture(
        string resolvedName,
        bool highResolution,
        bool repeatVertically)
    {
        string cacheKey = resolvedName
                          + (highResolution ? "\0@2x" : "\0@1x")
                          + (repeatVertically ? "\0repeat-y" : string.Empty);

        if (textureCache.TryGetValue(cacheKey, out Texture cached))
            return cached;

        Texture texture = textureStore.Get(
            resolvedName,
            WrapMode.ClampToEdge,
            repeatVertically ? WrapMode.Repeat : WrapMode.ClampToEdge);

        if (texture != null)
            texture.ScaleAdjust = highResolution ? 2 : 1;

        textureCache[cacheKey] = texture;
        return texture;
    }
}
