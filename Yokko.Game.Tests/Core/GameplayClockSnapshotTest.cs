using NUnit.Framework;
using Yokko.Core.Timing;

namespace Yokko.Game.Tests.Core
{
    [TestFixture]
    public sealed class GameplayClockSnapshotTest
    {
        [TestCase(10, 1010)]
        [TestCase(-10, 990)]
        public void UserOffsetIsAppliedToPresentedAudioTime(
            double userOffsetMilliseconds,
            double expectedJudgementTime)
        {
            var snapshot = new GameplayClockSnapshot(
                1000,
                24,
                userOffsetMilliseconds,
                true);

            Assert.That(
                snapshot.JudgementTimeMilliseconds,
                Is.EqualTo(expectedJudgementTime));
        }

        [Test]
        public void DeviceLatencyIsNotSubtractedFromPresentedAudioTime()
        {
            var lowLatency = new GameplayClockSnapshot(
                1000,
                2,
                7,
                true);
            var highLatency = lowLatency with
            {
                DeviceLatencyMilliseconds = 80,
            };

            Assert.That(
                highLatency.JudgementTimeMilliseconds,
                Is.EqualTo(lowLatency.JudgementTimeMilliseconds));
        }
    }
}
