using System;
using NUnit.Framework;
using osu.Framework.Bindables;
using osuTK.Input;
using Yokko.Core.Gameplay;
using Yokko.Game.Configuration;
using Yokko.Game.Gameplay;
using Yokko.Game.Skinning.OsuMania;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public class GameplayPresetStoreTest
{
    [Test]
    public void NamedPresetRestoresSkinKeysHudAndFeedbackAndCanUndo()
    {
        var gameplay = new YokkoGameplaySettings();
        var skins = new YokkoSkinSettings();
        var serialized = new Bindable<string>("{}");
        var store = new GameplayPresetStore(gameplay, skins, serialized, _ => true);
        gameplay.ScrollSpeed.Value = 18;
        gameplay.LayoutAccuracyOffsetX.Value = 0.42;
        gameplay.ShowTimingBar.Value = false;
        gameplay.SetBinding(KeyMode.FourKey, 0, Key.A);
        skins.SelectedSkinId.Value = "skin-one";
        store.Save(0, "4K");
        gameplay.ScrollSpeed.Value = 9;
        gameplay.LayoutAccuracyOffsetX.Value = -0.2;
        gameplay.ShowTimingBar.Value = true;
        skins.SelectedSkinId.Value = "skin-two";
        var restarted = new GameplayPresetStore(gameplay, skins, serialized, _ => true);
        restarted.Load(0);
        Assert.That(gameplay.ScrollSpeed.Value, Is.EqualTo(18));
        Assert.That(gameplay.LayoutAccuracyOffsetX.Value, Is.EqualTo(0.42));
        Assert.That(gameplay.ShowTimingBar.Value, Is.False);
        Assert.That(skins.SelectedSkinId.Value, Is.EqualTo("skin-one"));
        Assert.That(gameplay.GetKeys(KeyMode.FourKey)[0], Is.EqualTo(Key.A));
        restarted.Undo();
        Assert.That(gameplay.ScrollSpeed.Value, Is.EqualTo(9));
        Assert.That(gameplay.LayoutAccuracyOffsetX.Value, Is.EqualTo(-0.2));
        Assert.That(gameplay.ShowTimingBar.Value, Is.True);
        Assert.That(skins.SelectedSkinId.Value, Is.EqualTo("skin-two"));
    }

    [Test]
    public void ImportValidatesBeforeReplacingExistingSlotOrChangingLiveSettings()
    {
        var gameplay = new YokkoGameplaySettings();
        var store = new GameplayPresetStore(gameplay, new YokkoSkinSettings(), new Bindable<string>("{}"), _ => true);
        store.Save(0, "Original");
        string valid = store.Export(0);
        string invalid = valid.Replace("\"gameplay.ScrollSpeed\": 8", "\"gameplay.ScrollSpeed\": 900");
        Assert.That(() => store.Import(0, invalid), Throws.TypeOf<FormatException>());
        Assert.That(store.Presets[0].Name, Is.EqualTo("Original"));
        Assert.That(gameplay.ScrollSpeed.Value, Is.EqualTo(8));
        store.Import(1, valid);
        Assert.That(store.Presets[1].Name, Is.EqualTo("Original"));
    }

    [Test]
    public void MissingSkinPreventsPartialApplication()
    {
        var gameplay = new YokkoGameplaySettings();
        var skins = new YokkoSkinSettings();
        skins.SelectedSkinId.Value = "missing";
        var store = new GameplayPresetStore(gameplay, skins, new Bindable<string>("{}"), _ => false);
        gameplay.ScrollSpeed.Value = 16;
        store.Save(0, "Saved");
        gameplay.ScrollSpeed.Value = 8;
        skins.SelectedSkinId.Value = "";
        Assert.That(() => store.Load(0), Throws.TypeOf<FormatException>());
        Assert.That(gameplay.ScrollSpeed.Value, Is.EqualTo(8));
        Assert.That(skins.SelectedSkinId.Value, Is.Empty);
    }
}
