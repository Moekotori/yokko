using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Shapes;
using osuTK.Graphics;

namespace Yokko.Game.Presentation;

public enum YokkoDividerStyle
{
    Subtle,
    Brand,
}

public partial class YokkoDivider : Box
{
    private readonly YokkoDividerStyle style;
    private IBindable<YokkoUiTheme> currentTheme;

    public YokkoDivider(YokkoDividerStyle style = YokkoDividerStyle.Subtle)
    {
        this.style = style;
        RelativeSizeAxes = Axes.X;
        Height = 1;
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
        Color4 colour = style == YokkoDividerStyle.Brand
            ? theme.Colours.Settings.Divider
            : theme.Colours.Dark.Text;
        Colour = style == YokkoDividerStyle.Brand
            ? colour
            : new Color4(colour.R, colour.G, colour.B, 0.1f);
    }
}
