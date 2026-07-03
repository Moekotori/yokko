using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;
using Yokko.Game.Presentation;

namespace Yokko.Game.Screens.Main;

public partial class StatusPanel : CompositeDrawable
{
    public StatusPanel(string label, string value, string note, Color4 accent)
    {
        Size = new Vector2(360, 132);
        Masking = true;

        InternalChildren = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = YokkoPalette.Panel,
            },
            new Box
            {
                RelativeSizeAxes = Axes.Y,
                Width = 5,
                Colour = accent,
            },
            new FillFlowContainer
            {
                RelativeSizeAxes = Axes.Both,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 8),
                Padding = new MarginPadding { Left = 22, Right = 18, Top = 18 },
                Children = new Drawable[]
                {
                    new SpriteText
                    {
                        Text = label,
                        Font = FontUsage.Default.With(size: 18),
                        Colour = YokkoPalette.TextDim,
                    },
                    new SpriteText
                    {
                        Text = value,
                        Font = FontUsage.Default.With(size: 34),
                        Colour = YokkoPalette.Text,
                    },
                    new SpriteText
                    {
                        Text = note,
                        Font = FontUsage.Default.With(size: 17),
                        Colour = YokkoPalette.TextDim,
                    },
                }
            },
        };
    }
}
