using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using Yokko.Core.Beatmaps;
using Yokko.Core.Gameplay;
using Yokko.Core.Mods;
using Yokko.Core.Scoring;
using Yokko.Core.Timing;

namespace Yokko.Game.Tests.Core
{
    [TestFixture]
    public sealed class JudgementStateTest
    {
        [Test]
        public void DestinationOverloadsMatchConvenienceMethods()
        {
            var expected = new BeatmapJudgementState(createHoldBeatmap());
            var actual = new BeatmapJudgementState(createHoldBeatmap());
            var events = new List<JudgementEvent>();

            IReadOnlyList<JudgementEvent> expectedHead =
                expected.JudgeLanePress(1, 1000);
            actual.JudgeLanePress(1, 1000, events);
            Assert.That(events, Is.EqualTo(expectedHead));

            events.Clear();
            IReadOnlyList<JudgementEvent> expectedDrop =
                expected.JudgeLaneRelease(1, 1250);
            actual.JudgeLaneRelease(1, 1250, events);
            Assert.That(events, Is.EqualTo(expectedDrop));

            events.Clear();
            IReadOnlyList<JudgementEvent> expectedRegrab =
                expected.JudgeLanePress(1, 1400);
            actual.JudgeLanePress(1, 1400, events);
            Assert.That(events, Is.EqualTo(expectedRegrab));

            events.Clear();
            IReadOnlyList<JudgementEvent> expectedTail =
                expected.JudgeLaneRelease(1, 1500);
            actual.JudgeLaneRelease(1, 1500, events);

            Assert.Multiple(() =>
            {
                Assert.That(events, Is.EqualTo(expectedTail));
                Assert.That(
                    new[]
                    {
                        actual.Counts.Perfect,
                        actual.Counts.Great,
                        actual.Counts.Good,
                        actual.Counts.Ok,
                        actual.Counts.Meh,
                        actual.Counts.Miss,
                        actual.Counts.ComboBreak,
                    },
                    Is.EqualTo(new[]
                    {
                        expected.Counts.Perfect,
                        expected.Counts.Great,
                        expected.Counts.Good,
                        expected.Counts.Ok,
                        expected.Counts.Meh,
                        expected.Counts.Miss,
                        expected.Counts.ComboBreak,
                    }));
                Assert.That(actual.Score, Is.EqualTo(expected.Score));
                Assert.That(actual.Combo, Is.EqualTo(expected.Combo));
                Assert.That(actual.IsComplete, Is.EqualTo(expected.IsComplete));
            });
        }

        [Test]
        public void DestinationOverloadsAppendNothingForInvalidLane()
        {
            var state = new BeatmapJudgementState(createTapBeatmap(1000));
            var marker = new JudgementEvent(
                0,
                0,
                0,
                null,
                0,
                JudgementRating.None,
                JudgementPhase.Tap);
            var events = new List<JudgementEvent> { marker };

            state.JudgeLanePress(-1, 0, events);
            state.JudgeLaneRelease(99, 0, events);

            Assert.That(events, Is.EqualTo(new[] { marker }));
        }

        [TestCase(JudgementMode.Yokko, 1.5)]
        [TestCase(JudgementMode.Etterna, 1)]
        [TestCase(JudgementMode.Quaver, 1.5)]
        [TestCase(JudgementMode.OsuStable, 1)]
        [TestCase(JudgementMode.BmsBeatoraja, 1)]
        public void EveryJudgementModeReportsIndependentHoldInputTiming(
            JudgementMode mode,
            double expectedTailWindowScale)
        {
            YokkoBeatmap beatmap = createHoldBeatmap();
            JudgementConfiguration configuration = mode switch
            {
                JudgementMode.Yokko => JudgementConfiguration.YokkoDefault,
                JudgementMode.Etterna => JudgementConfiguration.EtternaDefault,
                JudgementMode.Quaver => JudgementConfiguration.QuaverDefault,
                JudgementMode.OsuStable =>
                    JudgementConfiguration.OsuStableDefault,
                JudgementMode.BmsBeatoraja =>
                    JudgementConfiguration.BmsBeatorajaDefault,
                _ => throw new ArgumentOutOfRangeException(nameof(mode)),
            };
            var state = new BeatmapJudgementState(
                beatmap,
                new JudgementWindows(
                    beatmap.OverallDifficulty,
                    configuration: configuration));
            var judgements = new List<JudgementEvent>();
            var inputs = new List<JudgementInputEvent>();

            state.JudgeLanePress(1, 1010, judgements, inputs);
            state.JudgeLaneRelease(1, 1520, judgements, inputs);

            Assert.Multiple(() =>
            {
                Assert.That(
                    inputs.Select(static input => input.Phase),
                    Is.EqualTo(new[]
                    {
                        JudgementPhase.HoldHead,
                        JudgementPhase.HoldTail,
                    }));
                Assert.That(inputs[0].ObjectTimeMilliseconds, Is.EqualTo(1000));
                Assert.That(inputs[0].HitTimeMilliseconds, Is.EqualTo(1010));
                Assert.That(inputs[0].HitErrorMilliseconds, Is.EqualTo(10));
                Assert.That(inputs[0].Rating, Is.Not.EqualTo(
                    JudgementRating.IgnoreHit));
                Assert.That(inputs[1].ObjectTimeMilliseconds, Is.EqualTo(1500));
                Assert.That(inputs[1].HitTimeMilliseconds, Is.EqualTo(1520));
                Assert.That(inputs[1].HitErrorMilliseconds, Is.EqualTo(20));
                Assert.That(
                    inputs[1].TimingWindowScale,
                    Is.EqualTo(expectedTailWindowScale));
            });
        }

        [Test]
        public void StableImmediateHoldMissIsStillAHeadInput()
        {
            BeatmapJudgementState state = createOsuStableState(
                createHoldBeatmap());
            var judgements = new List<JudgementEvent>();
            var inputs = new List<JudgementInputEvent>();

            state.JudgeLanePress(1, 1120, judgements, inputs);

            Assert.Multiple(() =>
            {
                Assert.That(inputs, Has.Count.EqualTo(1));
                Assert.That(inputs[0].Phase, Is.EqualTo(
                    JudgementPhase.HoldHead));
                Assert.That(inputs[0].HitErrorMilliseconds, Is.EqualTo(120));
                Assert.That(inputs[0].Rating, Is.EqualTo(
                    JudgementRating.Miss));
            });
        }

        [Test]
        public void BmsTailInputKeepsItsOwnErrorAndRating()
        {
            BeatmapJudgementState state = createBmsState(
                createBmsHoldBeatmap(BmsJudgementMetadata.FromRank(3)));
            var judgements = new List<JudgementEvent>();
            var inputs = new List<JudgementInputEvent>();

            state.JudgeLanePress(0, 1060, judgements, inputs);
            state.JudgeLaneRelease(0, 1500, judgements, inputs);

            JudgementInputEvent tail = inputs.Single(input =>
                input.Phase == JudgementPhase.HoldTail);
            JudgementEvent combined = judgements.Single(judgement =>
                judgement.Phase == JudgementPhase.Hold);
            Assert.Multiple(() =>
            {
                Assert.That(tail.HitErrorMilliseconds, Is.Zero);
                Assert.That(tail.Rating, Is.EqualTo(JudgementRating.Perfect));
                Assert.That(combined.HitErrorMilliseconds, Is.EqualTo(60));
                Assert.That(combined.Rating, Is.EqualTo(JudgementRating.Great));
            });
        }

        [Test]
        public void EtternaEarlyReleaseReportsThePhysicalTailInput()
        {
            BeatmapJudgementState state = createEtternaState(
                createHoldBeatmap());
            var judgements = new List<JudgementEvent>();
            var inputs = new List<JudgementInputEvent>();

            state.JudgeLanePress(1, 1000, judgements, inputs);
            state.JudgeLaneRelease(1, 1400, judgements, inputs);

            Assert.Multiple(() =>
            {
                Assert.That(judgements, Has.Count.EqualTo(1));
                Assert.That(inputs, Has.Count.EqualTo(2));
                Assert.That(inputs[1].Phase, Is.EqualTo(
                    JudgementPhase.HoldTail));
                Assert.That(inputs[1].HitTimeMilliseconds, Is.EqualTo(1400));
                Assert.That(inputs[1].HitErrorMilliseconds, Is.EqualTo(-100));
                Assert.That(inputs[1].Rating, Is.EqualTo(
                    JudgementRating.IgnoreHit));
            });
        }

