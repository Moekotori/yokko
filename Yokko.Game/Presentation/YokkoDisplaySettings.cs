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

    public readonly Bindable<YokkoFrameLimit> FrameLimit = new(YokkoFrameLimit.RefreshRate);
    public readonly BindableBool ShowPerformanceReadout = new(false);

    public Vector2 TargetDrawSize => GetTargetDrawSize(UiScale.Value);

    public static Vector2 GetTargetDrawSize(YokkoUiScale scale) => scale switch
    {
        // Yokko screens are authored on a 1280x720 stage. Keep enough of that
        // stage visible that edge controls remain inside the viewport.
        YokkoUiScale.Large => new Vector2(1216, 684),
        YokkoUiScale.Compact => new Vector2(1440, 810),
        _ => new Vector2(1280, 720),
    };
}
