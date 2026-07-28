using osu.Framework.Bindables;
using osuTK;

namespace Yokko.Game.Presentation;

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
    public static readonly Vector2 TargetDrawSize = new(1280, 720);

    public readonly Bindable<YokkoFrameLimit> FrameLimit = new(YokkoFrameLimit.RefreshRate);
    public readonly BindableBool ShowPerformanceReadout = new(false);
}
