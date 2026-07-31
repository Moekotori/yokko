using System;
using System.Collections.Generic;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;
using Yokko.Game.Gameplay;
using Yokko.Game.Localisation;
using Yokko.Game.Presentation;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Screens.Gameplay;

/// <summary>
/// A deliberately small direct-manipulation layer. Gameplay remains the main
/// canvas; this overlay only adds drag targets, cover handles and a full-page
/// overview while the song is paused.
/// </summary>
internal partial class GameplayLayoutEditorOverlay : CompositeDrawable
{
    private const float overviewWidth = 300;
    private const float overviewHeight = 168.75f;
    private const float overviewPadding = 10;

    private readonly GameplayPlayfield playfield;
    private readonly GameplayHud hud;
    private readonly GameplayTimingBar timingBar;
    private readonly YokkoGameplaySettings settings;
    private readonly Action save;
    private readonly Action close;
    private readonly LayoutTransformTarget playfieldTarget;
    private readonly LayoutTransformTarget hudTarget;
    private readonly LayoutTransformTarget timingBarTarget;
    private readonly CoverDragHandle topCoverHandle;
    private readonly CoverDragHandle bottomCoverHandle;
    private readonly Stack<LayoutSnapshot> undoHistory = new();
    private readonly Stack<LayoutSnapshot> redoHistory = new();
    private Container overviewContent;
    private Container miniPlayfield;
    private Container miniHud;
    private Container miniTimingBar;
    private Box miniTopCover;
    private Box miniBottomCover;
    private LayoutActionButton undoButton;
    private LayoutActionButton redoButton;
    private LayoutTransformTarget selectedTarget;
    private LayoutSnapshot sessionStart;

    internal bool IsEditing { get; private set; }

    internal float OverviewAspectRatio =>
        overviewContent.Width / overviewContent.Height;

    internal int TransformTargetCount => 3;

    internal int ResizeHandleCount =>
        playfieldTarget.ResizeHandleCount
        + hudTarget.ResizeHandleCount
        + timingBarTarget.ResizeHandleCount;

    internal void MoveTimingBarForTest(Vector2 delta) =>
        moveTimingBar(delta);

    internal void ResizeTimingBarForTest(Vector2 delta) =>
        resizeTimingBar(
            ResizeEdges.Right | ResizeEdges.Bottom,
            delta);

    internal bool NudgeTimingBarForTest(Key key, bool accelerated) =>
        timingBarTarget.TryNudge(key, accelerated ? 10 : 1);

    internal bool ResizeTimingBarWithKeyboardForTest(
        Key key,
        bool accelerated) =>
        timingBarTarget.TryResize(key, accelerated ? 10 : 1);

    internal string SelectedElementForTest =>
        selectedTarget?.Kind.ToString();

    internal void SelectNextElementForTest(bool backwards) =>
        selectAdjacentTarget(backwards);

