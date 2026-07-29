using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using Yokko.Core.Beatmaps;
using Yokko.Core.Gameplay;
using Yokko.Core.Scoring;
using Yokko.Core.Timing;

namespace Yokko.Game.Tests.Core;

/// <summary>
/// Behavioural golden cases mirrored from ppy/osu's
/// osu.Game.Rulesets.Mania.Tests/TestSceneHoldNoteInput.cs at
/// 9f227ed28b6c8ba46dfea1f000f778d8b2827ad0 (MIT).
/// </summary>
[TestFixture]
public sealed class LazerManiaJudgementParityTest
{
    private const double timeBeforeHead = 250;
    private const double timeHead = 1500;
    private const double timeDuringHold = 2500;
    private const double timeTail = 4000;
    private const double timeAfterTail = 5250;

    private static IEnumerable<TestCaseData> LazerRankCases
    {
        get
        {
            yield return rankCase(
                ScoreRank.X,
                1,
                JudgementRating.Perfect);
            yield return rankCase(
                ScoreRank.X,
                0.99,
                JudgementRating.Great);
            yield return rankCase(
                ScoreRank.D,
                0.1,
                JudgementRating.Great);
            yield return rankCase(
                ScoreRank.X,
                0.99,
                JudgementRating.Perfect,
                JudgementRating.Great);
            yield return rankCase(
                ScoreRank.X,
                0.99,
                JudgementRating.Great,
                JudgementRating.Great);
            yield return rankCase(
                ScoreRank.S,
                0.99,
                JudgementRating.Perfect,
                JudgementRating.Good);
            yield return rankCase(
                ScoreRank.S,
                0.99,
                JudgementRating.Perfect,
                JudgementRating.Ok);
            yield return rankCase(
                ScoreRank.S,
                0.99,
                JudgementRating.Perfect,
                JudgementRating.Meh);
            yield return rankCase(
                ScoreRank.S,
                0.99,
                JudgementRating.Perfect,
                JudgementRating.Miss);
            yield return rankCase(
                ScoreRank.S,
                0.99,
                JudgementRating.Great,
                JudgementRating.Good);
            yield return rankCase(
                ScoreRank.S,
                0.99,
                JudgementRating.Great,
                JudgementRating.Ok);
            yield return rankCase(
                ScoreRank.S,
                0.99,
                JudgementRating.Great,
                JudgementRating.Meh);
            yield return rankCase(
                ScoreRank.S,
                0.99,
                JudgementRating.Great,
                JudgementRating.Miss);
        }
    }

    [TestCaseSource(nameof(LazerRankCases))]
    public void RankMatrixMatchesLazer(
        ScoreRank expected,
        double accuracy,
        JudgementRating[] results)
    {
        var counts = new JudgementCounter();
        foreach (JudgementRating result in results)
            counts.Add(result);

        Assert.That(
            ManiaScoreProcessor.RankFromScore(accuracy, counts),
            Is.EqualTo(expected));
    }

    [Test]
    public void NoInputMatchesLazer()
    {
        IReadOnlyList<JudgementEvent> events = play(
            new InputEdge(timeBeforeHead, true),
            new InputEdge(timeAfterTail, false));

        assertHoldResults(
            events,
            JudgementRating.Miss,
            JudgementRating.Miss,
            JudgementRating.IgnoreMiss,
            JudgementRating.ComboBreak);
    }

    [Test]
    public void CorrectInputMatchesLazer()
    {
        IReadOnlyList<JudgementEvent> events = play(
            new InputEdge(timeHead, true),
            new InputEdge(timeTail, false));

        assertHoldResults(
            events,
            JudgementRating.Perfect,
            JudgementRating.Perfect,
            JudgementRating.IgnoreHit,
            JudgementRating.IgnoreHit);
    }

    [Test]
    public void TooEarlyPressCanBeRetriedAtTheHeadLikeLazer()
    {
        IReadOnlyList<JudgementEvent> events = play(
            new InputEdge(timeBeforeHead, true),
            new InputEdge(timeBeforeHead + 10, false),
            new InputEdge(timeHead, true),
            new InputEdge(timeTail, false));

        assertHoldResults(
            events,
            JudgementRating.Perfect,
            JudgementRating.Perfect,
            JudgementRating.IgnoreHit,
            JudgementRating.IgnoreHit);
    }

