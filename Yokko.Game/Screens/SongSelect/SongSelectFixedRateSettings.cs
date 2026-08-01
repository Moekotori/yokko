using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osuTK;
using Yokko.Core.Mods;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Screens.SongSelect;

/// <summary>
/// Configures lazer's fixed-rate Mania Mods. HT/DT expose AdjustPitch while
/// DC/NC keep their default frequency and vary tempo independently.
/// </summary>
internal partial class SongSelectFixedRateSettings : CompositeDrawable
{
    private readonly SpriteText title;
    private readonly SpriteText speedValue;
    private readonly SpriteText pitchNote;
    private readonly GameplayModSettingsStepButton decreaseButton;
    private readonly GameplayModSettingsStepButton increaseButton;
    private readonly GameplayModSettingsStateButton pitchButton;
    private readonly Action<double> speedChanged;
    private readonly Action<bool> pitchChanged;
    private bool enabled;

    internal ManiaModId ActiveMod { get; private set; } =
        ManiaModId.HalfTime;

    internal double SpeedChange { get; private set; } = 0.75;

    internal bool AdjustPitch { get; private set; }

    internal SongSelectFixedRateSettings(
        Action<double> speedChanged,
        Action<bool> pitchChanged)
    {
        this.speedChanged = speedChanged;
        this.pitchChanged = pitchChanged;
        Size = new Vector2(202, 224);
        InternalChildren =
        [
            title = new SpriteText
            {
                Font = HomeTypography.Display(12),
                Colour = GameplayModSettingsTheme.Text,
            },
            new SpriteText
            {
                Y = 34,
                Text = "SPEED CHANGE",
                Font = HomeTypography.Body(10),
                Colour = GameplayModSettingsTheme.Muted,
            },
            speedValue = new SpriteText
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                Y = 29,
                Font = HomeTypography.Display(18),
                Colour = GameplayModSettingsTheme.Selection,
            },
            decreaseButton = new GameplayModSettingsStepButton(
                "−",
                () => changeSpeed(-0.01))
            {
                Position = new Vector2(0, 58),
            },
            increaseButton = new GameplayModSettingsStepButton(
                "+",
                () => changeSpeed(0.01))
            {
                Position = new Vector2(51, 58),
            },
            pitchButton = new GameplayModSettingsStateButton(
                "ADJUST MUSIC PITCH",
                () =>
                {
                    if (supportsPitchToggle())
                        pitchChanged(!AdjustPitch);
                })
            {
                Position = new Vector2(0, 108),
            },
            pitchNote = new SpriteText
            {
                Y = 153,
                Font = HomeTypography.Body(9),
                Colour = GameplayModSettingsTheme.Accent,
            },
            new SpriteText
            {
                Y = 178,
                Text = "LAZER RANGE · 0.01× PRECISION",
                Font = HomeTypography.Body(8),
                Colour = GameplayModSettingsTheme.Muted,
            },
        ];
        SetState(
            false,
            ManiaModId.HalfTime,
            0.75,
            false);
    }

    internal void SetState(
        bool isEnabled,
        ManiaModId mod,
        double speedChange,
        bool adjustPitch)
    {
        enabled = isEnabled;
        ActiveMod = mod;
        SpeedChange = speedChange;
        AdjustPitch = supportsPitchToggle() && adjustPitch;
        title.Text = OsuManiaModParityCatalog.Get(mod).Name
                     .ToUpperInvariant();
        speedValue.Text = $"{speedChange:0.00}×";

        bool toggleSupported = supportsPitchToggle();
        decreaseButton.SetEnabled(isEnabled);
        increaseButton.SetEnabled(isEnabled);
        pitchButton.SetState(
            isEnabled && toggleSupported,
            AdjustPitch);
        pitchButton.Alpha = toggleSupported ? 1 : 0.38f;
        pitchNote.Text = toggleSupported
            ? "OFF PRESERVES PITCH · ON SCALES WITH RATE"
            : mod == ManiaModId.Daycore
                ? "MUSIC FREQUENCY LOCKED TO 0.75×"
                : "MUSIC FREQUENCY LOCKED TO 1.50×";

        this.ClearTransforms();
        this.FadeTo(isEnabled ? 1 : 0.42f, 120, Easing.OutQuint);
    }

    private void changeSpeed(double delta)
    {
        if (!enabled)
            return;

        bool slow = ActiveMod is ManiaModId.HalfTime
            or ManiaModId.Daycore;
        double minimum = slow ? 0.5 : 1.01;
        double maximum = slow ? 0.99 : 2;
        speedChanged(Math.Round(
            Math.Clamp(SpeedChange + delta, minimum, maximum),
            2));
    }

    private bool supportsPitchToggle() =>
        ActiveMod is ManiaModId.HalfTime
            or ManiaModId.DoubleTime;
}
