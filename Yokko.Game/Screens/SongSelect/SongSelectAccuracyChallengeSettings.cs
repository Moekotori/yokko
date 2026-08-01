using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osuTK;
using osuTK.Graphics;
using Yokko.Core.Mods;
using Yokko.Game.Localisation;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Screens.SongSelect;

internal partial class SongSelectAccuracyChallengeSettings
    : CompositeDrawable
{
    private readonly SpriteText valueText;
    private readonly SpriteText statusText;
    private readonly AccuracySlider slider;
    private readonly ModeButton maximumButton;
    private readonly ModeButton standardButton;
    private bool enabled;

    internal double MinimumAccuracy { get; private set; } = 0.9;
    internal ManiaAccuracyMode Mode { get; private set; } =
        ManiaAccuracyMode.MaximumAchievable;

    public SongSelectAccuracyChallengeSettings(
        Action<double> minimumChanged,
        Action<ManiaAccuracyMode> modeChanged)
    {
        Size = new Vector2(202, 224);

        InternalChildren = new Drawable[]
        {
            new SpriteText
            {
                Text = YokkoStrings.Get(
                    "mods.definition.accuracy-challenge.name"),
                Font = HomeTypography.Display(12),
                Spacing = new Vector2(0.35f, 0),
                Colour = GameplayModSettingsTheme.Text,
            },
            new SpriteText
            {
                Y = 24,
                Text = YokkoStrings.Get("mods.settings.minimum_accuracy"),
                Font = HomeTypography.Body(10),
                Colour = GameplayModSettingsTheme.Muted,
            },
            valueText = new SpriteText
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                Y = 19,
                Font = HomeTypography.Display(19),
                Colour = GameplayModSettingsTheme.Selection,
            },
            slider = new AccuracySlider(minimumChanged)
            {
                Y = 50,
            },
            new SpriteText
            {
                Y = 85,
                Text = "60.0%",
                Font = HomeTypography.Body(9),
                Colour = GameplayModSettingsTheme.Muted,
            },
            new SpriteText
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                Y = 85,
                Text = "99.9%",
                Font = HomeTypography.Body(9),
                Colour = GameplayModSettingsTheme.Muted,
            },
            new Box
            {
                Y = 108,
                Size = new Vector2(202, 1),
                Colour = new Color4(
                    GameplayModSettingsTheme.Accent.R,
                    GameplayModSettingsTheme.Accent.G,
                    GameplayModSettingsTheme.Accent.B,
                    0.25f),
            },
            new SpriteText
            {
                Y = 122,
                Text = YokkoStrings.Get("mods.settings.fail_rule"),
                Font = HomeTypography.Body(10),
                Colour = GameplayModSettingsTheme.Muted,
            },
            maximumButton = new ModeButton(
                YokkoStrings.Get("mods.settings.maximum_possible"),
                () => modeChanged(
                    ManiaAccuracyMode.MaximumAchievable))
            {
                Position = new Vector2(0, 145),
            },
            standardButton = new ModeButton(
                YokkoStrings.Get("mods.settings.current_accuracy"),
                () => modeChanged(ManiaAccuracyMode.Standard))
            {
                Position = new Vector2(105, 145),
            },
            statusText = new SpriteText
            {
                Y = 195,
                Font = HomeTypography.Body(10),
                Colour = GameplayModSettingsTheme.Accent,
            },
        };

        SetState(
            false,
            0.9,
            ManiaAccuracyMode.MaximumAchievable);
    }

    public void SetState(
        bool isEnabled,
        double minimumAccuracy,
        ManiaAccuracyMode mode)
    {
        enabled = isEnabled;
        MinimumAccuracy = minimumAccuracy;
        Mode = mode;

        valueText.Text = $"{minimumAccuracy * 100:0.0}%";
        slider.SetState(isEnabled, minimumAccuracy);
        maximumButton.SetState(
            isEnabled,
            mode == ManiaAccuracyMode.MaximumAchievable);
        standardButton.SetState(
            isEnabled,
            mode == ManiaAccuracyMode.Standard);
        statusText.Text = isEnabled
            ? mode == ManiaAccuracyMode.MaximumAchievable
                ? YokkoStrings.Get(
                    "mods.settings.fail_below_reachable",
                    $"{minimumAccuracy * 100:0.0}%")
                : YokkoStrings.Get(
                    "mods.settings.fail_below_current",
                    $"{minimumAccuracy * 100:0.0}%")
            : YokkoStrings.Get("mods.settings.select_first", "AC");
        this.ClearTransforms();
        this.FadeTo(isEnabled ? 1 : 0.42f, 120, Easing.OutQuint);
    }

    private partial class AccuracySlider : CompositeDrawable
    {
        private const double minimum = 0.6;
        private const double maximum = 0.999;
        private const float trackWidth = 202;
        private readonly Action<double> changed;
        private readonly Box fill;
        private readonly Circle knob;
        private bool enabled;

        public AccuracySlider(Action<double> changed)
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

        public void SetState(bool isEnabled, double value)
        {
            enabled = isEnabled;
            double progress =
                Math.Clamp((value - minimum) / (maximum - minimum), 0, 1);
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
            double value = Math.Round(
                minimum + progress * (maximum - minimum),
                3);
            changed(value);
        }
    }

    private partial class ModeButton : ClickableContainer
    {
        private readonly Box background;
        private readonly SpriteText label;
        private bool enabled;

        public ModeButton(LocalisableString text, Action action)
        {
            Action = () =>
            {
                if (enabled)
                    action();
            };
            Size = new Vector2(97, 38);
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
                    Font = HomeTypography.Display(10),
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
