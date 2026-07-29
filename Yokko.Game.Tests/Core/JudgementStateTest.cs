using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using Yokko.Core.Beatmaps;
using Yokko.Core.Gameplay;
using Yokko.Core.Scoring;
using Yokko.Core.Timing;

namespace Yokko.Game.Tests.Core
{
    [TestFixture]
    public sealed class JudgementStateTest
    {
        [TestCase(0, 22.5, 64.5, 97.5, 127.5, 151.5, 188.5)]
        [TestCase(5, 19.5, 49.5, 82.5, 112.5, 136.5, 173.5)]
        [TestCase(8, 16.5, 40.5, 73.5, 103.5, 127.5, 164.5)]
        [TestCase(10, 13.5, 34.5, 67.5, 97.5, 121.5, 158.5)]
        public void LazerWindowsFollowOverallDifficulty(
            double overallDifficulty,
            double perfect,
            double great,
            double good,
            double ok,
            double meh,
            double miss)
        {
            var windows = new JudgementWindows(overallDifficulty);

            Assert.Multiple(() =>
            {
                Assert.That(windows.PerfectMilliseconds, Is.EqualTo(perfect));
                Assert.That(windows.GreatMilliseconds, Is.EqualTo(great));
                Assert.That(windows.GoodMilliseconds, Is.EqualTo(good));
                Assert.That(windows.OkMilliseconds, Is.EqualTo(ok));
                Assert.That(windows.MehMilliseconds, Is.EqualTo(meh));
                Assert.That(windows.MissMilliseconds, Is.EqualTo(miss));
            });
        }

        [Test]
        public void EveryLazerWindowBoundaryIsInclusive()
        {
            var windows = new JudgementWindows(8);

            Assert.Multiple(() =>
            {
                Assert.That(
                    windows.Judge(windows.PerfectMilliseconds),
                    Is.EqualTo(JudgementRating.Perfect));
                Assert.That(
                    windows.Judge(windows.GreatMilliseconds),
                    Is.EqualTo(JudgementRating.Great));
                Assert.That(
                    windows.Judge(windows.GoodMilliseconds),
                    Is.EqualTo(JudgementRating.Good));
                Assert.That(
                    windows.Judge(windows.OkMilliseconds),
                    Is.EqualTo(JudgementRating.Ok));
                Assert.That(
                    windows.Judge(windows.MehMilliseconds),
                    Is.EqualTo(JudgementRating.Meh));
                Assert.That(
                    windows.Judge(windows.MissMilliseconds),
                    Is.EqualTo(JudgementRating.Miss));
                Assert.That(
                    windows.Judge(windows.MissMilliseconds + 0.01),
                    Is.EqualTo(JudgementRating.None));
            });
        }

        [Test]
        public void DoubleTimeKeepsRealWorldHitWindowDuration()
        {
            var windows = new JudgementWindows(
                overallDifficulty: 5,
                speedMultiplier: 1.5);

            Assert.That(
                windows.PerfectMilliseconds / 1.5,
                Is.EqualTo(
                    JudgementWindows.DefaultMania.PerfectMilliseconds)
                  .Within(0.34));
        }

        [Test]
        public void PerfectHitResolvesObject()
        {
            YokkoBeatmap beatmap = DemoBeatmaps.CreateFourKeyDemo();
            var state = new BeatmapJudgementState(beatmap);

            JudgementEvent judgement = state.TryJudgeLanePress(0, 1600);

            Assert.That(judgement, Is.Not.Null);
            Assert.That(judgement.Rating, Is.EqualTo(JudgementRating.Perfect));
            Assert.That(state.Combo, Is.EqualTo(1));
            Assert.That(state.Counts.Perfect, Is.EqualTo(1));
        }

        [Test]
        public void ActiveInputCanReceiveMissInsideOuterWindow()
        {
            var state = new BeatmapJudgementState(createTapBeatmap(1000));

            JudgementEvent judgement = state.TryJudgeLanePress(0, 1150);

            Assert.That(judgement.Rating, Is.EqualTo(JudgementRating.Miss));
            Assert.That(state.IsComplete, Is.True);
        }

        [Test]
        public void NaturalMissOccursAfterMehWindow()
        {
            var state = new BeatmapJudgementState(createTapBeatmap(1000));

            Assert.That(
                state.CollectExpiredMisses(
                    1000 + state.Windows.MehMilliseconds),
                Is.Empty);

            IReadOnlyList<JudgementEvent> misses =
                state.CollectExpiredMisses(
                    1000 + state.Windows.MehMilliseconds + 0.01);

            Assert.That(misses, Has.Count.EqualTo(1));
            Assert.That(misses[0].Rating, Is.EqualTo(JudgementRating.Miss));
        }

