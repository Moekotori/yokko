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
        Size = new Vector2(340, 104);
        Masking = true;
        CornerRadius = 10;
        BorderThickness = 1;
        BorderColour = YokkoPalette.Border;

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
                Width = 4,
                Colour = accent,
            },
            new FillFlowContainer
            {
                RelativeSizeAxes = Axes.Both,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 5),
                Padding = new MarginPadding { Left = 20, Right = 18, Top = 13 },
                Children = new Drawable[]
                {
                    new SpriteText
                    {
                        Text = label,
                        Font = FontUsage.Default.With(size: 12, weight: "Bold"),
                        Colour = accent,
                    },
                    new SpriteText
                    {
                        Text = value,
                        Font = FontUsage.Default.With(size: 23, weight: "SemiBold"),
                        Colour = YokkoPalette.Text,
                    },
                    new SpriteText
                    {
                        Text = note,
                        Font = FontUsage.Default.With(size: 15),
                        Colour = YokkoPalette.TextDim,
                    },
                }
            },
        };
    }
}
