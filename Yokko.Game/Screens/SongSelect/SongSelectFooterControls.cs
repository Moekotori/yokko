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

internal partial class SongSelectFooterBackButton : ClickableContainer
{
    private readonly Drawable background;
    private readonly Box underline;
    private readonly SpriteIcon chevron;

    public SongSelectFooterBackButton(Action action)
        : this(action, null, null)
    {
    }

    public SongSelectFooterBackButton(
        Action action,
        Texture paperTexture,
        Texture diamondTexture)
    {
        Action = action;
        Size = new Vector2(174, 74);

        InternalChildren = new Drawable[]
        {
            background = createPaperBackground(paperTexture),
            new Container
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 14,
                Y = -2,
                Size = new Vector2(44),
                Masking = true,
                CornerRadius = 6,
                BorderThickness = 1.5f,
                BorderColour = HomeControlColours.Navy,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = HomeControlColours.Ivory,
                    },
                    new SpriteText
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Text = "ESC",
                        Font = HomeTypography.Display(11),
                        Colour = HomeControlColours.Navy,
                    },
                },
            },
            new SpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 68,
                Y = -2,
                Text = "BACK",
                Font = HomeTypography.Display(20),
                Colour = HomeControlColours.Navy,
            },
            chevron = new SpriteIcon
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                X = -13,
                Y = -2,
                Size = new Vector2(12),
                Icon = FontAwesome.Solid.ChevronRight,
                Colour = HomeControlColours.Pink,
            },
            createDiamondDecoration(diamondTexture),
            underline = new Box
            {
                Anchor = Anchor.BottomLeft,
                Origin = Anchor.BottomLeft,
                X = 68,
                Y = -4,
                Width = 0,
                Height = 2,
                Colour = HomeControlColours.Cyan,
            },
        };
    }

    private static Drawable createPaperBackground(Texture texture) =>
        texture == null
            ? new Box
            {
                Size = new Vector2(174, 70),
                Colour = Color4.White,
            }
            : new Sprite
            {
                Size = new Vector2(174, 70),
                Texture = texture,
                FillMode = FillMode.Fill,
            };

    private static Drawable createDiamondDecoration(Texture texture) =>
        texture == null
            ? new Box
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.Centre,
                Size = new Vector2(17),
                Rotation = 45,
                Colour = HomeControlColours.Yellow,
            }
            : new Sprite
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.Centre,
                Position = new Vector2(0, 2),
                Size = new Vector2(28),
                Texture = texture,
                FillMode = FillMode.Fit,
            };

    protected override bool OnHover(HoverEvent e)
    {
        background.FadeColour(HomeControlColours.PaleCyan, 120, Easing.OutQuint);
        underline.ResizeWidthTo(58, 150, Easing.OutQuint);
        chevron.MoveToX(-9, 150, Easing.OutQuint);
        this.ScaleTo(1.018f, 120, Easing.OutQuint);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        background.FadeColour(Color4.White, 140, Easing.OutQuint);
        underline.ResizeWidthTo(0, 130, Easing.OutQuint);
        chevron.MoveToX(-13, 130, Easing.OutQuint);
        this.ScaleTo(1, 140, Easing.OutQuint);
    }
}
