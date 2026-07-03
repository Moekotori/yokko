using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using Yokko.Game.Presentation;

namespace Yokko.Game.Screens.Main;

public partial class MainHeader : CompositeDrawable
{
    public MainHeader()
    {
        RelativeSizeAxes = Axes.X;
        Height = 188;

        InternalChildren = new Drawable[]
        {
            new SpriteText
            {
                Text = "Yokko",
                Font = FontUsage.Default.With(size: 92),
                Colour = YokkoPalette.Text,
            },
            new SpriteText
            {
                Y = 108,
                Text = "precision vertical rhythm",
                Font = FontUsage.Default.With(size: 28),
                Colour = YokkoPalette.TextMuted,
            },
            new SpriteText
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                Text = "framework online",
                Font = FontUsage.Default.With(size: 20),
                Colour = YokkoPalette.Lime,
            },
        };
    }
}
