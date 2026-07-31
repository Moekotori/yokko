using NUnit.Framework;
using osuTK;
using Yokko.Game.Screens.SongSelect;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public class GameplayModsLayoutTest
{
    [TestCase(1600, 900, 0, 0)]
    [TestCase(1600, 1000, 0, 50)]
    [TestCase(1800, 1000, 100, 50)]
    [TestCase(960, 540, 0, 0)]
    public void AuthoredContentRemainsCentredWhileWorkspaceFillsViewport(
        float width,
        float height,
        float expectedX,
        float expectedY)
    {
        Assert.That(
            GameplayModsOrbitWorkspace.CalculateAuthoredContentOffset(
                new Vector2(width, height)),
            Is.EqualTo(new Vector2(expectedX, expectedY)));
    }
}
