using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osuTK;
using osuTK.Graphics;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Screens.SongSelect;

internal partial class SongSelectAmbientSticker : CompositeDrawable
{
    private readonly Vector2 restingPosition;
    private readonly float restingRotation;
    private readonly float restingAlpha;
    private readonly float travel;
    private readonly float horizontalTravel;
    private readonly float pulseScale;
    private readonly double motionDuration;
    private readonly bool twinkle;

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
        float horizontalTravel,
        float pulseScale,
        double motionDuration,
        bool twinkle = false)
    {
        restingPosition = position;
        restingRotation = rotation;
        restingAlpha = alpha;
        this.travel = travel;
        this.horizontalTravel = horizontalTravel;
        this.pulseScale = pulseScale;
        this.motionDuration = motionDuration;
        this.twinkle = twinkle;

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
        if (horizontalTravel > 0)
        {
            this.Delay(ambientDelay)
                .MoveToX(restingPosition.X + horizontalTravel,
                    motionDuration * 1.55,
                    Easing.InOutSine)
                .Then()
                .MoveToX(restingPosition.X - horizontalTravel,
                    motionDuration * 3.1,
                    Easing.InOutSine)
                .Then()
                .MoveToX(restingPosition.X,
                    motionDuration * 1.55,
                    Easing.InOutSine)
                .Loop();
        }

        double pulseDuration = twinkle
            ? motionDuration * 0.42
            : motionDuration * 0.72;
        this.Delay(ambientDelay + 90)
            .ScaleTo(1 + pulseScale, pulseDuration, Easing.InOutSine)
            .Then()
            .ScaleTo(1, pulseDuration, Easing.InOutSine)
            .Loop(twinkle ? 260 : 520);
        if (twinkle)
        {
            this.Delay(ambientDelay + 90)
                .FadeTo(restingAlpha * 0.48f,
                    pulseDuration,
                    Easing.InOutSine)
                .Then()
                .FadeTo(restingAlpha,
                    pulseDuration,
                    Easing.InOutSine)
                .Loop(260);
        }
    }
}

internal partial class SongSelectAmbientSignal : CompositeDrawable
{
    private readonly Vector2 restingPosition;

    internal Vector2 RestingPosition => restingPosition;

    internal SongSelectAmbientSignal(
        string label,
        Vector2 position,
        float width,
        Color4 colour)
    {
        restingPosition = position;
        Position = position;
        Size = new Vector2(width, 32);
        InternalChildren =
        [
            new SpriteText
            {
                Text = label,
                Font = HomeTypography.Display(8),
                Colour = new Color4(
                    SongSelectTheme.Navy.R,
                    SongSelectTheme.Navy.G,
                    SongSelectTheme.Navy.B,
                    0.58f),
            },
            new HomeMicroLine
            {
                Position = new Vector2(0, 21),
                Width = width,
            },
            new Box
            {
                Position = new Vector2(0, 20),
                Size = new Vector2(MathF.Min(54, width * 0.3f), 2),
                Colour = colour,
                Alpha = 0.64f,
            },
            new Circle
            {
                Anchor = Anchor.BottomRight,
                Origin = Anchor.Centre,
                Position = new Vector2(-1, -10),
                Size = new Vector2(5),
                Colour = colour,
                Alpha = 0.76f,
            },
        ];
    }

    internal void Play(double delay)
    {
        ClearTransforms();
        Alpha = 0;
        Y = restingPosition.Y + 5;
        this.Delay(delay)
            .FadeTo(0.74f, 260, Easing.OutQuint)
            .MoveToY(restingPosition.Y, 300, Easing.OutBack);
    }
}
