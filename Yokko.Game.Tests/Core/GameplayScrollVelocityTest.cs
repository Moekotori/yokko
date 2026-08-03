using NUnit.Framework;
using Yokko.Core.Beatmaps;
using Yokko.Core.Gameplay;
using Yokko.Core.Scoring;
using Yokko.Core.Timing;
using Yokko.Game.Gameplay;
using Yokko.Game.Screens.Gameplay;
using Yokko.Game.Screens.Settings;
using Yokko.Game.Skinning.OsuMania;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class GameplayScrollVelocityTest
{
    [TestCase(0, 0)]
    [TestCase(0.25, 0.5)]
    [TestCase(0.28, 0.6)]
    [TestCase(1, 2)]
    public void AdditionalLongNoteCutSliderSnapsToTenths(
        double progress,
        double expected)
    {
        Assert.That(
            AdditionalLongNoteCutSlider.ValueFromProgress(
                progress,
                YokkoSkinSettings.LongNoteCutAmountStep,
                YokkoSkinSettings.MinimumLongNoteCutAmount,
                YokkoSkinSettings.MaximumLongNoteCutAmount),
            Is.EqualTo(expected).Within(0.001));
    }

    [Test]
    public void BarLinesFollowTimingSectionsAndOmitFirstFlag()
    {
        var beatmap = new YokkoBeatmap(
            "Bar lines",
            "Artist",
            "Mapper",
            "4K",
            KeyMode.FourKey,
            ChartSourceFormat.OsuMania,
            [
                new YokkoTimingPoint(-2000, 500, Meter: 4),
                new YokkoTimingPoint(
                    3000,
                    250,
                    Meter: 4,
                    Effects: 8),
            ],
            null,
            [
                new YokkoHitObject(
                    0,
                    1000,
                    null,
                    HitObjectKind.Tap),
                new YokkoHitObject(
                    1,
                    4500,
                    null,
                    HitObjectKind.Tap),
            ]);

        Assert.That(
            GameplayPlayfield.GenerateBarLineTimes(beatmap),
            Is.EqualTo(new[] { 0d, 2000d, 4000d, 5000d }));
    }

    [Test]
    public void ZeroVelocityFreezesDrawableAndNegativeVelocityReversesIt()
    {
        var hitObject = new YokkoHitObject(
            0,
            3500,
            null,
            HitObjectKind.Tap);
        var drawable = new DrawableNote(0, hitObject, 80);
        ScrollVelocityMap map = createScrollMap();

        drawable.UpdatePosition(
            1000,
            false,
            false,
            0,
            500,
            1800,
            map);
        float atZeroStart = drawable.Y;

        drawable.UpdatePosition(
            1750,
            false,
            false,
            0,
            500,
            1800,
            map);
        float duringZero = drawable.Y;

        drawable.UpdatePosition(
            2500,
            false,
            false,
            0,
            500,
            1800,
            map);
        float duringReverse = drawable.Y;

        Assert.Multiple(() =>
        {
            Assert.That(duringZero, Is.EqualTo(atZeroStart));
            Assert.That(duringReverse, Is.LessThan(duringZero));
        });
    }

    [Test]
    public void TapCanLeaveAndReenterTheVisibleWindowDuringReverseSv()
    {
        YokkoBeatmap beatmap = createBeatmap(
            new YokkoHitObject(
                0,
                3500,
                null,
                HitObjectKind.Tap));
        var state = new BeatmapJudgementState(beatmap);
        var playfield = new GameplayPlayfield(
            beatmap,
            KeyModeBindings.ForMode(KeyMode.FourKey));

        playfield.UpdateGameplayTime(500, state);
        int beforeReverse = playfield.VisibleNoteCount;
        playfield.UpdateGameplayTime(1500, state);
        int duringFreeze = playfield.VisibleNoteCount;
        playfield.UpdateGameplayTime(2500, state);
        int afterReentry = playfield.VisibleNoteCount;

        Assert.Multiple(() =>
        {
            Assert.That(beforeReverse, Is.EqualTo(1));
            Assert.That(duringFreeze, Is.Zero);
            Assert.That(afterReentry, Is.EqualTo(1));
        });
    }

    [Test]
    public void HoldTailUsesLastNonZeroDirectionDuringZeroSv()
    {
        var hold = new YokkoHitObject(
            0,
            750,
            1750,
            HitObjectKind.Hold);
        var drawable = new DrawableNote(0, hold, 80);
        var map = new ScrollVelocityMap(
        [
            new YokkoScrollVelocity(1000, -1),
            new YokkoScrollVelocity(1500, 0),
            new YokkoScrollVelocity(2000, 1),
        ]);

        drawable.UpdatePosition(
            1000,
            false,
            false,
            0,
            500,
            1800,
            map);

        Assert.That(
            drawable.ReverseHoldTailForScrollVelocity,
            Is.True);
    }

    [Test]
    public void LegacyQuaverHoldBodyIgnoresIntermediateDirectionExtrema()
    {
        var hold = new YokkoHitObject(
            0,
            1000,
            3000,
            HitObjectKind.Hold);
        var modern = new DrawableNote(0, hold, 80);
        var legacy = new DrawableNote(
            0,
            hold,
            80,
            legacyLongNoteRendering: true);
        var map = new ScrollVelocityMap(
        [
            new YokkoScrollVelocity(1500, -1),
            new YokkoScrollVelocity(2500, 1),
        ]);

        modern.UpdatePosition(
            0,
            false,
            false,
            0,
            500,
            1800,
            map);
        legacy.UpdatePosition(
            0,
            false,
            false,
            0,
            500,
            1800,
            map);

        Assert.That(modern.Height, Is.GreaterThan(legacy.Height + 100));
    }

    [Test]
    public void LegacyQuaverHoldStillUsesFullPathForVisibility()
    {
        var hold = new YokkoHitObject(
            0,
            3000,
            5000,
            HitObjectKind.Hold);
        var beatmap = new YokkoBeatmap(
            "Legacy reverse SV",
            "Yokko",
            "Yokko",
            "4K",
            KeyMode.FourKey,
            ChartSourceFormat.Quaver,
            [YokkoTimingPoint.Default],
            null,
            [hold],
            ScrollVelocities:
            [
                new YokkoScrollVelocity(3500, -5),
                new YokkoScrollVelocity(4000, 2.5),
            ],
            LegacyLongNoteRendering: true);
        var state = new BeatmapJudgementState(beatmap);
        var playfield = new GameplayPlayfield(
            beatmap,
            KeyModeBindings.ForMode(KeyMode.FourKey));

        playfield.UpdateGameplayTime(0, state);

        Assert.That(playfield.VisibleNoteCount, Is.EqualTo(1));
    }

    [Test]
    public void AdditionalLongNoteCutUsesNoteWidthAndKeepsActualBounds()
    {
        var hold = new YokkoHitObject(
            0,
            1000,
            3000,
            HitObjectKind.Hold);
        var original = new DrawableNote(0, hold, 80);
        var cut = new DrawableNote(
            0,
            hold,
            80,
            longNoteCutAmount: 0.75);

        original.UpdatePosition(0, false, false, 0, 500, 1800);
        cut.UpdatePosition(0, false, false, 0, 500, 1800);

        Assert.Multiple(() =>
        {
            Assert.That(
                cut.AppliedLongNoteCutDistance,
                Is.EqualTo(60).Within(0.01));
            Assert.That(
                original.VisibleHoldBodyHeight
                - cut.VisibleHoldBodyHeight,
                Is.EqualTo(60).Within(0.01));
            Assert.That(
                System.Math.Abs(original.VisibleHoldTailY - cut.VisibleHoldTailY),
                Is.EqualTo(60).Within(0.01));
            Assert.That(cut.Height, Is.EqualTo(original.Height));
        });
    }

    [Test]
    public void ExtremelyLongHoldKeepsRenderedGeometryNearPlayfield()
    {
        var hold = new YokkoHitObject(
            0,
            1000,
            3_601_000,
            HitObjectKind.Hold);
        var drawable = new DrawableNote(0, hold, 80);

        drawable.UpdatePosition(
            1000,
            false,
            true,
            0,
            500,
            1800);

        Assert.Multiple(() =>
        {
            Assert.That(drawable.Alpha, Is.EqualTo(1));
            Assert.That(drawable.Height, Is.LessThan(800));
            Assert.That(drawable.VisibleHoldBodyHeight, Is.LessThan(800));
            Assert.That(drawable.VisibleHoldBodyHeight, Is.GreaterThan(500));
            Assert.That(float.IsFinite(drawable.Y), Is.True);
            Assert.That(float.IsFinite(drawable.Height), Is.True);
        });
    }

    [Test]
    public void PlaybackRateUsesSourceSpecificScrollNormalization()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                GameplayScreen.AdjustApproachTimeForPlaybackRate(
                    1800,
                    ChartSourceFormat.Quaver,
                    1.5),
                Is.EqualTo(2700));
            Assert.That(
                GameplayScreen.AdjustApproachTimeForPlaybackRate(
                    1800,
                    ChartSourceFormat.Quaver,
                    1.5,
                    50),
                Is.EqualTo(2160));
            Assert.That(
                GameplayScreen.AdjustApproachTimeForPlaybackRate(
                    1800,
                    ChartSourceFormat.Quaver,
                    1.5,
                    100),
                Is.EqualTo(1800));
            Assert.That(
                GameplayScreen.AdjustApproachTimeForPlaybackRate(
                    1800,
                    ChartSourceFormat.Quaver,
                    1.5,
                    100,
                    false),
                Is.EqualTo(2700));
            Assert.That(
                GameplayScreen.AdjustApproachTimeForPlaybackRate(
                    1800,
                    ChartSourceFormat.OsuMania,
                    1.5),
                Is.EqualTo(2700));
            Assert.That(
                GameplayScreen.AdjustApproachTimeForPlaybackRate(
                    1800,
                    ChartSourceFormat.OsuStandard,
                    0.75),
                Is.EqualTo(1350));
            Assert.That(
                GameplayScreen.AdjustApproachTimeForPlaybackRate(
                    1800,
                    ChartSourceFormat.Yokko,
                    1.5),
                Is.EqualTo(1800));
        });
    }

    [Test]
    public void ScrollSpeedAdjustmentMatchesLazerGameplayWindow()
    {
        const double longIntroGameplayStart = 28000;
        const double shortIntroGameplayStart = -1000;
        YokkoBreakPeriod[] breaks =
        [
            new YokkoBreakPeriod(45000, 48000),
        ];

        Assert.Multiple(() =>
        {
            Assert.That(
                GameplayScreen.IsScrollSpeedAdjustmentAllowed(
                    10000,
                    longIntroGameplayStart,
                    false,
                    breaks),
                Is.True);
            Assert.That(
                GameplayScreen.IsScrollSpeedAdjustmentAllowed(
                    38000,
                    longIntroGameplayStart,
                    false,
                    breaks),
                Is.True);
            Assert.That(
                GameplayScreen.IsScrollSpeedAdjustmentAllowed(
                    38000.01,
                    longIntroGameplayStart,
                    false,
                    breaks),
                Is.False);
            Assert.That(
                GameplayScreen.IsScrollSpeedAdjustmentAllowed(
                    9000,
                    shortIntroGameplayStart,
                    false,
                    breaks),
                Is.True);
            Assert.That(
                GameplayScreen.IsScrollSpeedAdjustmentAllowed(
                    9000.01,
                    shortIntroGameplayStart,
                    false,
                    breaks),
                Is.False);
            Assert.That(
                GameplayScreen.IsScrollSpeedAdjustmentAllowed(
                    46000,
                    longIntroGameplayStart,
                    false,
                    breaks),
                Is.True);
            Assert.That(
                GameplayScreen.IsScrollSpeedAdjustmentAllowed(
                    46000,
                    longIntroGameplayStart,
                    true,
                    breaks),
                Is.False);
        });
    }

    [Test]
    public void PlayerScrollDirectionMirrorsBuiltInPlayfield()
    {
        YokkoBeatmap beatmap = createBeatmap(
            new YokkoHitObject(
                0,
                2500,
                null,
                HitObjectKind.Tap));
        var downscroll = new GameplayPlayfield(
            beatmap,
            KeyModeBindings.ForMode(KeyMode.FourKey));
        var upscroll = new GameplayPlayfield(
            beatmap,
            KeyModeBindings.ForMode(KeyMode.FourKey),
            scrollDirection: ManiaScrollDirection.Upscroll);

        downscroll.GetDrawableNote(0).UpdatePosition(
            2500,
            false,
            false,
            downscroll.ScrollOrigin,
            downscroll.JudgementPosition,
            1800);
        upscroll.GetDrawableNote(0).UpdatePosition(
            2500,
            false,
            false,
            upscroll.ScrollOrigin,
            upscroll.JudgementPosition,
            1800);

        Assert.Multiple(() =>
        {
            Assert.That(downscroll.ScrollOrigin, Is.EqualTo(28));
            Assert.That(
                downscroll.JudgementPosition,
                Is.EqualTo(528));
            Assert.That(upscroll.ScrollOrigin, Is.EqualTo(592));
            Assert.That(
                upscroll.JudgementPosition,
                Is.EqualTo(92));
            Assert.That(
                downscroll.GetDrawableNote(0).Y
                + downscroll.GetDrawableNote(0).Height,
                Is.EqualTo(528).Within(0.01));
            Assert.That(
                upscroll.GetDrawableNote(0).Y,
                Is.EqualTo(92).Within(0.01));
        });
    }

    [Test]
    public void JudgementTimingDoesNotDependOnVisualDirection()
    {
        YokkoBeatmap beatmap = createBeatmap(
            new YokkoHitObject(
                0,
                2500,
                null,
                HitObjectKind.Tap));
        var state = new BeatmapJudgementState(beatmap);

        JudgementEvent result =
            state.TryJudgeLanePress(0, 2500);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Rating, Is.EqualTo(
                JudgementRating.Perfect));
            Assert.That(result.HitErrorMilliseconds, Is.Zero);
        });
    }

    private static ScrollVelocityMap createScrollMap() =>
        new(
        [
            new YokkoScrollVelocity(1000, 0),
            new YokkoScrollVelocity(2000, -1),
            new YokkoScrollVelocity(3000, 1),
        ]);

    private static YokkoBeatmap createBeatmap(
        params YokkoHitObject[] hitObjects) =>
        new(
            "Reverse SV test",
            "Yokko",
            "Yokko",
            "4K",
            KeyMode.FourKey,
            ChartSourceFormat.Quaver,
            [YokkoTimingPoint.Default],
            null,
            hitObjects,
            ScrollVelocities:
            [
                new YokkoScrollVelocity(1000, 0),
                new YokkoScrollVelocity(2000, -1),
                new YokkoScrollVelocity(3000, 1),
            ]);
}
