using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osuTK;
using Yokko.Game.Presentation;

namespace Yokko.Game.Screens.Main;

public partial class MainHeader : CompositeDrawable
{
    public MainHeader(Action openSettings)
    {
        RelativeSizeAxes = Axes.X;
        Height = 108;

        InternalChildren = new Drawable[]
        {
            new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 5),
                Children = new Drawable[]
                {
                    new SpriteText
                    {
                        Text = "Yokko",
                        Font = FontUsage.Default.With(size: 58, weight: "Bold"),
                        Colour = YokkoPalette.Text,
                    },
                    new SpriteText
                    {
                        Text = "Create a chart. Feel the rhythm. Make it yours.",
                        Font = FontUsage.Default.With(size: 19),
                        Colour = YokkoPalette.TextMuted,
                    },
                },
            },
            new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Anchor = Anchor.BottomRight,
                Origin = Anchor.BottomRight,
                Y = -2,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(10, 0),
                Children = new Drawable[]
                {
                    createTag("CHART", YokkoPalette.Lime),
                    createTag("PLAY", YokkoPalette.Cyan),
                    createTag("IMPROVE", YokkoPalette.Rose),
                },
            },
            new YokkoButton("Display", FontAwesome.Solid.Cog, openSettings, 112, 40, YokkoButtonStyle.Quiet)
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
            },
        };
    }

    private static Drawable createTag(string text, osuTK.Graphics.Color4 accent) => new Container
    {
        Size = new Vector2(88, 30),
        Masking = true,
        CornerRadius = 12,
        BorderThickness = 1,
        BorderColour = new osuTK.Graphics.Color4(accent.R, accent.G, accent.B, 0.45f),
        Children = new Drawable[]
        {
            new osu.Framework.Graphics.Shapes.Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = new osuTK.Graphics.Color4(accent.R, accent.G, accent.B, 0.1f),
            },
            new SpriteText
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Text = text,
                Font = FontUsage.Default.With(size: 13, weight: "Bold"),
                Colour = accent,
            },
        },
    };
}
