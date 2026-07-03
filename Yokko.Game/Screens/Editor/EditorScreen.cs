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
    private FillFlowContainer workspace;
    private EditableBeatmap editableBeatmap;
    private EditorGrid grid;
    private EditorInspector inspector;
    private SpriteText statusText;

    [Resolved]
    private GameHost host { get; set; }

    [BackgroundDependencyLoader]
    private void load()
    {
        editableBeatmap = EditableBeatmap.Create(KeyMode.FourKey);

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
                        Height = 540,
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
        rebuildWorkspace();
        setStatus($"New {(int)keyMode}K draft created.");
    }

    private void rebuildWorkspace()
    {
        grid = new EditorGrid(editableBeatmap)
        {
            Anchor = Anchor.TopLeft,
            Origin = Anchor.TopLeft,
        };
        grid.NotesChanged += refreshInspector;

        inspector = new EditorInspector(editableBeatmap)
        {
        };

        workspace.Children = new Drawable[]
        {
            grid,
            inspector,
        };
    }

    private void refreshInspector()
    {
        inspector.Refresh();
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
}
