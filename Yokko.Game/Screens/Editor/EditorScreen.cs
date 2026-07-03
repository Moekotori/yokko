using System;
using System.IO;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Platform;
using osu.Framework.Screens;
using osuTK;
using Yokko.Core.Editing;
using Yokko.Core.Gameplay;
using Yokko.Game.Presentation;
using Yokko.Game.Screens.Gameplay;
using Yokko.Import.Osu;

namespace Yokko.Game.Screens.Editor;

public partial class EditorScreen : Screen
{
    private const int visibleRows = 24;
    private const int rowStep = 4;
    private const int jumpStep = 16;
    private const int appendStep = 32;

    private FillFlowContainer workspace;
    private EditableBeatmap editableBeatmap;
    private TimelineViewport viewport;
    private EditorSignalStrip signalStrip;
    private EditorGrid grid;
    private EditorTimelineControls timelineControls;
    private EditorInspector inspector;
    private SpriteText statusText;

    [Resolved]
    private GameHost host { get; set; }

    [BackgroundDependencyLoader]
    private void load()
    {
        editableBeatmap = EditableBeatmap.Create(KeyMode.FourKey);
        viewport = new TimelineViewport(0, visibleRows);

        InternalChildren = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = YokkoPalette.Background,
            },
            new FillFlowContainer
            {
                RelativeSizeAxes = Axes.Both,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 16),
                Padding = new MarginPadding { Horizontal = 46, Vertical = 24 },
                Children = new Drawable[]
                {
                    new EditorHeader(
                        () => loadChart(KeyMode.FourKey),
                        () => loadChart(KeyMode.SevenKey),
                        importOsu,
                        exportOsu,
                        playtest),
                    workspace = new FillFlowContainer
                    {
                        AutoSizeAxes = Axes.X,
                        Height = 480,
                        Direction = FillDirection.Horizontal,
                        Spacing = new Vector2(32, 0),
                    },
                    statusText = new SpriteText
                    {
                        Text = "Ready. Import osu!mania or start charting from a blank grid.",
                        Font = FontUsage.Default.With(size: 16),
                        Colour = YokkoPalette.TextDim,
                    },
                },
            },
        };

        rebuildWorkspace();
    }

    private void loadChart(KeyMode keyMode)
    {
        editableBeatmap = EditableBeatmap.Create(keyMode);
        viewport = new TimelineViewport(0, visibleRows);
        rebuildWorkspace();
        setStatus($"New {(int)keyMode}K draft created.");
    }

    private void rebuildWorkspace()
    {
        viewport.MoveToRow(viewport.StartRow, editableBeatmap.Rows);

        signalStrip = new EditorSignalStrip(editableBeatmap, viewport);

        grid = new EditorGrid(editableBeatmap, viewport, scrollRows)
        {
            Anchor = Anchor.TopLeft,
            Origin = Anchor.TopLeft,
        };
        grid.NotesChanged += refreshEditorState;

        timelineControls = new EditorTimelineControls(
            editableBeatmap,
            viewport,
            () => scrollRows(-jumpStep),
            () => scrollRows(-rowStep),
            () => scrollRows(rowStep),
            () => scrollRows(jumpStep),
            appendRows);

        inspector = new EditorInspector(editableBeatmap, viewport)
        {
        };

        workspace.Children = new Drawable[]
        {
            new FillFlowContainer
            {
                AutoSizeAxes = Axes.X,
                Height = 480,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 8),
                Children = new Drawable[]
                {
                    signalStrip,
                    grid,
                    timelineControls,
                },
            },
            inspector,
        };
    }

    private void refreshEditorState()
    {
        signalStrip.Refresh();
        timelineControls.Refresh();
        inspector.Refresh();
    }

    private void scrollRows(int rowDelta)
    {
        int previousStart = viewport.StartRow;
        viewport.MoveByRows(rowDelta, editableBeatmap.Rows);

        if (viewport.StartRow == previousStart)
            return;

        rebuildWorkspace();
        setStatus($"Timeline {formatSeconds(viewport.StartMilliseconds(editableBeatmap.StepMilliseconds))}-{formatSeconds(viewport.EndMilliseconds(editableBeatmap.StepMilliseconds))}");
    }

    private void appendRows()
    {
        editableBeatmap.AppendRows(appendStep);
        viewport.MoveByRows(appendStep, editableBeatmap.Rows);
        rebuildWorkspace();
        setStatus($"Extended chart to {editableBeatmap.Rows} rows.");
    }

    private void playtest()
    {
        this.Push(new GameplayScreen(editableBeatmap.ToBeatmap()));
    }

    private void importOsu()
    {
        ISystemFileSelector selector = host.CreateSystemFileSelector([".osu"]);
        selector.Selected += file => Schedule(() => importOsu(file.FullName));
        selector.Present();
    }

    private void importOsu(string path)
    {
        try
        {
            editableBeatmap = OsuManiaBeatmapIO.ReadEditableFromFile(path);
            viewport = new TimelineViewport(0, visibleRows);
            rebuildWorkspace();
            setStatus($"Imported {Path.GetFileName(path)}.");
        }
        catch (Exception ex)
        {
            setStatus($"Import failed: {ex.Message}");
        }
    }

    private void exportOsu()
    {
        try
        {
            string outputPath = getExportPath();
            OsuManiaBeatmapIO.WriteEditableToFile(editableBeatmap, outputPath);
            editableBeatmap.SourcePath = outputPath;
            inspector.Refresh();
            setStatus($"Exported {outputPath}");
        }
        catch (Exception ex)
        {
            setStatus($"Export failed: {ex.Message}");
        }
    }

    private string getExportPath()
    {
        string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string exportDirectory = Path.Combine(documents, "Yokko Exports");
        string fileName = string.Join("_", editableBeatmap.Title.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));

        if (string.IsNullOrWhiteSpace(fileName))
            fileName = "yokko-chart";

        return Path.Combine(exportDirectory, $"{fileName}-{(int)editableBeatmap.KeyMode}K.osu");
    }

    private void setStatus(string message)
    {
        statusText.Text = message;
    }

    private static string formatSeconds(double milliseconds) => $"{milliseconds / 1000:0.00}s";
}
