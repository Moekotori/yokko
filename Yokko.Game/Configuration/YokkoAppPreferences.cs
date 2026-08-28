using osu.Framework.Bindables;
using osuTK.Input;
using Yokko.Core.Gameplay;

namespace Yokko.Game.Configuration;

public sealed class YokkoAccessibilitySettings
{
    public readonly BindableBool ReduceMotion = new(false);
    public readonly BindableBool HighContrast = new(false);

    /// <summary>
    /// Additional text scale percentage applied on top of display UI scale (90–130).
    /// </summary>
    public readonly Bindable<int> TextScalePercent = new(100);
}

public sealed class YokkoEditorSettings
{
    public readonly Bindable<KeyMode> DefaultKeyMode = new(KeyMode.FourKey);
    public readonly Bindable<int> VisibleRows = new(24);
    public readonly Bindable<int> SnapDivisor = new(4);
    public readonly BindableBool AutosaveEnabled = new(true);
    public readonly Bindable<int> AutosaveIntervalSeconds = new(60);
}

public sealed class YokkoPrivacySettings
{
    public readonly BindableBool SaveLocalReplays = new(true);
    public readonly BindableBool IncludeUsernameInExports = new(true);
}

public sealed class YokkoStartupSettings
{
    public readonly BindableBool OpenLastScreen = new(false);
    public readonly BindableBool RememberActiveMods = new(true);
}
