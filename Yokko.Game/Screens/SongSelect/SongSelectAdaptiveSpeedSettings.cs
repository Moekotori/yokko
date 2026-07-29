using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Screens.SongSelect;

internal partial class SongSelectAdaptiveSpeedSettings : CompositeDrawable
{
    private readonly SpriteText initialValue;
    private readonly ToggleButton pitchButton;
    private readonly Action<double> initialChanged;
    private readonly Action<bool> pitchChanged;
    private bool enabled;

    internal double InitialRate { get; private set; } = 1;
    internal bool AdjustPitch { get; private set; } = true;

    internal SongSelectAdaptiveSpeedSettings(
        Action<double> initialChanged,
        Action<bool> pitchChanged)
    {
        this.initialChanged = initialChanged;
        this.pitchChanged = pitchChanged;
        Size = new Vector2(202, 224);
        InternalChildren =
        [
            new SpriteText
            {
                Text = "ADAPTIVE SPEED",
                Font = HomeTypography.Display(12),
                Colour = GameplayModSettingsTheme.Text,
            },
            new SpriteText
            {
                Y = 34,
                Text = "INITIAL RATE",
                Font = HomeTypography.Body(10),
                Colour = GameplayModSettingsTheme.Muted,
            },
            initialValue = new SpriteText
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                Y = 29,
                Font = HomeTypography.Display(18),
                Colour = GameplayModSettingsTheme.Selection,
            },
            new StepButton("−", () => changeInitial(-0.05))
            {
                Position = new Vector2(0, 58),
            },
            new StepButton("+", () => changeInitial(0.05))
            {
                Position = new Vector2(51, 58),
            },
            pitchButton = new ToggleButton(
                "ADJUST MUSIC PITCH",
                () => pitchChanged(!AdjustPitch))
            {
                Position = new Vector2(0, 108),
            },
            new SpriteText
            {
                Y = 159,
                Text = "RESPONDS TO YOUR LAST 8 RESULTS",
                Font = HomeTypography.Body(9),
                Colour = GameplayModSettingsTheme.Accent,
            },
            new SpriteText
            {
                Y = 178,
                Text = "EARLY HITS SPEED UP · MISSES SLOW DOWN",
                Font = HomeTypography.Body(8),
                Colour = GameplayModSettingsTheme.Muted,
            },
        ];
        SetState(false, 1, true);
    }

    internal void SetState(
        bool isEnabled,
        double initialRate,
        bool adjustPitch)
    {
        enabled = isEnabled;
        InitialRate = initialRate;
        AdjustPitch = adjustPitch;
        initialValue.Text = $"{initialRate:0.00}×";
        pitchButton.SetState(isEnabled, adjustPitch);
        this.ClearTransforms();
        this.FadeTo(isEnabled ? 1 : 0.42f, 120, Easing.OutQuint);
    }

    private void changeInitial(double delta)
    {
        if (!enabled)
            return;

        initialChanged(Math.Round(
            Math.Clamp(InitialRate + delta, 0.5, 2),
            2));
    }

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

        internal void SetState(bool isEnabled, bool selected)
        {
            background.Colour = selected
                ? GameplayModSettingsTheme.Selection
                : GameplayModSettingsTheme.Control;
            state.Text = selected ? "ON" : "OFF";
            state.Colour = selected
                ? GameplayModSettingsTheme.AccentOn
                : GameplayModSettingsTheme.Muted;
            Alpha = isEnabled ? 1 : 0.55f;
        }
    }
}
