using System;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;
using Yokko.Core.Editing;
using Yokko.Game.Presentation;

namespace Yokko.Game.Screens.Editor;

public partial class EditorGrid : CompositeDrawable
{
    private readonly EditableBeatmap beatmap;
    private readonly EditorCell[,] cells;

    public EditorGrid(EditableBeatmap beatmap)
    {
        this.beatmap = beatmap;
        cells = new EditorCell[beatmap.LaneCount, beatmap.Rows];

        Width = beatmap.LaneCount == 4 ? 500 : 760;
        Height = 540;
        Masking = true;

        float laneWidth = Width / beatmap.LaneCount;
        float rowHeight = Height / beatmap.Rows;

        var gridCells = new Drawable[beatmap.LaneCount * beatmap.Rows];
        int cellIndex = 0;

        for (int row = 0; row < beatmap.Rows; row++)
        {
            for (int lane = 0; lane < beatmap.LaneCount; lane++)
            {
                var cell = new EditorCell(lane, row, toggleNote)
                {
                    X = lane * laneWidth,
                    Y = row * rowHeight,
                    Width = laneWidth,
                    Height = rowHeight,
                };

                cells[lane, row] = cell;
                gridCells[cellIndex++] = cell;
            }
        }

        InternalChildren = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = new Color4(0.025f, 0.032f, 0.044f, 1f),
            },
            new Container
            {
                RelativeSizeAxes = Axes.Both,
                Children = gridCells,
            },
            new Container
            {
                RelativeSizeAxes = Axes.X,
                Height = 30,
                Anchor = Anchor.BottomLeft,
                Origin = Anchor.BottomLeft,
                Children = createLaneLabels(laneWidth),
            },
        };

        Refresh();
    }

    public event Action NotesChanged;

    public void Refresh()
    {
        for (int row = 0; row < beatmap.Rows; row++)
        {
            for (int lane = 0; lane < beatmap.LaneCount; lane++)
                cells[lane, row].SetSelected(beatmap.HasNoteAt(lane, row));
        }
    }

    private Drawable[] createLaneLabels(float laneWidth)
        => Enumerable.Range(0, beatmap.LaneCount)
                     .Select(lane => new SpriteText
                     {
                         X = lane * laneWidth + laneWidth / 2,
                         Anchor = Anchor.BottomLeft,
                         Origin = Anchor.BottomCentre,
                         Y = -6,
                         Text = (lane + 1).ToString(),
                         Font = FontUsage.Default.With(size: 14),
                         Colour = YokkoPalette.TextDim,
                     })
                     .Cast<Drawable>()
                     .ToArray();

    private void toggleNote(int lane, int row)
    {
        beatmap.ToggleNote(lane, row);
        Refresh();
        NotesChanged?.Invoke();
    }
}
