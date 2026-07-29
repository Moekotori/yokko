using System;
using osu.Framework.Bindables;
using osuTK;

namespace Yokko.Game.Presentation;

public enum YokkoUiScale
{
    Large,
    Comfortable,
    Compact,
}

public enum YokkoFrameLimit
{
    RefreshRate,
    Limit2x,
    Limit4x,
    Limit8x,
    Unlimited,
}

public sealed class YokkoDisplaySettings
{
    public static readonly Vector2 DesignedDrawSize = new(1280, 720);

    private const float maximum_desktop_scale = 1.5f;

    public readonly Bindable<YokkoUiScale> UiScale = new(YokkoUiScale.Comfortable);

    public readonly Bindable<YokkoFrameLimit> FrameLimit =
        new(YokkoFrameLimit.Limit8x);
    public readonly BindableBool ShowPerformanceReadout = new(false);

    public Vector2 TargetDrawSize => GetTargetDrawSize(UiScale.Value);

    public static Vector2 GetTargetDrawSize(YokkoUiScale scale) => scale switch
    {
        // Yokko screens are authored on a 1280x720 stage. 100% is therefore
        // the largest safe setting; enlarging past it clips edge controls on
        // windows that are not wider than 16:9.
        YokkoUiScale.Large => new Vector2(1280, 720),
        YokkoUiScale.Compact => new Vector2(1600, 900),
        _ => new Vector2(1440, 810),
    };

    public static float GetScaleFactor(YokkoUiScale scale) =>
        GetScalePercentage(scale) / 100f;

    public static int GetScalePercentage(YokkoUiScale scale) => scale switch
    {
        YokkoUiScale.Large => 100,
        YokkoUiScale.Compact => 80,
        _ => 90,
    };

    /// <summary>
    /// Calculates the physical scale applied to Yokko's authored 1280x720 UI.
    /// The UI follows operating-system DPI, grows up to a 1080p-equivalent
    /// desktop size, and then stops growing with raw window resolution.
    /// Smaller windows always shrink to fit.
    /// </summary>
    public static float CalculateContentScale(
        Vector2 availableDrawSize,
        float displayScale,
        YokkoUiScale uiScale)
    {
        if (availableDrawSize.X <= 0 || availableDrawSize.Y <= 0)
            return 1;

        float fitScale = MathF.Min(
            availableDrawSize.X / DesignedDrawSize.X,
            availableDrawSize.Y / DesignedDrawSize.Y);
        float safeDisplayScale = float.IsFinite(displayScale)
            ? MathF.Max(displayScale, 0.5f)
            : 1;
        float preferredDesktopScale = MathF.Max(
            safeDisplayScale,
            MathF.Min(fitScale, maximum_desktop_scale));
        float baseScale = MathF.Min(fitScale, preferredDesktopScale);

        return MathF.Max(baseScale * GetScaleFactor(uiScale), 0.01f);
    }
}
