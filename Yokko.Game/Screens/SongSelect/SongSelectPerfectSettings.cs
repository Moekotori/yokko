using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Screens.SongSelect;

internal partial class SongSelectPerfectSettings
    : CompositeDrawable
{
    private readonly StrictModeButton strictModeButton;
    private readonly SpriteText statusText;

    internal bool RequirePerfectHits { get; private set; }

    public SongSelectPerfectSettings(
        Action<bool> requirePerfectHitsChanged)
    {
        Size = new Vector2(202, 224);

        InternalChildren = new Drawable[]
        {
            new SpriteText
            {
                Text = "PERFECT",
                Font = HomeTypography.Display(12),
                Spacing = new Vector2(0.35f, 0),
                Colour = GameplayModSettingsTheme.Text,
            },
            new SpriteText
            {
                Y = 28,
                Text = "DEFAULT LAZER MANIA",
                Font = HomeTypography.Body(10),
                Colour = GameplayModSettingsTheme.Muted,
            },
            new SpriteText
            {
                Y = 48,
                Text = "Great or better keeps the run alive.",
                Font = HomeTypography.Body(10),
                Colour = GameplayModSettingsTheme.Text,
            },
            new Box
            {
                Y = 78,
                Size = new Vector2(202, 1),
                Colour = new Color4(
                    GameplayModSettingsTheme.Accent.R,
                    GameplayModSettingsTheme.Accent.G,
                    GameplayModSettingsTheme.Accent.B,
                    0.25f),
            },
            new SpriteText
            {
                Y = 94,
                Text = "OPTION",
                Font = HomeTypography.Body(10),
                Colour = GameplayModSettingsTheme.Muted,
            },
            strictModeButton = new StrictModeButton(
                "REQUIRE PERFECT HITS",
                () => requirePerfectHitsChanged(
                    !RequirePerfectHits))
            {
                Y = 118,
            },
            statusText = new SpriteText
            {
                Y = 180,
                Font = HomeTypography.Body(10),
                Colour = GameplayModSettingsTheme.Accent,
            },
        };

        SetState(false, false);
    }

    public void SetState(
        bool isEnabled,
        bool requirePerfectHits)
    {
        RequirePerfectHits = requirePerfectHits;
        strictModeButton.SetState(
            isEnabled,
            requirePerfectHits);
        statusText.Text = !isEnabled
            ? "SELECT PF TO CONFIGURE"
            : requirePerfectHits
                ? "ONLY PERFECT HITS PASS"
                : "GREAT OR BETTER PASSES";
        this.ClearTransforms();
        this.FadeTo(isEnabled ? 1 : 0.42f, 120, Easing.OutQuint);
    }

    private partial class StrictModeButton
        : ClickableContainer
    {
        private readonly Box background;
        private readonly SpriteText label;
        private bool enabled;

        public StrictModeButton(
            string text,
            Action action)
        {
            Action = () =>
            {
                if (enabled)
                    action();
            };
            Size = new Vector2(202, 42);
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

        public void SetState(
            bool isEnabled,
            bool selected)
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
