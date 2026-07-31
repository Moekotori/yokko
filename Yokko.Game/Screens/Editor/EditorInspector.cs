using System.IO;
using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;
using Yokko.Core.Editing;
using Yokko.Core.Timing;
using Yokko.Game.Presentation;
using Yokko.Game.Localisation;

namespace Yokko.Game.Screens.Editor;

public partial class EditorInspector : CompositeDrawable
{
    private readonly EditableBeatmap beatmap;
    private readonly TimelineViewport viewport;
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

        Width = 330;
        Height = 466;
        Masking = true;

        InternalChildren = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = new Color4(0.04f, 0.052f, 0.073f, 0.96f),
            },
            new FillFlowContainer
            {
                RelativeSizeAxes = Axes.Both,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 18),
                Padding = new MarginPadding(22),
                Children = new Drawable[]
                {
                    new SpriteText
                    {
                        Text = beatmap.Title,
                        Font = FontUsage.Default.With(size: 26),
                        Colour = YokkoPalette.Text,
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
                        Colour = new Color4(1f, 1f, 1f, 0.1f),
                    },
                    sourceText = new SpriteText
                    {
                        Font = FontUsage.Default.With(size: 16),
                        Colour = YokkoPalette.TextDim,
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
        Font = FontUsage.Default.With(size: 20),
        Colour = YokkoPalette.TextMuted,
    };
}