        [Test]
        public void NaturalMissesCanReuseCallerOwnedBuffer()
        {
            var state = new BeatmapJudgementState(
                createTapBeatmap(1000, 1500));
            var misses = new List<JudgementEvent>();

            state.CollectExpiredMisses(1300, misses);
            Assert.That(misses, Has.Count.EqualTo(1));

            misses.Clear();
            state.CollectExpiredMisses(1800, misses);

            Assert.That(misses, Has.Count.EqualTo(1));
            Assert.That(state.ResolvedObjectCount, Is.EqualTo(2));
            Assert.That(state.IsComplete, Is.True);
        }

        [Test]
        public void LaterSuccessfulNoteForcesEarlierNoteToMiss()
        {
            var state = new BeatmapJudgementState(createTapBeatmap(1000, 1100));

            IReadOnlyList<JudgementEvent> events =
                state.JudgeLanePress(0, 1110);

            Assert.Multiple(() =>
            {
                Assert.That(events, Has.Count.EqualTo(2));
                Assert.That(events[0].HitObjectIndex, Is.EqualTo(1));
                Assert.That(events[0].Rating, Is.EqualTo(JudgementRating.Perfect));
                Assert.That(events[1].HitObjectIndex, Is.EqualTo(0));
                Assert.That(events[1].Rating, Is.EqualTo(JudgementRating.Miss));
                Assert.That(state.Combo, Is.Zero);
            });
        }

        [Test]
        public void HoldHeadTailAndBodyMatchLazer()
        {
            var state = new BeatmapJudgementState(createHoldBeatmap());

            IReadOnlyList<JudgementEvent> headEvents =
                state.JudgeLanePress(1, 1000);
            IReadOnlyList<JudgementEvent> tailEvents =
                state.JudgeLaneRelease(1, 1500);

            Assert.Multiple(() =>
            {
                Assert.That(headEvents.Single().Phase, Is.EqualTo(JudgementPhase.HoldHead));
                Assert.That(tailEvents.Select(e => e.Phase), Is.EqualTo(new[]
                {
                    JudgementPhase.HoldTail,
                    JudgementPhase.HoldBody,
                    JudgementPhase.Hold,
                }));
                Assert.That(tailEvents[0].Rating, Is.EqualTo(JudgementRating.Perfect));
                Assert.That(tailEvents[1].Rating, Is.EqualTo(JudgementRating.IgnoreHit));
                Assert.That(tailEvents[2].Rating, Is.EqualTo(JudgementRating.IgnoreHit));
                Assert.That(state.IsResolved(0), Is.True);
                Assert.That(state.Counts.Perfect, Is.EqualTo(2));
                Assert.That(state.Combo, Is.EqualTo(2));
                Assert.That(state.Score, Is.EqualTo(1_000_000));
            });
        }

        [Test]
        public void NoReleaseAutomaticallyPerfectsAHeldTail()
        {
            var state = new BeatmapJudgementState(
                createHoldBeatmap(),
                noRelease: true);
            state.JudgeLanePress(1, 1000);

            IReadOnlyList<JudgementEvent> events =
                state.CollectExpiredMisses(1500);

            Assert.Multiple(() =>
            {
                Assert.That(
                    events.Select(static judgement => judgement.Phase),
                    Is.EqualTo(new[]
                    {
                        JudgementPhase.HoldTail,
                        JudgementPhase.HoldBody,
                        JudgementPhase.Hold,
                    }));
                Assert.That(events[0].Rating, Is.EqualTo(JudgementRating.Perfect));
                Assert.That(state.Counts.Perfect, Is.EqualTo(2));
                Assert.That(state.IsComplete, Is.True);
                Assert.That(state.Score, Is.EqualTo(1_000_000));
            });
        }

