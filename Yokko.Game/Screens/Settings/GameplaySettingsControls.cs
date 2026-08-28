using System;
using System.Collections.Generic;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using Yokko.Audio;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;
using Yokko.Core.Scoring;
using Yokko.Game.Gameplay;
using Yokko.Game.Localisation;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Screens.Settings;

internal partial class GameplayScrollSpeedSlider : CompositeDrawable
{
    private const float track_x = 18;
    private const float track_width = 354;
    private const float track_y = 40;

    private readonly Bindable<double> value;
    private readonly Func<double, string> formatter;
    private readonly Bindable<ScrollSpeedAdjustmentMode> adjustmentMode;
    private readonly Action<double> adjustScrollTime;
    private readonly Box track;
    private readonly Box fill;
    private readonly Circle knob;
    private readonly SpriteText valueText;

    public override bool AcceptsFocus => true;

    internal GameplayScrollSpeedSlider(
        Bindable<double> value,
        Func<double, string> formatter,
        Bindable<ScrollSpeedAdjustmentMode> adjustmentMode,
        Action<double> adjustScrollTime,
        bool placeModeBelow = false)
    {
        this.value = value;
        this.formatter = formatter;
        this.adjustmentMode = adjustmentMode;
        this.adjustScrollTime = adjustScrollTime;
        Size = new Vector2(390, 54);

        var modeButton = new GameplayStepperModeButton(
            adjustmentMode,
            placeModeBelow)
        {
            Anchor = Anchor.TopRight,
            Origin = Anchor.TopRight,
            Position = placeModeBelow
                ? new Vector2(-2, 60)
                : new Vector2(-12, 7),
            Size = placeModeBelow
                ? new Vector2(148, 30)
                : new Vector2(112, 18),
        };

        InternalChildren = new Drawable[]
        {
            new Container
            {
                Position = new Vector2(0, 4),
                Size = new Vector2(390, 50),
                Masking = true,
                CornerRadius = 8,
                Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new Color4(0.015f, 0.045f, 0.28f, 0.2f),
                },
            },
            new Container
            {
                Position = new Vector2(-1.5f, -1.5f),
                Size = new Vector2(393, 57),
                Masking = true,
                CornerRadius = 8,
                Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new Color4(
                        HomeControlColours.Cyan.R,
                        HomeControlColours.Cyan.G,
                        HomeControlColours.Cyan.B,
                        0.4f),
                },
            },
            new Container
            {
                RelativeSizeAxes = Axes.Both,
                Masking = true,
                CornerRadius = 7,
                BorderThickness = 1.6f,
                BorderColour = HomeControlColours.Navy,
                Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.White,
                },
            },
            valueText = new SpriteText
            {
                Position = new Vector2(track_x, 8),
                Font = HomeTypography.Display(18),
                Colour = HomeControlColours.Navy,
            },
            track = new Box
            {
                Position = new Vector2(track_x, track_y),
                Size = new Vector2(track_width, 5),
                Colour = SettingsTheme.Divider,
            },
            fill = new Box
            {
                Position = new Vector2(track_x, track_y),
                Height = 5,
                Colour = HomeControlColours.Pink,
            },
            knob = new Circle
            {
                Origin = Anchor.Centre,
                Position = new Vector2(track_x, track_y + 2.5f),
                Size = new Vector2(15),
                Colour = Color4.White,
                BorderThickness = 2.5f,
                BorderColour = HomeControlColours.Pink,
            },
            modeButton,
        };

        value.BindValueChanged(onValueChanged, true);
        adjustmentMode.BindValueChanged(onAdjustmentModeChanged, true);
    }

    internal static double ValueFromProgress(
        double progress,
        ScrollSpeedAdjustmentMode mode)
    {
        double raw = OsuManiaScrollSpeed.Minimum
                     + Math.Clamp(progress, 0, 1)
                     * (OsuManiaScrollSpeed.Maximum
                        - OsuManiaScrollSpeed.Minimum);
        double clamped = OsuManiaScrollSpeed.Clamp(raw);
        return mode == ScrollSpeedAdjustmentMode.Milliseconds
            ? clamped
            : OsuManiaScrollSpeed.SnapToWholeStep(clamped);
    }

    internal static double AdjustForScroll(
        double currentValue,
        float scrollDelta) =>
        OsuManiaScrollSpeed.AdjustWholeStep(
            currentValue,
            Math.Sign(scrollDelta) * OsuManiaScrollSpeed.ShortcutStep);

    internal static double FineScrollTimeDeltaForDirection(double direction) =>
        -Math.Sign(direction)
        * OsuManiaScrollSpeed.ScrollTimeStepMilliseconds;

    protected override bool OnMouseDown(MouseDownEvent e)
    {
        if (e.Button != MouseButton.Left)
            return false;

        Vector2 local = ToLocalSpace(e.ScreenSpaceMousePosition);
        if (local.Y < 28)
            return false;

        updateFrom(local.X);
        return true;
    }

    protected override bool OnDragStart(DragStartEvent e) => true;

    protected override void OnDrag(DragEvent e) =>
        updateFrom(ToLocalSpace(e.ScreenSpaceMousePosition).X);

    protected override bool OnScroll(ScrollEvent e)
    {
        if (e.ScrollDelta.Y == 0)
            return false;

        if (adjustmentMode.Value == ScrollSpeedAdjustmentMode.Milliseconds)
            adjustScrollTime(
                FineScrollTimeDeltaForDirection(e.ScrollDelta.Y));
        else
            value.Value = AdjustForScroll(value.Value, e.ScrollDelta.Y);

        return true;
    }

    protected override bool OnKeyDown(KeyDownEvent e)
    {
        int direction = e.Key switch
        {
            Key.Left or Key.Down => -1,
            Key.Right or Key.Up => 1,
            _ => 0,
        };

        if (e.Key == Key.Home)
            value.Value = OsuManiaScrollSpeed.Minimum;
        else if (e.Key == Key.End)
            value.Value = OsuManiaScrollSpeed.Maximum;
        else if (direction != 0)
        {
            if (adjustmentMode.Value == ScrollSpeedAdjustmentMode.Milliseconds)
                adjustScrollTime(FineScrollTimeDeltaForDirection(direction));
            else
                value.Value = OsuManiaScrollSpeed.Adjust(
                    value.Value,
                    direction * OsuManiaScrollSpeed.ShortcutStep);
        }
        else
            return base.OnKeyDown(e);

        return true;
    }

    protected override bool OnHover(HoverEvent e)
    {
        track.FadeColour(SettingsTheme.PaleCyan, 100, Easing.OutQuint);
        knob.ScaleTo(1.18f, 100, Easing.OutQuint);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        track.FadeColour(SettingsTheme.Divider, 120, Easing.OutQuint);
        knob.ScaleTo(1, 120, Easing.OutQuint);
    }

    protected override void OnFocus(FocusEvent e)
    {
        base.OnFocus(e);
        valueText.FadeColour(HomeControlColours.Pink, 100, Easing.OutQuint);
        knob.BorderColour = HomeControlColours.Cyan;
    }

    protected override void OnFocusLost(FocusLostEvent e)
    {
        base.OnFocusLost(e);
        valueText.FadeColour(HomeControlColours.Navy, 100, Easing.OutQuint);
        knob.BorderColour = HomeControlColours.Pink;
    }

    private void updateFrom(float localX) =>
        value.Value = ValueFromProgress(
            (localX - track_x) / track_width,
            adjustmentMode.Value);

    private void onValueChanged(ValueChangedEvent<double> change)
    {
        float progress = (float)(
            (change.NewValue - OsuManiaScrollSpeed.Minimum)
            / (OsuManiaScrollSpeed.Maximum - OsuManiaScrollSpeed.Minimum));
        fill.Width = progress * track_width;
        knob.X = track_x + progress * track_width;
        valueText.Text = formatter(change.NewValue);
    }

    private void onAdjustmentModeChanged(
        ValueChangedEvent<ScrollSpeedAdjustmentMode> change)
    {
        if (change.NewValue == ScrollSpeedAdjustmentMode.OsuManiaScale)
        {
            value.Value = OsuManiaScrollSpeed.SnapToWholeStep(value.Value);
        }

        valueText.Text = formatter(value.Value);
    }

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
        {
            value.ValueChanged -= onValueChanged;
            adjustmentMode.ValueChanged -= onAdjustmentModeChanged;
        }

        base.Dispose(isDisposing);
    }
}

