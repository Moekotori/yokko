using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osuTK;
using Yokko.Game.Localisation;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Screens.SongSelect;

internal partial class SongSelectAdaptiveSpeedSettings : CompositeDrawable
{
    private readonly SpriteText initialValue;
    private readonly GameplayModSettingsStepButton decreaseButton;
    private readonly GameplayModSettingsStepButton increaseButton;
    private readonly GameplayModSettingsStateButton pitchButton;
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
                Text = YokkoStrings.Get(
                    "mods.definition.adaptive-speed.name"),
                Font = HomeTypography.Display(12),
                Colour = GameplayModSettingsTheme.Text,
            },
            new SpriteText
            {
                Y = 34,
                Text = YokkoStrings.Get("mods.settings.initial_rate"),
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
            decreaseButton = new GameplayModSettingsStepButton(
                "−",
                () => changeInitial(-0.05))
            {
                Position = new Vector2(0, 58),
            },
            increaseButton = new GameplayModSettingsStepButton(
                "+",
                () => changeInitial(0.05))
            {
                Position = new Vector2(51, 58),
            },
            pitchButton = new GameplayModSettingsStateButton(
                YokkoStrings.Get("mods.settings.adjust_pitch"),
                () => pitchChanged(!AdjustPitch))
            {
                Position = new Vector2(0, 108),
            },
            new SpriteText
            {
                Y = 159,
                Text = YokkoStrings.Get("mods.settings.adaptive_recent"),
                Font = HomeTypography.Body(9),
                Colour = GameplayModSettingsTheme.Accent,
            },
            new SpriteText
            {
                Y = 178,
                Text = YokkoStrings.Get("mods.settings.adaptive_rule"),
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
        decreaseButton.SetEnabled(isEnabled);
        increaseButton.SetEnabled(isEnabled);
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
}
