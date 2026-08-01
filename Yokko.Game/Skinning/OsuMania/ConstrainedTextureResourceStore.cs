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
    private readonly long maximumPixelCount;
    private readonly long maximumLongNoteBodyPixelCount;
    private readonly IReadOnlyDictionary<string, OversizedLongNoteBodyMode>
        longNoteBodyModes;

    internal ConstrainedTextureResourceStore(
        IResourceStore<byte[]> source,
        int maximumDimension,
        IReadOnlyDictionary<string, OversizedLongNoteBodyMode>
            longNoteBodyModes = null,
        long maximumPixelCount = long.MaxValue,
        long maximumLongNoteBodyPixelCount = long.MaxValue)
    {
        this.source = source ?? throw new ArgumentNullException(nameof(source));
        this.maximumDimension = Math.Max(1, maximumDimension);
        this.maximumPixelCount = Math.Max(1, maximumPixelCount);
        this.maximumLongNoteBodyPixelCount = Math.Max(
            1,
            maximumLongNoteBodyPixelCount);
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

        OversizedLongNoteBodyMode bodyMode =
            OversizedLongNoteBodyMode.Resize;
        bool isLongNoteBody = longNoteBodyModes.TryGetValue(
            name,
            out bodyMode);
        long pixelLimit = isLongNoteBody
            ? Math.Min(
                maximumPixelCount,
                maximumLongNoteBodyPixelCount)
            : maximumPixelCount;
        long sourcePixelCount = (long)info.Width * info.Height;

        if (info.Width <= maximumDimension
            && info.Height <= maximumDimension
            && sourcePixelCount <= pixelLimit)
        {
            return data;
        }

        using Image image = Image.Load(data);
        int targetWidth;
        int targetHeight;

        if (isLongNoteBody && info.Width <= maximumDimension)
        {
            targetWidth = info.Width;
            targetHeight = Math.Min(
                maximumDimension,
                Math.Max(1, (int)Math.Min(
                    int.MaxValue,
                    pixelLimit / Math.Max(1, targetWidth))));
        }
        else
        {
            double scale = Math.Min(
                1,
                Math.Min(
                    maximumDimension / (double)Math.Max(1, info.Width),
                    maximumDimension / (double)Math.Max(1, info.Height)));
            if (sourcePixelCount > pixelLimit)
            {
                scale = Math.Min(
                    scale,
                    Math.Sqrt(pixelLimit / (double)sourcePixelCount));
            }

            targetWidth = Math.Max(1, (int)Math.Floor(info.Width * scale));
            targetHeight = Math.Max(1, (int)Math.Floor(info.Height * scale));
        }

        if (isLongNoteBody
            && bodyMode != OversizedLongNoteBodyMode.Resize)
        {
            int sourceY = bodyMode switch
            {
                OversizedLongNoteBodyMode.CropEnd =>
                    info.Height - targetHeight,
                OversizedLongNoteBodyMode.CropCentre =>
                    (info.Height - targetHeight) / 2,
                _ => 0,
            };
            image.Mutate(context => context.Crop(new Rectangle(
                0,
                sourceY,
                info.Width,
                targetHeight)));
        }
        else
        {
            image.Mutate(context => context.Resize(new ResizeOptions
            {
                Size = new Size(targetWidth, targetHeight),
                Mode = ResizeMode.Stretch,
            }));
        }

        using var output = new MemoryStream();
        image.SaveAsPng(output);

        Logger.Log(
            $"{(isLongNoteBody && bodyMode != OversizedLongNoteBodyMode.Resize ? "Cropped" : "Resized")} oversized osu! skin texture '{name}' from "
            + $"{info.Width}x{info.Height} to {image.Width}x{image.Height} "
            + $"for the renderer's {maximumDimension}px / {pixelLimit} pixel texture limits"
            + (isLongNoteBody
                ? " while preserving long-note body pixels."
                : "."),
            LoggingTarget.Runtime,
            LogLevel.Important);

        return output.ToArray();
    }
}
