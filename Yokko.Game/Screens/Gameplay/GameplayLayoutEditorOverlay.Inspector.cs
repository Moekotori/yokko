using System;
using System.Collections.Generic;
using System.Globalization;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osuTK;
using osuTK.Graphics;
using Yokko.Game.Gameplay;
using Yokko.Game.Localisation;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Screens.Gameplay;

internal partial class GameplayLayoutEditorOverlay
{
    private const float snapThreshold = 8;
    private const float defaultCoverHeight = 120;

    private readonly Dictionary<LayoutElementKind, float> originalElementAlpha =
        new();

    private Box verticalSnapGuide;
    private Box horizontalSnapGuide;
    private LayoutInspectorPanel inspector;
    private CoverPanel coverPanel;
    private float nudgeStep = 1;

    internal bool TimingBarLockedForTest => timingBarTarget.IsLocked;

    internal bool HudHiddenForTest => hudTarget.EditorHidden;

    internal float TimingBarEditorWidthForTest => timingBarTarget.DrawWidth;

    internal float TimingBarEditorCentreXForTest =>
        timingBarTarget.X + timingBarTarget.DrawWidth / 2;

    internal float TopCoverHeightForTest => coverHeight(true);

    internal float BottomCoverHeightForTest => coverHeight(false);

    internal bool TopCoverEnabledForTest =>
        settings.LayoutTopCoverRatio.Value > 0.0001;

    internal bool BottomCoverEnabledForTest =>
        settings.LayoutBottomCoverRatio.Value > 0.0001;

    internal void SetTimingBarLockedForTest(bool locked) =>
        setLayerLocked(LayoutElementKind.TimingBar, locked);

    internal void SetHudHiddenForTest(bool hidden) =>
        setLayerHidden(LayoutElementKind.Hud, hidden);

    internal void SetTimingBarWidthForTest(double width) =>
        applyMetric(
            LayoutElementKind.TimingBar,
            LayoutMetricField.Width,
            width);

    internal void SetTopCoverEnabledForTest(bool enabled) =>
        setCoverEnabled(true, enabled);

    internal void SetBottomCoverEnabledForTest(bool enabled) =>
        setCoverEnabled(false, enabled);

    internal void SetTopCoverHeightForTest(double height) =>
        applyCoverHeight(true, height);

    internal void SetBottomCoverHeightForTest(double height) =>
        applyCoverHeight(false, height);

    internal Vector2 SnapTimingBarMoveForTest(
        Vector2 delta,
        bool bypass) =>
        snapTargetMove(timingBarTarget, delta, bypass);

    private enum LayoutElementKind
    {
        Playfield,
        Hud,
        TimingBar,
    }

    private enum LayoutMetricField
    {
        X,
        Y,
        Width,
        Height,
    }

    private static Box createSnapGuide(bool vertical) => new()
    {
        RelativeSizeAxes = vertical ? Axes.Y : Axes.X,
        Width = vertical ? 2 : 1,
        Height = vertical ? 1 : 2,
        Colour = HomeControlColours.Pink,
        Alpha = 0,
        Depth = -70,
    };

    private Drawable createInspectorCard()
    {
        inspector = new LayoutInspectorPanel(
            kind => selectTarget(targetFor(kind)),
            setLayerHidden,
            setLayerLocked,
            setAspectLocked,
            applyMetric,
            centreSelected,
            cycleNudgeStep);
        return inspector;
    }

    private Drawable createCoverPanel()
    {
        coverPanel = new CoverPanel(
            enabled => setCoverEnabled(true, enabled),
            height => applyCoverHeight(true, height),
            enabled => setCoverEnabled(false, enabled),
            height => applyCoverHeight(false, height));
        return coverPanel;
    }

    private void beginEditorSession()
    {
        originalElementAlpha[LayoutElementKind.Playfield] = playfield.Alpha;
        originalElementAlpha[LayoutElementKind.Hud] = hud.Alpha;
        originalElementAlpha[LayoutElementKind.TimingBar] = timingBar.Alpha;

        foreach (LayoutTransformTarget target in allTargets())
        {
            target.SetEditorHidden(false);
            target.SetLocked(false);
            target.SetAspectLocked(false);
        }

        applyElementAlpha(LayoutElementKind.Playfield, false);
        applyElementAlpha(LayoutElementKind.Hud, false);
        applyElementAlpha(LayoutElementKind.TimingBar, false);
        nudgeStep = 1;
        inspector.SetStep(nudgeStep);
        clearSnapGuides();
    }

