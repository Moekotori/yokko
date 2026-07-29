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

    private readonly record struct InputEdge(
        double TimeMilliseconds,
        bool IsPressed);
}