    [Test]
    public void MissedHeadRegrabCapsTailAtMehLikeLazer()
    {
        IReadOnlyList<JudgementEvent> events = play(
            new InputEdge(timeDuringHold, true),
            new InputEdge(timeTail, false));

        assertHoldResults(
            events,
            JudgementRating.Miss,
            JudgementRating.Meh,
            JudgementRating.IgnoreHit,
            JudgementRating.IgnoreHit);
    }

    [Test]
    public void BrokenHoldRegrabCapsTailAtMehLikeLazer()
    {
        IReadOnlyList<JudgementEvent> events = play(
            new InputEdge(timeHead, true),
            new InputEdge(timeHead + 10, false),
            new InputEdge(timeDuringHold, true),
            new InputEdge(timeTail, false));

        assertHoldResults(
            events,
            JudgementRating.Perfect,
            JudgementRating.Meh,
            JudgementRating.IgnoreHit,
            JudgementRating.ComboBreak);
    }

    [Test]
    public void LateReleaseMissesTailLikeLazer()
    {
        IReadOnlyList<JudgementEvent> events = play(
            new InputEdge(timeHead, true),
            new InputEdge(timeAfterTail, false));

        assertHoldResults(
            events,
            JudgementRating.Perfect,
            JudgementRating.Miss,
            JudgementRating.IgnoreMiss,
            JudgementRating.ComboBreak);
    }

    [Test]
    public void CloseHoldHeadDoesNotStealNearbyNoteLikeLazer()
    {
        YokkoBeatmap beatmap = createBeatmap(
            new YokkoHitObject(
                0,
                timeHead,
                timeHead + 50,
                HitObjectKind.Hold),
            new YokkoHitObject(
                0,
                timeHead + 60,
                null,
                HitObjectKind.Tap));

        IReadOnlyList<JudgementEvent> events = play(
            beatmap,
            new InputEdge(timeHead + 50, true),
            new InputEdge(timeHead + 60, false));

        Assert.Multiple(() =>
        {
            Assert.That(
                events.Single(result =>
                    result.Phase == JudgementPhase.HoldHead).Rating,
                Is.EqualTo(JudgementRating.Good));
            Assert.That(
                events.Single(result =>
                    result.Phase == JudgementPhase.HoldTail).Rating,
                Is.EqualTo(JudgementRating.Perfect));
            Assert.That(
                events.Single(result =>
                    result.Phase == JudgementPhase.Tap).Rating,
                Is.EqualTo(JudgementRating.Miss));
        });
    }

    [Test]
    public void PressBeforeTailHitsNearbyNoteAndMissesHoldLikeLazer()
    {
        YokkoBeatmap beatmap = createBeatmap(
            new YokkoHitObject(
                0,
                timeHead,
                timeTail,
                HitObjectKind.Hold),
            new YokkoHitObject(
                0,
                timeTail + 50,
                null,
                HitObjectKind.Tap));

        IReadOnlyList<JudgementEvent> events = play(
            beatmap,
            new InputEdge(timeTail - 10, true),
            new InputEdge(timeTail, false));

        Assert.Multiple(() =>
        {
            Assert.That(
                events.Single(result =>
                    result.Phase == JudgementPhase.HoldHead).Rating,
                Is.EqualTo(JudgementRating.Miss));
            Assert.That(
                events.Single(result =>
                    result.Phase == JudgementPhase.HoldTail).Rating,
                Is.EqualTo(JudgementRating.Miss));
            Assert.That(
                events.Single(result =>
                    result.Phase == JudgementPhase.Tap).Rating,
                Is.EqualTo(JudgementRating.Good));
        });
    }

