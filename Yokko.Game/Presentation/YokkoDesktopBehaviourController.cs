using System;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Configuration;
using osu.Framework.Platform;
using Yokko.Game.Audio;
using Yokko.Game.Configuration;
using Yokko.Game.Resources;

namespace Yokko.Game.Presentation;

internal sealed class YokkoDesktopBehaviourController : IDisposable
{
    private readonly GameHost host;
    private readonly FrameworkConfigManager frameworkConfig;
    private readonly YokkoDisplaySettings displaySettings;
    private readonly YokkoAudioSettings audioSettings;
    private readonly IBindable<bool> windowActive;
    private readonly IDesktopDisplayModeController displayModeController;
    private readonly YokkoConfigManager yokkoConfig;
    private readonly IWindow window;
    private readonly Bindable<WindowMode> windowMode;
    private readonly Bindable<Display> currentDisplay;

    internal YokkoDesktopBehaviourController(
        GameHost host,
        FrameworkConfigManager frameworkConfig,
        YokkoDisplaySettings displaySettings,
        YokkoAudioSettings audioSettings,
        IDesktopDisplayModeController displayModeController,
        YokkoConfigManager yokkoConfig)
    {
        this.host = host;
        this.frameworkConfig = frameworkConfig;
        this.displaySettings = displaySettings;
        this.audioSettings = audioSettings;
        this.displayModeController = displayModeController;
        this.yokkoConfig = yokkoConfig;
        window = host.Window;
        windowActive = window?.IsActive;
        windowMode = window?.WindowMode;
        currentDisplay = window?.CurrentDisplayBindable;

        displaySettings.FastAltTab.BindValueChanged(
            onFastAltTabChanged,
            true);
        displaySettings.BackgroundFrameRate.BindValueChanged(
            onBackgroundFrameRateChanged,
            true);
        displaySettings.FullscreenRefreshRate.BindValueChanged(
            onFullscreenRefreshRateChanged);
        windowMode?.BindValueChanged(onWindowModeChanged);
        currentDisplay?.BindValueChanged(onCurrentDisplayChanged);
        windowActive?.BindValueChanged(onWindowActiveChanged, true);
        if (window != null)
            window.WindowStateChanged += onWindowStateChanged;

        applyPreferredFullscreenMode();
    }

    internal static double GetMaximumInactiveHz(double rate) =>
        rate <= YokkoDisplaySettings.UnlimitedBackgroundFrameRate
            ? 0
            : Math.Clamp(
                rate,
                YokkoDisplaySettings.MinimumBackgroundFrameRate,
                YokkoDisplaySettings.MaximumBackgroundFrameRate);

    private void onFastAltTabChanged(ValueChangedEvent<bool> change) =>
        frameworkConfig.SetValue(
            FrameworkSetting.MinimiseOnFocusLossInFullscreen,
            !change.NewValue);

    private void onBackgroundFrameRateChanged(
        ValueChangedEvent<double> change) =>
        host.MaximumInactiveHz = GetMaximumInactiveHz(change.NewValue);

    private void onWindowActiveChanged(ValueChangedEvent<bool> change) =>
        audioSettings.SetApplicationActive(change.NewValue);

    private void onFullscreenRefreshRateChanged(ValueChangedEvent<int> _) =>
        applyPreferredFullscreenMode();

    private void onWindowModeChanged(ValueChangedEvent<WindowMode> change)
    {
        if (change.NewValue == WindowMode.Fullscreen)
            applyPreferredFullscreenMode();
    }

    private void onCurrentDisplayChanged(ValueChangedEvent<Display> _) =>
        applyPreferredFullscreenMode();

    private void onWindowStateChanged(WindowState state)
    {
        if (windowMode?.Value != WindowMode.Windowed)
            return;

        if (state == WindowState.Maximised)
            yokkoConfig.SetWindowMaximised(true);
        else if (state == WindowState.Normal)
        {
            yokkoConfig.SetWindowMaximised(false);
            displayModeController.EnsureWindowFrameVisible(window);
        }
    }

    internal void RestoreWindowState()
    {
        if (window != null
            && windowMode?.Value == WindowMode.Windowed
            && yokkoConfig.GetWindowMaximised())
        {
            window.WindowState = WindowState.Maximised;
        }
    }

    internal void EnsureWindowFrameVisible()
    {
        if (windowMode?.Value == WindowMode.Windowed
            && window?.WindowState == WindowState.Normal)
        {
            displayModeController.EnsureWindowFrameVisible(window);
        }
    }

    private void applyPreferredFullscreenMode()
    {
        if (window == null
            || windowMode?.Value != WindowMode.Fullscreen
            || displaySettings.FullscreenRefreshRate.Value <= 0)
        {
            return;
        }

        Display display = currentDisplay?.Value;
        int requested = displaySettings.FullscreenRefreshRate.Value;
        DisplayMode mode = display?.DisplayModes
            .Where(candidate => candidate.Size == display.Bounds.Size)
            .OrderBy(candidate =>
                Math.Abs(candidate.RefreshRate - requested))
            .ThenByDescending(candidate => candidate.BitsPerPixel)
            .FirstOrDefault() ?? default;

        if (mode.Size.Width > 0)
            displayModeController.TryApply(window, frameworkConfig, mode);
    }

    public void Dispose()
    {
        displaySettings.FastAltTab.ValueChanged -= onFastAltTabChanged;
        displaySettings.BackgroundFrameRate.ValueChanged -=
            onBackgroundFrameRateChanged;
        displaySettings.FullscreenRefreshRate.ValueChanged -=
            onFullscreenRefreshRateChanged;
        if (windowMode != null)
            windowMode.ValueChanged -= onWindowModeChanged;
        if (currentDisplay != null)
            currentDisplay.ValueChanged -= onCurrentDisplayChanged;
        if (windowActive != null)
            windowActive.ValueChanged -= onWindowActiveChanged;
        if (window != null)
            window.WindowStateChanged -= onWindowStateChanged;
    }
}
