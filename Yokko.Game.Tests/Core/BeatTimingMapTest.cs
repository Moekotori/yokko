using NUnit.Framework;
using Yokko.Core.Timing;

namespace Yokko.Game.Tests.Core
{
    [TestFixture]
    public sealed class BeatTimingMapTest
    {
        [Test]
        public void RowsFollowActiveUninheritedTimingPoint()
        {
            var timingMap = new BeatTimingMap(
            [
                new YokkoTimingPoint(0, 500),
                new YokkoTimingPoint(1000, -50, Uninherited: false),
                new YokkoTimingPoint(2000, 400),
            ]);

            Assert.That(timingMap.TimeAtRow(16), Is.EqualTo(2000).Within(0.001));
            Assert.That(timingMap.TimeAtRow(20), Is.EqualTo(2400).Within(0.001));
            Assert.That(timingMap.ClosestRowAt(2205), Is.EqualTo(18));
            Assert.That(timingMap.TimingPointAt(1500).BeatLengthMilliseconds, Is.EqualTo(500));
        }

        [Test]
        public void BeatAndMeasureRowsResetAtTimingChanges()
        {
            var timingMap = new BeatTimingMap(
            [
                new YokkoTimingPoint(0, 500, Meter: 4),
                new YokkoTimingPoint(2000, 400, Meter: 3),
            ]);

            Assert.That(timingMap.IsMeasureRow(0), Is.True);
            Assert.That(timingMap.IsBeatRow(4), Is.True);
            Assert.That(timingMap.IsMeasureRow(16), Is.True);
            Assert.That(timingMap.IsMeasureRow(28), Is.True);
        }

        [Test]
        public void FirstTimingPointExtendsGridBackwardsToNonNegativeTime()
        {
            var timingMap = new BeatTimingMap([new YokkoTimingPoint(1000, 500)]);

            Assert.That(timingMap.TimeAtRow(0), Is.EqualTo(0).Within(0.001));
            Assert.That(timingMap.TimeAtRow(8), Is.EqualTo(1000).Within(0.001));
            Assert.That(timingMap.ClosestRowAt(500), Is.EqualTo(4));
        }
    }
}