        [TestCase(JudgementMode.Yokko)]
        [TestCase(JudgementMode.Quaver)]
        [TestCase(JudgementMode.OsuStable)]
        public void UnscoredEarlyReleaseStillReportsThePhysicalTailInput(
            JudgementMode mode)
        {
            JudgementConfiguration configuration = mode switch
            {
                JudgementMode.Yokko => JudgementConfiguration.YokkoDefault,
                JudgementMode.Quaver => JudgementConfiguration.QuaverDefault,
                JudgementMode.OsuStable =>
                    JudgementConfiguration.OsuStableDefault,
                _ => throw new ArgumentOutOfRangeException(nameof(mode)),
            };
            YokkoBeatmap beatmap = createHoldBeatmap();
            var state = new BeatmapJudgementState(
                beatmap,
                new JudgementWindows(
                    beatmap.OverallDifficulty,
                    configuration: configuration));
            var judgements = new List<JudgementEvent>();
            var inputs = new List<JudgementInputEvent>();

            state.JudgeLanePress(1, 1000, judgements, inputs);
            state.JudgeLaneRelease(1, 1200, judgements, inputs);

            Assert.Multiple(() =>
            {
                Assert.That(inputs, Has.Count.EqualTo(2));
                Assert.That(inputs[1].Phase, Is.EqualTo(
                    JudgementPhase.HoldTail));
                Assert.That(inputs[1].HitTimeMilliseconds, Is.EqualTo(1200));
                Assert.That(inputs[1].HitErrorMilliseconds, Is.EqualTo(-300));
                Assert.That(inputs[1].Rating, Is.EqualTo(
                    JudgementRating.None));
            });
        }

        [Test]
        public void NoReleaseDoesNotReportPhysicalTailTiming()
        {
            var state = new BeatmapJudgementState(
                createHoldBeatmap(),
                noRelease: true);
            var judgements = new List<JudgementEvent>();
            var inputs = new List<JudgementInputEvent>();

            state.JudgeLanePress(1, 1000, judgements, inputs);
            state.JudgeLaneRelease(1, 1500, judgements, inputs);

            Assert.That(
                inputs.Select(static input => input.Phase),
                Is.EqualTo(new[] { JudgementPhase.HoldHead }));
        }

        [Test]
        public void EmptyInputEdgesDoNotAllocateAfterWarmup()
        {
            var state = new BeatmapJudgementState(createTapBeatmap(1000));
            var events = new List<JudgementEvent>(8);

            for (int index = 0; index < 1000; index++)
            {
                state.JudgeLanePress(3, 0, events);
                events.Clear();
                state.JudgeLaneRelease(3, 0, events);
                events.Clear();
            }

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int index = 0; index < 10_000; index++)
            {
                state.JudgeLanePress(3, 0, events);
                events.Clear();
                state.JudgeLaneRelease(3, 0, events);
                events.Clear();
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(allocated, Is.Zero);
        }

        [Test]
        public void ClassicUsesStableManiaWindowsUnlessScoreV2IsPresent()
        {
            var classic = new JudgementWindows(
                8,
                classic: true);
            Assert.That(classic.PerfectMilliseconds, Is.EqualTo(16.5));
            Assert.That(classic.GreatMilliseconds, Is.EqualTo(40.5));
            Assert.That(classic.GoodMilliseconds, Is.EqualTo(73.5));
            Assert.That(classic.OkMilliseconds, Is.EqualTo(103.5));
            Assert.That(classic.MehMilliseconds, Is.EqualTo(127.5));
            Assert.That(classic.MissMilliseconds, Is.EqualTo(164.5));

            var scoreV2 = new JudgementWindows(
                8,
                classic: true,
                scoreV2: true);
            var modern = new JudgementWindows(8);
            Assert.That(
                scoreV2.PerfectMilliseconds,
                Is.EqualTo(modern.PerfectMilliseconds));
            Assert.That(
                scoreV2.MissMilliseconds,
                Is.EqualTo(modern.MissMilliseconds));
        }

        [Test]
        public void ClassicConvertUsesLazerConvertedManiaWindows()
        {
            var windows = new JudgementWindows(
                overallDifficulty: 5,
                classic: true,
                isConvert: true);

            Assert.Multiple(() =>
            {
                Assert.That(windows.PerfectMilliseconds, Is.EqualTo(16.5));
                Assert.That(windows.GreatMilliseconds, Is.EqualTo(34.5));
                Assert.That(windows.GoodMilliseconds, Is.EqualTo(67.5));
                Assert.That(windows.OkMilliseconds, Is.EqualTo(97.5));
                Assert.That(windows.MehMilliseconds, Is.EqualTo(121.5));
                Assert.That(windows.MissMilliseconds, Is.EqualTo(158.5));
            });
        }

        [Test]
        public void OsuStableModeUsesScoreV1Windows()
        {
            var windows = new JudgementWindows(
                8,
                configuration: JudgementConfiguration.OsuStableDefault);

            Assert.Multiple(() =>
            {
                Assert.That(windows.PerfectMilliseconds, Is.EqualTo(16.5));
                Assert.That(windows.GreatMilliseconds, Is.EqualTo(40.5));
                Assert.That(windows.GoodMilliseconds, Is.EqualTo(73.5));
                Assert.That(windows.OkMilliseconds, Is.EqualTo(103.5));
                Assert.That(windows.MehMilliseconds, Is.EqualTo(127.5));
                Assert.That(windows.MissMilliseconds, Is.EqualTo(164.5));
            });
        }

        [Test]
        public void OsuStableLabelsKeepRainbowAndGold300Separate()
        {
            JudgementConfiguration configuration =
                JudgementConfiguration.OsuStableDefault;

            Assert.Multiple(() =>
            {
                Assert.That(
                    configuration.RatingLabel(JudgementRating.Perfect),
                    Is.EqualTo("300G"));
                Assert.That(
                    configuration.RatingLabel(JudgementRating.Great),
                    Is.EqualTo("300"));
                Assert.That(
                    configuration.RatingLabel(JudgementRating.Good),
                    Is.EqualTo("200"));
                Assert.That(
                    configuration.RatingLabel(JudgementRating.Ok),
                    Is.EqualTo("100"));
                Assert.That(
                    configuration.RatingLabel(JudgementRating.Meh),
                    Is.EqualTo("50"));
                Assert.That(
                    configuration.RatingLabel(JudgementRating.Miss),
                    Is.EqualTo("MISS"));
            });
        }

        [Test]
        public void OsuStableConvertUsesLegacyConvertWindows()
        {
            var windows = new JudgementWindows(
                overallDifficulty: 5,
                isConvert: true,
                configuration: JudgementConfiguration.OsuStableDefault);

            Assert.Multiple(() =>
            {
                Assert.That(windows.PerfectMilliseconds, Is.EqualTo(16.5));
                Assert.That(windows.GreatMilliseconds, Is.EqualTo(34.5));
                Assert.That(windows.GoodMilliseconds, Is.EqualTo(67.5));
                Assert.That(windows.OkMilliseconds, Is.EqualTo(97.5));
                Assert.That(windows.MehMilliseconds, Is.EqualTo(121.5));
                Assert.That(windows.MissMilliseconds, Is.EqualTo(158.5));
            });
        }

        [Test]
        public void OsuStableWindowsIgnoreRateButApplyHardRockScaling()
        {
            var doubleTime = new JudgementWindows(
                8,
                speedMultiplier: 1.5,
                configuration: JudgementConfiguration.OsuStableDefault);
            var hardRock = new JudgementWindows(
                8,
                speedMultiplier: 1.5,
                difficultyMultiplier: 1.4,
                configuration: JudgementConfiguration.OsuStableDefault);

            Assert.Multiple(() =>
            {
                Assert.That(doubleTime.PerfectMilliseconds,
                    Is.EqualTo(16.5));
                Assert.That(doubleTime.GreatMilliseconds,
                    Is.EqualTo(40.5));
                Assert.That(hardRock.PerfectMilliseconds,
                    Is.EqualTo(11.5));
                Assert.That(hardRock.GreatMilliseconds,
                    Is.EqualTo(28.5));
            });
        }

        [Test]
        public void OsuStableTapAllowsEarlyMehButNotLateMeh()
        {
            var early = createOsuStableState(createTapBeatmap(1000));
            var late = createOsuStableState(createTapBeatmap(1000));

            Assert.Multiple(() =>
            {
                Assert.That(
                    early.JudgeLanePress(0, 890).Single().Rating,
                    Is.EqualTo(JudgementRating.Meh));
                Assert.That(
                    late.JudgeLanePress(0, 1110).Single().Rating,
                    Is.EqualTo(JudgementRating.Miss));
            });
        }

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
        public void QuaverWindowsAndLabelsMatchUpstreamDefaults()
        {
            var configuration = JudgementConfiguration.QuaverDefault;
            var windows = new JudgementWindows(configuration: configuration);

            Assert.Multiple(() =>
            {
                Assert.That(windows.PerfectMilliseconds, Is.EqualTo(18));
                Assert.That(windows.GreatMilliseconds, Is.EqualTo(43));
                Assert.That(windows.GoodMilliseconds, Is.EqualTo(76));
                Assert.That(windows.OkMilliseconds, Is.EqualTo(106));
                Assert.That(windows.MehMilliseconds, Is.EqualTo(127));
                Assert.That(windows.MissMilliseconds, Is.EqualTo(164));
                Assert.That(
                    configuration.RatingLabel(JudgementRating.Perfect),
                    Is.EqualTo("MARVELOUS"));
                Assert.That(
                    configuration.RatingLabel(JudgementRating.Meh),
                    Is.EqualTo("OKAY"));
            });
        }

