using NUnit.Framework;
using Yokko.Core.Timing;

namespace Yokko.Game.Tests.Core
{
    [TestFixture]
    public sealed class ScrollVelocityMapTest
    {
        [Test]
        public void IntegratesPositiveZeroAndNegativeSegmentsContinuously()
        {
            var map = new ScrollVelocityMap(
            [
                new YokkoScrollVelocity(1000, 2),
                new YokkoScrollVelocity(1500, 0),
                new YokkoScrollVelocity(2000, -1),
            ]);

            Assert.That(map.PositionAt(500), Is.EqualTo(500));
            Assert.That(map.PositionAt(1000), Is.EqualTo(1000));
            Assert.That(map.PositionAt(1500), Is.EqualTo(2000));
            Assert.That(map.PositionAt(2000), Is.EqualTo(2000));
            Assert.That(map.PositionAt(2500), Is.EqualTo(1500));
            Assert.That(map.DistanceBetween(1250, 2250), Is.EqualTo(250));
        }

        [Test]
        public void LastVelocityAtSameTimeWins()
        {
            var map = new ScrollVelocityMap(
            [
                new YokkoScrollVelocity(1000, 2),
                new YokkoScrollVelocity(1000, -0.5),
            ],
                initialMultiplier: 1.5);

            Assert.That(map.PositionAt(1000), Is.EqualTo(1500));
            Assert.That(map.MultiplierAt(999), Is.EqualTo(1.5));
            Assert.That(map.MultiplierAt(1000), Is.EqualTo(-0.5));
        }

        [Test]
        public void PositionRangeIncludesDirectionChangesInsideHold()
        {
            var map = new ScrollVelocityMap(
            [
                new YokkoScrollVelocity(1000, 2),
                new YokkoScrollVelocity(1500, 0),
                new YokkoScrollVelocity(2000, -1),
            ]);

            ScrollPositionRange range = map.PositionRangeBetween(1250, 2250);

            Assert.That(map.PositionAt(1250), Is.EqualTo(1500));
            Assert.That(map.PositionAt(2250), Is.EqualTo(1750));
            Assert.That(range.Minimum, Is.EqualTo(1500));
            Assert.That(range.Maximum, Is.EqualTo(2000));
        }
    }
}
