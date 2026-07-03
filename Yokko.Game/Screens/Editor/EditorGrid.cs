using System;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osuTK;
using osuTK.Graphics;
using Yokko.Core.Editing;
using Yokko.Game.Presentation;

namespace Yokko.Game.Screens.Editor;

public partial class EditorGrid : CompositeDrawable
{
    private readonly EditableBeatmap beatmap;
    private readonly TimelineViewport viewport;
    private readonly Action<int> scrollByRows;
    private readonly EditorCell[,] cells;

    public EditorGrid(EditableBeatmap beatmap, TimelineViewport viewport, Action<int> scrollByRows)
    {
        this.beatmap = beatmap;
        this.viewport = viewport;
        this.scrollByRows = scrollByRows;
        cells = new EditorCell[beatmap.LaneCount, viewport.VisibleRows];

        Width = beatmap.LaneCount == 4 ? 500 : 760;
        Height = 336;
        Masking = true;
        CornerRadius = 6;

        float laneWidth = Width / beatmap.LaneCount;
        float rowHeight = Height / viewport.VisibleRows;

        var gridCells = new Drawable[beatmap.LaneCount * viewport.VisibleRows];
        int cellIndex = 0;

        for (int visualRow = 0; visualRow < viewport.VisibleRows; visualRow++)
        {
            int row = viewport.StartRow + visualRow;

            for (int lane = 0; lane < beatmap.LaneCount; lane++)
            {
                var cell = new EditorCell(lane, row, toggleNote)
                {
                    X = lane * laneWidth,
                    Y = visualRow * rowHeight,
                    Width = laneWidth,
                    Height = rowHeight,
                };

                cells[lane, visualRow] = cell;
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
        for (int visualRow = 0; visualRow < viewport.VisibleRows; visualRow++)
        {
            int row = viewport.StartRow + visualRow;

            for (int lane = 0; lane < beatmap.LaneCount; lane++)
            {
                cells[lane, visualRow].Bind(lane, row);
                cells[lane, visualRow].SetSelected(beatmap.HasNoteAt(lane, row));
            }
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

    protected override bool OnScroll(ScrollEvent e)
    {
        scrollByRows(e.ScrollDelta.Y > 0 ? -4 : 4);
        return true;
    }
}
