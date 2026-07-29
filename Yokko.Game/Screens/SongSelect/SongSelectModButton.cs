using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osuTK;
using osuTK.Graphics;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Screens.SongSelect;

internal partial class SongSelectModButton : ClickableContainer
{
    private readonly Color4 accent;
    private readonly Box background;
    private readonly SpriteText label;

    public SongSelectModButton(
        string acronym,
        Color4 accent,
        Action action)
    {
        this.accent = accent;
        Action = action;
        Size = new Vector2(54, 36);
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
                Text = acronym,
                Font = HomeTypography.Display(16),
            },
        };

        SetSelected(false);
    }

    public void SetSelected(bool selected)
    {
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
}

internal partial class SongSelectModsToggleButton : ClickableContainer
{
    private readonly Box background;
    private readonly Box topAccent;
    private readonly SpriteIcon icon;
    private readonly Circle badge;
    private readonly SpriteText countLabel;
    private bool open;
    private int count;

    public SongSelectModsToggleButton(Action action)
    {
        Action = action;
        Size = new Vector2(54, 48);
        Masking = true;
        CornerRadius = 9;
        BorderThickness = 1.5f;
        BorderColour = SongSelectTheme.Cyan;

        InternalChildren = new Drawable[]
        {
            background = new Box
            {
                RelativeSizeAxes = Axes.Both,
            },
            topAccent = new Box
            {
                RelativeSizeAxes = Axes.X,
                Height = 3,
                Colour = SongSelectTheme.Pink,
            },
            icon = new SpriteIcon
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Position = new Vector2(0, 1),
                Size = new Vector2(22),
                Icon = FontAwesome.Solid.SlidersH,
            },
            badge = new Circle
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                Position = new Vector2(-5, 6),
                Size = new Vector2(16),
                BorderThickness = 1,
                BorderColour = SongSelectTheme.DeepNavy,
            },
            countLabel = new SpriteText
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.Centre,
                Position = new Vector2(-13, 14),
                Font = HomeTypography.Display(9),
            },
        };

        SetCount(0);
        SetOpen(false);
    }

    public void SetCount(int value)
    {
        count = Math.Max(0, value);
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
            ? new Color4(
                SongSelectTheme.Navy.R,
                SongSelectTheme.Navy.G,
                SongSelectTheme.Navy.B,
                0.98f)
            : new Color4(
                SongSelectTheme.Navy.R,
                SongSelectTheme.Navy.G,
                SongSelectTheme.Navy.B,
                0.82f);
        icon.Colour = open
            ? SongSelectTheme.Yellow
            : SongSelectTheme.Cyan;
        BorderColour = open
            ? SongSelectTheme.Yellow
            : SongSelectTheme.Cyan;
        topAccent.Colour = open
            ? SongSelectTheme.Yellow
            : SongSelectTheme.Pink;
    }

    protected override bool OnHover(HoverEvent e)
    {
        this.ScaleTo(1.06f, 110, Easing.OutQuint);
        BorderColour = SongSelectTheme.Yellow;
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        this.ScaleTo(1, 130, Easing.OutQuint);
        SetOpen(open);
    }
}
