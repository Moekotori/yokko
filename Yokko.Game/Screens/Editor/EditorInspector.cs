using System.IO;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osuTK;
using Yokko.Core.Editing;
using Yokko.Core.Timing;
using Yokko.Game.Localisation;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Screens.Editor;

/// <summary>
/// The ivory chart-facts card on the right of the editor. It surfaces the
/// chart metadata, timeline window, timing, scroll-velocity, audio, and
/// source information for the active draft.
/// </summary>
public partial class EditorInspector : CompositeDrawable
{
    private readonly EditableBeatmap beatmap;
    private readonly TimelineViewport viewport;
    private readonly SpriteText titleText;
    private readonly SpriteText modeText;
    private readonly SpriteText noteCountText;
    private readonly SpriteText lengthText;
    private readonly SpriteText windowText;
    private readonly SpriteText densityText;
    private readonly SpriteText scrollVelocityText;
    private readonly SpriteText audioText;
    private readonly SpriteText sourceText;

    public EditorInspector(EditableBeatmap beatmap, TimelineViewport viewport)
    {
        this.beatmap = beatmap;
        this.viewport = viewport;

        Size = new Vector2(
            EditorScreen.InspectorWidth,
            EditorScreen.InspectorHeight);

        InternalChildren = new Drawable[]
        {
            EditorTheme.CreateCardShadow(),
            EditorTheme.CreateIvoryCard(),
            new FillFlowContainer
            {
                RelativeSizeAxes = Axes.Both,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 18),
                Padding = new MarginPadding(24),
                Children = new Drawable[]
                {
                    new SpriteText
                    {
                        Text = YokkoStrings.Get("editor.inspector.heading"),
                        Font = HomeTypography.Display(9),
                        Colour = EditorTheme.NavyText(0.55f),
                    },
                    new Box
                    {
                        Size = new Vector2(54, 2),
                        Colour = EditorTheme.Pink,
                    },
                    titleText = new SpriteText
                    {
                        Font = HomeTypography.Display(18),
                        Colour = EditorTheme.Navy,
                    },
                    modeText = createMetric(),
                    noteCountText = createMetric(),
                    lengthText = createMetric(),
                    windowText = createMetric(),
                    densityText = createMetric(),
                    scrollVelocityText = createMetric(),
                    audioText = createMetric(),
                    new Box
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = 1,
                        Colour = EditorTheme.Border(0.14f),
                    },
                    sourceText = new SpriteText
                    {
                        Font = HomeTypography.Body(11),
                        Colour = EditorTheme.NavyText(0.55f),
                    },
                },
            },
        };

        Refresh();
    }

    public void Refresh()
    {
        double lengthMilliseconds = beatmap.Notes.Count == 0
            ? 0
            : beatmap.Notes[^1].StartTimeMilliseconds + beatmap.TimingMap.StepAtTime(beatmap.Notes[^1].StartTimeMilliseconds);

        titleText.Text = beatmap.Title;
        modeText.Text = YokkoStrings.Get("editor.inspector.mode", (int)beatmap.KeyMode);
        noteCountText.Text = YokkoStrings.Get("editor.inspector.notes", beatmap.Notes.Count);
        lengthText.Text = YokkoStrings.Get("editor.inspector.length", $"{lengthMilliseconds / 1000:0.00}");
        windowText.Text = YokkoStrings.Get(
            "editor.inspector.window",
            viewport.StartRow + 1,
            viewport.EndRowExclusive);
        double bpm = beatmap.TimingMap.TimingPointAt(beatmap.TimeAtRow(viewport.StartRow)).BeatsPerMinute;
        double windowStartTime = beatmap.TimeAtRow(viewport.StartRow);
        var scrollMap = new ScrollVelocityMap(
            beatmap.ScrollVelocities,
            beatmap.InitialScrollVelocity);
        scrollVelocityText.Text = YokkoStrings.Get(
            "editor.inspector.scroll",
            $"{scrollMap.MultiplierAt(windowStartTime):0.###}",
            beatmap.ScrollVelocities.Count,
            beatmap.ScrollSpeedFactors.Count,
            beatmap.ScrollProfiles.Count);
        densityText.Text = YokkoStrings.Get(
            "editor.inspector.grid",
            beatmap.Rows,
            beatmap.BeatDivisor,
            $"{bpm:0.##}");
        audioText.Text = beatmap.AudioPath == null
            ? YokkoStrings.Get("editor.inspector.audio_missing")
            : YokkoStrings.Get("editor.inspector.audio", Path.GetFileName(beatmap.AudioPath));
        sourceText.Text = beatmap.SourcePath == null
            ? YokkoStrings.Get("editor.inspector.source_draft")
            : YokkoStrings.Get("editor.inspector.source", Path.GetFileName(beatmap.SourcePath));
    }

    private static SpriteText createMetric() => new()
    {
        Font = HomeTypography.Body(13),
        Colour = EditorTheme.NavyText(0.75f),
    };
}
