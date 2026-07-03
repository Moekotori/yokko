using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osuTK.Graphics;
using Yokko.Core.Gameplay;
using Yokko.Game.Presentation;

namespace Yokko.Game.Screens.Main;

public partial class MainFooter : CompositeDrawable
{
    public MainFooter()
    {
        RelativeSizeAxes = Axes.X;
        Height = 50;

        InternalChildren = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.X,
                Height = 1,
                Colour = new Color4(1f, 1f, 1f, 0.12f),
            },
            new SpriteText
            {
                Y = 16,
                Text = $"{KeyMode.FourKey:D}K and {KeyMode.SevenKey:D}K core shell ready",
                Font = FontUsage.Default.With(size: 18),
                Colour = YokkoPalette.TextMuted,
            },
        };
    }
}
