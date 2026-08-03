using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osuTK;

namespace Yokko.Game.Screens.SongSelect;

internal partial class SongSelectAmbientSticker : CompositeDrawable
{
    private readonly Vector2 restingPosition;
    private readonly float restingRotation;
    private readonly float restingAlpha;
    private readonly float travel;
    private readonly double motionDuration;

    internal Vector2 RestingPosition => restingPosition;
    internal float Travel => travel;
    internal double MotionDuration => motionDuration;

    internal SongSelectAmbientSticker(
        Texture texture,
        Vector2 position,
        float size,
        float rotation,
        float alpha,
        float travel,
        double motionDuration)
    {
        restingPosition = position;
        restingRotation = rotation;
        restingAlpha = alpha;
        this.travel = travel;
        this.motionDuration = motionDuration;

        Origin = Anchor.Centre;
        Position = position;
        Size = new Vector2(size);
        Rotation = rotation;
        Alpha = alpha;
        InternalChild = new Sprite
        {
            RelativeSizeAxes = Axes.Both,
            Texture = texture,
            FillMode = FillMode.Fit,
        };
    }

    internal void Play(double delay)
    {
        ClearTransforms();
        Position = restingPosition + new Vector2(0, 6);
        Rotation = restingRotation - 2;
        Scale = new Vector2(0.78f);
        Alpha = 0;

        this.Delay(delay)
            .FadeTo(restingAlpha, 240, Easing.OutQuint);
        this.Delay(delay)
            .ScaleTo(1, 300, Easing.OutBack);
        this.Delay(delay)
            .MoveToY(restingPosition.Y, 300, Easing.OutBack);

        double ambientDelay = delay + 300;
        this.Delay(ambientDelay)
            .MoveToY(restingPosition.Y - travel, motionDuration,
                Easing.InOutSine)
            .Then()
            .MoveToY(restingPosition.Y + travel, motionDuration * 2,
                Easing.InOutSine)
            .Then()
            .MoveToY(restingPosition.Y, motionDuration,
                Easing.InOutSine)
            .Loop();
        this.Delay(ambientDelay)
            .RotateTo(restingRotation + 2.2f, motionDuration * 1.3,
                Easing.InOutSine)
            .Then()
            .RotateTo(restingRotation - 2.2f, motionDuration * 2.6,
                Easing.InOutSine)
            .Then()
            .RotateTo(restingRotation, motionDuration * 1.3,
                Easing.InOutSine)
            .Loop();
    }
}
