using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;
using Yokko.Game.Gameplay;
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
    private Container overviewContent;
    private Container miniPlayfield;
    private Container miniHud;
    private Container miniTimingBar;
    private Box miniTopCover;
    private Box miniBottomCover;

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
            createTopBar(),
            playfieldTarget = new LayoutTransformTarget(
                this,
                "轨道 · 拖动 / 拉伸边框",
                movePlayfield,
                resizePlayfield,
                resizePlayfieldWithWheel),
            hudTarget = new LayoutTransformTarget(
                this,
                "信息面板 · 拖动 / 拉伸边框",
                moveHud,
                resizeHud),
            timingBarTarget = new LayoutTransformTarget(
                this,
                "判定条 · 拖动 / 拉伸边框",
                moveTimingBar,
                resizeTimingBar),
            topCoverHandle = new CoverDragHandle(
                this,
                "TOP COVER",
                updateTopCover),
            bottomCoverHandle = new CoverDragHandle(
                this,
                "BOTTOM COVER",
                updateBottomCover),
            createOverviewCard(),
        };
    }

    internal void SetEditing(bool editing)
    {
        IsEditing = editing;
        ClearTransforms();

        if (editing)
            this.FadeTo(1, 100, Easing.OutQuint);
        else
            this.FadeTo(0, 100, Easing.OutQuint);
    }

    internal void SaveAndClose()
    {
        save();
        close();
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
    }

    private Drawable createTopBar() =>
        new Container
        {
            Position = new Vector2(16, 14),
            Size = new Vector2(414, 60),
            Depth = -100,
            Children = new Drawable[]
            {
                new Container
                {
                    Position = new Vector2(4, 4),
                    Size = new Vector2(410, 56),
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
                    Size = new Vector2(410, 56),
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
                                    Text = "HUD 布局",
                                    Font = HomeTypography.Display(14),
                                    Colour = HomeControlColours.Navy,
                                },
                                new SpriteText
                                {
                                    Text = "拖动元素 · 拉边缩放",
                                    Font = HomeTypography.Body(9),
                                    Colour = new Color4(
                                        HomeControlColours.Navy.R,
                                        HomeControlColours.Navy.G,
                                        HomeControlColours.Navy.B,
                                        0.64f),
                                },
                            },
                        },
                        new LayoutActionButton(
                            "重置",
                            FontAwesome.Solid.Undo,
                            reset)
                        {
                            Anchor = Anchor.CentreRight,
                            Origin = Anchor.CentreRight,
                            X = -132,
                            Width = 96,
                        },
                        new LayoutActionButton(
                            "保存并返回",
                            FontAwesome.Solid.Check,
                            SaveAndClose,
                            true)
                        {
                            Anchor = Anchor.CentreRight,
                            Origin = Anchor.CentreRight,
                            X = -10,
                            Width = 116,
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
                            Text = "完整页面预览",
                            Font = HomeTypography.Display(11),
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
        settings.ResetGameplayLayout();
        save();
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
        handle.Position = new Vector2(x, boundaryY - 10);
        handle.Size = new Vector2(Math.Max(60, width), 20);
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
        private readonly Action<float> scroll;
        private Vector2 lastPosition;

        internal int ResizeHandleCount => 8;

        public LayoutTransformTarget(
            Drawable coordinateSpace,
            string label,
            Action<Vector2> drag,
            Action<ResizeEdges, Vector2> resize,
            Action<float> scroll = null)
        {
            this.coordinateSpace = coordinateSpace;
            this.drag = drag;
            this.resize = resize;
            this.scroll = scroll;
            Masking = false;

            InternalChildren = new Drawable[]
            {
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Masking = true,
                    BorderThickness = 2,
                    BorderColour = HomeControlColours.Cyan,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = new Color4(
                                HomeControlColours.PaleCyan.R,
                                HomeControlColours.PaleCyan.G,
                                HomeControlColours.PaleCyan.B,
                                0.035f),
                        },
                        new Container
                        {
                            Position = new Vector2(8, 6),
                            Size = new Vector2(202, 27),
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
                                    Font = HomeTypography.Display(9),
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
                edge)
            {
                Anchor = anchor,
                Origin = anchor,
                Size = edge
                    ? anchor is Anchor.TopCentre or Anchor.BottomCentre
                        ? new Vector2(22, 10)
                        : new Vector2(10, 22)
                    : new Vector2(13),
            };

        protected override bool OnMouseDown(MouseDownEvent e)
        {
            if (e.Button != MouseButton.Left)
                return false;

            lastPosition = coordinateSpace.ToLocalSpace(
                e.ScreenSpaceMousePosition);
            return true;
        }

        protected override bool OnDragStart(DragStartEvent e) => true;

        protected override void OnDrag(DragEvent e)
        {
            Vector2 current = coordinateSpace.ToLocalSpace(
                e.ScreenSpaceMousePosition);
            drag(current - lastPosition);
            lastPosition = current;
        }

        protected override bool OnScroll(ScrollEvent e)
        {
            if (scroll == null || e.ScrollDelta.Y == 0)
                return false;

            scroll(e.ScrollDelta.Y);
            return true;
        }
    }

    private partial class ResizeHandle : CompositeDrawable
    {
        private readonly Drawable coordinateSpace;
        private readonly Action<Vector2> resize;
        private Vector2 lastPosition;

        public ResizeHandle(
            Drawable coordinateSpace,
            Action<Vector2> resize,
            bool edge)
        {
            this.coordinateSpace = coordinateSpace;
            this.resize = resize;
            Depth = -30;
            Masking = true;
            CornerRadius = edge ? 3 : 2;
            BorderThickness = 2;
            BorderColour = HomeControlColours.Navy;
            InternalChild = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = edge
                    ? HomeControlColours.PaleCyan
                    : HomeControlColours.Yellow,
            };
        }

        protected override bool OnMouseDown(MouseDownEvent e)
        {
            if (e.Button != MouseButton.Left)
                return false;

            lastPosition = coordinateSpace.ToLocalSpace(
                e.ScreenSpaceMousePosition);
            return true;
        }

        protected override bool OnDragStart(DragStartEvent e) => true;

        protected override void OnDrag(DragEvent e)
        {
            Vector2 current = coordinateSpace.ToLocalSpace(
                e.ScreenSpaceMousePosition);
            resize(current - lastPosition);
            lastPosition = current;
        }
    }

    private partial class CoverDragHandle : CompositeDrawable
    {
        private readonly Action<Vector2> update;

        public CoverDragHandle(
            Drawable coordinateSpace,
            string label,
            Action<Vector2> update)
        {
            _ = coordinateSpace;
            this.update = update;
            Depth = -10;
            Masking = true;
            CornerRadius = 5;
            BorderThickness = 1.5f;
            BorderColour = HomeControlColours.Navy;

            InternalChildren = new Drawable[]
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
                    Colour = label.StartsWith("TOP", StringComparison.Ordinal)
                        ? HomeControlColours.Cyan
                        : HomeControlColours.Pink,
                },
                new SpriteText
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Text = label,
                    Font = HomeTypography.Display(8),
                    Colour = HomeControlColours.Navy,
                },
            };
        }

        protected override bool OnMouseDown(MouseDownEvent e)
        {
            if (e.Button != MouseButton.Left)
                return false;

            update(e.ScreenSpaceMousePosition);
            return true;
        }

        protected override bool OnDragStart(DragStartEvent e) => true;

        protected override void OnDrag(DragEvent e) =>
            update(e.ScreenSpaceMousePosition);
    }

    private partial class LayoutActionButton : ClickableContainer
    {
        private readonly Box background;
        private readonly Color4 idleColour;
        private readonly Color4 hoverColour;

        public LayoutActionButton(
            string text,
            IconUsage icon,
            Action action,
            bool primary = false)
        {
            Action = action;
            Size = new Vector2(112, 34);
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
                            Font = HomeTypography.Display(9),
                            Colour = primary
                                ? Color4.White
                                : HomeControlColours.Navy,
                        },
                    },
                },
            };
        }

        protected override bool OnHover(HoverEvent e)
        {
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
}