internal partial class GameplayValueStepper : CompositeDrawable
{
    private readonly Bindable<double> value;
    private readonly double step;
    private readonly double minimum;
    private readonly double maximum;
    private readonly Func<double, string> formatter;
    private readonly Action<double> adjustValue;
    private readonly Bindable<ScrollSpeedAdjustmentMode> adjustmentMode;
    private readonly Action<double> alternateAdjustValue;
    private readonly Func<double, string> alternateFormatter;
    private readonly GameplayStepperButton decreaseButton;
    private readonly GameplayStepperButton increaseButton;
    private readonly GameplayStepperModeButton modeButton;
    private readonly SpriteText valueText;
    private bool isEnabled = true;

    internal bool IsEnabled => isEnabled;

    public GameplayValueStepper(
        Bindable<double> value,
        double step,
        double minimum,
        double maximum,
        Func<double, string> formatter,
        Action<double> adjustValue = null,
        Bindable<ScrollSpeedAdjustmentMode> adjustmentMode = null,
        Action<double> alternateAdjustValue = null,
        Func<double, string> alternateFormatter = null)
    {
        this.value = value;
        this.step = step;
        this.minimum = minimum;
        this.maximum = maximum;
        this.formatter = formatter;
        this.adjustValue = adjustValue;
        this.adjustmentMode = adjustmentMode;
        this.alternateAdjustValue = alternateAdjustValue;
        this.alternateFormatter = alternateFormatter;
        Size = new Vector2(390, 54);

        decreaseButton = createButton(
            FontAwesome.Solid.Minus,
            Anchor.CentreLeft,
            -step);
        valueText = new SpriteText
        {
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            Y = adjustmentMode == null ? 0 : -7,
                Font = HomeTypography.Display(20),
            Colour = HomeControlColours.Navy,
        };
        increaseButton = createButton(
            FontAwesome.Solid.Plus,
            Anchor.CentreRight,
            step);

        var children = new List<Drawable>
        {
            new Container
            {
                Position = new Vector2(0, 4),
                Size = new Vector2(390, 50),
                Masking = true,
                CornerRadius = 8,
                Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new Color4(0.015f, 0.045f, 0.28f, 0.2f),
                },
            },
            new Container
            {
                Position = new Vector2(-1.5f, -1.5f),
                Size = new Vector2(393, 57),
                Masking = true,
                CornerRadius = 8,
                Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new Color4(
                        HomeControlColours.Cyan.R,
                        HomeControlColours.Cyan.G,
                        HomeControlColours.Cyan.B,
                        0.4f),
                },
            },
            new Container
            {
                RelativeSizeAxes = Axes.Both,
                Masking = true,
                CornerRadius = 7,
                BorderThickness = 1.6f,
                BorderColour = HomeControlColours.Navy,
                Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.White,
                },
            },
            decreaseButton,
            valueText,
            increaseButton,
        };

        if (adjustmentMode != null)
        {
            if (alternateAdjustValue == null || alternateFormatter == null)
            {
                throw new ArgumentException(
                    "An alternate scroll-speed mode requires an adjuster and formatter.");
            }

            children.Add(modeButton = new GameplayStepperModeButton(
                adjustmentMode));
            adjustmentMode.BindValueChanged(onAdjustmentModeChanged);
        }

        InternalChildren = children.ToArray();
        value.BindValueChanged(onValueChanged, true);
    }

    internal void SetEnabled(bool enabled)
    {
        if (isEnabled == enabled)
            return;

        isEnabled = enabled;
        decreaseButton.SetEnabled(enabled);
        increaseButton.SetEnabled(enabled);
        modeButton?.SetEnabled(enabled);
    }

    private GameplayStepperButton createButton(
        IconUsage itemIcon,
        Anchor anchor,
        double delta) => new GameplayStepperButton(
        itemIcon,
        anchor,
        () =>
        {
            if (adjustmentMode?.Value
                == ScrollSpeedAdjustmentMode.Milliseconds)
            {
                alternateAdjustValue(delta);
                return;
            }

            if (adjustValue != null)
            {
                adjustValue(delta);
                return;
            }

            double next = Math.Clamp(value.Value + delta, minimum, maximum);
            value.Value = Math.Round(next / step) * step;
        });

    private void onValueChanged(ValueChangedEvent<double> change) =>
        valueText.Text = activeFormatter(change.NewValue);

    private void onAdjustmentModeChanged(
        ValueChangedEvent<ScrollSpeedAdjustmentMode> _) =>
        valueText.Text = activeFormatter(value.Value);

    private string activeFormatter(double currentValue) =>
        adjustmentMode?.Value == ScrollSpeedAdjustmentMode.Milliseconds
            ? alternateFormatter(currentValue)
            : formatter(currentValue);

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
        {
            value.ValueChanged -= onValueChanged;
            if (adjustmentMode != null)
            {
                adjustmentMode.ValueChanged -=
                    onAdjustmentModeChanged;
            }
        }

        base.Dispose(isDisposing);
    }
}

