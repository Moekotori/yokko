using osu.Framework.Bindables;

namespace Yokko.Game.Presentation;

public sealed class YokkoAccessibilitySettings
{
    public readonly BindableBool ReduceMotion = new();
    public readonly BindableBool ReduceFlashes = new();
    public readonly BindableBool HideComboBursts = new();
    public readonly BindableBool HighContrastText = new();
}
