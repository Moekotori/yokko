using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;
using Yokko.Core.Difficulty;
using Yokko.Core.Gameplay;
using Yokko.Core.Scoring;
using Yokko.Game.Localisation;
using Yokko.Game.Presentation;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Screens.SongSelect;

internal partial class SongSelectSearchBox : BasicTextBox
{
    private readonly Action<string> queryChanged;
    private readonly Action escapePressed;
    private readonly Action submitPressed;
    private readonly Func<KeyDownEvent, bool> commandPressed;
    private readonly Box focusRail;
    private readonly SpriteText escapeHint;
    private bool holdFocus = true;

    protected override float LeftRightPadding => 48;
    public override bool HandleNonPositionalInput =>
        holdFocus || base.HandleNonPositionalInput;
    public override bool RequestsFocus => holdFocus;
    internal bool HoldFocus => holdFocus;

    public SongSelectSearchBox(
        Action<string> queryChanged,
        Action escapePressed,
        Action submitPressed,
        Func<KeyDownEvent, bool> commandPressed)
    {
        this.queryChanged = queryChanged;
        this.escapePressed = escapePressed;
        this.submitPressed = submitPressed;
        this.commandPressed = commandPressed;
        Size = new Vector2(206, 44);
        Masking = true;
        CornerRadius = 10;
        BorderThickness = 1.25f;
        BorderColour = new Color4(
            SongSelectTheme.Cyan.R,
            SongSelectTheme.Cyan.G,
            SongSelectTheme.Cyan.B,
            0.56f);
        BackgroundUnfocused = SongSelectSurface.Ivory(0.98f);
        BackgroundFocused = SongSelectSurface.Ivory(0.995f);
        FontSize = 17;
        PlaceholderText = YokkoStrings.Get("song_select.search");

        AddInternal(new Container
        {
            Anchor = Anchor.CentreLeft,
            Origin = Anchor.CentreLeft,
            X = 7,
            Size = new Vector2(31),
            Depth = -2,
            Masking = true,
            CornerRadius = 5,
            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new Color4(
                        SongSelectTheme.Cyan.R,
                        SongSelectTheme.Cyan.G,
                        SongSelectTheme.Cyan.B,
                        0.14f),
                },
                new SpriteIcon
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Size = new Vector2(15),
                    Icon = FontAwesome.Solid.Search,
                    Colour = SongSelectTheme.Cyan,
                },
            },
        });

        AddInternal(escapeHint = new SpriteText
        {
            Anchor = Anchor.CentreRight,
            Origin = Anchor.CentreRight,
            X = -13,
            Text = "ESC",
            Font = HomeTypography.Display(10),
            Colour = new Color4(
                SongSelectTheme.Navy.R,
                SongSelectTheme.Navy.G,
                SongSelectTheme.Navy.B,
                0.48f),
            Depth = -2,
        });

        AddInternal(focusRail = new Box
        {
            Anchor = Anchor.BottomLeft,
            Origin = Anchor.BottomLeft,
            RelativeSizeAxes = Axes.X,
            Height = 2,
            Colour = SongSelectTheme.Cyan,
            Alpha = 0.42f,
            Depth = -2,
        });

        Current.ValueChanged += onValueChanged;
    }

    internal void SetHoldFocus(bool value)
    {
        holdFocus = value;
        if (value)
            TakeFocus();
        else if (HasFocus)
            GetContainingFocusManager()?.ChangeFocus(null);
    }

    internal void TakeFocus()
    {
        if (!holdFocus)
            return;

        Scheduler.Add(() => GetContainingFocusManager()?.ChangeFocus(this));
    }

    private void onValueChanged(ValueChangedEvent<string> change)
    {
        escapeHint.Colour = change.NewValue.Length > 0
            ? SongSelectTheme.Pink
            : new Color4(
                SongSelectTheme.PaleCyan.R,
                SongSelectTheme.PaleCyan.G,
                SongSelectTheme.PaleCyan.B,
                0.48f);
        queryChanged(change.NewValue);
    }

    protected override Drawable GetDrawableCharacter(char c) => new SpriteText
    {
        Text = c.ToString(),
        Font = HomeTypography.Body(17),
        Colour = SongSelectTheme.Navy,
    };

    protected override SpriteText CreatePlaceholder() => new()
    {
        Font = HomeTypography.Body(17),
        Colour = new Color4(
            SongSelectTheme.Navy.R,
            SongSelectTheme.Navy.G,
            SongSelectTheme.Navy.B,
            0.58f),
    };

    protected override void OnFocus(FocusEvent e)
    {
        base.OnFocus(e);
        BorderColour = SongSelectTheme.Cyan;
        BorderThickness = 2;
        focusRail.FadeTo(1, 120, Easing.OutQuint);
        escapeHint.FadeTo(0.9f, 120, Easing.OutQuint);
    }

    protected override void OnFocusLost(FocusLostEvent e)
    {
        base.OnFocusLost(e);
        BorderColour = new Color4(
            SongSelectTheme.Cyan.R,
            SongSelectTheme.Cyan.G,
            SongSelectTheme.Cyan.B,
            0.56f);
        BorderThickness = 1.4f;
        focusRail.FadeTo(0.42f, 140, Easing.OutQuint);
        escapeHint.FadeTo(0.68f, 140, Easing.OutQuint);
    }

    protected override bool OnKeyDown(KeyDownEvent e)
    {
        if (!HasFocus)
            return false;
        if (ImeCompositionActive)
            return base.OnKeyDown(e);

        switch (e.Key)
        {
            case Key.Escape:
                escapePressed();
                return true;

            case Key.Enter:
            case Key.KeypadEnter:
                submitPressed();
                return true;

            case Key.Up:
            case Key.Down:
            case Key.PageUp:
            case Key.PageDown:
            case Key.Home:
            case Key.End:
            case Key.F1:
            case Key.F2:
            case Key.F3:
            case Key.F6:
            case Key.F5:
                return commandPressed(e);

            case Key.Left when Current.Value.Length == 0:
            case Key.Right when Current.Value.Length == 0:
                return commandPressed(e);

            case Key.BackSpace when Current.Value.Length == 0:
                return commandPressed(e);

            case Key.F when e.ControlPressed:
            case Key.Plus when e.ControlPressed:
            case Key.KeypadPlus when e.ControlPressed:
            case Key.Minus when e.ControlPressed:
            case Key.KeypadMinus when e.ControlPressed:
                return commandPressed(e);

            default:
                return base.OnKeyDown(e);
        }
    }
}

internal partial class SongSelectKeyModeFilterButton : ClickableContainer
{
    private readonly Box background;
    private readonly SpriteText valueText;
    private readonly Box selectionRail;

    internal string DisplayedValue => valueText.Text.ToString();
    internal float SelectionRailAlpha => selectionRail.Alpha;