        [Test]
        public void DroppedHoldCanBeRegrabbedAndTailIsCappedAtMeh()
        {
            var state = new BeatmapJudgementState(createHoldBeatmap());
            state.JudgeLanePress(1, 1000);

            IReadOnlyList<JudgementEvent> drop =
                state.JudgeLaneRelease(1, 1250);
            IReadOnlyList<JudgementEvent> regrab =
                state.JudgeLanePress(1, 1400);
            IReadOnlyList<JudgementEvent> tail =
                state.JudgeLaneRelease(1, 1500);

            Assert.Multiple(() =>
            {
                Assert.That(drop, Has.Count.EqualTo(1));
                Assert.That(drop[0].Phase, Is.EqualTo(JudgementPhase.HoldBody));
                Assert.That(drop[0].Rating, Is.EqualTo(JudgementRating.ComboBreak));
                Assert.That(regrab, Is.Empty);
                Assert.That(tail, Has.Count.EqualTo(2));
                Assert.That(tail[0].Phase, Is.EqualTo(JudgementPhase.HoldTail));
                Assert.That(tail[0].Rating, Is.EqualTo(JudgementRating.Meh));
                Assert.That(tail[1].Phase, Is.EqualTo(JudgementPhase.Hold));
                Assert.That(tail[1].Rating, Is.EqualTo(JudgementRating.IgnoreHit));
                Assert.That(state.Combo, Is.EqualTo(1));
                Assert.That(state.IsComplete, Is.True);
            });
        }

        [Test]
        public void HoldReleaseUsesOnePointFiveTimesWindow()
        {
            var state = new BeatmapJudgementState(createHoldBeatmap());
            state.JudgeLanePress(1, 1000);

            double releaseTime =
                1500 + state.Windows.GreatMilliseconds * 1.5;
            JudgementEvent tail =
                state.JudgeLaneRelease(1, releaseTime)
                     .First();

            Assert.That(tail.Rating, Is.EqualTo(JudgementRating.Great));
            Assert.That(
                tail.HitErrorMilliseconds,
                Is.EqualTo(state.Windows.GreatMilliseconds * 1.5));
        }

        [Test]
        public void NearbyNoteCanForceEarlierHoldToMissLikeOrderedHitPolicy()
        {
            var beatmap = new YokkoBeatmap(
                "Nearby note",
                "Yokko",
                "Yokko",
                "4K",
                KeyMode.FourKey,
                ChartSourceFormat.Yokko,
                [YokkoTimingPoint.Default],
                null,
                [
                    new YokkoHitObject(
                        0,
                        1500,
                        4000,
                        HitObjectKind.Hold),
                    new YokkoHitObject(
                        0,
                        4050,
                        null,
                        HitObjectKind.Tap),
                ],
                5);
            var state = new BeatmapJudgementState(beatmap);

            state.CollectExpiredMisses(3990);
            IReadOnlyList<JudgementEvent> events =
                state.JudgeLanePress(0, 3990);

            Assert.Multiple(() =>
            {
                Assert.That(events[0].HitObjectIndex, Is.EqualTo(1));
                Assert.That(events[0].Rating, Is.EqualTo(JudgementRating.Good));
                Assert.That(
                    events.Any(e => e.HitObjectIndex == 0
                                    && e.Phase == JudgementPhase.HoldTail
                                    && e.Rating == JudgementRating.Miss),
                    Is.True);
                Assert.That(state.IsHoldActive(0), Is.False);
                Assert.That(state.Combo, Is.Zero);
            });
        }

        [Test]
        public void ZeroLengthHoldCanHitHeadAndTail()
        {
            var beatmap = createHoldBeatmap(1000, 1000);
            var state = new BeatmapJudgementState(beatmap);

            state.JudgeLanePress(1, 1000);
            state.JudgeLaneRelease(1, 1001);

            Assert.Multiple(() =>
            {
                Assert.That(state.Counts.Perfect, Is.EqualTo(2));
                Assert.That(state.IsComplete, Is.True);
                Assert.That(state.Score, Is.EqualTo(1_000_000));
            });
        }

        [Test]
        public void UnplayedHoldProducesHeadTailAndBodyMisses()
        {
            var state = new BeatmapJudgementState(createHoldBeatmap());

            IReadOnlyList<JudgementEvent> events =
                state.CollectExpiredMisses(
                    1500 + state.Windows.MehMilliseconds * 1.5 + 1);

            Assert.Multiple(() =>
            {
                Assert.That(events.Select(e => e.Phase), Is.EqualTo(new[]
                {
                    JudgementPhase.HoldHead,
                    JudgementPhase.HoldTail,
                    JudgementPhase.Hold,
                    JudgementPhase.HoldBody,
                }));
                Assert.That(state.Counts.Miss, Is.EqualTo(2));
                Assert.That(state.Counts.ComboBreak, Is.EqualTo(1));
                Assert.That(state.IsComplete, Is.True);
            });
        }

