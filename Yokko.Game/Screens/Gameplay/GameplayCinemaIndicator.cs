using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;
using Yokko.Game.Presentation;

namespace Yokko.Game.Screens.Gameplay;

internal partial class GameplayCinemaIndicator : CompositeDrawable
{
    internal GameplayCinemaIndicator()
    {
        AutoSizeAxes = Axes.Both;
        Masking = true;
        CornerRadius = 10;
        InternalChildren =
        [
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = new Color4(0.025f, 0.03f, 0.045f, 0.9f),
            },
            new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 2),
                Padding = new MarginPadding
                {
                    Horizontal = 18,
                    Vertical = 12,
                },
                Children =
                [
                    new SpriteText
                    {
                        Text = "CINEMA",
                        Font = new FontUsage("NotoSansCJK").With(size: 18),
                        Colour = YokkoPalette.Cyan,
                    },
                    new SpriteText
                    {
                        Text = "AUTO · PLAYFIELD HIDDEN",
                        Font = new FontUsage("NotoSansCJK").With(size: 11),
                        Colour = YokkoPalette.TextMuted,
                    },
                ],
            },
        ];
    }
}
