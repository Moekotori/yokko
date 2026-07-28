using NUnit.Framework;
using Yokko.Core.Timing;

namespace Yokko.Game.Tests.Core
{
    [TestFixture]
    public sealed class ScrollSpeedFactorMapTest
    {
        [Test]
        public void InterpolatesBetweenQuaverStyleKeyframes()
        {
            var map = new ScrollSpeedFactorMap(
            [
                new YokkoScrollSpeedFactor(1000, 0.5),
                new YokkoScrollSpeedFactor(2000, -1.5),
            ]);

            Assert.That(map.FactorAt(999), Is.EqualTo(1));
            Assert.That(map.FactorAt(1000), Is.EqualTo(0.5));
            Assert.That(map.FactorAt(1500), Is.EqualTo(-0.5));
            Assert.That(map.FactorAt(2000), Is.EqualTo(-1.5));
            Assert.That(map.FactorAt(3000), Is.EqualTo(-1.5));
        }

        [Test]
        public void LastFactorAtSameTimeWins()
        {
            var map = new ScrollSpeedFactorMap(
            [
                new YokkoScrollSpeedFactor(0, 2),
                new YokkoScrollSpeedFactor(0, 0.25),
            ]);

            Assert.That(map.FactorAt(0), Is.EqualTo(0.25));
        }
    }
}
