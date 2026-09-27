using System;
using osu.Framework.Allocation;
using Yokko.Game.Presentation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using Yokko.Game.Localisation;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
using osuTK;
using osuTK.Graphics;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Screens.SongSelect;

internal partial class SongSelectFooterBackButton : ClickableContainer
{
    [Resolved(CanBeNull = true)]
    private YokkoAccessibilitySettings accessibility { get; set; }
    private bool reduceMotion => accessibility?.ReduceMotion.Value == true;

    private readonly Box background;
    private readonly Container keycap;
    private readonly Box underline;
    private readonly SpriteIcon chevron;
    private readonly SpriteText backLabel;
    private readonly Drawable diamondDecoration;

    public SongSelectFooterBackButton(Action action)
        : this(action, null)
    {
    }

    public SongSelectFooterBackButton(
        Action action,
        Texture diamondTexture)
    {
        Action = action;
        Size = new Vector2(210, 82);

        Container panel = SongSelectSurface.CreateCard(
            out background,
            SongSelectSurface.Ivory(SongSelectSurface.FooterCardOpacity),
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
            keycap = new Container
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
                        Font = HomeTypography.Display(12),
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
                Text = YokkoStrings.Get("song_select.back_label"),
                Font = HomeTypography.Display(24),
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
            diamondDecoration = createDiamondDecoration(diamondTexture),
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
                Position = new Vector2(-8, 2),
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
        keycap.RotateTo(reduceMotion ? 0 : -4, reduceMotion ? 0 : 140, Easing.OutQuint);
        diamondDecoration.RotateTo(reduceMotion ? 0 : 8, reduceMotion ? 0 : 160, Easing.OutQuint);
        this.ScaleTo(reduceMotion ? 1 : 1.018f, reduceMotion ? 0 : 120, Easing.OutQuint);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        background.FadeColour(
            SongSelectSurface.Ivory(SongSelectSurface.FooterCardOpacity),
            140,
            Easing.OutQuint);
        backLabel.FadeColour(SongSelectTheme.Navy, 140, Easing.OutQuint);
        underline.ResizeWidthTo(0, 130, Easing.OutQuint);
        chevron.MoveToX(-13, 130, Easing.OutQuint);
        keycap.RotateTo(0, reduceMotion ? 0 : 180, Easing.OutQuint);
        diamondDecoration.RotateTo(0, reduceMotion ? 0 : 200, Easing.OutQuint);
        this.ScaleTo(1, reduceMotion ? 0 : 140, Easing.OutQuint);
    }

    protected override bool OnMouseDown(MouseDownEvent e)
    {
        this.ScaleTo(reduceMotion ? 1 : 0.975f, reduceMotion ? 0 : 80, Easing.OutQuint);
        return base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseUpEvent e)
    {
        this.ScaleTo(reduceMotion ? 1 : (IsHovered ? 1.018f : 1), reduceMotion ? 0 : 180, Easing.OutBack);
        base.OnMouseUp(e);
    }
}

internal partial class SongSelectFooterToolButton : ClickableContainer
{
    [Resolved(CanBeNull = true)]
    private YokkoAccessibilitySettings accessibility { get; set; }
    private bool reduceMotion => accessibility?.ReduceMotion.Value == true;

    private readonly Box background;
    private readonly Color4 accent;
    private readonly Container iconTile;
    private readonly SpriteIcon icon;
    private readonly Box bottomAccent;

    public SongSelectFooterToolButton(
        LocalisableString label,
        IconUsage icon,
        Color4 accent,
        Action action)
    {
        this.accent = accent;
        Action = action;
        Size = new Vector2(154, 82);

        InternalChildren =
        [
            background = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = Color4.Transparent,
            },
            iconTile = new Container
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Y = 7,
                Size = new Vector2(42),
                Masking = true,
                CornerRadius = 10,
                BorderThickness = 1,
                BorderColour = new Color4(accent.R, accent.G, accent.B, 0.38f),
                Children =
                [
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = new Color4(accent.R, accent.G, accent.B, 0.08f),
                    },
                    this.icon = new SpriteIcon
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Size = new Vector2(23),
                        Icon = icon,
                        Colour = accent,
                    },
                ],
            },
            new SpriteText
            {
                Anchor = Anchor.BottomCentre,
                Origin = Anchor.BottomCentre,
                Y = -11,
                Text = label,
                Font = HomeTypography.Control(14),
                Spacing = new Vector2(1.2f, 0),
                Colour = SongSelectTheme.Navy,
            },
            bottomAccent = new Box
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
        iconTile.RotateTo(reduceMotion ? 0 : -5, reduceMotion ? 0 : 130, Easing.OutQuint);
        icon.RotateTo(reduceMotion ? 0 : 12, reduceMotion ? 0 : 150, Easing.OutQuint);
        bottomAccent.ResizeWidthTo(64, 150, Easing.OutQuint);
        this.ScaleTo(reduceMotion ? 1 : 1.025f, reduceMotion ? 0 : 110, Easing.OutQuint);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        background.FadeColour(
            Color4.Transparent,
            130,
            Easing.OutQuint);
        iconTile.RotateTo(0, reduceMotion ? 0 : 180, Easing.OutQuint);
        icon.RotateTo(0, reduceMotion ? 0 : 200, Easing.OutQuint);
        bottomAccent.ResizeWidthTo(36, 150, Easing.OutQuint);
        this.ScaleTo(1, reduceMotion ? 0 : 130, Easing.OutQuint);
    }

    protected override bool OnMouseDown(MouseDownEvent e)
    {
        this.ScaleTo(reduceMotion ? 1 : 0.97f, reduceMotion ? 0 : 70, Easing.OutQuint);
        return base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseUpEvent e)
    {
        this.ScaleTo(reduceMotion ? 1 : (IsHovered ? 1.025f : 1), reduceMotion ? 0 : 180, Easing.OutBack);
        base.OnMouseUp(e);
    }
}
