using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Screens.SongSelect;

internal partial class SongSelectMutedSettings : CompositeDrawable
{
    private readonly SpriteText comboValue;
    private readonly SettingToggle inverseButton;
    private readonly SettingToggle metronomeButton;
    private readonly SettingToggle hitSoundsButton;
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
                Text = "MUTED",
                Font = HomeTypography.Display(12),
                Colour = GameplayModSettingsTheme.Text,
            },
            new SpriteText
            {
                Y = 25,
                Text = "FADE LENGTH",
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
            new StepButton("− 25", () => changeCombo(-25))
            {
                Position = new Vector2(0, 48),
            },
            new StepButton("+ 25", () => changeCombo(25))
            {
                Position = new Vector2(105, 48),
            },
            inverseButton = new SettingToggle(
                "START MUTED",
                () => inverseChanged(!Inverse))
            {
                Position = new Vector2(0, 91),
            },
            metronomeButton = new SettingToggle(
                "METRONOME",
                () => metronomeChanged(!Metronome))
            {
                Position = new Vector2(0, 126),
            },
            hitSoundsButton = new SettingToggle(
                "MUTE KEYSOUNDS",
                () => hitSoundsChanged(!AffectsHitSounds))
            {
                Position = new Vector2(0, 161),
            },
            new SpriteText
            {
                Y = 204,
                Text = "500 MS SMOOTH AUDIO FADE",
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
        comboValue.Text = $"{comboCount} COMBO";
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

    private partial class StepButton : ClickableContainer
    {
        internal StepButton(string text, Action action)
        {
            Action = action;
            Size = new Vector2(97, 30);
            Masking = true;
            CornerRadius = 5;
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
                        0.78f),
                },
                new SpriteText
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Text = text,
                    Font = HomeTypography.Display(10),
                    Colour = GameplayModSettingsTheme.Text,
                },
            ];
        }
    }

    private partial class SettingToggle : ClickableContainer
    {
        private readonly Box background;
        private readonly SpriteText stateText;

        internal SettingToggle(string text, Action action)
        {
            Action = action;
            Size = new Vector2(202, 27);
            Masking = true;
            CornerRadius = 4;
            InternalChildren =
            [
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                },
                new SpriteText
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    X = 9,
                    Text = text,
                    Font = HomeTypography.Body(10),
                    Colour = GameplayModSettingsTheme.Text,
                },
                stateText = new SpriteText
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
                ? new Color4(
                    GameplayModSettingsTheme.Selection.R,
                    GameplayModSettingsTheme.Selection.G,
                    GameplayModSettingsTheme.Selection.B,
                    0.72f)
                : new Color4(
                    GameplayModSettingsTheme.Control.R,
                    GameplayModSettingsTheme.Control.G,
                    GameplayModSettingsTheme.Control.B,
                    0.8f);
            stateText.Text = selected ? "ON" : "OFF";
            stateText.Colour = selected
                ? GameplayModSettingsTheme.AccentOn
                : GameplayModSettingsTheme.Muted;
            Alpha = enabled ? 1 : 0.55f;
        }
    }
}
