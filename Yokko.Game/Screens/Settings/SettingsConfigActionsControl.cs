using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Framework.Platform;
using osuTK;
using Yokko.Game.Configuration;
using Yokko.Game.Localisation;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Screens.Settings;

internal partial class SettingsConfigActionsControl : CompositeDrawable
{
    private readonly YokkoConfigManager config;
    private readonly Clipboard clipboard;
    private readonly SpriteText statusText;

    public SettingsConfigActionsControl(
        YokkoConfigManager config,
        Clipboard clipboard)
    {
        this.config = config
            ?? throw new ArgumentNullException(nameof(config));
        this.clipboard = clipboard
            ?? throw new ArgumentNullException(nameof(clipboard));

        Size = new Vector2(SettingsChrome.ContentWidth, 96);

        InternalChildren = new Drawable[]
        {
            new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(12, 0),
                Children = new Drawable[]
                {
                    new SettingsOutlineButton(
                        YokkoStrings.Get("settings.general.config_export"),
                        FontAwesome.Solid.FileExport,
                        exportConfiguration),
                    new SettingsOutlineButton(
                        YokkoStrings.Get("settings.general.config_import"),
                        FontAwesome.Solid.FileImport,
                        importConfiguration),
                    new SettingsOutlineButton(
                        YokkoStrings.Get("settings.general.config_reset"),
                        FontAwesome.Solid.Undo,
                        resetConfiguration),
                },
            },
            statusText = new SpriteText
            {
                Position = new Vector2(0, 52),
                Width = SettingsChrome.ContentWidth,
                Font = HomeTypography.Body(16),
                Colour = SettingsTheme.MutedNavy,
            },
        };
    }

    internal LocalisableString StatusText => statusText.Text;

    private void exportConfiguration()
    {
        clipboard.SetText(YokkoConfigSnapshot.Export(config));
        setStatus(YokkoStrings.Get("settings.general.config_exported"));
    }

    private void importConfiguration()
    {
        bool imported = YokkoConfigSnapshot.TryImport(
            config,
            clipboard.GetText());
        setStatus(YokkoStrings.Get(
            imported
                ? "settings.general.config_imported"
                : "settings.general.config_import_failed"));
    }

    private void resetConfiguration()
    {
        YokkoConfigSnapshot.ResetAll(config);
        setStatus(YokkoStrings.Get("settings.general.config_reset_done"));
    }

    private void setStatus(LocalisableString message) => statusText.Text = message;
}