    [Test]
    public void PressAfterTailHitsNearbyNoteAndMissesHoldLikeLazer()
    {
        YokkoBeatmap beatmap = createBeatmap(
            new YokkoHitObject(
                0,
                timeHead,
                timeTail,
                HitObjectKind.Hold),
            new YokkoHitObject(
                0,
                timeTail + 50,
                null,
                HitObjectKind.Tap));

        IReadOnlyList<JudgementEvent> events = play(
            beatmap,
            new InputEdge(timeTail + 10, true),
            new InputEdge(timeTail + 20, false));

        Assert.Multiple(() =>
        {
            Assert.That(
                events.Single(result =>
                    result.Phase == JudgementPhase.HoldHead).Rating,
                Is.EqualTo(JudgementRating.Miss));
            Assert.That(
                events.Single(result =>
                    result.Phase == JudgementPhase.HoldTail).Rating,
                Is.EqualTo(JudgementRating.Miss));
            Assert.That(
                events.Single(result =>
                    result.Phase == JudgementPhase.Tap).Rating,
                Is.EqualTo(JudgementRating.Great));
        });
    }

    [Test]
    public void SimultaneousHoldAndNoteReachMaximumScoreLikeLazer()
    {
        YokkoBeatmap beatmap = createBeatmap(
            new YokkoHitObject(
                0,
                1000,
                3000,
                HitObjectKind.Hold),
            new YokkoHitObject(
                1,
                2000,
                null,
                HitObjectKind.Tap));
        var state = new BeatmapJudgementState(beatmap);

        state.JudgeLanePress(0, 1000);
        state.JudgeLanePress(1, 2000);
        state.JudgeLaneRelease(1, 2001);
        state.JudgeLaneRelease(0, 3000);

        Assert.Multiple(() =>
        {
            Assert.That(state.Counts.Perfect, Is.EqualTo(3));
            Assert.That(state.Counts.Miss, Is.Zero);
            Assert.That(state.Score, Is.EqualTo(1_000_000));
        });
    }

    [Test]
    public void SimultaneousLongNotesReachMaximumScoreLikeLazer()
    {
        YokkoBeatmap beatmap = createBeatmap(
            new YokkoHitObject(
                0,
                1000,
                3000,
                HitObjectKind.Hold),
            new YokkoHitObject(
                1,
                2000,
                4000,
                HitObjectKind.Hold));
        var state = new BeatmapJudgementState(beatmap);

        state.JudgeLanePress(0, 1000);
        state.JudgeLanePress(1, 2000);
        state.JudgeLaneRelease(0, 3000);
        state.JudgeLaneRelease(1, 4000);

        Assert.Multiple(() =>
        {
            Assert.That(state.Counts.Perfect, Is.EqualTo(4));
            Assert.That(state.Counts.Miss, Is.Zero);
            Assert.That(state.Score, Is.EqualTo(1_000_000));
        });
    }

    [Test]
    public void SameLaneStackOnlyHitsMostRecentObjectLikeLazer()
    {
        YokkoBeatmap beatmap = createBeatmap(
            new YokkoHitObject(
                0,
                1000,
                null,
                HitObjectKind.Tap),
            new YokkoHitObject(
                0,
                1000,
                null,
                HitObjectKind.Tap));
        var state = new BeatmapJudgementState(beatmap);

        JudgementEvent hit = state.JudgeLanePress(0, 1000).Single();
        IReadOnlyList<JudgementEvent> misses =
            state.CollectExpiredMisses(
                1000 + state.Windows.MehMilliseconds + 1);

        Assert.Multiple(() =>
        {
            Assert.That(hit.HitObjectIndex, Is.EqualTo(1));
            Assert.That(hit.Rating, Is.EqualTo(JudgementRating.Perfect));
            Assert.That(misses, Has.Count.EqualTo(1));
            Assert.That(misses[0].HitObjectIndex, Is.Zero);
            Assert.That(misses[0].Rating, Is.EqualTo(JudgementRating.Miss));
            Assert.That(state.Combo, Is.Zero);
        });
    }

