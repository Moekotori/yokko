using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Framework.Utils;
using osuTK;
using Yokko.Core.Gameplay;
using Yokko.Game.Screens.Editor;

namespace Yokko.Game.Tests.Visual
{
    [TestFixture]
    public partial class TestSceneEditorScreen : YokkoTestScene
    {
        private readonly ScreenStack screenStack;
        private readonly EditorScreen editorScreen;

        public TestSceneEditorScreen()
        {
            Add(screenStack = new ScreenStack(editorScreen = new EditorScreen()) { RelativeSizeAxes = Axes.Both });
        }

        [Test]
        public void TestEditorScreenIsCurrent()
        {
            AddAssert("editor screen is current", () => screenStack.CurrentScreen is EditorScreen);
        }

        [Test]
        public void TestLayoutAnchorsSharedCanvas()
        {
            AddAssert("navy header spans the top", () =>
            {
                EditorHeader header = editorScreen.ChildrenOfType<EditorHeader>().Single();
                return header.Position == Vector2.Zero
                       && header.Size == new Vector2(EditorScreen.CanvasWidth, EditorScreen.HeaderHeight);
            });
            AddAssert("ivory toolbar sits below the header", () =>
            {
                EditorToolbar toolbar = editorScreen.ChildrenOfType<EditorToolbar>().Single();
                return toolbar.Y == EditorScreen.ToolbarTop
                       && toolbar.Size == new Vector2(EditorScreen.CanvasWidth, EditorScreen.ToolbarHeight);
            });
            AddAssert("signal strip anchors the workspace column", () =>
            {
                EditorSignalStrip strip = editorScreen.ChildrenOfType<EditorSignalStrip>().Single();
                return almostEquals(
                           editorScreen.ToLocalSpace(strip.ScreenSpaceDrawQuad.TopLeft),
                           new Vector2(EditorScreen.WorkspaceLeft, EditorScreen.WorkspaceTop))
                       && strip.Size == new Vector2(EditorScreen.WorkspaceWidth, EditorScreen.SignalHeight);
            });
            AddAssert("grid canvas keeps its gutter and bounds", () =>
            {
                EditorGrid grid = editorScreen.ChildrenOfType<EditorGrid>().Single();
                return almostEquals(
                           editorScreen.ToLocalSpace(grid.ScreenSpaceDrawQuad.TopLeft),
                           new Vector2(EditorScreen.WorkspaceLeft, EditorScreen.GridTop))
                       && grid.Size == new Vector2(EditorScreen.WorkspaceWidth, EditorScreen.GridHeight);
            });
            AddAssert("transport row closes the workspace column", () =>
            {
                EditorTimelineControls transport = editorScreen.ChildrenOfType<EditorTimelineControls>().Single();
                return almostEquals(
                           editorScreen.ToLocalSpace(transport.ScreenSpaceDrawQuad.TopLeft),
                           new Vector2(EditorScreen.WorkspaceLeft, EditorScreen.TransportTop))
                       && transport.Size == new Vector2(EditorScreen.WorkspaceWidth, EditorScreen.TransportHeight);
            });
            AddAssert("inspector card fills the right column", () =>
            {
                EditorInspector inspector = editorScreen.ChildrenOfType<EditorInspector>().Single();
                return almostEquals(
                           editorScreen.ToLocalSpace(inspector.ScreenSpaceDrawQuad.TopLeft),
                           new Vector2(EditorScreen.InspectorLeft, EditorScreen.InspectorTop))
                       && inspector.Size == new Vector2(EditorScreen.InspectorWidth, EditorScreen.InspectorHeight);
            });
            AddAssert("status bar hugs the canvas bottom", () =>
            {
                EditorStatusBar statusBar = editorScreen.ChildrenOfType<EditorStatusBar>().Single();
                return statusBar.Y == EditorScreen.StatusBarTop
                       && statusBar.Size == new Vector2(EditorScreen.CanvasWidth, EditorScreen.StatusBarHeight);
            });
        }

