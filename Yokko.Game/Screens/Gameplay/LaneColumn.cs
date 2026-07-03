using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osuTK.Graphics;
using Yokko.Game.Presentation;

namespace Yokko.Game.Screens.Gameplay;

public partial class LaneColumn : CompositeDrawable
{
    private readonly Box pressedOverlay;

    public LaneColumn(string keyLabel)
    {
        RelativeSizeAxes = Axes.Y;

        InternalChildren = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = new Color4(0.04f, 0.052f, 0.07f, 0.9f),
            },
            new Box
            {
                RelativeSizeAxes = Axes.Y,
                Width = 1,
                Colour = new Color4(1f, 1f, 1f, 0.08f),
            },
            pressedOverlay = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = new Color4(YokkoPalette.Cyan.R, YokkoPalette.Cyan.G, YokkoPalette.Cyan.B, 0.22f),
                Alpha = 0,
            },
            new SpriteText
            {
                Anchor = Anchor.BottomCentre,
                Origin = Anchor.BottomCentre,
                Y = -26,
                Text = keyLabel,
                Font = FontUsage.Default.With(size: 18),
                Colour = YokkoPalette.TextMuted,
            },
        };
    }

    public void SetPressed(bool pressed)
    {
        pressedOverlay.Alpha = pressed ? 1 : 0;
    }
}