    public SongSelectKeyModeFilterButton(Action action)
    {
        Action = action;
        Size = new Vector2(130, 48);
        Masking = true;
        CornerRadius = 10;
        BorderThickness = 1.25f;
        BorderColour = new Color4(
            SongSelectTheme.Cyan.R,
            SongSelectTheme.Cyan.G,
            SongSelectTheme.Cyan.B,
            0.58f);
        InternalChildren = new Drawable[]
        {
            background = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = SongSelectSurface.Ivory(0.98f),
            },
            new Container
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 8,
                Size = new Vector2(32),
                Masking = true,
                CornerRadius = 7,
                Children =
                [
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = SongSelectTheme.Cyan,
                        Alpha = 0.16f,
                    },
                    new SpriteIcon
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Size = new Vector2(15),
                        Icon = FontAwesome.Solid.Keyboard,
                        Colour = SongSelectTheme.Cyan,
                    },
                ],
            },
            new SpriteText
            {
                Position = new Vector2(49, 8),
                Text = YokkoStrings.Get("song_select.key_mode"),
                Font = HomeTypography.Display(9),
                Colour = new Color4(
                    SongSelectTheme.Navy.R,
                    SongSelectTheme.Navy.G,
                    SongSelectTheme.Navy.B,
                    0.56f),
            },
            valueText = new SpriteText
            {
                Position = new Vector2(49, 22),
                Text = YokkoStrings.Get("song_select.all"),
                Font = HomeTypography.Control(16),
                Colour = SongSelectTheme.Navy,
            },
            new SpriteIcon
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                X = -10,
                Size = new Vector2(9),
                Icon = FontAwesome.Solid.SyncAlt,
                Colour = SongSelectTheme.Pink,
                Alpha = 0.82f,
            },
            selectionRail = new Box
            {
                Anchor = Anchor.BottomLeft,
                Origin = Anchor.BottomLeft,
                X = 48,
                Width = 42,
                Height = 3,
                Colour = SongSelectTheme.Pink,
            },
        };
    }

    internal void SetMode(KeyMode? mode)
    {
        valueText.Text = mode switch
        {
            KeyMode.FourKey => "4K",
            KeyMode.SevenKey => "7K",
            _ => YokkoStrings.Get("song_select.all"),
        };
        valueText.Colour = mode.HasValue
            ? SongSelectTheme.Pink
            : SongSelectTheme.Navy;
        selectionRail.Width = mode.HasValue ? 26 : 42;
    }

    protected override bool OnHover(HoverEvent e)
    {
        this.ScaleTo(1.012f, 110, Easing.OutQuint);
        background.FadeColour(
            Color4.White,
            110,
            Easing.OutQuint);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        this.ScaleTo(1, 130, Easing.OutQuint);
        background.FadeColour(
            SongSelectSurface.Ivory(0.98f),
            130,
            Easing.OutQuint);
    }
}

internal partial class SongSelectBrowseToolButton : ClickableContainer
{
    private readonly Box background;
    private readonly Box activeRail;
    private readonly SpriteText valueText;
    private readonly bool interactive;
    private bool active;

    internal bool Active => active;
    internal bool Interactive => interactive;
    internal string DisplayedValue => valueText.Text.ToString();
    public override bool AcceptsFocus => interactive;

    public SongSelectBrowseToolButton(
        LocalisableString label,
        LocalisableString value,
        float width,
        IconUsage icon,
        Action action,
        bool interactive = true,
        bool showChevron = true)
    {
        this.interactive = interactive;
        Action = interactive ? action : null;
        Size = new Vector2(width, 40);
        Masking = true;
        CornerRadius = 8;
        BorderThickness = 1;
        BorderColour = new Color4(
            SongSelectTheme.Cyan.R,
            SongSelectTheme.Cyan.G,
            SongSelectTheme.Cyan.B,
            0.22f);

        InternalChildren =
        [
            background = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = SongSelectSurface.Ivory(0.96f),
            },
            new Container
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 8,
                Size = new Vector2(28),
                Masking = true,
                CornerRadius = 8,
                Children =
                [
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = new Color4(
                            SongSelectTheme.Cyan.R,
                            SongSelectTheme.Cyan.G,
                            SongSelectTheme.Cyan.B,
                            0.13f),
                    },
                    new SpriteIcon
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Size = new Vector2(13),
                        Icon = icon,
                        Colour = SongSelectTheme.Cyan,
                    },
                ],
            },
            new SpriteText
            {
                Position = new Vector2(48, 4),
                Text = label,
                Font = HomeTypography.Display(8),
                Colour = new Color4(
                    SongSelectTheme.Navy.R,
                    SongSelectTheme.Navy.G,
                    SongSelectTheme.Navy.B,
                    0.62f),
            },
            valueText = new SpriteText
            {
                Position = new Vector2(48, 17),
                Width = width - 78,
                Truncate = true,
                Text = value,
                Font = HomeTypography.Control(14),
                Colour = SongSelectTheme.Navy,
            },
            new SpriteIcon
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                X = -12,
                Size = new Vector2(9),
                Icon = FontAwesome.Solid.ChevronDown,
                Colour = SongSelectTheme.Cyan,
                Alpha = showChevron ? 1 : 0,
            },
            activeRail = new Box
            {
                Anchor = Anchor.BottomLeft,
                Origin = Anchor.BottomLeft,
                X = 48,
                Width = 46,
                Height = 2,
                Colour = SongSelectTheme.Pink,
                Alpha = 0,
            },
        ];
    }

    public void SetValue(LocalisableString value) => valueText.Text = value;

    public void SetActive(bool value)
    {
        active = value;
        background.Colour = active
            ? new Color4(
                SongSelectTheme.PaleCyan.R,
                SongSelectTheme.PaleCyan.G,
                SongSelectTheme.PaleCyan.B,
                0.78f)
            : SongSelectSurface.Ivory(0.96f);
        activeRail.Alpha = active ? 1 : 0;
    }

    protected override bool OnKeyDown(KeyDownEvent e)
    {
        if (interactive && e.Key is Key.Enter or Key.Space)
        {
            TriggerClick();
            return true;
        }

        return base.OnKeyDown(e);
    }

    protected override void OnFocus(FocusEvent e)
    {
        base.OnFocus(e);
        BorderThickness = 2;
        BorderColour = SongSelectTheme.Pink;
    }

    protected override void OnFocusLost(FocusLostEvent e)
    {
        base.OnFocusLost(e);
        BorderThickness = 1;
        BorderColour = new Color4(
            SongSelectTheme.Cyan.R,
            SongSelectTheme.Cyan.G,
            SongSelectTheme.Cyan.B,
            0.22f);
    }

    protected override bool OnHover(HoverEvent e)
    {
        if (!interactive)
            return false;

        background.FadeColour(
            new Color4(
                SongSelectTheme.PaleCyan.R,
                SongSelectTheme.PaleCyan.G,
                SongSelectTheme.PaleCyan.B,
                0.72f),
            110,
            Easing.OutQuint);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        if (!interactive)
            return;

        background.FadeColour(
            active
                ? new Color4(
                    SongSelectTheme.PaleCyan.R,
                    SongSelectTheme.PaleCyan.G,
                    SongSelectTheme.PaleCyan.B,
                    0.78f)
                : SongSelectSurface.Ivory(0.96f),
            130,
            Easing.OutQuint);
    }
}

