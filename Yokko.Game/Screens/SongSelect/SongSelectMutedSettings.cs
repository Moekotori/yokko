using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osuTK;
using Yokko.Game.Localisation;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Screens.SongSelect;

internal partial class SongSelectMutedSettings : CompositeDrawable
{
    private readonly SpriteText comboValue;
    private readonly GameplayModSettingsStepButton decreaseButton;
    private readonly GameplayModSettingsStepButton increaseButton;
    private readonly GameplayModSettingsStateButton inverseButton;
    private readonly GameplayModSettingsStateButton metronomeButton;
    private readonly GameplayModSettingsStateButton hitSoundsButton;
    private readonly Action<bool> inverseChanged;
    private readonly Action<bool> metronomeChanged;
    private readonly Action<int> comboChanged;
    private readonly Action<bool> hitSoundsChanged;
    private bool enabled;

    internal bool Inverse { get; private set; }
    internal bool Metronome { get; private set; }
    internal int ComboCount { get; private set; }
    internal bool AffectsHitSounds { get; private set; }

    internal SongSelectMutedSettings(
        Action<bool> inverseChanged,
        Action<bool> metronomeChanged,
        Action<int> comboChanged,
        Action<bool> hitSoundsChanged)
    {
        this.inverseChanged = inverseChanged;
        this.metronomeChanged = metronomeChanged;
        this.comboChanged = comboChanged;
        this.hitSoundsChanged = hitSoundsChanged;
        Size = new Vector2(202, 224);

        InternalChildren =
        [
            new SpriteText
            {
                Text = YokkoStrings.Get("mods.definition.muted.name"),
                Font = HomeTypography.Display(12),
                Colour = GameplayModSettingsTheme.Text,
            },
            new SpriteText
            {
                Y = 25,
                Text = YokkoStrings.Get("mods.settings.fade_length"),
                Font = HomeTypography.Body(10),
                Colour = GameplayModSettingsTheme.Muted,
            },
            comboValue = new SpriteText
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                Y = 19,
                Font = HomeTypography.Display(18),
                Colour = GameplayModSettingsTheme.Selection,
            },
            decreaseButton = new GameplayModSettingsStepButton(
                "− 25",
                () => changeCombo(-25),
                GameplayModSettingsControlStyle.Muted)
            {
                Position = new Vector2(0, 48),
            },
            increaseButton = new GameplayModSettingsStepButton(
                "+ 25",
                () => changeCombo(25),
                GameplayModSettingsControlStyle.Muted)
            {
                Position = new Vector2(105, 48),
            },
            inverseButton = new GameplayModSettingsStateButton(
                YokkoStrings.Get("mods.settings.start_muted"),
                () => inverseChanged(!Inverse),
                GameplayModSettingsControlStyle.Muted)
            {
                Position = new Vector2(0, 91),
            },
            metronomeButton = new GameplayModSettingsStateButton(
                YokkoStrings.Get("mods.settings.keep_metronome"),
                () => metronomeChanged(!Metronome),
                GameplayModSettingsControlStyle.Muted)
            {
                Position = new Vector2(0, 126),
            },
            hitSoundsButton = new GameplayModSettingsStateButton(
                YokkoStrings.Get("mods.settings.mute_keysounds"),
                () => hitSoundsChanged(!AffectsHitSounds),
                GameplayModSettingsControlStyle.Muted)
            {
                Position = new Vector2(0, 161),
            },
            new SpriteText
            {
                Y = 204,
                Text = YokkoStrings.Get("mods.settings.smooth_fade"),
                Font = HomeTypography.Body(9),
                Colour = GameplayModSettingsTheme.Accent,
            },
        ];
    }

    internal void SetState(
        bool isEnabled,
        bool inverse,
        bool metronome,
        int comboCount,
        bool affectsHitSounds)
    {
        enabled = isEnabled;
        Inverse = inverse;
        Metronome = metronome;
        ComboCount = comboCount;
        AffectsHitSounds = affectsHitSounds;
        comboValue.Text = YokkoStrings.Get(
            "mods.settings.combo_count",
            comboCount);
        decreaseButton.SetEnabled(isEnabled);
        increaseButton.SetEnabled(isEnabled);
        inverseButton.SetState(isEnabled, inverse);
        metronomeButton.SetState(isEnabled, metronome);
        hitSoundsButton.SetState(isEnabled, affectsHitSounds);
        this.ClearTransforms();
        this.FadeTo(isEnabled ? 1 : 0.42f, 120, Easing.OutQuint);
    }

    private void changeCombo(int delta)
    {
        if (!enabled)
            return;
        comboChanged(Math.Clamp(
            ComboCount + delta,
            Inverse ? 1 : 0,
            500));
    }
}
