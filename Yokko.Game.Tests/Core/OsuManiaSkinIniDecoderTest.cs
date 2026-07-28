using NUnit.Framework;
using osuTK.Graphics;
using Yokko.Game.Skinning.OsuMania;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class OsuManiaSkinIniDecoderTest
{
    [Test]
    public void DecodesRepeatedManiaSectionsAndPerLaneAssets()
    {
        OsuManiaSkinInfo info = OsuManiaSkinIniDecoder.Decode("""
[General]
Name: Test Skin
Author: Yokko
Version: 2.5

[Mania]
Keys: 4
ColumnWidth: 40, 42, 42, 40
ColumnSpacing: 1, 2, 3, 4
ColumnLineWidth: 2, 3
HitPosition: 388
Colour1: 10, 20, 30, 128
ColourColumnLine: 200, 210, 220, 230
KeyImage0: custom/key-left
KeyImage0D: custom/key-left-down
NoteImage0: custom/note-left
NoteImage0H: custom/hold-head
NoteImage0L: custom/hold-body
NoteImage0T: custom/hold-tail

[Mania]
Keys: 7
HitPosition: 400
""");

        Assert.That(info.Name, Is.EqualTo("Test Skin"));
        Assert.That(info.Author, Is.EqualTo("Yokko"));
        Assert.That(info.Version, Is.EqualTo("2.5"));
        Assert.That(info.ManiaConfigurations.Keys, Is.EquivalentTo(new[] { 4, 7 }));

        OsuManiaSkinConfiguration fourKey = info.GetConfiguration(4);
        Assert.That(fourKey.ColumnWidths, Is.EqualTo(new[] { 40f, 42f, 42f, 40f }));
        Assert.That(fourKey.ColumnSpacings, Is.EqualTo(new[] { 1f, 2f, 3f, 4f }));
        Assert.That(fourKey.ColumnLineWidths, Is.EqualTo(new[] { 2f, 3f, 2f, 2f }));
        Assert.That(fourKey.HitPosition, Is.EqualTo(388));
        Assert.That(fourKey.LaneColours[0], Is.EqualTo(new Color4(10, 20, 30, 128)));
        Assert.That(fourKey.ColumnLineColour, Is.EqualTo(new Color4(200, 210, 220, 230)));
        Assert.That(fourKey.KeyImages[0], Is.EqualTo("custom/key-left"));
        Assert.That(fourKey.PressedKeyImages[0], Is.EqualTo("custom/key-left-down"));
        Assert.That(fourKey.NoteImages[0], Is.EqualTo("custom/note-left"));
        Assert.That(fourKey.HoldHeadImages[0], Is.EqualTo("custom/hold-head"));
        Assert.That(fourKey.HoldBodyImages[0], Is.EqualTo("custom/hold-body"));
        Assert.That(fourKey.HoldTailImages[0], Is.EqualTo("custom/hold-tail"));
    }

    [Test]
    public void FillsMissingValuesWithOsuDefaults()
    {
        OsuManiaSkinInfo info = OsuManiaSkinIniDecoder.Decode("""
[Mania]
Keys: 4
ColumnWidth: 36
KeyImage0: left
NoteImage0H: head
""");

        OsuManiaSkinConfiguration configuration = info.GetConfiguration(4);

        Assert.That(configuration.ColumnWidths, Is.EqualTo(new[] { 36f, 30f, 30f, 30f }));
        Assert.That(configuration.KeyImages, Is.EqualTo(new[]
        {
            "left",
            "mania-key2",
            "mania-key2",
            "mania-key1",
        }));
        Assert.That(configuration.HoldTailImages[0], Is.EqualTo("head"));
        Assert.That(configuration.HitPosition, Is.EqualTo(402));
    }

    [Test]
    public void ProvidesDefaultLayoutWhenKeySectionIsMissing()
    {
        OsuManiaSkinInfo info = OsuManiaSkinIniDecoder.Decode("""
[General]
Name: Assets Only
Version: 2.7
""");

        OsuManiaSkinConfiguration configuration = info.GetConfiguration(7);

        Assert.That(configuration.Keys, Is.EqualTo(7));
        Assert.That(configuration.NoteImages, Is.EqualTo(new[]
        {
            "mania-note1",
            "mania-note2",
            "mania-note1",
            "mania-noteS",
            "mania-note1",
            "mania-note2",
            "mania-note1",
        }));
    }
}
