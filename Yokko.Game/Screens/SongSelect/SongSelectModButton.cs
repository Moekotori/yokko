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
    private readonly Container card;
    private readonly Box background;
    private readonly Box topAccent;
    private readonly SpriteIcon icon;
    private readonly Circle badge;
    private readonly SpriteText countLabel;
    private bool open;

    public SongSelectModsToggleButton(Action action)
    {
        Action = action;
        Size = new Vector2(88, 108);

        InternalChildren = new Drawable[]
        {
            new Container
            {
                Position = new Vector2(8.5f, 17.5f),
                Size = new Vector2(75),
                Masking = true,
                CornerRadius = 8,
                Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new Color4(
                        HomeControlColours.Navy.R,
                        HomeControlColours.Navy.G,
                        HomeControlColours.Navy.B,
                        0.34f),
                },
            },
            card = new Container
            {
                Position = new Vector2(6.5f, 13.5f),
                Size = new Vector2(75),
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
                        Size = new Vector2(61),
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
                        Width = 27,
                        Height = 3,
                        Colour = HomeControlColours.Pink,
                    },
                },
            },
            new Box
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.Centre,
                Position = new Vector2(0, 7),
                Size = new Vector2(17),
                Rotation = 45,
                Colour = HomeControlColours.Yellow,
            },
            badge = new Circle
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                Position = new Vector2(-9, 13),
                Size = new Vector2(16),
                BorderThickness = 1,
                BorderColour = HomeControlColours.Navy,
            },
            countLabel = new SpriteText
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.Centre,
                Position = new Vector2(-17, 21),
                Font = HomeTypography.Display(9),
            },
            new SpriteText
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Y = 92,
                Text = "MODS",
                Font = HomeTypography.Display(10),
                Spacing = new Vector2(1.4f, 0),
                Colour = HomeControlColours.Navy,
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
            : Color4.White;
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
