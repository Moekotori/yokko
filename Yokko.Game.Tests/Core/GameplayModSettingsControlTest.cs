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
    public void SharedStylesUsePolishedTouchTargets()
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
            Assert.That(rateStep.Size.X, Is.EqualTo(52));
            Assert.That(rateStep.Size.Y, Is.EqualTo(34));
            Assert.That(mutedStep.Size.X, Is.EqualTo(103));
            Assert.That(mutedStep.Size.Y, Is.EqualTo(34));
            Assert.That(rateState.Size.Y, Is.EqualTo(35));
            Assert.That(mutedState.Size.Y, Is.EqualTo(33));
        });
    }
}
