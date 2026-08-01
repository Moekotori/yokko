using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;
using Yokko.Game.Localisation;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Screens.Gameplay;

internal enum GameplayLayoutEditorToolWindow
{
    Actions,
    LiveSettings,
    Feedback,
    Inspector,
    LaneCovers,
    Overview,
}

internal partial class GameplayLayoutEditorOverlay
{
    private readonly Dictionary<
        GameplayLayoutEditorToolWindow,
        DraggableToolWindow> toolWindows = new();

    private DraggableToolWindow toolWindowController;

    internal int ToolWindowCountForTest => toolWindows.Count;

    internal int VisibleToolWindowCountForTest =>
        toolWindows.Count(pair => pair.Value.Alpha > 0.9f);

    internal bool ToolWindowControllerVisibleForTest =>
        toolWindowController?.Alpha > 0.9f;

    internal bool IsToolWindowVisibleForTest(
        GameplayLayoutEditorToolWindow kind) =>
        toolWindows.TryGetValue(kind, out DraggableToolWindow window)
        && window.Alpha > 0.9f;

    internal void ToggleToolWindowForTest(
        GameplayLayoutEditorToolWindow kind) =>
        toggleToolWindow(kind);

    internal void MoveToolWindowForTest(
        GameplayLayoutEditorToolWindow kind,
        Vector2 delta)
    {
        if (toolWindows.TryGetValue(kind, out DraggableToolWindow window))
            window.MoveByForTest(delta);
    }

    internal bool IsToolWindowInsideViewportForTest(
        GameplayLayoutEditorToolWindow kind) =>
        toolWindows.TryGetValue(kind, out DraggableToolWindow window)
        && window.IsInsideParentForTest;

    internal void MoveToolWindowControllerForTest(Vector2 delta) =>
        toolWindowController?.MoveByForTest(delta);

    internal bool IsToolWindowControllerInsideViewportForTest =>
        toolWindowController?.IsInsideParentForTest == true;

    internal void ResetToolWindowPositionsForTest()
    {
        foreach (DraggableToolWindow window in toolWindows.Values)
            window.ResetPositionForTest();
        toolWindowController?.ResetPositionForTest();
    }

    private Drawable createToolWindow(
        GameplayLayoutEditorToolWindow kind,
        Drawable content)
    {
        var window = new DraggableToolWindow(content);
        toolWindows.Add(kind, window);
        return window;
    }

    private Drawable createToolWindowController()
    {
        var panel = new ToolWindowControllerPanel(
            isToolWindowVisible,
            toggleToolWindow)
        {
            Anchor = Anchor.TopRight,
            Origin = Anchor.TopRight,
            Position = new Vector2(-18, 654),
            Scale = new Vector2(1.08f),
            Size = new Vector2(360, 142),
            Depth = -140,
        };

        return toolWindowController = new DraggableToolWindow(panel);
    }

    private bool isToolWindowVisible(
        GameplayLayoutEditorToolWindow kind) =>
        toolWindows.TryGetValue(kind, out DraggableToolWindow window)
        && window.Alpha > 0.01f;

    private void toggleToolWindow(GameplayLayoutEditorToolWindow kind)
    {
        if (!toolWindows.TryGetValue(kind, out DraggableToolWindow window))
            return;

        window.ClearTransforms();
        window.FadeTo(window.Alpha > 0.01f ? 0 : 1, 100, Easing.OutQuint);
    }

    private sealed partial class DraggableToolWindow : CompositeDrawable
    {
        private const float title_drag_height = 32;

        private Vector2 dragStartPointer;
        private Vector2 dragStartPosition;
        private Vector2 initialPosition;
        private Vector2 lastParentDrawSize;
        private bool dragArmed;
        private bool dragging;
        private bool normalised;

        internal bool IsInsideParentForTest
        {
            get
            {
                if (Parent == null)
                    return false;

                Vector2 renderedSize = Size * Scale;
                return Position.X >= -0.01f
                       && Position.Y >= -0.01f
                       && Position.X + renderedSize.X
                       <= Parent.DrawWidth + 0.01f
                       && Position.Y + renderedSize.Y
                       <= Parent.DrawHeight + 0.01f;
            }
        }

        internal DraggableToolWindow(Drawable content)
        {
            ArgumentNullException.ThrowIfNull(content);

            Anchor = content.Anchor;
            Origin = content.Origin;
            Position = content.Position;
            Size = content.Size;
            Scale = content.Scale;
            Depth = content.Depth;

            content.Anchor = Anchor.TopLeft;
            content.Origin = Anchor.TopLeft;
            content.Position = Vector2.Zero;
            content.Scale = Vector2.One;
            content.Depth = 0;
            InternalChild = content;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            normaliseToTopLeft();
        }

        protected override void Update()
        {
            base.Update();

            if (!normalised || dragging || Parent == null)
                return;

            if (lastParentDrawSize != Parent.DrawSize)
            {
                lastParentDrawSize = Parent.DrawSize;
                Position = clampPosition(Position);
            }
        }

        protected override bool OnMouseDown(MouseDownEvent e)
        {
            if (e.Button != MouseButton.Left || Parent == null)
                return false;

            Vector2 local = ToLocalSpace(e.ScreenSpaceMousePosition);
            if (local.Y < 0 || local.Y > title_drag_height)
                return false;

            dragArmed = true;
            dragStartPointer = Parent.ToLocalSpace(
                e.ScreenSpaceMousePosition);
            dragStartPosition = Position;
            return true;
        }

