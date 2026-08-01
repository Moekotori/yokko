using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Shapes;

namespace Yokko.Game.Presentation;

public enum YokkoThemeBoxRole
{
    Background,
    Surface,
    SurfaceElevated,
    Panel,
    PanelAlt,
}

/// <summary>
/// Theme-aware solid fill for page backgrounds and simple surfaces.
/// </summary>
public partial class YokkoThemeBox : Box
{
    private readonly YokkoThemeBoxRole role;
    private IBindable<YokkoUiTheme> currentTheme;

    public YokkoThemeBox(YokkoThemeBoxRole role)
    {
        this.role = role;
        applyTheme(YokkoUiTheme.Default);
    }

    [BackgroundDependencyLoader]
    private void load(YokkoUiThemeStore themeStore)
    {
        currentTheme = themeStore.Current.GetBoundCopy();
        currentTheme.BindValueChanged(
            change => applyTheme(change.NewValue),
            true);
    }

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
            currentTheme?.UnbindAll();

        base.Dispose(isDisposing);
    }

    private void applyTheme(YokkoUiTheme theme)
    {
        YokkoDarkColourTokens colours = theme.Colours.Dark;
        Colour = role switch
        {
            YokkoThemeBoxRole.Surface => colours.Surface,
            YokkoThemeBoxRole.SurfaceElevated => colours.SurfaceElevated,
            YokkoThemeBoxRole.Panel => colours.Panel,
            YokkoThemeBoxRole.PanelAlt => colours.PanelAlt,
            _ => colours.Background,
        };
    }
}
