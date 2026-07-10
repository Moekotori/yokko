using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osuTK.Graphics;
using Yokko.Game.Presentation;

namespace Yokko.Game.Screens.Main;

public partial class MainBackdrop : CompositeDrawable
{
    public MainBackdrop()
    {
        RelativeSizeAxes = Axes.Both;

        InternalChildren = new Drawable[]
        {
            new Box
            {
                Width = 920,
                Height = 420,
                X = -260,
                Y = -160,
                Rotation = -10,
                Colour = new Color4(YokkoPalette.Cyan.R, YokkoPalette.Cyan.G, YokkoPalette.Cyan.B, 0.055f),
            },
            new Box
            {
                Width = 780,
                Height = 360,
                Anchor = Anchor.BottomRight,
                Origin = Anchor.BottomRight,
                X = 160,
                Y = 160,
                Rotation = -8,
                Colour = new Color4(YokkoPalette.Violet.R, YokkoPalette.Violet.G, YokkoPalette.Violet.B, 0.06f),
            },
            new Box
            {
                RelativeSizeAxes = Axes.X,
                Height = 1,
                Y = 170,
                Colour = new Color4(1f, 1f, 1f, 0.035f),
            },
        };
    }
}