        [Test]
        public void TestToolbarSwitchesKeyModes()
        {
            AddStep("create 7K draft", () => editorScreen.CreateChartForTesting(KeyMode.SevenKey));
            AddAssert("beatmap is 7K", () => editorScreen.BeatmapForTesting.LaneCount == 7);
            AddAssert("grid shows seven lanes", () =>
                editorScreen.ChildrenOfType<EditorCell>().Count()
                == 7 * editorScreen.ViewportForTesting.VisibleRows);
            AddStep("create 4K draft", () => editorScreen.CreateChartForTesting(KeyMode.FourKey));
            AddAssert("beatmap is 4K", () => editorScreen.BeatmapForTesting.LaneCount == 4);
            AddAssert("grid shows four lanes", () =>
                editorScreen.ChildrenOfType<EditorCell>().Count()
                == 4 * editorScreen.ViewportForTesting.VisibleRows);
        }

        [Test]
        public void TestGridCellsToggleNotes()
        {
            AddStep("reset draft", () => editorScreen.CreateChartForTesting(KeyMode.FourKey));
            AddAssert("draft starts empty", () => editorScreen.BeatmapForTesting.Notes.Count == 0);
            AddStep("click the first cell", () =>
                editorScreen.ChildrenOfType<EditorCell>().First().TriggerClick());
            AddAssert("note was placed", () => editorScreen.BeatmapForTesting.Notes.Count == 1);
            AddStep("click the same cell again", () =>
                editorScreen.ChildrenOfType<EditorCell>().First().TriggerClick());
            AddAssert("note was removed", () => editorScreen.BeatmapForTesting.Notes.Count == 0);
        }

        [Test]
        public void TestTimelineNavigationZoomAndAppend()
        {
            AddStep("reset draft", () => editorScreen.CreateChartForTesting(KeyMode.FourKey));
            AddAssert("viewport starts at the top", () => editorScreen.ViewportForTesting.StartRow == 0);
            AddStep("scroll forward four rows", () => editorScreen.ScrollRowsForTesting(4));
            AddAssert("viewport moved", () => editorScreen.ViewportForTesting.StartRow == 4);
            AddAssert("scroll updates the status bar", () =>
                editorScreen.StatusTextForTesting.Length > 0);
            AddStep("zoom out four rows", () => editorScreen.ZoomTimelineForTesting(4));
            AddAssert("viewport grew", () => editorScreen.ViewportForTesting.VisibleRows == 28);
            AddStep("append 32 rows", () => editorScreen.AppendRowsForTesting());
            AddAssert("chart extended", () => editorScreen.BeatmapForTesting.Rows == 64);
        }

        [Test]
        public void TestPreviewPlaybackAndSeek()
        {
            AddStep("reset draft", () => editorScreen.CreateChartForTesting(KeyMode.FourKey));
            AddAssert("preview starts stopped", () => !editorScreen.IsPreviewPlayingForTesting);
            AddStep("start preview", () => editorScreen.TogglePreviewForTesting());
            AddAssert("preview is playing", () => editorScreen.IsPreviewPlayingForTesting);
            AddStep("pause preview", () => editorScreen.TogglePreviewForTesting());
            AddAssert("preview is paused", () => !editorScreen.IsPreviewPlayingForTesting);
            AddStep("seek to 750 ms", () => editorScreen.SeekPreviewForTesting(750));
            AddAssert("preview time follows the seek", () =>
                System.Math.Abs(editorScreen.PreviewTimeForTesting - 750) < 0.001);
            AddStep("stop preview", () => editorScreen.StopPreviewForTesting());
            AddAssert("preview rewinds to the start", () =>
                !editorScreen.IsPreviewPlayingForTesting
                && editorScreen.PreviewTimeForTesting == 0);
        }

        [Test]
        public void TestStatusBarReportsActions()
        {
            string previousStatus = string.Empty;

            AddStep("reset draft", () => editorScreen.CreateChartForTesting(KeyMode.FourKey));
            AddStep("remember status", () => previousStatus = editorScreen.StatusTextForTesting);
            AddStep("zoom in", () => editorScreen.ZoomTimelineForTesting(-4));
            AddAssert("status reflects the zoom", () =>
                editorScreen.StatusTextForTesting.Length > 0
                && editorScreen.StatusTextForTesting != previousStatus);
            AddStep("restore zoom", () => editorScreen.ZoomTimelineForTesting(4));
        }

        private static bool almostEquals(Vector2 actual, Vector2 expected) =>
            Precision.AlmostEquals(actual, expected, 0.5f);
    }
}
