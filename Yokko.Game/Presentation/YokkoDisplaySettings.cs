using osu.Framework.Bindables;
using osuTK;

namespace Yokko.Game.Presentation;

public enum YokkoUiScale
{
    Large,
    Comfortable,
    Compact,
}

public sealed class YokkoDisplaySettings
{
    public readonly Bindable<YokkoUiScale> UiScale = new(YokkoUiScale.Comfortable);

    public Vector2 TargetDrawSize => UiScale.Value switch
    {
        YokkoUiScale.Large => new Vector2(1152, 648),
        YokkoUiScale.Compact => new Vector2(1440, 810),
        _ => new Vector2(1280, 720),
    };
}