    private void endEditorSession()
    {
        foreach (LayoutTransformTarget target in allTargets())
        {
            target.SetEditorHidden(false);
            target.SetLocked(false);
            target.SetAspectLocked(false);
        }

        restoreOriginalAlpha(LayoutElementKind.Playfield);
        restoreOriginalAlpha(LayoutElementKind.Hud);
        restoreOriginalAlpha(LayoutElementKind.TimingBar);
        clearSnapGuides();
    }

    private IEnumerable<LayoutTransformTarget> allTargets()
    {
        yield return playfieldTarget;
        yield return hudTarget;
        yield return timingBarTarget;
    }

    private LayoutTransformTarget targetFor(LayoutElementKind kind) =>
        kind switch
        {
            LayoutElementKind.Playfield => playfieldTarget,
            LayoutElementKind.Hud => hudTarget,
            LayoutElementKind.TimingBar => timingBarTarget,
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

    private Drawable drawableFor(LayoutElementKind kind) =>
        kind switch
        {
            LayoutElementKind.Playfield => playfield,
            LayoutElementKind.Hud => hud,
            LayoutElementKind.TimingBar => timingBar,
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

    private Drawable miniDrawableFor(LayoutElementKind kind) =>
        kind switch
        {
            LayoutElementKind.Playfield => miniPlayfield,
            LayoutElementKind.Hud => miniHud,
            LayoutElementKind.TimingBar => miniTimingBar,
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

    private void setLayerHidden(LayoutElementKind kind, bool hidden)
    {
        LayoutTransformTarget target = targetFor(kind);
        target.SetEditorHidden(hidden);
        applyElementAlpha(kind, hidden);
        inspector.SetLayerState(
            kind,
            target.IsLocked,
            target.EditorHidden,
            target.AspectLocked);
    }

    private void applyElementAlpha(LayoutElementKind kind, bool hidden)
    {
        Drawable drawable = drawableFor(kind);
        drawable.Alpha = hidden
            ? 0
            : originalElementAlpha.GetValueOrDefault(kind, 1);

        Drawable miniDrawable = miniDrawableFor(kind);
        if (miniDrawable != null)
            miniDrawable.Alpha = hidden ? 0 : 1;
    }

    private void restoreOriginalAlpha(LayoutElementKind kind)
    {
        drawableFor(kind).Alpha =
            originalElementAlpha.GetValueOrDefault(kind, 1);
        Drawable miniDrawable = miniDrawableFor(kind);
        if (miniDrawable != null)
            miniDrawable.Alpha = 1;
    }

    private void setLayerLocked(LayoutElementKind kind, bool locked)
    {
        LayoutTransformTarget target = targetFor(kind);
        target.SetLocked(locked);
        inspector.SetLayerState(
            kind,
            target.IsLocked,
            target.EditorHidden,
            target.AspectLocked);
    }

    private void setAspectLocked(LayoutElementKind kind, bool locked)
    {
        LayoutTransformTarget target = targetFor(kind);
        target.SetAspectLocked(locked);
        inspector.SetLayerState(
            kind,
            target.IsLocked,
            target.EditorHidden,
            target.AspectLocked);
    }

    private void cycleNudgeStep()
    {
        nudgeStep = nudgeStep switch
        {
            < 2 => 5,
            < 6 => 10,
            _ => 1,
        };
        inspector.SetStep(nudgeStep);
    }

    private void refreshInspector()
    {
        coverPanel?.SetState(
            TopCoverEnabledForTest,
            TopCoverHeightForTest,
            BottomCoverEnabledForTest,
            BottomCoverHeightForTest);

        if (selectedTarget == null)
            return;

        inspector.SetLayerState(
            selectedTarget.Kind,
            selectedTarget.IsLocked,
            selectedTarget.EditorHidden,
            selectedTarget.AspectLocked);
        inspector.SetMetrics(new LayoutElementMetrics(
            selectedTarget.X,
            selectedTarget.Y,
            selectedTarget.DrawWidth,
            selectedTarget.DrawHeight));
    }

    private void setCoverEnabled(bool top, bool enabled)
    {
        double currentRatio = top
            ? settings.LayoutTopCoverRatio.Value
            : settings.LayoutBottomCoverRatio.Value;
        bool currentlyEnabled = currentRatio > 0.0001;
        if (currentlyEnabled == enabled)
            return;

        beginChange();
        if (!enabled)
        {
            if (top)
                settings.LayoutTopCoverRatio.Value = 0;
            else
                settings.LayoutBottomCoverRatio.Value = 0;

            return;
        }

        setCoverHeightRatio(top, defaultCoverHeight);
    }

    private void applyCoverHeight(bool top, double rawHeight)
    {
        if (double.IsNaN(rawHeight))
            return;

        beginChange();
        setCoverHeightRatio(top, rawHeight);
    }

    private void setCoverHeightRatio(bool top, double rawHeight)
    {
        double maximumRatio = top
            ? YokkoGameplaySettings.MaximumTopCoverRatio
            : YokkoGameplaySettings.MaximumBottomCoverRatio;
        double nextRatio = Math.Clamp(
            Math.Max(0, rawHeight) / playfieldHeight(),
            0,
            maximumRatio);

        if (top)
            settings.LayoutTopCoverRatio.Value = nextRatio;
        else
            settings.LayoutBottomCoverRatio.Value = nextRatio;
    }

    private float coverHeight(bool top)
    {
        double ratio = top
            ? settings.LayoutTopCoverRatio.Value
            : settings.LayoutBottomCoverRatio.Value;
        double maximumRatio = top
            ? YokkoGameplaySettings.MaximumTopCoverRatio
            : YokkoGameplaySettings.MaximumBottomCoverRatio;
        return playfieldHeight()
               * (float)Math.Clamp(ratio, 0, maximumRatio);
    }

    private float playfieldHeight()
    {
        (Vector2 topLeft, Vector2 bottomRight) =
            GameplayLayoutGeometry.BoundsIn(this, playfield);
        return Math.Max(1, bottomRight.Y - topLeft.Y);
    }

    private void applyMetric(
        LayoutElementKind kind,
        LayoutMetricField field,
        double rawValue)
    {
        LayoutTransformTarget target = targetFor(kind);
        if (!target.CanEdit || double.IsNaN(rawValue))
            return;

        selectTarget(target);
        beginChange();

        float value = (float)Math.Clamp(rawValue, -10000, 10000);
        Vector2 currentPosition = target.Position;
        Vector2 currentSize = new(target.DrawWidth, target.DrawHeight);
        Vector2 requestedPosition = currentPosition;
        Vector2 requestedSize = currentSize;

        switch (field)
        {
            case LayoutMetricField.X:
                requestedPosition.X = value;
                break;

            case LayoutMetricField.Y:
                requestedPosition.Y = value;
                break;

            case LayoutMetricField.Width:
                requestedSize.X = Math.Max(8, value);
                if (target.AspectLocked)
                {
                    requestedSize.Y = requestedSize.X
                                      / target.LockedAspectRatio;
                }
                break;

            case LayoutMetricField.Height:
                requestedSize.Y = Math.Max(8, value);
                if (target.AspectLocked)
                {
                    requestedSize.X = requestedSize.Y
                                      * target.LockedAspectRatio;
                }
                break;
        }

        if (requestedSize != currentSize)
        {
            target.ResizeBy(
                ResizeEdges.Right | ResizeEdges.Bottom,
                requestedSize - currentSize);
        }

        if (requestedPosition != currentPosition)
            target.MoveBy(requestedPosition - currentPosition);
    }

    private void centreSelected(bool horizontal)
    {
        LayoutTransformTarget target = selectedTarget;
        if (target == null || !target.CanEdit)
            return;

        beginChange();
        Vector2 delta = Vector2.Zero;
        if (horizontal)
        {
            delta.X = DrawWidth / 2
                      - (target.X + target.DrawWidth / 2);
        }
        else
        {
            delta.Y = DrawHeight / 2
                      - (target.Y + target.DrawHeight / 2);
        }

        target.MoveBy(delta);
    }

    private Vector2 snapTargetMove(
        LayoutTransformTarget moving,
        Vector2 requestedDelta,
        bool bypass)
    {
        if (bypass || !moving.CanEdit)
        {
            clearSnapGuides();
            return requestedDelta;
        }

        float left = moving.X + requestedDelta.X;
        float top = moving.Y + requestedDelta.Y;
        float width = moving.DrawWidth;
        float height = moving.DrawHeight;

        List<float> xCandidates = new()
        {
            0,
            DrawWidth / 2,
            DrawWidth,
        };
        List<float> yCandidates = new()
        {
            0,
            DrawHeight / 2,
            DrawHeight,
        };

        foreach (LayoutTransformTarget target in allTargets())
        {
            if (target == moving || target.EditorHidden)
                continue;

            xCandidates.Add(target.X);
            xCandidates.Add(target.X + target.DrawWidth / 2);
            xCandidates.Add(target.X + target.DrawWidth);
            yCandidates.Add(target.Y);
            yCandidates.Add(target.Y + target.DrawHeight / 2);
            yCandidates.Add(target.Y + target.DrawHeight);
        }

        (float xAdjustment, float? xGuide) = findBestSnap(
            [left, left + width / 2, left + width],
            xCandidates);
        (float yAdjustment, float? yGuide) = findBestSnap(
            [top, top + height / 2, top + height],
            yCandidates);

        showSnapGuides(xGuide, yGuide);
        return requestedDelta + new Vector2(xAdjustment, yAdjustment);
    }

    private static (float Adjustment, float? Guide) findBestSnap(
        IReadOnlyList<float> movingAnchors,
        IReadOnlyList<float> candidates)
    {
        float bestDistance = snapThreshold + 1;
        float bestAdjustment = 0;
        float? guide = null;

        foreach (float anchor in movingAnchors)
        {
            foreach (float candidate in candidates)
            {
                float distance = Math.Abs(candidate - anchor);
                if (distance > snapThreshold || distance >= bestDistance)
                    continue;

                bestDistance = distance;
                bestAdjustment = candidate - anchor;
                guide = candidate;
            }
        }

        return (bestAdjustment, guide);
    }

    private void showSnapGuides(float? x, float? y)
    {
        if (x.HasValue)
        {
            verticalSnapGuide.X = x.Value - verticalSnapGuide.Width / 2;
            verticalSnapGuide.FadeTo(0.9f, 60, Easing.OutQuint);
        }
        else
            verticalSnapGuide.FadeOut(80, Easing.OutQuint);

        if (y.HasValue)
        {
            horizontalSnapGuide.Y = y.Value - horizontalSnapGuide.Height / 2;
            horizontalSnapGuide.FadeTo(0.9f, 60, Easing.OutQuint);
        }
        else
            horizontalSnapGuide.FadeOut(80, Easing.OutQuint);
    }

    private void clearSnapGuides()
    {
        verticalSnapGuide?.FadeOut(80, Easing.OutQuint);
        horizontalSnapGuide?.FadeOut(80, Easing.OutQuint);
    }

    private readonly record struct LayoutElementMetrics(
        float X,
        float Y,
        float Width,
        float Height);

    private partial class CoverPanel : CompositeDrawable
    {
        private readonly CoverRow topRow;
        private readonly CoverRow bottomRow;

        public CoverPanel(
            Action<bool> setTopEnabled,
            Action<double> setTopHeight,
            Action<bool> setBottomEnabled,
            Action<double> setBottomHeight)
        {
            Anchor = Anchor.TopRight;
            Origin = Anchor.TopRight;
            Position = new Vector2(-18, 466);
            Size = new Vector2(320, 178);
            Depth = -100;
            Masking = true;
            CornerRadius = 8;
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
                    RelativeSizeAxes = Axes.X,
                    Height = 4,
                    Colour = HomeControlColours.Pink,
                },
                new SpriteText
                {
                    Position = new Vector2(12, 10),
                    Text = YokkoStrings.Get(
                        "gameplay.layout_editor.covers"),
                    Font = LayoutEditorTypography.Bold(11),
                    Colour = HomeControlColours.Navy,
                },
                topRow = new CoverRow(
                    YokkoStrings.Get(
                        "gameplay.layout_editor.top_cover"),
                    setTopEnabled,
                    setTopHeight)
                {
                    Position = new Vector2(12, 40),
                },
                bottomRow = new CoverRow(
                    YokkoStrings.Get(
                        "gameplay.layout_editor.bottom_cover"),
                    setBottomEnabled,
                    setBottomHeight)
                {
                    Position = new Vector2(12, 92),
                },
                new SpriteText
                {
                    Position = new Vector2(12, 148),
                    Text = YokkoStrings.Get(
                        "gameplay.layout_editor.cover_hint"),
                    Font = LayoutEditorTypography.Regular(8),
                    Colour = new Color4(
                        HomeControlColours.Navy.R,
                        HomeControlColours.Navy.G,
                        HomeControlColours.Navy.B,
                        0.64f),
                },
            };
        }

        internal void SetState(
            bool topEnabled,
            float topHeight,
            bool bottomEnabled,
            float bottomHeight)
        {
            topRow.SetState(topEnabled, topHeight);
            bottomRow.SetState(bottomEnabled, bottomHeight);
        }
    }

    private partial class CoverRow : CompositeDrawable
    {
        private readonly NumericField heightField;
        private readonly CoverToggleButton toggleButton;

        public CoverRow(
            LocalisableString label,
            Action<bool> setEnabled,
            Action<double> setHeight)
        {
            Size = new Vector2(296, 46);
            Masking = true;
            CornerRadius = 5;
            BorderThickness = 1;
            BorderColour = new Color4(
                HomeControlColours.Navy.R,
                HomeControlColours.Navy.G,
                HomeControlColours.Navy.B,
                0.28f);

            InternalChildren = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.White,
                },
                new Box
                {
                    RelativeSizeAxes = Axes.Y,
                    Width = 4,
                    Colour = HomeControlColours.Pink,
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
                heightField = new NumericField(
                    "H",
                    setHeight)
                {
                    Position = new Vector2(88, 3),
                },
                toggleButton = new CoverToggleButton(setEnabled)
                {
                    Position = new Vector2(232, 5),
                    Size = new Vector2(60, 36),
                },
            };
        }

        internal void SetState(bool enabled, float height)
        {
            heightField.SetValue(height);
            heightField.ReadOnly = !enabled;
            toggleButton.SetValue(enabled);
        }
    }

