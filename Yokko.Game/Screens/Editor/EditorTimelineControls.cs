using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;
using Yokko.Core.Editing;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Screens.Editor;

/// <summary>
/// The ivory transport row under the grid: timeline window information,
/// playback state, and the navigation/zoom/append buttons.
/// </summary>
public partial class EditorTimelineControls : CompositeDrawable
{
    private readonly EditableBeatmap beatmap;
    private readonly TimelineViewport viewport;
    private readonly SpriteText windowText;
    private readonly SpriteText playbackText;
    private readonly EditorStepButton playPauseButton;

    public EditorTimelineControls(
        EditableBeatmap beatmap,
        TimelineViewport viewport,
        Action togglePlayback,
        Action stopPlayback,
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

        Width = EditorScreen.WorkspaceWidth;
        Height = EditorScreen.TransportHeight;
        Masking = true;
        CornerRadius = 8;
        BorderThickness = 1.25f;
        BorderColour = EditorTheme.Border();

        InternalChildren = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = EditorTheme.Ivory,
            },
            windowText = new SpriteText
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = 16,
                Font = HomeTypography.Display(11),
                Colour = EditorTheme.NavyText(0.72f),
            },
            playbackText = new SpriteText
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                X = 40,
                Font = HomeTypography.Display(11),
                Colour = EditorTheme.NavyText(0.62f),
            },
            new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(6, 0),
                X = -10,
                Children = new Drawable[]
                {
                    playPauseButton = new EditorStepButton("Play", togglePlayback, 56, EditorTheme.Cyan),
                    new EditorStepButton("Stop", stopPlayback, 50, EditorTheme.Pink),
                    new EditorStepButton("-16", jumpBack, 44),
                    new EditorStepButton("-4", stepBack, 40),
                    new EditorStepButton("+4", stepForward, 40),
                    new EditorStepButton("+16", jumpForward, 44),
                    new EditorStepButton("Zoom+", zoomIn, 62, EditorTheme.Yellow),
                    new EditorStepButton("Zoom-", zoomOut, 62, EditorTheme.Yellow),
                    new EditorStepButton("+32", appendRows, 48, EditorTheme.Cyan),
                },
            },
        };

        Refresh();
    }

    public void Refresh()
    {
        windowText.Text = $"Rows {viewport.StartRow + 1}-{viewport.EndRowExclusive} / {beatmap.Rows}   {formatSeconds(beatmap.TimeAtRow(viewport.StartRow))}-{formatSeconds(beatmap.TimeAtRow(viewport.EndRowExclusive))}   1/{beatmap.BeatDivisor}   zoom {viewport.VisibleRows}";
    }

    public void RefreshPlayback(double timeMilliseconds, double durationMilliseconds, bool isPlaying)
    {
        playPauseButton.SetText(isPlaying ? "Pause" : "Play");
        playbackText.Text = $"{formatSeconds(timeMilliseconds)} / {formatSeconds(durationMilliseconds)}";
        playbackText.Colour = isPlaying ? EditorTheme.Pink : EditorTheme.NavyText(0.62f);
    }

    private static string formatSeconds(double milliseconds) => $"{milliseconds / 1000:0.00}s";
}

public partial class EditorStepButton : ClickableContainer
{
    private readonly SpriteText label;

    public EditorStepButton(string text, Action action, float width = 48, Color4? accent = null)
    {
        Action = action;
        Size = new Vector2(width, 26);
        Masking = true;
        CornerRadius = 5;
        BorderThickness = 1;
        BorderColour = EditorTheme.Border(0.24f);

        InternalChildren = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = EditorTheme.NavyText(0.05f),
            },
            new Box
            {
                RelativeSizeAxes = Axes.Y,
                Width = 3,
                Colour = accent ?? EditorTheme.Navy,
            },
            label = new SpriteText
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Text = text,
                Font = HomeTypography.Display(9),
                Colour = EditorTheme.Navy,
            },
        };
    }

    public void SetText(string text)
    {
        label.Text = text;
    }
}
