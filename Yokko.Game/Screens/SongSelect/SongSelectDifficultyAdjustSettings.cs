using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osuTK;
using osuTK.Graphics;
using Yokko.Game.Screens.Main;
using Yokko.Game.Localisation;

namespace Yokko.Game.Screens.SongSelect;

internal partial class SongSelectDifficultyAdjustSettings
    : CompositeDrawable
{
    private readonly SpriteText drainValueText;
    private readonly SpriteText difficultyValueText;
    private readonly DifficultySlider drainSlider;
    private readonly DifficultySlider difficultySlider;
    private readonly SettingButton mapValuesButton;
    private readonly SettingButton extendedButton;
    private readonly SpriteText statusText;

    internal double? DrainRate { get; private set; }
    internal double? OverallDifficulty { get; private set; }
    internal bool ExtendedLimits { get; private set; }

    public SongSelectDifficultyAdjustSettings(
        Action<double?> drainRateChanged,
        Action<double?> overallDifficultyChanged,
        Action useMapValues,
        Action<bool> extendedLimitsChanged)
    {
        Size = new Vector2(202, 224);

        InternalChildren = new Drawable[]
        {
            new SpriteText
            {
                Text = YokkoStrings.Get(
                    "mods.definition.difficulty-adjust.name"),
                Font = HomeTypography.Display(12),
                Spacing = new Vector2(0.35f, 0),
                Colour = GameplayModSettingsTheme.Text,
            },
            new SpriteText
            {
                Y = 24,
                Text = YokkoStrings.Get("mods.settings.hp_drain"),
                Font = HomeTypography.Body(10),
                Colour = GameplayModSettingsTheme.Muted,
            },
            drainValueText = valueText(19),
            drainSlider = new DifficultySlider(drainRateChanged)
            {
                Y = 48,
            },
            new SpriteText
            {
                Y = 80,
                Text = YokkoStrings.Get(
                    "mods.settings.judgement_difficulty"),
                Font = HomeTypography.Body(10),
                Colour = GameplayModSettingsTheme.Muted,
            },
            difficultyValueText = valueText(75),
            difficultySlider =
                new DifficultySlider(overallDifficultyChanged)
                {
                    Y = 104,
                },
            mapValuesButton = new SettingButton(
                YokkoStrings.Get("mods.settings.map_values"),
                useMapValues)
            {
                Position = new Vector2(0, 143),
            },
            extendedButton = new SettingButton(
                YokkoStrings.Get("mods.settings.extended_range"),
                () => extendedLimitsChanged(!ExtendedLimits))
            {
                Position = new Vector2(105, 143),
            },
            statusText = new SpriteText
            {
                Y = 190,
                Font = HomeTypography.Body(10),
                Colour = GameplayModSettingsTheme.Accent,
            },
        };
    }

    public void SetState(
        bool isEnabled,
        double mapDrainRate,
        double mapOverallDifficulty,
        double? drainRate,
        double? overallDifficulty,
        bool extendedLimits)
    {
        DrainRate = drainRate;
        OverallDifficulty = overallDifficulty;
        ExtendedLimits = extendedLimits;

        double effectiveDrain = drainRate ?? mapDrainRate;
        double effectiveDifficulty =
            overallDifficulty ?? mapOverallDifficulty;
        double maxDrain = extendedLimits ? 11 : 10;
        double minDifficulty = extendedLimits ? -15 : 0;
        double maxDifficulty = extendedLimits ? 15 : 10;

        drainValueText.Text = formatValue(drainRate, effectiveDrain);
        difficultyValueText.Text =
            formatValue(overallDifficulty, effectiveDifficulty);
        drainSlider.SetState(
            isEnabled,
            effectiveDrain,
            0,
            maxDrain);
        difficultySlider.SetState(
            isEnabled,
            effectiveDifficulty,
            minDifficulty,
            maxDifficulty);
        mapValuesButton.SetState(
            isEnabled,
            drainRate == null && overallDifficulty == null);
        extendedButton.SetState(isEnabled, extendedLimits);
        statusText.Text = isEnabled
            ? $"HP {effectiveDrain:0.0}  ·  OD {effectiveDifficulty:0.0}"
            : YokkoStrings.Get("mods.settings.select_first", "DA");
        this.ClearTransforms();
        this.FadeTo(isEnabled ? 1 : 0.42f, 120, Easing.OutQuint);
    }

    private static SpriteText valueText(float y) => new()
    {
        Anchor = Anchor.TopRight,
        Origin = Anchor.TopRight,
        Y = y,
        Font = HomeTypography.Display(17),
        Colour = GameplayModSettingsTheme.Selection,
    };

    private static LocalisableString formatValue(
        double? configured,
        double effective) =>
        configured == null
            ? YokkoStrings.Get(
                "mods.settings.map_value",
                effective.ToString("0.0"))
            : effective.ToString("0.0");

    private partial class DifficultySlider : CompositeDrawable
    {
        private const float trackWidth = 202;
        private readonly Action<double?> changed;
        private readonly Box fill;
        private readonly Circle knob;
        private bool enabled;
        private double minimum;
        private double maximum = 10;

        public DifficultySlider(Action<double?> changed)
        {
            this.changed = changed;
            Size = new Vector2(trackWidth, 28);
            InternalChildren = new Drawable[]
            {
                new Box
                {
                    Y = 11,
                    Size = new Vector2(trackWidth, 5),
                    Colour = new Color4(
                        GameplayModSettingsTheme.Control.R,
                        GameplayModSettingsTheme.Control.G,
                        GameplayModSettingsTheme.Control.B,
                        0.9f),
                },
                fill = new Box
                {
                    Y = 11,
                    Height = 5,
                    Colour = GameplayModSettingsTheme.Selection,
                },
                knob = new Circle
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.Centre,
                    Y = 2,
                    Size = new Vector2(14),
                    Colour = GameplayModSettingsTheme.Text,
                    BorderThickness = 2.5f,
                    BorderColour = GameplayModSettingsTheme.Selection,
                },
            };
        }

        public void SetState(
            bool isEnabled,
            double value,
            double minimum,
            double maximum)
        {
            enabled = isEnabled;
            this.minimum = minimum;
            this.maximum = maximum;
            double progress = Math.Clamp(
                (value - minimum) / (maximum - minimum),
                0,
                1);
            float x = (float)(progress * trackWidth);
            fill.Width = x;
            knob.X = x;
        }

        protected override bool OnMouseDown(MouseDownEvent e)
        {
            updateFrom(e.ScreenSpaceMousePosition);
            return true;
        }

        protected override bool OnDragStart(DragStartEvent e) => enabled;

        protected override void OnDrag(DragEvent e) =>
            updateFrom(e.ScreenSpaceMousePosition);

        private void updateFrom(Vector2 screenPosition)
        {
            if (!enabled)
                return;

            double progress = Math.Clamp(
                ToLocalSpace(screenPosition).X / trackWidth,
                0,
                1);
            changed(Math.Round(
                minimum + progress * (maximum - minimum),
                1));
        }
    }

    private partial class SettingButton : ClickableContainer
    {
        private readonly Box background;
        private readonly SpriteText label;
        private bool enabled;

        public SettingButton(LocalisableString text, Action action)
        {
            Action = () =>
            {
                if (enabled)
                    action();
            };
            Size = new Vector2(97, 34);
            Masking = true;
            CornerRadius = 5;
            BorderThickness = 1.5f;
            InternalChildren = new Drawable[]
            {
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                },
                label = new SpriteText
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Text = text,
                    Font = HomeTypography.Display(9),
                },
            };
        }

        public void SetState(bool isEnabled, bool selected)
        {
            enabled = isEnabled;
            BorderColour = selected
                ? GameplayModSettingsTheme.Selection
                : GameplayModSettingsTheme.Accent;
            background.Colour = selected
                ? GameplayModSettingsTheme.Selection
                : new Color4(
                    GameplayModSettingsTheme.Control.R,
                    GameplayModSettingsTheme.Control.G,
                    GameplayModSettingsTheme.Control.B,
                    0.72f);
            label.Colour = selected
                ? GameplayModSettingsTheme.AccentOn
                : GameplayModSettingsTheme.Text;
        }
    }
}
