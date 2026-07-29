using System;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osuTK;
using osuTK.Graphics;

namespace Yokko.Game.Screens.Gameplay;

/// <summary>
/// Alpha-subtractive note cover matching lazer's
/// PlayfieldCoveringWrapper. The lane and receptor remain visible while only
/// hit objects are hidden.
/// </summary>
internal partial class ManiaNoteVisibilityCover : CompositeDrawable
{
    private readonly Drawable cover;
    private readonly Box gradient;
    private readonly Box filled;

    internal double Coverage { get; private set; }

    internal bool CoversBottom { get; private set; }

    internal ManiaNoteVisibilityCover(Drawable content)
    {
        RelativeSizeAxes = Axes.Both;
        InternalChild = new BufferedContainer
        {
            RelativeSizeAxes = Axes.Both,
            Children = new[]
            {
                content,
                cover = new Container
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    RelativeSizeAxes = Axes.Both,
                    Blending = new BlendingParameters
                    {
                        RGBEquation = BlendingEquation.Add,
                        Source = BlendingType.Zero,
                        Destination = BlendingType.One,
                        AlphaEquation = BlendingEquation.Add,
                        SourceAlpha = BlendingType.Zero,
                        DestinationAlpha =
                            BlendingType.OneMinusSrcAlpha,
                    },
                    Children = new Drawable[]
                    {
                        gradient = new Box
                        {
                            Anchor = Anchor.BottomLeft,
                            Origin = Anchor.BottomLeft,
                            RelativeSizeAxes = Axes.Both,
                            RelativePositionAxes = Axes.Both,
                            Height = 0.25f,
                            Colour = ColourInfo.GradientVertical(
                                Color4.White.Opacity(0),
                                Color4.White),
                        },
                        filled = new Box
                        {
                            Anchor = Anchor.BottomLeft,
                            Origin = Anchor.BottomLeft,
                            RelativeSizeAxes = Axes.Both,
                            Height = 0,
                        },
                    },
                },
            },
        };
    }

    internal void SetCoverage(
        double coverage,
        bool coversBottom)
    {
        coverage = Math.Clamp(coverage, 0, 1);
        Coverage = coverage;
        CoversBottom = coversBottom;
        filled.Height = (float)coverage;
        gradient.Y = -(float)coverage;
        gradient.Alpha = coverage > 0 ? 1 : 0;
        cover.Scale = coversBottom
            ? Vector2.One
            : new Vector2(1, -1);
    }
}
