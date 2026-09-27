using osu.Framework.Graphics;
using osuTK;

namespace Yokko.Game.Presentation;

/// <summary>
/// Shared navigation choreography. Durations are in milliseconds; offsets are
/// in the drawable's local layout coordinates, never physical window pixels.
/// </summary>
internal static class YokkoMotion
{
    internal const double EnterDuration = 240;
    internal const double ExitDuration = 180;
    internal const double ContentDuration = 320;
    internal const double Stagger = 35;
    internal const double ReducedDuration = 80;

    internal static void Enter(Drawable target, bool reduceMotion)
    {
        clearFade(target);
        target.FadeInFromZero(reduceMotion ? ReducedDuration : EnterDuration, Easing.OutCubic);
    }

    internal static void Exit(Drawable target, bool reduceMotion)
    {
        // Replace only our fade, retaining the current value when interrupted.
        clearFade(target);
        target.FadeOut(reduceMotion ? ReducedDuration : ExitDuration, Easing.OutCubic);
    }

    internal static void Restore(Drawable target)
    {
        clearFade(target);
        target.Alpha = 1;
    }

    internal static void Hold(Drawable target)
    {
        clearFade(target);
        // ScreenStack.Expire() runs after OnSuspending. Give the screen a first
        // update in which it can extend its lifetime until the new page covers it.
        target.FadeTo(1, EnterDuration, Easing.OutCubic);
    }

    internal static void Reveal(
        Drawable target, Vector2 restingPosition, Vector2 offset,
        bool reduceMotion, double delay = 0)
    {
        clearFade(target);
        target.ClearTransforms(targetMember: nameof(Drawable.Position));
        target.ClearTransforms(targetMember: nameof(Drawable.X));
        target.ClearTransforms(targetMember: nameof(Drawable.Y));
        target.Position = restingPosition + (reduceMotion ? Vector2.Zero : offset);
        target.Alpha = 0;
        target.Delay(reduceMotion ? 0 : delay)
              .FadeIn(reduceMotion ? ReducedDuration : EnterDuration, Easing.OutCubic)
              .MoveTo(restingPosition, reduceMotion ? 0 : ContentDuration, Easing.OutQuint);
    }

    private static void clearFade(Drawable target) =>
        target.ClearTransforms(targetMember: nameof(Drawable.Alpha));
}
