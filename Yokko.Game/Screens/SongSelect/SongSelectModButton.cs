using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
using osuTK;
using osuTK.Graphics;
using Yokko.Core.Mods;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Screens.SongSelect;

internal partial class SongSelectModButton : ClickableContainer
{
    private readonly Color4 accent;
    private readonly Box background;
    private readonly SpriteText label;
    private readonly Action<ManiaModId, bool> hoverChanged;
    private bool selected;

    public SongSelectModButton(
        ManiaModId mod,
        Color4 accent,
        Action action,
        Action<ManiaModId, bool> hoverChanged)
    {
        Mod = mod;
        this.accent = accent;
        this.hoverChanged = hoverChanged;
        Action = action;
        Size = new Vector2(49, 36);
        Masking = true;
        CornerRadius = 4;
        BorderThickness = 2;
        BorderColour = accent;

        InternalChildren = new Drawable[]
        {
            background = new Box
            {
                RelativeSizeAxes = Axes.Both,
            },
            label = new SpriteText
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Text = OsuManiaModParityCatalog.Get(mod).Acronym,
                Font = HomeTypography.Display(16),
            },
        };

        SetSelected(false);
    }

    public ManiaModId Mod { get; }

    public void SetSelected(bool selected)
    {
        this.selected = selected;
        background.Colour = selected
            ? accent
            : new Color4(
                SongSelectTheme.Navy.R,
                SongSelectTheme.Navy.G,
                SongSelectTheme.Navy.B,
                0.62f);
        label.Colour = selected
            ? SongSelectTheme.DeepNavy
            : accent;
        this.ScaleTo(selected ? 1.06f : 1, 120);
    }

    protected override bool OnHover(HoverEvent e)
    {
        hoverChanged?.Invoke(Mod, true);
        this.ScaleTo(selected ? 1.1f : 1.045f, 90, Easing.OutQuint);
        background.FadeColour(
            new Color4(accent.R, accent.G, accent.B, selected ? 1 : 0.2f),
            90,
            Easing.OutQuint);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        hoverChanged?.Invoke(Mod, false);
        SetSelected(selected);
    }
}

internal partial class SongSelectModsToggleButton : ClickableContainer
{
    private readonly Container card;
    private readonly Box background;
    private readonly Box topAccent;
    private readonly SpriteIcon icon;
    private readonly Circle badge;
    private readonly SpriteText countLabel;
    private bool open;

    public SongSelectModsToggleButton(
        Action action,
        Texture diamondTexture)
    {
        Action = action;
        Size = new Vector2(126, 96);

        InternalChildren = new Drawable[]
        {
            new Container
            {
                Position = new Vector2(10.5f, 10.5f),
                Size = new Vector2(106, 72),
                Masking = true,
                CornerRadius = 8,
                Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new Color4(
                        HomeControlColours.Navy.R,
                        HomeControlColours.Navy.G,
                        HomeControlColours.Navy.B,
                        0.18f),
                },
            },
            card = new Container
            {
                Position = new Vector2(8.5f, 7.5f),
                Size = new Vector2(106, 72),
                Masking = true,
                CornerRadius = 8,
                BorderThickness = 1.5f,
                BorderColour = HomeControlColours.Navy,
                Children = new Drawable[]
                {
                    background = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4.White,
                    },
                    new Container
                    {
                        Position = new Vector2(7),
                        Size = new Vector2(92, 58),
                        Masking = true,
                        CornerRadius = 5,
                        Child = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = HomeControlColours.PaleCyan,
                        },
                    },
                    icon = new SpriteIcon
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Size = new Vector2(30),
                        Icon = FontAwesome.Solid.SlidersH,
                        Colour = HomeControlColours.Navy,
                    },
                    topAccent = new Box
                    {
                        Anchor = Anchor.BottomLeft,
                        Origin = Anchor.BottomLeft,
                        X = 10,
                        Width = 42,
                        Height = 3,
                        Colour = HomeControlColours.Pink,
                    },
                },
            },
            new Sprite
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.Centre,
                Position = new Vector2(0, 2),
                Size = new Vector2(27),
                Texture = diamondTexture,
                FillMode = FillMode.Fit,
            },
            badge = new Circle
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                Position = new Vector2(-8, 8),
                Size = new Vector2(16),
                BorderThickness = 1,
                BorderColour = HomeControlColours.Navy,
            },
            countLabel = new SpriteText
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.Centre,
                Position = new Vector2(-16, 16),
                Font = HomeTypography.Display(9),
            },
            new SpriteText
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Y = 80,
                Text = "MODS",
                Font = HomeTypography.Display(11),
                Spacing = new Vector2(1.4f, 0),
                Colour = SongSelectTheme.Navy,
            },
        };

        SetCount(0);
        SetOpen(false);
    }

    public void SetCount(int value)
    {
        int count = Math.Max(0, value);
        countLabel.Text = count.ToString();
        badge.Alpha = count > 0 ? 1 : 0;
        countLabel.Alpha = count > 0 ? 1 : 0;
        badge.Colour = count > 0
            ? SongSelectTheme.Pink
            : SongSelectTheme.Navy;
        countLabel.Colour = count > 0
            ? SongSelectTheme.Ivory
            : SongSelectTheme.PaleCyan;
    }

    public void SetOpen(bool value)
    {
        open = value;
        background.Colour = open
            ? HomeControlColours.PaleCyan
            : SongSelectSurface.Ivory();
        icon.Colour = open
            ? HomeControlColours.Pink
            : HomeControlColours.Navy;
        card.BorderColour = open
            ? HomeControlColours.Yellow
            : HomeControlColours.Navy;
        topAccent.Colour = open
            ? HomeControlColours.Yellow
            : HomeControlColours.Pink;
    }

    protected override bool OnHover(HoverEvent e)
    {
        this.ScaleTo(1.045f, 110, Easing.OutQuint);
        card.BorderColour = HomeControlColours.Yellow;
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        this.ScaleTo(1, 130, Easing.OutQuint);
        SetOpen(open);
    }
}
