using NUnit.Framework;
using Yokko.Game.Screens.SongSelect;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public class GameplayModSettingsControlTest
{
    [Test]
    public void DisabledStateButtonCannotActivateItsAction()
    {
        int activationCount = 0;
        var button = new GameplayModSettingsStateButton(
            "TEST SETTING",
            () => activationCount++);

        button.SetState(false, false);
        button.ActivateForTest();

        Assert.That(button.InteractionEnabled, Is.False);
        Assert.That(button.AcceptsFocus, Is.False);
        Assert.That(activationCount, Is.Zero);

        button.SetState(true, false);
        button.ActivateForTest();

        Assert.That(button.InteractionEnabled, Is.True);
        Assert.That(button.AcceptsFocus, Is.True);
        Assert.That(activationCount, Is.EqualTo(1));
    }

    [Test]
    public void DisabledStepButtonCannotActivateItsAction()
    {
        int activationCount = 0;
        var button = new GameplayModSettingsStepButton(
            "+",
            () => activationCount++);

        button.SetEnabled(false);
        button.ActivateForTest();

        Assert.That(button.AcceptsFocus, Is.False);
        Assert.That(activationCount, Is.Zero);

        button.SetEnabled(true);
        button.ActivateForTest();

        Assert.That(button.AcceptsFocus, Is.True);
        Assert.That(activationCount, Is.EqualTo(1));
    }

    [Test]
    public void SharedStylesPreserveExistingGeometry()
    {
        var rateStep = new GameplayModSettingsStepButton(
            "−",
            () => { });
        var mutedStep = new GameplayModSettingsStepButton(
            "− 25",
            () => { },
            GameplayModSettingsControlStyle.Muted);
        var rateState = new GameplayModSettingsStateButton(
            "RATE",
            () => { });
        var mutedState = new GameplayModSettingsStateButton(
            "MUTED",
            () => { },
            GameplayModSettingsControlStyle.Muted);

        Assert.Multiple(() =>
        {
            Assert.That(rateStep.Size.X, Is.EqualTo(46));
            Assert.That(rateStep.Size.Y, Is.EqualTo(29));
            Assert.That(mutedStep.Size.X, Is.EqualTo(97));
            Assert.That(mutedStep.Size.Y, Is.EqualTo(30));
            Assert.That(rateState.Size.Y, Is.EqualTo(29));
            Assert.That(mutedState.Size.Y, Is.EqualTo(27));
        });
    }
}
