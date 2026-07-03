using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;
using Yokko.Core.Editing;
using Yokko.Game.Presentation;

namespace Yokko.Game.Screens.Editor;

public partial class EditorTimelineControls : CompositeDrawable
{
    private readonly EditableBeatmap beatmap;
    private readonly TimelineViewport viewport;
    private readonly SpriteText windowText;

    public EditorTimelineControls(
        EditableBeatmap beatmap,
        TimelineViewport viewport,
        Action jumpBack,
        Action stepBack,
        Action stepForward,
        Action jumpForward,
        Action zoomIn,
        Action zoomOut,
        Action appendRows)
    {
        this.beatmap = beatmap;
        this.viewport = viewport;

        Width = beatmap.LaneCount == 4 ? 500 : 760;
        Height = 44;
        Masking = true;
        CornerRadius = 6;

        InternalChildren = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = new Color4(0.04f, 0.052f, 0.073f, 0.95f),
            },
            windowText = new SpriteText
            {
                Anchor = Anchor.TopLeft,
                Origin = Anchor.TopLeft,
                X = 12,
                Y = 4,
                Font = FontUsage.Default.With(size: 14),
                Colour = YokkoPalette.TextMuted,
            },
            new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Anchor = Anchor.BottomRight,
                Origin = Anchor.BottomRight,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(6, 0),
                X = -8,
                Y = -4,
                Children = new Drawable[]
                {
                    new EditorStepButton("-16", jumpBack, 42),
                    new EditorStepButton("-4", stepBack, 38),
                    new EditorStepButton("+4", stepForward, 38),
                    new EditorStepButton("+16", jumpForward, 42),
                    new EditorStepButton("Zoom +", zoomIn, 58, YokkoPalette.Rose),
                    new EditorStepButton("Zoom -", zoomOut, 58, YokkoPalette.Rose),
                    new EditorStepButton("+32 rows", appendRows, 78, YokkoPalette.Lime),
                },
            },
        };

        Refresh();
    }

    public void Refresh()
    {
        windowText.Text = $"Rows {viewport.StartRow + 1}-{viewport.EndRowExclusive} / {beatmap.Rows}   {formatSeconds(viewport.StartMilliseconds(beatmap.StepMilliseconds))}-{formatSeconds(viewport.EndMilliseconds(beatmap.StepMilliseconds))}   zoom {viewport.VisibleRows}";
    }

    private static string formatSeconds(double milliseconds) => $"{milliseconds / 1000:0.00}s";
}

public partial class EditorStepButton : ClickableContainer
{
    public EditorStepButton(string text, Action action, float width = 48, Color4? accent = null)
    {
        Action = action;
        Size = new Vector2(width, 22);
        Masking = true;
        CornerRadius = 4;

        InternalChildren = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = new Color4(0.085f, 0.105f, 0.14f, 1f),
            },
            new Box
            {
                RelativeSizeAxes = Axes.Y,
                Width = 3,
                Colour = accent ?? YokkoPalette.Cyan,
            },
            new SpriteText
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Text = text,
                Font = FontUsage.Default.With(size: 13),
                Colour = YokkoPalette.Text,
            },
        };
    }
}
