using System.IO;
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

public partial class EditorInspector : CompositeDrawable
{
    private readonly EditableBeatmap beatmap;
    private readonly SpriteText modeText;
    private readonly SpriteText noteCountText;
    private readonly SpriteText lengthText;
    private readonly SpriteText densityText;
    private readonly SpriteText sourceText;

    public EditorInspector(EditableBeatmap beatmap)
    {
        this.beatmap = beatmap;

        Width = 330;
        Height = 540;
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
                    densityText = createMetric(),
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
            : beatmap.Notes[^1].StartTimeMilliseconds + beatmap.StepMilliseconds;

        modeText.Text = $"Mode {(int)beatmap.KeyMode}K";
        noteCountText.Text = $"Notes {beatmap.Notes.Count}";
        lengthText.Text = $"Length {lengthMilliseconds / 1000:0.00}s";
        densityText.Text = $"Grid {beatmap.Rows} rows @ {beatmap.StepMilliseconds:0}ms";
        sourceText.Text = beatmap.SourcePath == null
            ? "Source Yokko draft"
            : $"Source {Path.GetFileName(beatmap.SourcePath)}";
    }

    private static SpriteText createMetric() => new()
    {
        Font = FontUsage.Default.With(size: 20),
        Colour = YokkoPalette.TextMuted,
    };
}
