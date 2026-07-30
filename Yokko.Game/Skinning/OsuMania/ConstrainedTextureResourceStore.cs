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

internal enum OversizedLongNoteBodyMode
{
    Resize,
    CropStart,
    CropCentre,
    CropEnd,
}

/// <summary>
/// Keeps legacy skin images within the active renderer's texture limits.
/// Some osu! skins use extremely tall hold-body images which are valid image
/// files but cannot be uploaded by Direct3D 11 without resizing.
/// </summary>
internal sealed class ConstrainedTextureResourceStore : IResourceStore<byte[]>
{
    private readonly IResourceStore<byte[]> source;
    private readonly int maximumDimension;
    private readonly IReadOnlyDictionary<string, OversizedLongNoteBodyMode>
        longNoteBodyModes;

    internal ConstrainedTextureResourceStore(
        IResourceStore<byte[]> source,
        int maximumDimension,
        IReadOnlyDictionary<string, OversizedLongNoteBodyMode>
            longNoteBodyModes = null)
    {
        this.source = source ?? throw new ArgumentNullException(nameof(source));
        this.maximumDimension = Math.Max(1, maximumDimension);
        this.longNoteBodyModes =
            longNoteBodyModes
            ?? new Dictionary<string, OversizedLongNoteBodyMode>(
                StringComparer.OrdinalIgnoreCase);
    }

    public byte[] Get(string name) =>
        constrain(name, source.Get(name));

    public Stream GetStream(string name)
    {
        byte[] data = Get(name);
        return data == null ? null : new MemoryStream(data, writable: false);
    }

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
        OversizedLongNoteBodyMode bodyMode =
            OversizedLongNoteBodyMode.Resize;
        bool isLongNoteBody =
            info.Width <= maximumDimension
            && info.Height > maximumDimension
            && longNoteBodyModes.TryGetValue(
                name,
                out bodyMode);

        if (isLongNoteBody
            && bodyMode != OversizedLongNoteBodyMode.Resize)
        {
            int sourceY = bodyMode switch
            {
                OversizedLongNoteBodyMode.CropEnd =>
                    info.Height - maximumDimension,
                OversizedLongNoteBodyMode.CropCentre =>
                    (info.Height - maximumDimension) / 2,
                _ => 0,
            };
            image.Mutate(context => context.Crop(new Rectangle(
                0,
                sourceY,
                info.Width,
                maximumDimension)));
        }
        else
        {
            var targetSize = isLongNoteBody
                ? new Size(info.Width, maximumDimension)
                : new Size(maximumDimension, maximumDimension);
            image.Mutate(context => context.Resize(new ResizeOptions
            {
                Size = targetSize,
                Mode = isLongNoteBody
                    ? ResizeMode.Stretch
                    : ResizeMode.Max,
            }));
        }

        using var output = new MemoryStream();
        image.SaveAsPng(output);

        Logger.Log(
            $"{(isLongNoteBody && bodyMode != OversizedLongNoteBodyMode.Resize ? "Cropped" : "Resized")} oversized osu! skin texture '{name}' from "
            + $"{info.Width}x{info.Height} to {image.Width}x{image.Height} "
            + $"for the renderer's {maximumDimension}px texture limit"
            + (isLongNoteBody
                ? " while preserving long-note body pixels."
                : "."),
            LoggingTarget.Runtime,
            LogLevel.Important);

        return output.ToArray();
    }
}
