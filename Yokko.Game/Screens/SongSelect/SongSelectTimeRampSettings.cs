using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osuTK;
using osuTK.Graphics;
using Yokko.Core.Mods;
using Yokko.Game.Localisation;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Screens.SongSelect;

internal partial class SongSelectTimeRampSettings : CompositeDrawable
{
    private readonly SpriteText title;
    private readonly SpriteText initialValue;
    private readonly SpriteText finalValue;
    private readonly GameplayModSettingsStateButton pitchButton;
    private readonly GameplayModSettingsStepButton initialDecreaseButton;
    private readonly GameplayModSettingsStepButton initialIncreaseButton;
    private readonly GameplayModSettingsStepButton finalDecreaseButton;
    private readonly GameplayModSettingsStepButton finalIncreaseButton;
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
            label(YokkoStrings.Get("mods.settings.initial_rate"), 29),
            initialValue = value(24),
            initialDecreaseButton = new GameplayModSettingsStepButton(
                "−",
                () => changeInitial(-0.05))
            {
                Position = new Vector2(0, 50),
            },
            initialIncreaseButton = new GameplayModSettingsStepButton(
                "+",
                () => changeInitial(0.05))
            {
                Position = new Vector2(51, 50),
            },
            label(YokkoStrings.Get("mods.settings.final_rate"), 91),
            finalValue = value(86),
            finalDecreaseButton = new GameplayModSettingsStepButton(
                "−",
                () => changeFinal(-0.05))
            {
                Position = new Vector2(0, 112),
            },
            finalIncreaseButton = new GameplayModSettingsStepButton(
                "+",
                () => changeFinal(0.05))
            {
                Position = new Vector2(51, 112),
            },
            pitchButton = new GameplayModSettingsStateButton(
                YokkoStrings.Get("mods.settings.adjust_pitch"),
                () => pitchChanged(!AdjustPitch))
            {
                Position = new Vector2(0, 158),
            },
            new SpriteText
            {
                Y = 204,
                Text = YokkoStrings.Get("mods.settings.final_rate_at_75"),
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
        title.Text = YokkoStrings.ModName(
            OsuManiaModParityCatalog.Get(mod));
        initialValue.Text = $"{initialRate:0.00}×";
        finalValue.Text = $"{finalRate:0.00}×";
        initialDecreaseButton.SetEnabled(isEnabled);
        initialIncreaseButton.SetEnabled(isEnabled);
        finalDecreaseButton.SetEnabled(isEnabled);
        finalIncreaseButton.SetEnabled(isEnabled);
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

    private static SpriteText label(LocalisableString text, float y) => new()
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

}
