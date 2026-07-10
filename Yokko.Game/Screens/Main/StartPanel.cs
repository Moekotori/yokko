using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osuTK;
using osuTK.Graphics;
using Yokko.Game.Presentation;

namespace Yokko.Game.Screens.Main;

public partial class StartPanel : ClickableContainer
{
    private readonly Box background;
    private readonly Color4 idleColour;

    public StartPanel(string eyebrow, string title, string detail, IconUsage icon, Action action, Color4 accent, float width, bool primary = false)
    {
        Action = action;
        Size = new Vector2(width, 118);
        Masking = true;
        CornerRadius = 12;
        BorderThickness = 1;
        BorderColour = new Color4(accent.R, accent.G, accent.B, primary ? 0.68f : 0.32f);
        idleColour = primary
            ? new Color4(accent.R * 0.17f, accent.G * 0.17f, accent.B * 0.17f, 1f)
            : YokkoPalette.Surface;

        InternalChildren = new Drawable[]
        {
            background = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = idleColour,
            },
            new Box
            {
                RelativeSizeAxes = Axes.X,
                Height = 3,
                Colour = accent,
            },
            new SpriteIcon
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                X = -22,
                Size = new Vector2(primary ? 34 : 28),
                Icon = icon,
                Colour = new Color4(accent.R, accent.G, accent.B, primary ? 0.92f : 0.68f),
            },
            new FillFlowContainer
            {
                RelativeSizeAxes = Axes.Both,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 5),
                Padding = new MarginPadding { Left = 22, Top = 17, Right = 70 },
                Children = new Drawable[]
                {
                    new SpriteText
                    {
                        Text = eyebrow,
                        Font = FontUsage.Default.With(size: 12, weight: "Bold"),
                        Colour = accent,
                    },
                    new SpriteText
                    {
                        Text = title,
                        Font = FontUsage.Default.With(size: primary ? 26 : 22, weight: "SemiBold"),
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

    protected override bool OnHover(HoverEvent e)
    {
        background.FadeColour(YokkoPalette.SurfaceHover, 140, Easing.OutQuint);
        this.ScaleTo(1.015f, 140, Easing.OutQuint);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        background.FadeColour(idleColour, 160, Easing.OutQuint);
        this.ScaleTo(1f, 160, Easing.OutQuint);
    }
}
