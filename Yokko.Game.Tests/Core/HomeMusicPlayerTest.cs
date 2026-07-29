using System;
using System.Linq;
using NUnit.Framework;
using Yokko.Game.Screens.Main;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class HomeMusicPlayerTest
{
    [Test]
    public void InitialTrackIsSelectedRandomlyWithinPlaylist()
    {
        const int trackCount = 8;
        var random = new Random(20260729);

        int[] selections = Enumerable.Range(0, 16)
                                     .Select(_ =>
                                         HomeMusicPlayer
                                             .ChooseInitialTrackIndex(
                                                 trackCount,
                                                 random))
                                     .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(
                selections,
                Is.All.InRange(0, trackCount - 1));
            Assert.That(
                selections.Distinct().Count(),
                Is.GreaterThan(1));
        });
    }
}
