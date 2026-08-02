using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Screens;
using osuTK;
using Yokko.Game.Presentation;
using Yokko.Game.Importing;

namespace Yokko.Game.Screens.Gameplay;

/// <summary>
/// Keeps gameplay replacement inside one top-level screen so retry loading
/// cannot briefly resume the screen that launched the play session.
/// </summary>
internal partial class GameplaySessionScreen : Screen
{
    [Resolved]
    private YokkoFrameRateAdaptation frameRateAdaptation { get; set; }
    [Resolved]
    private ImportedChartLibrary importedChartLibrary { get; set; }

    private readonly GameplayScreen initialGameplay;
    private ScreenStack gameplayStack;
    private GameplaySessionRootScreen root;
    private GameplayRetryTransitionOverlay retryTransition;
    private bool retryTransitionActive;
    private bool retryHandoffInProgress;
    private bool initialGameplayPreloaded;
    private GameplayScreen pendingReplacement;
    private bool initialRevealStarted;
    private bool revealStarted;
    private bool frameRateSessionActive;

    private const double initialFadeDurationMilliseconds = 140;
    private const double initialMotionDurationMilliseconds = 220;
    private const float initialScale = 1.012f;
    private const float initialOffsetY = 10;

    internal GameplayScreen CurrentGameplay =>
        gameplayStack?.CurrentScreen as GameplayScreen;
    internal bool InitialGameplayPreloaded => initialGameplayPreloaded;
    internal bool InitialPresentationReady =>
        initialGameplayPreloaded
        && initialGameplay.PresentationTexturesReady;
    internal int PendingInitialTextureUploads =>
        initialGameplay.PendingPresentationTextureUploads;
    internal bool InitialRevealStarted => initialRevealStarted;
    internal bool InitialRevealAnimationComplete =>
        initialRevealStarted
        && Alpha == 1
        && gameplayStack?.Scale == Vector2.One
        && gameplayStack.Y == 0;
    internal bool RetryTransitionActive => retryTransitionActive;

    internal GameplaySessionScreen(GameplayScreen initialGameplay)
    {
        this.initialGameplay = initialGameplay
                               ?? throw new ArgumentNullException(
                                   nameof(initialGameplay));

        // The session becomes the outer stack's current screen before its
        // nested gameplay has finished loading. Keep the launching screen
        // visible through that gap instead of exposing the session's dark
        // fallback background.
        Alpha = 0.001f;
    }

    public override void OnEntering(ScreenTransitionEvent e)
    {
        base.OnEntering(e);
        if (frameRateSessionActive)
            return;

        frameRateSessionActive = true;
        frameRateAdaptation.BeginSession();
        importedChartLibrary.SetExternalIndexingPaused(true);
    }

    public override bool OnExiting(ScreenExitEvent e)
    {
        bool blocked = base.OnExiting(e);
        if (!blocked)
        {
            importedChartLibrary.SetExternalIndexingPaused(false);
            endFrameRateSession();
        }

        return blocked;
    }

    [BackgroundDependencyLoader]
    private void load()
    {
        // Load the expensive gameplay tree while the launching screen is
        // still current. Gameplay timing and audio are armed by OnEntering,
        // so this is preparation only and cannot consume the lead-in early.
        LoadComponent(initialGameplay);
        initialGameplayPreloaded = true;

        root = new GameplaySessionRootScreen(this);
        gameplayStack = new ScreenStack(root)
        {
            RelativeSizeAxes = Axes.Both,
        };
        gameplayStack.ScreenExited += onGameplayScreenExited;
        InternalChildren =
        [
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = YokkoPalette.Background,
            },
            gameplayStack,
            retryTransition = new GameplayRetryTransitionOverlay(),
        ];
    }

    internal void OpenInitialGameplay()
    {
        if (gameplayStack.CurrentScreen == root)
            root.Push(initialGameplay);
    }

    internal void BeginRetryTransition()
    {
        if (retryTransitionActive)
            return;

        retryTransitionActive = true;
        revealStarted = false;
        retryTransition.BeginCover();
    }

    internal void CancelRetryTransition()
    {
        retryTransitionActive = false;
        pendingReplacement = null;
        revealStarted = false;
        retryTransition.ResetInstant();
    }

    internal void ReplaceGameplay(GameplayScreen replacement)
    {
        if (pendingReplacement != null)
            return;

        BeginRetryTransition();
        pendingReplacement = replacement;
        revealStarted = false;
        performGameplayReplacement(replacement);
    }

    private void performGameplayReplacement(GameplayScreen replacement)
    {
        if (!ReferenceEquals(pendingReplacement, replacement))
            return;

        retryHandoffInProgress = true;
        try
        {
            root.MakeCurrent();
            root.Push(replacement);
        }
        finally
        {
            retryHandoffInProgress = false;
        }
    }

    protected override void Update()
    {
        base.Update();

        if (!initialRevealStarted
            && initialGameplay.IsLoaded
            && ReferenceEquals(CurrentGameplay, initialGameplay))
        {
            initialRevealStarted = true;
            ClearTransforms();
            this.FadeTo(1, initialFadeDurationMilliseconds,
                Easing.OutQuint);

            gameplayStack.ClearTransforms();
            gameplayStack.Scale = new Vector2(initialScale);
            gameplayStack.Y = initialOffsetY;
            gameplayStack.ScaleTo(1, initialMotionDurationMilliseconds,
                             Easing.OutQuint)
                         .MoveToY(0, initialMotionDurationMilliseconds,
                             Easing.OutQuint);
        }

        if (pendingReplacement == null
            || revealStarted
            || !pendingReplacement.IsLoaded
            || !pendingReplacement.PresentationTexturesReady
            || !retryTransition.CoverComplete
            || !ReferenceEquals(CurrentGameplay, pendingReplacement))
        {
            return;
        }

        revealStarted = true;
        retryTransition.BeginReveal();
        Scheduler.AddDelayed(completeRetryTransition,
            GameplayRetryTransitionOverlay.RevealDurationMilliseconds);
    }

    private void completeRetryTransition()
    {
        gameplayStack.ClearTransforms();
        gameplayStack.Alpha = 1;
        gameplayStack.Scale = Vector2.One;
        retryTransition.ResetInstant();
        pendingReplacement = null;
        revealStarted = false;
        retryTransitionActive = false;
    }

    private void onGameplayScreenExited(IScreen last, IScreen next)
    {
        if (!retryHandoffInProgress
            && last is GameplayScreen
            && ReferenceEquals(next, root))
        {
            this.Exit();
        }
    }

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
        {
            endFrameRateSession();
            if (gameplayStack != null)
                gameplayStack.ScreenExited -= onGameplayScreenExited;
        }

        base.Dispose(isDisposing);
    }

    private void endFrameRateSession()
    {
        if (!frameRateSessionActive)
            return;

        frameRateSessionActive = false;
        frameRateAdaptation.EndSession();
    }
}

internal sealed partial class GameplaySessionRootScreen : Screen
{
    private readonly GameplaySessionScreen session;

    internal GameplaySessionRootScreen(GameplaySessionScreen session)
    {
        this.session = session;
    }

    public override void OnEntering(ScreenTransitionEvent e)
    {
        base.OnEntering(e);
        Scheduler.Add(session.OpenInitialGameplay);
    }

    internal void ReplaceGameplay(GameplayScreen replacement) =>
        session.ReplaceGameplay(replacement);

    internal void BeginRetryTransition() =>
        session.BeginRetryTransition();

    internal void CancelRetryTransition() =>
        session.CancelRetryTransition();

    internal bool RetryTransitionActive =>
        session.RetryTransitionActive;
}
