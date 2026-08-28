using System;
using System.Collections.Generic;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osuTK;
using Yokko.Game.Configuration;
using Yokko.Game.Gameplay;
using Yokko.Game.Localisation;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Screens.Settings;

internal partial class ModPreferencesSettingsPanel
    : CompositeDrawable, ISettingsSearchTarget
{
    private static readonly IReadOnlyDictionary<string, float> search_scroll_targets =
        new Dictionary<string, float>
        {
            ["remember-mods"] = 236,
        };

    private readonly YokkoStartupSettings startupSettings;
    private readonly YokkoManiaModPreferences modPreferences;
    private readonly SettingsContentScrollContainer contentScroll;
    private readonly SpriteText statusMetadata;

    public ModPreferencesSettingsPanel(
        YokkoStartupSettings startupSettings,
        YokkoManiaModPreferences modPreferences)
    {
        this.startupSettings = startupSettings
            ?? throw new ArgumentNullException(nameof(startupSettings));
        this.modPreferences = modPreferences
            ?? throw new ArgumentNullException(nameof(modPreferences));
        RelativeSizeAxes = Axes.Both;

        var content = new Container
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            Children = new Drawable[]
            {
                SettingsChrome.CreateHeader(
                    YokkoStrings.Get("settings.mods.title"),
                    YokkoStrings.Get("settings.mods.subtitle"),
                    FontAwesome.Solid.LayerGroup,
                    (int)SettingsPageKind.Mods + 1),
                SettingsChrome.CreateStatusCard(
                    174,
                    FontAwesome.Solid.LayerGroup,
                    YokkoStrings.Get("settings.mods.session_memory"),
                    FontAwesome.Solid.Memory,
                    out statusMetadata),
                SettingsChrome.CreateDivider(228),
                SettingsChrome.CreateSettingRow(
                    236,
                    YokkoStrings.Get("settings.mods.remember_active_mods"),
                    new SettingsBooleanToggle(
                        startupSettings.RememberActiveMods,
                        "settings.mods.remember_active_mods_on",
                        "settings.mods.remember_active_mods_off")),
                SettingsChrome.CreateDivider(298),
                SettingsChrome.CreateSettingRow(
                    298,
                    YokkoStrings.Get("settings.mods.saved_configuration"),
                    SettingsChrome.CreateSegmentedControl(
                    [
                        new SettingsSegmentedChoiceButton(
                            YokkoStrings.Get("settings.mods.clear_configuration"),
                            FontAwesome.Solid.TrashAlt,
                            clearModConfiguration,
                            SettingsChrome.ControlWidth),
                    ])),
                new SpriteText
                {
                    Position = new Vector2(SettingsChrome.ContentX, 374),
                    Width = SettingsChrome.ContentWidth,
                    Text = YokkoStrings.Get("settings.mods.detail_note"),
                    Font = HomeTypography.Body(17),
                    Colour = SettingsTheme.MutedNavy,
                },
            },
        };

        InternalChild = contentScroll = new SettingsContentScrollContainer
        {
            RelativeSizeAxes = Axes.Both,
            Child = content,
        };

        startupSettings.RememberActiveMods.BindValueChanged(
            onRememberActiveModsChanged,
            true);
        modPreferences.SerializedConfiguration.BindValueChanged(
            onSerializedConfigurationChanged,
            true);
    }

    public bool TryFocusSearchItem(string itemId)
    {
        if (!search_scroll_targets.TryGetValue(itemId, out float y))
            return false;

        contentScroll.ScrollTo(y, true);
        return true;
    }

    internal void ClearModConfiguration() => clearModConfiguration();

    private void clearModConfiguration()
    {
        modPreferences.SerializedConfiguration.Value = string.Empty;
        statusMetadata.Text = YokkoStrings.Get("settings.mods.configuration_cleared");
    }

    private void refreshStatus()
    {
        bool hasConfiguration = !string.IsNullOrWhiteSpace(
            modPreferences.SerializedConfiguration.Value);
        statusMetadata.Text = YokkoStrings.Get(
            hasConfiguration
                ? "settings.mods.configuration_saved"
                : "settings.mods.configuration_empty",
            startupSettings.RememberActiveMods.Value
                ? YokkoStrings.Get("settings.mods.remember_active_mods_on")
                : YokkoStrings.Get("settings.mods.remember_active_mods_off"));
    }

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
        {
            startupSettings.RememberActiveMods.ValueChanged
                -= onRememberActiveModsChanged;
            modPreferences.SerializedConfiguration.ValueChanged
                -= onSerializedConfigurationChanged;
        }

        base.Dispose(isDisposing);
    }

    private void onRememberActiveModsChanged(ValueChangedEvent<bool> _) =>
        refreshStatus();

    private void onSerializedConfigurationChanged(ValueChangedEvent<string> _) =>
        refreshStatus();
}