        [Test]
        public void QuaverMinesUseMarvelousWindow()
        {
            var state = new BeatmapJudgementState(
                createMineBeatmap(),
                new JudgementWindows(
                    configuration: JudgementConfiguration.QuaverDefault));

            Assert.That(
                state.ActiveMineWindowMilliseconds,
                Is.EqualTo(18));
            Assert.That(
                state.JudgeLanePress(0, 1000 - 18.001),
                Is.Empty);
            Assert.That(
                state.JudgeLanePress(0, 1000 - 18).Single().Phase,
                Is.EqualTo(JudgementPhase.Mine));
        }

        [TestCase(0, 5, 15, 37.5, 55, 70)]
        [TestCase(1, 10, 30, 75, 110, 140)]
        [TestCase(2, 15, 45, 112.5, 165, 210)]
        [TestCase(3, 20, 60, 150, 220, 280)]
        [TestCase(4, 25, 75, 187.5, 275, 350)]
        public void BmsSevenKeyWindowsMatchBeatoraja(
            int rank,
            double pgreat,
            double great,
            double good,
            double badEarly,
            double badLate)
        {
            var windows = new BmsJudgementWindows(
                BmsJudgementMetadata.FromRank(rank));

            Assert.Multiple(() =>
            {
                Assert.That(
                    windows.Judge(pgreat, BmsJudgeObjectType.Note),
                    Is.EqualTo(JudgementRating.Perfect));
                Assert.That(
                    windows.Judge(great, BmsJudgeObjectType.Note),
                    Is.EqualTo(JudgementRating.Great));
                Assert.That(
                    windows.Judge(good, BmsJudgeObjectType.Note),
                    Is.EqualTo(JudgementRating.Good));
                Assert.That(
                    windows.Judge(-badEarly, BmsJudgeObjectType.Note),
                    Is.EqualTo(JudgementRating.Ok));
                Assert.That(
                    windows.Judge(badLate, BmsJudgeObjectType.Note),
                    Is.EqualTo(JudgementRating.Ok));
                Assert.That(
                    windows.Judge(
                        Math.BitDecrement(-badEarly),
                        BmsJudgeObjectType.Note),
                    Is.EqualTo(JudgementRating.None));
                Assert.That(
                    windows.Judge(
                        Math.BitIncrement(badLate),
                        BmsJudgeObjectType.Note),
                    Is.EqualTo(JudgementRating.None));
            });
        }

        [TestCase(5, BmsJudgeObjectType.Note, 20, 50, 100, JudgementRating.Good, 150, 150)]
        [TestCase(5, BmsJudgeObjectType.Scratch, 30, 60, 110, JudgementRating.Good, 160, 160)]
        [TestCase(5, BmsJudgeObjectType.LongNoteEnd, 120, 150, 200, JudgementRating.Good, 250, 250)]
        [TestCase(5, BmsJudgeObjectType.LongScratchEnd, 130, 160, 160, JudgementRating.Great, 260, 260)]
        [TestCase(7, BmsJudgeObjectType.Note, 20, 60, 150, JudgementRating.Good, 220, 280)]
        [TestCase(7, BmsJudgeObjectType.Scratch, 30, 70, 160, JudgementRating.Good, 230, 290)]
        [TestCase(7, BmsJudgeObjectType.LongNoteEnd, 120, 160, 200, JudgementRating.Good, 220, 280)]
        [TestCase(7, BmsJudgeObjectType.LongScratchEnd, 130, 170, 210, JudgementRating.Good, 230, 290)]
        public void BmsEasyObjectWindowsMatchBeatoraja(
            int regularKeys,
            BmsJudgeObjectType type,
            double pgreat,
            double great,
            double good,
            JudgementRating goodBoundaryRating,
            double badEarly,
            double badLate)
        {
            var windows = new BmsJudgementWindows(
                BmsJudgementMetadata.FromRank(3),
                regularKeysPerStage: regularKeys);

            Assert.Multiple(() =>
            {
                Assert.That(windows.Judge(pgreat, type),
                    Is.EqualTo(JudgementRating.Perfect));
                Assert.That(windows.Judge(great, type),
                    Is.EqualTo(JudgementRating.Great));
                Assert.That(windows.Judge(good, type),
                    Is.EqualTo(goodBoundaryRating));
                Assert.That(windows.Judge(-badEarly, type),
                    Is.EqualTo(JudgementRating.Ok));
                Assert.That(windows.Judge(badLate, type),
                    Is.EqualTo(JudgementRating.Ok));
            });
        }

        [Test]
        public void BmsEmptyPressDoesNotConsumeTheNote()
        {
            BeatmapJudgementState state = createBmsState(
                createBmsTapBeatmap(BmsJudgementMetadata.FromRank(3)));

            JudgementEvent emptyPress = state.JudgeLanePress(0, 600).Single();
            JudgementEvent hit = state.JudgeLanePress(0, 1000).Single();

            Assert.Multiple(() =>
            {
                Assert.That(emptyPress.Rating, Is.EqualTo(JudgementRating.Meh));
                Assert.That(emptyPress.Phase,
                    Is.EqualTo(JudgementPhase.BmsEmptyPress));
                Assert.That(state.Counts.Meh, Is.EqualTo(1));
                Assert.That(hit.Rating, Is.EqualTo(JudgementRating.Perfect));
                Assert.That(state.IsResolved(0), Is.True);
            });
        }

        [Test]
        public void BmsEmptyPressAfterFinalNoteUsesTheResolvedNote()
        {
            BeatmapJudgementState state = createBmsState(
                createBmsTapBeatmap(BmsJudgementMetadata.FromRank(3)));

            state.JudgeLanePress(0, 1000);
            state.JudgeLaneRelease(0, 1050);
            JudgementEvent emptyPress =
                state.JudgeLanePress(0, 1100).Single();

            Assert.Multiple(() =>
            {
                Assert.That(emptyPress.Rating, Is.EqualTo(JudgementRating.Meh));
                Assert.That(emptyPress.Phase,
                    Is.EqualTo(JudgementPhase.BmsEmptyPress));
                Assert.That(emptyPress.HitObjectIndex, Is.Zero);
                Assert.That(state.Counts.Meh, Is.EqualTo(1));
                Assert.That(state.Counts.Perfect, Is.EqualTo(1));
            });
        }

        [TestCase(5, true)]
        [TestCase(7, false)]
        public void BmsEmptyPressComboRuleMatchesKeyProfile(
            int regularKeys,
            bool breaksCombo)
        {
            var beatmap = new YokkoBeatmap(
                "BMS empty MS combo test",
                "Yokko",
                "Yokko",
                $"{regularKeys}K",
                regularKeys == 5 ? KeyMode.SixKey : KeyMode.EightKey,
                ChartSourceFormat.Bms,
                [YokkoTimingPoint.Default],
                null,
                [new YokkoHitObject(1, 1000, null, HitObjectKind.Tap)],
                ScratchLane: 0,
                BmsJudgement: BmsJudgementMetadata.FromRank(3));
            BeatmapJudgementState state = createBmsState(beatmap);

            state.JudgeLanePress(1, 1000);
            state.JudgeLaneRelease(1, 1050);
            state.JudgeLanePress(1, 1100);

            Assert.That(state.Combo, Is.EqualTo(breaksCombo ? 0 : 1));
        }

        [Test]
        public void BmsNaturalPoorOccursAfterLateBadBoundary()
        {
            BeatmapJudgementState state = createBmsState(
                createBmsTapBeatmap(BmsJudgementMetadata.FromRank(3)));

            Assert.That(state.CollectExpiredMisses(1280), Is.Empty);
            JudgementEvent poor =
                state.CollectExpiredMisses(1280.001).Single();

            Assert.That(poor.Rating, Is.EqualTo(JudgementRating.Miss));
            Assert.That(
                JudgementConfiguration.BmsBeatorajaDefault.RatingLabel(
                    poor.Rating),
                Is.EqualTo("POOR"));
        }

