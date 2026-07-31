using System;
using System.Reflection;
using osu.Framework.Bindables;
using osu.Framework.Configuration;
using osu.Framework.Logging;
using osu.Framework.Platform;
using Yokko.Game.Resources;

namespace Yokko.Desktop.Platform;

internal sealed class FrameworkDesktopDisplayModeController
    : IDesktopDisplayModeController
{
    public bool IsAvailable => OperatingSystem.IsWindows();

    public bool TryApply(
        IWindow window,
        FrameworkConfigManager frameworkConfig,
        DisplayMode mode)
    {
        if (!IsAvailable || window == null)
            return false;

        try
        {
            FieldInfo field = findField(
                window.GetType(),
                "currentDisplayMode");
            if (field?.GetValue(window) is not Bindable<DisplayMode> current)
                return false;

            // osu!framework persists fullscreen size but not the requested Hz.
            // Supply the selected mode before its normal size invalidation.
            frameworkConfig.SetValue(
                FrameworkSetting.SizeFullscreen,
                new System.Drawing.Size(9998, 9998));
            current.Value = mode;
            frameworkConfig.SetValue(
                FrameworkSetting.SizeFullscreen,
                mode.Size);
            return true;
        }
        catch (Exception exception)
        {
            Logger.Error(
                exception,
                "Could not apply the selected exclusive fullscreen mode.",
                LoggingTarget.Runtime);
            return false;
        }
    }

    private static FieldInfo findField(Type type, string name)
    {
        while (type != null)
        {
            FieldInfo field = type.GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (field != null)
                return field;
            type = type.BaseType;
        }

        return null;
    }
}
