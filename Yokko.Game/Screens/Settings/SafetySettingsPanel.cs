using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Platform;
using osuTK;
using Yokko.Game.Localisation;
using Yokko.Game.Presentation;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Screens.Settings;

internal partial class SafetySettingsPanel : CompositeDrawable
{
    private readonly GameHost host;
    private readonly SpriteText statusMetadata;

    internal string CrashReportDirectory { get; }

    public SafetySettingsPanel(GameHost host, string crashReportDirectory)
    {
        this.host = host ?? throw new ArgumentNullException(nameof(host));
        CrashReportDirectory = string.IsNullOrWhiteSpace(crashReportDirectory)
            ? throw new ArgumentException(
                "A crash report directory is required.",
                nameof(crashReportDirectory))
            : crashReportDirectory;
        RelativeSizeAxes = Axes.Both;

        InternalChildren = new Drawable[]
        {
            SettingsChrome.CreateHeader(
                YokkoStrings.Get("settings.safety.title"),
                YokkoStrings.Get("settings.safety.subtitle"),
                FontAwesome.Solid.ShieldAlt,
                11),
            SettingsChrome.CreateStatusCard(
                174,
                FontAwesome.Solid.ExclamationTriangle,
                YokkoStrings.Get("settings.safety.crash_reports"),
                FontAwesome.Solid.FolderOpen,
                out statusMetadata),
            SettingsChrome.CreateDivider(292),
            SettingsChrome.CreateSettingRow(
                318,
                YokkoStrings.Get("settings.safety.crash_reports"),
                SettingsChrome.CreateSegmentedControl(
                [
                    new SettingsSegmentedChoiceButton(
                        YokkoStrings.Get("settings.safety.open_crash_reports"),
                        FontAwesome.Solid.FolderOpen,
                        () => OpenCrashReportDirectory(),
                        SettingsChrome.ControlWidth),
                ])),
            new SpriteText
            {
                Position = new Vector2(SettingsChrome.ContentX, 404),
                Width = SettingsChrome.ContentWidth,
                Text = YokkoStrings.Get("settings.safety.note"),
                Font = HomeTypography.Body(17),
                Colour = SettingsTheme.MutedNavy,
            },
            new SettingsPanelFooter(YokkoStrings.Get("settings.safety.footer")),
        };

        statusMetadata.Text = YokkoStrings.Get(
            "settings.safety.crash_reports_ready");
    }

    internal bool OpenCrashReportDirectory()
    {
        bool opened;
        try
        {
            opened = host.OpenFileExternally(CrashReportDirectory);
        }
        catch
        {
            opened = false;
        }

        statusMetadata.Text = YokkoStrings.Get(
            opened
                ? "settings.safety.opened"
                : "settings.safety.open_failed");
        return opened;
    }
}
