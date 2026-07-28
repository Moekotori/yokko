using System;
using System.Collections.Generic;
using System.Drawing;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osuTK;
using osuTK.Graphics;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Screens.Settings;

internal static class SettingsTheme
{
    public static readonly Color4 MutedNavy = new(0.34f, 0.39f, 0.64f, 1f);
    public static readonly Color4 Divider = new(0.12f, 0.22f, 0.55f, 0.18f);
    public static readonly Color4 StatusCyan = new(0.36f, 0.84f, 0.96f, 1f);
    public static readonly Color4 PaleCyan = new(0.87f, 0.98f, 1f, 1f);
}

public partial class SettingsSearchTextBox : BasicTextBox
{
    protected override float LeftRightPadding => 42;

    public SettingsSearchTextBox()
    {
        Size = new Vector2(244, 44);
        Masking = true;
        CornerRadius = 7;
        BorderThickness = 1.2f;
        BorderColour = SettingsTheme.MutedNavy;
        BackgroundUnfocused = Color4.White;
        BackgroundFocused = SettingsTheme.PaleCyan;
        FontSize = 15;
        PlaceholderText = "Search settings";

        AddInternal(new SpriteIcon
        {
            Anchor = Anchor.CentreLeft,
            Origin = Anchor.CentreLeft,
            X = 15,
            Size = new Vector2(17),
            Icon = FontAwesome.Solid.Search,
            Colour = SettingsTheme.MutedNavy,
            Depth = -2,
        });
    }

    protected override Drawable GetDrawableCharacter(char c) => new SpriteText
    {
        Text = c.ToString(),
        Font = HomeTypography.Body(15),
        Colour = HomeControlColours.Navy,
    };

    protected override SpriteText CreatePlaceholder() => new SpriteText
    {
        Font = HomeTypography.Body(15),
        Colour = SettingsTheme.MutedNavy,
    };

    protected override void OnFocus(FocusEvent e)
    {
        base.OnFocus(e);
        BorderColour = HomeControlColours.Cyan;
        BorderThickness = 2;
    }

    protected override void OnFocusLost(FocusLostEvent e)
    {
        base.OnFocusLost(e);
        BorderColour = SettingsTheme.MutedNavy;
        BorderThickness = 1.2f;
    }
}

public partial class SettingsOutlineButton : ClickableContainer
{
    private readonly Box background;

    public SettingsOutlineButton(string label, IconUsage icon, Action action)
    {
        Action = action;
        Size = new Vector2(244, 44);
        Masking = true;
        CornerRadius = 7;
        BorderThickness = 1.2f;
        BorderColour = SettingsTheme.MutedNavy;

        InternalChildren = new Drawable[]
        {
            background = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = Color4.White,
            },
            new SpriteIcon
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 16,
                Size = new Vector2(17),
                Icon = icon,
                Colour = HomeControlColours.Navy,
            },
            new SpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 51,
                Text = label,
                Font = HomeTypography.Display(16),
                Colour = HomeControlColours.Navy,
            },
        };
    }

    protected override bool OnHover(HoverEvent e)
    {
        background.FadeColour(SettingsTheme.PaleCyan, 120, Easing.OutQuint);
        this.MoveToX(DrawPosition.X + 2, 120, Easing.OutQuint);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        background.FadeColour(Color4.White, 140, Easing.OutQuint);
        this.MoveToX(38, 140, Easing.OutQuint);
    }
}

public partial class SettingsNavHeader : CompositeDrawable
{
    public SettingsNavHeader(string label)
    {
        Size = new Vector2(252, 22);
        InternalChild = new SpriteText
        {
            Anchor = Anchor.BottomLeft,
            Origin = Anchor.BottomLeft,
            X = 8,
            Text = label,
            Font = HomeTypography.Display(11),
            Spacing = new Vector2(1.3f, 0),
            Colour = new Color4(SettingsTheme.MutedNavy.R, SettingsTheme.MutedNavy.G, SettingsTheme.MutedNavy.B, 0.75f),
        };
    }

    public void SetFiltered(bool visible)
    {
        if (visible)
            Show();
        else
            Hide();
    }
}

public partial class SettingsNavItem : ClickableContainer
{
    private readonly Box background;
    private readonly SpriteIcon icon;
    private readonly SpriteText text;
    private readonly SpriteIcon plus;
    private readonly bool selected;

    public string Label { get; }

