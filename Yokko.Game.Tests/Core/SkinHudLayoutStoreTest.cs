using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Platform;
using Yokko.Game.Gameplay;
using Yokko.Game.Skinning.OsuMania;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class SkinHudLayoutStoreTest
{
    private string testRoot;

    [SetUp]
    public void SetUp()
    {
        testRoot = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            "skin-hud-layouts",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);
    }

    [Test]
    public void SwitchingSkinsRestoresIndependentProfilesAcrossInstances()
    {
        var skinSettings = new YokkoSkinSettings();
        skinSettings.SelectedSkinId.Value = "skin-a";
        var gameplaySettings = new YokkoGameplaySettings();

        using (var store = new SkinHudLayoutStore())
        {
            store.Initialise(
                new NativeStorage(testRoot),
                gameplaySettings,
                skinSettings);
            gameplaySettings.LayoutPlayfieldOffsetX.Value = 0.24;
            gameplaySettings.LayoutComboScaleX.Value = 1.35;
            gameplaySettings.LayoutComboVisible.Value = 0;
            gameplaySettings.LayoutJudgementVisible.Value = 0;
            gameplaySettings.LayoutHitEffectsVisible.Value = 0;
            gameplaySettings.LayoutJudgementLineOffsetY.Value = 0.12;
            gameplaySettings.BackgroundDim.Value = 0.4;
            store.Flush();

            skinSettings.SelectedSkinId.Value = "skin-b.osk";
            Assert.Multiple(() =>
            {
                Assert.That(gameplaySettings.LayoutPlayfieldOffsetX.Value, Is.Zero);
                Assert.That(gameplaySettings.LayoutComboScaleX.Value, Is.EqualTo(1));
                Assert.That(gameplaySettings.LayoutComboVisible.Value, Is.EqualTo(1));
                Assert.That(gameplaySettings.LayoutJudgementVisible.Value, Is.EqualTo(1));
                Assert.That(gameplaySettings.LayoutHitEffectsVisible.Value, Is.EqualTo(1));
                Assert.That(gameplaySettings.LayoutJudgementLineOffsetY.Value, Is.Zero);
                Assert.That(
                    gameplaySettings.BackgroundDim.Value,
                    Is.EqualTo(YokkoGameplaySettings.DefaultBackgroundDim));
            });

            gameplaySettings.LayoutPlayfieldOffsetX.Value = -0.31;
            gameplaySettings.LayoutComboScaleX.Value = 0.7;
            gameplaySettings.LayoutComboVisible.Value = 1;
            gameplaySettings.LayoutJudgementVisible.Value = 1;
            gameplaySettings.LayoutHitEffectsVisible.Value = 1;
            gameplaySettings.LayoutJudgementLineOffsetY.Value = -0.08;
            gameplaySettings.BackgroundDim.Value = 0.15;
            store.Flush();

            skinSettings.SelectedSkinId.Value = "skin-a";
            Assert.Multiple(() =>
            {
                Assert.That(gameplaySettings.LayoutPlayfieldOffsetX.Value, Is.EqualTo(0.24));
                Assert.That(gameplaySettings.LayoutComboScaleX.Value, Is.EqualTo(1.35));
                Assert.That(gameplaySettings.LayoutComboVisible.Value, Is.Zero);
                Assert.That(gameplaySettings.LayoutJudgementVisible.Value, Is.Zero);
                Assert.That(gameplaySettings.LayoutHitEffectsVisible.Value, Is.Zero);
                Assert.That(gameplaySettings.LayoutJudgementLineOffsetY.Value, Is.EqualTo(0.12));
                Assert.That(gameplaySettings.BackgroundDim.Value, Is.EqualTo(0.4));
            });
        }

        var restoredSkins = new YokkoSkinSettings();
        restoredSkins.SelectedSkinId.Value = "skin-b.osk";
        var restoredGameplay = new YokkoGameplaySettings();
        using var restoredStore = new SkinHudLayoutStore();
        restoredStore.Initialise(
            new NativeStorage(testRoot),
            restoredGameplay,
            restoredSkins);

        Assert.Multiple(() =>
        {
            Assert.That(restoredGameplay.LayoutPlayfieldOffsetX.Value, Is.EqualTo(-0.31));
            Assert.That(restoredGameplay.LayoutComboScaleX.Value, Is.EqualTo(0.7));
            Assert.That(restoredGameplay.LayoutComboVisible.Value, Is.EqualTo(1));
            Assert.That(restoredGameplay.LayoutJudgementVisible.Value, Is.EqualTo(1));
            Assert.That(restoredGameplay.LayoutHitEffectsVisible.Value, Is.EqualTo(1));
            Assert.That(restoredGameplay.LayoutJudgementLineOffsetY.Value, Is.EqualTo(-0.08));
            Assert.That(restoredGameplay.BackgroundDim.Value, Is.EqualTo(0.15));
            Assert.That(
                Directory.EnumerateFiles(
                    Path.Combine(testRoot, "skin-hud-layouts"),
                    "*.json").Count(),
                Is.EqualTo(2));
            Assert.That(
                Directory.EnumerateFiles(
                    Path.Combine(testRoot, "skin-hud-layouts"),
                    "*.tmp").Any(),
                Is.False);
        });
    }

    [Test]
    public void EditorCancelDoesNotPersistChangesToAnyVisitedSkin()
    {
        var skinSettings = new YokkoSkinSettings();
        skinSettings.SelectedSkinId.Value = "skin-a";
        var gameplaySettings = new YokkoGameplaySettings();

        using var store = new SkinHudLayoutStore();
        store.Initialise(
            new NativeStorage(testRoot),
            gameplaySettings,
            skinSettings);
        gameplaySettings.LayoutHudOffsetY.Value = 0.1;
        store.Flush();

        skinSettings.SelectedSkinId.Value = "skin-b";
        gameplaySettings.LayoutHudOffsetY.Value = -0.2;
        store.Flush();
        skinSettings.SelectedSkinId.Value = "skin-a";

        store.BeginEditSession();
        gameplaySettings.LayoutHudOffsetY.Value = 0.45;
        skinSettings.SelectedSkinId.Value = "skin-b";
        gameplaySettings.LayoutHudOffsetY.Value = -0.5;
        store.CancelEditSession();

        Assert.Multiple(() =>
        {
            Assert.That(skinSettings.SelectedSkinId.Value, Is.EqualTo("skin-a"));
            Assert.That(gameplaySettings.LayoutHudOffsetY.Value, Is.EqualTo(0.1));
        });

        skinSettings.SelectedSkinId.Value = "skin-b";
        Assert.That(gameplaySettings.LayoutHudOffsetY.Value, Is.EqualTo(-0.2));
    }

    [Test]
    public void DisposingDuringEditRollsBackInsteadOfSaving()
    {
        var skinSettings = new YokkoSkinSettings();
        skinSettings.SelectedSkinId.Value = "skin-a";
        var gameplaySettings = new YokkoGameplaySettings();
        var store = new SkinHudLayoutStore();
        store.Initialise(
            new NativeStorage(testRoot),
            gameplaySettings,
            skinSettings);
        gameplaySettings.LayoutHudOffsetY.Value = 0.18;
        store.Flush();

        store.BeginEditSession();
        gameplaySettings.LayoutHudOffsetY.Value = -0.47;
        store.Dispose();

        Assert.That(gameplaySettings.LayoutHudOffsetY.Value, Is.EqualTo(0.18));

        var restoredGameplay = new YokkoGameplaySettings();
        using var restoredStore = new SkinHudLayoutStore();
        restoredStore.Initialise(
            new NativeStorage(testRoot),
            restoredGameplay,
            skinSettings);
        Assert.That(restoredGameplay.LayoutHudOffsetY.Value, Is.EqualTo(0.18));
    }

    [Test]
    public void FirstProfileMigratesExistingGlobalLayoutOnlyOnce()
    {
        var skinSettings = new YokkoSkinSettings();
        skinSettings.SelectedSkinId.Value = "skin-a";
        var legacyGameplay = new YokkoGameplaySettings();
        legacyGameplay.LayoutProgressOffsetX.Value = 0.33;

        using (var store = new SkinHudLayoutStore())
        {
            store.Initialise(
                new NativeStorage(testRoot),
                legacyGameplay,
                skinSettings);
        }

        var laterGameplay = new YokkoGameplaySettings();
        laterGameplay.LayoutProgressOffsetX.Value = -0.6;
        using var restoredStore = new SkinHudLayoutStore();
        restoredStore.Initialise(
            new NativeStorage(testRoot),
            laterGameplay,
            skinSettings);

        Assert.That(laterGameplay.LayoutProgressOffsetX.Value, Is.EqualTo(0.33));
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(testRoot))
            Directory.Delete(testRoot, true);
    }
}
