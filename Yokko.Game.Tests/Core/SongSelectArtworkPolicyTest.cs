using NUnit.Framework;
using Yokko.Game.Screens.SongSelect;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public class SongSelectArtworkPolicyTest
{
    [Test]
    public void ExistingBeatmapArtworkIsNeverReplaced()
    {
        const string artwork =
            @"D:\Beatmaps\Any Song\completely-different-background.jpg";

        Assert.That(
            SongSelectArtworkPolicy.Resolve(artwork),
            Is.EqualTo(artwork));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("  ")]
    public void MissingArtworkUsesOnlyTheGenericFallback(string artwork)
    {
        Assert.That(
            SongSelectArtworkPolicy.Resolve(artwork),
            Is.EqualTo(SongSelectArtworkPolicy.FallbackTexture));
    }
}
