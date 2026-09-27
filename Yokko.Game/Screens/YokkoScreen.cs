using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Screens;
using Yokko.Game.Presentation;

namespace Yokko.Game.Screens;

/// <summary>
/// Shared page transitions; page-specific music, state and layout stay in the page.
/// </summary>
public abstract partial class YokkoScreen : Screen
{
    [Resolved(CanBeNull = true)]
    private YokkoAccessibilitySettings motionAccessibility { get; set; }

    protected bool ReduceMotionEnabled => motionAccessibility?.ReduceMotion.Value == true;

    private IScreen coveringScreen;

    public override void OnEntering(ScreenTransitionEvent e)
    {
        base.OnEntering(e);
        YokkoMotion.Enter(this, ReduceMotionEnabled);
    }

    public override void OnSuspending(ScreenTransitionEvent e)
    {
        base.OnSuspending(e);
        coveringScreen = e.Next;
        YokkoMotion.Hold(this);
    }

    public override void OnResuming(ScreenTransitionEvent e)
    {
        base.OnResuming(e);
        coveringScreen = null;
        // The outgoing page fades above us. Fading both pages would expose the
        // clear colour; restoring also cancels any pending suspension fade.
        YokkoMotion.Restore(this);
    }

    public override bool OnExiting(ScreenExitEvent e)
    {
        if (base.OnExiting(e))
            return true;

        coveringScreen = null;
        YokkoMotion.Exit(this, ReduceMotionEnabled);
        return false;
    }

    protected override void Update()
    {
        base.Update();
        if (coveringScreen == null)
            return;

        Drawable cover = (Drawable)coveringScreen;
        if (coveringScreen.ValidForResume && (!cover.IsLoaded || cover.Alpha < 1))
        {
            // Also covers the nested gameplay session's delayed initial reveal.
            LifetimeEnd = double.MaxValue;
            return;
        }

        coveringScreen = null;
        LifetimeEnd = Time.Current;
    }
}
