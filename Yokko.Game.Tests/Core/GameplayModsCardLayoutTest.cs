using System.Linq;
using NUnit.Framework;
using osuTK;
using Yokko.Game.Screens.SongSelect;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public class GameplayModsCardLayoutTest
{
    [Test]
    public void SevenConversionFamiliesFitCardBrowser()
    {
        Vector2[] positions = Enumerable.Range(0, 7)
            .Select(GameplayModsOrbitWorkspace.CalculateModCardPosition)
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(positions.All(position => position.X == 24), Is.True);
            Assert.That(positions.Select(position => position.Y).Distinct().Count(), Is.EqualTo(7));
            Assert.That(positions[^1].Y + 60, Is.LessThanOrEqualTo(620));
        });
    }
}
