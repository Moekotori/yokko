using System;
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

internal partial class SongSelectPlayButton : ClickableContainer
{
    private readonly Box background;
    private readonly SpriteIcon chevron;
    private readonly SpriteIcon stateIcon;
    private readonly Container playTile;
    private readonly Box shine;
    private readonly Sprite tape;
    private readonly SpriteText eyebrowText;
    private readonly SpriteText actionText;
    private bool ambientSelection;

    internal string EyebrowText => eyebrowText.Text.ToString();
    internal string ActionText => actionText.Text.ToString();

    public SongSelectPlayButton(
        Action action,
        Texture tapeTexture)
    {
        Action = action;
        Size = new Vector2(400, 82);

        Container panel = SongSelectSurface.CreateCard(
            out background,
            SongSelectTheme.Yellow,
            new Color4(
                SongSelectTheme.Navy.R,
                SongSelectTheme.Navy.G,
                SongSelectTheme.Navy.B,
                0.52f),
            11,
            1.5f);

        InternalChildren =
        [
            SongSelectSurface.CreateShadow(11, 0.24f, 4),
            new Container
            {
                Size = new Vector2(397, 77),
                Masking = true,
                CornerRadius = 11,
                Children =
                [
                    panel,
                    shine = new Box
                    {
                        Position = new Vector2(-88, -30),
                        Size = new Vector2(40, 145),
                        Rotation = -18,
                        Colour = new Color4(1f, 1f, 1f, 0.18f),
                    },
                    playTile = new Container
                    {
                        Position = new Vector2(13, 7),
                        Size = new Vector2(64),
                        Masking = true,
                        CornerRadius = 8,
                        BorderThickness = 1.5f,
                        BorderColour = new Color4(1f, 1f, 1f, 0.32f),
                        Children =
                        [
                            new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Colour = SongSelectTheme.Navy,
                            },
                            stateIcon = new SpriteIcon
                            {
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                Size = new Vector2(28),
                                Icon = FontAwesome.Solid.Play,
                                Colour = Color4.White,
                            },
                        ],
                    },
                    eyebrowText = new SpriteText
                    {
                        Position = new Vector2(96, 13),
                        Text = "START SELECTED CHART",
                        Font = HomeTypography.Display(10),
                        Spacing = new Vector2(1.1f, 0),
                        Colour = new Color4(
                            SongSelectTheme.Navy.R,
                            SongSelectTheme.Navy.G,
                            SongSelectTheme.Navy.B,
                            0.64f),
                    },
                    actionText = new SpriteText
                    {
                        Position = new Vector2(95, 25),
                        Text = "PLAY",
                        Font = HomeTypography.Display(39),
                        Colour = SongSelectTheme.Navy,
                    },
                    chevron = new SpriteIcon
                    {
                        Anchor = Anchor.CentreRight,
                        Origin = Anchor.CentreRight,
                        X = -15,
                        Size = new Vector2(14),
                        Icon = FontAwesome.Solid.ChevronRight,
                        Colour = SongSelectTheme.Navy,
                    },
                ],
            },
            tape = new Sprite
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.Centre,
                Position = new Vector2(-28, 2),
                Size = new Vector2(58, 32),
                Texture = tapeTexture,
                FillMode = FillMode.Fit,
            },
        ];
    }

    internal void SetReady(bool useAmbientSelection = false)
    {
        ambientSelection = useAmbientSelection;
        Enabled.Value = true;
        eyebrowText.Text = ambientSelection
            ? "PLAY PREVIOUS SELECTION"
            : "START SELECTED CHART";
        actionText.Text = "PLAY";
        stateIcon.Icon = FontAwesome.Solid.Play;
        background.FadeColour(readyColour(), 120, Easing.OutQuint);
        chevron.FadeTo(ambientSelection ? 0.72f : 1, 120, Easing.OutQuint);
        shine.FadeTo(ambientSelection ? 0.42f : 1, 120, Easing.OutQuint);
    }

    internal void SetAmbientSelection(bool value)
    {
        ambientSelection = value;
        if (actionText.Text.ToString() == "PLAY")
            SetReady(value);
    }

    internal void SetPreparing(string message = "PREPARING CHART")
    {
        ambientSelection = false;
        Enabled.Value = false;
        eyebrowText.Text = message;
        actionText.Text = "LOADING...";
        stateIcon.Icon = FontAwesome.Solid.HourglassHalf;
        background.FadeColour(
            new Color4(1f, 0.91f, 0.38f, 1f),
            100,
            Easing.OutQuint);
        chevron.FadeTo(0.38f, 100, Easing.OutQuint);
        shine.FadeTo(0.28f, 100, Easing.OutQuint);
    }

    internal void SetError()
    {
        ambientSelection = false;
        Enabled.Value = true;
        eyebrowText.Text = "CHART COULD NOT LOAD";
        actionText.Text = "RETRY";
        stateIcon.Icon = FontAwesome.Solid.ExclamationTriangle;
        background.FadeColour(
            new Color4(1f, 0.70f, 0.76f, 1f),
            120,
            Easing.OutQuint);
        chevron.FadeTo(1, 120, Easing.OutQuint);
        shine.FadeTo(0.55f, 120, Easing.OutQuint);
    }

    protected override bool OnHover(HoverEvent e)
    {
        background.FadeColour(
            ambientSelection
                ? new Color4(1f, 0.93f, 0.48f, 0.92f)
                : new Color4(1f, 0.95f, 0.42f, 1f),
            110,
            Easing.OutQuint);
        chevron.MoveToX(-10, 130, Easing.OutQuint);
        playTile.RotateTo(-3, 150, Easing.OutQuint);
        tape.RotateTo(6, 170, Easing.OutQuint);
        this.ScaleTo(1.018f, 110, Easing.OutQuint);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        background.FadeColour(readyColour(), 130, Easing.OutQuint);
        chevron.MoveToX(-15, 130, Easing.OutQuint);
        playTile.RotateTo(0, 190, Easing.OutQuint);
        tape.RotateTo(0, 210, Easing.OutQuint);
        this.ScaleTo(1, 130, Easing.OutQuint);
    }

    private Color4 readyColour() => ambientSelection
        ? new Color4(
            SongSelectTheme.Yellow.R,
            SongSelectTheme.Yellow.G,
            SongSelectTheme.Yellow.B,
            0.78f)
        : SongSelectTheme.Yellow;

    protected override void LoadComplete()
    {
        base.LoadComplete();
        shine.MoveToX(-88)
             .Then()
             .MoveToX(438, 820, Easing.InOutQuart)
             .Loop(2500);
    }

    protected override bool OnMouseDown(MouseDownEvent e)
    {
        this.ScaleTo(0.975f, 75, Easing.OutQuint);
        return base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseUpEvent e)
    {
        this.ScaleTo(IsHovered ? 1.018f : 1, 190, Easing.OutBack);
        base.OnMouseUp(e);
    }
}