internal partial class GameplayStepperModeButton : ClickableContainer
{
    private readonly Bindable<ScrollSpeedAdjustmentMode> mode;
    private readonly bool prominent;
    private readonly Box background;
    private readonly Box switchTrack;
    private readonly Circle switchThumb;
    private readonly SpriteText text;
    private bool isEnabled = true;

    public override bool AcceptsFocus => isEnabled;

    internal ScrollSpeedAdjustmentMode DisplayedMode => mode.Value;
    internal bool IsFineAdjustmentEnabled =>
        mode.Value == ScrollSpeedAdjustmentMode.Milliseconds;

    public GameplayStepperModeButton(
        Bindable<ScrollSpeedAdjustmentMode> mode,
        bool prominent = false)
    {
        this.mode = mode;
        this.prominent = prominent;
        Anchor = prominent ? Anchor.TopRight : Anchor.BottomCentre;
        Origin = prominent ? Anchor.TopRight : Anchor.BottomCentre;
        Y = prominent ? 0 : -2;
        Size = prominent
            ? new Vector2(148, 30)
            : new Vector2(124, 22);
        Masking = true;
        CornerRadius = prominent ? 15 : 5;
        BorderThickness = prominent ? 1.5f : 1;
        BorderColour = prominent
            ? HomeControlColours.Navy
            : SettingsTheme.Divider;
        Action = toggleMode;

        InternalChildren = new Drawable[]
        {
            background = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = SettingsTheme.PaleCyan,
            },
            text = new SpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = prominent ? 13 : 7,
                Text = YokkoStrings.Get(
                    "settings.general.fine_adjustment"),
                Font = HomeTypography.Control(prominent ? 16 : 14),
                Colour = HomeControlColours.Navy,
            },
            new Container
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                X = prominent ? -7 : -5,
                Size = prominent
                    ? new Vector2(46, 22)
                    : new Vector2(32, 16),
                Masking = true,
                CornerRadius = prominent ? 11 : 8,
                BorderThickness = prominent ? 1.5f : 1,
                BorderColour = HomeControlColours.Navy,
                Children = new Drawable[]
                {
                    switchTrack = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = SettingsTheme.Divider,
                    },
                    switchThumb = new Circle
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.Centre,
                        X = prominent ? 11 : 8,
                        Size = new Vector2(prominent ? 16 : 11),
                        Colour = Color4.White,
                    },
                },
            },
        };

        mode.BindValueChanged(onModeChanged, true);
    }

    internal void SetEnabled(bool enabled)
    {
        isEnabled = enabled;
        Alpha = enabled ? 1 : 0.55f;
    }

    private void toggleMode()
    {
        if (!isEnabled)
            return;

        mode.Value = mode.Value == ScrollSpeedAdjustmentMode.OsuManiaScale
            ? ScrollSpeedAdjustmentMode.Milliseconds
            : ScrollSpeedAdjustmentMode.OsuManiaScale;
    }

    private void onModeChanged(
        ValueChangedEvent<ScrollSpeedAdjustmentMode> change)
    {
        bool milliseconds =
            change.NewValue == ScrollSpeedAdjustmentMode.Milliseconds;
        background.Colour = milliseconds
            ? SettingsTheme.StatusCyan
            : prominent ? Color4.White : SettingsTheme.PaleCyan;
        switchTrack.Colour = milliseconds
            ? HomeControlColours.Navy
            : SettingsTheme.Divider;
        switchThumb.MoveToX(
            milliseconds
                ? prominent ? 35 : 24
                : prominent ? 11 : 8,
            180,
            Easing.OutBack);
    }

    protected override bool OnHover(HoverEvent e)
    {
        if (isEnabled)
            background.FadeColour(Color4.White, 100, Easing.OutQuint);

        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        bool milliseconds =
            mode.Value == ScrollSpeedAdjustmentMode.Milliseconds;
        background.FadeColour(
            milliseconds
                ? SettingsTheme.StatusCyan
                : prominent ? Color4.White : SettingsTheme.PaleCyan,
            120,
            Easing.OutQuint);
    }

    protected override bool OnKeyDown(KeyDownEvent e)
    {
        if (isEnabled && e.Key is Key.Enter or Key.Space)
        {
            toggleMode();
            return true;
        }

        return base.OnKeyDown(e);
    }

    protected override void OnFocus(FocusEvent e)
    {
        base.OnFocus(e);
        BorderColour = HomeControlColours.Pink;
        BorderThickness = prominent ? 2.4f : 2f;
    }

    protected override void OnFocusLost(FocusLostEvent e)
    {
        base.OnFocusLost(e);
        BorderColour = prominent
            ? HomeControlColours.Navy
            : SettingsTheme.Divider;
        BorderThickness = prominent ? 1.5f : 1f;
    }

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
            mode.ValueChanged -= onModeChanged;

        base.Dispose(isDisposing);
    }
}