        [Test]
        public void AllGreatsReceiveLazerSsRank()
        {
            var state = new BeatmapJudgementState(createTapBeatmap(1000, 1500));
            double greatOffset = state.Windows.PerfectMilliseconds + 1;

            state.JudgeLanePress(0, 1000 + greatOffset);
            state.JudgeLaneRelease(0, 1200);
            state.JudgeLanePress(0, 1500 + greatOffset);

            Assert.Multiple(() =>
            {
                Assert.That(state.Counts.Great, Is.EqualTo(2));
                Assert.That(state.Accuracy, Is.EqualTo(600d / 610).Within(1e-12));
                Assert.That(state.Rank, Is.EqualTo(ScoreRank.X));
                Assert.That(state.Score, Is.LessThan(1_000_000));
            });
        }

        [Test]
        public void MixedResultsUseLazerAccuracyWeightsAndComboCurve()
        {
            YokkoBeatmap beatmap =
                createTapBeatmap(1000, 1500, 2000, 2500, 3000);
            var processor = new ManiaScoreProcessor(beatmap);
            JudgementRating[] results =
            [
                JudgementRating.Perfect,
                JudgementRating.Great,
                JudgementRating.Good,
                JudgementRating.Ok,
                JudgementRating.Meh,
            ];

            foreach (JudgementRating result in results)
                processor.Apply(result);

            double expectedAccuracy = 955d / 1525;
            double maximumComboPortion = Enumerable.Range(1, 5)
                .Sum(combo => 300 * comboMultiplier(combo));
            double currentComboPortion = results
                .Select((result, index) =>
                    comboBaseScore(result)
                    * comboMultiplier(index + 1))
                .Sum();
            long expectedScore = (long)Math.Round(
                150_000 * currentComboPortion / maximumComboPortion
                + 850_000 * Math.Pow(
                    expectedAccuracy,
                    2 + 2 * expectedAccuracy));

            Assert.Multiple(() =>
            {
                Assert.That(
                    processor.Accuracy,
                    Is.EqualTo(expectedAccuracy).Within(1e-12));
                Assert.That(processor.TotalScore, Is.EqualTo(expectedScore));
                Assert.That(processor.Combo, Is.EqualTo(5));
                Assert.That(processor.Rank, Is.EqualTo(ScoreRank.D));
            });
        }

        [Test]
        public void InvalidHoldTimesAreRejectedAtTheDomainBoundary()
        {
            Assert.Throws<ArgumentException>(() =>
                new YokkoHitObject(
                    0,
                    1000,
                    null,
                    HitObjectKind.Hold));
            Assert.Throws<ArgumentException>(() =>
                new YokkoHitObject(
                    0,
                    1000,
                    999,
                    HitObjectKind.Hold));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new YokkoHitObject(
                    0,
                    double.NaN,
                    null,
                    HitObjectKind.Tap));
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(-0.01)]
        [TestCase(10.01)]
        public void InvalidOverallDifficultyIsRejected(double overallDifficulty)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                createBeatmap(
                    new YokkoHitObject(
                        0,
                        1000,
                        null,
                        HitObjectKind.Tap),
                    overallDifficulty));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new JudgementWindows(overallDifficulty));
        }

        private static YokkoBeatmap createTapBeatmap(params double[] times)
            => new(
                "Tap test",
                "Yokko",
                "Yokko",
                "4K",
                KeyMode.FourKey,
                ChartSourceFormat.Yokko,
                [YokkoTimingPoint.Default],
                null,
                times.Select(time =>
                    new YokkoHitObject(
                        0,
                        time,
                        null,
                        HitObjectKind.Tap)).ToArray(),
                8);

        private static YokkoBeatmap createBeatmap(
            YokkoHitObject hitObject,
            double overallDifficulty)
            => new(
                "Validation test",
                "Yokko",
                "Yokko",
                "4K",
                KeyMode.FourKey,
                ChartSourceFormat.Yokko,
                [YokkoTimingPoint.Default],
                null,
                [hitObject],
                overallDifficulty);

        private static YokkoBeatmap createHoldBeatmap(
            double startTime = 1000,
            double endTime = 1500)
            => new(
                "Hold test",
                "Yokko",
                "Yokko",
                "4K",
                KeyMode.FourKey,
                ChartSourceFormat.Yokko,
                [YokkoTimingPoint.Default],
                null,
                [new YokkoHitObject(
                    1,
                    startTime,
                    endTime,
                    HitObjectKind.Hold)],
                8);

        private static double comboMultiplier(int combo)
            => Math.Min(
                Math.Max(0.5, Math.Log(combo, 4)),
                Math.Log(400, 4));

        private static int comboBaseScore(JudgementRating rating)
            => rating switch
            {
                JudgementRating.Perfect => 300,
                _ => ManiaScoreProcessor.BaseScoreFor(rating),
            };
    }
}