internal partial class SongSelectSortPopover : CompositeDrawable
{
    private readonly IReadOnlyDictionary<
        SongSelectSortMode,
        SongSelectSortOptionButton> optionButtons;
    private readonly SongSelectSortDirectionButton ascendingButton;
    private readonly SongSelectSortDirectionButton descendingButton;
    private readonly IReadOnlyList<ClickableContainer> focusableButtons;
    private SongSelectSortMode mode;
    private SongSelectSortDirection direction;

    internal bool IsOpen { get; private set; }
    internal SongSelectSortMode Mode => mode;
    internal SongSelectSortDirection Direction => direction;

    internal SongSelectSortPopover(
        Action<SongSelectSortMode> modeSelected,
        Action<SongSelectSortDirection> directionSelected)
    {
        Size = new Vector2(500, 294);
        Masking = true;
        CornerRadius = 12;
        BorderThickness = 1;
        BorderColour = SongSelectSurface.Border(0.24f);
        Alpha = 0;

        var modes = Enum.GetValues<SongSelectSortMode>();
        var buttons = new Dictionary<
            SongSelectSortMode,
            SongSelectSortOptionButton>();
        foreach (SongSelectSortMode candidate in modes)
        {
            int index = (int)candidate;
            buttons[candidate] = new SongSelectSortOptionButton(
                candidate,
                () => modeSelected(candidate))
            {
                Position = new Vector2(
                    14 + index % 2 * 236,
                    62 + index / 2 * 40),
            };
        }
        optionButtons = buttons;

        InternalChildren =
        [
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = SongSelectSurface.Ivory(0.985f),
            },
            new Box
            {
                RelativeSizeAxes = Axes.X,
                Height = 50,
                Colour = new Color4(
                    SongSelectTheme.PaleCyan.R,
                    SongSelectTheme.PaleCyan.G,
                    SongSelectTheme.PaleCyan.B,
                    0.76f),
            },
            new SpriteIcon
            {
                Position = new Vector2(16, 17),
                Size = new Vector2(14),
                Icon = FontAwesome.Solid.SortAmountDown,
                Colour = SongSelectTheme.Cyan,
            },
            new SpriteText
            {
                Position = new Vector2(42, 7),
                Text = YokkoStrings.Get("song_select.sort.title"),
                Font = HomeTypography.Display(12),
                Colour = SongSelectTheme.Navy,
            },
            new SpriteText
            {
                Position = new Vector2(42, 27),
                Text = YokkoStrings.Get("song_select.sort.subtitle"),
                Font = HomeTypography.Body(9),
                Colour = new Color4(
                    SongSelectTheme.Navy.R,
                    SongSelectTheme.Navy.G,
                    SongSelectTheme.Navy.B,
                    0.58f),
            },
            .. buttons.Values,
            new Box
            {
                Position = new Vector2(14, 228),
                Size = new Vector2(472, 1),
                Colour = SongSelectSurface.Border(0.16f),
            },
            new SpriteText
            {
                Position = new Vector2(14, 252),
                Text = YokkoStrings.Get("song_select.sort.direction"),
                Font = HomeTypography.Display(8),
                Colour = new Color4(
                    SongSelectTheme.Navy.R,
                    SongSelectTheme.Navy.G,
                    SongSelectTheme.Navy.B,
                    0.62f),
            },
            ascendingButton = new SongSelectSortDirectionButton(
                YokkoStrings.Get("song_select.sort.ascending"),
                FontAwesome.Solid.ArrowUp,
                () => directionSelected(SongSelectSortDirection.Ascending))
            {
                Position = new Vector2(112, 240),
            },
            descendingButton = new SongSelectSortDirectionButton(
                YokkoStrings.Get("song_select.sort.descending"),
                FontAwesome.Solid.ArrowDown,
                () => directionSelected(SongSelectSortDirection.Descending))
            {
                Position = new Vector2(306, 240),
            },
        ];
        focusableButtons =
        [
            .. modes.Select(candidate => buttons[candidate]),
            ascendingButton,
            descendingButton,
        ];
    }

    internal void SetState(
        SongSelectSortMode value,
        SongSelectSortDirection newDirection)
    {
        mode = value;
        direction = newDirection;
        foreach ((SongSelectSortMode candidate, SongSelectSortOptionButton button)
                 in optionButtons)
        {
            button.SetSelected(candidate == mode);
        }
        ascendingButton.SetSelected(direction == SongSelectSortDirection.Ascending);
        descendingButton.SetSelected(direction == SongSelectSortDirection.Descending);
    }

    internal void Open()
    {
        IsOpen = true;
        this.ClearTransforms();
        this.FadeIn(120, Easing.OutQuint);
        Scheduler.AddDelayed(FocusSelected, 50);
    }

    internal void Close()
    {
        IsOpen = false;
        this.ClearTransforms();
        this.FadeOut(90, Easing.OutQuint);
    }

    internal void FocusSelected()
    {
        ClickableContainer target = optionButtons.TryGetValue(mode, out SongSelectSortOptionButton selected)
            ? selected
            : focusableButtons[0];
        GetContainingFocusManager()?.ChangeFocus(target);
    }

    internal bool HandleNavigation(Key key)
    {
        int index = focusedIndex();
        if (index < 0)
            index = Math.Max(0, Array.IndexOf(Enum.GetValues<SongSelectSortMode>(), mode));

        int next = index;
        switch (key)
        {
            case Key.Left:
                next = index switch
                {
                    9 => 8,
                    > 0 and < 8 when index % 2 == 1 => index - 1,
                    _ => index,
                };
                break;

            case Key.Right:
                next = index switch
                {
                    8 => 9,
                    >= 0 and < 7 when index % 2 == 0 => index + 1,
                    _ => index,
                };
                break;

            case Key.Up:
                next = index switch
                {
                    >= 8 => 6 + index - 8,
                    >= 2 => index - 2,
                    _ => index,
                };
                break;

            case Key.Down:
                next = index switch
                {
                    < 6 => index + 2,
                    6 => 8,
                    7 => 9,
                    _ => index,
                };
                break;

            case Key.Home:
                next = 0;
                break;

            case Key.End:
                next = focusableButtons.Count - 1;
                break;

            case Key.Enter:
            case Key.KeypadEnter:
            case Key.Space:
                focusableButtons[index].TriggerClick();
                return true;

            default:
                return false;
        }

        GetContainingFocusManager()?.ChangeFocus(focusableButtons[next]);
        return true;
    }

    private int focusedIndex()
    {
        for (int index = 0; index < focusableButtons.Count; index++)
        {
            if (focusableButtons[index].HasFocus)
                return index;
        }

        return -1;
    }
}