        [Test]
        public void BmsScratchAndTraditionalLongNoteUseDedicatedWindows()
        {
            YokkoBeatmap scratchBeatmap = createBmsTapBeatmap(
                BmsJudgementMetadata.FromRank(3),
                scratchLane: 0);
            BeatmapJudgementState scratchState = createBmsState(scratchBeatmap);
            Assert.That(
                scratchState.JudgeLanePress(0, 1030).Single().Rating,
                Is.EqualTo(JudgementRating.Perfect));

            BeatmapJudgementState holdState = createBmsState(
                createBmsHoldBeatmap(BmsJudgementMetadata.FromRank(3)));
            JudgementEvent head = holdState.JudgeLanePress(0, 1060).Single();
            JudgementEvent tail = holdState.JudgeLaneRelease(0, 1380).Single();

            Assert.Multiple(() =>
            {
                Assert.That(head.Rating, Is.EqualTo(JudgementRating.IgnoreHit));
                Assert.That(holdState.Counts.TotalBasic, Is.EqualTo(1));
                Assert.That(tail.Phase, Is.EqualTo(JudgementPhase.Hold));
                Assert.That(tail.Rating, Is.EqualTo(JudgementRating.Great));
            });
        }

        [Test]
        public void BmsTraditionalLongNoteLateReleaseCanDegradeHead()
        {
            BeatmapJudgementState state = createBmsState(
                createBmsHoldBeatmap(BmsJudgementMetadata.FromRank(3)));

            state.JudgeLanePress(0, 1000);
            JudgementEvent tail =
                state.JudgeLaneRelease(0, 1710).Single();

            Assert.That(tail.Rating, Is.EqualTo(JudgementRating.Ok));
        }

        [Test]
        public void BmsTraditionalLongNoteHeldPastTailKeepsHeadRating()
        {
            BeatmapJudgementState state = createBmsState(
                createBmsHoldBeatmap(BmsJudgementMetadata.FromRank(3)));

            state.JudgeLanePress(0, 1060);

            Assert.That(state.CollectExpiredMisses(1500), Is.Empty);
            JudgementEvent tail =
                state.CollectExpiredMisses(1500.001).Single();
            Assert.Multiple(() =>
            {
                Assert.That(tail.Rating, Is.EqualTo(JudgementRating.Great));
                Assert.That(tail.HitTimeMilliseconds, Is.Null);
            });
        }

        [TestCase(4, 22.5, 45, 90, 135)]
        [TestCase(5, 18.9, 37.8, 75.6, 113.4)]
        [TestCase(6, 14.85, 29.7, 59.4, 89.1)]
        [TestCase(7, 11.25, 22.5, 45, 67.5)]
        [TestCase(8, 7.425, 14.85, 29.7, 44.55)]
        [TestCase(9, 4.5, 9, 18, 27)]
        public void EtternaJudgeWindowsMatchUpstream(
            int justice,
            double w1,
            double w2,
            double w3,
            double w4)
        {
            var configuration = new JudgementConfiguration(
                JudgementMode.Etterna,
                justice);
            var windows = new JudgementWindows(
                configuration: configuration);

            Assert.Multiple(() =>
            {
                Assert.That(
                    windows.PerfectMilliseconds,
                    Is.EqualTo(w1).Within(0.000001));
                Assert.That(
                    windows.GreatMilliseconds,
                    Is.EqualTo(w2).Within(0.000001));
                Assert.That(
                    windows.GoodMilliseconds,
                    Is.EqualTo(w3).Within(0.000001));
                Assert.That(
                    windows.OkMilliseconds,
                    Is.EqualTo(w4).Within(0.000001));
                Assert.That(windows.MehMilliseconds, Is.EqualTo(180));
                Assert.That(windows.MissMilliseconds, Is.EqualTo(180));
            });
        }

        [Test]
        public void EtternaBoundariesAreInclusiveAndW5ReachesFixedMissBoundary()
        {
            var windows = new JudgementWindows(
                configuration: new JudgementConfiguration(
                    JudgementMode.Etterna,
                    9));

            Assert.Multiple(() =>
            {
                Assert.That(
                    windows.Judge(windows.PerfectMilliseconds),
                    Is.EqualTo(JudgementRating.Perfect));
                Assert.That(
                    windows.Judge(windows.PerfectMilliseconds + 0.001),
                    Is.EqualTo(JudgementRating.Great));
                Assert.That(
                    windows.Judge(windows.OkMilliseconds + 0.001),
                    Is.EqualTo(JudgementRating.Meh));
                Assert.That(
                    windows.Judge(180),
                    Is.EqualTo(JudgementRating.Meh));
                Assert.That(
                    windows.Judge(180.001),
                    Is.EqualTo(JudgementRating.None));
            });
        }

        [TestCase(1050)]
        [TestCase(1070)]
        public void EtternaSelectsClosestNoteAndBreaksTiesTowardFuture(
            double inputTime)
        {
            var state = new BeatmapJudgementState(
                createTapBeatmap(1000, 1100),
                new JudgementWindows(
                    configuration: new JudgementConfiguration(
                        JudgementMode.Etterna,
                        4)));

            IReadOnlyList<JudgementEvent> events =
                state.JudgeLanePress(0, inputTime);

            Assert.Multiple(() =>
            {
                Assert.That(events, Has.Count.EqualTo(1));
                Assert.That(events[0].HitObjectIndex, Is.EqualTo(1));
                Assert.That(
                    events[0].HitErrorMilliseconds,
                    Is.EqualTo(inputTime - 1100));
                Assert.That(state.IsResolved(0), Is.False);
            });
        }

        [Test]
        public void EtternaNaturalMissOccursAfterFixedOuterBoundary()
        {
            var state = new BeatmapJudgementState(
                createTapBeatmap(1000),
                new JudgementWindows(
                    configuration: new JudgementConfiguration(
                        JudgementMode.Etterna,
                        9)));

            Assert.That(state.CollectExpiredMisses(1180), Is.Empty);
            IReadOnlyList<JudgementEvent> events =
                state.CollectExpiredMisses(1180.001);

            Assert.Multiple(() =>
            {
                Assert.That(events, Has.Count.EqualTo(1));
                Assert.That(
                    events[0].Rating,
                    Is.EqualTo(JudgementRating.Miss));
            });
        }

        [Test]
        public void EtternaRateChangeKeepsRealWorldWindowsConstant()
        {
            var windows = new JudgementWindows(
                speedMultiplier: 1.5,
                configuration: new JudgementConfiguration(
                    JudgementMode.Etterna,
                    9));

            Assert.Multiple(() =>
            {
                Assert.That(
                    windows.PerfectMilliseconds / 1.5,
                    Is.EqualTo(4.5).Within(0.000001));
                Assert.That(
                    windows.MissMilliseconds / 1.5,
                    Is.EqualTo(180).Within(0.000001));
            });
        }

        [Test]
        public void EtternaW4W5AndMissBreakCombo()
        {
            BeatmapJudgementState state = createEtternaState(
                createTapBeatmap(
                    1000,
                    1500,
                    2000,
                    2500,
                    3000,
                    3500));

            state.JudgeLanePress(0, 1000);
            state.JudgeLanePress(0, 1560);
            Assert.That(state.Combo, Is.EqualTo(2));

            state.JudgeLanePress(0, 2100);
            Assert.That(state.Combo, Is.Zero);

            state.JudgeLanePress(0, 2500);
            Assert.That(state.Combo, Is.EqualTo(1));

            state.JudgeLanePress(0, 3150);
            Assert.That(state.Combo, Is.Zero);

            state.CollectExpiredMisses(3680.001);

            Assert.Multiple(() =>
            {
                Assert.That(state.Combo, Is.Zero);
                Assert.That(state.ComboBreaks, Is.EqualTo(3));
                Assert.That(state.Counts.Ok, Is.EqualTo(1));
                Assert.That(state.Counts.Meh, Is.EqualTo(1));
                Assert.That(state.Counts.Miss, Is.EqualTo(1));
                Assert.That(state.MissCombo, Is.EqualTo(2));
                Assert.That(state.MaxMissCombo, Is.EqualTo(2));
            });
        }

        [Test]
        public void EtternaComboBreakRulesDoNotLeakIntoYokkoScoring()
        {
            YokkoBeatmap beatmap =
                createTapBeatmap(1000, 1500, 2000, 2500);
            var yokko = new ManiaScoreProcessor(beatmap);
            var etterna = new ManiaScoreProcessor(
                beatmap,
                judgementConfiguration:
                    JudgementConfiguration.EtternaDefault);
            JudgementRating[] ratings =
            [
                JudgementRating.Perfect,
                JudgementRating.Ok,
                JudgementRating.Perfect,
                JudgementRating.Meh,
            ];

            foreach (JudgementRating rating in ratings)
            {
                yokko.Apply(rating);
                etterna.Apply(rating);
            }

            Assert.Multiple(() =>
            {
                Assert.That(yokko.Combo, Is.EqualTo(4));
                Assert.That(yokko.MaxCombo, Is.EqualTo(4));
                Assert.That(yokko.ComboBreaks, Is.Zero);
                Assert.That(yokko.MissCombo, Is.Zero);
                Assert.That(etterna.Combo, Is.Zero);
                Assert.That(etterna.MaxCombo, Is.EqualTo(1));
                Assert.That(etterna.ComboBreaks, Is.EqualTo(2));
                Assert.That(etterna.MissCombo, Is.EqualTo(1));
                Assert.That(etterna.MaxMissCombo, Is.EqualTo(1));
            });
        }

