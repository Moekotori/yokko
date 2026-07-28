using System;
using System.Collections.Generic;
using System.IO;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Animations;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.PixelFormats;
using Yokko.Resources;

namespace Yokko.Game.Presentation;

/// <summary>
/// Decodes every frame of an embedded GIF and plays it with the source timing.
/// </summary>
internal partial class AnimatedGifSprite : CompositeDrawable
{
    private readonly string resourceName;
    private readonly List<Texture> frameTextures = new();
    private TextureAnimation animation;

    internal int FrameCount => animation?.FrameCount ?? 0;

    public AnimatedGifSprite(string resourceName)
    {
        this.resourceName = resourceName;
    }

    [BackgroundDependencyLoader]
    private void load(IRenderer renderer)
    {
        using var resources = new DllResourceStore(typeof(YokkoResources).Assembly);
        using Stream stream = resources.GetStream(resourceName)
                              ?? throw new InvalidOperationException(
                                  $"Animated GIF resource '{resourceName}' was not found.");
        using Image<Rgba32> gif = Image.Load<Rgba32>(stream);

        InternalChild = animation = new TextureAnimation
        {
            RelativeSizeAxes = Axes.Both,
            FillMode = FillMode.Fit,
        };

        for (int i = 0; i < gif.Frames.Count; i++)
        {
            Image<Rgba32> frameImage = gif.Frames.CloneFrame(i);
            Texture texture = renderer.CreateTexture(
                frameImage.Width,
                frameImage.Height);

            try
            {
                texture.SetData(new TextureUpload(frameImage));
            }
            catch
            {
                frameImage.Dispose();
                texture.Dispose();
                throw;
            }

            frameTextures.Add(texture);

            int sourceDelay = gif.Frames[i]
                                 .Metadata
                                 .GetGifMetadata()
                                 .FrameDelay;
            animation.AddFrame(texture, Math.Max(10, sourceDelay * 10));
        }
    }

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
        {
            foreach (Texture texture in frameTextures)
                texture.Dispose();

            frameTextures.Clear();
        }

        base.Dispose(isDisposing);
    }
}
