using System;
using osu.Framework.Bindables;
using osuTK;
using Yokko.Core.Difficulty;

namespace Yokko.Game.Presentation;

public enum YokkoUiScale
{
    Large,
    Comfortable,
    Compact,
}

public enum YokkoFrameLimit
{
    VSync,

    // Keep the old persisted enum name readable. Existing installations which
    // selected "RefreshRate" migrate to the equivalent tear-free mode.
    [Obsolete("Use VSync.")]
    RefreshRate = VSync,

    Limit2x,
    Limit4x,
    Limit8x,
    Unlimited,
    Auto,
}

public enum YokkoBackgroundFrameRate
{
    Fps30 = 30,
    Fps60 = 60,
    Unlimited = 0,
}

public sealed class YokkoDisplaySettings
{
    public static readonly Vector2 ReferenceLayoutSize = new(1920, 1080);

    public readonly Bindable<YokkoUiScale> UiScale = new(YokkoUiScale.Comfortable);

    public readonly Bindable<YokkoFrameLimit> FrameLimit =
        new(YokkoFrameRateLimits.LowLatencyDefault);
    public readonly BindableBool ShowPerformanceReadout = new(false);
    public readonly BindableBool FastAltTab = new(true);
    public readonly BindableBool DynamicBackgroundFrameRate = new(true);
    public const double MinimumBackgroundFrameRate = 15;
    public const double MaximumBackgroundFrameRate = 240;
    public const double BackgroundFrameRateStep = 5;
    public const double UnlimitedBackgroundFrameRate = 0;

    public readonly Bindable<double> BackgroundFrameRate = new(30);
    public readonly Bindable<ManiaDifficultyRatingMode>
        DifficultyRatingMode = new(
            ManiaDifficultyRatingMode.EtternaMsd);

    public Vector2 TargetDrawSize => GetTargetDrawSize(UiScale.Value);

    public static Vector2 GetTargetDrawSize(YokkoUiScale scale) => scale switch
    {
        YokkoUiScale.Large => ReferenceLayoutSize / 1.1f,
        YokkoUiScale.Compact => ReferenceLayoutSize / 0.9f,
        _ => ReferenceLayoutSize,
    };

    public static float GetScaleFactor(YokkoUiScale scale) =>
        GetScalePercentage(scale) / 100f;

    public static int GetScalePercentage(YokkoUiScale scale) => scale switch
    {
        YokkoUiScale.Large => 110,
        YokkoUiScale.Compact => 90,
        _ => 100,
    };

    /// <summary>
    /// Calculates the physical scale applied to Yokko's shared 1920x1080 UI.
    /// The default 100% setting fits Yokko's shared 1920x1080 layout space to
    /// the full client resolution. Rendering still occurs at the native client
    /// resolution; 110% increases content density and 90% exposes more space.
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
