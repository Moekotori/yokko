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
        Action appendRows)
    {
        this.beatmap = beatmap;
        this.viewport = viewport;

        Width = beatmap.LaneCount == 4 ? 500 : 760;
        Height = 32;
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
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 12,
                Font = FontUsage.Default.With(size: 15),
                Colour = YokkoPalette.TextMuted,
            },
            new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(6, 0),
                X = -8,
                Children = new Drawable[]
                {
                    new EditorStepButton("-16", jumpBack),
                    new EditorStepButton("-4", stepBack),
                    new EditorStepButton("+4", stepForward),
                    new EditorStepButton("+16", jumpForward),
                    new EditorStepButton("+32 rows", appendRows, 78, YokkoPalette.Lime),
                },
            },
        };

        Refresh();
    }

    public void Refresh()
    {
        windowText.Text = $"Rows {viewport.StartRow + 1}-{viewport.EndRowExclusive} / {beatmap.Rows}   {formatSeconds(viewport.StartMilliseconds(beatmap.StepMilliseconds))}-{formatSeconds(viewport.EndMilliseconds(beatmap.StepMilliseconds))}";
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
