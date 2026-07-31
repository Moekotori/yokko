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
    private readonly YokkoGameplaySettings settings;
    private readonly Action save;
    private readonly Action close;
    private readonly LayoutDragTarget playfieldTarget;
    private readonly LayoutDragTarget hudTarget;
    private readonly CoverDragHandle topCoverHandle;
    private readonly CoverDragHandle bottomCoverHandle;
    private readonly Container overviewContent;
    private readonly Container miniPlayfield;
    private readonly Container miniHud;
    private readonly Box miniTopCover;
    private readonly Box miniBottomCover;

    internal bool IsEditing { get; private set; }

    internal float OverviewAspectRatio =>
        overviewContent.Width / overviewContent.Height;

    public GameplayLayoutEditorOverlay(
        GameplayPlayfield playfield,
        GameplayHud hud,
        YokkoGameplaySettings settings,
        Action save,
        Action close)
    {
        this.playfield = playfield;
        this.hud = hud;
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
                Colour = new Color4(0f, 0f, 0f, 0.08f),
            },
            createTopBar(),
            playfieldTarget = new LayoutDragTarget(
                this,
                "PLAYFIELD · DRAG · WHEEL TO RESIZE",
                movePlayfield,
                resizePlayfield),
            hudTarget = new LayoutDragTarget(
                this,
                "INFO PANEL · DRAG",
                moveHud),
            topCoverHandle = new CoverDragHandle(
                this,
                "TOP COVER",
                updateTopCover),
            bottomCoverHandle = new CoverDragHandle(
                this,
                "BOTTOM COVER",
                updateBottomCover),
            new Container
            {
                Anchor = Anchor.BottomRight,
                Origin = Anchor.BottomRight,
                Position = new Vector2(-18, -18),
                Size = new Vector2(
                    overviewWidth + overviewPadding * 2,
                    overviewHeight + 42),
                Masking = true,
                CornerRadius = 5,
                BorderThickness = 2,
                BorderColour = YokkoPalette.Cyan,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = new Color4(0.025f, 0.032f, 0.046f, 0.98f),
                    },
                    new SpriteText
                    {
                        Position = new Vector2(overviewPadding, 8),
                        Text = "FULL PAGE PREVIEW",
                        Font = FontUsage.Default.With(
                            size: 13,
                            weight: "SemiBold"),
                        Colour = YokkoPalette.Text,
                    },
                    overviewContent = new Container
                    {
                        Position = new Vector2(overviewPadding, 32),
                        Size = new Vector2(overviewWidth, overviewHeight),
                        Masking = true,
                        BorderThickness = 1,
                        BorderColour = YokkoPalette.Border,
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
                                BorderColour = YokkoPalette.TextDim,
                                Child = new Box
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Colour = new Color4(
                                        0.035f,
                                        0.045f,
                                        0.065f,
                                        0.96f),
                                },
                            },
                            miniTopCover = createMiniCover(),
                            miniBottomCover = createMiniCover(),
                        },
                    },
                },
            },
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

        setBounds(
            playfieldTarget,
            playfieldTopLeft,
            playfieldBottomRight);
        setBounds(hudTarget, hudTopLeft, hudBottomRight);

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
            hudBottomRight);
    }

    private Drawable createTopBar() =>
        new Container
        {
            RelativeSizeAxes = Axes.X,
            Height = 44,
            Masking = true,
            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new Color4(0.018f, 0.023f, 0.036f, 0.97f),
                },
                new SpriteText
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    X = 18,
                    Text =
                        "LAYOUT EDIT  ·  DRAG ELEMENTS  ·  WHEEL RESIZES PLAYFIELD",
                    Font = FontUsage.Default.With(
                        size: 14,
                        weight: "SemiBold"),
                    Colour = YokkoPalette.Text,
                },
                new LayoutActionButton(
                    "重置布局",
                    reset)
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    X = -174,
                },
                new LayoutActionButton(
                    "保存并返回",
                    SaveAndClose,
                    YokkoPalette.Lime)
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    X = -12,
                    Width = 148,
                },
            },
        };

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

    private void resizePlayfield(float direction)
    {
        settings.LayoutPlayfieldWidthScale.Value = Math.Clamp(
            settings.LayoutPlayfieldWidthScale.Value
            + Math.Sign(direction) * 0.05,
            YokkoGameplaySettings.MinimumPlayfieldWidthScale,
            YokkoGameplaySettings.MaximumPlayfieldWidthScale);
    }

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
        Vector2 hudBottomRight)
    {
        setOverviewBounds(
            miniPlayfield,
            playfieldTopLeft,
            playfieldBottomRight);
        setOverviewBounds(miniHud, hudTopLeft, hudBottomRight);

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
            BorderThickness = 1,
            BorderColour = YokkoPalette.Cyan,
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
                    Colour = YokkoPalette.TextDim,
                },
            },
        };

    private static Box createMiniCover() => new()
    {
        Colour = new Color4(0f, 0f, 0f, 0.94f),
    };

    private partial class LayoutDragTarget : CompositeDrawable
    {
        private readonly Drawable coordinateSpace;
        private readonly Action<Vector2> drag;
        private readonly Action<float> scroll;
        private Vector2 lastPosition;

        public LayoutDragTarget(
            Drawable coordinateSpace,
            string label,
            Action<Vector2> drag,
            Action<float> scroll = null)
        {
            this.coordinateSpace = coordinateSpace;
            this.drag = drag;
            this.scroll = scroll;
            Masking = true;
            BorderThickness = 2;
            BorderColour = YokkoPalette.Cyan;

            InternalChildren = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new Color4(
                        YokkoPalette.Cyan.R,
                        YokkoPalette.Cyan.G,
                        YokkoPalette.Cyan.B,
                        0.025f),
                },
                new SpriteText
                {
                    Position = new Vector2(8, 6),
                    Text = label,
                    Font = FontUsage.Default.With(
                        size: 12,
                        weight: "SemiBold"),
                    Colour = YokkoPalette.Cyan,
                },
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
            CornerRadius = 4;
            BorderThickness = 1;
            BorderColour = YokkoPalette.Text;

            InternalChildren = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new Color4(0.03f, 0.04f, 0.058f, 0.96f),
                },
                new SpriteText
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Text = label,
                    Font = FontUsage.Default.With(
                        size: 11,
                        weight: "SemiBold"),
                    Colour = YokkoPalette.Text,
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
        private readonly Color4 accent;

        public LayoutActionButton(
            string text,
            Action action,
            Color4? accent = null)
        {
            Action = action;
            this.accent = accent ?? YokkoPalette.Cyan;
            Size = new Vector2(140, 30);
            Masking = true;
            CornerRadius = 4;
            BorderThickness = 1;
            BorderColour = this.accent;

            InternalChildren = new Drawable[]
            {
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new Color4(0.04f, 0.05f, 0.07f, 1f),
                },
                new SpriteText
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Text = text,
                    Font = FontUsage.Default.With(
                        size: 12,
                        weight: "SemiBold"),
                    Colour = YokkoPalette.Text,
                },
            };
        }

        protected override bool OnHover(HoverEvent e)
        {
            background.FadeColour(accent, 90, Easing.OutQuint);
            return true;
        }

        protected override void OnHoverLost(HoverLostEvent e) =>
            background.FadeColour(
                new Color4(0.04f, 0.05f, 0.07f, 1f),
                120,
                Easing.OutQuint);
    }
}
