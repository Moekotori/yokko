using System;
using NUnit.Framework;
using Yokko.Core.Mods;
using Yokko.Core.Scoring;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class ManiaModSetTest
{
    [Test]
    public void KeyConversionFamilyIsExclusive()
    {
        ManiaModSet mods = ManiaModSet.Empty
            .With(ManiaModId.Key4, true)
            .With(ManiaModId.Key7, true);

        Assert.That(mods.Contains(ManiaModId.Key7), Is.True);
        Assert.That(mods.Contains(ManiaModId.Key4), Is.False);
        Assert.Throws<ArgumentException>(() =>
            new ManiaModSet(
                [ManiaModId.Key4, ManiaModId.Key7]));
    }

    [Test]
    public void WindRateRampsMatchLazerProgressAndIdentity()
    {
        ManiaModSet windUp = ManiaModSet.Empty.WithTimeRamp(
            ManiaModId.WindUp,
            1,
            1.5,
            true);

        Assert.That(windUp.PlaybackRateAt(1000, 1000, 5000), Is.EqualTo(1));
        Assert.That(windUp.PlaybackRateAt(2500, 1000, 5000), Is.EqualTo(1.25));
        Assert.That(windUp.PlaybackRateAt(4000, 1000, 5000), Is.EqualTo(1.5));
        Assert.That(windUp.PlaybackRateAt(5000, 1000, 5000), Is.EqualTo(1.5));
        Assert.That(
            windUp.Fingerprint,
            Is.EqualTo("wind-up:1>1.5:pitch"));

        ManiaModSet windDown = windUp.With(
            ManiaModId.WindDown,
            true);
        Assert.That(windDown.Contains(ManiaModId.WindUp), Is.False);
        Assert.That(windDown.TimeRampInitialRate, Is.EqualTo(1));
        Assert.That(windDown.TimeRampFinalRate, Is.EqualTo(0.75));

        Assert.Throws<ArgumentException>(() =>
            new ManiaModSet(
                [ManiaModId.WindUp, ManiaModId.DoubleTime]));
    }

    [Test]
    public void OnlyLazerManiaFixedRateModsScaleHitWindows()
    {
        ManiaModSet doubleTime =
            ManiaModSet.Empty.With(ManiaModId.DoubleTime, true);
        ManiaModSet windUp = ManiaModSet.Empty.WithTimeRamp(
            ManiaModId.WindUp,
            1.2,
            1.5,
            true);
        ManiaModSet adaptive = ManiaModSet.Empty.WithAdaptiveSpeed(
            1.3,
            true);

        Assert.Multiple(() =>
        {
            Assert.That(
                doubleTime.HitWindowSpeedMultiplier,
                Is.EqualTo(1.5));
            Assert.That(
                windUp.HitWindowSpeedMultiplier,
                Is.EqualTo(1));
            Assert.That(
                adaptive.HitWindowSpeedMultiplier,
                Is.EqualTo(1));
        });
    }

    [Test]
    public void TimeRampTimelineIntegratesVariableRateForDifficulty()
    {
        double realTime = ManiaTimeRampTimeline.ToRealTime(
            4000,
            1000,
            5000,
            1,
            1.5);

        Assert.That(
            realTime,
            Is.EqualTo(1000 + Math.Log(1.5) / (0.5 / 3000))
                .Within(0.001));
        Assert.That(
            ManiaTimeRampTimeline.ToRealTime(
                5500,
                1000,
                5000,
                1,
                1.5),
            Is.EqualTo(realTime + 1000).Within(0.001));
    }

    [Test]
    public void CanonicalSetIsStableAndDeduplicated()
    {
        var mods = new ManiaModSet(
        [
            ManiaModId.Autoplay,
            ManiaModId.DoubleTime,
            ManiaModId.Autoplay,
        ]);

        Assert.Multiple(() =>
        {
            Assert.That(
                mods.Mods,
                Is.EqualTo(new[]
                {
                    ManiaModId.DoubleTime,
                    ManiaModId.Autoplay,
                }));
            Assert.That(mods.Acronyms, Is.EqualTo(new[] { "DT", "AD" }));
            Assert.That(mods.Fingerprint, Is.EqualTo("double-time+autoplay"));
            Assert.That(mods.PlaybackRate, Is.EqualTo(1.5));
            Assert.That(mods.ChangesAudioPitch, Is.False);
            Assert.That(mods.IsAutomation, Is.True);
        });
    }

    [Test]
    public void EnablingNightcoreReplacesDoubleTime()
    {
        ManiaModSet mods = ManiaModSet.Empty
                                            .With(
                                                ManiaModId.DoubleTime,
                                                true)
                                            .With(
                                                ManiaModId.Nightcore,
                                                true);

        Assert.Multiple(() =>
        {
            Assert.That(mods.Contains(ManiaModId.DoubleTime), Is.False);
            Assert.That(mods.Contains(ManiaModId.Nightcore), Is.True);
            Assert.That(mods.PlaybackRate, Is.EqualTo(1.5));
            Assert.That(mods.ChangesAudioPitch, Is.True);
        });
    }

    [Test]
    public void InvalidRateCombinationFailsClosed()
    {
        Assert.That(
            () => new ManiaModSet(
            [
                ManiaModId.DoubleTime,
                ManiaModId.Nightcore,
            ]),
            Throws.TypeOf<ArgumentException>());
    }

    [Test]
    public void HalfTimeAndDaycoreUseTheSlowRateFamily()
    {
        ManiaModSet mods = ManiaModSet.Empty
                                       .With(ManiaModId.HalfTime, true);

        Assert.Multiple(() =>
        {
            Assert.That(mods.PlaybackRate, Is.EqualTo(0.75));
            Assert.That(mods.ChangesAudioPitch, Is.False);
        });

        mods = mods.With(ManiaModId.Daycore, true);
        Assert.Multiple(() =>
        {
            Assert.That(mods.Contains(ManiaModId.HalfTime), Is.False);
            Assert.That(mods.Contains(ManiaModId.Daycore), Is.True);
            Assert.That(mods.PlaybackRate, Is.EqualTo(0.75));
            Assert.That(mods.ChangesAudioPitch, Is.True);
        });
    }

    [Test]
    public void RandomSeedIsPartOfCanonicalIdentity()
    {
        ManiaModSet first =
            ManiaModSet.Empty.WithRandomSeed(123456);
        ManiaModSet same =
            ManiaModSet.Empty.WithRandomSeed(123456);
        ManiaModSet different =
            ManiaModSet.Empty.WithRandomSeed(654321);

        Assert.Multiple(() =>
        {
            Assert.That(first.RandomSeed, Is.EqualTo(123456));
            Assert.That(first.Fingerprint, Is.EqualTo("random:123456"));
            Assert.That(first, Is.EqualTo(same));
            Assert.That(first, Is.Not.EqualTo(different));
        });
    }

    [Test]
    public void NoReleaseAndHoldOffReplaceEachOther()
    {
        ManiaModSet mods = ManiaModSet.Empty
                                       .With(ManiaModId.NoRelease, true)
                                       .With(ManiaModId.HoldOff, true);

        Assert.Multiple(() =>
        {
            Assert.That(mods.Contains(ManiaModId.HoldOff), Is.True);
            Assert.That(mods.Contains(ManiaModId.NoRelease), Is.False);
            Assert.That(
                () => new ManiaModSet(
                [
                    ManiaModId.NoRelease,
                    ManiaModId.HoldOff,
                ]),
                Throws.TypeOf<ArgumentException>());
        });
    }

    [Test]
    public void VisibilityModsUseSilverTopRanks()
    {
        var hidden = new ManiaModSet([ManiaModId.Hidden]);

        Assert.Multiple(() =>
        {
            Assert.That(
                hidden.AdjustRank(ScoreRank.X),
                Is.EqualTo(ScoreRank.XH));
            Assert.That(
                hidden.AdjustRank(ScoreRank.S),
                Is.EqualTo(ScoreRank.SH));
            Assert.That(
                hidden.AdjustRank(ScoreRank.A),
                Is.EqualTo(ScoreRank.A));
            Assert.That(
                ManiaModSet.Empty.AdjustRank(ScoreRank.X),
                Is.EqualTo(ScoreRank.X));
            Assert.That(
                ScoreRank.XH.ToDisplayLabel(),
                Is.EqualTo("SSH"));
        });
    }

    [Test]
    public void FailConditionModsReplaceEachOtherAndInvalidSetsFail()
    {
        ManiaModSet mods = ManiaModSet.Empty
                                       .With(ManiaModId.NoFail, true)
                                       .With(ManiaModId.SuddenDeath, true)
                                       .With(ManiaModId.Perfect, true);

        Assert.Multiple(() =>
        {
            Assert.That(mods.Contains(ManiaModId.NoFail), Is.False);
            Assert.That(
                mods.Contains(ManiaModId.SuddenDeath),
                Is.False);
            Assert.That(mods.Contains(ManiaModId.Perfect), Is.True);
            Assert.That(
                () => new ManiaModSet(
                [
                    ManiaModId.NoFail,
                    ManiaModId.SuddenDeath,
                ]),
                Throws.TypeOf<ArgumentException>());
        });
    }

    [Test]
    public void PerfectConfigurationMatchesLazerAndIsCanonical()
    {
        ManiaModSet defaultPerfect =
            ManiaModSet.Empty.With(ManiaModId.Perfect, true);
        ManiaModSet strictPerfect =
            defaultPerfect.WithPerfect(true);
        ManiaModSet preserved =
            strictPerfect.With(ManiaModId.Hidden, true);

        Assert.Multiple(() =>
        {
            Assert.That(
                defaultPerfect.PerfectRequirePerfectHits,
                Is.False);
            Assert.That(
                defaultPerfect.Fingerprint,
                Is.EqualTo("perfect"));
            Assert.That(
                strictPerfect.PerfectRequirePerfectHits,
                Is.True);
            Assert.That(
                strictPerfect.Fingerprint,
                Is.EqualTo("perfect:require-perfect"));
            Assert.That(
                strictPerfect.DisplayLabels,
                Is.EqualTo(new[] { "PF MAX" }));
            Assert.That(
                preserved.PerfectRequirePerfectHits,
                Is.True);
            Assert.That(strictPerfect, Is.Not.EqualTo(defaultPerfect));
        });
    }

    [Test]
    public void HardRockAndEasyReplaceEachOther()
    {
        ManiaModSet mods = ManiaModSet.Empty
                                       .With(ManiaModId.Easy, true)
                                       .With(ManiaModId.HardRock, true);

        Assert.Multiple(() =>
        {
            Assert.That(mods.Contains(ManiaModId.Easy), Is.False);
            Assert.That(mods.Contains(ManiaModId.HardRock), Is.True);
            Assert.That(
                () => new ManiaModSet(
                [
                    ManiaModId.Easy,
                    ManiaModId.HardRock,
                ]),
                Throws.TypeOf<ArgumentException>());
        });
    }

    [Test]
    public void AccuracyChallengeConfigurationIsCanonicalAndValidated()
    {
        ManiaModSet mods = ManiaModSet.Empty
                                       .With(ManiaModId.SuddenDeath, true)
                                       .WithAccuracyChallenge(
                                           0.975,
                                           ManiaAccuracyMode.Standard);

        Assert.Multiple(() =>
        {
            Assert.That(
                mods.Contains(ManiaModId.SuddenDeath),
                Is.True);
            Assert.That(
                mods.Contains(ManiaModId.AccuracyChallenge),
                Is.True);
            Assert.That(
                mods.Fingerprint,
                Is.EqualTo(
                    "sudden-death+accuracy-challenge:0.975:standard"));
            Assert.That(
                mods.DisplayLabels,
                Is.EqualTo(new[] { "SD", "AC 97.5% CURRENT" }));
            Assert.That(
                () => ManiaModSet.Empty.WithAccuracyChallenge(
                    0.5,
                    ManiaAccuracyMode.MaximumAchievable),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        });
    }

    [Test]
    public void AccuracyChallengeRemovesItsIncompatibleMods()
    {
        ManiaModSet mods = ManiaModSet.Empty
                                       .With(ManiaModId.Easy, true)
                                       .With(ManiaModId.Perfect, true)
                                       .WithAccuracyChallenge(
                                           0.9,
                                           ManiaAccuracyMode.MaximumAchievable);

        Assert.Multiple(() =>
        {
            Assert.That(mods.Contains(ManiaModId.Easy), Is.False);
            Assert.That(mods.Contains(ManiaModId.Perfect), Is.False);
            Assert.That(
                mods.Contains(ManiaModId.AccuracyChallenge),
                Is.True);
        });
    }

    [Test]
    public void DifficultyAdjustIsCanonicalAndReplacesEasyOrHardRock()
    {
        ManiaModSet mods = ManiaModSet.Empty
                                       .With(ManiaModId.HardRock, true)
                                       .WithDifficultyAdjust(
                                           7.5,
                                           12.3,
                                           true);

        Assert.Multiple(() =>
        {
            Assert.That(
                mods.Contains(ManiaModId.HardRock),
                Is.False);
            Assert.That(
                mods.Contains(ManiaModId.DifficultyAdjust),
                Is.True);
            Assert.That(
                mods.EffectiveDrainRate(5),
                Is.EqualTo(7.5));
            Assert.That(
                mods.EffectiveOverallDifficulty(5),
                Is.EqualTo(12.3));
            Assert.That(
                mods.Fingerprint,
                Is.EqualTo(
                    "difficulty-adjust:hp=7.5:od=12.3:extended"));
            Assert.That(
                mods.DisplayLabels,
                Is.EqualTo(new[] { "DA HP7.5 OD12.3" }));
        });

        ManiaModSet replaced =
            mods.With(ManiaModId.Easy, true);
        Assert.Multiple(() =>
        {
            Assert.That(
                replaced.Contains(ManiaModId.DifficultyAdjust),
                Is.False);
            Assert.That(replaced.Contains(ManiaModId.Easy), Is.True);
        });
    }

    [Test]
    public void DifficultyAdjustValidatesAndClampsNormalLimits()
    {
        ManiaModSet extended =
            ManiaModSet.Empty.WithDifficultyAdjust(11, -15, true);
        ManiaModSet normal = extended.WithDifficultyAdjust(
            extended.DifficultyAdjustDrainRate,
            extended.DifficultyAdjustOverallDifficulty,
            false);

        Assert.Multiple(() =>
        {
            Assert.That(
                normal.DifficultyAdjustDrainRate,
                Is.EqualTo(10));
            Assert.That(
                normal.DifficultyAdjustOverallDifficulty,
                Is.Zero);
            Assert.That(
                () => ManiaModSet.Empty.WithDifficultyAdjust(
                    11.1,
                    0,
                    true),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(
                () => new ManiaModSet(
                    [
                        ManiaModId.Easy,
                        ManiaModId.DifficultyAdjust,
                    ]),
                Throws.TypeOf<ArgumentException>());
        });
    }

    [Test]
    public void InvertReplacesHoldSemanticMods()
    {
        ManiaModSet mods = ManiaModSet.Empty
                                       .With(ManiaModId.NoRelease, true)
                                       .With(ManiaModId.Invert, true);

        Assert.Multiple(() =>
        {
            Assert.That(mods.Contains(ManiaModId.Invert), Is.True);
            Assert.That(
                mods.Contains(ManiaModId.NoRelease),
                Is.False);
            Assert.That(
                () => new ManiaModSet(
                [
                    ManiaModId.Invert,
                    ManiaModId.HoldOff,
                ]),
                Throws.TypeOf<ArgumentException>());
        });
    }

    [Test]
    public void CinemaReplacesAutomationAndFailConditions()
    {
        ManiaModSet cinema = ManiaModSet.Empty
                                           .With(
                                               ManiaModId.Autoplay,
                                               true)
                                           .With(
                                               ManiaModId.Perfect,
                                               true)
                                           .With(
                                               ManiaModId.Cinema,
                                               true);

        Assert.Multiple(() =>
        {
            Assert.That(cinema.IsAutomation, Is.True);
            Assert.That(cinema.IsCinema, Is.True);
            Assert.That(
                cinema.Contains(ManiaModId.Autoplay),
                Is.False);
            Assert.That(
                cinema.Contains(ManiaModId.Perfect),
                Is.False);
            Assert.That(
                cinema.With(ManiaModId.NoFail, true)
                      .Contains(ManiaModId.Cinema),
                Is.False);
        });
    }

    [Test]
    public void MutedConfigurationAndMixPolicyAreCanonical()
    {
        ManiaModSet mods = ManiaModSet.Empty.WithMuted(
            inverse: false,
            metronome: true,
            comboCount: 100,
            affectsHitSounds: true);
        ManiaMutedMix start = ManiaMutedPolicy.Resolve(mods, 0);
        ManiaMutedMix middle = ManiaMutedPolicy.Resolve(mods, 50);
        ManiaMutedMix end = ManiaMutedPolicy.Resolve(mods, 100);
        ManiaModSet inverse = mods.WithMuted(
            inverse: true,
            metronome: false,
            comboCount: 0,
            affectsHitSounds: false);

        Assert.Multiple(() =>
        {
            Assert.That(
                mods.Fingerprint,
                Is.EqualTo(
                    "muted:normal:metronome:combo=100:hitsounds"));
            Assert.That(start.MusicVolume, Is.EqualTo(1));
            Assert.That(start.MetronomeVolume, Is.Zero);
            Assert.That(middle.MusicVolume, Is.EqualTo(0.5));
            Assert.That(middle.HitSoundVolume, Is.EqualTo(0.5));
            Assert.That(end.MusicVolume, Is.Zero);
            Assert.That(end.MetronomeVolume, Is.EqualTo(1));
            Assert.That(inverse.MutedComboCount, Is.EqualTo(1));
            Assert.That(
                ManiaMutedPolicy.Resolve(inverse, 0).MusicVolume,
                Is.Zero);
            Assert.That(
                ManiaMutedPolicy.Resolve(inverse, 0).HitSoundVolume,
                Is.EqualTo(1));
        });
    }

    [Test]
    public void FixedRateConfigurationMatchesPinnedLazerContract()
    {
        ManiaModSet halfTime = ManiaModSet.Empty.WithFixedRate(
            ManiaModId.HalfTime,
            0.80,
            adjustPitch: true);
        ManiaModSet daycore = ManiaModSet.Empty.WithFixedRate(
            ManiaModId.Daycore,
            0.60);
        ManiaModSet doubleTime = ManiaModSet.Empty.WithFixedRate(
            ManiaModId.DoubleTime,
            1.25);
        ManiaModSet nightcore = ManiaModSet.Empty.WithFixedRate(
            ManiaModId.Nightcore,
            1.25);

        Assert.Multiple(() =>
        {
            Assert.That(halfTime.PlaybackRate, Is.EqualTo(0.80));
            Assert.That(
                halfTime.HitWindowSpeedMultiplier,
                Is.EqualTo(0.80));
            Assert.That(halfTime.FixedRateAdjustPitch, Is.True);
            Assert.That(halfTime.ChangesAudioPitch, Is.True);
            Assert.That(halfTime.FixedAudioFrequencyScale, Is.Null);
            Assert.That(
                halfTime.Fingerprint,
                Is.EqualTo("half-time:0.8:pitch"));

            Assert.That(daycore.PlaybackRate, Is.EqualTo(0.60));
            Assert.That(
                daycore.FixedAudioFrequencyScale,
                Is.EqualTo(0.75));
            Assert.That(
                daycore.Fingerprint,
                Is.EqualTo("daycore:0.6"));

            Assert.That(doubleTime.PlaybackRate, Is.EqualTo(1.25));
            Assert.That(doubleTime.ChangesAudioPitch, Is.False);
            Assert.That(
                doubleTime.Fingerprint,
                Is.EqualTo("double-time:1.25:tempo"));

            Assert.That(nightcore.PlaybackRate, Is.EqualTo(1.25));
            Assert.That(
                nightcore.FixedAudioFrequencyScale,
                Is.EqualTo(1.5));
            Assert.That(
                nightcore.Fingerprint,
                Is.EqualTo("nightcore:1.25"));
        });
    }

    [Test]
    public void FixedRateDefaultsStayCanonicalAndSwitchingResetsConfig()
    {
        ManiaModSet defaultHalfTime =
            ManiaModSet.Empty.With(ManiaModId.HalfTime, true);
        ManiaModSet switched = defaultHalfTime
            .WithFixedRate(ManiaModId.HalfTime, 0.80, true)
            .With(ManiaModId.Nightcore, true);

        Assert.Multiple(() =>
        {
            Assert.That(defaultHalfTime.PlaybackRate, Is.EqualTo(0.75));
            Assert.That(
                defaultHalfTime.Fingerprint,
                Is.EqualTo("half-time"));
            Assert.That(switched.PlaybackRate, Is.EqualTo(1.5));
            Assert.That(switched.FixedRateAdjustPitch, Is.False);
            Assert.That(switched.Fingerprint, Is.EqualTo("nightcore"));
        });
    }

    [Test]
    public void FixedRateConfigurationRejectsValuesOutsideLazerRange()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                () => ManiaModSet.Empty.WithFixedRate(
                    ManiaModId.HalfTime,
                    0.49),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(
                () => ManiaModSet.Empty.WithFixedRate(
                    ManiaModId.Daycore,
                    1),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(
                () => ManiaModSet.Empty.WithFixedRate(
                    ManiaModId.DoubleTime,
                    1),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(
                () => ManiaModSet.Empty.WithFixedRate(
                    ManiaModId.Nightcore,
                    2.01),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        });
    }

    [Test]
    public void NoPauseIsConfigurableAndMutuallyExclusiveWithNoFail()
    {
        ManiaModSet noPause = ManiaModSet.Empty.WithNoPause(2);
        ManiaModSet noFail = noPause.With(ManiaModId.NoFail, true);
        ManiaModSet restored = noFail.WithNoPause(0);

        Assert.Multiple(() =>
        {
            Assert.That(noPause.Contains(ManiaModId.NoPause), Is.True);
            Assert.That(noPause.NoPauseAllowedPauses, Is.EqualTo(2));
            Assert.That(noPause.Fingerprint, Is.EqualTo("no-pause:2"));
            Assert.That(noPause.DisplayLabels, Is.EqualTo(new[] { "NP 2" }));
            Assert.That(noFail.Contains(ManiaModId.NoPause), Is.False);
            Assert.That(noFail.Contains(ManiaModId.NoFail), Is.True);
            Assert.That(restored.Contains(ManiaModId.NoFail), Is.False);
            Assert.That(restored.NoPauseAllowedPauses, Is.Zero);
            Assert.That(
                () => new ManiaModSet(
                    [ManiaModId.NoFail, ManiaModId.NoPause]),
                Throws.TypeOf<ArgumentException>());
        });
    }
}
