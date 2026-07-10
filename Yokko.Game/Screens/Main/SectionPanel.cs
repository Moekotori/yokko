using System.Collections.Generic;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osuTK;
using Yokko.Game.Presentation;

namespace Yokko.Game.Screens.Main;

public partial class SectionPanel : CompositeDrawable
{
    public SectionPanel(string title, IReadOnlyList<string> items)
    {
        Size = new Vector2(515, 154);
        Masking = true;
        CornerRadius = 10;
        BorderThickness = 1;
        BorderColour = YokkoPalette.Border;

        InternalChildren = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = YokkoPalette.PanelAlt,
            },
            new FillFlowContainer
            {
                RelativeSizeAxes = Axes.Both,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 14),
                Padding = new MarginPadding { Horizontal = 22, Vertical = 18 },
                Children = new Drawable[]
                {
                    new SpriteText
                    {
                        Text = title,
                        Font = FontUsage.Default.With(size: 20, weight: "SemiBold"),
                        Colour = YokkoPalette.Text,
                    },
                    new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Horizontal,
                        Spacing = new Vector2(10, 10),
                        Children = items.Select(createItem).ToArray(),
                    },
                }
            },
        };
    }

    private static Drawable createItem(string text) => new Container
    {
        Width = 112,
        Height = 34,
        Masking = true,
        CornerRadius = 7,
        BorderThickness = 1,
        BorderColour = YokkoPalette.Border,
        Children = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = YokkoPalette.Chip,
            },
            new SpriteText
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Text = text,
                Font = FontUsage.Default.With(size: 13),
                Colour = YokkoPalette.TextMuted,
            },
        }
    };
}
