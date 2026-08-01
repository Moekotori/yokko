using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osuTK;

namespace Yokko.Game.Screens.SongSelect;

internal static class SongSelectArtworkCrop
{
    internal static Sprite Create(Texture texture, Vector2 frameSize)
    {
        Vector2 sourceSize = texture == null
            ? frameSize
            : new Vector2(texture.DisplayWidth, texture.DisplayHeight);
        return new Sprite
        {
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            Size = CalculateCoverSize(sourceSize, frameSize),
            Texture = texture,
            FillMode = FillMode.Fill,
        };
    }

    internal static Vector2 CalculateCoverSize(
        Vector2 sourceSize,
        Vector2 frameSize)
    {
        if (sourceSize.X <= 0
            || sourceSize.Y <= 0
            || frameSize.X <= 0
            || frameSize.Y <= 0)
        {
            return frameSize;
        }

        float scale = Math.Max(
            frameSize.X / sourceSize.X,
            frameSize.Y / sourceSize.Y);
        return sourceSize * scale;
    }
}
