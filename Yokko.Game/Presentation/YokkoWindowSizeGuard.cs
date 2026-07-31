using System;
using System.Drawing;
using osu.Framework.Bindables;
using osu.Framework.Platform;

namespace Yokko.Game.Presentation;

/// <summary>
/// Keeps the persisted windowed size inside the logical bounds of the display.
/// Display modes use physical pixels while window sizes use scaled pixels, so
/// the window DPI scale must be applied before comparing them.
/// </summary>
internal sealed class YokkoWindowSizeGuard : IDisposable
{
    private const int minimum_width = 640;
    private const int minimum_height = 360;
    private const int horizontal_window_chrome_allowance = 32;
    private const int vertical_window_chrome_allowance = 80;

    // Physical window choices only; these are not logical layout references.
    private static readonly Size[] safeFallbackSizes =
    {
        new(1920, 1080),
        new(1600, 900),
        new(1280, 720),
        new(1024, 576),
        new(960, 540),
        new(800, 450),
        new(minimum_width, minimum_height),
    };

    private readonly Bindable<Size> windowedSize;
    private readonly IBindable<DisplayMode> currentDisplayMode;
    private readonly Func<float> getWindowScale;
    private readonly Action<Size, Size> onRepaired;
    private bool isRepairing;

    public YokkoWindowSizeGuard(
        Bindable<Size> windowedSize,
        IBindable<DisplayMode> currentDisplayMode,
        Func<float> getWindowScale,
        Action<Size, Size> onRepaired = null)
    {
        this.windowedSize = windowedSize;
        this.currentDisplayMode = currentDisplayMode;
        this.getWindowScale = getWindowScale;
        this.onRepaired = onRepaired;

        windowedSize.BindValueChanged(onWindowedSizeChanged);
        currentDisplayMode.BindValueChanged(onDisplayModeChanged);
        Repair();
    }

    internal void Repair()
    {
        if (isRepairing)
            return;

        Size requested = windowedSize.Value;
        Size corrected = CalculateSafeWindowedSize(
            requested,
            currentDisplayMode.Value.Size,
            getWindowScale());

        if (corrected == requested)
            return;

        isRepairing = true;

        try
        {
            windowedSize.Value = corrected;
            onRepaired?.Invoke(requested, corrected);
        }
        finally
        {
            isRepairing = false;
        }
    }

    internal static Size CalculateSafeWindowedSize(
        Size requested,
        Size physicalDisplaySize,
        float windowScale)
    {
        if (physicalDisplaySize.Width <= 0
            || physicalDisplaySize.Height <= 0)
        {
            return hasUsableDimensions(requested)
                ? requested
                : new Size(1280, 720);
        }

        float scale = sanitiseScale(windowScale);
        int maximumWidth = Math.Max(
            minimum_width,
            (int)MathF.Floor(physicalDisplaySize.Width / scale)
            - horizontal_window_chrome_allowance);
        int maximumHeight = Math.Max(
            minimum_height,
            (int)MathF.Floor(physicalDisplaySize.Height / scale)
            - vertical_window_chrome_allowance);

        if (hasUsableDimensions(requested)
            && requested.Width <= maximumWidth
            && requested.Height <= maximumHeight)
        {
            return requested;
        }

        float requestedAspect = requested.Height > 0
            ? requested.Width / (float)requested.Height
            : 0;

        // Preserve a normal landscape resize when moving to a smaller or
        // higher-DPI display. Clearly corrupted or portrait values use a
        // predictable fallback instead of producing another unusable window.
        if (hasUsableDimensions(requested)
            && requestedAspect >= 4f / 3f
            && requestedAspect <= 21f / 9f)
        {
            float fit = MathF.Min(
                maximumWidth / (float)requested.Width,
                maximumHeight / (float)requested.Height);
            var fitted = new Size(
                (int)MathF.Floor(requested.Width * fit),
                (int)MathF.Floor(requested.Height * fit));

            if (hasUsableDimensions(fitted))
                return fitted;
        }

        foreach (Size fallback in safeFallbackSizes)
        {
            if (fallback.Width <= maximumWidth
                && fallback.Height <= maximumHeight)
            {
                return fallback;
            }
        }

        return new Size(maximumWidth, maximumHeight);
    }

    private static bool hasUsableDimensions(Size size) =>
        size.Width >= minimum_width && size.Height >= minimum_height;

    private static float sanitiseScale(float scale) =>
        float.IsFinite(scale) && scale > 0 ? scale : 1;

    private void onWindowedSizeChanged(ValueChangedEvent<Size> _) => Repair();

    private void onDisplayModeChanged(ValueChangedEvent<DisplayMode> _) => Repair();

    public void Dispose()
    {
        windowedSize.ValueChanged -= onWindowedSizeChanged;
        currentDisplayMode.ValueChanged -= onDisplayModeChanged;
    }
}
