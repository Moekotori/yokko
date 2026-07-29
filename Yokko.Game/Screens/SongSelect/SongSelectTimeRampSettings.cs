using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;
using Yokko.Core.Mods;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Screens.SongSelect;

internal partial class SongSelectTimeRampSettings : CompositeDrawable
{
    private readonly SpriteText title;
    private readonly SpriteText initialValue;
    private readonly SpriteText finalValue;
    private readonly ToggleButton pitchButton;
    private readonly Action<double> initialChanged;
    private readonly Action<double> finalChanged;
    private readonly Action<bool> pitchChanged;
    private bool enabled;

    internal ManiaModId ActiveMod { get; private set; } = ManiaModId.WindUp;
    internal double InitialRate { get; private set; } = 1;
    internal double FinalRate { get; private set; } = 1.5;
    internal bool AdjustPitch { get; private set; } = true;

    internal SongSelectTimeRampSettings(
        Action<double> initialChanged,
        Action<double> finalChanged,
        Action<bool> pitchChanged)
    {
        this.initialChanged = initialChanged;
        this.finalChanged = finalChanged;
        this.pitchChanged = pitchChanged;
        Size = new Vector2(202, 224);
        InternalChildren =
        [
            title = new SpriteText
            {
                Font = HomeTypography.Display(12),
                Colour = GameplayModSettingsTheme.Text,
            },
            label("INITIAL RATE", 29),
            initialValue = value(24),
            new StepButton("−", () => changeInitial(-0.05))
            {
                Position = new Vector2(0, 50),
            },
            new StepButton("+", () => changeInitial(0.05))
            {
                Position = new Vector2(51, 50),
            },
            label("FINAL RATE", 91),
            finalValue = value(86),
            new StepButton("−", () => changeFinal(-0.05))
            {
                Position = new Vector2(0, 112),
            },
            new StepButton("+", () => changeFinal(0.05))
            {
                Position = new Vector2(51, 112),
            },
            pitchButton = new ToggleButton(
                "ADJUST MUSIC PITCH",
                () => pitchChanged(!AdjustPitch))
            {
                Position = new Vector2(0, 158),
            },
            new SpriteText
            {
                Y = 204,
                Text = "FINAL RATE AT 75% OF MAP",
                Font = HomeTypography.Body(9),
                Colour = GameplayModSettingsTheme.Accent,
            },
        ];
        SetState(false, ManiaModId.WindUp, 1, 1.5, true);
    }

    internal void SetState(
        bool isEnabled,
        ManiaModId mod,
        double initialRate,
        double finalRate,
        bool adjustPitch)
    {
        enabled = isEnabled;
        ActiveMod = mod;
        InitialRate = initialRate;
        FinalRate = finalRate;
        AdjustPitch = adjustPitch;
        title.Text = mod == ManiaModId.WindDown
            ? "WIND DOWN"
            : "WIND UP";
        initialValue.Text = $"{initialRate:0.00}×";
        finalValue.Text = $"{finalRate:0.00}×";
        pitchButton.SetState(isEnabled, adjustPitch);
        this.ClearTransforms();
        this.FadeTo(isEnabled ? 1 : 0.42f, 120, Easing.OutQuint);
    }

    private void changeInitial(double delta)
    {
        if (!enabled)
            return;
        double min = ActiveMod == ManiaModId.WindUp ? 0.5 : 0.51;
        double max = ActiveMod == ManiaModId.WindUp
            ? Math.Min(1.99, FinalRate - 0.01)
            : 2;
        if (ActiveMod == ManiaModId.WindDown)
            min = Math.Max(min, FinalRate + 0.01);
        initialChanged(Math.Round(Math.Clamp(InitialRate + delta, min, max), 2));
    }

    private void changeFinal(double delta)
    {
        if (!enabled)
            return;
        double min = ActiveMod == ManiaModId.WindUp
            ? Math.Max(0.51, InitialRate + 0.01)
            : 0.5;
        double max = ActiveMod == ManiaModId.WindUp
            ? 2
            : Math.Min(1.99, InitialRate - 0.01);
        finalChanged(Math.Round(Math.Clamp(FinalRate + delta, min, max), 2));
    }

    private static SpriteText label(string text, float y) => new()
    {
        Y = y,
        Text = text,
        Font = HomeTypography.Body(10),
        Colour = GameplayModSettingsTheme.Muted,
    };

    private static SpriteText value(float y) => new()
    {
        Anchor = Anchor.TopRight,
        Origin = Anchor.TopRight,
        Y = y,
        Font = HomeTypography.Display(18),
        Colour = GameplayModSettingsTheme.Selection,
    };

    private partial class StepButton : ClickableContainer
    {
        internal StepButton(string text, Action action)
        {
            Action = action;
            Size = new Vector2(46, 29);
            Masking = true;
            CornerRadius = 4;
            BorderThickness = 1;
            BorderColour = GameplayModSettingsTheme.Accent;
            InternalChildren =
            [
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new Color4(
                        GameplayModSettingsTheme.Control.R,
                        GameplayModSettingsTheme.Control.G,
                        GameplayModSettingsTheme.Control.B,
                        0.8f),
                },
                new SpriteText
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Text = text,
                    Font = HomeTypography.Display(14),
                    Colour = GameplayModSettingsTheme.Text,
                },
            ];
        }
    }

    private partial class ToggleButton : ClickableContainer
    {
        private readonly Box background;
        private readonly SpriteText state;

        internal ToggleButton(string text, Action action)
        {
            Action = action;
            Size = new Vector2(202, 29);
            Masking = true;
            CornerRadius = 4;
            InternalChildren =
            [
                background = new Box { RelativeSizeAxes = Axes.Both },
                new SpriteText
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    X = 9,
                    Text = text,
                    Font = HomeTypography.Body(10),
                    Colour = GameplayModSettingsTheme.Text,
                },
                state = new SpriteText
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    X = -9,
                    Font = HomeTypography.Display(9),
                },
            ];
        }

        internal void SetState(bool enabled, bool selected)
        {
            background.Colour = selected
                ? GameplayModSettingsTheme.Selection
                : GameplayModSettingsTheme.Control;
            state.Text = selected ? "ON" : "OFF";
            state.Colour = selected
                ? GameplayModSettingsTheme.AccentOn
                : GameplayModSettingsTheme.Muted;
            Alpha = enabled ? 1 : 0.55f;
        }
    }
}
