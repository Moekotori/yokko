using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osuTK;
using Yokko.Game.Localisation;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Screens.SongSelect;

internal partial class SongSelectNoPauseSettings : CompositeDrawable
{
    private readonly Action<int> allowedPausesChanged;
    private readonly SpriteText valueText;
    private readonly SpriteText statusText;
    private readonly StepButton decreaseButton;
    private readonly StepButton increaseButton;
    private bool enabled;

    internal int AllowedPauses { get; private set; }

    internal SongSelectNoPauseSettings(Action<int> allowedPausesChanged)
    {
        this.allowedPausesChanged = allowedPausesChanged;
        Size = new Vector2(202, 224);

        InternalChildren =
        [
            new SpriteText
            {
                Text = YokkoStrings.Get("mods.definition.no-pause.name"),
                Font = HomeTypography.Display(14),
                Colour = GameplayModSettingsTheme.Text,
            },
            new SpriteText
            {
                Y = 27,
                Text = YokkoStrings.Get("mods.settings.allowed_pauses"),
                Font = HomeTypography.Body(12),
                Colour = GameplayModSettingsTheme.Muted,
            },
            decreaseButton = new StepButton("−", () => changeBy(-1))
            {
                Position = new Vector2(0, 56),
            },
            new Container
            {
                Position = new Vector2(51, 56),
                Size = new Vector2(100, 48),
                Masking = true,
                CornerRadius = 5,
                BorderThickness = 1.5f,
                BorderColour = GameplayModSettingsTheme.Accent,
                Children =
                [
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = GameplayModSettingsTheme.Surface,
                    },
                    valueText = new SpriteText
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Font = HomeTypography.Display(22),
                        Colour = GameplayModSettingsTheme.Text,
                    },
                ],
            },
            increaseButton = new StepButton("+", () => changeBy(1))
            {
                Position = new Vector2(157, 56),
            },
            new SpriteText
            {
                Y = 122,
                Text = YokkoStrings.Get("mods.settings.zero_pauses"),
                Font = HomeTypography.Body(11),
                Colour = GameplayModSettingsTheme.Text,
            },
            statusText = new SpriteText
            {
                Y = 154,
                Font = HomeTypography.Body(11),
                Colour = GameplayModSettingsTheme.Accent,
            },
        ];

        SetState(false, 0);
    }

    internal void SetState(bool isEnabled, int allowedPauses)
    {
        enabled = isEnabled;
        AllowedPauses = Math.Clamp(allowedPauses, 0, 10);
        valueText.Text = AllowedPauses.ToString();
        decreaseButton.SetEnabled(isEnabled && AllowedPauses > 0);
        increaseButton.SetEnabled(isEnabled && AllowedPauses < 10);
        statusText.Text = !isEnabled
            ? YokkoStrings.Get("mods.settings.select_first", "NP")
            : AllowedPauses == 0
                ? YokkoStrings.Get("mods.settings.pause_disabled")
                : YokkoStrings.Get(
                    "mods.settings.pause_count_allowed",
                    AllowedPauses);
        this.ClearTransforms();
        this.FadeTo(isEnabled ? 1 : 0.42f, 120, Easing.OutQuint);
    }

    private void changeBy(int amount)
    {
        if (!enabled)
            return;

        int next = Math.Clamp(AllowedPauses + amount, 0, 10);
        if (next == AllowedPauses)
            return;

        SetState(true, next);
        allowedPausesChanged(next);
    }

    private partial class StepButton : ClickableContainer
    {
        private readonly Box background;
        private readonly SpriteText label;
        private bool enabled;

        internal StepButton(string text, Action action)
        {
            Action = () =>
            {
                if (enabled)
                    action();
            };
            Size = new Vector2(45, 48);
            Masking = true;
            CornerRadius = 5;
            BorderThickness = 1.5f;
            BorderColour = GameplayModSettingsTheme.Accent;
            InternalChildren =
            [
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = GameplayModSettingsTheme.Control,
                },
                label = new SpriteText
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Text = text,
                    Font = HomeTypography.Display(22),
                },
            ];
        }

        internal void SetEnabled(bool value)
        {
            enabled = value;
            background.Colour = value
                ? GameplayModSettingsTheme.Control
                : GameplayModSettingsTheme.Surface;
            label.Colour = value
                ? GameplayModSettingsTheme.Text
                : GameplayModSettingsTheme.Muted;
        }
    }
}
