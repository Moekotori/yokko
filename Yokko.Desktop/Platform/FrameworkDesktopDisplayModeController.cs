using System;
using System.Reflection;
using System.Runtime.InteropServices;
using osu.Framework.Logging;
using osu.Framework.Platform;
using Yokko.Game.Resources;

namespace Yokko.Desktop.Platform;

internal sealed class FrameworkDesktopDisplayModeController
    : IDesktopDisplayModeController
{
    private const uint monitor_default_to_nearest = 2;
    private const uint swp_no_size = 0x0001;
    private const uint swp_no_z_order = 0x0004;
    private const uint swp_no_activate = 0x0010;

    public bool IsAvailable => OperatingSystem.IsWindows();

    public void EnsureWindowFrameVisible(IWindow window)
    {
        if (!IsAvailable || window == null)
            return;

        try
        {
            PropertyInfo handleProperty = window.GetType().GetProperty(
                "WindowHandle",
                BindingFlags.Instance | BindingFlags.Public);
            if (handleProperty?.GetValue(window) is not IntPtr handle
                || handle == IntPtr.Zero
                || !GetWindowRect(handle, out NativeRectangle windowBounds))
            {
                return;
            }

            IntPtr monitor = MonitorFromWindow(
                handle,
                monitor_default_to_nearest);
            var monitorInfo = new MonitorInfo
            {
                Size = Marshal.SizeOf<MonitorInfo>(),
            };

            if (monitor == IntPtr.Zero
                || !GetMonitorInfo(monitor, ref monitorInfo))
            {
                return;
            }

            int width = windowBounds.Right - windowBounds.Left;
            int height = windowBounds.Bottom - windowBounds.Top;
            int correctedX = clampWindowOrigin(
                windowBounds.Left,
                width,
                monitorInfo.WorkArea.Left,
                monitorInfo.WorkArea.Right);
            int correctedY = clampWindowOrigin(
                windowBounds.Top,
                height,
                monitorInfo.WorkArea.Top,
                monitorInfo.WorkArea.Bottom);

            if (correctedX == windowBounds.Left
                && correctedY == windowBounds.Top)
            {
                return;
            }

            if (!SetWindowPos(
                    handle,
                    IntPtr.Zero,
                    correctedX,
                    correctedY,
                    0,
                    0,
                    swp_no_size | swp_no_z_order | swp_no_activate))
            {
                Logger.Log(
                    "Could not move the window frame back inside the display work area.",
                    LoggingTarget.Runtime,
                    LogLevel.Important);
            }
        }
        catch (Exception exception)
        {
            Logger.Error(
                exception,
                "Could not validate the window frame position.",
                LoggingTarget.Runtime);
        }
    }

    private static int clampWindowOrigin(
        int position,
        int windowLength,
        int workAreaStart,
        int workAreaEnd)
    {
        int availableLength = workAreaEnd - workAreaStart;
        if (windowLength >= availableLength)
            return workAreaStart;

        return Math.Clamp(
            position,
            workAreaStart,
            workAreaEnd - windowLength);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRectangle
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRectangle MonitorArea;
        public NativeRectangle WorkArea;
        public uint Flags;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(
        IntPtr window,
        out NativeRectangle rectangle);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(
        IntPtr window,
        uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(
        IntPtr monitor,
        ref MonitorInfo monitorInfo);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        IntPtr window,
        IntPtr insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);
}
