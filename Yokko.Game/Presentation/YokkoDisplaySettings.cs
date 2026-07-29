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

    public static int GetScalePercentage(YokkoUiScale scale) => scale switch
    {
        YokkoUiScale.Large => 100,
        YokkoUiScale.Compact => 80,
        _ => 90,
    };
}
