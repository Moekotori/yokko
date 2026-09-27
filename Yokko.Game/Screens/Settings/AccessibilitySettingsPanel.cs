using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;
using Yokko.Game.Localisation;
using Yokko.Game.Presentation;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Screens.Settings;

internal partial class AccessibilitySettingsPanel : CompositeDrawable
{
    internal AccessibilitySettingsPanel(YokkoAccessibilitySettings settings)
    {
        RelativeSizeAxes = Axes.Both;
        InternalChildren = new Drawable[]
        {
            SettingsChrome.CreateHeader(YokkoStrings.Get("settings.accessibility.title"),
                YokkoStrings.Get("accessibility.subtitle"), FontAwesome.Solid.UniversalAccess, 10),
            SettingsChrome.CreateSettingRow(190, YokkoStrings.Get("accessibility.motion"), new SettingsBooleanToggle(settings.ReduceMotion)),
            note("accessibility.motion_note", 258),
            SettingsChrome.CreateDivider(302),
            SettingsChrome.CreateSettingRow(320, YokkoStrings.Get("accessibility.flashes"), new SettingsBooleanToggle(settings.ReduceFlashes)),
            note("accessibility.flashes_note", 388),
            SettingsChrome.CreateDivider(432),
            SettingsChrome.CreateSettingRow(448, YokkoStrings.Get("accessibility.bursts"), new SettingsBooleanToggle(settings.HideComboBursts)),
            note("accessibility.bursts_note", 516),
            SettingsChrome.CreateDivider(556),
            SettingsChrome.CreateSettingRow(574, YokkoStrings.Get("accessibility.contrast"), new SettingsBooleanToggle(settings.HighContrastText)),
            note("accessibility.contrast_note", 642),
        };
    }
    private static SpriteText note(string key, float y) => new SettingsReadableText
    {
        Position = new Vector2(378, y), Text = YokkoStrings.Get(key),
        Font = HomeTypography.Body(15), Colour = SettingsTheme.MutedNavy,
    };
}

/// <summary>Raises muted settings text contrast without recolouring accents or dark buttons.</summary>
internal partial class SettingsReadableText : SpriteText
{
    [Resolved(CanBeNull = true)] private YokkoAccessibilitySettings accessibility { get; set; }
    private Color4? original;
    protected override void Update()
    {
        base.Update();
        bool enabled = accessibility?.HighContrastText.Value == true;
        if (enabled && original == null && Colour == SettingsTheme.MutedNavy)
        {
            original = SettingsTheme.MutedNavy;
            Colour = HomeControlColours.Navy;
        }
        else if (!enabled && original.HasValue)
        {
            if (Colour == HomeControlColours.Navy) Colour = original.Value;
            original = null;
        }
    }
}
