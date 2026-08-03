using System;
using System.Collections.Generic;
using osu.Framework.Bindables;
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
    private const float overviewWidth = 340;
    private const float overviewHeight = 191.25f;
    private const float overviewPadding = 10;

    private GameplayPlayfield playfield;
    private GameplayHud hud;
    private readonly GameplayTimingBar timingBar;
    private readonly GameplayComboReadout comboReadout;
    private readonly JudgementReadout judgementReadout;
    private readonly YokkoGameplaySettings settings;
    private readonly GameplayLayoutEditorLiveSettings liveSettings;
    private readonly Action beginTestPlay;
    private readonly Action beginAutoplayDemo;
    private readonly Action exitAutoplayDemo;
    private readonly Action save;
    private readonly Action close;
    private readonly LayoutTransformTarget playfieldTarget;
    private readonly LayoutTransformTarget accuracyTarget;
    private readonly LayoutTransformTarget progressTarget;
    private readonly LayoutTransformTarget informationTarget;
    private readonly LayoutTransformTarget timingBarTarget;
    private readonly LayoutTransformTarget comboTarget;
    private readonly LayoutTransformTarget judgementTarget;
    private readonly LayoutTransformTarget performanceReadoutTarget;
    private readonly CoverDragHandle topCoverHandle;
    private readonly CoverDragHandle bottomCoverHandle;
    private readonly CoverDragHandle judgementLineHandle;
    private readonly Stack<LayoutSnapshot> undoHistory = new();
    private readonly Stack<LayoutSnapshot> redoHistory = new();
    private readonly Container editorChrome;
    private readonly DemoInputBlocker demoInputBlocker;
    private readonly AutoplayDemoControl autoplayDemoControl;
    private Container overviewContent;
    private Container miniPlayfield;
    private Container miniAccuracy;
    private Container miniProgress;
    private Container miniInformation;
    private Container miniTimingBar;
    private Container miniCombo;
    private Container miniJudgement;
    private Container miniPerformanceReadout;
    private YokkoPerformanceReadout performanceReadoutPreview;
    private Box miniTopCover;
    private Box miniBottomCover;
    private Box miniBackgroundDim;
    private LayoutActionButton undoButton;
    private LayoutActionButton redoButton;
    private LayoutActionButton cancelButton;
    private SpriteText editorHint;
    private LayoutTransformTarget selectedTarget;
    private LayoutSnapshot sessionStart;
    private LiveSettingsSnapshot liveSettingsSessionStart;
    private bool cancelConfirmationPending;
    private double cancelConfirmationExpiresAt;
    private LayoutSnapshot cancelConfirmationLayout;
    private LiveSettingsSnapshot cancelConfirmationLiveSettings;
    private bool? displayedDirtyState;
    private bool displayedCancelConfirmation;

    internal bool IsEditing { get; private set; }

    internal bool IsTestingLayout { get; private set; }

    internal bool IsAutoplayDemo { get; private set; }

    internal bool IsSessionActive => IsEditing || IsTestingLayout;

    internal bool IsChromeVisible { get; private set; } = true;

    internal bool IsChromeVisibleForTest => IsChromeVisible;

    internal float ChromeAlphaForTest => editorChrome.Alpha;

    internal bool AutoplayControlVisibleForTest =>
        autoplayDemoControl.Alpha > 0.9f;

    internal float PerformanceReadoutPreviewAlphaForTest =>
        performanceReadoutPreview.Alpha;

    internal void ExitAutoplayDemoForTest() => exitAutoplayDemo();

    internal bool HasUnsavedChangesForTest => hasUnsavedChanges();

    internal bool IsCancelConfirmationPendingForTest =>
        cancelConfirmationPending;

    internal float OverviewAspectRatio =>
        overviewContent.Width / overviewContent.Height;

    internal int TransformTargetCount => 8;

    internal int ResizeHandleCount =>
        playfieldTarget.ResizeHandleCount
        + accuracyTarget.ResizeHandleCount
        + progressTarget.ResizeHandleCount
        + informationTarget.ResizeHandleCount
        + timingBarTarget.ResizeHandleCount
        + comboTarget.ResizeHandleCount
        + judgementTarget.ResizeHandleCount
        + performanceReadoutTarget.ResizeHandleCount;

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
        GameplayComboReadout comboReadout,
        JudgementReadout judgementReadout,
        YokkoGameplaySettings settings,
        GameplayLayoutEditorLiveSettings liveSettings,
        Action beginTestPlay,
        Action beginAutoplayDemo,
        Action exitAutoplayDemo,
        Action save,
        Action close)
    {
        this.playfield = playfield;
        this.hud = hud;
        this.timingBar = timingBar;
        this.comboReadout = comboReadout;
        this.judgementReadout = judgementReadout;
        this.settings = settings;
        this.liveSettings = liveSettings;
        this.beginTestPlay = beginTestPlay;
        this.beginAutoplayDemo = beginAutoplayDemo;
        this.exitAutoplayDemo = exitAutoplayDemo;
        this.save = save;
        this.close = close;

        RelativeSizeAxes = Axes.Both;
        Depth = -2000;
        Alpha = 0;

        editorChrome = new Container
        {
            RelativeSizeAxes = Axes.Both,
            Children = new Drawable[]
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
                performanceReadoutPreview = createPerformanceReadoutPreview(),
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
                    delta => constrainOffsetDelta(
                        delta,
                        settings.LayoutPlayfieldOffsetX,
                        settings.LayoutPlayfieldOffsetY),
                    resizePlayfieldWithWheel),
                accuracyTarget = new LayoutTransformTarget(
                    this,
                    LayoutElementKind.Accuracy,
                    YokkoStrings.Get("gameplay.layout_editor.accuracy"),
                    moveAccuracy,
                    resizeAccuracy,
                    resetAccuracy,
                    beginChange,
                    selectTarget,
                    snapTargetMove,
                    clearSnapGuides,
                    delta => constrainOffsetDelta(
                        delta,
                        settings.LayoutAccuracyOffsetX,
                        settings.LayoutAccuracyOffsetY)),
                progressTarget = new LayoutTransformTarget(
                    this,
                    LayoutElementKind.Progress,
                    YokkoStrings.Get("gameplay.layout_editor.progress"),
                    moveProgress,
                    resizeProgress,
                    resetProgress,
                    beginChange,
                    selectTarget,
                    snapTargetMove,
                    clearSnapGuides,
                    delta => constrainOffsetDelta(
                        delta,
                        settings.LayoutProgressOffsetX,
                        settings.LayoutProgressOffsetY)),
                informationTarget = new LayoutTransformTarget(
                    this,
                    LayoutElementKind.Information,
                    YokkoStrings.Get("gameplay.layout_editor.information"),
                    moveInformation,
                    resizeInformation,
                    resetInformation,
                    beginChange,
                    selectTarget,
                    snapTargetMove,
                    clearSnapGuides,
                    delta => constrainOffsetDelta(
                        delta,
                        settings.LayoutHudOffsetX,
                        settings.LayoutHudOffsetY)),
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
                    clearSnapGuides,
                    delta => constrainOffsetDelta(
                        delta,
                        settings.LayoutTimingBarOffsetX,
                        settings.LayoutTimingBarOffsetY)),
                comboTarget = new LayoutTransformTarget(
                    this,
                    LayoutElementKind.Combo,
                    YokkoStrings.Get(
                        "gameplay.layout_editor.combo"),
                    moveCombo,
                    resizeCombo,
                    resetCombo,
                    beginChange,
                    selectTarget,
                    snapTargetMove,
                    clearSnapGuides,
                    delta => constrainOffsetDelta(
                        delta,
                        settings.LayoutComboOffsetX,
                        settings.LayoutComboOffsetY)),
                judgementTarget = new LayoutTransformTarget(
                    this,
                    LayoutElementKind.Judgement,
                    YokkoStrings.Get(
                        "gameplay.layout_editor.judgement"),
                    moveJudgement,
                    resizeJudgement,
                    resetJudgement,
                    beginChange,
                    selectTarget,
                    snapTargetMove,
                    clearSnapGuides,
                    delta => constrainOffsetDelta(
                        delta,
                        settings.LayoutJudgementOffsetX,
                        settings.LayoutJudgementOffsetY)),
                performanceReadoutTarget = new LayoutTransformTarget(
                    this,
                    LayoutElementKind.PerformanceReadout,
                    YokkoStrings.Get(
                        "gameplay.layout_editor.performance_readout"),
                    movePerformanceReadout,
                    null,
                    resetPerformanceReadout,
                    beginChange,
                    selectTarget,
                    snapTargetMove,
                    clearSnapGuides,
                    delta => constrainOffsetDelta(
                        delta,
                        settings.LayoutPerformanceReadoutOffsetX,
                        settings.LayoutPerformanceReadoutOffsetY,
                        YokkoGameplaySettings.MinimumPerformanceReadoutOffset,
                        YokkoGameplaySettings.MaximumPerformanceReadoutOffset)),
                topCoverHandle = new CoverDragHandle(
                    this,
                    YokkoStrings.Get(
                        "gameplay.layout_editor.cover_top_drag"),
                    HomeControlColours.Cyan,
                    updateTopCover,
                    beginChange),
                bottomCoverHandle = new CoverDragHandle(
                    this,
                    YokkoStrings.Get(
                        "gameplay.layout_editor.cover_bottom_drag"),
                    HomeControlColours.Pink,
                    updateBottomCover,
                    beginChange),
                judgementLineHandle = new CoverDragHandle(
                    this,
                    YokkoStrings.Get(
                        "gameplay.layout_editor.judgement_line_drag"),
                    HomeControlColours.Yellow,
                    updateJudgementLine,
                    beginChange),
                createOverviewCard(),
                createInspectorCard(),
                createCoverPanel(),
                createLiveSettingsCard(),
                createFeedbackSettingsCard(),
                createToolWindowController(),
            },
        };
        InternalChildren = new Drawable[]
        {
            editorChrome,
            demoInputBlocker = new DemoInputBlocker(),
            autoplayDemoControl = new AutoplayDemoControl(
                exitAutoplayDemo),
        };

        settings.ToggleLayoutEditorUiKey.ValueChanged +=
            onToggleLayoutEditorUiKeyChanged;
        updateEditorHint();
    }

    private void onToggleLayoutEditorUiKeyChanged(
        ValueChangedEvent<Key> _) =>
        updateEditorHint();

    private static YokkoPerformanceReadout
        createPerformanceReadoutPreview()
    {
        var preview = new YokkoPerformanceReadout
        {
            Alpha = 0,
        };
        preview.SetTrackingEnabled(false);
        return preview;
    }

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
        {
            settings.ToggleLayoutEditorUiKey.ValueChanged -=
                onToggleLayoutEditorUiKeyChanged;
        }

        base.Dispose(isDisposing);
    }

    internal void SetEditing(bool editing)
    {
        if (editing && IsSessionActive)
            return;

        bool hadActiveSession = IsSessionActive;
        IsEditing = editing;
        IsTestingLayout = false;
        IsAutoplayDemo = false;
        ClearTransforms();
        autoplayDemoControl.ClearTransforms();
        autoplayDemoControl.Alpha = 0;
        demoInputBlocker.Alpha = 0;

        if (editing)
        {
            setChromeVisible(true, animate: false);
            sessionStart = captureLayout();
            liveSettingsSessionStart = captureLiveSettings();
            cancelConfirmationPending = false;
            displayedDirtyState = null;
            displayedCancelConfirmation = false;
            undoHistory.Clear();
            redoHistory.Clear();
            beginEditorSession();
            selectTarget(playfieldTarget);
            updateHistoryButtons();
            refreshSessionHint(force: true);
            this.FadeTo(1, 100, Easing.OutQuint);
        }
        else if (hadActiveSession)
        {
            setChromeVisible(true, animate: false);
            endEditorSession();
            selectTarget(null);
            this.FadeTo(0, 100, Easing.OutQuint);
        }
    }

    internal void BeginTestPlay()
    {
        beginPreview(false);
    }

    internal void BeginAutoplayDemo()
    {
        beginPreview(true);
    }

    private void beginPreview(bool autoplay)
    {
        if (!IsEditing)
            return;

        IsEditing = false;
        IsTestingLayout = true;
        IsAutoplayDemo = autoplay;
        demoInputBlocker.Alpha = autoplay ? 1 : 0;
        ClearTransforms();
        setComboEditorPreview(false);
        setJudgementEditorPreview(false);
        foreach (LayoutTransformTarget target in allTargets())
            applyElementAlpha(target.Kind, target.EditorHidden);
        if (autoplay)
        {
            this.FadeTo(1, 90, Easing.OutQuint);
            autoplayDemoControl.FadeTo(1, 120, Easing.OutQuint);
        }
        else
            this.FadeTo(0, 90, Easing.OutQuint);
    }

    internal void EndTestPlay()
    {
        if (!IsTestingLayout)
            return;

        IsTestingLayout = false;
        IsAutoplayDemo = false;
        IsEditing = true;
        demoInputBlocker.Alpha = 0;
        autoplayDemoControl.FadeTo(0, 80, Easing.OutQuint);
        setChromeVisible(true, animate: false);
        setComboEditorPreview(true);
        setJudgementEditorPreview(true);
        applyElementAlpha(
            LayoutElementKind.Playfield,
            playfieldTarget.EditorHidden);
        applyElementAlpha(
            LayoutElementKind.Accuracy,
            accuracyTarget.EditorHidden);
        applyElementAlpha(
            LayoutElementKind.Progress,
            progressTarget.EditorHidden);
        applyElementAlpha(
            LayoutElementKind.Information,
            informationTarget.EditorHidden);
        applyElementAlpha(
            LayoutElementKind.TimingBar,
            timingBarTarget.EditorHidden);
        applyElementAlpha(
            LayoutElementKind.Combo,
            comboTarget.EditorHidden);
        applyElementAlpha(
            LayoutElementKind.Judgement,
            judgementTarget.EditorHidden);
        applyElementAlpha(
            LayoutElementKind.PerformanceReadout,
            performanceReadoutTarget.EditorHidden);
        this.FadeTo(1, 100, Easing.OutQuint);
    }

    internal void ReplaceTargets(
        GameplayPlayfield nextPlayfield,
        GameplayHud nextHud,
        bool clearHistory = false)
    {
        playfield.SetSkinComboEditorPreview(false);
        playfield.SetSkinJudgementEditorPreview(false);
        playfield.SetSkinComboVisible(true);
        playfield.SetSkinJudgementVisible(true);
        playfield = nextPlayfield
                    ?? throw new ArgumentNullException(
                        nameof(nextPlayfield));
        hud = nextHud
              ?? throw new ArgumentNullException(nameof(nextHud));

        syncTargetVisibilityFromSettings();

        // Rebuilt targets may belong to a different skin. Layout snapshots do
        // not carry skin identity, so never let history from the old target
        // tree mutate the newly selected skin.
        if (clearHistory)
        {
            undoHistory.Clear();
            redoHistory.Clear();
            updateHistoryButtons();
        }

        if (IsSessionActive)
        {
            setComboEditorPreview(IsEditing);
            setJudgementEditorPreview(IsEditing);
            applyElementAlpha(
                LayoutElementKind.Playfield,
                playfieldTarget.EditorHidden);
            applyElementAlpha(
                LayoutElementKind.Accuracy,
                accuracyTarget.EditorHidden);
            applyElementAlpha(
                LayoutElementKind.Progress,
                progressTarget.EditorHidden);
            applyElementAlpha(
                LayoutElementKind.Information,
                informationTarget.EditorHidden);
            applyElementAlpha(
                LayoutElementKind.TimingBar,
                timingBarTarget.EditorHidden);
            applyElementAlpha(
                LayoutElementKind.Combo,
                comboTarget.EditorHidden);
            applyElementAlpha(
                LayoutElementKind.Judgement,
                judgementTarget.EditorHidden);
            applyElementAlpha(
                LayoutElementKind.PerformanceReadout,
                performanceReadoutTarget.EditorHidden);
        }
    }

    internal void SaveAndClose()
    {
        if (!IsEditing)
            return;

        cancelConfirmationPending = false;
        save();
        close();
    }

    internal void CancelAndClose()
    {
        if (!IsEditing)
            return;

        if (!hasUnsavedChanges())
        {
            cancelConfirmationPending = false;
            close();
            return;
        }

        if (!cancelConfirmationPending
            || Time.Current > cancelConfirmationExpiresAt
            || captureLayout() != cancelConfirmationLayout
            || captureLiveSettings() != cancelConfirmationLiveSettings)
        {
            cancelConfirmationPending = true;
            cancelConfirmationExpiresAt = Time.Current + 3000;
            cancelConfirmationLayout = captureLayout();
            cancelConfirmationLiveSettings = captureLiveSettings();
            refreshSessionHint(force: true);
            return;
        }

        cancelConfirmationPending = false;
        applyLiveSettings(liveSettingsSessionStart);
        applyLayout(sessionStart);
        close();
    }

    internal void ResetAll()
    {
        if (!IsEditing)
            return;

        beginChange();
        settings.ResetGameplayLayout();
        syncTargetVisibilityFromSettings();
    }

    internal void ToggleChrome()
    {
        if (!IsEditing)
            return;

        setChromeVisible(!IsChromeVisible, animate: true);
    }

    protected override bool OnKeyDown(KeyDownEvent e)
    {
        if (!IsEditing)
            return false;

        if (!e.Repeat
            && e.Key == settings.GetShortcutBinding(
                ManiaShortcutAction.ToggleLayoutEditorUi))
        {
            ToggleChrome();
            return true;
        }

        if (!IsChromeVisible)
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

        if (!e.Repeat
            && e.ControlPressed
            && e.Key == Key.S)
        {
            SaveAndClose();
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
            && e.Key is (Key.Delete or Key.BackSpace)
            && target != null)
        {
            resetTarget(target);
            return true;
        }

        if (!e.Repeat
            && e.Key == Key.Home
            && target != null)
        {
            selectTarget(target);
            centreSelectedBoth();
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
        if (!IsChromeVisible || e.Button != MouseButton.Left)
            return false;

        return true;
    }

    protected override void Update()
    {
        base.Update();

        if (!IsEditing
            || !IsChromeVisible
            || DrawWidth <= 0
            || DrawHeight <= 0)
        {
            return;
        }

        (Vector2 playfieldTopLeft, Vector2 playfieldBottomRight) =
            GameplayLayoutGeometry.BoundsIn(this, playfield);
        (Vector2 accuracyTopLeft, Vector2 accuracyBottomRight) =
            GameplayLayoutGeometry.BoundsIn(
                this,
                hud.AccuracyLayoutDrawable);
        (Vector2 progressTopLeft, Vector2 progressBottomRight) =
            GameplayLayoutGeometry.BoundsIn(
                this,
                hud.ProgressLayoutDrawable);
        (Vector2 informationTopLeft, Vector2 informationBottomRight) =
            GameplayLayoutGeometry.BoundsIn(
                this,
                hud.InformationLayoutDrawable);
        (Vector2 timingBarTopLeft, Vector2 timingBarBottomRight) =
            GameplayLayoutGeometry.BoundsIn(this, timingBar);
        (Vector2 comboTopLeft, Vector2 comboBottomRight) =
            GameplayLayoutGeometry.BoundsIn(
                this,
                drawableFor(LayoutElementKind.Combo));
        (Vector2 judgementTopLeft, Vector2 judgementBottomRight) =
            GameplayLayoutGeometry.BoundsIn(
                this,
                drawableFor(LayoutElementKind.Judgement));
        performanceReadoutPreview.Position =
            YokkoPerformanceReadout.GetLayoutPosition(
                new Vector2(DrawWidth, DrawHeight),
                settings.LayoutPerformanceReadoutOffsetX.Value,
                settings.LayoutPerformanceReadoutOffsetY.Value);
        (Vector2 performanceTopLeft, Vector2 performanceBottomRight) =
            GameplayLayoutGeometry.BoundsIn(
                this,
                performanceReadoutPreview);

        setBounds(
            playfieldTarget,
            playfieldTopLeft,
            playfieldBottomRight);
        setBounds(accuracyTarget, accuracyTopLeft, accuracyBottomRight);
        setBounds(progressTarget, progressTopLeft, progressBottomRight);
        setBounds(
            informationTarget,
            informationTopLeft,
            informationBottomRight);
        setBounds(
            timingBarTarget,
            timingBarTopLeft,
            timingBarBottomRight);
        setBounds(comboTarget, comboTopLeft, comboBottomRight);
        setBounds(
            judgementTarget,
            judgementTopLeft,
            judgementBottomRight);
        setBounds(
            performanceReadoutTarget,
            performanceTopLeft,
            performanceBottomRight);

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
        bool topCoverActive =
            settings.LayoutTopCoverRatio.Value > 0.0001;
        bool bottomCoverActive =
            settings.LayoutBottomCoverRatio.Value > 0.0001;

        topCoverHandle.SetActive(topCoverActive);
        bottomCoverHandle.SetActive(bottomCoverActive);

        if (!topCoverActive)
        {
            topBoundary = Math.Min(
                playfieldBottomRight.Y - 18,
                playfieldTopLeft.Y + 18);
        }

        if (!bottomCoverActive)
        {
            bottomBoundary = Math.Max(
                playfieldTopLeft.Y + 18,
                playfieldBottomRight.Y - 18);
        }

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
        float judgementLineY = playfieldTopLeft.Y
                               + playfieldHeight
                               * playfield.JudgementPosition
                               / Math.Max(1, playfield.DrawHeight);
        judgementLineHandle.SetActive(IsEditing);
        setHandleBounds(
            judgementLineHandle,
            playfieldTopLeft.X,
            judgementLineY,
            playfieldBottomRight.X - playfieldTopLeft.X);

        updateOverview(
            playfieldTopLeft,
            playfieldBottomRight,
            accuracyTopLeft,
            accuracyBottomRight,
            progressTopLeft,
            progressBottomRight,
            informationTopLeft,
            informationBottomRight,
            timingBarTopLeft,
            timingBarBottomRight,
            comboTopLeft,
            comboBottomRight,
            judgementTopLeft,
            judgementBottomRight,
            performanceTopLeft,
            performanceBottomRight);
        refreshInspector();
        refreshSessionHint();
    }

    private Drawable createTopBar()
    {
        const float panelWidth = 330;
        const float panelHeight = 392;
        const float buttonWidth = 298;
        const float splitButtonWidth = 145;

        return createToolWindow(
            GameplayLayoutEditorToolWindow.Actions,
            new Container
            {
            Position = new Vector2(18),
            Size = new Vector2(panelWidth + 5, panelHeight + 5),
            Depth = -100,
            Children = new Drawable[]
            {
                new Container
                {
                    Position = new Vector2(5),
                    Size = new Vector2(panelWidth, panelHeight),
                    Masking = true,
                    CornerRadius = 12,
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
                    Size = new Vector2(panelWidth, panelHeight),
                    Masking = true,
                    CornerRadius = 12,
                    BorderThickness = 1.25f,
                    BorderColour = new Color4(
                        HomeControlColours.Navy.R,
                        HomeControlColours.Navy.G,
                        HomeControlColours.Navy.B,
                        0.72f),
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
                            Width = 6,
                            Colour = HomeControlColours.Cyan,
                        },
                        new SpriteText
                        {
                            Position = new Vector2(20, 15),
                            Text = YokkoStrings.Get(
                                "gameplay.layout_editor.title"),
                            Font = LayoutEditorTypography.Bold(16),
                            Colour = HomeControlColours.Navy,
                        },
                        new SpriteText
                        {
                            Position = new Vector2(20, 49),
                            Text = YokkoStrings.Get(
                                "gameplay.layout_editor.hint"),
                            Font = LayoutEditorTypography.Regular(10),
                            Colour = new Color4(
                                HomeControlColours.Navy.R,
                                HomeControlColours.Navy.G,
                                HomeControlColours.Navy.B,
                                0.64f),
                        },
                        editorHint = new SpriteText
                        {
                            Position = new Vector2(20, 77),
                            Font = LayoutEditorTypography.Bold(10),
                            Colour = HomeControlColours.Pink,
                        },
                        new FillFlowContainer
                        {
                            Position = new Vector2(16, 112),
                            AutoSizeAxes = Axes.Y,
                            Width = buttonWidth,
                            Direction = FillDirection.Vertical,
                            Spacing = new Vector2(0, 8),
                            Children = new Drawable[]
                            {
                                new FillFlowContainer
                                {
                                    Size = new Vector2(buttonWidth, 44),
                                    Direction = FillDirection.Horizontal,
                                    Spacing = new Vector2(8, 0),
                                    Children = new Drawable[]
                                    {
                                        undoButton = new LayoutActionButton(
                                            YokkoStrings.Get(
                                                "gameplay.layout_editor.undo"),
                                            FontAwesome.Solid.Undo,
                                            undo)
                                        {
                                            Size = new Vector2(
                                                splitButtonWidth,
                                                44),
                                        },
                                        redoButton = new LayoutActionButton(
                                            YokkoStrings.Get(
                                                "gameplay.layout_editor.redo"),
                                            FontAwesome.Solid.Redo,
                                            redo)
                                        {
                                            Size = new Vector2(
                                                splitButtonWidth,
                                                44),
                                        },
                                    },
                                },
                                new FillFlowContainer
                                {
                                    Size = new Vector2(buttonWidth, 44),
                                    Direction = FillDirection.Horizontal,
                                    Spacing = new Vector2(8, 0),
                                    Children = new Drawable[]
                                    {
                                        new LayoutActionButton(
                                            YokkoStrings.Get(
                                                "gameplay.layout_editor.reset"),
                                            FontAwesome.Solid.Trash,
                                            reset)
                                        {
                                            Size = new Vector2(
                                                splitButtonWidth,
                                                44),
                                        },
                                        cancelButton = new LayoutActionButton(
                                            YokkoStrings.Get(
                                                "gameplay.layout_editor.cancel"),
                                            FontAwesome.Solid.Times,
                                            CancelAndClose)
                                        {
                                            Size = new Vector2(
                                                splitButtonWidth,
                                                44),
                                        },
                                    },
                                },
                                new LayoutActionButton(
                                    YokkoStrings.Get(
                                        "gameplay.layout_editor.test_play"),
                                    FontAwesome.Solid.Play,
                                    beginTestPlay)
                                {
                                    Size = new Vector2(buttonWidth, 44),
                                },
                                new LayoutActionButton(
                                    YokkoStrings.Get(
                                        "gameplay.layout_editor.autoplay_demo"),
                                    FontAwesome.Solid.PlayCircle,
                                    beginAutoplayDemo)
                                {
                                    Size = new Vector2(buttonWidth, 44),
                                },
                                new LayoutActionButton(
                                    YokkoStrings.Get(
                                        "gameplay.layout_editor.save"),
                                    FontAwesome.Solid.Check,
                                    SaveAndClose,
                                    true)
                                {
                                    Size = new Vector2(buttonWidth, 48),
                                },
                            },
                        },
                    },
                },
                new Box
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.Centre,
                    Position = new Vector2(-8, 5),
                    Size = new Vector2(13),
                    Rotation = 45,
                    Colour = HomeControlColours.Yellow,
                },
            },
            });
    }

    private void setChromeVisible(bool visible, bool animate)
    {
        IsChromeVisible = visible;
        editorChrome.ClearTransforms();

        if (animate)
        {
            editorChrome.FadeTo(
                visible ? 1 : 0,
                visible ? 100 : 80,
                Easing.OutQuint);
        }
        else
        {
            editorChrome.Alpha = visible ? 1 : 0;
        }

        if (!visible)
            clearSnapGuides();
    }

    private void updateEditorHint()
    {
        refreshSessionHint(force: true);
    }

    private void refreshSessionHint(bool force = false)
    {
        if (editorHint == null || !IsEditing)
            return;

        if (cancelConfirmationPending
            && Time.Current > cancelConfirmationExpiresAt)
        {
            cancelConfirmationPending = false;
        }

        bool dirty = hasUnsavedChanges();
        if (!dirty)
            cancelConfirmationPending = false;

        if (!force
            && displayedDirtyState == dirty
            && displayedCancelConfirmation
               == cancelConfirmationPending)
        {
            return;
        }

        displayedDirtyState = dirty;
        displayedCancelConfirmation = cancelConfirmationPending;

        if (cancelConfirmationPending)
        {
            editorHint.Text = YokkoStrings.Get(
                "gameplay.layout_editor.discard_confirm_hint");
            editorHint.Colour = HomeControlColours.Pink;
            cancelButton?.SetText(YokkoStrings.Get(
                "gameplay.layout_editor.discard_confirm"));
            return;
        }

        cancelButton?.SetText(YokkoStrings.Get(
            "gameplay.layout_editor.cancel"));
        if (dirty)
        {
            editorHint.Text = YokkoStrings.Get(
                "gameplay.layout_editor.unsaved_hint");
            editorHint.Colour = HomeControlColours.Pink;
            return;
        }

        string key = KeyModeBindings.FormatKey(
                settings.GetShortcutBinding(
                    ManiaShortcutAction.ToggleLayoutEditorUi))
            .ToUpperInvariant();
        editorHint.Text = YokkoStrings.Get(
            "gameplay.layout_editor.hide_hint",
            key);
        editorHint.Colour = HomeControlColours.Cyan;
    }

    private Drawable createOverviewCard()
    {
        Vector2 cardSize = new(
            overviewWidth + overviewPadding * 2,
            overviewHeight + 42);

        return createToolWindow(
            GameplayLayoutEditorToolWindow.Overview,
            new Container
            {
            Anchor = Anchor.BottomRight,
            Origin = Anchor.BottomRight,
            Position = new Vector2(-18, -16),
            Scale = new Vector2(1.08f),
            Size = cardSize + new Vector2(5),
            Children = new Drawable[]
            {
                new Container
                {
                    Position = new Vector2(5),
                    Size = cardSize,
                    Masking = true,
                    CornerRadius = 11,
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
                    CornerRadius = 11,
                    BorderThickness = 1.25f,
                    BorderColour = new Color4(
                        HomeControlColours.Navy.R,
                        HomeControlColours.Navy.G,
                        HomeControlColours.Navy.B,
                        0.72f),
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
                            Font = LayoutEditorTypography.Bold(12),
                            Colour = HomeControlColours.Navy,
                        },
                        overviewContent = new Container
                        {
                            Position = new Vector2(overviewPadding, 32),
                            Size = new Vector2(overviewWidth, overviewHeight),
                            Masking = true,
                            CornerRadius = 7,
                            BorderThickness = 1.25f,
                            BorderColour = HomeControlColours.Navy,
                            Children = new Drawable[]
                            {
                                new Box
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Colour = YokkoPalette.Background,
                                },
                                miniBackgroundDim = new Box
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Colour = Color4.Black,
                                },
                                miniPlayfield = createMiniPlayfield(),
                                miniAccuracy = createMiniReadout(
                                    HomeControlColours.PaleCyan),
                                miniProgress = createMiniReadout(
                                    HomeControlColours.Cyan),
                                miniInformation = createMiniReadout(
                                    HomeControlColours.Navy),
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
                                miniCombo = createMiniReadout(
                                    HomeControlColours.Pink),
                                miniJudgement = createMiniReadout(
                                    HomeControlColours.Yellow),
                                miniPerformanceReadout = createMiniReadout(
                                    HomeControlColours.Cyan),
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
            });
    }

    private LayoutTransformTarget hoveredTarget()
    {
        if (performanceReadoutTarget.IsHovered)
            return performanceReadoutTarget;

        if (judgementTarget.IsHovered)
            return judgementTarget;

        if (comboTarget.IsHovered)
            return comboTarget;

        if (timingBarTarget.IsHovered)
            return timingBarTarget;

        if (informationTarget.IsHovered)
            return informationTarget;

        if (progressTarget.IsHovered)
            return progressTarget;

        if (accuracyTarget.IsHovered)
            return accuracyTarget;

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
            accuracyTarget,
            progressTarget,
            informationTarget,
            timingBarTarget,
            comboTarget,
            judgementTarget,
            performanceReadoutTarget,
        ];

        int direction = backwards ? -1 : 1;
        int current = selectedTarget == null
            ? backwards ? 0 : targets.Length - 1
            : Array.IndexOf(targets, selectedTarget);

        for (int offset = 1; offset <= targets.Length; offset++)
        {
            int next = (current + direction * offset + targets.Length)
                       % targets.Length;
            if (targets[next].CanEdit)
            {
                selectTarget(targets[next]);
                return;
            }
        }

        selectTarget(null);
    }

    private void resetTarget(LayoutTransformTarget target)
    {
        if (target == null)
            return;

        selectTarget(target);
        if (!target.CanEdit)
            return;

        beginChange();
        target.Reset();
    }

    private void beginChange()
    {
        cancelConfirmationPending = false;
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
        settings.LayoutAccuracyOffsetX.Value,
        settings.LayoutAccuracyOffsetY.Value,
        settings.LayoutAccuracyScaleX.Value,
        settings.LayoutAccuracyScaleY.Value,
        settings.LayoutProgressOffsetX.Value,
        settings.LayoutProgressOffsetY.Value,
        settings.LayoutProgressScaleX.Value,
        settings.LayoutProgressScaleY.Value,
        settings.LayoutTimingBarOffsetX.Value,
        settings.LayoutTimingBarOffsetY.Value,
        settings.LayoutTimingBarScaleX.Value,
        settings.LayoutTimingBarScaleY.Value,
        settings.LayoutComboOffsetX.Value,
        settings.LayoutComboOffsetY.Value,
        settings.LayoutComboScaleX.Value,
        settings.LayoutComboScaleY.Value,
        settings.LayoutJudgementOffsetX.Value,
        settings.LayoutJudgementOffsetY.Value,
        settings.LayoutJudgementScaleX.Value,
        settings.LayoutJudgementScaleY.Value,
        settings.LayoutPerformanceReadoutOffsetX.Value,
        settings.LayoutPerformanceReadoutOffsetY.Value,
        settings.LayoutPlayfieldVisible.Value,
        settings.LayoutAccuracyVisible.Value,
        settings.LayoutProgressVisible.Value,
        settings.LayoutInformationVisible.Value,
        settings.LayoutTimingBarVisible.Value,
        settings.LayoutComboVisible.Value,
        settings.LayoutJudgementVisible.Value,
        settings.LayoutPerformanceReadoutVisible.Value,
        settings.LayoutHitEffectsVisible.Value,
        settings.LayoutJudgementLineOffsetY.Value,
        settings.LayoutTopCoverRatio.Value,
        settings.LayoutBottomCoverRatio.Value);

    private LiveSettingsSnapshot captureLiveSettings() => new(
        liveSettings.SelectedSkinId() ?? string.Empty,
        liveSettings.ScrollSpeed(),
        liveSettings.ScrollDirection(),
        liveSettings.BackgroundDim(),
        liveSettings.LongNoteCutEnabled(),
        liveSettings.LongNoteCutAmount(),
        liveSettings.JudgementDisplayDuration(),
        liveSettings.JudgementOpacity(),
        liveSettings.JudgementHitErrorScale(),
        liveSettings.ShowJudgementHitError(),
        liveSettings.ShowTimingBar());

    private bool hasUnsavedChanges() =>
        captureLayout() != sessionStart
        || captureLiveSettings() != liveSettingsSessionStart;

    private void applyLiveSettings(LiveSettingsSnapshot snapshot)
    {
        liveSettings.SelectSkin(snapshot.SkinId);
        liveSettings.SetScrollDirection(snapshot.ScrollDirection);
        liveSettings.SetScrollSpeed(snapshot.ScrollSpeed);
        liveSettings.SetBackgroundDim(snapshot.BackgroundDim);
        liveSettings.SetLongNoteCutAmount(snapshot.LongNoteCutAmount);
        liveSettings.SetLongNoteCutEnabled(snapshot.LongNoteCutEnabled);
        liveSettings.SetJudgementDisplayDuration(
            snapshot.JudgementDisplayDuration);
        liveSettings.SetJudgementOpacity(snapshot.JudgementOpacity);
        liveSettings.SetJudgementHitErrorScale(
            snapshot.JudgementHitErrorScale);
        liveSettings.SetShowJudgementHitError(
            snapshot.ShowJudgementHitError);
        liveSettings.SetShowTimingBar(snapshot.ShowTimingBar);
    }

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
        settings.LayoutAccuracyOffsetX.Value = snapshot.AccuracyOffsetX;
        settings.LayoutAccuracyOffsetY.Value = snapshot.AccuracyOffsetY;
        settings.LayoutAccuracyScaleX.Value = snapshot.AccuracyScaleX;
        settings.LayoutAccuracyScaleY.Value = snapshot.AccuracyScaleY;
        settings.LayoutProgressOffsetX.Value = snapshot.ProgressOffsetX;
        settings.LayoutProgressOffsetY.Value = snapshot.ProgressOffsetY;
        settings.LayoutProgressScaleX.Value = snapshot.ProgressScaleX;
        settings.LayoutProgressScaleY.Value = snapshot.ProgressScaleY;
        settings.LayoutTimingBarOffsetX.Value =
            snapshot.TimingBarOffsetX;
        settings.LayoutTimingBarOffsetY.Value =
            snapshot.TimingBarOffsetY;
        settings.LayoutTimingBarScaleX.Value =
            snapshot.TimingBarScaleX;
        settings.LayoutTimingBarScaleY.Value =
            snapshot.TimingBarScaleY;
        settings.LayoutComboOffsetX.Value = snapshot.ComboOffsetX;
        settings.LayoutComboOffsetY.Value = snapshot.ComboOffsetY;
        settings.LayoutComboScaleX.Value = snapshot.ComboScaleX;
        settings.LayoutComboScaleY.Value = snapshot.ComboScaleY;
        settings.LayoutJudgementOffsetX.Value =
            snapshot.JudgementOffsetX;
        settings.LayoutJudgementOffsetY.Value =
            snapshot.JudgementOffsetY;
        settings.LayoutJudgementScaleX.Value =
            snapshot.JudgementScaleX;
        settings.LayoutJudgementScaleY.Value =
            snapshot.JudgementScaleY;
        settings.LayoutPerformanceReadoutOffsetX.Value =
            snapshot.PerformanceReadoutOffsetX;
        settings.LayoutPerformanceReadoutOffsetY.Value =
            snapshot.PerformanceReadoutOffsetY;
        settings.LayoutPlayfieldVisible.Value = snapshot.PlayfieldVisible;
        settings.LayoutAccuracyVisible.Value = snapshot.AccuracyVisible;
        settings.LayoutProgressVisible.Value = snapshot.ProgressVisible;
        settings.LayoutInformationVisible.Value = snapshot.InformationVisible;
        settings.LayoutTimingBarVisible.Value = snapshot.TimingBarVisible;
        settings.LayoutComboVisible.Value = snapshot.ComboVisible;
        settings.LayoutJudgementVisible.Value = snapshot.JudgementVisible;
        settings.LayoutPerformanceReadoutVisible.Value =
            snapshot.PerformanceReadoutVisible;
        settings.LayoutHitEffectsVisible.Value =
            snapshot.HitEffectsVisible;
        settings.LayoutJudgementLineOffsetY.Value =
            snapshot.JudgementLineOffsetY;
        settings.LayoutTopCoverRatio.Value = snapshot.TopCoverRatio;
        settings.LayoutBottomCoverRatio.Value =
            snapshot.BottomCoverRatio;
        syncTargetVisibilityFromSettings();
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

    private void moveAccuracy(Vector2 delta)
    {
        settings.LayoutAccuracyOffsetX.Value = clampOffset(
            settings.LayoutAccuracyOffsetX.Value
            + delta.X / Math.Max(1, DrawWidth));
        settings.LayoutAccuracyOffsetY.Value = clampOffset(
            settings.LayoutAccuracyOffsetY.Value
            + delta.Y / Math.Max(1, DrawHeight));
    }

    private void moveProgress(Vector2 delta)
    {
        settings.LayoutProgressOffsetX.Value = clampOffset(
            settings.LayoutProgressOffsetX.Value
            + delta.X / Math.Max(1, DrawWidth));
        settings.LayoutProgressOffsetY.Value = clampOffset(
            settings.LayoutProgressOffsetY.Value
            + delta.Y / Math.Max(1, DrawHeight));
    }

    private void moveInformation(Vector2 delta)
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

    private void moveCombo(Vector2 delta)
    {
        settings.LayoutComboOffsetX.Value = clampOffset(
            settings.LayoutComboOffsetX.Value
            + delta.X / Math.Max(1, DrawWidth));
        settings.LayoutComboOffsetY.Value = clampOffset(
            settings.LayoutComboOffsetY.Value
            + delta.Y / Math.Max(1, DrawHeight));
    }

    private void moveJudgement(Vector2 delta)
    {
        settings.LayoutJudgementOffsetX.Value = clampOffset(
            settings.LayoutJudgementOffsetX.Value
            + delta.X / Math.Max(1, DrawWidth));
        settings.LayoutJudgementOffsetY.Value = clampOffset(
            settings.LayoutJudgementOffsetY.Value
            + delta.Y / Math.Max(1, DrawHeight));
    }

    private void movePerformanceReadout(Vector2 delta)
    {
        settings.LayoutPerformanceReadoutOffsetX.Value =
            clampPerformanceReadoutOffset(
                settings.LayoutPerformanceReadoutOffsetX.Value
                + delta.X / Math.Max(1, DrawWidth));
        settings.LayoutPerformanceReadoutOffsetY.Value =
            clampPerformanceReadoutOffset(
                settings.LayoutPerformanceReadoutOffsetY.Value
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

    private void resetAccuracy()
    {
        settings.LayoutAccuracyOffsetX.SetDefault();
        settings.LayoutAccuracyOffsetY.SetDefault();
        settings.LayoutAccuracyScaleX.SetDefault();
        settings.LayoutAccuracyScaleY.SetDefault();
    }

    private void resetProgress()
    {
        settings.LayoutProgressOffsetX.SetDefault();
        settings.LayoutProgressOffsetY.SetDefault();
        settings.LayoutProgressScaleX.SetDefault();
        settings.LayoutProgressScaleY.SetDefault();
    }

    private void resetInformation()
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

    private void resetCombo()
    {
        settings.LayoutComboOffsetX.SetDefault();
        settings.LayoutComboOffsetY.SetDefault();
        settings.LayoutComboScaleX.SetDefault();
        settings.LayoutComboScaleY.SetDefault();
    }

    private void resetJudgement()
    {
        settings.LayoutJudgementOffsetX.SetDefault();
        settings.LayoutJudgementOffsetY.SetDefault();
        settings.LayoutJudgementScaleX.SetDefault();
        settings.LayoutJudgementScaleY.SetDefault();
    }

    private void resetPerformanceReadout()
    {
        settings.LayoutPerformanceReadoutOffsetX.SetDefault();
        settings.LayoutPerformanceReadoutOffsetY.SetDefault();
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

    private void resizeAccuracy(ResizeEdges edges, Vector2 delta) =>
        resizeTopRightHudPart(
            hud.AccuracyLayoutDrawable,
            edges,
            delta,
            () => settings.LayoutAccuracyScaleX.Value,
            value => settings.LayoutAccuracyScaleX.Value = value,
            () => settings.LayoutAccuracyScaleY.Value,
            value => settings.LayoutAccuracyScaleY.Value = value,
            () => settings.LayoutAccuracyOffsetX.Value,
            value => settings.LayoutAccuracyOffsetX.Value = value,
            () => settings.LayoutAccuracyOffsetY.Value,
            value => settings.LayoutAccuracyOffsetY.Value = value);

    private void resizeProgress(ResizeEdges edges, Vector2 delta) =>
        resizeTopRightHudPart(
            hud.ProgressLayoutDrawable,
            edges,
            delta,
            () => settings.LayoutProgressScaleX.Value,
            value => settings.LayoutProgressScaleX.Value = value,
            () => settings.LayoutProgressScaleY.Value,
            value => settings.LayoutProgressScaleY.Value = value,
            () => settings.LayoutProgressOffsetX.Value,
            value => settings.LayoutProgressOffsetX.Value = value,
            () => settings.LayoutProgressOffsetY.Value,
            value => settings.LayoutProgressOffsetY.Value = value);

    private void resizeInformation(ResizeEdges edges, Vector2 delta) =>
        resizeTopRightHudPart(
            hud.InformationLayoutDrawable,
            edges,
            delta,
            () => settings.LayoutHudScaleX.Value,
            value => settings.LayoutHudScaleX.Value = value,
            () => settings.LayoutHudScaleY.Value,
            value => settings.LayoutHudScaleY.Value = value,
            () => settings.LayoutHudOffsetX.Value,
            value => settings.LayoutHudOffsetX.Value = value,
            () => settings.LayoutHudOffsetY.Value,
            value => settings.LayoutHudOffsetY.Value = value);

    private void resizeTopRightHudPart(
        Drawable drawable,
        ResizeEdges edges,
        Vector2 delta,
        Func<double> scaleX,
        Action<double> setScaleX,
        Func<double> scaleY,
        Action<double> setScaleY,
        Func<double> offsetX,
        Action<double> setOffsetX,
        Func<double> offsetY,
        Action<double> setOffsetY)
    {
        (Vector2 topLeft, Vector2 bottomRight) =
            GameplayLayoutGeometry.BoundsIn(this, drawable);
        float width = Math.Max(1, bottomRight.X - topLeft.X);
        float height = Math.Max(1, bottomRight.Y - topLeft.Y);

        if (hasHorizontalEdge(edges))
        {
            bool fromRight = edges.HasFlag(ResizeEdges.Right);
            float realisedChange = resizeDimension(
                scaleX(),
                width,
                (fromRight ? 1 : -1) * delta.X,
                YokkoGameplaySettings.MinimumLayoutScale,
                YokkoGameplaySettings.MaximumLayoutScale,
                setScaleX);
            if (fromRight)
            {
                setOffsetX(clampOffset(
                    offsetX()
                    + realisedChange / Math.Max(1, DrawWidth)));
            }
        }

        if (hasVerticalEdge(edges))
        {
            bool fromBottom = edges.HasFlag(ResizeEdges.Bottom);
            float realisedChange = resizeDimension(
                scaleY(),
                height,
                (fromBottom ? 1 : -1) * delta.Y,
                YokkoGameplaySettings.MinimumLayoutScale,
                YokkoGameplaySettings.MaximumLayoutScale,
                setScaleY);
            if (!fromBottom)
            {
                setOffsetY(clampOffset(
                    offsetY()
                    - realisedChange / Math.Max(1, DrawHeight)));
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

    private void resizeCombo(ResizeEdges edges, Vector2 delta) =>
        resizeReadout(
            drawableFor(LayoutElementKind.Combo),
            edges,
            delta,
            () => settings.LayoutComboScaleX.Value,
            value => settings.LayoutComboScaleX.Value = value,
            () => settings.LayoutComboScaleY.Value,
            value => settings.LayoutComboScaleY.Value = value,
            () => settings.LayoutComboOffsetX.Value,
            value => settings.LayoutComboOffsetX.Value = value,
            () => settings.LayoutComboOffsetY.Value,
            value => settings.LayoutComboOffsetY.Value = value);

    private void resizeJudgement(ResizeEdges edges, Vector2 delta) =>
        resizeReadout(
            drawableFor(LayoutElementKind.Judgement),
            edges,
            delta,
            () => settings.LayoutJudgementScaleX.Value,
            value => settings.LayoutJudgementScaleX.Value = value,
            () => settings.LayoutJudgementScaleY.Value,
            value => settings.LayoutJudgementScaleY.Value = value,
            () => settings.LayoutJudgementOffsetX.Value,
            value => settings.LayoutJudgementOffsetX.Value = value,
            () => settings.LayoutJudgementOffsetY.Value,
            value => settings.LayoutJudgementOffsetY.Value = value);

    private void resizeReadout(
        Drawable drawable,
        ResizeEdges edges,
        Vector2 delta,
        Func<double> scaleX,
        Action<double> setScaleX,
        Func<double> scaleY,
        Action<double> setScaleY,
        Func<double> offsetX,
        Action<double> setOffsetX,
        Func<double> offsetY,
        Action<double> setOffsetY)
    {
        (Vector2 topLeft, Vector2 bottomRight) =
            GameplayLayoutGeometry.BoundsIn(this, drawable);
        float width = Math.Max(1, bottomRight.X - topLeft.X);
        float height = Math.Max(1, bottomRight.Y - topLeft.Y);

        if (hasHorizontalEdge(edges))
        {
            bool fromRight = edges.HasFlag(ResizeEdges.Right);
            float realisedChange = resizeDimension(
                scaleX(),
                width,
                (fromRight ? 1 : -1) * delta.X,
                YokkoGameplaySettings.MinimumLayoutScale,
                YokkoGameplaySettings.MaximumLayoutScale,
                setScaleX);
            setOffsetX(clampOffset(
                offsetX()
                + (fromRight ? realisedChange : -realisedChange)
                / 2
                / Math.Max(1, DrawWidth)));
        }

        if (hasVerticalEdge(edges))
        {
            bool fromBottom = edges.HasFlag(ResizeEdges.Bottom);
            float realisedChange = resizeDimension(
                scaleY(),
                height,
                (fromBottom ? 1 : -1) * delta.Y,
                YokkoGameplaySettings.MinimumLayoutScale,
                YokkoGameplaySettings.MaximumLayoutScale,
                setScaleY);
            setOffsetY(clampOffset(
                offsetY()
                + (fromBottom ? realisedChange : -realisedChange)
                / 2
                / Math.Max(1, DrawHeight)));
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

    private void updateJudgementLine(Vector2 screenSpacePosition)
    {
        Vector2 local = ToLocalSpace(screenSpacePosition);
        (Vector2 topLeft, Vector2 bottomRight) =
            GameplayLayoutGeometry.BoundsIn(this, playfield);
        double logicalY = (local.Y - topLeft.Y)
                          / Math.Max(1, bottomRight.Y - topLeft.Y)
                          * playfield.DrawHeight;
        applyJudgementLinePosition(logicalY);
    }

    private void reset()
    {
        ResetAll();
    }

    private void updateOverview(
        Vector2 playfieldTopLeft,
        Vector2 playfieldBottomRight,
        Vector2 accuracyTopLeft,
        Vector2 accuracyBottomRight,
        Vector2 progressTopLeft,
        Vector2 progressBottomRight,
        Vector2 informationTopLeft,
        Vector2 informationBottomRight,
        Vector2 timingBarTopLeft,
        Vector2 timingBarBottomRight,
        Vector2 comboTopLeft,
        Vector2 comboBottomRight,
        Vector2 judgementTopLeft,
        Vector2 judgementBottomRight,
        Vector2 performanceTopLeft,
        Vector2 performanceBottomRight)
    {
        setOverviewBounds(
            miniPlayfield,
            playfieldTopLeft,
            playfieldBottomRight);
        setOverviewBounds(
            miniAccuracy,
            accuracyTopLeft,
            accuracyBottomRight);
        setOverviewBounds(
            miniProgress,
            progressTopLeft,
            progressBottomRight);
        setOverviewBounds(
            miniInformation,
            informationTopLeft,
            informationBottomRight);
        setOverviewBounds(
            miniTimingBar,
            timingBarTopLeft,
            timingBarBottomRight);
        setOverviewBounds(miniCombo, comboTopLeft, comboBottomRight);
        setOverviewBounds(
            miniJudgement,
            judgementTopLeft,
            judgementBottomRight);
        setOverviewBounds(
            miniPerformanceReadout,
            performanceTopLeft,
            performanceBottomRight);
        miniBackgroundDim.Alpha = (float)Math.Clamp(
            settings.BackgroundDim.Value,
            YokkoGameplaySettings.MinimumBackgroundDim,
            YokkoGameplaySettings.MaximumBackgroundDim);

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
        handle.Position = new Vector2(x, boundaryY - 18);
        handle.Size = new Vector2(Math.Max(80, width), 36);
    }

    private static double clampOffset(double value) => Math.Clamp(
        value,
        YokkoGameplaySettings.MinimumLayoutOffset,
        YokkoGameplaySettings.MaximumLayoutOffset);

    private static double clampPerformanceReadoutOffset(double value) =>
        Math.Clamp(
            value,
            YokkoGameplaySettings.MinimumPerformanceReadoutOffset,
            YokkoGameplaySettings.MaximumPerformanceReadoutOffset);

    private Vector2 constrainOffsetDelta(
        Vector2 delta,
        Bindable<double> x,
        Bindable<double> y,
        double minimum = YokkoGameplaySettings.MinimumLayoutOffset,
        double maximum = YokkoGameplaySettings.MaximumLayoutOffset) =>
        new(
            Math.Clamp(
                delta.X,
                (float)((minimum - x.Value) * Math.Max(1, DrawWidth)),
                (float)((maximum - x.Value) * Math.Max(1, DrawWidth))),
            Math.Clamp(
                delta.Y,
                (float)((minimum - y.Value) * Math.Max(1, DrawHeight)),
                (float)((maximum - y.Value) * Math.Max(1, DrawHeight))));

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
        Colour = Color4.Black,
    };

    private static Container createMiniReadout(Color4 colour) => new()
    {
        Masking = true,
        BorderThickness = 1,
        BorderColour = colour,
        Child = new Box
        {
            RelativeSizeAxes = Axes.Both,
            Colour = new Color4(colour.R, colour.G, colour.B, 0.5f),
        },
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
        private readonly Func<Vector2, Vector2> constrainMoveDelta;
        private readonly Action<float> scroll;
        private readonly Container frame;
        private readonly Container labelPanel;
        private readonly Container handles;
        private readonly Box selectionTint;
        private Vector2 dragStartMousePosition;
        private Vector2 dragStartLogicalPosition;
        private Vector2? dragLogicalPosition;
        private Axes? constrainedDragAxis;
        private bool selected;
        private bool hovered;

        internal LayoutElementKind Kind { get; }

        internal int ResizeHandleCount => resize == null ? 0 : 4;

        internal bool CanResize => resize != null;

        internal bool IsLocked { get; private set; }

        internal bool EditorHidden { get; private set; }

        internal bool AspectLocked { get; private set; }

        internal float LockedAspectRatio { get; private set; } = 1;

        internal bool CanEdit => !IsLocked && !EditorHidden;

        internal Vector2 MovementPosition => dragLogicalPosition ?? Position;

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
            Func<Vector2, Vector2> constrainMoveDelta = null,
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
            this.constrainMoveDelta = constrainMoveDelta;
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
                handles = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                },
            };

            if (resize != null)
            {
                handles.Children = new Drawable[]
                {
                    createHandle(
                        Anchor.TopLeft,
                        ResizeEdges.Left | ResizeEdges.Top),
                    createHandle(
                        Anchor.TopRight,
                        ResizeEdges.Right | ResizeEdges.Top),
                    createHandle(
                        Anchor.BottomLeft,
                        ResizeEdges.Left | ResizeEdges.Bottom),
                    createHandle(
                        Anchor.BottomRight,
                        ResizeEdges.Right | ResizeEdges.Bottom),
                };
            }

            updateEmphasis();
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
                () => selected && CanEdit,
                () => new Vector2(DrawWidth, DrawHeight),
                anchor,
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
            dragStartMousePosition = coordinateSpace.ToLocalSpace(
                e.ScreenSpaceMousePosition);
            constrainedDragAxis = null;
            return true;
        }

        protected override bool OnDragStart(DragStartEvent e)
        {
            if (!CanEdit)
                return false;

            beginChange();
            dragStartLogicalPosition = Position;
            dragLogicalPosition = Position;
            return true;
        }

        protected override bool OnDoubleClick(DoubleClickEvent e)
        {
            if (!CanEdit)
                return false;

            select(this);
            beginChange();
            reset();
            return true;
        }

        protected override bool OnHover(HoverEvent e)
        {
            hovered = true;
            updateEmphasis();
            return true;
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            hovered = false;
            updateEmphasis();
        }

        protected override void OnDrag(DragEvent e)
        {
            Vector2 current = coordinateSpace.ToLocalSpace(
                e.ScreenSpaceMousePosition);
            applyPointerDrag(current, e.ShiftPressed, e.AltPressed);
        }

        private void applyPointerDrag(
            Vector2 current,
            bool shiftPressed,
            bool altPressed)
        {
            // Keep the unsnapped displacement from the start of the gesture.
            // If snapping is applied to each individual mouse event, slow
            // movement never accumulates past the snap threshold and a
            // centred element can feel permanently stuck.
            Vector2 totalDelta = current - dragStartMousePosition;
            if (shiftPressed)
            {
                constrainedDragAxis ??=
                    Math.Abs(totalDelta.X) >= Math.Abs(totalDelta.Y)
                        ? Axes.X
                        : Axes.Y;
                if (constrainedDragAxis == Axes.X)
                    totalDelta.Y = 0;
                else
                    totalDelta.X = 0;
            }
            else
                constrainedDragAxis = null;

            Vector2 requestedPosition =
                dragStartLogicalPosition + totalDelta;
            Vector2 requestedDelta =
                requestedPosition - MovementPosition;
            Vector2 delta = snapMove(
                this,
                requestedDelta,
                altPressed);
            Vector2 realisedDelta = MoveBy(delta);
            dragLogicalPosition = MovementPosition + realisedDelta;
        }

        protected override void OnDragEnd(DragEndEvent e)
        {
            constrainedDragAxis = null;
            dragLogicalPosition = null;
            clearGuides();
            base.OnDragEnd(e);
        }

        protected override bool OnScroll(ScrollEvent e)
        {
            if (scroll == null
                || e.ScrollDelta.Y == 0
                || !e.ControlPressed)
            {
                return false;
            }

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
            updateEmphasis();
        }

        private void updateEmphasis()
        {
            bool emphasised = selected || hovered;
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
                    : new Color4(
                        HomeControlColours.Cyan.R,
                        HomeControlColours.Cyan.G,
                        HomeControlColours.Cyan.B,
                        emphasised ? 0.92f : 0.24f);
            frame.BorderThickness = selected ? 3 : 2;
            labelPanel.BorderColour = selected
                ? HomeControlColours.Yellow
                : HomeControlColours.Navy;
            labelPanel.FadeTo(
                emphasised ? 1 : 0,
                90,
                Easing.OutQuint);
            handles.FadeTo(
                selected ? 1 : 0,
                90,
                Easing.OutQuint);
            selectionTint.Colour = new Color4(
                HomeControlColours.PaleCyan.R,
                HomeControlColours.PaleCyan.G,
                HomeControlColours.PaleCyan.B,
                selected ? 0.03f : hovered ? 0.018f : 0);
        }

        internal void Reset() => reset();

        internal Vector2 MoveBy(Vector2 delta)
        {
            Vector2 constrained =
                constrainMoveDelta?.Invoke(delta) ?? delta;
            drag(constrained);
            return constrained;
        }

        internal void ResizeBy(ResizeEdges edges, Vector2 delta) =>
            resize?.Invoke(edges, delta);

        internal void DragPointerIncrementallyForTest(
            Vector2 totalDelta,
            int steps)
        {
            if (!CanEdit)
                return;

            beginChange();
            dragStartMousePosition = Vector2.Zero;
            dragStartLogicalPosition = Position;
            dragLogicalPosition = Position;
            constrainedDragAxis = null;
            for (int i = 1; i <= Math.Max(1, steps); i++)
            {
                applyPointerDrag(
                    totalDelta * i / Math.Max(1, steps),
                    false,
                    false);
            }

            constrainedDragAxis = null;
            dragLogicalPosition = null;
            clearGuides();
        }

        internal bool CentreAvoidsResizeHandlesForTest
        {
            get
            {
                Vector2 centre = ToScreenSpace(
                    new Vector2(DrawWidth / 2, DrawHeight / 2));
                foreach (Drawable child in handles.Children)
                {
                    if (child.ReceivePositionalInputAt(centre))
                        return false;
                }

                return true;
            }
        }

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
            if (!CanResize)
            {
                AspectLocked = false;
                return;
            }

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

            Vector2 requestedDelta =
                direction * Math.Max(0.1f, distance);
            Vector2 delta = snapMove(this, requestedDelta, true);
            MoveBy(delta);
            return true;
        }

        internal bool TryResize(Key key, float distance)
        {
            if (!CanEdit || resize == null)
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
        private readonly Func<Vector2> targetSize;
        private readonly Anchor anchor;
        private readonly Box fill;
        private readonly Color4 idleColour;
        private Vector2 lastPosition;
        private Vector2 pendingDelta;

        public ResizeHandle(
            Drawable coordinateSpace,
            Action<Vector2> resize,
            Action beginChange,
            Action select,
            Func<bool> canEdit,
            Func<Vector2> targetSize,
            Anchor anchor,
            bool edge)
        {
            this.coordinateSpace = coordinateSpace;
            this.resize = resize;
            this.beginChange = beginChange;
            this.select = select;
            this.canEdit = canEdit;
            this.targetSize = targetSize;
            this.anchor = anchor;
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

        public override bool ReceivePositionalInputAt(
            Vector2 screenSpacePos)
        {
            if (!base.ReceivePositionalInputAt(screenSpacePos))
                return false;

            Vector2 local = ToLocalSpace(screenSpacePos);
            Vector2 size = targetSize();
            float hitWidth = Math.Min(
                DrawWidth,
                Math.Max(3, size.X / 3));
            float hitHeight = Math.Min(
                DrawHeight,
                Math.Max(3, size.Y / 3));
            bool right = anchor is Anchor.TopRight or Anchor.BottomRight;
            bool bottom = anchor is Anchor.BottomLeft or Anchor.BottomRight;
            float distanceX = right ? DrawWidth - local.X : local.X;
            float distanceY = bottom ? DrawHeight - local.Y : local.Y;
            return distanceX >= 0
                   && distanceY >= 0
                   && distanceX <= hitWidth
                   && distanceY <= hitHeight;
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
            pendingDelta += current - lastPosition;
            lastPosition = current;
        }

        protected override void Update()
        {
            base.Update();
            flushPendingResize();
        }

        protected override void OnDragEnd(DragEndEvent e)
        {
            flushPendingResize();
            base.OnDragEnd(e);
        }

        private void flushPendingResize()
        {
            if (pendingDelta == Vector2.Zero)
                return;

            Vector2 delta = pendingDelta;
            pendingDelta = Vector2.Zero;
            resize(delta);
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

    private partial class DemoInputBlocker : CompositeDrawable
    {
        public override bool HandlePositionalInput => Alpha > 0;

        public DemoInputBlocker()
        {
            RelativeSizeAxes = Axes.Both;
            Depth = -100;
            Alpha = 0;
        }

        protected override bool OnMouseDown(MouseDownEvent e) => true;

        protected override bool OnScroll(ScrollEvent e) => true;
    }

    private partial class CoverDragHandle : CompositeDrawable
    {
        private readonly Action<Vector2> update;
        private readonly Action beginChange;
        private readonly Box background;
        private bool active;

        public CoverDragHandle(
            Drawable coordinateSpace,
            LocalisableString label,
            Color4 accentColour,
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
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    RelativeSizeAxes = Axes.X,
                    Height = 4,
                    Colour = accentColour,
                },
                new FillFlowContainer
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(7, 0),
                    Children = new Drawable[]
                    {
                        new SpriteIcon
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Size = new Vector2(13),
                            Icon = FontAwesome.Solid.ArrowsAltV,
                            Colour = accentColour,
                        },
                        new SpriteText
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Text = label,
                            Font = LayoutEditorTypography.Bold(8),
                            Colour = HomeControlColours.Navy,
                        },
                    },
                },
            };

            SetActive(false);
        }

        internal void SetActive(bool value)
        {
            active = value;
            Alpha = active ? 1 : 0;
            background.Colour = active
                ? HomeControlColours.Ivory
                : HomeControlColours.PaleCyan;
            BorderColour = active
                ? HomeControlColours.Navy
                : HomeControlColours.Cyan;
        }

        protected override bool OnMouseDown(MouseDownEvent e)
        {
            if (!active || e.Button != MouseButton.Left)
                return false;

            beginChange();
            update(e.ScreenSpaceMousePosition);
            return true;
        }

        protected override bool OnDragStart(DragStartEvent e) => active;

        protected override void OnDrag(DragEvent e)
        {
            if (active)
                update(e.ScreenSpaceMousePosition);
        }

        protected override bool OnHover(HoverEvent e)
        {
            if (!active)
                return false;

            this.FadeTo(1, 80, Easing.OutQuint);
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
                active
                    ? HomeControlColours.Ivory
                    : HomeControlColours.PaleCyan,
                120,
                Easing.OutQuint);
            BorderColour = active
                ? HomeControlColours.Navy
                : HomeControlColours.Cyan;
            this.FadeTo(active ? 1 : 0, 120, Easing.OutQuint);
        }
    }

    private partial class AutoplayDemoControl : CompositeDrawable
    {
        public AutoplayDemoControl(Action exit)
        {
            Position = new Vector2(18);
            Size = new Vector2(430, 84);
            Depth = -110;

            InternalChildren = new Drawable[]
            {
                new Container
                {
                    Position = new Vector2(5),
                    Size = new Vector2(430, 84),
                    Masking = true,
                    CornerRadius = 8,
                    Child = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = new Color4(
                            HomeControlColours.Cyan.R,
                            HomeControlColours.Cyan.G,
                            HomeControlColours.Cyan.B,
                            0.5f),
                    },
                },
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
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
                        new SpriteText
                        {
                            Position = new Vector2(16, 13),
                            Text = YokkoStrings.Get(
                                "gameplay.layout_editor.autoplay_demo_active"),
                            Font = LayoutEditorTypography.Bold(13),
                            Colour = HomeControlColours.Navy,
                        },
                        new SpriteText
                        {
                            Position = new Vector2(16, 48),
                            Text = YokkoStrings.Get(
                                "gameplay.layout_editor.autoplay_demo_exit_hint"),
                            Font = LayoutEditorTypography.Regular(8.5f),
                            Colour = new Color4(
                                HomeControlColours.Navy.R,
                                HomeControlColours.Navy.G,
                                HomeControlColours.Navy.B,
                                0.66f),
                        },
                        new LayoutActionButton(
                            YokkoStrings.Get(
                                "gameplay.layout_editor.autoplay_demo_exit"),
                            FontAwesome.Solid.SignOutAlt,
                            exit,
                            true)
                        {
                            Anchor = Anchor.CentreRight,
                            Origin = Anchor.CentreRight,
                            Position = new Vector2(-14, 0),
                            Size = new Vector2(112, 44),
                        },
                    },
                },
                new Box
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.Centre,
                    Position = new Vector2(-7, 4),
                    Size = new Vector2(11),
                    Rotation = 45,
                    Colour = HomeControlColours.Yellow,
                },
            };
        }
    }

    private partial class LayoutActionButton : ClickableContainer
    {
        private readonly Box background;
        private readonly SpriteText label;
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
            CornerRadius = 8;
            BorderThickness = 1.25f;
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
                            Size = new Vector2(15),
                            Icon = icon,
                            Colour = primary
                                ? HomeControlColours.Yellow
                                : HomeControlColours.Navy,
                        },
                        label = new SpriteText
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Text = text,
                            Font = LayoutEditorTypography.Bold(10),
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

        internal void SetText(LocalisableString text) =>
            label.Text = text;

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
        double AccuracyOffsetX,
        double AccuracyOffsetY,
        double AccuracyScaleX,
        double AccuracyScaleY,
        double ProgressOffsetX,
        double ProgressOffsetY,
        double ProgressScaleX,
        double ProgressScaleY,
        double TimingBarOffsetX,
        double TimingBarOffsetY,
        double TimingBarScaleX,
        double TimingBarScaleY,
        double ComboOffsetX,
        double ComboOffsetY,
        double ComboScaleX,
        double ComboScaleY,
        double JudgementOffsetX,
        double JudgementOffsetY,
        double JudgementScaleX,
        double JudgementScaleY,
        double PerformanceReadoutOffsetX,
        double PerformanceReadoutOffsetY,
        double PlayfieldVisible,
        double AccuracyVisible,
        double ProgressVisible,
        double InformationVisible,
        double TimingBarVisible,
        double ComboVisible,
        double JudgementVisible,
        double PerformanceReadoutVisible,
        double HitEffectsVisible,
        double JudgementLineOffsetY,
        double TopCoverRatio,
        double BottomCoverRatio);

    private readonly record struct LiveSettingsSnapshot(
        string SkinId,
        double ScrollSpeed,
        ManiaScrollDirection ScrollDirection,
        double BackgroundDim,
        bool LongNoteCutEnabled,
        double LongNoteCutAmount,
        double JudgementDisplayDuration,
        double JudgementOpacity,
        double JudgementHitErrorScale,
        bool ShowJudgementHitError,
        bool ShowTimingBar);

    private static class LayoutEditorTypography
    {
        public static FontUsage Regular(float size) =>
            new("PlusJakartaSans", readableSize(size));

        public static FontUsage Bold(float size) =>
            new("PlusJakartaSans", readableSize(size));

        private static float readableSize(float size) =>
            MathF.Max(23, size + MathF.Min(15, 12 + size * 0.12f));
    }
}
