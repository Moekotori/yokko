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
    public static readonly Vector2 ReferenceLayoutSize = new(1600, 900);

    public readonly Bindable<YokkoUiScale> UiScale = new(YokkoUiScale.Comfortable);

    public readonly Bindable<YokkoFrameLimit> FrameLimit =
        new(YokkoFrameLimit.Limit8x);
    public readonly BindableBool ShowPerformanceReadout = new(false);

    public Vector2 TargetDrawSize => GetTargetDrawSize(UiScale.Value);

    public static Vector2 GetTargetDrawSize(YokkoUiScale scale) => scale switch
    {
        YokkoUiScale.Large => new Vector2(1600, 900),
        YokkoUiScale.Compact => new Vector2(2000, 1125),
        _ => new Vector2(1600f / 0.9f, 1000),
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
    /// Calculates the physical scale applied to Yokko's shared 1600x900 UI.
    /// The 100% setting fits Yokko's shared 1600x900 layout space to the
    /// full client resolution. Rendering still occurs at the native client
    /// resolution; 90% and 80% expose proportionally more layout space.
    /// </summary>
    public static float CalculateContentScale(
        Vector2 availableDrawSize,
        YokkoUiScale uiScale)
    {
        if (availableDrawSize.X <= 0 || availableDrawSize.Y <= 0)
            return 1;

        float fitScale = MathF.Min(
            availableDrawSize.X / ReferenceLayoutSize.X,
            availableDrawSize.Y / ReferenceLayoutSize.Y);

        return MathF.Max(fitScale * GetScaleFactor(uiScale), 0.01f);
    }
}