        [Test]
        public void EtternaBrokenChordDoesNotRestartComboOnSameRow()
        {
            var beatmap = new YokkoBeatmap(
                "Etterna chord",
                "Yokko",
                "Yokko",
                "4K",
                KeyMode.FourKey,
                ChartSourceFormat.Etterna,
                [YokkoTimingPoint.Default],
                null,
                [
                    new YokkoHitObject(
                        0,
                        1000,
                        null,
                        HitObjectKind.Tap),
                    new YokkoHitObject(
                        1,
                        1000,
                        null,
                        HitObjectKind.Tap),
                ]);
            BeatmapJudgementState state =
                createEtternaState(beatmap);

            state.JudgeLanePress(0, 1100);
            state.JudgeLanePress(1, 1000);

            Assert.Multiple(() =>
            {
                Assert.That(state.Combo, Is.Zero);
                Assert.That(state.MaxCombo, Is.Zero);
                Assert.That(state.ComboBreaks, Is.EqualTo(1));
                Assert.That(state.MissCombo, Is.EqualTo(1));
            });
        }

        [Test]
        public void EtternaAccuracyUsesWife3AndMissWeight()
        {
            BeatmapJudgementState state = createEtternaState(
                createTapBeatmap(1000, 1500));

            state.JudgeLanePress(0, 1000);
            state.CollectExpiredMisses(1680.001);

            Assert.Multiple(() =>
            {
                Assert.That(
                    state.Accuracy,
                    Is.EqualTo(-0.875).Within(1e-12));
                Assert.That(state.Score, Is.Zero);
                Assert.That(
                    state.MaximumAchievableAccuracy,
                    Is.EqualTo(-0.875).Within(1e-12));
                Assert.That(
                    EtternaScoringRules.GradeLabel(state.Accuracy),
                    Is.EqualTo("D"));
            });
        }

        [Test]
        public void EtternaHoldUsesHeadForComboAndNoTailTimingJudgement()
        {
            BeatmapJudgementState state = createEtternaState(
                createHoldBeatmap());

            JudgementEvent head =
                state.JudgeLanePress(1, 1000).Single();
            IReadOnlyList<JudgementEvent> end =
                state.JudgeLaneRelease(1, 1500);

            Assert.Multiple(() =>
            {
                Assert.That(
                    head.Phase,
                    Is.EqualTo(JudgementPhase.HoldHead));
                Assert.That(
                    end.Select(static judgement => judgement.Phase),
                    Is.EqualTo(new[]
                    {
                        JudgementPhase.HoldTail,
                        JudgementPhase.HoldBody,
                        JudgementPhase.Hold,
                    }));
                Assert.That(
                    end.All(static judgement =>
                        judgement.Rating == JudgementRating.IgnoreHit),
                    Is.True);
                Assert.That(state.Counts.Perfect, Is.EqualTo(1));
                Assert.That(state.Combo, Is.EqualTo(1));
                Assert.That(state.Accuracy, Is.EqualTo(1));
                Assert.That(state.Score, Is.EqualTo(1_000_000));
                Assert.That(state.IsComplete, Is.True);
            });
        }

        [Test]
        public void EtternaHoldDropPenalisesWifeWithoutBreakingTapCombo()
        {
            BeatmapJudgementState state = createEtternaState(
                createHoldBeatmap());

            state.JudgeLanePress(1, 1000);
            IReadOnlyList<JudgementEvent> release =
                state.JudgeLaneRelease(1, 1200);
            IReadOnlyList<JudgementEvent> drop =
                state.CollectExpiredMisses(1450);

            Assert.Multiple(() =>
            {
                Assert.That(release, Is.Empty);
                Assert.That(
                    drop.Any(static judgement =>
                        judgement.Phase == JudgementPhase.HoldBody
                        && judgement.Rating
                        == JudgementRating.ComboBreak),
                    Is.True);
                Assert.That(state.Combo, Is.EqualTo(1));
                Assert.That(state.ComboBreaks, Is.Zero);
                Assert.That(state.MissCombo, Is.Zero);
                Assert.That(
                    state.Accuracy,
                    Is.EqualTo(-1.25).Within(1e-12));
                Assert.That(state.IsComplete, Is.True);
            });
        }

        [Test]
        public void EtternaRollDropsWhenItIsNotRetapped()
        {
            BeatmapJudgementState state = createEtternaState(
                createHoldBeatmap(
                    endTime: 2000,
                    holdType: HoldNoteType.Roll));

            state.JudgeLanePress(1, 1000);
            Assert.That(state.JudgeLaneRelease(1, 1020), Is.Empty);
            Assert.That(
                state.CollectExpiredMisses(1499.999),
                Is.Empty);
            IReadOnlyList<JudgementEvent> drop =
                state.CollectExpiredMisses(1500);

            Assert.Multiple(() =>
            {
                Assert.That(
                    drop.Single(static judgement =>
                        judgement.Phase == JudgementPhase.HoldBody)
                        .Rating,
                    Is.EqualTo(JudgementRating.ComboBreak));
                Assert.That(state.Combo, Is.EqualTo(1));
                Assert.That(state.MissCombo, Is.Zero);
                Assert.That(
                    state.Accuracy,
                    Is.EqualTo(-1.25).Within(1e-12));
                Assert.That(state.IsComplete, Is.True);
            });
        }

        [Test]
        public void EtternaRollPressesRefillLifeUntilItsEnd()
        {
            BeatmapJudgementState state = createEtternaState(
                createHoldBeatmap(
                    endTime: 2000,
                    holdType: HoldNoteType.Roll));

            state.JudgeLanePress(1, 1000);
            state.JudgeLaneRelease(1, 1020);
            Assert.That(state.JudgeLanePress(1, 1400), Is.Empty);
            state.JudgeLaneRelease(1, 1420);
            Assert.That(state.JudgeLanePress(1, 1800), Is.Empty);
            state.JudgeLaneRelease(1, 1820);
            IReadOnlyList<JudgementEvent> end =
                state.CollectExpiredMisses(2000);

            Assert.Multiple(() =>
            {
                Assert.That(
                    end.All(static judgement =>
                        judgement.Rating == JudgementRating.IgnoreHit),
                    Is.True);
                Assert.That(state.Accuracy, Is.EqualTo(1));
                Assert.That(state.Combo, Is.EqualTo(1));
                Assert.That(state.MissCombo, Is.Zero);
                Assert.That(state.IsComplete, Is.True);
            });
        }

        [Test]
        public void EtternaHoldCanBeRegrabbedBeforeLifeDrains()
        {
            BeatmapJudgementState state = createEtternaState(
                createHoldBeatmap());

            state.JudgeLanePress(1, 1000);
            Assert.That(state.JudgeLaneRelease(1, 1200), Is.Empty);
            Assert.That(state.JudgeLanePress(1, 1300), Is.Empty);
            IReadOnlyList<JudgementEvent> end =
                state.CollectExpiredMisses(1500);

            Assert.Multiple(() =>
            {
                Assert.That(
                    end.All(static judgement =>
                        judgement.Rating == JudgementRating.IgnoreHit),
                    Is.True);
                Assert.That(state.Accuracy, Is.EqualTo(1));
                Assert.That(state.Combo, Is.EqualTo(1));
                Assert.That(state.IsComplete, Is.True);
            });
        }

