using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Screens.SongSelect;

internal enum GameplayModSettingsControlStyle
{
    Rate,
    Muted,
}

/// <summary>
/// Shared presentation for numeric steppers inside Song Select's light Mod
/// settings workspace. Range and step rules remain with the owning page.
/// </summary>
internal partial class GameplayModSettingsStepButton : ClickableContainer
{
    public override bool AcceptsFocus => Enabled.Value;

    internal GameplayModSettingsStepButton(
        string text,
        Action action,
        GameplayModSettingsControlStyle style =
            GameplayModSettingsControlStyle.Rate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentNullException.ThrowIfNull(action);

        bool muted = style == GameplayModSettingsControlStyle.Muted;
        Action = action;
        Size = muted
            ? new Vector2(97, 30)
            : new Vector2(46, 29);
        Masking = true;
        CornerRadius = muted ? 5 : 4;
        BorderThickness = 1;
        BorderColour = GameplayModSettingsTheme.Accent;
        InternalChildren =
        [
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = withAlpha(
                    GameplayModSettingsTheme.Control,
                    muted ? 0.78f : 0.8f),
            },
            new SpriteText
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Text = text,
                Font = HomeTypography.Display(muted ? 10 : 14),
                Colour = GameplayModSettingsTheme.Text,
            },
        ];
    }

    internal void SetEnabled(bool enabled) => Enabled.Value = enabled;

    internal void ActivateForTest() => TriggerClick();

    protected override bool OnClick(ClickEvent e)
    {
        if (!Enabled.Value)
            return true;

        return base.OnClick(e);
    }

    protected override bool OnKeyDown(KeyDownEvent e)
    {
        if (Enabled.Value && (e.Key is Key.Enter or Key.Space))
        {
            TriggerClick();
            return true;
        }

        return base.OnKeyDown(e);
    }

    private static Color4 withAlpha(Color4 colour, float alpha) =>
        new(colour.R, colour.G, colour.B, alpha);
}

/// <summary>
/// Shared ON/OFF row for configurable Mods. Disabled state is an interaction
/// boundary, not just a visual fade, so previewing an inactive Mod cannot
/// silently enable it through one of its sub-settings.
/// </summary>
internal partial class GameplayModSettingsStateButton : ClickableContainer
{
    private readonly Box background;
    private readonly SpriteText stateText;
    private readonly GameplayModSettingsControlStyle style;

    public override bool AcceptsFocus => Enabled.Value;

    internal bool InteractionEnabled => Enabled.Value;

    internal GameplayModSettingsStateButton(
        string text,
        Action action,
        GameplayModSettingsControlStyle style =
            GameplayModSettingsControlStyle.Rate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentNullException.ThrowIfNull(action);

        this.style = style;
        Action = action;
        Size = new Vector2(
            202,
            style == GameplayModSettingsControlStyle.Muted ? 27 : 29);
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
        Enabled.Value = enabled;
        Color4 colour = selected
            ? GameplayModSettingsTheme.Selection
            : GameplayModSettingsTheme.Control;
        background.Colour = style == GameplayModSettingsControlStyle.Muted
            ? withAlpha(colour, selected ? 0.72f : 0.8f)
            : colour;
        stateText.Text = selected ? "ON" : "OFF";
        stateText.Colour = selected
            ? GameplayModSettingsTheme.AccentOn
            : GameplayModSettingsTheme.Muted;
        Alpha = enabled ? 1 : 0.55f;
    }

    internal void ActivateForTest() => TriggerClick();

    protected override bool OnClick(ClickEvent e)
    {
        if (!Enabled.Value)
            return true;

        return base.OnClick(e);
    }

    protected override bool OnKeyDown(KeyDownEvent e)
    {
        if (Enabled.Value && (e.Key is Key.Enter or Key.Space))
        {
            TriggerClick();
            return true;
        }

        return base.OnKeyDown(e);
    }

    private static Color4 withAlpha(Color4 colour, float alpha) =>
        new(colour.R, colour.G, colour.B, alpha);
}