internal partial class GameplayEtternaJusticeControls : CompositeDrawable
{
    private readonly Bindable<JudgementMode> mode;
    private readonly GameplayValueStepper stepper;

    internal bool IsEnabled { get; private set; }

    public GameplayEtternaJusticeControls(
        Bindable<JudgementMode> mode,
        Bindable<double> value,
        Func<double, string> formatter)
    {
        this.mode = mode;
        Size = new Vector2(800, 104);

        InternalChildren = new Drawable[]
        {
            new SpriteText
            {
                Position = new Vector2(0, 5),
                Text = YokkoStrings.Get(
                    "settings.gameplay.etterna_justice"),
                Font = HomeTypography.Display(19),
                Colour = HomeControlColours.Navy,
            },
            new SpriteText
            {
                Position = new Vector2(0, 34),
                Text = YokkoStrings.Get(
                    "settings.gameplay.etterna_justice_note"),
                Font = HomeTypography.Body(15),
                Colour = SettingsTheme.MutedNavy,
            },
            stepper = new GameplayValueStepper(
                value,
                1,
                JudgementConfiguration.MinimumEtternaJustice,
                JudgementConfiguration.MaximumEtternaJustice,
                formatter)
            {
                Position = new Vector2(410, 0),
            },
            new SpriteText
            {
                Position = new Vector2(0, 79),
                Text = YokkoStrings.Get(
                    "settings.gameplay.etterna_boundaries"),
                Font = HomeTypography.Body(15),
                Colour = SettingsTheme.MutedNavy,
            },
        };

        mode.BindValueChanged(onModeChanged, true);
    }

    private void onModeChanged(ValueChangedEvent<JudgementMode> change)
    {
        IsEnabled = change.NewValue == JudgementMode.Etterna;
        stepper.SetEnabled(IsEnabled);
        this.FadeTo(IsEnabled ? 1 : 0.42f, 120, Easing.OutQuint);
    }

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
            mode.ValueChanged -= onModeChanged;

