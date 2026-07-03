using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;
using Yokko.Game.Presentation;

namespace Yokko.Game.Screens.Editor;

public partial class EditorHeader : CompositeDrawable
{
    public EditorHeader(Action newFourKey, Action newSevenKey, Action importOsu, Action exportOsu, Action playtest)
    {
        Width = 1122;
        Height = 70;

        InternalChildren = new Drawable[]
        {
            new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 4),
                Children = new Drawable[]
                {
                    new SpriteText
                    {
                        Text = "Yokko Editor",
                        Font = FontUsage.Default.With(size: 34),
                        Colour = YokkoPalette.Text,
                    },
                    new SpriteText
                    {
                        Text = "4K / 7K charting workstation",
                        Font = FontUsage.Default.With(size: 16),
                        Colour = YokkoPalette.TextMuted,
                    },
                },
            },
            new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(8, 0),
                Children = new Drawable[]
                {
                    new EditorToolButton("New 4K", newFourKey),
                    new EditorToolButton("New 7K", newSevenKey),
                    new EditorToolButton("Import", importOsu),
                    new EditorToolButton("Export", exportOsu),
                    new EditorToolButton("Playtest", playtest, YokkoPalette.Lime),
                },
            },
        };
    }
}

public partial class EditorToolButton : ClickableContainer
{
    public EditorToolButton(string text, Action action, Color4? accent = null)
    {
        Action = action;
        Size = new Vector2(104, 42);
        Masking = true;

        InternalChildren = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = new Color4(0.09f, 0.11f, 0.145f, 0.98f),
            },
            new Box
            {
                RelativeSizeAxes = Axes.Y,
                Width = 4,
                Colour = accent ?? YokkoPalette.Cyan,
            },
            new SpriteText
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Text = text,
                Font = FontUsage.Default.With(size: 17),
                Colour = YokkoPalette.Text,
            },
        };
    }
}
