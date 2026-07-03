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
                Width = 780,
                Height = 6,
                X = -80,
                Y = 184,
                Rotation = -12,
                Colour = new Color4(YokkoPalette.Cyan.R, YokkoPalette.Cyan.G, YokkoPalette.Cyan.B, 0.42f),
            },
            new Box
            {
                Width = 1040,
                Height = 4,
                X = 360,
                Y = 520,
                Rotation = -12,
                Colour = new Color4(YokkoPalette.Rose.R, YokkoPalette.Rose.G, YokkoPalette.Rose.B, 0.32f),
            },
            new Box
            {
                Width = 520,
                Height = 320,
                Anchor = Anchor.BottomRight,
                Origin = Anchor.BottomRight,
                Colour = new Color4(0.08f, 0.12f, 0.17f, 0.74f),
            },
        };
    }
}
