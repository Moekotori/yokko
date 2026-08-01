using System;
using System.Linq;
using NUnit.Framework;
using Yokko.Game.Screens.SongSelect;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class SongSelectSelectionMemoryTest
{
    [Test]
    public void InitialEntryIsSelectedRandomlyWithinLibrary()
    {
        const int entryCount = 8;
        var random = new Random(20260801);

        int[] selections = Enumerable.Range(0, 16)
                                     .Select(_ =>
                                         SongSelectSelectionMemory
                                             .ChooseInitialEntryIndex(
                                                 entryCount,
                                                 random))
                                     .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(
                selections,
                Is.All.InRange(0, entryCount - 1));
            Assert.That(
                selections.Distinct().Count(),
                Is.GreaterThan(1));
        });
    }
}