internal partial class SongSelectSortOptionButton : ClickableContainer
{
    private readonly Box background;
    private readonly SpriteIcon check;
    private bool selected;

    internal SongSelectSortMode Mode { get; }
    internal bool Selected => selected;
    public override bool AcceptsFocus => true;

    internal SongSelectSortOptionButton(
        SongSelectSortMode mode,
        Action action)
    {
        Mode = mode;
        Action = action;
        Size = new Vector2(222, 34);
        Masking = true;
        CornerRadius = 6;

        InternalChildren =
        [
            background = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = Color4.Transparent,
            },
            new SpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 12,
                Text = SongSelectSorting.Label(mode),
                Font = HomeTypography.Control(14),
                Colour = SongSelectTheme.Navy,
            },
            check = new SpriteIcon
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                X = -12,
                Size = new Vector2(11),
                Icon = FontAwesome.Solid.Check,
                Colour = SongSelectTheme.Pink,
                Alpha = 0,
            },
        ];
    }

    internal void SetSelected(bool value)
    {
        selected = value;
        background.Colour = selected
            ? new Color4(
                SongSelectTheme.PaleCyan.R,
                SongSelectTheme.PaleCyan.G,
                SongSelectTheme.PaleCyan.B,
                0.72f)
            : Color4.Transparent;
        check.Alpha = selected ? 1 : 0;
    }

    protected override bool OnKeyDown(KeyDownEvent e)
    {
        if (e.Key is Key.Enter or Key.Space)
        {
            TriggerClick();
            return true;
        }

        return base.OnKeyDown(e);
    }

    protected override void OnFocus(FocusEvent e)
    {
        base.OnFocus(e);
        BorderThickness = 2;
        BorderColour = SongSelectTheme.Pink;
    }

    protected override void OnFocusLost(FocusLostEvent e)
    {
        base.OnFocusLost(e);
        BorderThickness = 0;
    }

    protected override bool OnHover(HoverEvent e)
    {
        if (!selected)
            background.FadeColour(
                new Color4(
                    SongSelectTheme.PaleCyan.R,
                    SongSelectTheme.PaleCyan.G,
                    SongSelectTheme.PaleCyan.B,
                    0.48f),
                90);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        background.FadeColour(
            selected
                ? new Color4(
                    SongSelectTheme.PaleCyan.R,
                    SongSelectTheme.PaleCyan.G,
                    SongSelectTheme.PaleCyan.B,
                    0.72f)
                : Color4.Transparent,
            100);
    }
}

internal partial class SongSelectSortDirectionButton : ClickableContainer
{
    private readonly Box background;
    private readonly SpriteIcon icon;
    private readonly SpriteText text;
    public override bool AcceptsFocus => true;

    internal SongSelectSortDirectionButton(
        LocalisableString label,
        IconUsage iconUsage,
        Action action)
    {
        Action = action;
        Size = new Vector2(180, 40);
        Masking = true;
        CornerRadius = 6;
        BorderThickness = 1;
        BorderColour = SongSelectSurface.Border(0.16f);
        InternalChildren =
        [
            background = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = Color4.Transparent,
            },
            icon = new SpriteIcon
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 10,
                Size = new Vector2(10),
                Icon = iconUsage,
                Colour = SongSelectTheme.Cyan,
            },
            text = new SpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 28,
                Text = label,
                Font = HomeTypography.Control(14),
                Colour = SongSelectTheme.Navy,
            },
        ];
    }

    internal void SetSelected(bool selected)
    {
        background.Colour = selected ? SongSelectTheme.Navy : Color4.Transparent;
        icon.Colour = selected ? SongSelectTheme.Cyan : SongSelectTheme.Cyan;
        text.Colour = selected ? Color4.White : SongSelectTheme.Navy;
    }

    protected override bool OnKeyDown(KeyDownEvent e)
    {
        if (e.Key is Key.Enter or Key.Space)
        {
            TriggerClick();
            return true;
        }

        return base.OnKeyDown(e);
    }

    protected override void OnFocus(FocusEvent e)
    {
        base.OnFocus(e);
        BorderThickness = 2;
        BorderColour = SongSelectTheme.Pink;
    }

    protected override void OnFocusLost(FocusLostEvent e)
    {
        base.OnFocusLost(e);
        BorderThickness = 1;
        BorderColour = SongSelectSurface.Border(0.16f);
    }
}

internal partial class SongSelectNoResultsPanel : CompositeDrawable
{
    private readonly SpriteText title;
    private readonly SpriteText summary;
    private readonly ClickableContainer primaryButton;
    private readonly ClickableContainer resetButton;
    private readonly SpriteText primaryButtonText;
    private readonly Action clearSearch;
    private readonly Action clearFilters;

    internal string Title => title.Text.ToString();
    internal string Summary => summary.Text.ToString();
    internal bool ClearButtonVisible => primaryButton.Alpha > 0.5f;
    internal bool ResetButtonVisible => resetButton.Alpha > 0.5f;
    internal string PrimaryButtonText => primaryButtonText.Text.ToString();
    internal void ActivatePrimaryButton() => primaryButton.TriggerClick();
    internal void ActivateResetButton() => resetButton.TriggerClick();

