using NUnit.Framework;
using Yokko.Game.Screens.Gameplay;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class GameplayAccuracyProgressDisplayTest
{
    [Test]
    public void ProgressUsesPlayableBoundsAndFallsBackWithoutSkinFont()
    {
        var display = new GameplayAccuracyProgressDisplay(
            null,
            1000,
            5000);

        display.UpdateState(500, null);
        Assert.Multiple(() =>
        {
            Assert.That(display.DisplayedProgress, Is.Zero);
            Assert.That(display.DisplayedProgressTime,
                Is.EqualTo("PROGRESS  00:00 / 00:04"));
            Assert.That(display.UsesSkinAccuracyFont, Is.False);
        });

        display.UpdateState(3000, null);
        Assert.Multiple(() =>
        {
            Assert.That(display.DisplayedProgress, Is.EqualTo(0.5));
            Assert.That(display.DisplayedProgressTime,
                Is.EqualTo("PROGRESS  00:02 / 00:04"));
        });

        display.UpdateState(8000, null);
        Assert.That(display.DisplayedProgress, Is.EqualTo(1));
    }
}