    public SettingsNavItem(string label, IconUsage itemIcon, bool selected)
    {
        Label = label;
        this.selected = selected;
        Size = new Vector2(252, 39);
        Masking = true;
        CornerRadius = 7;

        InternalChildren = new Drawable[]
        {
            background = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = selected ? HomeControlColours.Navy : Color4.Transparent,
            },
            new Box
            {
                RelativeSizeAxes = Axes.Y,
                Width = 5,
                Colour = HomeControlColours.Cyan,
                Alpha = selected ? 1 : 0,
            },
            icon = new SpriteIcon
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 22,
                Size = new Vector2(18),
                Icon = itemIcon,
                Colour = selected ? Color4.White : HomeControlColours.Navy,
            },
            text = new SpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 57,
                Text = label,
                Font = HomeTypography.Display(16),
                Colour = selected ? Color4.White : HomeControlColours.Navy,
            },
            plus = new SpriteIcon
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                X = -17,
                Size = new Vector2(12),
                Icon = FontAwesome.Solid.Plus,
                Colour = selected ? HomeControlColours.Yellow : HomeControlColours.Pink,
            },
            new Box
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.Centre,
                Size = new Vector2(14),
                Rotation = 45,
                Colour = HomeControlColours.Yellow,
                Alpha = selected ? 1 : 0,
            },
        };
    }

    public void SetFiltered(bool visible)
    {
        if (visible)
            Show();
        else
            Hide();
    }

    protected override bool OnHover(HoverEvent e)
    {
        if (!selected)
        {
            background.FadeColour(SettingsTheme.PaleCyan, 120, Easing.OutQuint);
            icon.FadeColour(HomeControlColours.Cyan, 120, Easing.OutQuint);
            plus.RotateTo(90, 120, Easing.OutQuint);
        }
        else
        {
            background.FadeColour(new Color4(0.055f, 0.15f, 0.7f, 1f), 120, Easing.OutQuint);
        }

        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        background.FadeColour(selected ? HomeControlColours.Navy : Color4.Transparent, 140, Easing.OutQuint);
        icon.FadeColour(selected ? Color4.White : HomeControlColours.Navy, 140, Easing.OutQuint);
        plus.RotateTo(0, 140, Easing.OutQuint);
    }
}

public partial class SettingsSegmentedChoiceButton : ClickableContainer
{
    private readonly Box background;
    private readonly SpriteIcon icon;
    private readonly SpriteText text;
    private readonly SpriteIcon check;
    private bool selected;

    public object Value { get; init; }

    public SettingsSegmentedChoiceButton(string label, IconUsage itemIcon, Action action, float width)
    {
        Action = action;
        Size = new Vector2(width, 54);

        InternalChildren = new Drawable[]
        {
            background = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = Color4.White,
            },
            new Box
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                Width = 1,
                RelativeSizeAxes = Axes.Y,
                Colour = SettingsTheme.Divider,
            },
            icon = new SpriteIcon
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 28,
                Size = new Vector2(16),
                Icon = itemIcon,
                Colour = HomeControlColours.Navy,
            },
            text = new SpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 59,
                Text = label,
                Font = HomeTypography.Body(16),
                Colour = HomeControlColours.Navy,
            },
            check = new SpriteIcon
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                X = -22,
                Size = new Vector2(17),
                Icon = FontAwesome.Solid.Check,
                Colour = HomeControlColours.Yellow,
                Alpha = 0,
            },
        };
    }

    public void SetSelected(bool isSelected)
    {
        selected = isSelected;
        background.FadeColour(selected ? HomeControlColours.Navy : Color4.White, 120, Easing.OutQuint);
        icon.FadeColour(selected ? Color4.White : HomeControlColours.Navy, 120, Easing.OutQuint);
        text.FadeColour(selected ? Color4.White : HomeControlColours.Navy, 120, Easing.OutQuint);
        check.FadeTo(selected ? 1 : 0, 120, Easing.OutQuint);
    }

    protected override bool OnHover(HoverEvent e)
    {
        if (!selected)
            background.FadeColour(SettingsTheme.PaleCyan, 120, Easing.OutQuint);

        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        if (!selected)
            background.FadeColour(Color4.White, 140, Easing.OutQuint);
    }
}

public partial class SettingsResolutionButton : ClickableContainer
{
    private readonly IReadOnlyList<Size> options;
    private readonly Action<Size> onSelected;
    private readonly Box background;
    private readonly SpriteText valueText;
    private readonly SpriteIcon chevron;
    private Size selected;

    public SettingsResolutionButton(IReadOnlyList<Size> options, Action<Size> onSelected)
    {
        this.options = options;
        this.onSelected = onSelected;
        Action = selectNext;
        Size = new Vector2(598, 54);
        Masking = true;
        CornerRadius = 7;
        BorderThickness = 1.4f;
        BorderColour = HomeControlColours.Navy;

        InternalChildren = new Drawable[]
        {
            background = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = Color4.White,
            },
            valueText = new SpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 18,
                Font = HomeTypography.Body(17),
                Colour = HomeControlColours.Navy,
            },
            chevron = new SpriteIcon
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                X = -20,
                Size = new Vector2(15),
                Icon = FontAwesome.Solid.ChevronDown,
                Colour = HomeControlColours.Pink,
            },
        };
    }

    public void SetSelected(Size size)
    {
        selected = size;
        valueText.Text = $"{size.Width} × {size.Height}";
    }

    private void selectNext()
    {
        int currentIndex = -1;

        for (int i = 0; i < options.Count; i++)
        {
            if (options[i] == selected)
            {
                currentIndex = i;
                break;
            }
        }

        Size next = options[(currentIndex + 1 + options.Count) % options.Count];
        onSelected(next);
    }

    protected override bool OnHover(HoverEvent e)
    {
        background.FadeColour(SettingsTheme.PaleCyan, 120, Easing.OutQuint);
        chevron.MoveToY(3, 120, Easing.OutQuint);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        background.FadeColour(Color4.White, 140, Easing.OutQuint);
        chevron.MoveToY(0, 140, Easing.OutQuint);
    }
}