        base.Dispose(isDisposing);
    }
}

internal partial class GameplayJudgementModeSelector : CompositeDrawable
{
    private readonly Bindable<JudgementMode> mode;
    private readonly SettingsSegmentedChoiceButton lazerButton;
    private readonly SettingsSegmentedChoiceButton stableButton;
    private readonly SettingsSegmentedChoiceButton etternaButton;
    private readonly SettingsSegmentedChoiceButton bmsButton;

    public GameplayJudgementModeSelector(
        Bindable<JudgementMode> mode)
    {
        this.mode = mode;
        Size = new Vector2(800, 54);

        var card = new SettingsStickerCard(new Vector2(800, 54), 8);
        card.SetContent(new FillFlowContainer
        {
            RelativeSizeAxes = Axes.Both,
            Direction = FillDirection.Horizontal,
            Children = new Drawable[]
            {
                lazerButton = new SettingsSegmentedChoiceButton(
                    YokkoStrings.Get(
                        "settings.gameplay.judgement_yokko"),
                    FontAwesome.Solid.Gamepad,
                    () => mode.Value = JudgementMode.Yokko,
                    800f / 4),
                stableButton = new SettingsSegmentedChoiceButton(
                    YokkoStrings.Get(
                        "settings.gameplay.judgement_osu_stable"),
                    FontAwesome.Solid.Clock,
                    () => mode.Value = JudgementMode.OsuStable,
                    800f / 4),
                etternaButton = new SettingsSegmentedChoiceButton(
                    YokkoStrings.Get(
                        "settings.gameplay.judgement_etterna"),
                    FontAwesome.Solid.Bullseye,
                    () => mode.Value = JudgementMode.Etterna,
                    800f / 4),
                bmsButton = new SettingsSegmentedChoiceButton(
                    YokkoStrings.Get(
                        "settings.gameplay.judgement_bms_beatoraja"),
                    FontAwesome.Solid.CompactDisc,
                    () => mode.Value = JudgementMode.BmsBeatoraja,
                    800f / 4),
            },
        });
        InternalChild = card;

        mode.BindValueChanged(onModeChanged, true);
    }

    private void onModeChanged(
        ValueChangedEvent<JudgementMode> change)
    {
        lazerButton.SetSelected(change.NewValue == JudgementMode.Yokko);
        stableButton.SetSelected(
            change.NewValue == JudgementMode.OsuStable);
        etternaButton.SetSelected(
            change.NewValue == JudgementMode.Etterna);
        bmsButton.SetSelected(
            change.NewValue == JudgementMode.BmsBeatoraja);
    }

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
            mode.ValueChanged -= onModeChanged;

        base.Dispose(isDisposing);
    }
}

internal partial class GameplayScrollDirectionSelector : CompositeDrawable
{
    private readonly Bindable<ManiaScrollDirection> direction;
    private readonly SettingsSegmentedChoiceButton downscrollButton;
    private readonly SettingsSegmentedChoiceButton upscrollButton;

    public GameplayScrollDirectionSelector(
        Bindable<ManiaScrollDirection> direction)
    {
        this.direction = direction;
        Size = new Vector2(390, 54);

        var card = new SettingsStickerCard(new Vector2(390, 54), 8);
        card.SetContent(new FillFlowContainer
        {
            RelativeSizeAxes = Axes.Both,
            Direction = FillDirection.Horizontal,
            Children = new Drawable[]
            {
                downscrollButton = new SettingsSegmentedChoiceButton(
                    YokkoStrings.Get(
                        "settings.gameplay.scroll_direction_down"),
                    FontAwesome.Solid.ChevronDown,
                    () => direction.Value =
                        ManiaScrollDirection.Downscroll,
                    195),
                upscrollButton = new SettingsSegmentedChoiceButton(
                    YokkoStrings.Get(
                        "settings.gameplay.scroll_direction_up"),
                    FontAwesome.Solid.ChevronUp,
                    () => direction.Value =
                        ManiaScrollDirection.Upscroll,
                    195),
            },
        });
        InternalChild = card;

        direction.BindValueChanged(onDirectionChanged, true);
    }

    private void onDirectionChanged(
        ValueChangedEvent<ManiaScrollDirection> change)
    {
        downscrollButton.SetSelected(
            change.NewValue == ManiaScrollDirection.Downscroll);
        upscrollButton.SetSelected(
            change.NewValue == ManiaScrollDirection.Upscroll);
    }

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
            direction.ValueChanged -= onDirectionChanged;

        base.Dispose(isDisposing);
    }
}

internal partial class GameplayRatePitchModeSelector : CompositeDrawable
{
    private readonly Bindable<AudioPitchMode> mode;
    private readonly SettingsSegmentedChoiceButton doubleTimeButton;
    private readonly SettingsSegmentedChoiceButton nightcoreButton;

