using System;
using NUnit.Framework;
using Yokko.Core.Beatmaps;
using Yokko.Core.Gameplay;
using Yokko.Core.Mods;
using Yokko.Core.Scoring;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class ManiaAdaptiveSpeedStateTest
{
    [Test]
    public void FirstResultIsIgnoredAndEarlyHitRaisesSmoothedRate()
    {
        var state = new ManiaAdaptiveSpeedState(createBeatmap());

        Assert.That(state.Apply(judgement(0, 0, -50)), Is.False);
        Assert.That(state.Apply(judgement(1, 1000, 900)), Is.True);
        Assert.That(
            state.TargetRate,
            Is.EqualTo(1 + (1.11 / 8 - 0.125) / 7)
                .Within(0.0000001));

        state.Update(50);

        Assert.That(
            state.CurrentRate,
            Is.EqualTo((1 + state.TargetRate) / 2)
                .Within(0.0000001));
    }

    [Test]
    public void MissUsesLazerSlowdownMultiplier()
    {
        var state = new ManiaAdaptiveSpeedState(createBeatmap());

        Assert.That(
            state.Apply(new JudgementEvent(
                1,
                0,
                1000,
                null,
                0,
                JudgementRating.Miss)),
            Is.True);
        Assert.That(
            state.RecentRates[^1],
            Is.EqualTo(0.95).Within(0.0000001));
        Assert.That(state.TargetRate, Is.LessThan(1));
    }

    [Test]
    public void InitialRateAndModIdentityAreCanonical()
    {
        ManiaModSet mods = ManiaModSet.Empty
            .WithAdaptiveSpeed(1.25, false);

        Assert.Multiple(() =>
        {
            Assert.That(mods.HasDynamicRate, Is.True);
            Assert.That(mods.PlaybackRate, Is.EqualTo(1.25));
            Assert.That(mods.ChangesAudioPitch, Is.False);
            Assert.That(
                mods.Fingerprint,
                Is.EqualTo(
                    "adaptive-speed:initial=1.25:tempo"));
            Assert.That(
                mods.With(ManiaModId.Autoplay, true)
                    .HasAdaptiveSpeed,
                Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                ManiaModSet.Empty.WithAdaptiveSpeed(2.01, true));
        });
    }

    private static JudgementEvent judgement(
        int index,
        double objectTime,
        double hitTime) =>
        new(
            index,
            0,
            objectTime,
            hitTime,
            hitTime - objectTime,
            JudgementRating.Perfect);

    private static YokkoBeatmap createBeatmap() =>
        new(
            "AS",
            "Yokko",
            "Tests",
            "Adaptive",
            KeyMode.FourKey,
            ChartSourceFormat.Yokko,
            [],
            null,
            [
                new YokkoHitObject(
                    0,
                    0,
                    null,
                    HitObjectKind.Tap),
                new YokkoHitObject(
                    0,
                    1000,
                    null,
                    HitObjectKind.Tap),
                new YokkoHitObject(
                    0,
                    2000,
                    null,
                    HitObjectKind.Tap),
            ]);
}
