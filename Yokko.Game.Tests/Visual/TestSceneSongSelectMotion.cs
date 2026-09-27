using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osuTK;
using Yokko.Game.Presentation;
using Yokko.Game.Screens.SongSelect;

namespace Yokko.Game.Tests.Visual;

[TestFixture]
public partial class TestSceneSongSelectMotion : YokkoTestScene
{
    [Resolved]
    private YokkoAccessibilitySettings accessibility { get; set; }

    private SongSelectBackground background;
    private readonly List<string> decoded = [];
    private bool coverageLost;

    [SetUpSteps]
    public void SetUpSteps()
    {
        AddStep("reset motion scene", () =>
        {
            Clear();
            decoded.Clear();
            coverageLost = false;
            accessibility.ReduceMotion.Value = false;
            Add(background = new SongSelectBackground("A", null, () => accessibility.ReduceMotion.Value));
            background.OnUpdate += _ => coverageLost |= background.CoverageAlpha < 0.999f;
        });
        AddUntilStep("background loaded", () => background.IsLoaded);
    }

    [Test]
    public void RapidRequestsKeepCoverageAndDecodeOnlyLatestPendingArtwork()
    {
        AddStep("request three songs in one burst", () =>
        {
            request("B");
            request("C");
            request("D");
            Assert.That(decoded, Is.EqualTo(new[] { "B" }));
        });
        AddUntilStep("latest song settles", () => background.DisplayedKey == "D" && !background.IsTransitioning);
        AddAssert("intermediate artwork never decoded", () => decoded.SequenceEqual(new[] { "B", "D" }));
        AddAssert("no frame exposes the stage", () => !coverageLost);
    }

    [Test]
    public void ReturningToCurrentArtworkDuringDissolveRetainsLatestSelection()
    {
        AddStep("reverse during transition", () => { request("B"); request("A"); });
        AddUntilStep("original artwork restored", () => background.DisplayedKey == "A" && !background.IsTransitioning);
        AddAssert("coverage survives reversal", () => !coverageLost);
        AddStep("same artwork does not restart", () => request("A"));
        AddAssert("same path is not decoded again", () => decoded.SequenceEqual(new[] { "B", "A" }));
    }

    [Test]
    public void ReducedMotionCancelsAnInFlightDissolve()
    {
        AddStep("replace while enabling reduced motion", () =>
        {
            request("B");
            accessibility.ReduceMotion.Value = true;
            request("C");
            Assert.That(background.DisplayedKey, Is.EqualTo("C"));
            Assert.That(background.IsTransitioning, Is.False);
        });
        AddWaitStep("allow old completion callback", 4);
        AddAssert("stale callback cannot replace current song", () => background.DisplayedKey == "C" && !coverageLost);
        AddStep("restore motion", () => accessibility.ReduceMotion.Value = false);
    }

    [Test]
    public void PopoverCloseStopsInputAndReopenCancelsExit()
    {
        SongSelectPopover popover = null;
        AddStep("add popover", () => Add(popover = new SongSelectPopover
        {
            Size = new Vector2(250, 160),
            Alpha = 0,
        }));
        AddUntilStep("popover loaded", () => popover.IsLoaded);
        AddStep("show popover", () => popover.Open());
        AddUntilStep("popover visible", () => popover.Alpha > 0);
        AddStep("close then reopen", () =>
        {
            popover.Close();
            Assert.That(popover.PropagatePositionalInputSubTree, Is.False);
            Assert.That(popover.PropagateNonPositionalInputSubTree, Is.False);
            popover.Open();
        });
        AddUntilStep("reopened at rest", () => popover.Alpha == 1 && popover.Scale == Vector2.One);
        AddAssert("input is restored", () => popover.PropagatePositionalInputSubTree && popover.IsOpen);
    }

    private void request(string key) => background.Request(key, () =>
    {
        decoded.Add(key);
        return null;
    });
}
