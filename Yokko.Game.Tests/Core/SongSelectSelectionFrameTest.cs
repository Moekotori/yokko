using NUnit.Framework;
using osu.Framework.Timing;
using osuTK;
using Yokko.Game.Screens.SongSelect;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public partial class SongSelectSelectionFrameTest
{
    [Test]
    public void InterruptedSelectionContinuesFromVisiblePosition()
    {
        using var frame = new TimedFrame();
        frame.Follow(Vector2.Zero, new Vector2(976, 64), false);
        frame.Advance(300);
        frame.Follow(new Vector2(4, 200), new Vector2(976, 64), false);
        frame.Advance(390);
        float visibleY = frame.Y;
        Assert.That(visibleY, Is.InRange(1, 199));
        frame.Follow(new Vector2(4, 71), new Vector2(976, 64), false);
        Assert.That(frame.Y, Is.EqualTo(visibleY));
        frame.Advance(700);
        Assert.That(frame.Y, Is.EqualTo(71));
    }

    [Test]
    public void RepeatedLayoutRefreshDoesNotRestartSelectionTravel()
    {
        using var frame = new TimedFrame();
        frame.Follow(Vector2.Zero, new Vector2(976, 64), false);
        frame.Advance(300);
        var destination = new Vector2(4, 142);
        var size = new Vector2(976, 64);
        frame.Follow(destination, size, false);
        frame.Advance(390);
        frame.Follow(destination, size, false);
        frame.Advance(530);
        Assert.That(frame.Position, Is.EqualTo(destination));
    }

    [Test]
    public void ReducedMotionSettlesExistingTravelImmediately()
    {
        using var frame = new TimedFrame();
        frame.Follow(Vector2.Zero, new Vector2(976, 64), false);
        frame.Advance(300);
        var destination = new Vector2(4, 142);
        var size = new Vector2(976, 64);
        frame.Follow(destination, size, false);
        frame.Advance(350);
        frame.Follow(destination, size, true);
        frame.Advance(380);
        Assert.That(frame.Position, Is.EqualTo(destination));
    }

    private partial class TimedFrame : SongSelectSelectionFrame
    {
        private readonly ManualClock clock = new();
        internal TimedFrame() => Clock = new FramedClock(clock);
        internal void Advance(double time)
        {
            clock.CurrentTime = time;
            Clock.ProcessFrame();
            UpdateTransforms();
        }
    }
}
