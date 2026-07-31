using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Screens.Gameplay;

internal partial class GameplayScrollSpeedOverlay : CompositeDrawable
{
    internal static readonly Vector2 ReferenceSize = new(162, 70);
    internal const float PreferredLeft = 156;
    internal const float TopOffset = 113;
    internal const float PlayfieldGap = 22;

    private const float faceWidth = 158;
    private const float faceHeight = 66;
    private const double displayDurationMilliseconds = 1050;
    private const double exitDurationMilliseconds = 170;

    private readonly Container stage;
    private readonly Box underline;
    private readonly Box diamond;
    private readonly SpriteText label;
    private readonly SpriteText speedText;
    private readonly SpriteText detailText;

    internal double DisplayedSpeed { get; private set; }

    internal int DisplayedTimeRangeMilliseconds { get; private set; }

    internal bool IsLocked { get; private set; }

    internal string DisplayedLabel =>
        label.Text.ToString();

    internal string DisplayedDetail =>
        detailText.Text.ToString();

    internal GameplayScrollSpeedOverlay()
    {
        Size = ReferenceSize;

        InternalChild = stage = new Container
        {
            Size = ReferenceSize,
            Children =
            [
                new Container
                {
                    Position = new Vector2(0, 2),
                    Size = new Vector2(faceWidth, faceHeight),
                    Masking = true,
                    CornerRadius = 5,
                    Child = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = HomeControlColours.PaleCyan,
                    },
                },
                new Container
                {
                    Position = new Vector2(2, 0),
                    Size = new Vector2(faceWidth, faceHeight),
                    Masking = true,
                    CornerRadius = 5,
                    BorderThickness = 1,
                    BorderColour = HomeControlColours.Navy,
                    Children =
                    [
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = HomeControlColours.Ivory,
                        },
                        label = new SpriteText
                        {
                            Position = new Vector2(10, 7),
                            Text = "SCROLL SPEED",
                            Font = HomeTypography.Display(8),
                            Spacing = new Vector2(0.65f, 0),
                            Colour = HomeControlColours.Navy,
                        },
                        speedText = new SpriteText
                        {
                            Anchor = Anchor.BottomLeft,
                            Origin = Anchor.BottomLeft,
                            Position = new Vector2(10, -10),
                            Font = HomeTypography.Display(29),
                            Colour = HomeControlColours.Navy,
                        },
                        detailText = new SpriteText
                        {
                            Anchor = Anchor.BottomRight,
                            Origin = Anchor.BottomRight,
                            Position = new Vector2(-10, -12),
                            Font = HomeTypography.Display(8),
                            Colour = new Color4(
                                HomeControlColours.Navy.R,
                                HomeControlColours.Navy.G,
                                HomeControlColours.Navy.B,
                                0.78f),
                        },
                        underline = new Box
                        {
                            Anchor = Anchor.BottomLeft,
                            Origin = Anchor.BottomLeft,
                            Position = new Vector2(10, -5),
                            Size = new Vector2(22, 2),
                            Colour = HomeControlColours.Cyan,
                        },
                    ],
                },
                diamond = new Box
                {
                    Origin = Anchor.Centre,
                    Position = new Vector2(160, 2),
                    Size = new Vector2(6),
                    Rotation = 45,
                    Colour = HomeControlColours.Yellow,
                },
            ],
        };

        Alpha = 0;
    }

    internal void Show(
        double speed,
        int timeRangeMilliseconds,
        bool showMilliseconds = false,
        bool locked = false)
    {
        DisplayedSpeed = speed;
        DisplayedTimeRangeMilliseconds = timeRangeMilliseconds;
        IsLocked = locked;

        Color4 accent = locked
            ? HomeControlColours.Pink
            : HomeControlColours.Cyan;
        label.Text = locked
            ? "SPEED LOCKED"
            : showMilliseconds
                ? "SCROLL TIME"
                : "SCROLL SPEED";
        label.Colour = locked
            ? HomeControlColours.Pink
            : HomeControlColours.Navy;
        speedText.Text = showMilliseconds
            ? timeRangeMilliseconds.ToString()
            : speed.ToString("0.0");
        detailText.Text = locked
            ? "INTRO / BREAK"
            : showMilliseconds
                ? $"ms · {speed:0.000}"
                : $"{timeRangeMilliseconds} ms";
        underline.Colour = accent;

        bool wasVisible = Alpha > 0.01f;

        ClearTransforms();
        stage.ClearTransforms();
        speedText.ClearTransforms();
        underline.ClearTransforms();
        diamond.ClearTransforms();

        if (!wasVisible)
        {
            Alpha = 0;
            stage.Y = 6;
            stage.Scale = new Vector2(0.965f);
            underline.Width = 0;
            diamond.Scale = new Vector2(0.72f);

            this.FadeIn(90, Easing.OutQuint);
            stage.MoveToY(-1.25f, 140, Easing.OutQuint)
                 .Then()
                 .MoveToY(0, 90, Easing.OutBack);
            stage.ScaleTo(1.012f, 140, Easing.OutQuint)
                 .Then()
                 .ScaleTo(1, 100, Easing.OutBack);
            underline.ResizeWidthTo(22, 180, Easing.OutQuint);
            diamond.ScaleTo(1.08f, 120, Easing.OutQuint)
                   .Then()
                   .ScaleTo(1, 120, Easing.OutBack);
        }
        else
        {
            Alpha = 1;
            stage.Y = 0;
            stage.Scale = Vector2.One;
            speedText.Scale = Vector2.One;
            underline.Width = 15;
            diamond.Scale = Vector2.One;

            speedText.ScaleTo(1.045f, 80, Easing.OutQuint)
                     .Then()
                     .ScaleTo(1, 150, Easing.OutBack);
            underline.ResizeWidthTo(22, 130, Easing.OutQuint);
            diamond.FlashColour(accent, 180, Easing.OutQuint);
        }

        this.Delay(displayDurationMilliseconds)
            .FadeOut(exitDurationMilliseconds, Easing.OutQuint);
        stage.Delay(displayDurationMilliseconds)
             .MoveToY(-5, exitDurationMilliseconds, Easing.InQuint);
        stage.Delay(displayDurationMilliseconds)
             .ScaleTo(
                 0.985f,
                 exitDurationMilliseconds,
                 Easing.InQuint);
    }
}