        [Test]
        public void EtternaMissedHoldAddsOneMissAndMissedHoldPenalty()
        {
            BeatmapJudgementState state = createEtternaState(
                createHoldBeatmap());

            state.CollectExpiredMisses(1180.001);
            IReadOnlyList<JudgementEvent> end =
                state.CollectExpiredMisses(1500);

            Assert.Multiple(() =>
            {
                Assert.That(state.Counts.Miss, Is.EqualTo(1));
                Assert.That(state.ComboBreaks, Is.EqualTo(1));
                Assert.That(
                    end.Single(static judgement =>
                        judgement.Phase == JudgementPhase.HoldBody)
                       .Rating,
                    Is.EqualTo(JudgementRating.IgnoreMiss));
                Assert.That(
                    state.Accuracy,
                    Is.EqualTo(-5).Within(1e-12));
                Assert.That(state.IsComplete, Is.True);
            });
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
        public void MinePressUsesIndependentInclusiveWindow()
        {
            var state = new BeatmapJudgementState(createMineBeatmap());

            Assert.That(
                state.JudgeLanePress(
                    0,
                    1000
                    - BeatmapJudgementState.MineWindowMilliseconds
                    - 0.01),
                Is.Empty);
            JudgementEvent mine = state.JudgeLanePress(
                0,
                1000 - BeatmapJudgementState.MineWindowMilliseconds)
                .Single();

            Assert.Multiple(() =>
            {
                Assert.That(mine.Phase, Is.EqualTo(JudgementPhase.Mine));
                Assert.That(
                    mine.Rating,
                    Is.EqualTo(JudgementRating.IgnoreMiss));
                Assert.That(state.Combo, Is.Zero);
                Assert.That(state.Accuracy, Is.EqualTo(1));
                Assert.That(state.IsComplete, Is.True);
            });
        }

        [Test]
        public void HeldLaneTriggersMineAsItCrossesJudgementLine()
        {
            var state = new BeatmapJudgementState(createMineBeatmap());

            JudgementEvent mine = state.CollectMineJudgements(
                1000,
                new[] { true, false, false, false })
                .Single();

            Assert.That(mine.Rating, Is.EqualTo(JudgementRating.IgnoreMiss));
            Assert.That(mine.HitErrorMilliseconds, Is.Zero);
            Assert.That(state.IsComplete, Is.True);
        }

        [Test]
        public void AvoidedAndDisabledMinesDoNotPenaliseScore()
        {
            var enabled = new BeatmapJudgementState(createMineBeatmap());

            Assert.That(
                enabled.CollectExpiredMisses(
                    1000
                    + BeatmapJudgementState.MineWindowMilliseconds),
                Is.Empty);
            JudgementEvent avoided = enabled.CollectExpiredMisses(
                    1000
                    + BeatmapJudgementState.MineWindowMilliseconds
                    + 0.01)
                .Single();

            var disabled = new BeatmapJudgementState(
                createMineBeatmap(),
                minesEnabled: false);

            Assert.Multiple(() =>
            {
                Assert.That(
                    avoided.Rating,
                    Is.EqualTo(JudgementRating.IgnoreHit));
                Assert.That(enabled.Combo, Is.Zero);
                Assert.That(enabled.Accuracy, Is.EqualTo(1));
                Assert.That(disabled.IsComplete, Is.True);
                Assert.That(disabled.TotalJudgementObjectCount, Is.Zero);
                Assert.That(disabled.JudgeLanePress(0, 1000), Is.Empty);
            });
        }

        [Test]
        public void EtternaMineWindowMatchesFixedUpstreamWindow()
        {
            var state = new BeatmapJudgementState(
                createMineBeatmap(),
                new JudgementWindows(
                    configuration: new JudgementConfiguration(
                        JudgementMode.Etterna,
                        9)));

            Assert.That(
                state.JudgeLanePress(
                    0,
                    1000
                    - BeatmapJudgementState
                        .EtternaMineWindowMilliseconds
                    - 0.01),
                Is.Empty);
            JudgementEvent mine = state.JudgeLanePress(
                    0,
                    1000
                    - BeatmapJudgementState
                        .EtternaMineWindowMilliseconds)
                .Single();

            Assert.That(mine.Phase, Is.EqualTo(JudgementPhase.Mine));
        }

        [Test]
        public void EtternaMineWindowIgnoresJusticeAndPreservesRealTimeAtRate()
        {
            var j4 = new BeatmapJudgementState(
                createMineBeatmap(),
                new JudgementWindows(
                    speedMultiplier: 1.5,
                    configuration: new JudgementConfiguration(
                        JudgementMode.Etterna,
                        4)));
            var justice = new BeatmapJudgementState(
                createMineBeatmap(),
                new JudgementWindows(
                    speedMultiplier: 1.5,
                    configuration: new JudgementConfiguration(
                        JudgementMode.Etterna,
                        9)));

            Assert.Multiple(() =>
            {
                Assert.That(
                    j4.ActiveMineWindowMilliseconds,
                    Is.EqualTo(112.5).Within(0.000001));
                Assert.That(
                    justice.ActiveMineWindowMilliseconds,
                    Is.EqualTo(j4.ActiveMineWindowMilliseconds));
                Assert.That(
                    justice.ActiveMineWindowMilliseconds / 1.5,
                    Is.EqualTo(
                        BeatmapJudgementState
                            .EtternaMineWindowMilliseconds));
                Assert.That(
                    justice.ActiveMineAvoidWindowMilliseconds,
                    Is.EqualTo(270).Within(0.000001));
            });
        }

        [Test]
        public void EtternaClosestObjectRoutesOnePressToOnlyOneObject()
        {
            var futureMine = createEtternaState(createLaneBeatmap(
                (1000, HitObjectKind.Tap),
                (1100, HitObjectKind.Mine)));
            JudgementEvent mine = futureMine
                .JudgeLanePress(0, 1050)
                .Single();

            var passedMine = createEtternaState(createLaneBeatmap(
                (1000, HitObjectKind.Mine),
                (1100, HitObjectKind.Tap)));
            JudgementEvent tap = passedMine
                .JudgeLanePress(0, 1050)
                .Single();

            Assert.Multiple(() =>
            {
                Assert.That(mine.HitObjectIndex, Is.EqualTo(1));
                Assert.That(mine.Phase, Is.EqualTo(JudgementPhase.Mine));
                Assert.That(futureMine.IsResolved(0), Is.False);
                Assert.That(tap.HitObjectIndex, Is.EqualTo(1));
                Assert.That(tap.Phase, Is.EqualTo(JudgementPhase.Tap));
                Assert.That(passedMine.IsResolved(0), Is.False);
            });
        }

        [Test]
        public void EtternaClosestMineOutsideItsWindowBlocksFartherTap()
        {
            var state = createEtternaState(createLaneBeatmap(
                (850, HitObjectKind.Tap),
                (1080, HitObjectKind.Mine)));

            IReadOnlyList<JudgementEvent> events =
                state.JudgeLanePress(0, 1000);

            Assert.Multiple(() =>
            {
                Assert.That(events, Is.Empty);
                Assert.That(state.IsResolved(0), Is.False);
                Assert.That(state.IsResolved(1), Is.False);
            });
        }

        [Test]
        public void EtternaHeldMineUsesCrossingTimeAndLatePressCannotBackHit()
        {
            var held = createEtternaState(createMineBeatmap());
            Assert.That(held.JudgeLanePress(0, 900), Is.Empty);

            JudgementEvent crossing = held.CollectMineJudgements(
                    1010,
                    new[] { true, false, false, false })
                .Single();

            var late = createEtternaState(createMineBeatmap());
            Assert.That(late.JudgeLanePress(0, 1010), Is.Empty);
            Assert.That(
                late.CollectMineJudgements(
                    1010,
                    new[] { true, false, false, false }),
                Is.Empty);
            Assert.That(late.CollectExpiredMisses(1180), Is.Empty);
            JudgementEvent avoided =
                late.CollectExpiredMisses(1180.001).Single();

            Assert.Multiple(() =>
            {
                Assert.That(
                    crossing.Rating,
                    Is.EqualTo(JudgementRating.IgnoreMiss));
                Assert.That(crossing.HitErrorMilliseconds, Is.Zero);
                Assert.That(
                    crossing.HitTimeMilliseconds,
                    Is.EqualTo(1000));
                Assert.That(
                    avoided.Rating,
                    Is.EqualTo(JudgementRating.IgnoreHit));
            });
        }

        [Test]
        public void EtternaReleaseBetweenUpdatesStillHitsCrossedMine()
        {
            var state = createEtternaState(createMineBeatmap());
            Assert.That(state.JudgeLanePress(0, 900), Is.Empty);

            JudgementEvent mine =
                state.JudgeLaneRelease(0, 1010).Single();

            Assert.Multiple(() =>
            {
                Assert.That(mine.Phase, Is.EqualTo(JudgementPhase.Mine));
                Assert.That(mine.HitErrorMilliseconds, Is.Zero);
                Assert.That(state.IsComplete, Is.True);
            });
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
        public void OsuStableHoldProducesOneCombinedScoreV1Judgement()
        {
            BeatmapJudgementState state = createOsuStableState(
                createHoldBeatmap());

            JudgementEvent head = state.JudgeLanePress(1, 1000).Single();
            JudgementEvent result = state.JudgeLaneRelease(1, 1500).Single();

            Assert.Multiple(() =>
            {
                Assert.That(head.Rating, Is.EqualTo(JudgementRating.IgnoreHit));
                Assert.That(result.Phase, Is.EqualTo(JudgementPhase.Hold));
                Assert.That(result.Rating, Is.EqualTo(JudgementRating.Perfect));
                Assert.That(state.Counts.Perfect, Is.EqualTo(1));
                Assert.That(state.Combo, Is.EqualTo(1));
                Assert.That(state.Score, Is.EqualTo(1_000_000));
                Assert.That(state.IsComplete, Is.True);
            });
        }

        [Test]
        public void OsuStableDroppedHoldIsCappedAtMehAfterRegrab()
        {
            BeatmapJudgementState state = createOsuStableState(
                createHoldBeatmap());

            state.JudgeLanePress(1, 1000);
            Assert.That(state.JudgeLaneRelease(1, 1200), Is.Empty);
            state.JudgeLanePress(1, 1300);
            JudgementEvent result = state.JudgeLaneRelease(1, 1500).Single();

            Assert.That(result.Rating, Is.EqualTo(JudgementRating.Meh));
            Assert.That(state.Counts.Meh, Is.EqualTo(1));
            Assert.That(state.Combo, Is.Zero);
            Assert.That(state.ComboBreaks, Is.EqualTo(1));
        }

        [Test]
        public void OsuStableHoldComboStartsAtHeadAndBreaksDuringBody()
        {
            BeatmapJudgementState state = createOsuStableState(
                createHoldBeatmap());

            state.JudgeLanePress(1, 1000);
            Assert.That(state.Combo, Is.EqualTo(1));

            state.JudgeLaneRelease(1, 1200);
            Assert.Multiple(() =>
            {
                Assert.That(state.Combo, Is.Zero);
                Assert.That(state.ComboBreaks, Is.EqualTo(1));
            });
        }

        [Test]
        public void OsuStableEarlyMissBandHoldCanRecoverToMeh()
        {
            BeatmapJudgementState state = createOsuStableState(
                createHoldBeatmap());

            JudgementEvent head = state.JudgeLanePress(1, 850).Single();
            JudgementEvent tail = state.JudgeLaneRelease(1, 1500).Single();

            Assert.Multiple(() =>
            {
                Assert.That(head.Rating,
                    Is.EqualTo(JudgementRating.IgnoreHit));
                Assert.That(tail.Rating,
                    Is.EqualTo(JudgementRating.Meh));
                Assert.That(state.Combo, Is.EqualTo(1));
            });
        }

        [Test]
        public void OsuStableHoldRoundsHeadAndTailBeforeCombiningErrors()
        {
            BeatmapJudgementState inside = createOsuStableState(
                createHoldBeatmap());
            BeatmapJudgementState outside = createOsuStableState(
                createHoldBeatmap());

            inside.JudgeLanePress(1, 1019.49);
            outside.JudgeLanePress(1, 1019.51);

            Assert.Multiple(() =>
            {
                Assert.That(
                    inside.JudgeLaneRelease(1, 1500).Single().Rating,
                    Is.EqualTo(JudgementRating.Perfect));
                Assert.That(
                    outside.JudgeLaneRelease(1, 1500).Single().Rating,
                    Is.EqualTo(JudgementRating.Great));
            });
        }

        [Test]
        public void OsuStableScoreV1TreatsMaxAndGreatAsFullAccuracy()
        {
            var processor = new ManiaScoreProcessor(
                createTapBeatmap(1000, 1500),
                judgementConfiguration:
                    JudgementConfiguration.OsuStableDefault);

            processor.Apply(JudgementRating.Perfect);
            processor.Apply(JudgementRating.Great);

            Assert.Multiple(() =>
            {
                Assert.That(processor.Counts.Perfect, Is.EqualTo(1));
                Assert.That(processor.Counts.Great, Is.EqualTo(1));
                Assert.That(processor.Accuracy, Is.EqualTo(1));
                Assert.That(processor.Combo, Is.EqualTo(2));
                Assert.That(processor.TotalScore, Is.EqualTo(984_375));
            });
        }

        [Test]
        public void OsuStableScoreV1UsesDocumentedBonusDividerAndMultiplier()
        {
            YokkoBeatmap beatmap = createTapBeatmap(1000, 1500);
            var noMods = new ManiaScoreProcessor(
                beatmap,
                judgementConfiguration:
                    JudgementConfiguration.OsuStableDefault);
            var doubleTime = new ManiaScoreProcessor(
                beatmap,
                scoreMultiplier: 1,
                judgementConfiguration:
                    JudgementConfiguration.OsuStableDefault,
                osuStableBonusPunishmentDivider: 1.1);
            var easy = new ManiaScoreProcessor(
                beatmap,
                scoreMultiplier: 0.5,
                judgementConfiguration:
                    JudgementConfiguration.OsuStableDefault);

            foreach (ManiaScoreProcessor processor in
                     new[] { noMods, doubleTime })
            {
                processor.Apply(JudgementRating.Good);
                processor.Apply(JudgementRating.Perfect);
            }
            easy.Apply(JudgementRating.Perfect);
            easy.Apply(JudgementRating.Perfect);

            Assert.Multiple(() =>
            {
                Assert.That(noMods.TotalScore, Is.EqualTo(768_530));
                Assert.That(doubleTime.TotalScore, Is.EqualTo(769_939));
                Assert.That(easy.TotalScoreWithoutMods,
                    Is.EqualTo(1_000_000));
                Assert.That(easy.TotalScore, Is.EqualTo(500_000));
            });
        }

        [Test]
        public void OsuStableScoreV1MatchesFormulaForEveryThreeHitSequence()
        {
            JudgementRating[] ratings =
            [
                JudgementRating.Perfect,
                JudgementRating.Great,
                JudgementRating.Good,
                JudgementRating.Ok,
                JudgementRating.Meh,
                JudgementRating.Miss,
            ];
            YokkoBeatmap beatmap = createTapBeatmap(1000, 1100, 1200);

            foreach (double divider in new[] { 1d, 1.08, 1.1, 1.08 * 1.1 * 1.06 })
            foreach (JudgementRating first in ratings)
            foreach (JudgementRating second in ratings)
            foreach (JudgementRating third in ratings)
            {
                JudgementRating[] sequence = [first, second, third];
                var processor = new ManiaScoreProcessor(
                    beatmap,
                    judgementConfiguration:
                        JudgementConfiguration.OsuStableDefault,
                    osuStableBonusPunishmentDivider: divider);
                foreach (JudgementRating rating in sequence)
                    processor.Apply(rating);

                (long score, double accuracy, int combo, int maxCombo,
                    ScoreRank rank) = referenceStableScore(
                        sequence,
                        divider);
                string message =
                    $"divider={divider}, sequence={string.Join(',', sequence)}";
                Assert.Multiple(() =>
                {
                    Assert.That(processor.TotalScore,
                        Is.EqualTo(score), message);
                    Assert.That(processor.Accuracy,
                        Is.EqualTo(accuracy).Within(1e-12), message);
                    Assert.That(processor.Combo,
                        Is.EqualTo(combo), message);
                    Assert.That(processor.MaxCombo,
                        Is.EqualTo(maxCombo), message);
                    Assert.That(processor.Rank,
                        Is.EqualTo(rank), message);
                });
            }
        }

        [Test]
        public void OsuStableScoreV1UsesStrictGradeThresholds()
        {
            double[] times = Enumerable.Range(0, 20)
                .Select(index => 1000d + index * 100)
                .ToArray();
            var exactlyNinetyFive = new ManiaScoreProcessor(
                createTapBeatmap(times),
                judgementConfiguration:
                    JudgementConfiguration.OsuStableDefault);
            var aboveNinetyFive = new ManiaScoreProcessor(
                createTapBeatmap(times),
                judgementConfiguration:
                    JudgementConfiguration.OsuStableDefault);

            for (int index = 0; index < 20; index++)
            {
                exactlyNinetyFive.Apply(index < 17
                    ? JudgementRating.Great
                    : JudgementRating.Good);
                aboveNinetyFive.Apply(index < 18
                    ? JudgementRating.Great
                    : JudgementRating.Good);
            }

            Assert.Multiple(() =>
            {
                Assert.That(exactlyNinetyFive.Accuracy,
                    Is.EqualTo(0.95).Within(1e-12));
                Assert.That(exactlyNinetyFive.Rank,
                    Is.EqualTo(ScoreRank.A));
                Assert.That(aboveNinetyFive.Rank,
                    Is.EqualTo(ScoreRank.S));
            });
        }

        [Test]
        public void OsuStableScoreV1ModTableMatchesStableValues()
        {
            YokkoBeatmap beatmap = createTapBeatmap(1000);
            var reductions = new ManiaModSet(new[]
            {
                ManiaModId.Easy,
                ManiaModId.NoFail,
                ManiaModId.HalfTime,
            });
            var increases = new ManiaModSet(new[]
            {
                ManiaModId.HardRock,
                ManiaModId.DoubleTime,
                ManiaModId.Hidden,
            });

            OsuStableScoreV1ModMultipliers reductionMultipliers =
                OsuStableScoreV1Mods.Calculate(beatmap, reductions);
            OsuStableScoreV1ModMultipliers increaseMultipliers =
                OsuStableScoreV1Mods.Calculate(beatmap, increases);

            Assert.Multiple(() =>
            {
                Assert.That(reductionMultipliers.ScoreMultiplier,
                    Is.EqualTo(0.125));
                Assert.That(increaseMultipliers.BonusPunishmentDivider,
                    Is.EqualTo(1.08 * 1.1 * 1.06).Within(1e-12));
            });
        }

        [Test]
        public void OsuStableScoreV1UsesStableConvertKeyMultiplierTable()
        {
            YokkoBeatmap beatmap = createTapBeatmap(1000) with
            {
                ConversionSource = new ManiaConversionSource(
                    CircleSize: 4,
                    OverallDifficulty: 3,
                    ApproachRate: 5,
                    DrainRate: 5,
                    HitObjects: []),
            };
            var oneKey = new ManiaModSet(new[] { ManiaModId.Key1 });

            OsuStableScoreV1ModMultipliers multipliers =
                OsuStableScoreV1Mods.Calculate(beatmap, oneKey);

            Assert.That(multipliers.ScoreMultiplier,
                Is.EqualTo(0.78).Within(1e-12));
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
                Assert.That(events[0].HitTimeMilliseconds, Is.Null);
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
        public void QuaverHoldReleasePromotesOkayAndStopsBeforeMissWindow()
        {
            var windows = new JudgementWindows(
                configuration: JudgementConfiguration.QuaverDefault);
            var promotedState = new BeatmapJudgementState(
                createHoldBeatmap(),
                windows);
            promotedState.JudgeLanePress(1, 1000);

            JudgementEvent promoted = promotedState.JudgeLaneRelease(
                1,
                1500 + windows.MehMilliseconds * 1.5).First();

            var outsideState = new BeatmapJudgementState(
                createHoldBeatmap(),
                windows);
            outsideState.JudgeLanePress(1, 1000);
            IReadOnlyList<JudgementEvent> outside =
                outsideState.JudgeLaneRelease(
                    1,
                    1500 + windows.MehMilliseconds * 1.5 + 0.01);

            Assert.Multiple(() =>
            {
                Assert.That(
                    promoted.Rating,
                    Is.EqualTo(JudgementRating.Ok));
                Assert.That(
                    outside.Select(static judgement => judgement.Phase),
                    Is.EqualTo(new[] { JudgementPhase.HoldBody }));
                Assert.That(
                    outside.Single().Rating,
                    Is.EqualTo(JudgementRating.ComboBreak));
            });
        }

        [Test]
        public void QuaverScoreUsesUpstreamWeightsAndMultiplierCurve()
        {
            var beatmap = new YokkoBeatmap(
                "Quaver score",
                "Yokko",
                "Yokko",
                "4K",
                KeyMode.FourKey,
                ChartSourceFormat.Quaver,
                [YokkoTimingPoint.Default],
                null,
                [
                    new YokkoHitObject(0, 1000, null, HitObjectKind.Tap),
                    new YokkoHitObject(1, 1100, null, HitObjectKind.Tap),
                ]);
            var processor = new ManiaScoreProcessor(beatmap);

            processor.Apply(JudgementRating.Great);
            long firstScore = processor.TotalScore;
            processor.Apply(JudgementRating.Perfect);

            Assert.Multiple(() =>
            {
                Assert.That(firstScore, Is.EqualTo(250_000));
                Assert.That(processor.TotalScore, Is.EqualTo(750_000));
                Assert.That(processor.Accuracy, Is.EqualTo(0.99125));
                Assert.That(processor.Combo, Is.EqualTo(2));
            });
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
        [TestCase(-15.01)]
        [TestCase(15.01)]
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

        private static (long Score, double Accuracy, int Combo,
            int MaxCombo, ScoreRank Rank) referenceStableScore(
            IReadOnlyList<JudgementRating> sequence,
            double punishmentDivider)
        {
            double bonus = 100;
            double baseScore = 0;
            double bonusScore = 0;
            double accuracyTotal = 0;
            int combo = 0;
            int maxCombo = 0;
            double perHitHalf = 500_000d / sequence.Count;

            foreach (JudgementRating rating in sequence)
            {
                (double hitValue, double bonusValue, double bonusGain,
                    double punishment, double accuracyValue) = rating switch
                {
                    JudgementRating.Perfect => (320, 32, 2, 0, 300),
                    JudgementRating.Great => (300, 32, 1, 0, 300),
                    JudgementRating.Good => (200, 16, 0, 8, 200),
                    JudgementRating.Ok => (100, 8, 0, 24, 100),
                    JudgementRating.Meh => (50, 4, 0, 44, 50),
                    JudgementRating.Miss =>
                        (0, 0, 0, double.PositiveInfinity, 0),
                    _ => throw new ArgumentOutOfRangeException(
                        nameof(sequence)),
                };
                bonus = double.IsPositiveInfinity(punishment)
                    ? 0
                    : Math.Clamp(
                        bonus + bonusGain
                        - punishment / punishmentDivider,
                        0,
                        100);
                baseScore += perHitHalf * hitValue / 320;
                bonusScore += perHitHalf
                              * bonusValue
                              * Math.Sqrt(bonus)
                              / 320;
                accuracyTotal += accuracyValue;
                combo = rating == JudgementRating.Miss ? 0 : combo + 1;
                maxCombo = Math.Max(maxCombo, combo);
            }

            double accuracy = accuracyTotal / (sequence.Count * 300);
            ScoreRank rank = accuracy switch
            {
                1 => ScoreRank.X,
                > 0.95 => ScoreRank.S,
                > 0.90 => ScoreRank.A,
                > 0.80 => ScoreRank.B,
                > 0.70 => ScoreRank.C,
                _ => ScoreRank.D,
            };
            return (
                (long)Math.Round(baseScore + bonusScore),
                accuracy,
                combo,
                maxCombo,
                rank);
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
            double endTime = 1500,
            HoldNoteType holdType = HoldNoteType.Standard)
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
                    HitObjectKind.Hold,
                    HoldType: holdType)],
                8);

        private static YokkoBeatmap createMineBeatmap()
            => createLaneBeatmap((1000, HitObjectKind.Mine));

        private static BeatmapJudgementState createEtternaState(
            YokkoBeatmap beatmap)
            => new(
                beatmap,
                new JudgementWindows(
                    configuration: new JudgementConfiguration(
                        JudgementMode.Etterna,
                        4)));

        private static BeatmapJudgementState createOsuStableState(
            YokkoBeatmap beatmap)
            => new(
                beatmap,
                new JudgementWindows(
                    beatmap.OverallDifficulty,
                    configuration:
                        JudgementConfiguration.OsuStableDefault));

        private static BeatmapJudgementState createBmsState(
            YokkoBeatmap beatmap)
            => new(
                beatmap,
                new JudgementWindows(
                    configuration:
                        JudgementConfiguration.BmsBeatorajaDefault,
                    bmsJudgeWindowMultiplier:
                        beatmap.BmsJudgement?.WindowMultiplier
                        ?? BmsJudgementMetadata.Default.WindowMultiplier,
                    bmsRegularKeysPerStage:
                        beatmap.RegularLaneCount / beatmap.StageCount == 5
                            ? 5
                            : 7));

        private static YokkoBeatmap createBmsTapBeatmap(
            BmsJudgementMetadata metadata,
            int? scratchLane = null)
            => new(
                "BMS tap test",
                "Yokko",
                "Yokko",
                "7K",
                scratchLane.HasValue
                    ? KeyMode.EightKey
                    : KeyMode.SevenKey,
                ChartSourceFormat.Bms,
                [YokkoTimingPoint.Default],
                null,
                [new YokkoHitObject(0, 1000, null, HitObjectKind.Tap)],
                ScratchLane: scratchLane,
                BmsJudgement: metadata);

        private static YokkoBeatmap createBmsHoldBeatmap(
            BmsJudgementMetadata metadata)
            => new(
                "BMS LN test",
                "Yokko",
                "Yokko",
                "7K",
                KeyMode.SevenKey,
                ChartSourceFormat.Bms,
                [YokkoTimingPoint.Default],
                null,
                [new YokkoHitObject(
                    0,
                    1000,
                    1500,
                    HitObjectKind.Hold)],
                BmsJudgement: metadata);

        private static YokkoBeatmap createLaneBeatmap(
            params (double Time, HitObjectKind Kind)[] objects)
            => new(
                "Lane test",
                "Yokko",
                "Yokko",
                "4K",
                KeyMode.FourKey,
                ChartSourceFormat.Etterna,
                [YokkoTimingPoint.Default],
                null,
                objects.Select(item =>
                    new YokkoHitObject(
                        0,
                        item.Time,
                        null,
                        item.Kind)).ToArray(),
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
