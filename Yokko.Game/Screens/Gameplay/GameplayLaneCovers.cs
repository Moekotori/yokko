using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osuTK;
using osuTK.Graphics;
using Yokko.Game.Gameplay;

namespace Yokko.Game.Screens.Gameplay;

/// <summary>
/// Screen-space covers tied to the rendered playfield. They deliberately live
/// outside GameplayPlayfield so changing their size never changes note timing,
/// judgement coordinates or skin geometry.
/// </summary>
internal partial class GameplayLaneCovers : CompositeDrawable
{
    private readonly GameplayPlayfield playfield;
    private readonly YokkoGameplaySettings settings;
    private readonly Box topCover;
    private readonly Box bottomCover;

    internal float DisplayedTopHeight => topCover.Height;

    internal float DisplayedBottomHeight => bottomCover.Height;

    public GameplayLaneCovers(
        GameplayPlayfield playfield,
        YokkoGameplaySettings settings)
    {
        this.playfield = playfield;
        this.settings = settings;

        RelativeSizeAxes = Axes.Both;
        Depth = -40;

        InternalChildren = new Drawable[]
        {
            topCover = createCover(),
            bottomCover = createCover(),
        };
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
        float width = Math.Max(0, bottomRight.X - topLeft.X);
        float height = Math.Max(0, bottomRight.Y - topLeft.Y);
        float topHeight = height * (float)Math.Clamp(
            settings.LayoutTopCoverRatio.Value,
            0,
            YokkoGameplaySettings.MaximumTopCoverRatio);
        float bottomHeight = height * (float)Math.Clamp(
            settings.LayoutBottomCoverRatio.Value,
            0,
            YokkoGameplaySettings.MaximumBottomCoverRatio);

        topCover.Position = topLeft;
        topCover.Size = new Vector2(width, topHeight);
        topCover.Alpha = topHeight > 0.5f ? 1 : 0;

        bottomCover.Position = new Vector2(
            topLeft.X,
            bottomRight.Y - bottomHeight);
        bottomCover.Size = new Vector2(width, bottomHeight);
        bottomCover.Alpha = bottomHeight > 0.5f ? 1 : 0;
    }

    private static Box createCover() => new()
    {
        Colour = Color4.Black,
    };
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