    public GameplayRatePitchModeSelector(
        Bindable<AudioPitchMode> mode)
    {
        this.mode = mode;
        Size = new Vector2(800, 54);

        var card = new SettingsStickerCard(new Vector2(800, 54), 8);
        card.SetContent(new FillFlowContainer
        {
            RelativeSizeAxes = Axes.Both,
            Direction = FillDirection.Horizontal,
            Children = new Drawable[]
            {
                doubleTimeButton = new SettingsSegmentedChoiceButton(
                    YokkoStrings.Get(
                        "settings.gameplay.playback_rate_dt"),
                    FontAwesome.Solid.Clock,
                    () => mode.Value = AudioPitchMode.Preserve,
                    400),
                nightcoreButton = new SettingsSegmentedChoiceButton(
                    YokkoStrings.Get(
                        "settings.gameplay.playback_rate_nc"),
                    FontAwesome.Solid.Bolt,
                    () => mode.Value = AudioPitchMode.ScaleWithRate,
                    400),
            },
        });
        InternalChild = card;

        mode.BindValueChanged(onModeChanged, true);
    }

    private void onModeChanged(
        ValueChangedEvent<AudioPitchMode> change)
    {
        doubleTimeButton.SetSelected(
            change.NewValue == AudioPitchMode.Preserve);
        nightcoreButton.SetSelected(
            change.NewValue == AudioPitchMode.ScaleWithRate);
    }

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
            mode.ValueChanged -= onModeChanged;

        base.Dispose(isDisposing);
    }
}

internal partial class GameplayStepperButton : ClickableContainer
{
    private readonly Box background;
    private readonly Box focusLine;
    private readonly SpriteIcon icon;
    private bool isEnabled = true;

    public override bool AcceptsFocus => isEnabled;

    public GameplayStepperButton(
        IconUsage itemIcon,
        Anchor anchor,
        Action action)
    {
        Anchor = anchor;
        Origin = anchor;
        Width = 68;
        RelativeSizeAxes = Axes.Y;
        Action = action;

        InternalChildren = new Drawable[]
        {
            background = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = Color4.Transparent,
            },
            icon = new SpriteIcon
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Size = new Vector2(16),
                Icon = itemIcon,
                Colour = HomeControlColours.Pink,
            },
            focusLine = new Box
            {
                Anchor = Anchor.BottomLeft,
                Origin = Anchor.BottomLeft,
                RelativeSizeAxes = Axes.X,
                Height = 3,
                Colour = HomeControlColours.Pink,
                Alpha = 0,
            },
        };
    }

    public void SetEnabled(bool enabled)
    {
        isEnabled = enabled;
        icon.FadeTo(enabled ? 1 : 0.7f, 100, Easing.OutQuint);
        focusLine.FadeOut(80, Easing.OutQuint);
        background.FadeColour(Color4.Transparent, 80, Easing.OutQuint);
    }

    protected override bool OnHover(HoverEvent e)
    {
        if (isEnabled)
            background.FadeColour(SettingsTheme.PaleCyan, 100, Easing.OutQuint);

        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e) =>
        background.FadeColour(Color4.Transparent, 120, Easing.OutQuint);

    protected override bool OnKeyDown(KeyDownEvent e)
    {
        if (isEnabled && e.Key is Key.Enter or Key.Space)
        {
            Action?.Invoke();
            return true;
        }

        return base.OnKeyDown(e);
    }

    protected override bool OnClick(ClickEvent e)
    {
        if (!isEnabled)
            return true;

        return base.OnClick(e);
    }

    protected override void OnFocus(FocusEvent e)
    {
        base.OnFocus(e);
        focusLine.FadeIn(100, Easing.OutQuint);
    }

    protected override void OnFocusLost(FocusLostEvent e)
    {
        base.OnFocusLost(e);
        focusLine.FadeOut(100, Easing.OutQuint);
    }
}

internal partial class GameplayInlineToggle : ClickableContainer
{
    private readonly BindableBool value;
    private readonly Box hoverBackground;
    private readonly Box switchTrack;
    private readonly Circle switchThumb;
    private readonly SpriteText stateText;
    private readonly SpriteText titleText;

    public override bool AcceptsFocus => true;

