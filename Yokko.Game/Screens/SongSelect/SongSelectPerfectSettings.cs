using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;
using Yokko.Game.Localisation;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Screens.SongSelect;

internal partial class SongSelectPerfectSettings
    : CompositeDrawable
{
    private readonly GameplayModSettingsStateButton strictModeButton;
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
                Text = YokkoStrings.Get("mods.definition.perfect.name"),
                Font = HomeTypography.Display(12),
                Spacing = new Vector2(0.35f, 0),
                Colour = GameplayModSettingsTheme.Text,
            },
            new SpriteText
            {
                Y = 28,
                Text = YokkoStrings.Get(
                    "mods.settings.default_perfect_rule"),
                Font = HomeTypography.Body(10),
                Colour = GameplayModSettingsTheme.Muted,
            },
            new SpriteText
            {
                Y = 48,
                Text = YokkoStrings.Get("mods.settings.great_keeps_run"),
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
                Text = YokkoStrings.Get("mods.settings.extra_rule"),
                Font = HomeTypography.Body(10),
                Colour = GameplayModSettingsTheme.Muted,
            },
            strictModeButton = new GameplayModSettingsStateButton(
                YokkoStrings.Get("mods.settings.require_perfect"),
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
            ? YokkoStrings.Get("mods.settings.select_first", "PF")
            : YokkoStrings.Get(
                requirePerfectHits
                    ? "mods.settings.only_perfect"
                    : "mods.settings.great_or_better");
        this.ClearTransforms();
        this.FadeTo(isEnabled ? 1 : 0.42f, 120, Easing.OutQuint);
    }

}