    public SongSelectNoResultsPanel(
        Action clearSearch,
        Action clearFilters)
    {
        this.clearSearch = clearSearch;
        this.clearFilters = clearFilters;
        Size = new Vector2(560, 206);
        Alpha = 0;

        InternalChildren =
        [
            new Box
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Size = new Vector2(460, 1),
                Y = -73,
                Colour = new Color4(
                    SongSelectTheme.Cyan.R,
                    SongSelectTheme.Cyan.G,
                    SongSelectTheme.Cyan.B,
                    0.32f),
            },
            new Container
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Y = 8,
                Size = new Vector2(46),
                Masking = true,
                CornerRadius = 12,
                Children =
                [
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = new Color4(
                            SongSelectTheme.Cyan.R,
                            SongSelectTheme.Cyan.G,
                            SongSelectTheme.Cyan.B,
                            0.13f),
                    },
                    new SpriteIcon
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Size = new Vector2(18),
                        Icon = FontAwesome.Solid.Search,
                        Colour = SongSelectTheme.Cyan,
                    },
                ],
            },
            title = new SpriteText
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Y = 67,
                Font = HomeTypography.Display(18),
                Colour = SongSelectTheme.Navy,
            },
            summary = new SpriteText
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Y = 96,
                Width = 510,
                Truncate = true,
                Font = HomeTypography.Body(11),
                Colour = new Color4(
                    SongSelectTheme.Navy.R,
                    SongSelectTheme.Navy.G,
                    SongSelectTheme.Navy.B,
                    0.64f),
            },
            primaryButton = new ClickableContainer
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Y = 130,
                Size = new Vector2(168, 40),
                Masking = true,
                CornerRadius = 9,
                BorderThickness = 1.25f,
                BorderColour = new Color4(
                    SongSelectTheme.Cyan.R,
                    SongSelectTheme.Cyan.G,
                    SongSelectTheme.Cyan.B,
                    0.58f),
                Action = clearSearch,
                Children =
                [
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = SongSelectSurface.Ivory(0.94f),
                    },
                    new SpriteIcon
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        X = 17,
                        Size = new Vector2(13),
                        Icon = FontAwesome.Solid.UndoAlt,
                        Colour = SongSelectTheme.Pink,
                    },
                    primaryButtonText = new SpriteText
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        X = 10,
                        Text = "CLEAR SEARCH",
                        Font = HomeTypography.Display(10),
                        Colour = SongSelectTheme.Navy,
                    },
                ],
            },
            resetButton = new ClickableContainer
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Y = 176,
                Size = new Vector2(168, 30),
                Action = clearFilters,
                Children =
                [
                    new SpriteText
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Text = "RESET ALL FILTERS",
                        Font = HomeTypography.Display(9),
                        Colour = new Color4(
                            SongSelectTheme.Navy.R,
                            SongSelectTheme.Navy.G,
                            SongSelectTheme.Navy.B,
                            0.76f),
                    },
                ],
            },
        ];
    }

    internal void SetState(
        bool empty,
        bool hasLibraryEntries,
        string query,
        KeyMode? keyMode,
        double minimumDifficulty,
        string difficultyUnit,
        bool showConverts)
    {
        if (!empty)
        {
            this.FadeOut(100, Easing.OutQuint);
            return;
        }

        var filters = new List<string>();
        string trimmedQuery = query?.Trim() ?? string.Empty;
        if (trimmedQuery.Length > 0)
        {
            if (trimmedQuery.Length > 24)
                trimmedQuery = $"{trimmedQuery[..23]}…";
            filters.Add($"SEARCH “{trimmedQuery}”");
        }
        if (keyMode.HasValue)
            filters.Add($"{(int)keyMode.Value}K");
        if (minimumDifficulty > 0)
            filters.Add($"{difficultyUnit} {minimumDifficulty:0.00}+");
        if (!showConverts)
            filters.Add("CONVERTS HIDDEN");

        bool hasQuery = trimmedQuery.Length > 0;
        bool hasOtherFilters = keyMode.HasValue
                               || minimumDifficulty > 0
                               || !showConverts;
        bool hasFilters = filters.Count > 0;
        title.Text = hasLibraryEntries && hasFilters
            ? "NO SONGS MATCH"
            : "NO SONGS IN YOUR LIBRARY";
        summary.Text = hasLibraryEntries && hasFilters
            ? string.Join("  ·  ", filters)
            : "IMPORT A BEATMAP TO START PLAYING";
        primaryButton.Alpha = hasLibraryEntries && hasFilters ? 1 : 0;
        primaryButton.Action = hasQuery ? clearSearch : clearFilters;
        primaryButtonText.Text = hasQuery
            ? "CLEAR SEARCH"
            : "RESET FILTERS";
        resetButton.Alpha = hasLibraryEntries
                            && hasQuery
                            && hasOtherFilters
            ? 1
            : 0;

        this.ClearTransforms();
        this.FadeIn(140, Easing.OutQuint);
    }
}

internal partial class LegacySongSelectSongRow : ClickableContainer
{
    private readonly Box tint;
    private readonly Box selectedTopBorder;
    private readonly Box selectedBottomBorder;
    private readonly Box selectedLeftBorder;
    private readonly SpriteIcon selectionArrow;
    private readonly Container rowBackground;
    private readonly Container thumbnail;
    private readonly SpriteText title;
    private readonly SpriteText metadata;
    private readonly SpriteText mapper;
    private bool selected;

    public SongSelectEntry Entry { get; }

