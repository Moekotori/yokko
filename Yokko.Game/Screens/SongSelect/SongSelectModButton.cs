using System;
using System.Linq;
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
    private readonly Box background;
    private readonly Box bottomAccent;
    private readonly Container iconTile;
    private readonly SpriteIcon icon;
    private readonly Sprite diamond;
    private readonly Circle badge;
    private readonly SpriteText countLabel;
    private bool open;

    public SongSelectModsToggleButton(
        Action action,
        Texture diamondTexture)
    {
        Action = action;
        Size = new Vector2(154, 82);
        BorderThickness = 0;

        InternalChildren = new Drawable[]
        {
            background = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = SongSelectSurface.Ivory(0.99f),
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
                BorderColour = new Color4(
                    HomeControlColours.Pink.R,
                    HomeControlColours.Pink.G,
                    HomeControlColours.Pink.B,
                    0.38f),
                Children =
                [
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = new Color4(
                            HomeControlColours.Pink.R,
                            HomeControlColours.Pink.G,
                            HomeControlColours.Pink.B,
                            0.08f),
                    },
                    icon = new SpriteIcon
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Size = new Vector2(23),
                        Icon = FontAwesome.Solid.SlidersH,
                        Colour = HomeControlColours.Navy,
                    },
                ],
            },
            diamond = new Sprite
            {
                Position = new Vector2(7, 4),
                Size = new Vector2(24),
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
                Anchor = Anchor.BottomCentre,
                Origin = Anchor.BottomCentre,
                Y = -11,
                Text = "MODS",
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
                Colour = HomeControlColours.Pink,
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
            : Color4.Transparent;
        icon.Colour = open
            ? HomeControlColours.Pink
            : HomeControlColours.Navy;
        iconTile.BorderColour = open
            ? HomeControlColours.Yellow
            : new Color4(
                HomeControlColours.Pink.R,
                HomeControlColours.Pink.G,
                HomeControlColours.Pink.B,
                0.38f);
        BorderColour = open
            ? HomeControlColours.Yellow
            : Color4.Transparent;
        bottomAccent.Colour = open
            ? HomeControlColours.Yellow
            : HomeControlColours.Pink;
    }

    protected override bool OnHover(HoverEvent e)
    {
        background.FadeColour(
            HomeControlColours.PaleCyan,
            110,
            Easing.OutQuint);
        iconTile.RotateTo(-5, 130, Easing.OutQuint);
        icon.RotateTo(9, 150, Easing.OutQuint);
        diamond.RotateTo(12, 160, Easing.OutQuint);
        bottomAccent.ResizeWidthTo(64, 150, Easing.OutQuint);
        this.ScaleTo(1.025f, 110, Easing.OutQuint);
        BorderColour = HomeControlColours.Yellow;
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        this.ScaleTo(1, 130, Easing.OutQuint);
        iconTile.RotateTo(0, 180, Easing.OutQuint);
        icon.RotateTo(0, 200, Easing.OutQuint);
        diamond.RotateTo(0, 220, Easing.OutQuint);
        bottomAccent.ResizeWidthTo(36, 150, Easing.OutQuint);
        SetOpen(open);
    }

    protected override bool OnMouseDown(MouseDownEvent e)
    {
        this.ScaleTo(0.97f, 70, Easing.OutQuint);
        return base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseUpEvent e)
    {
        this.ScaleTo(IsHovered ? 1.025f : 1, 180, Easing.OutBack);
        base.OnMouseUp(e);
    }
}

internal partial class SongSelectSelectedModsButton : ClickableContainer
{
    private readonly Box background;
    private readonly Box accentRail;
    private readonly Circle countBadge;
    private readonly SpriteText countText;
    private readonly SpriteText summaryText;
    private int count;

    internal int ActiveModCount => count;
    internal string Summary => summaryText.Text.ToString();

    internal SongSelectSelectedModsButton(
        Action action,
        ManiaModSet mods)
    {
        Action = action;
        Size = new Vector2(154, 40);
        Masking = true;
        CornerRadius = 7;
        BorderThickness = 1;

        InternalChildren =
        [
            background = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = SongSelectSurface.Ivory(0.98f),
            },
            accentRail = new Box
            {
                Anchor = Anchor.BottomCentre,
                Origin = Anchor.BottomCentre,
                RelativeSizeAxes = Axes.X,
                Height = 3,
                Colour = SongSelectTheme.Cyan,
            },
            new SpriteIcon
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 11,
                Size = new Vector2(12),
                Icon = FontAwesome.Solid.SlidersH,
                Colour = SongSelectTheme.Cyan,
            },
            new SpriteText
            {
                Position = new Vector2(29, 6),
                Text = "SELECTED MODS",
                Font = HomeTypography.Display(8),
                Colour = new Color4(
                    SongSelectTheme.Navy.R,
                    SongSelectTheme.Navy.G,
                    SongSelectTheme.Navy.B,
                    0.62f),
            },
            summaryText = new SpriteText
            {
                Position = new Vector2(29, 21),
                Width = 91,
                Truncate = true,
                Font = HomeTypography.Control(14),
                Colour = SongSelectTheme.Navy,
            },
            countBadge = new Circle
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                X = -9,
                Size = new Vector2(18),
                BorderThickness = 1,
                BorderColour = SongSelectTheme.Navy,
            },
            countText = new SpriteText
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.Centre,
                Position = new Vector2(-18, 0),
                Font = HomeTypography.Display(8),
            },
        ];

        SetState(mods);
    }

    internal void SetState(ManiaModSet mods)
    {
        count = mods?.Mods.Count ?? 0;
        summaryText.Text = count == 0
            ? "NONE"
            : string.Join(" · ", mods.DisplayLabels.Take(3));
        summaryText.Colour = count == 0
            ? SongSelectTheme.Navy
            : SongSelectTheme.Pink;
        countText.Text = count.ToString();
        countBadge.Colour = count == 0
            ? SongSelectTheme.PaleCyan
            : SongSelectTheme.Pink;
        countText.Colour = count == 0
            ? SongSelectTheme.Navy
            : SongSelectTheme.Ivory;
        accentRail.Colour = count == 0
            ? SongSelectTheme.Cyan
            : SongSelectTheme.Pink;
        BorderColour = new Color4(
            SongSelectTheme.Navy.R,
            SongSelectTheme.Navy.G,
            SongSelectTheme.Navy.B,
            0.24f);
    }

    protected override bool OnHover(HoverEvent e)
    {
        background.FadeColour(
            SongSelectTheme.PaleCyan,
            100,
            Easing.OutQuint);
        this.ScaleTo(1.02f, 100, Easing.OutQuint);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        background.FadeColour(
            SongSelectSurface.Ivory(0.98f),
            120,
            Easing.OutQuint);
        this.ScaleTo(1, 120, Easing.OutQuint);
    }
}
