using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.IO.Stores;
using osu.Framework.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace Yokko.Game.Skinning.OsuMania;

/// <summary>
/// Keeps legacy skin images within the active renderer's texture limits.
/// Some osu! skins use extremely tall hold-body images which are valid image
/// files but cannot be uploaded by Direct3D 11 without resizing.
/// </summary>
internal sealed class ConstrainedTextureResourceStore : IResourceStore<byte[]>
{
    private readonly IResourceStore<byte[]> source;
    private readonly int maximumDimension;

    internal ConstrainedTextureResourceStore(
        IResourceStore<byte[]> source,
        int maximumDimension)
    {
        this.source = source ?? throw new ArgumentNullException(nameof(source));
        this.maximumDimension = Math.Max(1, maximumDimension);
    }

    public byte[] Get(string name) =>
        constrain(name, source.Get(name));

    public async Task<byte[]> GetAsync(
        string name,
        CancellationToken cancellationToken = default) =>
        constrain(
            name,
            await source.GetAsync(name, cancellationToken)
                        .ConfigureAwait(false));

    public IEnumerable<string> GetAvailableResources() =>
        source.GetAvailableResources();

    public void Dispose() =>
        source.Dispose();

    private byte[] constrain(string name, byte[] data)
    {
        if (data == null)
            return null;

        ImageInfo info;

        try
        {
            info = Image.Identify(data);
        }
        catch
        {
            // TextureLoaderStore provides the normal decode error handling.
            return data;
        }

        if (info.Width <= maximumDimension
            && info.Height <= maximumDimension)
            return data;

        using Image image = Image.Load(data);
        image.Mutate(context => context.Resize(new ResizeOptions
        {
            Size = new Size(maximumDimension, maximumDimension),
            Mode = ResizeMode.Max,
        }));

        using var output = new MemoryStream();
        image.SaveAsPng(output);

        Logger.Log(
            $"Resized oversized osu! skin texture '{name}' from "
            + $"{info.Width}x{info.Height} to {image.Width}x{image.Height} "
            + $"for the renderer's {maximumDimension}px texture limit.",
            LoggingTarget.Runtime,
            LogLevel.Important);

        return output.ToArray();
    }
}
