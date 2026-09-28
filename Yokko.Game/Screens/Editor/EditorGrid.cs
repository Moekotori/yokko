using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osuTK.Graphics;
using Yokko.Core.Editing;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Screens.Editor;

/// <summary>
/// The deep navy note canvas. A 96px left gutter carries the time ruler while
/// the remaining width hosts the clickable lane/row cells.
/// </summary>
public partial class EditorGrid : CompositeDrawable
{
    internal const float GutterWidth = 96;

    private readonly EditableBeatmap beatmap;
    private readonly TimelineViewport viewport;
    private readonly Action<int> scrollByRows;
    private readonly EditorCell[,] cells;
    private readonly Container rulerContent;
    private readonly Box playheadLine;
    private readonly float rowHeight;

    public EditorGrid(EditableBeatmap beatmap, TimelineViewport viewport, Action<int> scrollByRows)
    {
        this.beatmap = beatmap;
        this.viewport = viewport;
        this.scrollByRows = scrollByRows;
        cells = new EditorCell[beatmap.LaneCount, viewport.VisibleRows];

        Width = EditorScreen.WorkspaceWidth;
        Height = EditorScreen.GridHeight;
        Masking = true;
        CornerRadius = EditorTheme.CardRadius;
        BorderThickness = 1.25f;
        BorderColour = EditorTheme.SurfaceRaised;

        float laneAreaWidth = Width - GutterWidth;
        float laneWidth = laneAreaWidth / beatmap.LaneCount;
        rowHeight = Height / viewport.VisibleRows;

        var gridCells = new Drawable[beatmap.LaneCount * viewport.VisibleRows];
        int cellIndex = 0;

        for (int visualRow = 0; visualRow < viewport.VisibleRows; visualRow++)
        {
            int row = viewport.StartRow + visualRow;

            for (int lane = 0; lane < beatmap.LaneCount; lane++)
            {
                var cell = new EditorCell(lane, row, toggleNote)
                {
                    X = GutterWidth + lane * laneWidth,
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
                Colour = EditorTheme.DeepNavy,
            },
            new Box
            {
                RelativeSizeAxes = Axes.Y,
                Width = GutterWidth,
                Colour = EditorTheme.Surface,
            },
            new Container
            {
                RelativeSizeAxes = Axes.Both,
                Children = gridCells,
            },
            rulerContent = new Container
            {
                RelativeSizeAxes = Axes.Y,
                Width = GutterWidth,
            },
            new Box
            {
                RelativeSizeAxes = Axes.Y,
                Width = 1,
                X = GutterWidth,
                Colour = EditorTheme.Cyan,
                Alpha = 0.32f,
            },
            playheadLine = new Box
            {
                X = GutterWidth,
                Width = Width - GutterWidth,
                Height = 2,
                Alpha = 0,
                Colour = EditorTheme.Yellow,
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

    public void SetPlayheadTime(double timeMilliseconds)
    {
        double startMilliseconds = beatmap.TimeAtRow(viewport.StartRow);
        double endMilliseconds = beatmap.TimeAtRow(viewport.EndRowExclusive);

        if (timeMilliseconds < startMilliseconds || timeMilliseconds > endMilliseconds)
        {
            playheadLine.Alpha = 0;
            return;
        }

        double progress = (timeMilliseconds - startMilliseconds) / Math.Max(1, endMilliseconds - startMilliseconds);
        playheadLine.Y = Math.Clamp((float)progress * Height, 0, Height);
        playheadLine.Alpha = 0.95f;
    }

    public void Refresh()
    {
        for (int visualRow = 0; visualRow < viewport.VisibleRows; visualRow++)
        {
            int row = viewport.StartRow + visualRow;

            for (int lane = 0; lane < beatmap.LaneCount; lane++)
            {
                cells[lane, visualRow].Bind(
                    lane,
                    row,
                    beatmap.TimingMap.IsBeatRow(row),
                    beatmap.TimingMap.IsMeasureRow(row));
                cells[lane, visualRow].SetSelected(beatmap.HasNoteAt(lane, row));
            }
        }

        rebuildRuler();
    }

    private void rebuildRuler()
    {
        var markers = new List<Drawable>();

        for (int visualRow = 0; visualRow < viewport.VisibleRows; visualRow++)
        {
            int row = viewport.StartRow + visualRow;

            if (!beatmap.TimingMap.IsBeatRow(row))
                continue;

            bool isMeasure = beatmap.TimingMap.IsMeasureRow(row);
            float y = visualRow * rowHeight;

            markers.Add(new Box
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                Y = y,
                Width = isMeasure ? 18 : 10,
                Height = 1,
                Colour = new Color4(1f, 1f, 1f, isMeasure ? 0.4f : 0.18f),
            });

            if (!isMeasure)
                continue;

            markers.Add(new SpriteText
            {
                X = 10,
                Y = y + 4,
                Text = formatSeconds(beatmap.TimeAtRow(row)),
                Font = HomeTypography.Display(10),
                Colour = EditorTheme.Muted,
            });
        }

        rulerContent.Children = markers;
    }

    private Drawable[] createLaneLabels(float laneWidth)
        => Enumerable.Range(0, beatmap.LaneCount)
                     .Select(lane => new SpriteText
                     {
                         X = GutterWidth + lane * laneWidth + laneWidth / 2,
                         Anchor = Anchor.BottomLeft,
                         Origin = Anchor.BottomCentre,
                         Y = -6,
                         Text = (lane + 1).ToString(),
                         Font = HomeTypography.Display(12),
                         Colour = EditorTheme.Muted,
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

    private static string formatSeconds(double milliseconds) => $"{milliseconds / 1000:0.00}s";
}
