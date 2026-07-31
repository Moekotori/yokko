using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;
using Yokko.Core.Difficulty;
using Yokko.Game.Presentation;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Screens.Gameplay;

internal partial class GameplayPlaybackRateOverlay : CompositeDrawable
{
    internal static readonly Vector2 ReferenceSize = new(214, 78);
    internal const float PreferredLeft = 160;
    internal const float TopOffset = 198;
    internal const float PlayfieldGap = 22;

    private const float faceWidth = 210;
    private const float faceHeight = 74;
    private const double displayDurationMilliseconds = 1400;
    private const double exitDurationMilliseconds = 170;

    private readonly Container stage;
    private readonly Box underline;
    private readonly Box diamond;
    private readonly SpriteText rateText;
    private readonly SpriteText detailText;

    internal double DisplayedRate { get; private set; } = 1;

    internal double DisplayedBpm { get; private set; }

    internal double? DisplayedDifficulty { get; private set; }

    internal string DisplayedDetail =>
        detailText.Text.ToString();

    internal bool IsVisible => Alpha > 0.01f;

    internal GameplayPlaybackRateOverlay()
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
                        new SpriteText
                        {
                            Position = new Vector2(10, 7),
                            Text = "PLAYBACK RATE",
                            Font = HomeTypography.Display(8),
                            Spacing = new Vector2(0.65f, 0),
                            Colour = HomeControlColours.Navy,
                        },
                        rateText = new SpriteText
                        {
                            Anchor = Anchor.BottomLeft,
                            Origin = Anchor.BottomLeft,
                            Position = new Vector2(10, -11),
                            Font = HomeTypography.Display(25),
                            Colour = HomeControlColours.Navy,
                        },
                        detailText = new SpriteText
                        {
                            Anchor = Anchor.BottomRight,
                            Origin = Anchor.BottomRight,
                            Position = new Vector2(-10, -14),
                            Font = HomeTypography.Display(7),
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
                            Colour = HomeControlColours.Pink,
                        },
                    ],
                },
                diamond = new Box
                {
                    Origin = Anchor.Centre,
                    Position = new Vector2(212, 2),
                    Size = new Vector2(6),
                    Rotation = 45,
                    Colour = HomeControlColours.Yellow,
                },
            ],
        };

        Alpha = 0;
    }

    internal void Show(
        double rate,
        double bpm,
        ManiaMsdResult difficulty)
    {
        UpdateValues(rate, bpm, difficulty);

        bool wasVisible = IsVisible;

        ClearTransforms();
        stage.ClearTransforms();
        rateText.ClearTransforms();
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
            rateText.Scale = Vector2.One;
            underline.Width = 15;
            diamond.Scale = Vector2.One;

            rateText.ScaleTo(1.045f, 80, Easing.OutQuint)
                    .Then()
                    .ScaleTo(1, 150, Easing.OutBack);
            underline.ResizeWidthTo(22, 130, Easing.OutQuint);
            diamond.FlashColour(
                HomeControlColours.Pink,
                180,
                Easing.OutQuint);
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

    internal void UpdateValues(
        double rate,
        double bpm,
        ManiaMsdResult difficulty)
    {
        DisplayedRate = rate;
        DisplayedBpm = bpm;
        DisplayedDifficulty = difficulty?.Value;

        rateText.Text = $"{rate:0.00}x";
        string bpmText = bpm > 0
            ? $"{bpm:0.##} BPM"
            : "-- BPM";
        string difficultyText =
            ManiaMsdPresentation.FormatMsd(difficulty);
        detailText.Text = $"{bpmText} / {difficultyText}";
    }
}