    public LegacySongSelectSongRow(SongSelectEntry entry, Texture wallpaper, Action select, Action play)
    {
        Entry = entry;
        Action = select;
        Size = new Vector2(668, 64);

        InternalChildren = new Drawable[]
        {
            rowBackground = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Masking = true,
                CornerRadius = 6,
                BorderThickness = 1,
                BorderColour = new Color4(1f, 1f, 1f, 0.1f),
                Children = new Drawable[]
                {
                    new Sprite
                    {
                        RelativeSizeAxes = Axes.Both,
                        Texture = wallpaper,
                        FillMode = FillMode.Fill,
                        Alpha = 0.22f,
                    },
                    tint = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = new Color4(SongSelectTheme.DeepNavy.R, SongSelectTheme.DeepNavy.G, SongSelectTheme.DeepNavy.B, 0.76f),
                    },
                },
            },
            thumbnail = new Container
            {
                Position = new Vector2(5, 4),
                Size = new Vector2(98, 56),
                Masking = true,
                CornerRadius = 5,
                BorderThickness = 1,
                BorderColour = new Color4(1f, 1f, 1f, 0.28f),
                Child = new Sprite
                {
                    RelativeSizeAxes = Axes.Both,
                    Texture = wallpaper,
                    FillMode = FillMode.Fill,
                },
            },
            selectionArrow = new SpriteIcon
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreRight,
                X = -10,
                Size = new Vector2(16),
                Icon = FontAwesome.Solid.Play,
                Colour = SongSelectTheme.Yellow,
                Alpha = 0,
            },
            new Container
            {
                X = 116,
                RelativeSizeAxes = Axes.Both,
                Width = 0.82f,
                Children = new Drawable[]
                {
                    title = new SpriteText
                    {
                        Y = 6,
                        Width = 505,
                        Truncate = true,
                        Text = entry.Beatmap.Title,
                        Font = HomeTypography.Display(
                            titleFontSize(entry.Beatmap.Title, false)),
                        Colour = SongSelectTheme.Ivory,
                    },
                    new SpriteText
                    {
                        Y = 31,
                        Width = 330,
                        Truncate = true,
                        Text = entry.Beatmap.Artist,
                        Font = HomeTypography.Body(13),
                        Colour = SongSelectTheme.Ivory,
                    },
                    mapper = new SpriteText
                    {
                        Y = 48,
                        Width = 330,
                        Truncate = true,
                        Text = $"mapped by {entry.Beatmap.Creator}",
                        Font = HomeTypography.Body(11),
                        Colour = SongSelectTheme.PaleCyan,
                    },
                    metadata = new SpriteText
                    {
                        Anchor = Anchor.TopRight,
                        Origin = Anchor.TopRight,
                        X = -22,
                        Y = 32,
                        Width = 160,
                        Truncate = true,
                        Text = $"{(int)entry.Beatmap.KeyMode}K · {entry.Beatmap.DifficultyName}",
                        Font = HomeTypography.Display(11),
                        Colour = entry.Beatmap.DifficultyName.Contains("Insane", StringComparison.OrdinalIgnoreCase)
                            ? new Color4(0.55f, 0.36f, 1f, 1f)
                            : SongSelectTheme.Pink,
                    },
                    createRowDifficultyRating(entry.DifficultyRating),
                    new SpriteIcon
                    {
                        Anchor = Anchor.CentreRight,
                        Origin = Anchor.CentreRight,
                        X = -3,
                        Size = new Vector2(12),
                        Icon = FontAwesome.Solid.EllipsisV,
                        Colour = SongSelectTheme.Ivory,
                    },
                },
            },
            new Box
            {
                Anchor = Anchor.BottomLeft,
                Origin = Anchor.BottomLeft,
                RelativeSizeAxes = Axes.X,
                Height = 1,
                Colour = new Color4(1f, 1f, 1f, 0.24f),
            },
            selectedTopBorder = new Box
            {
                RelativeSizeAxes = Axes.X,
                Height = 2,
                Colour = SongSelectTheme.Cyan,
                Alpha = 0,
            },
            selectedBottomBorder = new Box
            {
                Anchor = Anchor.BottomLeft,
                Origin = Anchor.BottomLeft,
                RelativeSizeAxes = Axes.X,
                Height = 2,
                Colour = SongSelectTheme.Cyan,
                Alpha = 0,
            },
            selectedLeftBorder = new Box
            {
                RelativeSizeAxes = Axes.Y,
                Width = 5,
                Colour = SongSelectTheme.Cyan,
                Alpha = 0,
            },
        };

        DoubleClickAction = play;
    }

    public Action DoubleClickAction { get; }

    public void SetSelected(bool value)
    {
        selected = value;
        selectedTopBorder.FadeTo(selected ? 0.55f : 0, 120, Easing.OutQuint);
        selectedBottomBorder.FadeTo(selected ? 0.55f : 0, 120, Easing.OutQuint);
        selectedLeftBorder.FadeTo(selected ? 1 : 0, 120, Easing.OutQuint);

        if (selected)
        {
            selectionArrow.FadeTo(1, 120, Easing.OutQuint);
            selectionArrow.ScaleTo(1, 120, Easing.OutQuint);
        }
        else
        {
            selectionArrow.ClearTransforms();
            selectionArrow.Scale = Vector2.One;
            selectionArrow.Alpha = 0;
        }
        rowBackground.Shear = Vector2.Zero;
        thumbnail.Shear = Vector2.Zero;
        tint.FadeColour(
            selected
                ? new Color4(
                    SongSelectTheme.SurfaceRaised.R,
                    SongSelectTheme.SurfaceRaised.G,
                    SongSelectTheme.SurfaceRaised.B,
                    0.92f)
                : new Color4(
                    SongSelectTheme.Surface.R,
                    SongSelectTheme.Surface.G,
                    SongSelectTheme.Surface.B,
                    0.8f),
            150,
            Easing.OutQuint);
        this.ResizeHeightTo(selected ? 70 : 64, 170, Easing.OutQuint);
        thumbnail.ResizeHeightTo(selected ? 62 : 56, 170, Easing.OutQuint);
        this.MoveToX(0, 170, Easing.OutQuint);
        title.Font = HomeTypography.Display(
            titleFontSize(Entry.Beatmap.Title, selected));
        mapper.Colour = selected ? SongSelectTheme.Cyan : SongSelectTheme.PaleCyan;
    }

    protected override bool OnHover(HoverEvent e)
    {
        tint.FadeColour(new Color4(SongSelectTheme.Navy.R, SongSelectTheme.Navy.G, SongSelectTheme.Navy.B, 0.72f), 110, Easing.OutQuint);
        this.MoveToX(2, 130, Easing.OutQuint);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e) => SetSelected(selected);

    protected override bool OnDoubleClick(DoubleClickEvent e)
    {
        DoubleClickAction?.Invoke();
        return true;
    }

    private static float titleFontSize(string text, bool selected)
    {
        int length = Math.Max(1, text?.Length ?? 0);
        float normalSize = Math.Clamp(880f / length, 13f, 19f);
        return selected
            ? Math.Min(20f, normalSize + 1)
            : normalSize;
    }

    private static Drawable createRowDifficultyRating(
        ManiaMsdResult rating)
    {
        var flow = new FillFlowContainer
        {
            Anchor = Anchor.BottomRight,
            Origin = Anchor.BottomRight,
            Position = new Vector2(-27, -12),
            AutoSizeAxes = Axes.Both,
            Direction = FillDirection.Horizontal,
            Spacing = new Vector2(3, 0),
        };

        flow.Add(new SpriteText
        {
            Anchor = Anchor.CentreLeft,
            Origin = Anchor.CentreLeft,
            Text = "MSD",
            Font = HomeTypography.Display(9),
            Colour = SongSelectTheme.Cyan,
        });
        flow.Add(new SpriteText
        {
            Anchor = Anchor.CentreLeft,
            Origin = Anchor.CentreLeft,
            Text = ManiaMsdPresentation.FormatValue(rating),
            Font = HomeTypography.Display(15),
            Colour = rating.IsSuccess ? SongSelectTheme.Ivory : SongSelectTheme.PaleCyan,
        });

        return flow;
    }
}

internal partial class LegacySongSelectPackageHeader : ClickableContainer
{
    private readonly SpriteIcon chevron;