    [Test]
    public void OverlappingSameLaneHoldsRespectLazerNoteLock()
    {
        YokkoBeatmap beatmap = createBeatmap(
            new YokkoHitObject(
                0,
                1000,
                3000,
                HitObjectKind.Hold),
            new YokkoHitObject(
                0,
                2000,
                4000,
                HitObjectKind.Hold));
        var state = new BeatmapJudgementState(beatmap);
        var events = new List<JudgementEvent>();

        events.AddRange(state.JudgeLanePress(0, 1000));
        events.AddRange(state.JudgeLaneRelease(0, 1500));
        events.AddRange(state.JudgeLanePress(0, 2000));

        Assert.Multiple(() =>
        {
            Assert.That(state.IsHoldActive(0), Is.False);
            Assert.That(state.IsHoldActive(1), Is.True);
        });

        events.AddRange(state.JudgeLaneRelease(0, 3000));
        state.CollectExpiredMisses(3300, events);
        events.AddRange(state.JudgeLanePress(0, 3990));
        events.AddRange(state.JudgeLaneRelease(0, 4000));

        Assert.Multiple(() =>
        {
            Assert.That(
                events.Single(result =>
                    result.HitObjectIndex == 0
                    && result.Phase == JudgementPhase.HoldTail).Rating,
                Is.EqualTo(JudgementRating.Miss));
            Assert.That(
                events.Single(result =>
                    result.HitObjectIndex == 1
                    && result.Phase == JudgementPhase.HoldHead).Rating,
                Is.EqualTo(JudgementRating.Perfect));
            Assert.That(
                events.Single(result =>
                    result.HitObjectIndex == 1
                    && result.Phase == JudgementPhase.HoldTail).Rating,
                Is.EqualTo(JudgementRating.Meh));
        });
    }

    private static IReadOnlyList<JudgementEvent> play(
        params InputEdge[] inputs)
        => play(createHoldBeatmap(), inputs);

    private static IReadOnlyList<JudgementEvent> play(
        YokkoBeatmap beatmap,
        params InputEdge[] inputs)
    {
        var state = new BeatmapJudgementState(beatmap);
        var events = new List<JudgementEvent>();

        foreach (InputEdge input in inputs)
        {
            state.CollectExpiredMisses(input.TimeMilliseconds, events);
            events.AddRange(input.IsPressed
                ? state.JudgeLanePress(0, input.TimeMilliseconds)
                : state.JudgeLaneRelease(0, input.TimeMilliseconds));
        }

        state.CollectExpiredMisses(
            timeAfterTail + state.Windows.MissMilliseconds + 1,
            events);
        return events;
    }

    private static void assertHoldResults(
        IReadOnlyList<JudgementEvent> events,
        JudgementRating expectedHead,
        JudgementRating expectedTail,
        JudgementRating expectedParent,
        JudgementRating expectedBody)
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                events.Single(result =>
                    result.Phase == JudgementPhase.HoldHead).Rating,
                Is.EqualTo(expectedHead));
            Assert.That(
                events.Single(result =>
                    result.Phase == JudgementPhase.HoldTail).Rating,
                Is.EqualTo(expectedTail));
            Assert.That(
                events.Single(result =>
                    result.Phase == JudgementPhase.Hold).Rating,
                Is.EqualTo(expectedParent));
            Assert.That(
                events.Single(result =>
                    result.Phase == JudgementPhase.HoldBody).Rating,
                Is.EqualTo(expectedBody));
        });
    }

    private static YokkoBeatmap createHoldBeatmap() => createBeatmap(
        new YokkoHitObject(
            0,
            timeHead,
            timeTail,
            HitObjectKind.Hold));

    private static YokkoBeatmap createBeatmap(
        params YokkoHitObject[] hitObjects) => new(
        "lazer hold parity",
        "ppy",
        "Yokko",
        "4K",
        KeyMode.FourKey,
        ChartSourceFormat.OsuMania,
        [YokkoTimingPoint.Default],
        null,
        hitObjects);

    private static TestCaseData rankCase(
        ScoreRank expected,
        double accuracy,
        params JudgementRating[] results)
        => new(expected, accuracy, results);

    private readonly record struct InputEdge(
        double TimeMilliseconds,
        bool IsPressed);
}
