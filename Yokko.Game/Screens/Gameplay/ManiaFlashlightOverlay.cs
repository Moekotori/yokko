using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Utils;
using osuTK.Graphics;

namespace Yokko.Game.Screens.Gameplay;

/// <summary>
/// Rectangular full-width mania flashlight with a soft vertical falloff.
/// </summary>
internal partial class ManiaFlashlightOverlay : CompositeDrawable
{
    private const float fadeHeight = 36;

    private readonly Box topFill;
    private readonly Box topFade;
    private readonly Box bottomFade;
    private readonly Box bottomFill;
    private double targetWindowSize;
    private double displayedWindowSize;

    internal double WindowSize => targetWindowSize;

    internal ManiaFlashlightOverlay(double initialWindowSize)
    {
        RelativeSizeAxes = Axes.Both;
        targetWindowSize = displayedWindowSize = initialWindowSize;
        InternalChildren = new Drawable[]
        {
            topFill = blackBox(),
            topFade = blackBox(),
            bottomFade = blackBox(),
            bottomFill = blackBox(),
        };
    }

    internal void SetWindowSize(double size)
        => targetWindowSize = Math.Max(1, size);

    protected override void Update()
    {
        base.Update();
        displayedWindowSize = Interpolation.DampContinuously(
            displayedWindowSize,
            targetWindowSize,
            8,
            Math.Abs(Time.Elapsed));
        updateGeometry();
    }

    private void updateGeometry()
    {
        float halfWindow =
            (float)Math.Min(DrawHeight / 2, displayedWindowSize / 2);
        float centre = DrawHeight / 2;
        float topFadeStart = Math.Max(
            0,
            centre - halfWindow - fadeHeight);
        float bottomFadeStart = Math.Min(
            DrawHeight,
            centre + halfWindow);
        float actualTopFadeHeight = Math.Min(
            fadeHeight,
            Math.Max(0, centre - halfWindow));
        float actualBottomFadeHeight = Math.Min(
            fadeHeight,
            Math.Max(0, DrawHeight - bottomFadeStart));

        topFill.Position = new osuTK.Vector2(0, 0);
        topFill.Height = topFadeStart;

        topFade.Y = topFadeStart;
        topFade.Height = actualTopFadeHeight;
        topFade.Colour = ColourInfo.GradientVertical(
            Color4.Black,
            new Color4(0, 0, 0, 0));

        bottomFade.Y = bottomFadeStart;
        bottomFade.Height = actualBottomFadeHeight;
        bottomFade.Colour = ColourInfo.GradientVertical(
            new Color4(0, 0, 0, 0),
            Color4.Black);

        bottomFill.Y = bottomFadeStart + actualBottomFadeHeight;
        bottomFill.Height = Math.Max(0, DrawHeight - bottomFill.Y);
    }

    private static Box blackBox() => new()
    {
        RelativeSizeAxes = Axes.X,
        Colour = Color4.Black,
    };
}
