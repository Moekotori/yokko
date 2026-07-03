using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;
using Yokko.Game.Presentation;

namespace Yokko.Game.Screens.Main;

public partial class StartPanel : ClickableContainer
{
    public StartPanel(string title, string detail, Action action)
    {
        Action = action;
        Size = new Vector2(340, 74);
        Masking = true;

        InternalChildren = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = new Color4(0.085f, 0.105f, 0.135f, 0.98f),
            },
            new Box
            {
                RelativeSizeAxes = Axes.Y,
                Width = 5,
                Colour = YokkoPalette.Lime,
            },
            new FillFlowContainer
            {
                RelativeSizeAxes = Axes.Both,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 6),
                Padding = new MarginPadding { Left = 22, Top = 12, Right = 18 },
                Children = new Drawable[]
                {
                    new SpriteText
                    {
                        Text = title,
                        Font = FontUsage.Default.With(size: 22),
                        Colour = YokkoPalette.Text,
                    },
                    new SpriteText
                    {
                        Text = detail,
                        Font = FontUsage.Default.With(size: 15),
                        Colour = YokkoPalette.TextMuted,
                    },
                },
            },
        };
    }
}