    public LegacySongSelectPackageHeader(
        string packageName,
        int songCount,
        int chartCount,
        bool collapsed,
        Action toggle)
    {
        Action = toggle;
        Size = new Vector2(668, 36);
        Masking = true;
        CornerRadius = 6;
        BorderThickness = 1;
        BorderColour = new Color4(
            SongSelectTheme.Cyan.R,
            SongSelectTheme.Cyan.G,
            SongSelectTheme.Cyan.B,
            0.36f);

        InternalChildren = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = new Color4(
                    SongSelectTheme.DeepNavy.R,
                    SongSelectTheme.DeepNavy.G,
                    SongSelectTheme.DeepNavy.B,
                    0.94f),
            },
            new SpriteIcon
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 14,
                Size = new Vector2(13),
                Icon = FontAwesome.Solid.LayerGroup,
                Colour = SongSelectTheme.Yellow,
            },
            new SpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 39,
                Width = 350,
                Truncate = true,
                Text = packageName,
                Font = HomeTypography.Display(14),
                Colour = SongSelectTheme.Ivory,
            },
            new SpriteText
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                X = -40,
                Text = $"{songCount} SONG{(songCount == 1 ? string.Empty : "S")} · {chartCount} CHART{(chartCount == 1 ? string.Empty : "S")}",
                Font = HomeTypography.Display(9),
                Colour = SongSelectTheme.PaleCyan,
            },
            chevron = new SpriteIcon
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.Centre,
                X = -17,
                Size = new Vector2(13),
                Icon = FontAwesome.Solid.ChevronDown,
                Colour = SongSelectTheme.Cyan,
            },
        };

        chevron.Rotation = collapsed ? -90 : 0;
    }

    protected override bool OnHover(HoverEvent e)
    {
        this.FadeColour(new Color4(1f, 1f, 1f, 0.86f), 100, Easing.OutQuint);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e) =>
        this.FadeColour(Color4.White, 100, Easing.OutQuint);
}

internal partial class LegacySongSelectRankingPanel : ClickableContainer
{
    private const float panel_width = 440;
    private const float content_height = 152;

    private readonly Container content;
    private readonly Box selectorBackground;
    private readonly SpriteText selectorText;
    private readonly SpriteIcon selectorIcon;
    private readonly SongSelectEntry entry;
    private readonly TextureStore textures;
    private SongSelectScoreView view;

    public SongSelectScoreView View => view;
    internal Vector2 ContentSize => content.Size;

