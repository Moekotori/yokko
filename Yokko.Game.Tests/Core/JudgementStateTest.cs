using NUnit.Framework;
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
        [Test]
        public void PerfectHitResolvesObject()
        {
            YokkoBeatmap beatmap = DemoBeatmaps.CreateFourKeyDemo();
            var state = new BeatmapJudgementState(beatmap, JudgementWindows.DefaultMania);

            JudgementEvent judgement = state.TryJudgeLanePress(0, 1600);

            Assert.That(judgement, Is.Not.Null);
            Assert.That(judgement.Rating, Is.EqualTo(JudgementRating.Perfect));
            Assert.That(state.Combo, Is.EqualTo(1));
            Assert.That(state.Counts.Perfect, Is.EqualTo(1));
        }

        [Test]
        public void ExpiredObjectBecomesMiss()
        {
            YokkoBeatmap beatmap = DemoBeatmaps.CreateFourKeyDemo();
            var state = new BeatmapJudgementState(beatmap, JudgementWindows.DefaultMania);

            var misses = state.CollectExpiredMisses(1600 + JudgementWindows.DefaultMania.BadMilliseconds + 1);

            Assert.That(misses, Has.Count.EqualTo(1));
            Assert.That(misses[0].Rating, Is.EqualTo(JudgementRating.Miss));
            Assert.That(state.Counts.Miss, Is.EqualTo(1));
        }

        [Test]
        public void HoldRequiresHeadAndTailJudgements()
        {
            var state = new BeatmapJudgementState(createHoldBeatmap(), JudgementWindows.DefaultMania);

            JudgementEvent head = state.TryJudgeLanePress(1, 1000);

            Assert.That(head.Phase, Is.EqualTo(JudgementPhase.HoldHead));
            Assert.That(state.IsHoldActive(0), Is.True);
            Assert.That(state.IsResolved(0), Is.False);
            Assert.That(state.IsComplete, Is.False);

            JudgementEvent tail = state.TryJudgeLaneRelease(1, 1500);

            Assert.That(tail.Phase, Is.EqualTo(JudgementPhase.HoldTail));
            Assert.That(tail.Rating, Is.EqualTo(JudgementRating.Perfect));
            Assert.That(state.IsResolved(0), Is.True);
            Assert.That(state.IsComplete, Is.True);
            Assert.That(state.Counts.Perfect, Is.EqualTo(2));
            Assert.That(state.MaxCombo, Is.EqualTo(2));
        }

        [Test]
        public void EarlyHoldReleaseBreaksCombo()
        {
            var state = new BeatmapJudgementState(createHoldBeatmap(), JudgementWindows.DefaultMania);
            state.TryJudgeLanePress(1, 1000);

            JudgementEvent tail = state.TryJudgeLaneRelease(1, 1250);

            Assert.That(tail.Phase, Is.EqualTo(JudgementPhase.HoldTail));
            Assert.That(tail.Rating, Is.EqualTo(JudgementRating.Miss));
            Assert.That(state.Combo, Is.Zero);
            Assert.That(state.IsResolved(0), Is.True);
        }

        [Test]
        public void UnplayedHoldMissesHeadAndTail()
        {
            var state = new BeatmapJudgementState(createHoldBeatmap(), JudgementWindows.DefaultMania);

            IReadOnlyList<JudgementEvent> misses = state.CollectExpiredMisses(1601);

            Assert.That(misses.Select(judgement => judgement.Phase), Is.EqualTo(new[]
            {
                JudgementPhase.HoldHead,
                JudgementPhase.HoldTail,
            }));
            Assert.That(state.Counts.Miss, Is.EqualTo(2));
            Assert.That(state.IsComplete, Is.True);
        }

        private static YokkoBeatmap createHoldBeatmap()
            => new(
                "Hold test",
                "Yokko",
                "Yokko",
                "4K",
                KeyMode.FourKey,
                ChartSourceFormat.Yokko,
                [YokkoTimingPoint.Default],
                null,
                [new YokkoHitObject(1, 1000, 1500, HitObjectKind.Hold)]);
    }
}
