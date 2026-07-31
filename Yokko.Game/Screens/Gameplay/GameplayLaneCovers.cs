using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osuTK;
using Yokko.Game.Gameplay;

namespace Yokko.Game.Screens.Gameplay;

/// <summary>
/// Synchronises configured blocker ratios with the rendered playfield. The
/// black blocker surfaces live inside GameplayPlayfield above lane content and
/// below skin combo/judgement feedback, without changing timing or geometry.
/// </summary>
internal partial class GameplayLaneCovers : CompositeDrawable
{
    private readonly GameplayPlayfield playfield;
    private readonly YokkoGameplaySettings settings;
    private float displayedTopHeight;
    private float displayedBottomHeight;

    internal float DisplayedTopHeight => displayedTopHeight;

    internal float DisplayedBottomHeight => displayedBottomHeight;

    public GameplayLaneCovers(
        GameplayPlayfield playfield,
        YokkoGameplaySettings settings)
    {
        this.playfield = playfield;
        this.settings = settings;

        RelativeSizeAxes = Axes.Both;
        Depth = -40;

    }

    protected override void Update()
    {
        base.Update();

        if (DrawWidth <= 0
            || DrawHeight <= 0
            || playfield.DrawWidth <= 0
            || playfield.DrawHeight <= 0)
        {
            return;
        }

        (Vector2 topLeft, Vector2 bottomRight) =
            GameplayLayoutGeometry.BoundsIn(this, playfield);
        float height = Math.Max(0, bottomRight.Y - topLeft.Y);
        double topRatio = Math.Clamp(
            settings.LayoutTopCoverRatio.Value,
            0,
            YokkoGameplaySettings.MaximumTopCoverRatio);
        double bottomRatio = Math.Clamp(
            settings.LayoutBottomCoverRatio.Value,
            0,
            YokkoGameplaySettings.MaximumBottomCoverRatio);

        displayedTopHeight = height * (float)topRatio;
        displayedBottomHeight = height * (float)bottomRatio;
        playfield.SetLayoutCoverRatios(topRatio, bottomRatio);
    }
}

internal static class GameplayLayoutGeometry
{
    public static (Vector2 TopLeft, Vector2 BottomRight) BoundsIn(
        Drawable space,
        Drawable target)
    {
        var quad = target.ScreenSpaceDrawQuad;
        Vector2[] corners =
        {
            space.ToLocalSpace(quad.TopLeft),
            space.ToLocalSpace(quad.TopRight),
            space.ToLocalSpace(quad.BottomLeft),
            space.ToLocalSpace(quad.BottomRight),
        };
        float left = Math.Min(
            Math.Min(corners[0].X, corners[1].X),
            Math.Min(corners[2].X, corners[3].X));
        float top = Math.Min(
            Math.Min(corners[0].Y, corners[1].Y),
            Math.Min(corners[2].Y, corners[3].Y));
        float right = Math.Max(
            Math.Max(corners[0].X, corners[1].X),
            Math.Max(corners[2].X, corners[3].X));
        float bottom = Math.Max(
            Math.Max(corners[0].Y, corners[1].Y),
            Math.Max(corners[2].Y, corners[3].Y));
        return (new Vector2(left, top), new Vector2(right, bottom));
    }
}
