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
        public void RowLookupsResolveSegmentBoundariesAcrossManySegments()
        {
            // Each segment spans exactly 10 rows, so every StartRow boundary
            // is known: 0, 10, 20, 30 and 40.
            var timingMap = new BeatTimingMap(
            [
                new YokkoTimingPoint(0, 400),
                new YokkoTimingPoint(1000, 800),
                new YokkoTimingPoint(3000, 400),
                new YokkoTimingPoint(4000, 200),
                new YokkoTimingPoint(4500, 100),
            ]);

            Assert.Multiple(() =>
            {
                Assert.That(timingMap.TimeAtRow(0), Is.EqualTo(0).Within(0.001));
                Assert.That(timingMap.TimeAtRow(9), Is.EqualTo(900).Within(0.001));
                Assert.That(timingMap.TimeAtRow(10), Is.EqualTo(1000).Within(0.001));
                Assert.That(timingMap.TimeAtRow(19), Is.EqualTo(2800).Within(0.001));
                Assert.That(timingMap.TimeAtRow(20), Is.EqualTo(3000).Within(0.001));
                Assert.That(timingMap.TimeAtRow(29), Is.EqualTo(3900).Within(0.001));
                Assert.That(timingMap.TimeAtRow(30), Is.EqualTo(4000).Within(0.001));
                Assert.That(timingMap.TimeAtRow(39), Is.EqualTo(4450).Within(0.001));
                Assert.That(timingMap.TimeAtRow(40), Is.EqualTo(4500).Within(0.001));
                // Rows past the last segment extrapolate with its step.
                Assert.That(timingMap.TimeAtRow(45), Is.EqualTo(4625).Within(0.001));
                // Negative rows clamp to the first row.
                Assert.That(timingMap.TimeAtRow(-5), Is.EqualTo(0).Within(0.001));
            });
        }

        [Test]
        public void BeatRowsResetAtEverySegmentBoundary()
        {
            var timingMap = new BeatTimingMap(
            [
                new YokkoTimingPoint(0, 400),
                new YokkoTimingPoint(1000, 800),
                new YokkoTimingPoint(3000, 400),
                new YokkoTimingPoint(4000, 200),
                new YokkoTimingPoint(4500, 100),
            ]);

            Assert.Multiple(() =>
            {
                foreach (int startRow in new[] { 0, 10, 20, 30, 40 })
                {
                    Assert.That(
                        timingMap.IsBeatRow(startRow),
                        Is.True,
                        $"row {startRow}");
                    Assert.That(
                        timingMap.IsBeatRow(startRow + 2),
                        Is.False,
                        $"row {startRow + 2}");
                    Assert.That(
                        timingMap.IsBeatRow(startRow + 4),
                        Is.True,
                        $"row {startRow + 4}");
                }
            });
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
