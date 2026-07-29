using System;
using System.Linq;
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

        [Test]
        public void ZeroVelocityKeepsThePreviousNonZeroDirection()
        {
            var map = new ScrollVelocityMap(
            [
                new YokkoScrollVelocity(1000, -2),
                new YokkoScrollVelocity(1500, 0),
                new YokkoScrollVelocity(2000, 0),
                new YokkoScrollVelocity(2500, 1),
            ]);

            Assert.Multiple(() =>
            {
                Assert.That(
                    map.IsNegativeDirectionAt(999),
                    Is.False);
                Assert.That(
                    map.IsNegativeDirectionAt(1000),
                    Is.True);
                Assert.That(
                    map.IsNegativeDirectionAt(2250),
                    Is.True);
                Assert.That(
                    map.IsNegativeDirectionAt(2500),
                    Is.False);
            });
        }

        [Test]
        public void IndexedPositionRangesMatchBruteForceAcrossDenseSv()
        {
            YokkoScrollVelocity[] velocities =
                Enumerable.Range(0, 4096)
                          .Select(index =>
                              new YokkoScrollVelocity(
                                  index * 7.25,
                                  index % 11 switch
                                  {
                                      0 => 0,
                                      1 or 2 or 3 => -1.5,
                                      _ => 0.75,
                                  }))
                          .ToArray();
            var map = new ScrollVelocityMap(
                velocities,
                initialMultiplier: -0.5);

            for (int query = 0; query < 200; query++)
            {
                double first = (query * 137.5) % 31_000 - 500;
                double second =
                    (query * 983.25 + 1234) % 31_000 - 500;
                double start = Math.Min(first, second);
                double end = Math.Max(first, second);
                double[] positions = velocities
                                     .Where(velocity =>
                                         velocity.TimeMilliseconds > start
                                         && velocity.TimeMilliseconds < end)
                                     .Select(velocity =>
                                         map.PositionAt(
                                             velocity.TimeMilliseconds))
                                     .Append(map.PositionAt(start))
                                     .Append(map.PositionAt(end))
                                     .ToArray();
                ScrollPositionRange indexed =
                    map.PositionRangeBetween(first, second);

                Assert.Multiple(() =>
                {
                    Assert.That(
                        indexed.Minimum,
                        Is.EqualTo(positions.Min()).Within(0.000001));
                    Assert.That(
                        indexed.Maximum,
                        Is.EqualTo(positions.Max()).Within(0.000001));
                });
            }
        }
    }
}
