using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osuTK;
using osuTK.Graphics;

namespace Yokko.Game.Presentation;

public enum YokkoButtonStyle
{
    Quiet,
    Secondary,
    Primary,
}

public partial class YokkoButton : ClickableContainer
{
    private readonly Box background;
    private readonly Color4 idleColour;
    private readonly Color4 hoverColour;

    public YokkoButton(
        string text,
        IconUsage icon,
        Action action,
        float width = 112,
        float height = 42,
        YokkoButtonStyle style = YokkoButtonStyle.Secondary,
        Color4? accent = null)
    {
        Color4 resolvedAccent = accent ?? YokkoPalette.Cyan;
        Action = action;
        Size = new Vector2(width, height);
        Masking = true;
        CornerRadius = 8;
        BorderThickness = 1;
        BorderColour = style == YokkoButtonStyle.Primary
            ? new Color4(resolvedAccent.R, resolvedAccent.G, resolvedAccent.B, 0.72f)
            : YokkoPalette.Border;

        idleColour = style switch
        {
            YokkoButtonStyle.Primary => new Color4(resolvedAccent.R * 0.24f, resolvedAccent.G * 0.24f, resolvedAccent.B * 0.24f, 1f),
            YokkoButtonStyle.Quiet => new Color4(0.035f, 0.046f, 0.064f, 0.72f),
            _ => YokkoPalette.SurfaceElevated,
        };
        hoverColour = style == YokkoButtonStyle.Primary
            ? new Color4(resolvedAccent.R * 0.34f, resolvedAccent.G * 0.34f, resolvedAccent.B * 0.34f, 1f)
            : YokkoPalette.SurfaceHover;

        InternalChildren = new Drawable[]
        {
            background = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = idleColour,
            },
            new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(8, 0),
                Children = new Drawable[]
                {
                    new SpriteIcon
                    {
                        Size = new Vector2(15),
                        Icon = icon,
                        Colour = style == YokkoButtonStyle.Primary ? resolvedAccent : YokkoPalette.TextMuted,
                    },
                    new SpriteText
                    {
                        Text = text,
                        Font = FontUsage.Default.With(size: 16, weight: "SemiBold"),
                        Colour = YokkoPalette.Text,
                    },
                },
            },
        };
    }

    protected override bool OnHover(HoverEvent e)
    {
        background.FadeColour(hoverColour, 120, Easing.OutQuint);
        this.ScaleTo(1.025f, 120, Easing.OutQuint);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        background.FadeColour(idleColour, 140, Easing.OutQuint);
        this.ScaleTo(1f, 140, Easing.OutQuint);
    }
}
