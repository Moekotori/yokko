using System;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Platform;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;
using Yokko.Game.Diagnostics;
using Yokko.Game.Localisation;
using Yokko.Game.Presentation;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Screens.Settings;

internal partial class SafetySettingsPanel
    : CompositeDrawable, ISettingsSearchTarget
{
    private readonly GameHost host;
    private readonly YokkoDiagnostics diagnostics;
    private readonly SpriteText statusMetadata;
    private readonly Bindable<double> exitHoldDuration;
    private readonly SettingsContentScrollContainer contentScroll;

    internal string CrashReportDirectory { get; }

    public SafetySettingsPanel(
        GameHost host,
        YokkoDiagnostics diagnostics,
        string crashReportDirectory,
        Bindable<double> exitHoldDuration)
    {
        this.host = host ?? throw new ArgumentNullException(nameof(host));
        this.diagnostics = diagnostics
                           ?? throw new ArgumentNullException(nameof(diagnostics));
        this.exitHoldDuration = exitHoldDuration
            ?? throw new ArgumentNullException(nameof(exitHoldDuration));
        CrashReportDirectory = string.IsNullOrWhiteSpace(crashReportDirectory)
            ? throw new ArgumentException(
                "A crash report directory is required.",
                nameof(crashReportDirectory))
            : crashReportDirectory;
        RelativeSizeAxes = Axes.Both;

        var content = new Container
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            Children = new Drawable[]
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
                SettingsChrome.CreateDivider(390),
                SettingsChrome.CreateSettingRow(
                    402,
                    YokkoStrings.Get("settings.safety.export_diagnostics"),
                    SettingsChrome.CreateSegmentedControl(
                    [
                        new SettingsSegmentedChoiceButton(
                            YokkoStrings.Get("settings.safety.export_diagnostics"),
                            FontAwesome.Solid.FileArchive,
                            () => ExportDiagnosticsBundle(),
                            SettingsChrome.ControlWidth),
                    ])),
                SettingsChrome.CreateDivider(474),
                SettingsChrome.CreateSettingRow(
                    486,
                    YokkoStrings.Get("settings.safety.exit_hold_duration"),
                    new HomeExitHoldDurationSlider(exitHoldDuration)),
                new SpriteText
                {
                    Position = new Vector2(SettingsChrome.ContentX, 570),
                    Width = SettingsChrome.ContentWidth,
                    Text = YokkoStrings.Get("settings.safety.note"),
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

        statusMetadata.Text = YokkoStrings.Get(
            "settings.safety.crash_reports_ready");
    }

    public bool TryFocusSearchItem(string itemId) =>
        SettingsSearchScroll.TryFocus(
            SettingsPageKind.Safety,
            itemId,
            contentScroll);

    internal double ExitHoldDurationMilliseconds => exitHoldDuration.Value;

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

    internal bool ExportDiagnosticsBundle()
    {
        bool opened;
        try
        {
            string exportPath = diagnostics.ExportBundle();
            opened = host.OpenFileExternally(exportPath);
        }
        catch
        {
            opened = false;
        }

        statusMetadata.Text = YokkoStrings.Get(
            opened
                ? "settings.safety.exported"
                : "settings.safety.export_failed");
        return opened;
    }
}

internal partial class HomeExitHoldDurationSlider : CompositeDrawable
{
    internal const double MinimumMilliseconds = 500;
    internal const double MaximumMilliseconds = 3000;
    internal const double StepMilliseconds = 100;

    private const float track_x = 18;
    private const float track_y = 38;
    private const float track_width = SettingsChrome.ControlWidth - track_x * 2;

    private readonly Bindable<double> value;
    private readonly Box track;
    private readonly Box fill;
    private readonly Circle knob;
    private readonly SpriteText valueText;

    public override bool AcceptsFocus => true;

    internal HomeExitHoldDurationSlider(Bindable<double> value)
    {
        this.value = value;
        Size = new Vector2(SettingsChrome.ControlWidth, 54);

        InternalChildren = new Drawable[]
        {
            valueText = new SpriteText
            {
                Position = new Vector2(track_x, 5),
                Font = HomeTypography.Display(18),
                Colour = HomeControlColours.Navy,
            },
            track = new Box
            {
                Position = new Vector2(track_x, track_y),
                Size = new Vector2(track_width, 5),
                Colour = SettingsTheme.Divider,
            },
            fill = new Box
            {
                Position = new Vector2(track_x, track_y),
                Height = 5,
                Colour = HomeControlColours.Pink,
            },
            knob = new Circle
            {
                Origin = Anchor.Centre,
                Position = new Vector2(track_x, track_y + 2.5f),
                Size = new Vector2(15),
                Colour = Color4.White,
                BorderThickness = 2.5f,
                BorderColour = HomeControlColours.Pink,
            },
        };

        value.BindValueChanged(onValueChanged, true);
    }

    internal static double ValueFromProgress(double progress) =>
        snap(MinimumMilliseconds
             + Math.Clamp(progress, 0, 1)
             * (MaximumMilliseconds - MinimumMilliseconds));

    protected override bool OnMouseDown(MouseDownEvent e)
    {
        if (e.Button != MouseButton.Left)
            return false;

        updateFrom(ToLocalSpace(e.ScreenSpaceMousePosition).X);
        return true;
    }

    protected override bool OnDragStart(DragStartEvent e) => true;

    protected override void OnDrag(DragEvent e) =>
        updateFrom(ToLocalSpace(e.ScreenSpaceMousePosition).X);

    protected override bool OnScroll(ScrollEvent e)
    {
        if (e.ScrollDelta.Y == 0)
            return false;

        value.Value = snap(value.Value
                           + Math.Sign(e.ScrollDelta.Y) * StepMilliseconds);
        return true;
    }

    protected override bool OnKeyDown(KeyDownEvent e)
    {
        double next = e.Key switch
        {
            Key.Left or Key.Down => value.Value - StepMilliseconds,
            Key.Right or Key.Up => value.Value + StepMilliseconds,
            Key.Home => MinimumMilliseconds,
            Key.End => MaximumMilliseconds,
            _ => double.NaN,
        };

        if (double.IsNaN(next))
            return base.OnKeyDown(e);

        value.Value = snap(next);
        return true;
    }

    protected override bool OnHover(HoverEvent e)
    {
        track.FadeColour(SettingsTheme.PaleCyan, 100, Easing.OutQuint);
        knob.ScaleTo(1.18f, 100, Easing.OutQuint);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        track.FadeColour(SettingsTheme.Divider, 120, Easing.OutQuint);
        knob.ScaleTo(1, 120, Easing.OutQuint);
    }

    private void updateFrom(float localX) =>
        value.Value = ValueFromProgress((localX - track_x) / track_width);

    private void onValueChanged(ValueChangedEvent<double> change)
    {
        double snapped = snap(change.NewValue);
        if (snapped != change.NewValue)
        {
            value.Value = snapped;
            return;
        }

        float progress = (float)((snapped - MinimumMilliseconds)
                                 / (MaximumMilliseconds - MinimumMilliseconds));
        fill.Width = progress * track_width;
        knob.X = track_x + progress * track_width;
        valueText.Text = YokkoStrings.Get(
            "settings.safety.exit_hold_duration_value",
            snapped / 1000);
    }

    private static double snap(double milliseconds) =>
        Math.Clamp(
            Math.Round(milliseconds / StepMilliseconds) * StepMilliseconds,
            MinimumMilliseconds,
            MaximumMilliseconds);

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
            value.ValueChanged -= onValueChanged;

        base.Dispose(isDisposing);
    }
}
