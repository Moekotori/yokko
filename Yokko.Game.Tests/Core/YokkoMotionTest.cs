using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Timing;
using osuTK;
using Yokko.Game.Presentation;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public partial class YokkoMotionTest
{
    [Test]
    public void ReducedMotionHasNoTravelOrStagger()
    {
        using var target = new MotionContainer();
        var resting = new Vector2(40, 80);
        YokkoMotion.Reveal(target, resting, new Vector2(20, 12), true, 500);
        Assert.That(target.Position, Is.EqualTo(resting));
        target.Advance(YokkoMotion.ReducedDuration);
        Assert.That(target.Alpha, Is.EqualTo(1));
        Assert.That(target.Position, Is.EqualTo(resting));
    }

    [Test]
    public void ResumeCancelsPendingFadeWithoutTouchingOtherAnimation()
    {
        using var target = new MotionContainer();
        target.RotateTo(30, 600);
        YokkoMotion.Exit(target, false);
        YokkoMotion.Restore(target);
        target.Advance(1000);
        Assert.That(target.Alpha, Is.EqualTo(1));
        Assert.That(target.Rotation, Is.EqualTo(30));
    }

    [Test]
    public void ExitDoesNotJumpToFullOpacityWhenEntryIsInterrupted()
    {
        using var target = new MotionContainer { Alpha = 0.4f };
        YokkoMotion.Exit(target, false);
        Assert.That(target.Alpha, Is.EqualTo(0.4f));
        target.Advance(YokkoMotion.ExitDuration);
        Assert.That(target.Alpha, Is.Zero);
    }

    [Test]
    public void ForwardNavigationContinuesInterruptedEntryWithoutSnapping()
    {
        using var target = new MotionContainer { Alpha = 0.4f };
        YokkoMotion.Hold(target);
        Assert.That(target.Alpha, Is.EqualTo(0.4f));
        target.Advance(YokkoMotion.EnterDuration);
        Assert.That(target.Alpha, Is.EqualTo(1));
    }

    [Test]
    public void ContentSettlesAtExplicitRestingPosition()
    {
        using var target = new MotionContainer();
        var resting = new Vector2(-100, 50);
        YokkoMotion.Reveal(target, resting, new Vector2(20, 8), false, YokkoMotion.Stagger);
        target.Advance(YokkoMotion.ContentDuration + YokkoMotion.Stagger);
        Assert.That(target.Position, Is.EqualTo(resting));
        Assert.That(target.Alpha, Is.EqualTo(1));
    }

    private partial class MotionContainer : Container
    {
        private readonly ManualClock clock = new();

        public MotionContainer() => Clock = new FramedClock(clock);

        internal void Advance(double time)
        {
            clock.CurrentTime = time;
            Clock.ProcessFrame();
            UpdateTransforms();
        }
    }
}
