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
        Size = new Vector2(545, 202);
        Masking = true;

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
                Spacing = new Vector2(0, 16),
                Padding = new MarginPadding { Horizontal = 24, Vertical = 22 },
                Children = new Drawable[]
                {
                    new SpriteText
                    {
                        Text = title,
                        Font = FontUsage.Default.With(size: 26),
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
        Width = 148,
        Height = 40,
        Masking = true,
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
                Font = FontUsage.Default.With(size: 17),
                Colour = YokkoPalette.TextMuted,
            },
        }
    };
}