    public GameplayInlineToggle(
        LocalisableString title,
        LocalisableString note,
        BindableBool value)
    {
        this.value = value;
        Action = () => value.Value = !value.Value;

        InternalChildren = new Drawable[]
        {
            hoverBackground = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = Color4.Transparent,
            },
            titleText = new SpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                Text = title,
                Font = HomeTypography.Display(17),
                Colour = HomeControlColours.Navy,
            },
            new SpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 142,
                Text = note,
                Font = HomeTypography.Body(15),
                Colour = SettingsTheme.MutedNavy,
            },
            new Container
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                X = -82,
                Size = new Vector2(48, 24),
                Masking = true,
                CornerRadius = 12,
                Children = new Drawable[]
                {
                    switchTrack = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = SettingsTheme.Divider,
                    },
                    switchThumb = new Circle
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.Centre,
                        X = 12,
                        Size = new Vector2(18),
                        Colour = Color4.White,
                    },
                },
            },
            stateText = new SpriteText
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                Font = HomeTypography.Display(15),
                Colour = HomeControlColours.Navy,
            },
        };

        value.BindValueChanged(onValueChanged, true);
    }

    private void onValueChanged(ValueChangedEvent<bool> change)
    {
        switchTrack.FadeColour(
            change.NewValue ? HomeControlColours.Navy : SettingsTheme.Divider,
            120,
            Easing.OutQuint);
        switchThumb.MoveToX(
            change.NewValue ? 36 : 12,
            120,
            Easing.OutQuint);
        stateText.Text = YokkoStrings.Get(change.NewValue
            ? "settings.gameplay.enabled"
            : "settings.gameplay.disabled");
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
        titleText.FadeColour(HomeControlColours.Pink, 100);
    }

    protected override void OnFocusLost(FocusLostEvent e)
    {
        base.OnFocusLost(e);
        titleText.FadeColour(HomeControlColours.Navy, 100);
        hoverBackground.FadeColour(Color4.Transparent, 100, Easing.OutQuint);
    }

    protected override bool OnHover(HoverEvent e)
    {
        hoverBackground.FadeColour(SettingsTheme.PaleCyan, 100, Easing.OutQuint);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        hoverBackground.FadeColour(Color4.Transparent, 120, Easing.OutQuint);
    }

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
            value.ValueChanged -= onValueChanged;

        base.Dispose(isDisposing);
    }
}

internal partial class GameplayToggleCard : ClickableContainer
{
    private readonly BindableBool value;
    private readonly Box background;
    private readonly Box switchTrack;
    private readonly Circle switchThumb;
    private readonly SpriteText stateText;

    public override bool AcceptsFocus => true;

    public GameplayToggleCard(
        LocalisableString title,
        LocalisableString note,
        IconUsage itemIcon,
        BindableBool value,
        float height = 84)
    {
        this.value = value;
        Action = () => value.Value = !value.Value;
        Size = new Vector2(393, height);
        Masking = true;
        CornerRadius = 8;
        BorderThickness = 1.2f;
        BorderColour = SettingsTheme.Divider;

        InternalChildren = new Drawable[]
        {
            background = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = Color4.White,
            },
            new Circle
            {
                Position = new Vector2(16, 14),
                Size = new Vector2(34),
                Colour = SettingsTheme.PaleCyan,
            },
            new SpriteIcon
            {
                Position = new Vector2(24, 22),
                Size = new Vector2(18),
                Icon = itemIcon,
                Colour = HomeControlColours.Navy,
            },
            new SpriteText
            {
                Position = new Vector2(60, 13),
                Text = title,
                Font = HomeTypography.Display(18),
                Colour = HomeControlColours.Navy,
            },
            new SpriteText
            {
                Position = new Vector2(60, 42),
                Text = note,
                Font = HomeTypography.Body(15),
                Colour = SettingsTheme.MutedNavy,
            },
            new Container
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                X = -18,
                Y = -10,
                Size = new Vector2(48, 24),
                Masking = true,
                CornerRadius = 12,
                Children = new Drawable[]
                {
                    switchTrack = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = SettingsTheme.Divider,
                    },
                    switchThumb = new Circle
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.Centre,
                        X = 12,
                        Size = new Vector2(18),
                        Colour = Color4.White,
                    },
                },
            },
            stateText = new SpriteText
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                X = -18,
                Y = 20,
                Font = HomeTypography.Display(15),
                Colour = HomeControlColours.Navy,
            },
        };

        value.BindValueChanged(onValueChanged, true);
    }

    private void onValueChanged(ValueChangedEvent<bool> change)
    {
        switchTrack.FadeColour(
            change.NewValue ? HomeControlColours.Navy : SettingsTheme.Divider,
            120,
            Easing.OutQuint);
        switchThumb.MoveToX(
            change.NewValue ? 36 : 12,
            120,
            Easing.OutQuint);
        stateText.Text = YokkoStrings.Get(change.NewValue
            ? "settings.gameplay.enabled"
            : "settings.gameplay.disabled");
    }

    protected override bool OnHover(HoverEvent e)
    {
        background.FadeColour(SettingsTheme.PaleCyan, 110, Easing.OutQuint);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e) =>
        background.FadeColour(Color4.White, 130, Easing.OutQuint);

    protected override bool OnKeyDown(KeyDownEvent e)
    {
        if (e.Key is Key.Enter or Key.Space)
        {
            Action?.Invoke();
            return true;
        }

        return base.OnKeyDown(e);
    }

    protected override void OnFocus(FocusEvent e)
    {
        base.OnFocus(e);
        BorderColour = HomeControlColours.Pink;
        BorderThickness = 2.4f;
    }

    protected override void OnFocusLost(FocusLostEvent e)
    {
        base.OnFocusLost(e);
        BorderColour = SettingsTheme.Divider;
        BorderThickness = 1.2f;
    }

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
            value.ValueChanged -= onValueChanged;

        base.Dispose(isDisposing);
    }
}

