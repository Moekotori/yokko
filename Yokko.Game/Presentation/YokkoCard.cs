using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osuTK.Graphics;

namespace Yokko.Game.Presentation;

public enum YokkoCardStyle
{
    Surface,
    Elevated,
    Panel,
}

/// <summary>
/// A theme-aware surface for grouping page-owned content.
/// </summary>
public partial class YokkoCard : CompositeDrawable
{
    private readonly Box background;
    private readonly Container content;
    private readonly YokkoCardStyle style;
    private IBindable<YokkoUiTheme> currentTheme;

    public Drawable CardContent
    {
        get => content.Child;
        set => content.Child = value;
    }

    public MarginPadding ContentPadding
    {
        get => content.Padding;
        set => content.Padding = value;
    }

    public Color4 CurrentBackgroundColour => background.Colour;

    public YokkoCard(YokkoCardStyle style = YokkoCardStyle.Surface)
    {
        this.style = style;
        Masking = true;
        CornerRadius = YokkoUiTheme.Default.Metrics.CardCornerRadius;
        BorderThickness = 1;

        InternalChildren = new Drawable[]
        {
            background = new Box
            {
                RelativeSizeAxes = Axes.Both,
            },
            content = new Container
            {
                RelativeSizeAxes = Axes.Both,
            },
        };

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
        CornerRadius = theme.Metrics.CardCornerRadius;
        BorderColour = colours.Border;
        background.Colour = style switch
        {
            YokkoCardStyle.Elevated => colours.SurfaceElevated,
            YokkoCardStyle.Panel => colours.PanelAlt,
            _ => colours.Surface,
        };
    }
}
