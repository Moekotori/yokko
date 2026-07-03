using NUnit.Framework;
using Yokko.Core.Beatmaps;
using Yokko.Core.Scoring;

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
    }
}
