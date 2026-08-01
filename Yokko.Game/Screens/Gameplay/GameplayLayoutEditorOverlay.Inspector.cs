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
    private const float minimumVisibleTargetArea = 24;
    private const float defaultCoverHeight = 120;

    private readonly Dictionary<LayoutElementKind, float> originalElementAlpha =
        new();

    private Box verticalSnapGuide;
    private Box horizontalSnapGuide;
    private LayoutInspectorPanel inspector;
    private CoverPanel coverPanel;
    private float nudgeStep = 1;

    internal bool TimingBarLockedForTest => timingBarTarget.IsLocked;

    internal bool HudHiddenForTest => informationTarget.EditorHidden;

    internal bool AccuracyHiddenForTest => accuracyTarget.EditorHidden;

    internal bool ProgressHiddenForTest => progressTarget.EditorHidden;

    internal float TimingBarEditorWidthForTest => timingBarTarget.DrawWidth;

    internal float TimingBarEditorCentreXForTest =>
        timingBarTarget.X + timingBarTarget.DrawWidth / 2;

    internal float ComboEditorCentreXForTest =>
        comboTarget.X + comboTarget.DrawWidth / 2;

    internal float JudgementEditorCentreYForTest =>
        judgementTarget.Y + judgementTarget.DrawHeight / 2;

    internal float JudgementEditorWidthForTest =>
        judgementTarget.DrawWidth;

    internal float ComboEditorLeftForTest => comboTarget.X;

    internal float ComboEditorRightForTest =>
        comboTarget.X + comboTarget.DrawWidth;

    internal float TopCoverHandleTopForTest => topCoverHandle.Y;

    internal float BottomCoverHandleBottomForTest =>
        bottomCoverHandle.Y + bottomCoverHandle.DrawHeight;

    internal float TopCoverHeightForTest => coverHeight(true);

    internal float BottomCoverHeightForTest => coverHeight(false);

    internal bool TopCoverEnabledForTest =>
        settings.LayoutTopCoverRatio.Value > 0.0001;

    internal bool BottomCoverEnabledForTest =>
        settings.LayoutBottomCoverRatio.Value > 0.0001;

    internal void SetTimingBarLockedForTest(bool locked) =>
        setLayerLocked(LayoutElementKind.TimingBar, locked);

    internal void SetHudHiddenForTest(bool hidden) =>
        setLayerHidden(LayoutElementKind.Information, hidden);

    internal void SetAccuracyHiddenForTest(bool hidden) =>
        setLayerHidden(LayoutElementKind.Accuracy, hidden);

    internal void SetProgressHiddenForTest(bool hidden) =>
        setLayerHidden(LayoutElementKind.Progress, hidden);

    internal void SetTimingBarWidthForTest(double width) =>
        applyMetric(
            LayoutElementKind.TimingBar,
            LayoutMetricField.Width,
            width);

    internal void MoveComboForTest(Vector2 delta) =>
        comboTarget.MoveBy(delta);

    internal void MoveAccuracyForTest(Vector2 delta) =>
        accuracyTarget.MoveBy(delta);

    internal void MoveProgressForTest(Vector2 delta) =>
        progressTarget.MoveBy(delta);

    internal void MoveInformationForTest(Vector2 delta) =>
        informationTarget.MoveBy(delta);

    internal void MoveComboSafelyForTest(Vector2 delta) =>
        comboTarget.MoveBy(
            snapTargetMove(comboTarget, delta, true));

    internal void SelectComboForTest() => selectTarget(comboTarget);

    internal void CentreSelectedForTest() => centreSelectedBoth();

    internal void ResizeJudgementForTest(Vector2 delta) =>
        judgementTarget.ResizeBy(
            ResizeEdges.Right | ResizeEdges.Bottom,
            delta);

    internal void SetTopCoverEnabledForTest(bool enabled) =>
        setCoverEnabled(true, enabled);

    internal void SetBottomCoverEnabledForTest(bool enabled) =>
        setCoverEnabled(false, enabled);

    internal void SetTopCoverHeightForTest(double height) =>
        applyCoverHeight(true, height);

    internal void SetBottomCoverHeightForTest(double height) =>
        applyCoverHeight(false, height);

    internal void DragTopCoverResizeForTest(float height)
    {
        (Vector2 topLeft, Vector2 bottomRight) =
            GameplayLayoutGeometry.BoundsIn(this, playfield);
        float requestedY = Math.Clamp(
            topLeft.Y + height,
            topLeft.Y,
            bottomRight.Y);
        updateTopCover(ToScreenSpace(
            new Vector2(topLeft.X, requestedY)));
    }

    internal Vector2 SnapTimingBarMoveForTest(
        Vector2 delta,
        bool bypass) =>
        snapTargetMove(timingBarTarget, delta, bypass);

    private enum LayoutElementKind
    {
        Playfield,
        Accuracy,
        Progress,
        Information,
        TimingBar,
        Combo,
        Judgement,
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
        return createToolWindow(
            GameplayLayoutEditorToolWindow.Inspector,
            inspector);
    }

    private Drawable createCoverPanel()
    {
        coverPanel = new CoverPanel(
            enabled => setCoverEnabled(true, enabled),
            height => applyCoverHeight(true, height),
            enabled => setCoverEnabled(false, enabled),
            height => applyCoverHeight(false, height));
        return createToolWindow(
            GameplayLayoutEditorToolWindow.LaneCovers,
            coverPanel);
    }

    private void beginEditorSession()
    {
        originalElementAlpha[LayoutElementKind.Playfield] = playfield.Alpha;
        originalElementAlpha[LayoutElementKind.Accuracy] =
            hud.AccuracyLayoutDrawable.Alpha;
        originalElementAlpha[LayoutElementKind.Progress] =
            hud.ProgressLayoutDrawable.Alpha;
        originalElementAlpha[LayoutElementKind.Information] =
            hud.InformationLayoutDrawable.Alpha;
        originalElementAlpha[LayoutElementKind.TimingBar] = timingBar.Alpha;
        originalElementAlpha[LayoutElementKind.Combo] = comboReadout.Alpha;
        originalElementAlpha[LayoutElementKind.Judgement] =
            judgementReadout.Alpha;
        setComboEditorPreview(true);
        setJudgementEditorPreview(true);

        foreach (LayoutTransformTarget target in allTargets())
        {
            target.SetEditorHidden(false);
            target.SetLocked(false);
            target.SetAspectLocked(false);
        }

        applyElementAlpha(LayoutElementKind.Playfield, false);
        applyElementAlpha(LayoutElementKind.Accuracy, false);
        applyElementAlpha(LayoutElementKind.Progress, false);
        applyElementAlpha(LayoutElementKind.Information, false);
        applyElementAlpha(LayoutElementKind.TimingBar, false);
        applyElementAlpha(LayoutElementKind.Combo, false);
        applyElementAlpha(LayoutElementKind.Judgement, false);
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

        setComboEditorPreview(false);
        setJudgementEditorPreview(false);
        restoreOriginalAlpha(LayoutElementKind.Playfield);
        restoreOriginalAlpha(LayoutElementKind.Accuracy);
        restoreOriginalAlpha(LayoutElementKind.Progress);
        restoreOriginalAlpha(LayoutElementKind.Information);
        restoreOriginalAlpha(LayoutElementKind.TimingBar);
        restoreOriginalAlpha(LayoutElementKind.Combo);
        restoreOriginalAlpha(LayoutElementKind.Judgement);
        clearSnapGuides();
    }

    private IEnumerable<LayoutTransformTarget> allTargets()
    {
        yield return playfieldTarget;
        yield return accuracyTarget;
        yield return progressTarget;
        yield return informationTarget;
        yield return timingBarTarget;
        yield return comboTarget;
        yield return judgementTarget;
    }

    private LayoutTransformTarget targetFor(LayoutElementKind kind) =>
        kind switch
        {
            LayoutElementKind.Playfield => playfieldTarget,
            LayoutElementKind.Accuracy => accuracyTarget,
            LayoutElementKind.Progress => progressTarget,
            LayoutElementKind.Information => informationTarget,
            LayoutElementKind.TimingBar => timingBarTarget,
            LayoutElementKind.Combo => comboTarget,
            LayoutElementKind.Judgement => judgementTarget,
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

    private Drawable drawableFor(LayoutElementKind kind) =>
        kind switch
        {
            LayoutElementKind.Playfield => playfield,
            LayoutElementKind.Accuracy => hud.AccuracyLayoutDrawable,
            LayoutElementKind.Progress => hud.ProgressLayoutDrawable,
            LayoutElementKind.Information => hud.InformationLayoutDrawable,
            LayoutElementKind.TimingBar => timingBar,
            LayoutElementKind.Combo =>
                playfield.SkinComboLayoutDrawable ?? comboReadout,
            LayoutElementKind.Judgement =>
                playfield.SkinJudgementLayoutDrawable
                ?? judgementReadout,
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

    private Drawable miniDrawableFor(LayoutElementKind kind) =>
        kind switch
        {
            LayoutElementKind.Playfield => miniPlayfield,
            LayoutElementKind.Accuracy => miniAccuracy,
            LayoutElementKind.Progress => miniProgress,
            LayoutElementKind.Information => miniInformation,
            LayoutElementKind.TimingBar => miniTimingBar,
            LayoutElementKind.Combo => miniCombo,
            LayoutElementKind.Judgement => miniJudgement,
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
        if (kind == LayoutElementKind.Combo
            && playfield.UsesSkinJudgementOverlay)
        {
            comboReadout.Alpha = 0;
            playfield.SetSkinComboEditorPreview(
                IsEditing && !hidden);
        }
        else if (kind == LayoutElementKind.Judgement
            && playfield.UsesSkinJudgementOverlay)
        {
            drawable.Alpha = 0;
            playfield.SetSkinJudgementEditorPreview(
                IsEditing && !hidden);
        }
        else
        {
            drawable.Alpha = hidden
                ? 0
                : kind == LayoutElementKind.TimingBar
                    ? liveSettings.ShowTimingBar() ? 1 : 0
                : kind is LayoutElementKind.Combo
                    or LayoutElementKind.Judgement
                    ? 1
                    : originalElementAlpha.GetValueOrDefault(kind, 1);
        }

        Drawable miniDrawable = miniDrawableFor(kind);
        if (miniDrawable != null)
            miniDrawable.Alpha = hidden ? 0 : 1;
    }

    private void restoreOriginalAlpha(LayoutElementKind kind)
    {
        if (kind == LayoutElementKind.Combo
            && playfield.UsesSkinJudgementOverlay)
        {
            comboReadout.Alpha = 0;
            playfield.SetSkinComboEditorPreview(false);
        }
        else
        {
            drawableFor(kind).Alpha = kind == LayoutElementKind.TimingBar
                ? liveSettings.ShowTimingBar() ? 1 : 0
                : originalElementAlpha.GetValueOrDefault(kind, 1);
        }
        Drawable miniDrawable = miniDrawableFor(kind);
        if (miniDrawable != null)
            miniDrawable.Alpha = 1;
    }

    private void setJudgementEditorPreview(bool preview)
    {
        bool useSkinPreview = preview
                              && playfield.UsesSkinJudgementOverlay;
        playfield.SetSkinJudgementEditorPreview(useSkinPreview);
        judgementReadout.SetEditorPreview(preview && !useSkinPreview);
    }

    private void setComboEditorPreview(bool preview)
    {
        bool useSkinPreview = preview
                              && playfield.UsesSkinJudgementOverlay;
        playfield.SetSkinComboEditorPreview(useSkinPreview);
        comboReadout.SetEditorPreview(preview && !useSkinPreview);
        if (playfield.UsesSkinJudgementOverlay)
            comboReadout.Alpha = 0;
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

    private void centreSelectedBoth()
    {
        LayoutTransformTarget target = selectedTarget;
        if (target == null || !target.CanEdit)
            return;

        beginChange();
        Vector2 requestedDelta = new(
            DrawWidth / 2 - (target.X + target.DrawWidth / 2),
            DrawHeight / 2 - (target.Y + target.DrawHeight / 2));
        target.MoveBy(
            clampMoveToViewport(target, requestedDelta));
    }

    private Vector2 snapTargetMove(
        LayoutTransformTarget moving,
        Vector2 requestedDelta,
        bool bypass)
    {
        if (!moving.CanEdit)
        {
            clearSnapGuides();
            return requestedDelta;
        }

        Vector2 adjustedDelta = requestedDelta;
        if (bypass)
        {
            clearSnapGuides();
            return clampMoveToViewport(moving, adjustedDelta);
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
        adjustedDelta += new Vector2(xAdjustment, yAdjustment);
        return clampMoveToViewport(moving, adjustedDelta);
    }

    private Vector2 clampMoveToViewport(
        LayoutTransformTarget moving,
        Vector2 requestedDelta)
    {
        Vector2 requestedPosition = moving.Position + requestedDelta;
        float width = Math.Max(1, moving.DrawWidth);
        float height = Math.Max(1, moving.DrawHeight);

        requestedPosition.X = clampTargetAxis(
            requestedPosition.X,
            width,
            DrawWidth,
            moving.Position.X);
        requestedPosition.Y = clampTargetAxis(
            requestedPosition.Y,
            height,
            DrawHeight,
            moving.Position.Y);
        return requestedPosition - moving.Position;
    }

    private static float clampTargetAxis(
        float position,
        float targetSize,
        float viewportSize,
        float currentPosition)
    {
        float minimum;
        float maximum;
        if (targetSize <= viewportSize)
        {
            minimum = 0;
            maximum = viewportSize - targetSize;
        }
        else
        {
            float visibleArea = Math.Min(
                minimumVisibleTargetArea,
                viewportSize);
            minimum = visibleArea - targetSize;
            maximum = viewportSize - visibleArea;
        }

        // Some default layouts intentionally sit a few pixels beyond the
        // viewport (for example the top-right HUD at X=-20 and the bottom
        // timing bar). Preserve that current position as a valid boundary so
        // the first drag frame does not snap the element into the viewport.
        return Math.Clamp(
            position,
            Math.Min(currentPosition, minimum),
            Math.Max(currentPosition, maximum));
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
            Position = new Vector2(-18, 462);
            Scale = new Vector2(1.08f);
            Size = new Vector2(360, 178);
            Depth = -100;
            Masking = true;
            CornerRadius = 11;
            BorderThickness = 1.25f;
            BorderColour = new Color4(
                HomeControlColours.Navy.R,
                HomeControlColours.Navy.G,
                HomeControlColours.Navy.B,
                0.72f);

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
                    Font = LayoutEditorTypography.Bold(12),
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
                    Font = LayoutEditorTypography.Regular(9),
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
            Size = new Vector2(336, 46);
            Masking = true;
            CornerRadius = 7;
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
                    Font = LayoutEditorTypography.Bold(10),
                    Colour = HomeControlColours.Navy,
                },
                heightField = new NumericField(
                    "H",
                    setHeight)
                {
                    Position = new Vector2(108, 3),
                },
                toggleButton = new CoverToggleButton(setEnabled)
                {
                    Position = new Vector2(272, 5),
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
            CornerRadius = 7;
            BorderThickness = 1.25f;
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
                    Font = LayoutEditorTypography.Bold(9),
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
            Position = new Vector2(-18, 18);
            Scale = new Vector2(1.08f);
            Size = new Vector2(360, 400);
            Depth = -100;
            Masking = true;
            CornerRadius = 11;
            BorderThickness = 1.25f;
            BorderColour = new Color4(
                HomeControlColours.Navy.R,
                HomeControlColours.Navy.G,
                HomeControlColours.Navy.B,
                0.72f);

            LayerRow playfieldRow;
            LayerRow accuracyRow;
            LayerRow progressRow;
            LayerRow informationRow;
            LayerRow timingRow;
            LayerRow comboRow;
            LayerRow judgementRow;

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
                    Font = LayoutEditorTypography.Bold(12),
                    Colour = HomeControlColours.Navy,
                },
                playfieldRow = createLayerRow(
                    LayoutElementKind.Playfield,
                    YokkoStrings.Get(
                        "gameplay.layout_editor.layer.playfield"),
                    42),
                accuracyRow = createLayerRow(
                    LayoutElementKind.Accuracy,
                    YokkoStrings.Get(
                        "gameplay.layout_editor.layer.accuracy"),
                    68),
                progressRow = createLayerRow(
                    LayoutElementKind.Progress,
                    YokkoStrings.Get(
                        "gameplay.layout_editor.layer.progress"),
                    94),
                informationRow = createLayerRow(
                    LayoutElementKind.Information,
                    YokkoStrings.Get(
                        "gameplay.layout_editor.layer.information"),
                    120),
                timingRow = createLayerRow(
                    LayoutElementKind.TimingBar,
                    YokkoStrings.Get(
                        "gameplay.layout_editor.layer.timing_bar"),
                    146),
                comboRow = createLayerRow(
                    LayoutElementKind.Combo,
                    YokkoStrings.Get(
                        "gameplay.layout_editor.layer.combo"),
                    172),
                judgementRow = createLayerRow(
                    LayoutElementKind.Judgement,
                    YokkoStrings.Get(
                        "gameplay.layout_editor.layer.judgement"),
                    198),
                new Box
                {
                    Position = new Vector2(12, 230),
                    Size = new Vector2(336, 1),
                    Colour = new Color4(
                        HomeControlColours.Navy.R,
                        HomeControlColours.Navy.G,
                        HomeControlColours.Navy.B,
                        0.16f),
                },
                xField = createNumericField(
                    "X",
                    LayoutMetricField.X,
                    new Vector2(12, 240),
                    applyMetric),
                yField = createNumericField(
                    "Y",
                    LayoutMetricField.Y,
                    new Vector2(188, 240),
                    applyMetric),
                widthField = createNumericField(
                    "W",
                    LayoutMetricField.Width,
                    new Vector2(12, 286),
                    applyMetric),
                heightField = createNumericField(
                    "H",
                    LayoutMetricField.Height,
                    new Vector2(188, 286),
                    applyMetric),
                aspectButton = new ToggleIconButton(
                    FontAwesome.Solid.Link,
                    FontAwesome.Solid.Unlink,
                    value => setAspectLocked(selected, value))
                {
                    Position = new Vector2(12, 340),
                    Size = new Vector2(34),
                },
                new LayoutActionButton(
                    YokkoStrings.Get(
                        "gameplay.layout_editor.centre_x"),
                    FontAwesome.Solid.ArrowsAltH,
                    () => centre(true))
                {
                    Position = new Vector2(52, 339),
                    Size = new Vector2(112, 34),
                },
                new LayoutActionButton(
                    YokkoStrings.Get(
                        "gameplay.layout_editor.centre_y"),
                    FontAwesome.Solid.ArrowsAltV,
                    () => centre(false))
                {
                    Position = new Vector2(170, 339),
                    Size = new Vector2(112, 34),
                },
                new ClickableContainer
                {
                    Position = new Vector2(288, 339),
                    Size = new Vector2(60, 34),
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
                            Font = LayoutEditorTypography.Bold(10),
                            Colour = HomeControlColours.Navy,
                        },
                    },
                },
                new SpriteText
                {
                    Position = new Vector2(12, 382),
                    Text = YokkoStrings.Get(
                        "gameplay.layout_editor.snap_hint"),
                    Font = LayoutEditorTypography.Regular(9),
                    Colour = new Color4(
                        HomeControlColours.Navy.R,
                        HomeControlColours.Navy.G,
                        HomeControlColours.Navy.B,
                        0.64f),
                },
            };

            layerRows[LayoutElementKind.Playfield] = playfieldRow;
            layerRows[LayoutElementKind.Accuracy] = accuracyRow;
            layerRows[LayoutElementKind.Progress] = progressRow;
            layerRows[LayoutElementKind.Information] = informationRow;
            layerRows[LayoutElementKind.TimingBar] = timingRow;
            layerRows[LayoutElementKind.Combo] = comboRow;
            layerRows[LayoutElementKind.Judgement] = judgementRow;
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
            Size = new Vector2(336, 26);
            Masking = true;
            CornerRadius = 7;
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
                    Font = LayoutEditorTypography.Bold(10),
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
                    Size = new Vector2(24),
                },
                lockButton = new ToggleIconButton(
                    FontAwesome.Solid.Lock,
                    FontAwesome.Solid.UnlockAlt,
                    setLocked)
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    X = -5,
                    Size = new Vector2(24),
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
            CornerRadius = 7;
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
                    Size = new Vector2(15),
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
        private readonly Action<double> committed;
        private readonly NumericTextBox textBox;
        private float currentValue;

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
            this.committed = committed;
            Size = new Vector2(160, 40);
            InternalChildren = new Drawable[]
            {
                new SpriteText
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Text = label,
                    Font = LayoutEditorTypography.Bold(10),
                    Colour = HomeControlColours.Navy,
                },
                textBox = new NumericTextBox
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    Size = new Vector2(128, 36),
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
                    currentValue = (float)value;
                    this.committed(value);
                }
            };
        }

        internal void SetValue(float value)
        {
            currentValue = value;
            if (textBox.HasFocus)
                return;

            string text = value.ToString("0.0", CultureInfo.InvariantCulture);
            if (textBox.Current.Value != text)
                textBox.Current.Value = text;
        }

        protected override bool OnScroll(ScrollEvent e)
        {
            if (ReadOnly || e.ScrollDelta.Y == 0)
                return false;

            double step = e.ControlPressed
                ? 0.1
                : e.ShiftPressed
                    ? 10
                    : 1;
            double next = currentValue
                          + Math.Sign(e.ScrollDelta.Y) * step;
            currentValue = (float)next;
            textBox.Current.Value = next.ToString(
                "0.0",
                CultureInfo.InvariantCulture);
            committed(next);
            return true;
        }
    }

    private partial class NumericTextBox : BasicTextBox
    {
        protected override float LeftRightPadding => 9;

        public NumericTextBox()
        {
            Masking = true;
            CornerRadius = 7;
            BorderThickness = 1.25f;
            BorderColour = new Color4(
                HomeControlColours.Navy.R,
                HomeControlColours.Navy.G,
                HomeControlColours.Navy.B,
                0.72f);
            BackgroundUnfocused = Color4.White;
            BackgroundFocused = HomeControlColours.PaleCyan;
            FontSize = 18;
            CommitOnFocusLost = true;
            ReleaseFocusOnCommit = true;
        }

        protected override Drawable GetDrawableCharacter(char c) =>
            new SpriteText
            {
                Text = c.ToString(),
                Font = LayoutEditorTypography.Regular(10),
                Colour = HomeControlColours.Navy,
            };

        protected override SpriteText CreatePlaceholder() => new()
        {
            Font = LayoutEditorTypography.Regular(9),
            Colour = new Color4(
                HomeControlColours.Navy.R,
                HomeControlColours.Navy.G,
                HomeControlColours.Navy.B,
                0.48f),
        };
    }
}