/// <summary>
/// Feedback-section row combining the resume-countdown toggle with a compact
/// duration stepper. Visually follows <see cref="GameplayInlineToggle"/> and
/// dims the stepper while the countdown is disabled.
/// </summary>
internal partial class GameplayCountdownSettingRow : ClickableContainer
{
    private readonly BindableBool enabled;
    private readonly Bindable<double> duration;
    private readonly Box switchTrack;
    private readonly Circle switchThumb;
    private readonly SpriteText stateText;
    private readonly SpriteText titleText;
    private readonly SpriteText valueText;
    private readonly Container stepperHost;

    public override bool AcceptsFocus => true;

    public GameplayCountdownSettingRow(
        LocalisableString title,
        LocalisableString note,
        BindableBool enabled,
        Bindable<double> duration)
    {
        this.enabled = enabled;
        this.duration = duration;
        Action = () => enabled.Value = !enabled.Value;

        InternalChildren = new Drawable[]
        {
            titleText = new SpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                Text = title,
                Font = HomeTypography.Display(17),
                Colour = HomeControlColours.Navy,
            },
            new SpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 142,
                Text = note,
                Font = HomeTypography.Body(15),
                Colour = SettingsTheme.MutedNavy,
            },
            stepperHost = new Container
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                X = -142,
                Size = new Vector2(136, 24),
                Children = new Drawable[]
                {
                    new GameplayCountdownStepButton(
                        FontAwesome.Solid.Minus,
                        Anchor.CentreLeft,
                        () => adjust(
                            -YokkoGameplaySettings
                                .ResumeCountdownStepMilliseconds)),
                    valueText = new SpriteText
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Font = HomeTypography.Display(15),
                        Colour = HomeControlColours.Navy,
                    },
                    new GameplayCountdownStepButton(
                        FontAwesome.Solid.Plus,
                        Anchor.CentreRight,
                        () => adjust(
                            YokkoGameplaySettings
                                .ResumeCountdownStepMilliseconds)),
                },
            },
            new Container
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                X = -82,
                Size = new Vector2(48, 24),
                Masking = true,
                CornerRadius = 12,
                Children = new Drawable[]
                {
                    switchTrack = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = SettingsTheme.Divider,
                    },
                    switchThumb = new Circle
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.Centre,
                        X = 12,
                        Size = new Vector2(18),
                        Colour = Color4.White,
                    },
                },
            },
            stateText = new SpriteText
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                Font = HomeTypography.Display(15),
                Colour = HomeControlColours.Navy,
            },
        };

        enabled.BindValueChanged(onEnabledChanged, true);
        duration.BindValueChanged(onDurationChanged, true);
    }

    internal void AdjustDuration(double delta) => adjust(delta);

    private void adjust(double delta)
    {
        if (!enabled.Value)
            return;

        double step =
            YokkoGameplaySettings.ResumeCountdownStepMilliseconds;
        double next = Math.Clamp(
            duration.Value + delta,
            YokkoGameplaySettings.MinimumResumeCountdownMilliseconds,
            YokkoGameplaySettings.MaximumResumeCountdownMilliseconds);
        duration.Value = Math.Round(next / step) * step;
    }

    private void onEnabledChanged(ValueChangedEvent<bool> change)
    {
        switchTrack.FadeColour(
            change.NewValue ? HomeControlColours.Navy : SettingsTheme.Divider,
            120,
            Easing.OutQuint);
        switchThumb.MoveToX(
            change.NewValue ? 36 : 12,
            120,
            Easing.OutQuint);
        stepperHost.FadeTo(
            change.NewValue ? 1 : 0.35f,
            120,
            Easing.OutQuint);
        stateText.Text = YokkoStrings.Get(change.NewValue
            ? "settings.gameplay.enabled"
            : "settings.gameplay.disabled");
    }

    private void onDurationChanged(ValueChangedEvent<double> change) =>
        valueText.Text = $"{change.NewValue:0} ms";

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
        titleText.FadeColour(HomeControlColours.Pink, 100);
    }

    protected override void OnFocusLost(FocusLostEvent e)
    {
        base.OnFocusLost(e);
        titleText.FadeColour(HomeControlColours.Navy, 100);
    }

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
        {
            enabled.ValueChanged -= onEnabledChanged;
            duration.ValueChanged -= onDurationChanged;
        }

        base.Dispose(isDisposing);
    }
}

internal partial class GameplayCountdownStepButton : ClickableContainer
{
    private readonly Box background;

    public GameplayCountdownStepButton(
        IconUsage itemIcon,
        Anchor anchor,
        Action action)
    {
        Anchor = anchor;
        Origin = anchor;
        Size = new Vector2(26, 24);
        Masking = true;
        CornerRadius = 6;
        BorderThickness = 1.2f;
        BorderColour = SettingsTheme.Divider;
        Action = action;

        InternalChildren = new Drawable[]
        {
            background = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = Color4.White,
            },
            new SpriteIcon
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Size = new Vector2(11),
                Icon = itemIcon,
                Colour = HomeControlColours.Pink,
            },
        };
    }

    protected override bool OnHover(HoverEvent e)
    {
        background.FadeColour(SettingsTheme.PaleCyan, 100, Easing.OutQuint);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e) =>
        background.FadeColour(Color4.White, 120, Easing.OutQuint);
}
