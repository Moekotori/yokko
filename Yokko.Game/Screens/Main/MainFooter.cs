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
        Height = 46;

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
                Text = $"{KeyMode.FourKey:D}K + {KeyMode.SevenKey:D}K ready   •   Esc always takes you back",
                Font = FontUsage.Default.With(size: 15),
                Colour = YokkoPalette.TextMuted,
            },
            new SpriteText
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                Y = 16,
                Text = "Keyboard first. Mouse friendly.",
                Font = FontUsage.Default.With(size: 15),
                Colour = YokkoPalette.TextDim,
            },
        };
    }
}
