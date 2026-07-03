using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Screens;
using osuTK;
using Yokko.Core.Editing;
using Yokko.Core.Gameplay;
using Yokko.Game.Presentation;
using Yokko.Game.Screens.Gameplay;

namespace Yokko.Game.Screens.Editor;

public partial class EditorScreen : Screen
{
    private FillFlowContainer workspace;
    private EditableBeatmap editableBeatmap;
    private EditorGrid grid;
    private EditorInspector inspector;

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
                        playtest),
                    workspace = new FillFlowContainer
                    {
                        AutoSizeAxes = Axes.X,
                        Height = 540,
                        Direction = FillDirection.Horizontal,
                        Spacing = new Vector2(32, 0),
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
}
