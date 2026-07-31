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
    private readonly Box background;
    private readonly Box underline;
    private readonly SpriteIcon chevron;
    private readonly SpriteText backLabel;

    public SongSelectFooterBackButton(Action action)
        : this(action, null)
    {
    }

    public SongSelectFooterBackButton(
        Action action,
        Texture diamondTexture)
    {
        Action = action;
        Size = new Vector2(194, 78);

        Container panel = SongSelectSurface.CreateCard(
            out background,
            SongSelectSurface.Ivory(0.99f),
            new Color4(
                SongSelectTheme.Navy.R,
                SongSelectTheme.Navy.G,
                SongSelectTheme.Navy.B,
                0.30f),
            10,
            1.25f);

        InternalChildren = new Drawable[]
        {
            SongSelectSurface.CreateShadow(10, 0.18f, 3),
            panel,
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
            backLabel = new SpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 68,
                Y = -2,
                Text = "BACK",
                Font = HomeTypography.Display(20),
                Colour = SongSelectTheme.Navy,
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

    private static Drawable createDiamondDecoration(Texture texture) =>
        texture == null
            ? new Container
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.Centre,
                Alpha = 0,
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
        background.FadeColour(SongSelectTheme.Navy, 120, Easing.OutQuint);
        backLabel.FadeColour(Color4.White, 120, Easing.OutQuint);
        underline.ResizeWidthTo(58, 150, Easing.OutQuint);
        chevron.MoveToX(-9, 150, Easing.OutQuint);
        this.ScaleTo(1.018f, 120, Easing.OutQuint);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        background.FadeColour(
            SongSelectSurface.Ivory(0.99f),
            140,
            Easing.OutQuint);
        backLabel.FadeColour(SongSelectTheme.Navy, 140, Easing.OutQuint);
        underline.ResizeWidthTo(0, 130, Easing.OutQuint);
        chevron.MoveToX(-13, 130, Easing.OutQuint);
        this.ScaleTo(1, 140, Easing.OutQuint);
    }
}

internal partial class SongSelectFooterToolButton : ClickableContainer
{
    private readonly Box background;
    private readonly Color4 accent;

    public SongSelectFooterToolButton(
        string label,
        IconUsage icon,
        Color4 accent,
        Action action)
    {
        this.accent = accent;
        Action = action;
        Size = new Vector2(126, 82);
        Masking = true;
        CornerRadius = 10;
        BorderThickness = 1.25f;
        BorderColour = new Color4(
            SongSelectTheme.Navy.R,
            SongSelectTheme.Navy.G,
            SongSelectTheme.Navy.B,
            0.24f);

        InternalChildren =
        [
            background = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = SongSelectSurface.Ivory(0.99f),
            },
            new SpriteIcon
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Y = 14,
                Size = new Vector2(27),
                Icon = icon,
                Colour = accent,
            },
            new SpriteText
            {
                Anchor = Anchor.BottomCentre,
                Origin = Anchor.BottomCentre,
                Y = -11,
                Text = label,
                Font = HomeTypography.Display(9),
                Spacing = new Vector2(1.2f, 0),
                Colour = SongSelectTheme.Navy,
            },
            new Box
            {
                Anchor = Anchor.BottomCentre,
                Origin = Anchor.BottomCentre,
                Y = -4,
                Width = 36,
                Height = 3,
                Colour = accent,
            },
        ];
    }

    protected override bool OnHover(HoverEvent e)
    {
        background.FadeColour(
            SongSelectTheme.PaleCyan,
            110,
            Easing.OutQuint);
        this.ScaleTo(1.025f, 110, Easing.OutQuint);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        background.FadeColour(
            SongSelectSurface.Ivory(0.99f),
            130,
            Easing.OutQuint);
        this.ScaleTo(1, 130, Easing.OutQuint);
    }
}
