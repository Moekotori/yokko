using System;
using System.Collections.Generic;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Utils;
using osuTK;
using osuTK.Graphics;
using Yokko.Game.Skinning.OsuMania;

namespace Yokko.Game.Screens.Gameplay;

/// <summary>
/// osu!stable-style mania health display backed by the active skin's
/// scorebar-bg and scorebar-colour assets.
/// </summary>
internal partial class LegacyManiaHealthBar : CompositeDrawable
{
    private const float mania_scale = 0.7f;
    private readonly Container fillClip;
    private readonly LegacyManiaAnimatedSprite fillAnimation;
    private readonly float maximumFillWidth;
    private readonly bool usesMarkerStyle;
    private float targetFillWidth;

    internal LegacyManiaHealthBar(OsuManiaSkin skin)
    {
        Texture background = skin?.GetTexture("scorebar-bg");
        IReadOnlyList<Texture> fillFrames =
            skin?.GetAnimationFrames("scorebar-colour")
            ?? Array.Empty<Texture>();
        Texture firstFill = fillFrames.Count > 0
            ? fillFrames[0]
            : null;

        if (background == null || firstFill == null)
            return;

        // scorebar-marker itself is hidden in osu!mania, but its presence
        // still selects the newer fill offset and critical-health tinting.
        usesMarkerStyle = skin.GetTexture("scorebar-marker") != null;
        float coordinateScale =
            OsuManiaSkinConfiguration.LegacyPositionScaleFactor;
        Vector2 backgroundSize = new(
            background.DisplayWidth / coordinateScale,
            background.DisplayHeight / coordinateScale);
        Vector2 fillSize = new(
            firstFill.DisplayWidth / coordinateScale,
            firstFill.DisplayHeight / coordinateScale);
        double frameDuration = skin.Info.AnimationFrameRate > 0
            ? 1000.0 / skin.Info.AnimationFrameRate
            : 1000.0 / fillFrames.Count;
        fillAnimation = new LegacyManiaAnimatedSprite(
            fillFrames,
            frameDuration)
        {
            Size = fillSize,
        };

        Size = backgroundSize;
        Rotation = -90;
        Scale = new Vector2(mania_scale);
        maximumFillWidth = fillSize.X;
        targetFillWidth = maximumFillWidth;
        InternalChildren = new Drawable[]
        {
            new Sprite
            {
                Size = backgroundSize,
                Texture = background,
            },
            fillClip = new Container
            {
                Position = (usesMarkerStyle
                    ? new Vector2(12, 12)
                    : new Vector2(5, 16)) / coordinateScale,
                Size = fillSize,
                Masking = true,
                Child = fillAnimation,
            },
        };
        IsAvailable = true;
    }

    internal bool IsAvailable { get; }

    internal float FillFraction =>
        maximumFillWidth <= 0
            ? 0
            : fillClip.Width / maximumFillWidth;

    internal float TargetFillFraction =>
        maximumFillWidth <= 0
            ? 0
            : targetFillWidth / maximumFillWidth;

    internal int AnimationFrameCount =>
        fillAnimation?.FrameCount ?? 0;

    internal Vector2 FillPosition =>
        fillClip?.Position ?? Vector2.Zero;

    internal Color4 FillColour =>
        fillAnimation?.Colour ?? Color4.White;

    internal bool UsesMarkerStyle => usesMarkerStyle;

    internal void SetHealth(double health)
    {
        health = Math.Clamp(health, 0, 1);
        targetFillWidth =
            maximumFillWidth * (float)health;
        if (usesMarkerStyle)
            fillAnimation.Colour = getMarkerStyleFillColour(health);
    }

    internal void SetPlayfieldWidthScale(float widthScale)
    {
        widthScale = Math.Max(0.01f, widthScale);
        Scale = new Vector2(
            mania_scale,
            mania_scale * widthScale);
    }

    protected override void Update()
    {
        base.Update();

        if (!IsAvailable)
            return;

        fillClip.Width = Interpolation.ValueAt(
            Math.Clamp(Clock.ElapsedFrameTime, 0, 200),
            fillClip.Width,
            targetFillWidth,
            0,
            200,
            Easing.OutQuint);
    }

    private static Color4 getMarkerStyleFillColour(double health)
    {
        if (health < 0.2)
            return interpolateSrgb(
                0.2 - health,
                Color4.Black,
                Color4.Red,
                0.2);

        if (health < 0.5)
            return interpolateSrgb(
                0.5 - health,
                Color4.White,
                Color4.Black,
                0.5);

        return Color4.White;
    }

    private static Color4 interpolateSrgb(
        double value,
        Color4 start,
        Color4 end,
        double duration)
    {
        float amount = duration <= 0
            ? 0
            : (float)Math.Clamp(value / duration, 0, 1);
        return new Color4(
            start.R + amount * (end.R - start.R),
            start.G + amount * (end.G - start.G),
            start.B + amount * (end.B - start.B),
            start.A + amount * (end.A - start.A));
    }
}