    public GameplayLayoutEditorOverlay(
        GameplayPlayfield playfield,
        GameplayHud hud,
        GameplayTimingBar timingBar,
        YokkoGameplaySettings settings,
        Action save,
        Action close)
    {
        this.playfield = playfield;
        this.hud = hud;
        this.timingBar = timingBar;
        this.settings = settings;
        this.save = save;
        this.close = close;

        RelativeSizeAxes = Axes.Both;
        Depth = -2000;
        Alpha = 0;

        InternalChildren = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = new Color4(
                    HomeControlColours.Navy.R,
                    HomeControlColours.Navy.G,
                    HomeControlColours.Navy.B,
                    0.055f),
            },
            verticalSnapGuide = createSnapGuide(true),
            horizontalSnapGuide = createSnapGuide(false),
            createTopBar(),
            playfieldTarget = new LayoutTransformTarget(
                this,
                LayoutElementKind.Playfield,
                YokkoStrings.Get("gameplay.layout_editor.playfield"),
                movePlayfield,
                resizePlayfield,
                resetPlayfield,
                beginChange,
                selectTarget,
                snapTargetMove,
                clearSnapGuides,
                resizePlayfieldWithWheel),
            hudTarget = new LayoutTransformTarget(
                this,
                LayoutElementKind.Hud,
                YokkoStrings.Get("gameplay.layout_editor.hud"),
                moveHud,
                resizeHud,
                resetHud,
                beginChange,
                selectTarget,
                snapTargetMove,
                clearSnapGuides),
            timingBarTarget = new LayoutTransformTarget(
                this,
                LayoutElementKind.TimingBar,
                YokkoStrings.Get("gameplay.layout_editor.timing_bar"),
                moveTimingBar,
                resizeTimingBar,
                resetTimingBar,
                beginChange,
                selectTarget,
                snapTargetMove,
                clearSnapGuides),
            topCoverHandle = new CoverDragHandle(
                this,
                "TOP COVER",
                updateTopCover,
                beginChange),
            bottomCoverHandle = new CoverDragHandle(
                this,
                "BOTTOM COVER",
                updateBottomCover,
                beginChange),
            createOverviewCard(),
            createInspectorCard(),
            createCoverPanel(),
        };
    }

    internal void SetEditing(bool editing)
    {
        IsEditing = editing;
        ClearTransforms();

        if (editing)
        {
            sessionStart = captureLayout();
            undoHistory.Clear();
            redoHistory.Clear();
            beginEditorSession();
            selectTarget(playfieldTarget);
            updateHistoryButtons();
            this.FadeTo(1, 100, Easing.OutQuint);
        }
        else
        {
            endEditorSession();
            selectTarget(null);
            this.FadeTo(0, 100, Easing.OutQuint);
        }
    }

    internal void SaveAndClose()
    {
        save();
        close();
    }

    internal void CancelAndClose()
    {
        applyLayout(sessionStart);
        close();
    }

    internal void ResetAll()
    {
        beginChange();
        settings.ResetGameplayLayout();
    }

    protected override bool OnKeyDown(KeyDownEvent e)
    {
        if (!IsEditing)
            return false;

        if (!e.Repeat
            && e.ControlPressed
            && e.Key == Key.Z)
        {
            if (e.ShiftPressed)
                redo();
            else
                undo();

            return true;
        }

        if (!e.Repeat
            && e.ControlPressed
            && e.Key == Key.Y)
        {
            redo();
            return true;
        }

        if (!e.Repeat && e.Key == Key.Escape)
        {
            CancelAndClose();
            return true;
        }

        if (!e.Repeat
            && e.Key is Key.Enter or Key.KeypadEnter)
        {
            SaveAndClose();
            return true;
        }

        if (!e.Repeat && e.Key == Key.Tab)
        {
            selectAdjacentTarget(e.ShiftPressed);
            return true;
        }

        LayoutTransformTarget target = hoveredTarget() ?? selectedTarget;

        if (!e.Repeat
            && e.Key == Key.Delete
            && target != null)
        {
            selectTarget(target);
            if (!target.CanEdit)
                return true;

            beginChange();
            target.Reset();
            return true;
        }

        if (e.Key is not (
                Key.Left
                or Key.Right
                or Key.Up
                or Key.Down)
            || target == null)
        {
            return false;
        }

        selectTarget(target);
        if (!target.CanEdit)
            return true;

        beginChange();
        float distance = e.ShiftPressed ? 10 : nudgeStep;
        return e.ControlPressed
            ? target.TryResize(e.Key, distance)
            : target.TryNudge(e.Key, distance);
    }

    protected override bool OnMouseDown(MouseDownEvent e)
    {
        if (e.Button != MouseButton.Left)
            return false;

        return true;
    }

    protected override void Update()
    {
        base.Update();

        if (!IsEditing
            || DrawWidth <= 0
            || DrawHeight <= 0)
        {
            return;
        }

        (Vector2 playfieldTopLeft, Vector2 playfieldBottomRight) =
            GameplayLayoutGeometry.BoundsIn(this, playfield);
        (Vector2 hudTopLeft, Vector2 hudBottomRight) =
            GameplayLayoutGeometry.BoundsIn(this, hud);
        (Vector2 timingBarTopLeft, Vector2 timingBarBottomRight) =
            GameplayLayoutGeometry.BoundsIn(this, timingBar);

        setBounds(
            playfieldTarget,
            playfieldTopLeft,
            playfieldBottomRight);
        setBounds(hudTarget, hudTopLeft, hudBottomRight);
        setBounds(
            timingBarTarget,
            timingBarTopLeft,
            timingBarBottomRight);

        float playfieldHeight = Math.Max(
            1,
            playfieldBottomRight.Y - playfieldTopLeft.Y);
        float topBoundary = playfieldTopLeft.Y
                            + playfieldHeight * (float)Math.Clamp(
                                settings.LayoutTopCoverRatio.Value,
                                0,
                                YokkoGameplaySettings.MaximumTopCoverRatio);
        float bottomBoundary = playfieldBottomRight.Y
                               - playfieldHeight * (float)Math.Clamp(
                                   settings.LayoutBottomCoverRatio.Value,
                                   0,
                                   YokkoGameplaySettings.MaximumBottomCoverRatio);

        setHandleBounds(
            topCoverHandle,
            playfieldTopLeft.X,
            topBoundary,
            playfieldBottomRight.X - playfieldTopLeft.X);
        setHandleBounds(
            bottomCoverHandle,
            playfieldTopLeft.X,
            bottomBoundary,
            playfieldBottomRight.X - playfieldTopLeft.X);

        updateOverview(
            playfieldTopLeft,
            playfieldBottomRight,
            hudTopLeft,
            hudBottomRight,
            timingBarTopLeft,
            timingBarBottomRight);
        refreshInspector();
    }

    private Drawable createTopBar() =>
        new Container
        {
            Position = new Vector2(16, 14),
            Size = new Vector2(764, 68),
            Depth = -100,
            Children = new Drawable[]
            {
                new Container
                {
                    Position = new Vector2(4, 4),
                    Size = new Vector2(760, 64),
                    Masking = true,
                    CornerRadius = 8,
                    Child = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = new Color4(
                            HomeControlColours.Cyan.R,
                            HomeControlColours.Cyan.G,
                            HomeControlColours.Cyan.B,
                            0.48f),
                    },
                },
                new Container
                {
                    Size = new Vector2(760, 64),
                    Masking = true,
                    CornerRadius = 8,
                    BorderThickness = 1.5f,
                    BorderColour = HomeControlColours.Navy,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = HomeControlColours.Ivory,
                        },
                        new Box
                        {
                            RelativeSizeAxes = Axes.Y,
                            Width = 5,
                            Colour = HomeControlColours.Cyan,
                        },
                        new FillFlowContainer
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            X = 16,
                            AutoSizeAxes = Axes.Both,
                            Direction = FillDirection.Vertical,
                            Spacing = new Vector2(0, -2),
                            Children = new Drawable[]
                            {
                                new SpriteText
                                {
                                    Text = YokkoStrings.Get(
                                        "gameplay.layout_editor.title"),
                                    Font = LayoutEditorTypography.Bold(14),
                                    Colour = HomeControlColours.Navy,
                                },
                                new SpriteText
                                {
                                    Text = YokkoStrings.Get(
                                        "gameplay.layout_editor.hint"),
                                    Font = LayoutEditorTypography.Regular(9),
                                    Colour = new Color4(
                                        HomeControlColours.Navy.R,
                                        HomeControlColours.Navy.G,
                                        HomeControlColours.Navy.B,
                                        0.64f),
                                },
                            },
                        },
                        new FillFlowContainer
                        {
                            Anchor = Anchor.CentreRight,
                            Origin = Anchor.CentreRight,
                            X = -10,
                            AutoSizeAxes = Axes.Both,
                            Direction = FillDirection.Horizontal,
                            Spacing = new Vector2(6, 0),
                            Children = new Drawable[]
                            {
                                undoButton = new LayoutActionButton(
                                    YokkoStrings.Get(
                                        "gameplay.layout_editor.undo"),
                                    FontAwesome.Solid.Undo,
                                    undo)
                                {
                                    Width = 68,
                                },
                                redoButton = new LayoutActionButton(
                                    YokkoStrings.Get(
                                        "gameplay.layout_editor.redo"),
                                    FontAwesome.Solid.Redo,
                                    redo)
                                {
                                    Width = 68,
                                },
                                new LayoutActionButton(
                                    YokkoStrings.Get(
                                        "gameplay.layout_editor.reset"),
                                    FontAwesome.Solid.Trash,
                                    reset)
                                {
                                    Width = 84,
                                },
                                new LayoutActionButton(
                                    YokkoStrings.Get(
                                        "gameplay.layout_editor.cancel"),
                                    FontAwesome.Solid.Times,
                                    CancelAndClose)
                                {
                                    Width = 84,
                                },
                                new LayoutActionButton(
                                    YokkoStrings.Get(
                                        "gameplay.layout_editor.save"),
                                    FontAwesome.Solid.Check,
                                    SaveAndClose,
                                    true)
                                {
                                    Width = 116,
                                },
                            },
                        },
                    },
                },
                new Box
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.Centre,
                    Position = new Vector2(-6, 3),
                    Size = new Vector2(11),
                    Rotation = 45,
                    Colour = HomeControlColours.Yellow,
                },
            },
        };

    private Drawable createOverviewCard()
    {
        Vector2 cardSize = new(
            overviewWidth + overviewPadding * 2,
            overviewHeight + 42);

        return new Container
        {
            Anchor = Anchor.BottomRight,
            Origin = Anchor.BottomRight,
            Position = new Vector2(-18, -18),
            Size = cardSize + new Vector2(5),
            Children = new Drawable[]
            {
                new Container
                {
                    Position = new Vector2(5),
                    Size = cardSize,
                    Masking = true,
                    CornerRadius = 8,
                    Child = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = new Color4(
                            HomeControlColours.Cyan.R,
                            HomeControlColours.Cyan.G,
                            HomeControlColours.Cyan.B,
                            0.45f),
                    },
                },
                new Container
                {
                    Size = cardSize,
                    Masking = true,
                    CornerRadius = 8,
                    BorderThickness = 1.5f,
                    BorderColour = HomeControlColours.Navy,
                    Children = new Drawable[]
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
                            Position = new Vector2(overviewPadding, 8),
                            Text = YokkoStrings.Get(
                                "gameplay.layout_editor.preview"),
                            Font = LayoutEditorTypography.Bold(11),
                            Colour = HomeControlColours.Navy,
                        },
                        overviewContent = new Container
                        {
                            Position = new Vector2(overviewPadding, 32),
                            Size = new Vector2(overviewWidth, overviewHeight),
                            Masking = true,
                            CornerRadius = 4,
                            BorderThickness = 1.5f,
                            BorderColour = HomeControlColours.Navy,
                            Children = new Drawable[]
                            {
                                new Box
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Colour = YokkoPalette.Background,
                                },
                                miniPlayfield = createMiniPlayfield(),
                                miniHud = new Container
                                {
                                    Masking = true,
                                    BorderThickness = 1,
                                    BorderColour = HomeControlColours.Cyan,
                                    Child = new Box
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        Colour = new Color4(
                                            HomeControlColours.PaleCyan.R,
                                            HomeControlColours.PaleCyan.G,
                                            HomeControlColours.PaleCyan.B,
                                            0.18f),
                                    },
                                },
                                miniTimingBar = new Container
                                {
                                    Masking = true,
                                    BorderThickness = 1,
                                    BorderColour = HomeControlColours.Yellow,
                                    Child = new Box
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        Colour = new Color4(
                                            HomeControlColours.Yellow.R,
                                            HomeControlColours.Yellow.G,
                                            HomeControlColours.Yellow.B,
                                            0.68f),
                                    },
                                },
                                miniTopCover = createMiniCover(),
                                miniBottomCover = createMiniCover(),
                            },
                        },
                    },
                },
                new Box
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.Centre,
                    Position = new Vector2(-7, 3),
                    Size = new Vector2(10),
                    Rotation = 45,
                    Colour = HomeControlColours.Yellow,
                },
            },
        };
    }

    private LayoutTransformTarget hoveredTarget()
    {
        if (timingBarTarget.IsHovered)
            return timingBarTarget;

        if (hudTarget.IsHovered)
            return hudTarget;

        return playfieldTarget.IsHovered
            ? playfieldTarget
            : null;
    }

    private void selectTarget(LayoutTransformTarget target)
    {
        if (selectedTarget == target)
            return;

        selectedTarget?.SetSelected(false);
        selectedTarget = target;
        selectedTarget?.SetSelected(true);
        inspector?.Select(target?.Kind);
    }

    private void selectAdjacentTarget(bool backwards)
    {
        LayoutTransformTarget[] targets =
        [
            playfieldTarget,
            hudTarget,
            timingBarTarget,
        ];

        if (selectedTarget == null)
        {
            selectTarget(backwards ? targets[^1] : targets[0]);
            return;
        }

        int current = Array.IndexOf(targets, selectedTarget);
        int direction = backwards ? -1 : 1;
        int next = (current + direction + targets.Length)
                   % targets.Length;
        selectTarget(targets[next]);
    }

    private void beginChange()
    {
        LayoutSnapshot current = captureLayout();
        if (undoHistory.Count == 0 || undoHistory.Peek() != current)
            undoHistory.Push(current);

        redoHistory.Clear();
        updateHistoryButtons();
    }

    private void undo()
    {
        if (undoHistory.Count == 0)
            return;

        redoHistory.Push(captureLayout());
        applyLayout(undoHistory.Pop());
        updateHistoryButtons();
    }

    private void redo()
    {
        if (redoHistory.Count == 0)
            return;

        undoHistory.Push(captureLayout());
        applyLayout(redoHistory.Pop());
        updateHistoryButtons();
    }

    private void updateHistoryButtons()
    {
        undoButton?.SetAvailable(undoHistory.Count > 0);
        redoButton?.SetAvailable(redoHistory.Count > 0);
    }

    private LayoutSnapshot captureLayout() => new(
        settings.LayoutPlayfieldOffsetX.Value,
        settings.LayoutPlayfieldOffsetY.Value,
        settings.LayoutHudOffsetX.Value,
        settings.LayoutHudOffsetY.Value,
        settings.LayoutPlayfieldWidthScale.Value,
        settings.LayoutPlayfieldHeightScale.Value,
        settings.LayoutHudScaleX.Value,
        settings.LayoutHudScaleY.Value,
        settings.LayoutTimingBarOffsetX.Value,
        settings.LayoutTimingBarOffsetY.Value,
        settings.LayoutTimingBarScaleX.Value,
        settings.LayoutTimingBarScaleY.Value,
        settings.LayoutTopCoverRatio.Value,
        settings.LayoutBottomCoverRatio.Value);

    private void applyLayout(LayoutSnapshot snapshot)
    {
        settings.LayoutPlayfieldOffsetX.Value =
            snapshot.PlayfieldOffsetX;
        settings.LayoutPlayfieldOffsetY.Value =
            snapshot.PlayfieldOffsetY;
        settings.LayoutHudOffsetX.Value = snapshot.HudOffsetX;
        settings.LayoutHudOffsetY.Value = snapshot.HudOffsetY;
        settings.LayoutPlayfieldWidthScale.Value =
            snapshot.PlayfieldWidthScale;
        settings.LayoutPlayfieldHeightScale.Value =
            snapshot.PlayfieldHeightScale;
        settings.LayoutHudScaleX.Value = snapshot.HudScaleX;
        settings.LayoutHudScaleY.Value = snapshot.HudScaleY;
        settings.LayoutTimingBarOffsetX.Value =
            snapshot.TimingBarOffsetX;
        settings.LayoutTimingBarOffsetY.Value =
            snapshot.TimingBarOffsetY;
        settings.LayoutTimingBarScaleX.Value =
            snapshot.TimingBarScaleX;
        settings.LayoutTimingBarScaleY.Value =
            snapshot.TimingBarScaleY;
        settings.LayoutTopCoverRatio.Value = snapshot.TopCoverRatio;
        settings.LayoutBottomCoverRatio.Value =
            snapshot.BottomCoverRatio;
    }

    private void movePlayfield(Vector2 delta)
    {
        settings.LayoutPlayfieldOffsetX.Value = clampOffset(
            settings.LayoutPlayfieldOffsetX.Value
            + delta.X / Math.Max(1, DrawWidth));
        settings.LayoutPlayfieldOffsetY.Value = clampOffset(
            settings.LayoutPlayfieldOffsetY.Value
            + delta.Y / Math.Max(1, DrawHeight));
    }

    private void moveHud(Vector2 delta)
    {
        settings.LayoutHudOffsetX.Value = clampOffset(
            settings.LayoutHudOffsetX.Value
            + delta.X / Math.Max(1, DrawWidth));
        settings.LayoutHudOffsetY.Value = clampOffset(
            settings.LayoutHudOffsetY.Value
            + delta.Y / Math.Max(1, DrawHeight));
    }

    private void moveTimingBar(Vector2 delta)
    {
        settings.LayoutTimingBarOffsetX.Value = clampOffset(
            settings.LayoutTimingBarOffsetX.Value
            + delta.X / Math.Max(1, DrawWidth));
        settings.LayoutTimingBarOffsetY.Value = clampOffset(
            settings.LayoutTimingBarOffsetY.Value
            + delta.Y / Math.Max(1, DrawHeight));
    }

    private void resetPlayfield()
    {
        settings.LayoutPlayfieldOffsetX.SetDefault();
        settings.LayoutPlayfieldOffsetY.SetDefault();
        settings.LayoutPlayfieldWidthScale.SetDefault();
        settings.LayoutPlayfieldHeightScale.SetDefault();
        settings.LayoutTopCoverRatio.SetDefault();
        settings.LayoutBottomCoverRatio.SetDefault();
    }

    private void resetHud()
    {
        settings.LayoutHudOffsetX.SetDefault();
        settings.LayoutHudOffsetY.SetDefault();
        settings.LayoutHudScaleX.SetDefault();
        settings.LayoutHudScaleY.SetDefault();
    }

    private void resetTimingBar()
    {
        settings.LayoutTimingBarOffsetX.SetDefault();
        settings.LayoutTimingBarOffsetY.SetDefault();
        settings.LayoutTimingBarScaleX.SetDefault();
        settings.LayoutTimingBarScaleY.SetDefault();
    }

    private void resizePlayfield(
        ResizeEdges edges,
        Vector2 delta)
    {
        (Vector2 topLeft, Vector2 bottomRight) =
            GameplayLayoutGeometry.BoundsIn(this, playfield);
        float width = Math.Max(1, bottomRight.X - topLeft.X);
        float height = Math.Max(1, bottomRight.Y - topLeft.Y);

        if (hasHorizontalEdge(edges))
        {
            bool fromRight = edges.HasFlag(ResizeEdges.Right);
            float realisedChange = resizeDimension(
                settings.LayoutPlayfieldWidthScale.Value,
                width,
                (fromRight ? 1 : -1) * delta.X,
                YokkoGameplaySettings.MinimumPlayfieldWidthScale,
                YokkoGameplaySettings.MaximumPlayfieldWidthScale,
                value => settings.LayoutPlayfieldWidthScale.Value = value);
            settings.LayoutPlayfieldOffsetX.Value = clampOffset(
                settings.LayoutPlayfieldOffsetX.Value
                + (fromRight ? realisedChange : -realisedChange)
                / 2
                / Math.Max(1, DrawWidth));
        }

        if (hasVerticalEdge(edges))
        {
            bool fromBottom = edges.HasFlag(ResizeEdges.Bottom);
            float realisedChange = resizeDimension(
                settings.LayoutPlayfieldHeightScale.Value,
                height,
                (fromBottom ? 1 : -1) * delta.Y,
                YokkoGameplaySettings.MinimumLayoutScale,
                YokkoGameplaySettings.MaximumLayoutScale,
                value => settings.LayoutPlayfieldHeightScale.Value = value);
            if (fromBottom)
            {
                settings.LayoutPlayfieldOffsetY.Value = clampOffset(
                    settings.LayoutPlayfieldOffsetY.Value
                    + realisedChange / Math.Max(1, DrawHeight));
            }
        }
    }

    private void resizeHud(
        ResizeEdges edges,
        Vector2 delta)
    {
        (Vector2 topLeft, Vector2 bottomRight) =
            GameplayLayoutGeometry.BoundsIn(this, hud);
        float width = Math.Max(1, bottomRight.X - topLeft.X);
        float height = Math.Max(1, bottomRight.Y - topLeft.Y);

        if (hasHorizontalEdge(edges))
        {
            bool fromRight = edges.HasFlag(ResizeEdges.Right);
            float realisedChange = resizeDimension(
                settings.LayoutHudScaleX.Value,
                width,
                (fromRight ? 1 : -1) * delta.X,
                YokkoGameplaySettings.MinimumLayoutScale,
                YokkoGameplaySettings.MaximumLayoutScale,
                value => settings.LayoutHudScaleX.Value = value);
            if (fromRight)
            {
                settings.LayoutHudOffsetX.Value = clampOffset(
                    settings.LayoutHudOffsetX.Value
                    + realisedChange / Math.Max(1, DrawWidth));
            }
        }

        if (hasVerticalEdge(edges))
        {
            bool fromBottom = edges.HasFlag(ResizeEdges.Bottom);
            float realisedChange = resizeDimension(
                settings.LayoutHudScaleY.Value,
                height,
                (fromBottom ? 1 : -1) * delta.Y,
                YokkoGameplaySettings.MinimumLayoutScale,
                YokkoGameplaySettings.MaximumLayoutScale,
                value => settings.LayoutHudScaleY.Value = value);
            if (!fromBottom)
            {
                settings.LayoutHudOffsetY.Value = clampOffset(
                    settings.LayoutHudOffsetY.Value
                    - realisedChange / Math.Max(1, DrawHeight));
            }
        }
    }

    private void resizeTimingBar(
        ResizeEdges edges,
        Vector2 delta)
    {
        (Vector2 topLeft, Vector2 bottomRight) =
            GameplayLayoutGeometry.BoundsIn(this, timingBar);
        float width = Math.Max(1, bottomRight.X - topLeft.X);
        float height = Math.Max(1, bottomRight.Y - topLeft.Y);

        if (hasHorizontalEdge(edges))
        {
            bool fromRight = edges.HasFlag(ResizeEdges.Right);
            float realisedChange = resizeDimension(
                settings.LayoutTimingBarScaleX.Value,
                width,
                (fromRight ? 1 : -1) * delta.X,
                YokkoGameplaySettings.MinimumLayoutScale,
                YokkoGameplaySettings.MaximumLayoutScale,
                value => settings.LayoutTimingBarScaleX.Value = value);
            settings.LayoutTimingBarOffsetX.Value = clampOffset(
                settings.LayoutTimingBarOffsetX.Value
                + (fromRight ? realisedChange : -realisedChange)
                / 2
                / Math.Max(1, DrawWidth));
        }

        if (hasVerticalEdge(edges))
        {
            bool fromBottom = edges.HasFlag(ResizeEdges.Bottom);
            float realisedChange = resizeDimension(
                settings.LayoutTimingBarScaleY.Value,
                height,
                (fromBottom ? 1 : -1) * delta.Y,
                YokkoGameplaySettings.MinimumLayoutScale,
                YokkoGameplaySettings.MaximumLayoutScale,
                value => settings.LayoutTimingBarScaleY.Value = value);
            if (fromBottom)
            {
                settings.LayoutTimingBarOffsetY.Value = clampOffset(
                    settings.LayoutTimingBarOffsetY.Value
                    + realisedChange / Math.Max(1, DrawHeight));
            }
        }
    }

    private void resizePlayfieldWithWheel(float direction)
    {
        settings.LayoutPlayfieldWidthScale.Value = Math.Clamp(
            settings.LayoutPlayfieldWidthScale.Value
            + Math.Sign(direction) * 0.05,
            YokkoGameplaySettings.MinimumPlayfieldWidthScale,
            YokkoGameplaySettings.MaximumPlayfieldWidthScale);
    }

    private static float resizeDimension(
        double currentScale,
        float currentSize,
        float requestedSizeChange,
        double minimumScale,
        double maximumScale,
        Action<double> apply)
    {
        double nextScale = Math.Clamp(
            currentScale
            * Math.Max(8, currentSize + requestedSizeChange)
            / Math.Max(1, currentSize),
            minimumScale,
            maximumScale);
        apply(nextScale);
        return currentSize
               * ((float)(nextScale / Math.Max(0.0001, currentScale)) - 1);
    }

    private static bool hasHorizontalEdge(ResizeEdges edges) =>
        edges.HasFlag(ResizeEdges.Left)
        || edges.HasFlag(ResizeEdges.Right);

    private static bool hasVerticalEdge(ResizeEdges edges) =>
        edges.HasFlag(ResizeEdges.Top)
        || edges.HasFlag(ResizeEdges.Bottom);

    private void updateTopCover(Vector2 screenSpacePosition)
    {
        Vector2 local = ToLocalSpace(screenSpacePosition);
        (Vector2 topLeft, Vector2 bottomRight) =
            GameplayLayoutGeometry.BoundsIn(this, playfield);
        settings.LayoutTopCoverRatio.Value = Math.Clamp(
            (local.Y - topLeft.Y)
            / Math.Max(1, bottomRight.Y - topLeft.Y),
            0,
            YokkoGameplaySettings.MaximumTopCoverRatio);
    }

    private void updateBottomCover(Vector2 screenSpacePosition)
    {
        Vector2 local = ToLocalSpace(screenSpacePosition);
        (Vector2 topLeft, Vector2 bottomRight) =
            GameplayLayoutGeometry.BoundsIn(this, playfield);
        settings.LayoutBottomCoverRatio.Value = Math.Clamp(
            (bottomRight.Y - local.Y)
            / Math.Max(1, bottomRight.Y - topLeft.Y),
            0,
            YokkoGameplaySettings.MaximumBottomCoverRatio);
    }

    private void reset()
    {
        ResetAll();
    }

    private void updateOverview(
        Vector2 playfieldTopLeft,
        Vector2 playfieldBottomRight,
        Vector2 hudTopLeft,
        Vector2 hudBottomRight,
        Vector2 timingBarTopLeft,
        Vector2 timingBarBottomRight)
    {
        setOverviewBounds(
            miniPlayfield,
            playfieldTopLeft,
            playfieldBottomRight);
        setOverviewBounds(miniHud, hudTopLeft, hudBottomRight);
        setOverviewBounds(
            miniTimingBar,
            timingBarTopLeft,
            timingBarBottomRight);

        float playfieldMiniHeight = miniPlayfield.Height;
        miniTopCover.Position = miniPlayfield.Position;
        miniTopCover.Size = new Vector2(
            miniPlayfield.Width,
            playfieldMiniHeight * (float)Math.Clamp(
                settings.LayoutTopCoverRatio.Value,
                0,
                YokkoGameplaySettings.MaximumTopCoverRatio));
        miniBottomCover.Position = new Vector2(
            miniPlayfield.X,
            miniPlayfield.Y + playfieldMiniHeight
            - playfieldMiniHeight * (float)Math.Clamp(
                settings.LayoutBottomCoverRatio.Value,
                0,
                YokkoGameplaySettings.MaximumBottomCoverRatio));
        miniBottomCover.Size = new Vector2(
            miniPlayfield.Width,
            playfieldMiniHeight * (float)Math.Clamp(
                settings.LayoutBottomCoverRatio.Value,
                0,
                YokkoGameplaySettings.MaximumBottomCoverRatio));
    }

    private void setOverviewBounds(
        Drawable target,
        Vector2 topLeft,
        Vector2 bottomRight)
    {
        float x = topLeft.X / Math.Max(1, DrawWidth)
                  * overviewContent.Width;
        float y = topLeft.Y / Math.Max(1, DrawHeight)
                  * overviewContent.Height;
        float width = (bottomRight.X - topLeft.X)
                      / Math.Max(1, DrawWidth)
                      * overviewContent.Width;
        float height = (bottomRight.Y - topLeft.Y)
                       / Math.Max(1, DrawHeight)
                       * overviewContent.Height;
        target.Position = new Vector2(x, y);
        target.Size = new Vector2(
            Math.Max(2, width),
            Math.Max(2, height));
    }

    private static void setBounds(
        Drawable target,
        Vector2 topLeft,
        Vector2 bottomRight)
    {
        target.Position = topLeft;
        target.Size = new Vector2(
            Math.Max(8, bottomRight.X - topLeft.X),
            Math.Max(8, bottomRight.Y - topLeft.Y));
    }

    private static void setHandleBounds(
        Drawable handle,
        float x,
        float boundaryY,
        float width)
    {
        handle.Position = new Vector2(x, boundaryY - 13);
        handle.Size = new Vector2(Math.Max(60, width), 26);
    }

    private static double clampOffset(double value) => Math.Clamp(
        value,
        YokkoGameplaySettings.MinimumLayoutOffset,
        YokkoGameplaySettings.MaximumLayoutOffset);

    private static Container createMiniPlayfield() =>
        new()
        {
            Masking = true,
            BorderThickness = 1.5f,
            BorderColour = HomeControlColours.Cyan,
            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new Color4(0.006f, 0.008f, 0.013f, 1f),
                },
                new Box
                {
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.BottomCentre,
                    RelativeSizeAxes = Axes.X,
                    Height = 2,
                    Y = -5,
                    Colour = HomeControlColours.PaleCyan,
                },
            },
        };

    private static Box createMiniCover() => new()
    {
        Colour = new Color4(0f, 0f, 0f, 0.94f),
    };

    [Flags]
    private enum ResizeEdges
    {
        None = 0,
        Left = 1 << 0,
        Right = 1 << 1,
        Top = 1 << 2,
        Bottom = 1 << 3,
    }

    private partial class LayoutTransformTarget : CompositeDrawable
    {
        private readonly Drawable coordinateSpace;
        private readonly Action<Vector2> drag;
        private readonly Action<ResizeEdges, Vector2> resize;
        private readonly Action reset;
        private readonly Action beginChange;
        private readonly Action<LayoutTransformTarget> select;
        private readonly Func<
            LayoutTransformTarget,
            Vector2,
            bool,
            Vector2> snapMove;
        private readonly Action clearGuides;
        private readonly Action<float> scroll;
        private readonly Container frame;
        private readonly Container labelPanel;
        private readonly Box selectionTint;
        private Vector2 lastPosition;
        private Axes? constrainedDragAxis;
        private bool selected;

        internal LayoutElementKind Kind { get; }

        internal int ResizeHandleCount => 8;

        internal bool IsLocked { get; private set; }

        internal bool EditorHidden { get; private set; }

        internal bool AspectLocked { get; private set; }

        internal float LockedAspectRatio { get; private set; } = 1;

        internal bool CanEdit => !IsLocked && !EditorHidden;

        public LayoutTransformTarget(
            Drawable coordinateSpace,
            LayoutElementKind kind,
            LocalisableString label,
            Action<Vector2> drag,
            Action<ResizeEdges, Vector2> resize,
            Action reset,
            Action beginChange,
            Action<LayoutTransformTarget> select,
            Func<
                LayoutTransformTarget,
                Vector2,
                bool,
                Vector2> snapMove,
            Action clearGuides,
            Action<float> scroll = null)
        {
            this.coordinateSpace = coordinateSpace;
            Kind = kind;
            this.drag = drag;
            this.resize = resize;
            this.reset = reset;
            this.beginChange = beginChange;
            this.select = select;
            this.snapMove = snapMove;
            this.clearGuides = clearGuides;
            this.scroll = scroll;
            Masking = false;

            InternalChildren = new Drawable[]
            {
                frame = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Masking = true,
                    BorderThickness = 2,
                    BorderColour = HomeControlColours.Cyan,
                    Children = new Drawable[]
                    {
                        selectionTint = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = new Color4(
                                HomeControlColours.PaleCyan.R,
                                HomeControlColours.PaleCyan.G,
                                HomeControlColours.PaleCyan.B,
                                0.035f),
                        },
                        labelPanel = new Container
                        {
                            Position = new Vector2(8, 6),
                            Size = new Vector2(218, 32),
                            Masking = true,
                            CornerRadius = 5,
                            BorderThickness = 1,
                            BorderColour = HomeControlColours.Navy,
                            Children = new Drawable[]
                            {
                                new Box
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Colour = HomeControlColours.Ivory,
                                },
                                new Box
                                {
                                    RelativeSizeAxes = Axes.Y,
                                    Width = 4,
                                    Colour = HomeControlColours.Cyan,
                                },
                                new SpriteText
                                {
                                    Anchor = Anchor.CentreLeft,
                                    Origin = Anchor.CentreLeft,
                                    X = 10,
                                    Text = label,
                                    Font = LayoutEditorTypography.Bold(9),
                                    Colour = HomeControlColours.Navy,
                                },
                            },
                        },
                    },
                },
                createHandle(
                    Anchor.TopLeft,
                    ResizeEdges.Left | ResizeEdges.Top),
                createHandle(Anchor.TopCentre, ResizeEdges.Top, true),
                createHandle(
                    Anchor.TopRight,
                    ResizeEdges.Right | ResizeEdges.Top),
                createHandle(Anchor.CentreLeft, ResizeEdges.Left, true),
                createHandle(Anchor.CentreRight, ResizeEdges.Right, true),
                createHandle(
                    Anchor.BottomLeft,
                    ResizeEdges.Left | ResizeEdges.Bottom),
                createHandle(Anchor.BottomCentre, ResizeEdges.Bottom, true),
                createHandle(
                    Anchor.BottomRight,
                    ResizeEdges.Right | ResizeEdges.Bottom),
            };
        }

        private Drawable createHandle(
            Anchor anchor,
            ResizeEdges edges,
            bool edge = false) =>
            new ResizeHandle(
                coordinateSpace,
                delta => resize(edges, delta),
                beginChange,
                () => select(this),
                () => CanEdit,
                edge)
            {
                Anchor = anchor,
                Origin = anchor,
                Size = edge
                    ? anchor is Anchor.TopCentre or Anchor.BottomCentre
                        ? new Vector2(28, 12)
                        : new Vector2(12, 28)
                    : new Vector2(16),
            };

        protected override bool OnMouseDown(MouseDownEvent e)
        {
            if (e.Button != MouseButton.Left)
                return false;

            select(this);
            lastPosition = coordinateSpace.ToLocalSpace(
                e.ScreenSpaceMousePosition);
            constrainedDragAxis = null;
            return true;
        }

        protected override bool OnDragStart(DragStartEvent e)
        {
            if (!CanEdit)
                return false;

            beginChange();
            return true;
        }

        protected override void OnDrag(DragEvent e)
        {
            Vector2 current = coordinateSpace.ToLocalSpace(
                e.ScreenSpaceMousePosition);
            Vector2 requestedDelta = current - lastPosition;
            if (e.ShiftPressed)
            {
                constrainedDragAxis ??=
                    Math.Abs(requestedDelta.X) >= Math.Abs(requestedDelta.Y)
                        ? Axes.X
                        : Axes.Y;
                if (constrainedDragAxis == Axes.X)
                    requestedDelta.Y = 0;
                else
                    requestedDelta.X = 0;
            }
            else
                constrainedDragAxis = null;

            Vector2 delta = snapMove(
                this,
                requestedDelta,
                e.AltPressed);
            drag(delta);
            lastPosition = current;
        }

        protected override void OnDragEnd(DragEndEvent e)
        {
            constrainedDragAxis = null;
            clearGuides();
            base.OnDragEnd(e);
        }

        protected override bool OnScroll(ScrollEvent e)
        {
            if (scroll == null || e.ScrollDelta.Y == 0)
                return false;

            select(this);
            if (!CanEdit)
                return true;

            beginChange();
            scroll(e.ScrollDelta.Y);
            return true;
        }

        internal void SetSelected(bool selected)
        {
            this.selected = selected;
            frame.BorderColour = selected
                ? IsLocked
                    ? HomeControlColours.Pink
                    : HomeControlColours.Yellow
                : IsLocked
                    ? new Color4(
                        HomeControlColours.Navy.R,
                        HomeControlColours.Navy.G,
                        HomeControlColours.Navy.B,
                        0.62f)
                    : HomeControlColours.Cyan;
            frame.BorderThickness = selected ? 3 : 2;
            labelPanel.BorderColour = selected
                ? HomeControlColours.Yellow
                : HomeControlColours.Navy;
            selectionTint.Colour = new Color4(
                HomeControlColours.PaleCyan.R,
                HomeControlColours.PaleCyan.G,
                HomeControlColours.PaleCyan.B,
                selected ? 0.11f : 0.035f);
        }

        internal void Reset() => reset();

        internal void MoveBy(Vector2 delta) => drag(delta);

        internal void ResizeBy(ResizeEdges edges, Vector2 delta) =>
            resize(edges, delta);

        internal void SetLocked(bool value)
        {
            IsLocked = value;
            SetSelected(selected);
        }

        internal void SetEditorHidden(bool value)
        {
            EditorHidden = value;
            Alpha = value ? 0 : 1;
        }

        internal void SetAspectLocked(bool value)
        {
            AspectLocked = value;
            LockedAspectRatio = Math.Max(
                0.01f,
                DrawWidth / Math.Max(1, DrawHeight));
        }

        internal bool TryNudge(Key key, float distance)
        {
            if (!CanEdit)
                return false;

            Vector2 direction = key switch
            {
                Key.Left => -Vector2.UnitX,
                Key.Right => Vector2.UnitX,
                Key.Up => -Vector2.UnitY,
                Key.Down => Vector2.UnitY,
                _ => Vector2.Zero,
            };

            if (direction == Vector2.Zero)
                return false;

            drag(direction * Math.Max(0.1f, distance));
            return true;
        }

        internal bool TryResize(Key key, float distance)
        {
            if (!CanEdit)
                return false;

            float step = Math.Max(0.1f, distance);
            Vector2 delta;
            switch (key)
            {
                case Key.Left:
                    delta = new Vector2(-step, 0);
                    if (AspectLocked)
                        delta.Y = delta.X / LockedAspectRatio;
                    break;

                case Key.Right:
                    delta = new Vector2(step, 0);
                    if (AspectLocked)
                        delta.Y = delta.X / LockedAspectRatio;
                    break;

                case Key.Up:
                    delta = new Vector2(0, -step);
                    if (AspectLocked)
                        delta.X = delta.Y * LockedAspectRatio;
                    break;

                case Key.Down:
                    delta = new Vector2(0, step);
                    if (AspectLocked)
                        delta.X = delta.Y * LockedAspectRatio;
                    break;

                default:
                    return false;
            }

            resize(ResizeEdges.Right | ResizeEdges.Bottom, delta);
            return true;
        }
    }

    private partial class ResizeHandle : CompositeDrawable
    {
        private readonly Drawable coordinateSpace;
        private readonly Action<Vector2> resize;
        private readonly Action beginChange;
        private readonly Action select;
        private readonly Func<bool> canEdit;
        private readonly Box fill;
        private readonly Color4 idleColour;
        private Vector2 lastPosition;

        public ResizeHandle(
            Drawable coordinateSpace,
            Action<Vector2> resize,
            Action beginChange,
            Action select,
            Func<bool> canEdit,
            bool edge)
        {
            this.coordinateSpace = coordinateSpace;
            this.resize = resize;
            this.beginChange = beginChange;
            this.select = select;
            this.canEdit = canEdit;
            Depth = -30;
            Masking = true;
            CornerRadius = edge ? 3 : 2;
            BorderThickness = 2;
            BorderColour = HomeControlColours.Navy;
            idleColour = edge
                ? HomeControlColours.PaleCyan
                : HomeControlColours.Yellow;
            InternalChild = fill = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = idleColour,
            };
        }

        protected override bool OnMouseDown(MouseDownEvent e)
        {
            if (e.Button != MouseButton.Left)
                return false;

            select();
            lastPosition = coordinateSpace.ToLocalSpace(
                e.ScreenSpaceMousePosition);
            return true;
        }

        protected override bool OnDragStart(DragStartEvent e)
        {
            if (!canEdit())
                return false;

            beginChange();
            return true;
        }

        protected override void OnDrag(DragEvent e)
        {
            Vector2 current = coordinateSpace.ToLocalSpace(
                e.ScreenSpaceMousePosition);
            resize(current - lastPosition);
            lastPosition = current;
        }

        protected override bool OnHover(HoverEvent e)
        {
            fill.FadeColour(
                HomeControlColours.Pink,
                80,
                Easing.OutQuint);
            BorderColour = HomeControlColours.Yellow;
            this.ScaleTo(1.14f, 90, Easing.OutQuint);
            return true;
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            fill.FadeColour(idleColour, 110, Easing.OutQuint);
            BorderColour = HomeControlColours.Navy;
            this.ScaleTo(1, 110, Easing.OutQuint);
        }
    }

    private partial class CoverDragHandle : CompositeDrawable
    {
        private readonly Action<Vector2> update;
        private readonly Action beginChange;
        private readonly Box background;

        public CoverDragHandle(
            Drawable coordinateSpace,
            string label,
            Action<Vector2> update,
            Action beginChange)
        {
            _ = coordinateSpace;
            this.update = update;
            this.beginChange = beginChange;
            Depth = -10;
            Masking = true;
            CornerRadius = 5;
            BorderThickness = 1.5f;
            BorderColour = HomeControlColours.Navy;

            InternalChildren = new Drawable[]
            {
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = HomeControlColours.Ivory,
                },
                new Box
                {
                    RelativeSizeAxes = Axes.Y,
                    Width = 5,
                    Colour = label.StartsWith("TOP", StringComparison.Ordinal)
                        ? HomeControlColours.Cyan
                        : HomeControlColours.Pink,
                },
                new SpriteText
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Text = label,
                    Font = LayoutEditorTypography.Bold(8),
                    Colour = HomeControlColours.Navy,
                },
            };
        }

        protected override bool OnMouseDown(MouseDownEvent e)
        {
            if (e.Button != MouseButton.Left)
                return false;

            beginChange();
            update(e.ScreenSpaceMousePosition);
            return true;
        }

        protected override bool OnDragStart(DragStartEvent e) => true;

        protected override void OnDrag(DragEvent e) =>
            update(e.ScreenSpaceMousePosition);

        protected override bool OnHover(HoverEvent e)
        {
            background.FadeColour(
                HomeControlColours.PaleCyan,
                90,
                Easing.OutQuint);
            BorderColour = HomeControlColours.Pink;
            return true;
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            background.FadeColour(
                HomeControlColours.Ivory,
                120,
                Easing.OutQuint);
            BorderColour = HomeControlColours.Navy;
        }
    }

    private partial class LayoutActionButton : ClickableContainer
    {
        private readonly Box background;
        private readonly Color4 idleColour;
        private readonly Color4 hoverColour;
        private bool available = true;

        public LayoutActionButton(
            LocalisableString text,
            IconUsage icon,
            Action action,
            bool primary = false)
        {
            Action = () =>
            {
                if (available)
                    action();
            };
            Size = new Vector2(112, 38);
            Masking = true;
            CornerRadius = 6;
            BorderThickness = 1.5f;
            BorderColour = primary
                ? HomeControlColours.Navy
                : new Color4(
                    HomeControlColours.Navy.R,
                    HomeControlColours.Navy.G,
                    HomeControlColours.Navy.B,
                    0.72f);

            idleColour = primary
                ? HomeControlColours.Navy
                : Color4.White;
            hoverColour = primary
                ? new Color4(0.055f, 0.15f, 0.7f, 1f)
                : HomeControlColours.PaleCyan;

            InternalChildren = new Drawable[]
            {
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = idleColour,
                },
                new FillFlowContainer
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(6, 0),
                    Children = new Drawable[]
                    {
                        new SpriteIcon
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Size = new Vector2(13),
                            Icon = icon,
                            Colour = primary
                                ? HomeControlColours.Yellow
                                : HomeControlColours.Navy,
                        },
                        new SpriteText
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Text = text,
                            Font = LayoutEditorTypography.Bold(9),
                            Colour = primary
                                ? Color4.White
                                : HomeControlColours.Navy,
                        },
                    },
                },
            };
        }

        internal void SetAvailable(bool value)
        {
            available = value;
            Alpha = value ? 1 : 0.42f;
        }

        protected override bool OnHover(HoverEvent e)
        {
            if (!available)
                return false;

            background.FadeColour(hoverColour, 90, Easing.OutQuint);
            this.ScaleTo(1.025f, 100, Easing.OutQuint);
            return true;
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            background.FadeColour(idleColour, 120, Easing.OutQuint);
            this.ScaleTo(1f, 120, Easing.OutQuint);
        }
    }

    private readonly record struct LayoutSnapshot(
        double PlayfieldOffsetX,
        double PlayfieldOffsetY,
        double HudOffsetX,
        double HudOffsetY,
        double PlayfieldWidthScale,
        double PlayfieldHeightScale,
        double HudScaleX,
        double HudScaleY,
        double TimingBarOffsetX,
        double TimingBarOffsetY,
        double TimingBarScaleX,
        double TimingBarScaleY,
        double TopCoverRatio,
        double BottomCoverRatio);

    private static class LayoutEditorTypography
    {
        public static FontUsage Regular(float size) =>
            new("Yokko", readableSize(size));

        public static FontUsage Bold(float size) =>
            new("Yokko", readableSize(size), "Bold");

        private static float readableSize(float size) =>
            MathF.Max(18, size + MathF.Min(10, 8 + size * 0.08f));
    }
}
