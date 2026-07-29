using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osuTK;
using osuTK.Graphics;
using Yokko.Core.Timing;

namespace Yokko.Game.Screens.Gameplay;

internal partial class LegacyManiaBarLine : CompositeDrawable
{
    private readonly double startPosition;
    private readonly float baseHeight;
    private readonly (Box Box, float X, float Width)[] segments;

    public LegacyManiaBarLine(
        double time,
        ScrollVelocityMap velocityMap,
        IReadOnlyList<(float X, float Width)> stageSegments,
        float height,
        Color4 colour)
    {
        StartTime = time;
        startPosition = velocityMap.PositionAt(time);
        baseHeight = height;
        RelativeSizeAxes = Axes.X;
        Height = height;
        Alpha = 0;
        segments = stageSegments
                   .Select(segment =>
                   {
                       var box = new Box
                       {
                           X = segment.X,
                           Width = segment.Width,
                           RelativeSizeAxes = Axes.Y,
                           Colour = colour,
                       };
                       return (box, segment.X, segment.Width);
                   })
                   .ToArray();
        InternalChildren = segments.Select(segment => segment.Box).ToArray();
    }

    public double StartTime { get; }

    public void SetWidthScale(float value)
    {
        value = Math.Max(0.01f, value);

        foreach ((Box box, float x, float width) in segments)
        {
            box.X = x * value;
            box.Width = width * value;
        }
    }

    public void UpdatePosition(
        double gameplayTime,
        float topY,
        float judgementY,
        double approachTime,
        ScrollVelocityMap velocityMap,
        ScrollSpeedFactorMap speedFactorMap)
    {
        double factor = speedFactorMap.FactorAt(gameplayTime);
        double currentPosition = velocityMap.PositionAt(gameplayTime);
        double progress = 1
                          - (startPosition - currentPosition)
                          / approachTime
                          * factor;
        Y = topY + (float)(progress * (judgementY - topY))
            - baseHeight / 2;
        Alpha = progress is
            >= DrawableNote.MinimumVisibleProgress
            and <= DrawableNote.MaximumVisibleProgress
            ? 1
            : 0;
    }
}
