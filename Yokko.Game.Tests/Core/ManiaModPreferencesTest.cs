using System;
using System.IO;
using System.Text.Json;
using NUnit.Framework;
using osu.Framework.Platform;
using Yokko.Core.Mods;
using Yokko.Game.Configuration;
using Yokko.Game.Gameplay;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class ManiaModPreferencesTest
{
    [Test]
    public void MutuallyExclusiveModSettingsAreRememberedIndependently()
    {
        var preferences = new YokkoManiaModPreferences();
        preferences.Remember(
            ManiaModSet.Empty.WithCover(
                0.66,
                ManiaCoverDirection.AgainstScroll));
        preferences.Remember(
            ManiaModSet.Empty.WithFlashlight(1.8, true));
        preferences.Remember(
            ManiaModSet.Empty.WithTimeRamp(
                ManiaModId.WindUp,
                0.7,
                1.9,
                false));
        preferences.Remember(
            ManiaModSet.Empty.WithAdaptiveSpeed(1.4, false));

        ManiaModSet cover = preferences.Apply(
            ManiaModSet.Empty.With(ManiaModId.Cover, true),
            ManiaModId.Cover);
        ManiaModSet flashlight = preferences.Apply(
            ManiaModSet.Empty.With(ManiaModId.Flashlight, true),
            ManiaModId.Flashlight);
        ManiaModSet windUp = preferences.Apply(
            ManiaModSet.Empty.With(ManiaModId.WindUp, true),
            ManiaModId.WindUp);
        ManiaModSet adaptive = preferences.Apply(
            ManiaModSet.Empty.With(ManiaModId.AdaptiveSpeed, true),
            ManiaModId.AdaptiveSpeed);

        Assert.Multiple(() =>
        {
            Assert.That(cover.CoverCoverage, Is.EqualTo(0.66));
            Assert.That(
                cover.CoverDirection,
                Is.EqualTo(ManiaCoverDirection.AgainstScroll));
            Assert.That(
                flashlight.FlashlightSizeMultiplier,
                Is.EqualTo(1.8));
            Assert.That(flashlight.FlashlightComboBasedSize, Is.True);
            Assert.That(windUp.TimeRampInitialRate, Is.EqualTo(0.7));
            Assert.That(windUp.TimeRampFinalRate, Is.EqualTo(1.9));
            Assert.That(windUp.TimeRampAdjustPitch, Is.False);
            Assert.That(adaptive.AdaptiveInitialRate, Is.EqualTo(1.4));
            Assert.That(adaptive.AdaptiveAdjustPitch, Is.False);
        });
    }

    [Test]
    public void PreferencesPersistAcrossConfigInstances()
    {
        string directory = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            "mania-mod-preferences",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            var first = new YokkoManiaModPreferences();
            using (var config =
                   new YokkoConfigManager(new NativeStorage(directory)))
            {
                config.BindModPreferences(first);
                first.Remember(
                    ManiaModSet.Empty.WithFixedRate(
                        ManiaModId.DoubleTime,
                        1.67,
                        true));
                first.Remember(
                    ManiaModSet.Empty.WithMuted(
                        true,
                        false,
                        321,
                        false));
                Assert.That(config.Save(), Is.True);
            }

            var restored = new YokkoManiaModPreferences();
            using (var config =
                   new YokkoConfigManager(new NativeStorage(directory)))
            {
                config.BindModPreferences(restored);
                ManiaModSet doubleTime = restored.Apply(
                    ManiaModSet.Empty.With(
                        ManiaModId.DoubleTime,
                        true),
                    ManiaModId.DoubleTime);
                ManiaModSet muted = restored.Apply(
                    ManiaModSet.Empty.With(
                        ManiaModId.Muted,
                        true),
                    ManiaModId.Muted);

                Assert.Multiple(() =>
                {
                    Assert.That(
                        doubleTime.FixedRateSpeedChange,
                        Is.EqualTo(1.5));
                    Assert.That(
                        doubleTime.FixedRateAdjustPitch,
                        Is.False);
                    Assert.That(muted.MutedInverse, Is.True);
                    Assert.That(muted.MutedMetronome, Is.False);
                    Assert.That(muted.MutedComboCount, Is.EqualTo(321));
                    Assert.That(
                        muted.MutedAffectsHitSounds,
                        Is.False);
                });
            }
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void ActiveModsPersistUntilUserDisablesThem()
    {
        string directory = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            "mania-active-mods",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            using (var config =
                   new YokkoConfigManager(new NativeStorage(directory)))
            {
                var first = new YokkoManiaModPreferences();
                config.BindModPreferences(first);
                first.RememberActiveMods(
                    ManiaModSet.Empty
                        .WithFixedRate(
                            ManiaModId.HalfTime,
                            0.82,
                            true)
                        .With(ManiaModId.Hidden, true));
                Assert.That(config.Save(), Is.True);
            }

            using (var config =
                   new YokkoConfigManager(new NativeStorage(directory)))
            {
                var restored = new YokkoManiaModPreferences();
                config.BindModPreferences(restored);
                ManiaModSet active = restored.RestoreActiveMods();

                Assert.Multiple(() =>
                {
                    Assert.That(
                        active.Contains(ManiaModId.HalfTime),
                        Is.True);
                    Assert.That(
                        active.Contains(ManiaModId.Hidden),
                        Is.True);
                    Assert.That(
                        active.FixedRateSpeedChange,
                        Is.EqualTo(0.82));
                    Assert.That(active.FixedRateAdjustPitch, Is.True);
                });

                restored.RememberActiveMods(
                    active.With(ManiaModId.HalfTime, false));
                Assert.That(config.Save(), Is.True);
            }

            using (var config =
                   new YokkoConfigManager(new NativeStorage(directory)))
            {
                var restored = new YokkoManiaModPreferences();
                config.BindModPreferences(restored);
                ManiaModSet active = restored.RestoreActiveMods();

                Assert.Multiple(() =>
                {
                    Assert.That(
                        active.Contains(ManiaModId.HalfTime),
                        Is.False);
                    Assert.That(
                        active.Contains(ManiaModId.Hidden),
                        Is.True);
                });
            }
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [TestCase(ManiaModId.Autoplay)]
    [TestCase(ManiaModId.Cinema)]
    [TestCase(ManiaModId.SuddenDeath)]
    [TestCase(ManiaModId.Perfect)]
    [TestCase(ManiaModId.AccuracyChallenge)]
    [TestCase(ManiaModId.ScoreV2)]
    public void UnsafeActiveModIsNotPersisted(ManiaModId mod)
    {
        var preferences = new YokkoManiaModPreferences();

        preferences.RememberActiveMods(
            ManiaModSet.Empty.With(mod, true));

        Assert.Multiple(() =>
        {
            Assert.That(
                preferences.RestoreActiveMods(),
                Is.EqualTo(ManiaModSet.Empty));
            Assert.That(preferences.SerializedActiveMods.Value, Is.Empty);
        });
    }

    [Test]
    public void ExistingUnsafeSelectionIsSanitisedOnRestore()
    {
        var preferences = new YokkoManiaModPreferences();
        ManiaModSet stored = ManiaModSet.Empty
            .WithFixedRate(ManiaModId.HalfTime, 0.82, true)
            .With(ManiaModId.Autoplay, true);
        preferences.SerializedActiveMods.Value = JsonSerializer.Serialize(
            ManiaModConfigurationCodec.Capture(stored),
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            });

        ManiaModSet restored = preferences.RestoreActiveMods();

        Assert.Multiple(() =>
        {
            Assert.That(
                restored.Contains(ManiaModId.HalfTime),
                Is.True);
            Assert.That(
                restored.Contains(ManiaModId.Autoplay),
                Is.False);
            Assert.That(
                restored.FixedRateSpeedChange,
                Is.EqualTo(0.82));
            Assert.That(restored.FixedRateAdjustPitch, Is.True);
            Assert.That(
                preferences.SerializedActiveMods.Value,
                Does.Not.Contain("autoplay"));
        });
    }

    [Test]
    public void CorruptActiveModsFallBackToEmpty()
    {
        var preferences = new YokkoManiaModPreferences();
        preferences.SerializedActiveMods.Value =
            """{"schemaVersion":1,"mods":[{"key":"future-mod"}]}""";

        ManiaModSet active = preferences.RestoreActiveMods();

        Assert.Multiple(() =>
        {
            Assert.That(active, Is.EqualTo(ManiaModSet.Empty));
            Assert.That(preferences.SerializedActiveMods.Value, Is.Empty);
        });
    }

    [Test]
    public void CorruptPreferenceFallsBackToModDefault()
    {
        var preferences = new YokkoManiaModPreferences();
        preferences.SerializedConfiguration.Value =
            """{"schemaVersion":1,"mods":[{"key":"future-mod"}]}""";
        ManiaModSet selected =
            ManiaModSet.Empty.With(ManiaModId.Cover, true);

        ManiaModSet applied = preferences.Apply(
            selected,
            ManiaModId.Cover);

        Assert.Multiple(() =>
        {
            Assert.That(applied, Is.EqualTo(selected));
            Assert.That(
                preferences.SerializedConfiguration.Value,
                Is.Empty);
        });
    }
}
