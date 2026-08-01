using System;
using System.Collections.Generic;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
using osuTK;
using osuTK.Graphics;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Screens.SongSelect;

internal partial class SongSelectAccountCard : ClickableContainer
{
    private static readonly string[] metricLabels = ["PLAYS", "ACC", "GLOBAL"];
    private static readonly string[] metricValues = ["0", "0.00%", "#0"];

    private readonly Box background;
    private readonly Container avatarContainer;
    private readonly SpriteText displayName;
    private readonly SpriteText levelText;
    private readonly SpriteText statusLabel;
    private readonly Box focusLine;
    private readonly Sprite star;
    private int reactionVersion;

    public SongSelectAccountCard(
        string name,
        string level,
        Texture avatar,
        Texture starTexture)
    {
        Size = new Vector2(520, 82);
        Action = react;

        Container panel = SongSelectSurface.CreateCard(
            out background,
            SongSelectSurface.Ivory(0.98f),
            new Color4(
                SongSelectTheme.Cyan.R,
                SongSelectTheme.Cyan.G,
                SongSelectTheme.Cyan.B,
                0.48f),
            10,
            1.25f);

        InternalChildren =
        [
            SongSelectSurface.CreateShadow(10, 0.18f, 3),
            panel,
            avatarContainer = new Container
            {
                Position = new Vector2(9, 7),
                Size = new Vector2(68),
                Masking = true,
                CornerRadius = 32,
                BorderThickness = 2,
                BorderColour = SongSelectTheme.Cyan,
                Child = new Sprite
                {
                    RelativeSizeAxes = Axes.Both,
                    Texture = avatar,
                    FillMode = FillMode.Fill,
                },
            },
            new Circle
            {
                Position = new Vector2(63, 58),
                Size = new Vector2(13),
                BorderThickness = 2,
                BorderColour = Color4.White,
                Colour = new Color4(0.24f, 0.82f, 0.48f, 1f),
            },
            displayName = new SpriteText
            {
                Position = new Vector2(92, 6),
                Text = name,
                Font = HomeTypography.Display(18),
                Colour = SongSelectTheme.Navy,
            },
            new SpriteIcon
            {
                Position = new Vector2(232, 16),
                Size = new Vector2(7),
                Icon = FontAwesome.Solid.Circle,
                Colour = new Color4(0.22f, 0.72f, 0.46f, 1f),
            },
            statusLabel = new SpriteText
            {
                Position = new Vector2(244, 11),
                Text = "READY",
                Font = HomeTypography.Display(9),
                Colour = new Color4(0.22f, 0.72f, 0.46f, 1f),
            },
            createMetric(metricValues[0], metricLabels[0], 92),
            createMetric(metricValues[1], metricLabels[1], 180),
            createMetric(metricValues[2], metricLabels[2], 278),
            new Container
            {
                Position = new Vector2(402, 9),
                Size = new Vector2(78, 22),
                Masking = true,
                CornerRadius = 7,
                Children =
                [
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = new Color4(
                            SongSelectTheme.Yellow.R,
                            SongSelectTheme.Yellow.G,
                            SongSelectTheme.Yellow.B,
                            0.46f),
                    },
                    new SpriteText
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Text = "DEMO",
                        Font = HomeTypography.Display(9),
                        Colour = SongSelectTheme.Navy,
                    },
                ],
            },
            new Box
            {
                Position = new Vector2(92, 67),
                Size = new Vector2(286, 4),
                Colour = new Color4(
                    SongSelectTheme.Cyan.R,
                    SongSelectTheme.Cyan.G,
                    SongSelectTheme.Cyan.B,
                    0.20f),
            },
            focusLine = new Box
            {
                Position = new Vector2(92, 67),
                Size = new Vector2(0, 4),
                Colour = SongSelectTheme.Cyan,
            },
            levelText = new SpriteText
            {
                Position = new Vector2(398, 54),
                Text = level,
                Font = HomeTypography.Display(10),
                Colour = SongSelectTheme.Pink,
            },
            star = new Sprite
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.Centre,
                Position = new Vector2(-5, 4),
                Size = new Vector2(30),
                Texture = starTexture,
                FillMode = FillMode.Fit,
            },
        ];
    }

    internal string DisplayName => displayName.Text.ToString();
    internal string LevelText => levelText.Text.ToString();
    internal IReadOnlyList<string> MetricLabels => metricLabels;
    internal IReadOnlyList<string> MetricValues => metricValues;

    protected override void LoadComplete()
    {
        base.LoadComplete();
        star.RotateTo(-4)
            .Then().RotateTo(5, 1500, Easing.InOutSine)
            .Then().RotateTo(-4, 1500, Easing.InOutSine)
            .Loop();
    }

    protected override bool OnHover(HoverEvent e)
    {
        background.FadeColour(new Color4(0.96f, 0.995f, 1f, 1f), 120, Easing.OutQuint);
        avatarContainer.RotateTo(-3, 150, Easing.OutQuint);
        focusLine.ResizeWidthTo(286, 180, Easing.OutQuint);
        star.ScaleTo(1.12f, 170, Easing.OutBack);
        this.ScaleTo(1.012f, 120, Easing.OutQuint);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        background.FadeColour(SongSelectSurface.Ivory(0.98f), 140, Easing.OutQuint);
        avatarContainer.RotateTo(0, 190, Easing.OutQuint);
        focusLine.ResizeWidthTo(0, 150, Easing.OutQuint);
        star.ScaleTo(1, 190, Easing.OutQuint);
        this.ScaleTo(1, 140, Easing.OutQuint);
    }

    protected override bool OnMouseDown(MouseDownEvent e)
    {
        this.ScaleTo(0.985f, 70, Easing.OutQuint);
        return base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseUpEvent e)
    {
        this.ScaleTo(IsHovered ? 1.012f : 1, 180, Easing.OutBack);
        base.OnMouseUp(e);
    }

    private void react()
    {
        int version = ++reactionVersion;
        statusLabel.Text = "HELLO!";
        statusLabel.FlashColour(SongSelectTheme.Pink, 360);
        avatarContainer.ScaleTo(0.92f, 70, Easing.OutQuint)
                       .Then().ScaleTo(1.08f, 140, Easing.OutBack)
                       .Then().ScaleTo(1, 100, Easing.OutQuint);
        star.RotateTo(24, 150, Easing.OutBack)
            .Then().RotateTo(0, 220, Easing.OutQuint);
        Scheduler.AddDelayed(() =>
        {
            if (reactionVersion == version)
                statusLabel.Text = "READY";
        }, 750);
    }

    private static Drawable createMetric(
        string value,
        string label,
        float x) => new Container
    {
        Position = new Vector2(x, 31),
        Size = new Vector2(82, 27),
        Children =
        [
            new SpriteText
            {
                Text = value,
                Font = HomeTypography.Display(12),
                Colour = SongSelectTheme.Navy,
            },
            new SpriteText
            {
                Y = 14,
                Text = label,
                Font = HomeTypography.Display(8),
                Colour = SongSelectTheme.Cyan,
            },
        ],
    };
}