    private partial class CoverToggleButton : ClickableContainer
    {
        private readonly Action<bool> changed;
        private readonly Box background;
        private readonly SpriteText text;
        private bool enabled;

        public CoverToggleButton(Action<bool> changed)
        {
            this.changed = changed;
            Masking = true;
            CornerRadius = 5;
            BorderThickness = 1.5f;
            BorderColour = HomeControlColours.Navy;
            Action = () => this.changed(!enabled);

            InternalChildren = new Drawable[]
            {
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                },
                text = new SpriteText
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Font = LayoutEditorTypography.Bold(8),
                    Colour = HomeControlColours.Navy,
                },
            };
            SetValue(false);
        }

        internal void SetValue(bool next)
        {
            enabled = next;
            text.Text = YokkoStrings.Get(enabled
                ? "gameplay.layout_editor.remove_cover"
                : "gameplay.layout_editor.add_cover");
            background.Colour = enabled
                ? HomeControlColours.Yellow
                : HomeControlColours.PaleCyan;
            BorderColour = enabled
                ? HomeControlColours.Pink
                : HomeControlColours.Navy;
        }

        protected override bool OnHover(HoverEvent e)
        {
            background.FadeColour(
                enabled
                    ? HomeControlColours.Yellow
                    : Color4.White,
                80,
                Easing.OutQuint);
            this.ScaleTo(1.035f, 90, Easing.OutQuint);
            BorderColour = HomeControlColours.Pink;
            return true;
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            this.ScaleTo(1, 110, Easing.OutQuint);
            SetValue(enabled);
        }
    }

    private partial class LayoutInspectorPanel : CompositeDrawable
    {
        private readonly Action<LayoutElementKind> select;
        private readonly Action<LayoutElementKind, bool> setHidden;
        private readonly Action<LayoutElementKind, bool> setLocked;
        private readonly Action<LayoutElementKind, bool> setAspectLocked;
        private readonly Dictionary<LayoutElementKind, LayerRow> layerRows =
            new();
        private readonly NumericField xField;
        private readonly NumericField yField;
        private readonly NumericField widthField;
        private readonly NumericField heightField;
        private readonly ToggleIconButton aspectButton;
        private readonly SpriteText stepText;
        private LayoutElementKind selected = LayoutElementKind.Playfield;

        public LayoutInspectorPanel(
            Action<LayoutElementKind> select,
            Action<LayoutElementKind, bool> setHidden,
            Action<LayoutElementKind, bool> setLocked,
            Action<LayoutElementKind, bool> setAspectLocked,
            Action<LayoutElementKind, LayoutMetricField, double> applyMetric,
            Action<bool> centre,
            Action cycleStep)
        {
            this.select = select;
            this.setHidden = setHidden;
            this.setLocked = setLocked;
            this.setAspectLocked = setAspectLocked;

            Anchor = Anchor.TopRight;
            Origin = Anchor.TopRight;
            Position = new Vector2(-18, 92);
            Size = new Vector2(320, 360);
            Depth = -100;
            Masking = true;
            CornerRadius = 8;
            BorderThickness = 1.5f;
            BorderColour = HomeControlColours.Navy;

            LayerRow playfieldRow;
            LayerRow hudRow;
            LayerRow timingRow;

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
                    Position = new Vector2(12, 10),
                    Text = YokkoStrings.Get(
                        "gameplay.layout_editor.inspector"),
                    Font = LayoutEditorTypography.Bold(11),
                    Colour = HomeControlColours.Navy,
                },
                playfieldRow = createLayerRow(
                    LayoutElementKind.Playfield,
                    YokkoStrings.Get(
                        "gameplay.layout_editor.layer.playfield"),
                    42),
                hudRow = createLayerRow(
                    LayoutElementKind.Hud,
                    YokkoStrings.Get(
                        "gameplay.layout_editor.layer.hud"),
                    76),
                timingRow = createLayerRow(
                    LayoutElementKind.TimingBar,
                    YokkoStrings.Get(
                        "gameplay.layout_editor.layer.timing_bar"),
                    110),
                new Box
                {
                    Position = new Vector2(12, 150),
                    Size = new Vector2(296, 1),
                    Colour = new Color4(
                        HomeControlColours.Navy.R,
                        HomeControlColours.Navy.G,
                        HomeControlColours.Navy.B,
                        0.16f),
                },
                xField = createNumericField(
                    "X",
                    LayoutMetricField.X,
                    new Vector2(12, 164),
                    applyMetric),
                yField = createNumericField(
                    "Y",
                    LayoutMetricField.Y,
                    new Vector2(164, 164),
                    applyMetric),
                widthField = createNumericField(
                    "W",
                    LayoutMetricField.Width,
                    new Vector2(12, 216),
                    applyMetric),
                heightField = createNumericField(
                    "H",
                    LayoutMetricField.Height,
                    new Vector2(164, 216),
                    applyMetric),
                aspectButton = new ToggleIconButton(
                    FontAwesome.Solid.Link,
                    FontAwesome.Solid.Unlink,
                    value => setAspectLocked(selected, value))
                {
                    Position = new Vector2(12, 275),
                    Size = new Vector2(34),
                },
                new LayoutActionButton(
                    YokkoStrings.Get(
                        "gameplay.layout_editor.centre_x"),
                    FontAwesome.Solid.ArrowsAltH,
                    () => centre(true))
                {
                    Position = new Vector2(52, 274),
                    Size = new Vector2(96, 34),
                },
                new LayoutActionButton(
                    YokkoStrings.Get(
                        "gameplay.layout_editor.centre_y"),
                    FontAwesome.Solid.ArrowsAltV,
                    () => centre(false))
                {
                    Position = new Vector2(154, 274),
                    Size = new Vector2(96, 34),
                },
                new ClickableContainer
                {
                    Position = new Vector2(256, 274),
                    Size = new Vector2(52, 34),
                    Action = cycleStep,
                    Masking = true,
                    CornerRadius = 6,
                    BorderThickness = 1.5f,
                    BorderColour = HomeControlColours.Navy,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = HomeControlColours.PaleCyan,
                        },
                        stepText = new SpriteText
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Font = LayoutEditorTypography.Bold(9),
                            Colour = HomeControlColours.Navy,
                        },
                    },
                },
                new SpriteText
                {
                    Position = new Vector2(12, 324),
                    Text = YokkoStrings.Get(
                        "gameplay.layout_editor.snap_hint"),
                    Font = LayoutEditorTypography.Regular(8),
                    Colour = new Color4(
                        HomeControlColours.Navy.R,
                        HomeControlColours.Navy.G,
                        HomeControlColours.Navy.B,
                        0.64f),
                },
            };

            layerRows[LayoutElementKind.Playfield] = playfieldRow;
            layerRows[LayoutElementKind.Hud] = hudRow;
            layerRows[LayoutElementKind.TimingBar] = timingRow;
            Select(LayoutElementKind.Playfield);
            SetStep(1);
        }

        internal void Select(LayoutElementKind? kind)
        {
            if (!kind.HasValue)
                return;

            selected = kind.Value;
            foreach ((LayoutElementKind rowKind, LayerRow row) in layerRows)
                row.SetSelected(rowKind == selected);
        }

        internal void SetLayerState(
            LayoutElementKind kind,
            bool locked,
            bool hidden,
            bool aspectLocked)
        {
            layerRows[kind].SetState(locked, hidden);
            if (kind != selected)
                return;

            aspectButton.SetValue(aspectLocked);
            setFieldsReadOnly(locked || hidden);
        }

        internal void SetMetrics(LayoutElementMetrics metrics)
        {
            xField.SetValue(metrics.X);
            yField.SetValue(metrics.Y);
            widthField.SetValue(metrics.Width);
            heightField.SetValue(metrics.Height);
        }

        internal void SetStep(float step)
        {
            stepText.Text = $"{step:0}px";
        }

        private LayerRow createLayerRow(
            LayoutElementKind kind,
            LocalisableString text,
            float y) =>
            new(
                text,
                () => select(kind),
                hidden => setHidden(kind, hidden),
                locked => setLocked(kind, locked))
            {
                Position = new Vector2(12, y),
            };

        private NumericField createNumericField(
            string label,
            LayoutMetricField field,
            Vector2 position,
            Action<LayoutElementKind, LayoutMetricField, double> apply) =>
            new(label, value => apply(selected, field, value))
            {
                Position = position,
            };

        private void setFieldsReadOnly(bool value)
        {
            xField.ReadOnly = value;
            yField.ReadOnly = value;
            widthField.ReadOnly = value;
            heightField.ReadOnly = value;
            aspectButton.SetAvailable(!value);
        }
    }

    private partial class LayerRow : ClickableContainer
    {
        private readonly Box background;
        private readonly Box accent;
        private readonly ToggleIconButton visibilityButton;
        private readonly ToggleIconButton lockButton;
        private bool selected;

        public LayerRow(
            LocalisableString text,
            Action select,
            Action<bool> setHidden,
            Action<bool> setLocked)
        {
            Action = select;
            Size = new Vector2(296, 32);
            Masking = true;
            CornerRadius = 5;
            BorderThickness = 1;
            BorderColour = new Color4(
                HomeControlColours.Navy.R,
                HomeControlColours.Navy.G,
                HomeControlColours.Navy.B,
                0.32f);

            InternalChildren = new Drawable[]
            {
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.White,
                },
                accent = new Box
                {
                    RelativeSizeAxes = Axes.Y,
                    Width = 4,
                    Colour = HomeControlColours.Cyan,
                },
                new SpriteText
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    X = 12,
                    Text = text,
                    Font = LayoutEditorTypography.Bold(9),
                    Colour = HomeControlColours.Navy,
                },
                visibilityButton = new ToggleIconButton(
                    FontAwesome.Solid.EyeSlash,
                    FontAwesome.Solid.Eye,
                    setHidden)
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    X = -39,
                    Size = new Vector2(30),
                },
                lockButton = new ToggleIconButton(
                    FontAwesome.Solid.Lock,
                    FontAwesome.Solid.UnlockAlt,
                    setLocked)
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    X = -5,
                    Size = new Vector2(30),
                },
            };
        }

        internal void SetSelected(bool selected)
        {
            this.selected = selected;
            background.Colour = selected
                ? HomeControlColours.PaleCyan
                : Color4.White;
            accent.Colour = selected
                ? HomeControlColours.Yellow
                : HomeControlColours.Cyan;
            BorderColour = selected
                ? HomeControlColours.Navy
                : new Color4(
                    HomeControlColours.Navy.R,
                    HomeControlColours.Navy.G,
                    HomeControlColours.Navy.B,
                    0.32f);
        }

        internal void SetState(bool locked, bool hidden)
        {
            lockButton.SetValue(locked);
            visibilityButton.SetValue(hidden);
            Alpha = hidden ? 0.62f : 1;
        }

        protected override bool OnHover(HoverEvent e)
        {
            if (!selected)
            {
                background.FadeColour(
                    HomeControlColours.PaleCyan,
                    90,
                    Easing.OutQuint);
                BorderColour = HomeControlColours.Cyan;
            }

            return true;
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            background.FadeColour(
                selected
                    ? HomeControlColours.PaleCyan
                    : Color4.White,
                110,
                Easing.OutQuint);
            BorderColour = selected
                ? HomeControlColours.Navy
                : new Color4(
                    HomeControlColours.Navy.R,
                    HomeControlColours.Navy.G,
                    HomeControlColours.Navy.B,
                    0.32f);
        }
    }

    private partial class ToggleIconButton : ClickableContainer
    {
        private readonly IconUsage trueIcon;
        private readonly IconUsage falseIcon;
        private readonly Action<bool> changed;
        private readonly Box background;
        private readonly SpriteIcon icon;
        private bool value;
        private bool available = true;
        private bool hovered;

        public ToggleIconButton(
            IconUsage trueIcon,
            IconUsage falseIcon,
            Action<bool> changed)
        {
            this.trueIcon = trueIcon;
            this.falseIcon = falseIcon;
            this.changed = changed;
            Masking = true;
            CornerRadius = 5;
            BorderThickness = 1;
            BorderColour = new Color4(
                HomeControlColours.Navy.R,
                HomeControlColours.Navy.G,
                HomeControlColours.Navy.B,
                0.42f);
            Action = () =>
            {
                if (!available)
                    return;

                value = !value;
                updateVisual();
                changed(value);
            };
            InternalChildren = new Drawable[]
            {
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.White,
                },
                icon = new SpriteIcon
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Size = new Vector2(14),
                    Colour = HomeControlColours.Navy,
                },
            };
            updateVisual();
        }

        internal void SetValue(bool next)
        {
            value = next;
            updateVisual();
        }

        internal void SetAvailable(bool next)
        {
            available = next;
            Alpha = next ? 1 : 0.42f;
        }

        protected override bool OnHover(HoverEvent e)
        {
            if (!available)
                return false;

            hovered = true;
            updateVisual();
            this.ScaleTo(1.06f, 90, Easing.OutQuint);
            return true;
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            hovered = false;
            updateVisual();
            this.ScaleTo(1, 110, Easing.OutQuint);
        }

        private void updateVisual()
        {
            icon.Icon = value ? trueIcon : falseIcon;
            background.Colour = value
                ? HomeControlColours.PaleCyan
                : hovered
                    ? HomeControlColours.PaleCyan
                    : Color4.White;
            BorderColour = value
                ? HomeControlColours.Pink
                : hovered
                    ? HomeControlColours.Cyan
                : new Color4(
                    HomeControlColours.Navy.R,
                    HomeControlColours.Navy.G,
                    HomeControlColours.Navy.B,
                    0.42f);
        }
    }

    private partial class NumericField : CompositeDrawable
    {
        private readonly NumericTextBox textBox;

        internal bool ReadOnly
        {
            get => textBox.ReadOnly;
            set
            {
                textBox.ReadOnly = value;
                Alpha = value ? 0.52f : 1;
            }
        }

        public NumericField(string label, Action<double> committed)
        {
            Size = new Vector2(140, 40);
            InternalChildren = new Drawable[]
            {
                new SpriteText
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Text = label,
                    Font = LayoutEditorTypography.Bold(9),
                    Colour = HomeControlColours.Navy,
                },
                textBox = new NumericTextBox
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    Size = new Vector2(108, 36),
                },
            };
            textBox.OnCommit += (_, _) =>
            {
                if (double.TryParse(
                        textBox.Current.Value,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out double value))
                {
                    committed(value);
                }
            };
        }

        internal void SetValue(float value)
        {
            if (textBox.HasFocus)
                return;

            string text = value.ToString("0.0", CultureInfo.InvariantCulture);
            if (textBox.Current.Value != text)
                textBox.Current.Value = text;
        }
    }

    private partial class NumericTextBox : BasicTextBox
    {
        protected override float LeftRightPadding => 9;

        public NumericTextBox()
        {
            Masking = true;
            CornerRadius = 5;
            BorderThickness = 1.5f;
            BorderColour = HomeControlColours.Navy;
            BackgroundUnfocused = Color4.White;
            BackgroundFocused = HomeControlColours.PaleCyan;
            FontSize = 15;
            CommitOnFocusLost = true;
            ReleaseFocusOnCommit = true;
        }

        protected override Drawable GetDrawableCharacter(char c) =>
            new SpriteText
            {
                Text = c.ToString(),
                Font = LayoutEditorTypography.Regular(9),
                Colour = HomeControlColours.Navy,
            };

        protected override SpriteText CreatePlaceholder() => new()
        {
            Font = LayoutEditorTypography.Regular(8),
            Colour = new Color4(
                HomeControlColours.Navy.R,
                HomeControlColours.Navy.G,
                HomeControlColours.Navy.B,
                0.48f),
        };
    }
}