        protected override bool OnDragStart(DragStartEvent e)
        {
            dragging = dragArmed && Parent != null;
            return dragging;
        }

        protected override void OnDrag(DragEvent e)
        {
            if (!dragging || Parent == null)
                return;

            Vector2 pointer = Parent.ToLocalSpace(
                e.ScreenSpaceMousePosition);
            Position = clampPosition(
                dragStartPosition + pointer - dragStartPointer);
        }

        protected override void OnDragEnd(DragEndEvent e)
        {
            dragArmed = false;
            dragging = false;
            base.OnDragEnd(e);
        }

        protected override void OnMouseUp(MouseUpEvent e)
        {
            dragArmed = false;
            base.OnMouseUp(e);
        }

        internal void MoveByForTest(Vector2 delta)
        {
            normaliseToTopLeft();
            Position = clampPosition(Position + delta);
        }

        internal void ResetPositionForTest()
        {
            normaliseToTopLeft();
            Position = clampPosition(initialPosition);
        }

        private void normaliseToTopLeft()
        {
            if (normalised || Parent == null)
                return;

            Vector2 topLeft = Parent.ToLocalSpace(
                ToScreenSpace(Vector2.Zero));
            Anchor = Anchor.TopLeft;
            Origin = Anchor.TopLeft;
            Position = clampPosition(topLeft);
            initialPosition = Position;
            lastParentDrawSize = Parent.DrawSize;
            normalised = true;
        }

        private Vector2 clampPosition(Vector2 position)
        {
            if (Parent == null)
                return position;

            Vector2 renderedSize = Size * Scale;
            return new Vector2(
                Math.Clamp(
                    position.X,
                    0,
                    Math.Max(0, Parent.DrawWidth - renderedSize.X)),
                Math.Clamp(
                    position.Y,
                    0,
                    Math.Max(0, Parent.DrawHeight - renderedSize.Y)));
        }
    }

    private sealed partial class ToolWindowControllerPanel : CompositeDrawable
    {
        private readonly Dictionary<
            GameplayLayoutEditorToolWindow,
            CompactTextButton> buttons = new();
        private readonly Func<GameplayLayoutEditorToolWindow, bool> isVisible;

        internal ToolWindowControllerPanel(
            Func<GameplayLayoutEditorToolWindow, bool> isVisible,
            Action<GameplayLayoutEditorToolWindow> toggle)
        {
            this.isVisible = isVisible;
            Masking = true;
            CornerRadius = 11;
            BorderThickness = 1.25f;
            BorderColour = new Color4(
                HomeControlColours.Navy.R,
                HomeControlColours.Navy.G,
                HomeControlColours.Navy.B,
                0.72f);

            var buttonFlow = new FillFlowContainer
            {
                Position = new Vector2(12, 43),
                Width = 336,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Full,
                Spacing = new Vector2(8, 6),
            };

            foreach (GameplayLayoutEditorToolWindow kind
                     in Enum.GetValues<GameplayLayoutEditorToolWindow>())
            {
                GameplayLayoutEditorToolWindow captured = kind;
                var button = new CompactTextButton(
                    YokkoStrings.Get(toolWindowLabelKey(kind)),
                    () => toggle(captured))
                {
                    Size = new Vector2(164, 27),
                };
                buttons.Add(kind, button);
                buttonFlow.Add(button);
            }

            InternalChildren = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = HomeControlColours.Ivory,
                },
                new Box
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 4,
                    Colour = HomeControlColours.Cyan,
                },
                new SpriteText
                {
                    Position = new Vector2(12, 8),
                    Text = YokkoStrings.Get(
                        "gameplay.layout_editor.window_controls"),
                    Font = LayoutEditorTypography.Bold(12),
                    Colour = HomeControlColours.Navy,
                },
                new SpriteText
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                    Position = new Vector2(-12, 10),
                    Text = YokkoStrings.Get(
                        "gameplay.layout_editor.drag_title_hint"),
                    Font = LayoutEditorTypography.Regular(8),
                    Colour = new Color4(
                        HomeControlColours.Navy.R,
                        HomeControlColours.Navy.G,
                        HomeControlColours.Navy.B,
                        0.58f),
                },
                buttonFlow,
            };
        }

        protected override void Update()
        {
            base.Update();

            foreach ((GameplayLayoutEditorToolWindow kind,
                      CompactTextButton button) in buttons)
            {
                button.SetSelected(isVisible(kind));
            }
        }

        private static string toolWindowLabelKey(
            GameplayLayoutEditorToolWindow kind) => kind switch
            {
                GameplayLayoutEditorToolWindow.Actions =>
                    "gameplay.layout_editor.window_actions",
                GameplayLayoutEditorToolWindow.LiveSettings =>
                    "gameplay.layout_editor.live_settings",
                GameplayLayoutEditorToolWindow.Feedback =>
                    "gameplay.layout_editor.feedback_settings",
                GameplayLayoutEditorToolWindow.Inspector =>
                    "gameplay.layout_editor.inspector",
                GameplayLayoutEditorToolWindow.LaneCovers =>
                    "gameplay.layout_editor.covers",
                GameplayLayoutEditorToolWindow.Overview =>
                    "gameplay.layout_editor.preview",
                _ => "gameplay.layout_editor.window_actions",
            };
    }
}
