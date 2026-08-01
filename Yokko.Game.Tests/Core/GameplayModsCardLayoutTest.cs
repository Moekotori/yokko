using System;
using System.Linq;
using NUnit.Framework;
using osuTK;
using Yokko.Game.Screens.SongSelect;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public class GameplayModsArcLayoutTest
{
    [Test]
    public void SixFamiliesFollowRightHandArc()
    {
        Vector2[] positions = Enumerable.Range(0, 6)
            .Select(GameplayModsOrbitWorkspace.CalculateModArcPosition)
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(positions.Take(4).Select(position => position.Y),
                Is.Ordered.Ascending);
            Assert.That(positions[0].X, Is.LessThan(positions[2].X));
            Assert.That(positions[^1].X, Is.LessThan(positions[0].X));
            Assert.That(positions.All(position => position.X >= 150), Is.True);
            Assert.That(positions.All(position => position.Y <= 524), Is.True);
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                GameplayModsOrbitWorkspace.CalculateModArcPosition(6));
        });
    }
}
