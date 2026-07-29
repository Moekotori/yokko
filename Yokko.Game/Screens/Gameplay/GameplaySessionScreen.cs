using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Screens;
using Yokko.Game.Presentation;

namespace Yokko.Game.Screens.Gameplay;

/// <summary>
/// Keeps gameplay replacement inside one top-level screen so retry loading
/// cannot briefly resume the screen that launched the play session.
/// </summary>
internal partial class GameplaySessionScreen : Screen
{
    private readonly GameplayScreen initialGameplay;
    private ScreenStack gameplayStack;
    private GameplaySessionRootScreen root;
    private bool retryHandoffInProgress;

    internal GameplayScreen CurrentGameplay =>
        gameplayStack?.CurrentScreen as GameplayScreen;

    internal GameplaySessionScreen(GameplayScreen initialGameplay)
    {
        this.initialGameplay = initialGameplay
                               ?? throw new ArgumentNullException(
                                   nameof(initialGameplay));
    }

    [BackgroundDependencyLoader]
    private void load()
    {
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
        ];
    }

    internal void OpenInitialGameplay()
    {
        if (gameplayStack.CurrentScreen == root)
            root.Push(initialGameplay);
    }

    internal void ReplaceGameplay(GameplayScreen replacement)
    {
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
        if (isDisposing && gameplayStack != null)
            gameplayStack.ScreenExited -= onGameplayScreenExited;

        base.Dispose(isDisposing);
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
}
