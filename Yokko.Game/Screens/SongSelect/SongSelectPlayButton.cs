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
                Children =
                [
                    panel,
                    new Container
                    {
                        Position = new Vector2(13, 7),
                        Size = new Vector2(64),
                        Masking = true,
                        CornerRadius = 8,
                        Children =
                        [
                            new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Colour = SongSelectTheme.Navy,
                            },
                            new SpriteIcon
                            {
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                Size = new Vector2(28),
                                Icon = FontAwesome.Solid.Play,
                                Colour = Color4.White,
                            },
                        ],
                    },
                    new SpriteText
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
                    new SpriteText
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
            new Sprite
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

    protected override bool OnHover(HoverEvent e)
    {
        background.FadeColour(
            new Color4(1f, 0.95f, 0.42f, 1f),
            110,
            Easing.OutQuint);
        chevron.MoveToX(-10, 130, Easing.OutQuint);
        this.ScaleTo(1.018f, 110, Easing.OutQuint);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        background.FadeColour(SongSelectTheme.Yellow, 130, Easing.OutQuint);
        chevron.MoveToX(-15, 130, Easing.OutQuint);
        this.ScaleTo(1, 130, Easing.OutQuint);
    }
}
