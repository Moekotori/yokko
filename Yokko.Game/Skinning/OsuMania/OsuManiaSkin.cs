using System;
using System.Collections.Generic;
using System.IO;
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

    public Texture GetTexture(string assetName)
    {
        if (string.IsNullOrWhiteSpace(assetName))
            return null;

        if (textureCache.TryGetValue(assetName, out Texture cached))
            return cached;

        (string resolvedName, bool highResolution) = resolveTextureName(assetName);

        if (resolvedName == null)
        {
            textureCache[assetName] = null;
            return null;
        }

        Texture texture = textureStore.Get(resolvedName);

        if (texture != null)
            texture.ScaleAdjust = highResolution ? 2 : 1;

        textureCache[assetName] = texture;
        return texture;
    }

    public void Dispose()
    {
        textureStore.Dispose();
    }

    private (string Name, bool HighResolution) resolveTextureName(string assetName)
    {
        string normalized = assetName.Trim().Replace('\\', '/');
        string extension = Path.GetExtension(normalized);
        string withoutExtension = extension.Length > 0 ? normalized[..^extension.Length] : normalized;
        string[] extensions = extension.Length > 0 ? [extension] : [".png", ".jpg"];

        foreach (string candidateExtension in extensions)
        {
            string highResolution = withoutExtension + "@2x" + candidateExtension;

            if (source.Contains(highResolution))
                return (highResolution, true);

            string animatedHighResolution = withoutExtension + "-0@2x" + candidateExtension;

            if (source.Contains(animatedHighResolution))
                return (animatedHighResolution, true);

            string standard = withoutExtension + candidateExtension;

            if (source.Contains(standard))
                return (standard, false);

            string animatedStandard = withoutExtension + "-0" + candidateExtension;

            if (source.Contains(animatedStandard))
                return (animatedStandard, false);
        }

        return (null, false);
    }
}