    public LegacySongSelectRankingPanel(SongSelectEntry entry, TextureStore textures, Action<SongSelectScoreView> viewChanged)
    {
        this.entry = entry;
        this.textures = textures;
        Size = new Vector2(panel_width, 190);
        Action = toggleView;

        InternalChildren = new Drawable[]
        {
            new Container
            {
                Size = new Vector2(panel_width, 32),
                Masking = true,
                CornerRadius = 6,
                BorderThickness = 1,
                BorderColour = new Color4(
                    SongSelectTheme.Cyan.R,
                    SongSelectTheme.Cyan.G,
                    SongSelectTheme.Cyan.B,
                    0.24f),
                Children = new Drawable[]
                {
                    selectorBackground = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = new Color4(
                            SongSelectTheme.PaleCyan.R,
                            SongSelectTheme.PaleCyan.G,
                            SongSelectTheme.PaleCyan.B,
                            0.56f),
                    },
                    selectorText = new SpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        X = 12,
                        Font = HomeTypography.Display(13),
                        Colour = SongSelectTheme.Navy,
                    },
                    selectorIcon = new SpriteIcon
                    {
                        Anchor = Anchor.CentreRight,
                        Origin = Anchor.CentreRight,
                        X = -10,
                        Size = new Vector2(12),
                        Icon = FontAwesome.Solid.ChevronDown,
                        Colour = SongSelectTheme.Cyan,
                    },
                },
            },
            content = new Container
            {
                Y = 38,
                Size = new Vector2(panel_width, content_height),
            },
        };

        SetView(SongSelectScoreView.GlobalRanking, textures);

        void toggleView()
        {
            SetView(view == SongSelectScoreView.GlobalRanking
                ? SongSelectScoreView.Personal
                : SongSelectScoreView.GlobalRanking);
            viewChanged(View);
        }
    }

    public void SetView(SongSelectScoreView newView, TextureStore textures = null)
    {
        textures ??= this.textures;
        view = newView;
        selectorText.Text = view == SongSelectScoreView.GlobalRanking
            ? "GLOBAL RANKING"
            : "MY RECORD";
        selectorIcon.RotateTo(view == SongSelectScoreView.GlobalRanking ? 0 : 180, 150, Easing.OutQuint);
        content.Clear();

        if (view == SongSelectScoreView.Personal)
            content.Add(createPersonalRecord());
        else
            content.Add(createRanking(textures));
    }

    private Drawable createPersonalRecord() => new Container
    {
        Size = new Vector2(panel_width, 150),
        Masking = true,
        CornerRadius = 6,
        BorderThickness = 1,
        BorderColour = new Color4(
            SongSelectTheme.Cyan.R,
            SongSelectTheme.Cyan.G,
            SongSelectTheme.Cyan.B,
            0.2f),
        Children = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = new Color4(
                    SongSelectTheme.PaleCyan.R,
                    SongSelectTheme.PaleCyan.G,
                    SongSelectTheme.PaleCyan.B,
                    0.62f),
            },
            new SpriteText
            {
                Position = new Vector2(16, 16),
                Text = YokkoStrings.Get("song_select.local_best"),
                Font = HomeTypography.Display(14),
                Colour = SongSelectTheme.Cyan,
            },
            new SpriteText
            {
                Position = new Vector2(16, 44),
                Text = $"{entry.BestScore:N0}",
                Font = HomeTypography.Display(36),
                Colour = SongSelectTheme.Navy,
            },
            new SpriteText
            {
                Position = new Vector2(210, 51),
                Text = $"{entry.BestAccuracy:P2}",
                Font = HomeTypography.Display(24),
                Colour = SongSelectTheme.Pink,
            },
        },
    };

    private Drawable createRanking(TextureStore textures)
    {
        if (entry.Ranking.Count == 0)
        {
            return new Container
            {
                Size = new Vector2(panel_width, content_height),
                Masking = true,
                CornerRadius = 6,
                BorderThickness = 1,
                BorderColour = new Color4(
                    SongSelectTheme.Cyan.R,
                    SongSelectTheme.Cyan.G,
                    SongSelectTheme.Cyan.B,
                    0.18f),
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = new Color4(
                            SongSelectTheme.PaleCyan.R,
                            SongSelectTheme.PaleCyan.G,
                            SongSelectTheme.PaleCyan.B,
                            0.62f),
                    },
                    new SpriteIcon
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        X = 16,
                        Size = new Vector2(15),
                        Icon = FontAwesome.Solid.Trophy,
                        Colour = new Color4(
                            SongSelectTheme.Navy.R,
                            SongSelectTheme.Navy.G,
                            SongSelectTheme.Navy.B,
                            0.58f),
                    },
                    new SpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        X = 44,
                        Text = "NO RANKING DATA · PLAY TO SET A RECORD",
                        Font = HomeTypography.Display(10),
                        Colour = new Color4(
                            SongSelectTheme.Navy.R,
                            SongSelectTheme.Navy.G,
                            SongSelectTheme.Navy.B,
                            0.68f),
                    },
                },
            };
        }

        int visibleScoreCount = Math.Min(entry.Ranking.Count, 5);
        float rowHeight = MathF.Min(
            52,
            (content_height - visibleScoreCount + 1)
            / visibleScoreCount);
        var flow = new FillFlowContainer
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            Direction = FillDirection.Vertical,
            Spacing = new Vector2(0, 1),
        };

        foreach (SongSelectScore score in entry.Ranking.Take(5))
        {
            Texture avatar = score.IsCurrentPlayer
                ? textures.Get("SongSelect/Ui/yokko-avatar-256")
                : textures.Get(score.AvatarTexture);
            flow.Add(createRankingRow(score, avatar, rowHeight));
        }

        return new Container
        {
            Size = new Vector2(panel_width, content_height),
            Masking = true,
            CornerRadius = 6,
            BorderThickness = 1,
            BorderColour = new Color4(
                SongSelectTheme.Cyan.R,
                SongSelectTheme.Cyan.G,
                SongSelectTheme.Cyan.B,
                0.2f),
            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new Color4(
                        SongSelectTheme.PaleCyan.R,
                        SongSelectTheme.PaleCyan.G,
                        SongSelectTheme.PaleCyan.B,
                        0.34f),
                },
                flow,
            },
        };
    }

    private static Drawable createRankingRow(
        SongSelectScore score,
        Texture avatar,
        float rowHeight)
    {
        bool spacious = rowHeight >= 40;
        float avatarSize = spacious ? 34 : 22;
        Color4 accent = score.IsCurrentPlayer ? SongSelectTheme.Pink : score.Rank == 1 ? SongSelectTheme.Yellow : SongSelectTheme.Cyan;
        var row = new Container
        {
            Size = new Vector2(panel_width, rowHeight),
            Masking = true,
            CornerRadius = 2,
            BorderThickness = score.IsCurrentPlayer ? 1 : 0,
            BorderColour = accent,
            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new Color4(
                        SongSelectTheme.PaleCyan.R,
                        SongSelectTheme.PaleCyan.G,
                        SongSelectTheme.PaleCyan.B,
                        score.IsCurrentPlayer ? 0.76f : 0.46f),
                },
                new SpriteText
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    X = 9,
                    Text = score.Rank.ToString(),
                    Font = HomeTypography.Display(spacious ? 18 : 16),
                    Colour = accent,
                },
                new Container
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    X = 30,
                    Size = new Vector2(avatarSize),
                    Masking = true,
                    CornerRadius = avatarSize / 2,
                    BorderThickness = 1,
                    BorderColour = accent,
                    Child = new Sprite
                    {
                        RelativeSizeAxes = Axes.Both,
                        Texture = avatar,
                        FillMode = FillMode.Fill,
                    },
                },
                new SpriteText
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    X = 60,
                    Width = 78,
                    Truncate = true,
                    Text = score.PlayerName,
                    Font = HomeTypography.Display(spacious ? 14 : 12),
                    Colour = score.IsCurrentPlayer ? SongSelectTheme.Pink : SongSelectTheme.Navy,
                },
                new Container
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    X = 143,
                    Size = spacious
                        ? new Vector2(38, 26)
                        : new Vector2(32, 22),
                    Masking = true,
                    CornerRadius = 4,
                    BorderThickness = 1,
                    BorderColour = gradeColour(score.Grade),
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = new Color4(
                                SongSelectTheme.Ivory.R,
                                SongSelectTheme.Ivory.G,
                                SongSelectTheme.Ivory.B,
                                0.9f),
                        },
                        new SpriteText
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Text = score.Grade.ToDisplayLabel(),
                            Font = HomeTypography.Display(spacious ? 16 : 14),
                            Colour = gradeColour(score.Grade),
                        },
                    },
                },
                new SpriteText
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    X = -54,
                    Y = spacious ? -5 : -3,
                    Text = $"{score.Score:N0}",
                    Font = HomeTypography.Display(spacious ? 14 : 12),
                    Colour = SongSelectTheme.Navy,
                },
                new SpriteText
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    X = -54,
                    Y = spacious ? 10 : 9,
                    Text = $"{score.Accuracy:P2}",
                    Font = HomeTypography.Display(spacious ? 10 : 9),
                    Colour = SongSelectTheme.Cyan,
                },
                createMods(score.Mods),
            },
        };

        if (score.IsCurrentPlayer)
        {
            row.Add(new SpriteText
            {
                Anchor = Anchor.TopLeft,
                Origin = Anchor.TopLeft,
                X = 116,
                Y = 2,
                Text = YokkoStrings.Get("song_select.you"),
                Font = HomeTypography.Display(8),
                Colour = SongSelectTheme.Pink,
            });
        }

        return row;
    }

    protected override bool OnHover(HoverEvent e)
    {
        selectorBackground.FadeColour(
            new Color4(
                SongSelectTheme.PaleCyan.R,
                SongSelectTheme.PaleCyan.G,
                SongSelectTheme.PaleCyan.B,
                0.88f),
            90,
            Easing.OutQuint);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        selectorBackground.FadeColour(
            new Color4(
                SongSelectTheme.PaleCyan.R,
                SongSelectTheme.PaleCyan.G,
                SongSelectTheme.PaleCyan.B,
                0.56f),
            110,
            Easing.OutQuint);
    }

    private static Drawable createMods(IReadOnlyList<string> mods)
    {
        var flow = new FillFlowContainer
        {
            Anchor = Anchor.CentreRight,
            Origin = Anchor.CentreRight,
            X = -6,
            Y = 6,
            AutoSizeAxes = Axes.Both,
            Direction = FillDirection.Horizontal,
            Spacing = new Vector2(3, 0),
        };

        foreach (string mod in mods.Take(2))
        {
            flow.Add(new Container
            {
                Size = new Vector2(22, 15),
                Masking = true,
                CornerRadius = 3,
                BorderThickness = 1,
                BorderColour = mod == "DT" ? SongSelectTheme.Pink : SongSelectTheme.Cyan,
                Child = new SpriteText
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Text = mod,
                    Font = HomeTypography.Display(8),
                    Colour = mod == "DT" ? SongSelectTheme.Pink : SongSelectTheme.Cyan,
                },
            });
        }

        return flow;
    }

    private static Color4 gradeColour(Yokko.Core.Scoring.ScoreRank grade) => grade switch
    {
        Yokko.Core.Scoring.ScoreRank.X => SongSelectTheme.PaleCyan,
        Yokko.Core.Scoring.ScoreRank.XH => SongSelectTheme.PaleCyan,
        Yokko.Core.Scoring.ScoreRank.S => SongSelectTheme.Cyan,
        Yokko.Core.Scoring.ScoreRank.SH => SongSelectTheme.Cyan,
        Yokko.Core.Scoring.ScoreRank.A => new Color4(0.56f, 0.95f, 0.34f, 1f),
        Yokko.Core.Scoring.ScoreRank.B => SongSelectTheme.Yellow,
        _ => SongSelectTheme.Pink,
    };
}
