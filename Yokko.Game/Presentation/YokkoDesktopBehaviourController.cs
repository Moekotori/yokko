using System;
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

        displaySettings.FastAltTab.BindValueChanged(
            onFastAltTabChanged,
            true);
        displaySettings.DynamicBackgroundFrameRate.BindValueChanged(
            onDynamicBackgroundFrameRateChanged,
            true);
        displaySettings.BackgroundFrameRate.BindValueChanged(
            onBackgroundFrameRateChanged,
            true);
        windowActive?.BindValueChanged(onWindowActiveChanged, true);
        if (window != null)
            window.WindowStateChanged += onWindowStateChanged;
    }

    internal static double GetMaximumInactiveHz(
        bool dynamicBackgroundFrameRate,
        double rate) =>
        !dynamicBackgroundFrameRate
        || rate <= YokkoDisplaySettings.UnlimitedBackgroundFrameRate
            ? 0
            : Math.Clamp(
                rate,
                YokkoDisplaySettings.MinimumBackgroundFrameRate,
                YokkoDisplaySettings.MaximumBackgroundFrameRate);

    private void onFastAltTabChanged(ValueChangedEvent<bool> change) =>
        frameworkConfig.SetValue(
            FrameworkSetting.MinimiseOnFocusLossInFullscreen,
            !change.NewValue);

    private void onDynamicBackgroundFrameRateChanged(
        ValueChangedEvent<bool> _) =>
        applyBackgroundFrameRate();

    private void onBackgroundFrameRateChanged(ValueChangedEvent<double> _) =>
        applyBackgroundFrameRate();

    private void applyBackgroundFrameRate() =>
        host.MaximumInactiveHz = GetMaximumInactiveHz(
            displaySettings.DynamicBackgroundFrameRate.Value,
            displaySettings.BackgroundFrameRate.Value);

    private void onWindowActiveChanged(ValueChangedEvent<bool> change) =>
        audioSettings.SetApplicationActive(change.NewValue);

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

    public void Dispose()
    {
        displaySettings.FastAltTab.ValueChanged -= onFastAltTabChanged;
        displaySettings.DynamicBackgroundFrameRate.ValueChanged -=
            onDynamicBackgroundFrameRateChanged;
        displaySettings.BackgroundFrameRate.ValueChanged -=
            onBackgroundFrameRateChanged;
        if (windowActive != null)
            windowActive.ValueChanged -= onWindowActiveChanged;
        if (window != null)
            window.WindowStateChanged -= onWindowStateChanged;
    }
}
