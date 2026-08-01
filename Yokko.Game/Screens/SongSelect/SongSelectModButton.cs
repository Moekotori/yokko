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
    private readonly SpriteIcon icon;
    private readonly Circle badge;
    private readonly SpriteText countLabel;
    private bool open;

    public SongSelectModsToggleButton(
        Action action,
        Texture diamondTexture)
    {
        Action = action;
        Size = new Vector2(176, 82);
        Masking = true;
        CornerRadius = 10;
        BorderThickness = 1.25f;

        InternalChildren = new Drawable[]
        {
            background = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = SongSelectSurface.Ivory(0.99f),
            },
            icon = new SpriteIcon
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Y = 14,
                Size = new Vector2(27),
                Icon = FontAwesome.Solid.SlidersH,
                Colour = HomeControlColours.Navy,
            },
            new Sprite
            {
                Position = new Vector2(8, 5),
                Size = new Vector2(22),
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
                Font = HomeTypography.Display(11),
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
            : SongSelectSurface.Ivory();
        icon.Colour = open
            ? HomeControlColours.Pink
            : HomeControlColours.Navy;
        BorderColour = open
            ? HomeControlColours.Yellow
            : new Color4(
                SongSelectTheme.Navy.R,
                SongSelectTheme.Navy.G,
                SongSelectTheme.Navy.B,
                0.24f);
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
        this.ScaleTo(1.025f, 110, Easing.OutQuint);
        BorderColour = HomeControlColours.Yellow;
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        this.ScaleTo(1, 130, Easing.OutQuint);
        SetOpen(open);
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
                Font = HomeTypography.Display(9),
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
