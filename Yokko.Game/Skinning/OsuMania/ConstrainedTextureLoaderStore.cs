using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Framework.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
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
/// Keeps decoded textures within the active renderer's texture limits.
/// Some osu! skins use extremely tall hold-body images which are valid image
/// files but cannot be uploaded by Direct3D 11 without resizing, and chart
/// artwork is bounded to thumbnail-sized uploads for song select.
/// Constraining happens on the already-decoded <see cref="TextureUpload"/>
/// pixels so oversized images are decoded exactly once, without the previous
/// PNG re-encode/re-decode round trip. The wrapping approach follows
/// ppy/osu osu.Game/Skinning/MaxDimensionLimitedTextureLoaderStore.cs
/// (MIT, master @ 2026-08).
/// </summary>
internal sealed class ConstrainedTextureLoaderStore : IResourceStore<TextureUpload>
{
    private readonly IResourceStore<TextureUpload> source;
    private readonly int maximumDimension;
    private readonly long maximumPixelCount;
    private readonly long maximumLongNoteBodyPixelCount;
    private readonly IReadOnlyDictionary<string, OversizedLongNoteBodyMode>
        longNoteBodyModes;

    internal ConstrainedTextureLoaderStore(
        IResourceStore<TextureUpload> source,
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

    public TextureUpload Get(string name) =>
        constrain(name, source.Get(name));

    public async Task<TextureUpload> GetAsync(
        string name,
        CancellationToken cancellationToken = default) =>
        constrain(
            name,
            await source.GetAsync(name, cancellationToken)
                        .ConfigureAwait(false));

    public Stream GetStream(string name) =>
        source.GetStream(name);

    public IEnumerable<string> GetAvailableResources() =>
        source.GetAvailableResources();

    public void Dispose() =>
        source.Dispose();

    private TextureUpload constrain(string name, TextureUpload upload)
    {
        if (upload == null)
            return null;

        int sourceWidth = upload.Width;
        int sourceHeight = upload.Height;

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
        long sourcePixelCount = (long)sourceWidth * sourceHeight;

        if (sourceWidth <= maximumDimension
            && sourceHeight <= maximumDimension
            && sourcePixelCount <= pixelLimit)
        {
            return upload;
        }

        int targetWidth;
        int targetHeight;

        if (isLongNoteBody && sourceWidth <= maximumDimension)
        {
            targetWidth = sourceWidth;
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
                    maximumDimension / (double)Math.Max(1, sourceWidth),
                    maximumDimension / (double)Math.Max(1, sourceHeight)));
            if (sourcePixelCount > pixelLimit)
            {
                scale = Math.Min(
                    scale,
                    Math.Sqrt(pixelLimit / (double)sourcePixelCount));
            }

            targetWidth = Math.Max(1, (int)Math.Floor(sourceWidth * scale));
            targetHeight = Math.Max(1, (int)Math.Floor(sourceHeight * scale));
        }

        bool crop = isLongNoteBody
                    && bodyMode != OversizedLongNoteBodyMode.Resize;
        Image<Rgba32> constrained;

        if (crop)
        {
            int sourceY = bodyMode switch
            {
                OversizedLongNoteBodyMode.CropEnd =>
                    sourceHeight - targetHeight,
                OversizedLongNoteBodyMode.CropCentre =>
                    (sourceHeight - targetHeight) / 2,
                _ => 0,
            };
            // Rows are contiguous in the upload, so a full-width vertical
            // crop is a plain slice copy without any resampling.
            constrained = Image.LoadPixelData<Rgba32>(
                upload.Data.Slice(
                    sourceY * sourceWidth,
                    targetHeight * sourceWidth),
                sourceWidth,
                targetHeight);
            targetWidth = sourceWidth;
        }
        else
        {
            constrained = Image.LoadPixelData<Rgba32>(
                upload.Data,
                sourceWidth,
                sourceHeight);
            constrained.Mutate(context => context.Resize(new ResizeOptions
            {
                Size = new Size(targetWidth, targetHeight),
                Mode = ResizeMode.Stretch,
            }));
        }

        upload.Dispose();

        Logger.Log(
            $"{(crop ? "Cropped" : "Resized")} oversized texture '{name}' from "
            + $"{sourceWidth}x{sourceHeight} to {targetWidth}x{targetHeight} "
            + $"for the renderer's {maximumDimension}px / {pixelLimit} pixel texture limits"
            + (isLongNoteBody
                ? " while preserving long-note body pixels."
                : "."),
            LoggingTarget.Runtime,
            LogLevel.Important);

        return new TextureUpload(constrained);
    }
}
