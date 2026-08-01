using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osuTK;
using Yokko.Core.Beatmaps;
using Yokko.Core.Difficulty;
using Yokko.Core.Scoring;
using Yokko.Game.Presentation;
using Yokko.Game.Screens.SongSelect;

namespace Yokko.Game.Tests.Visual;

[TestFixture]
public partial class TestSceneSongSelectVirtualisedList : YokkoTestScene
{
    private const int item_count = 10_000;
    private readonly SongSelectVirtualisedList list;
    private readonly SongSelectEntry[] entries;

    public TestSceneSongSelectVirtualisedList()
    {
        var beatmap = DemoBeatmaps.CreateFourKeyDemo();
        var ratings = new ManiaDifficultyRatings(
            ManiaMsdCalculator.CalculateResult(beatmap),
            ManiaStarRatingCalculator.CalculateResult(beatmap));
        entries = Enumerable.Range(0, item_count)
                            .Select(index => new SongSelectEntry(
                                beatmap,
                                string.Empty,
                                ratings.EtternaMsd,
                                ratings.RebirthStars,
                                TimeSpan.FromMinutes(2),
                                120,
                                0,
                                0,
                                [],
                                [],
                                "scale-package",
                                "Scale package",
                                true,
                                $"scale-{index}"))
                            .ToArray();

        Add(list = new SongSelectVirtualisedList(
            _ => ratings,
            _ => null,
            () => ManiaDifficultyRatingMode.EtternaMsd,
            null,
            _ => { },
            _ => { },
            _ => { })
        {
            RelativeSizeAxes = Axes.Both,
        });
    }

    [Test]
    public void TestTenThousandRowsKeepBoundedDrawableCount()
    {
        AddStep("index 10,000 lightweight rows", () => list.SetItems(
            entries.Select(entry => new SongSelectVirtualItem
            {
                Entry = entry,
                VisualHeight = 58,
            })));
        AddAssert("all logical rows indexed", () =>
            list.ItemCount == item_count);
        AddStep("reset to first row", () =>
            list.ScrollEntryIntoView(entries[0], false));
        AddUntilStep("initial viewport actualised", () =>
            list.MaterialisedDrawableCount > 0);
        AddAssert("initial drawable count is bounded", () =>
            list.MaterialisedDrawableCount <= 40);
        AddStep("jump to last row", () =>
            list.ScrollEntryIntoView(entries[^1], false));
        AddUntilStep("last row actualised", () =>
            list.MaterialisedRows.Any(row =>
                ReferenceEquals(row.Entry, entries[^1])));
        AddAssert("far jump remains bounded", () =>
            list.MaterialisedDrawableCount <= 40);
    }

    [Test]
    public void TestAnimatedRebuildKeepsMaterialisationBounded()
    {
        AddStep("index initial rows", () => list.SetItems(
            entries.Take(6000).Select(entry => new SongSelectVirtualItem
            {
                Entry = entry,
                VisualHeight = 58,
            })));
        AddUntilStep("initial rows actualised", () =>
            list.MaterialisedDrawableCount > 0);
        AddStep("animate smaller rebuild", () => list.SetItems(
            entries.Take(3000).Select(entry => new SongSelectVirtualItem
            {
                Entry = entry,
                VisualHeight = 58,
            }),
            animateLayout: true,
            transitionPackageId: "scale-package"));
        AddUntilStep("animated rows actualised", () =>
            list.MaterialisedDrawableCount > 0);
        AddAssert("animated drawable count remains bounded", () =>
            list.MaterialisedDrawableCount <= 40);
    }

    [Test]
    public void TestScrollAffordancesTrackRemainingContent()
    {
        AddStep("index a long browser", () => list.SetItems(
            entries.Take(200).Select(entry => new SongSelectVirtualItem
            {
                Entry = entry,
                VisualHeight = 58,
            })));
        AddStep("return to first row", () =>
            list.ScrollEntryIntoView(entries[0], false));
        AddUntilStep("only lower continuation is visible", () =>
            list.TopScrollHintAlpha < 0.01f
            && list.BottomScrollHintAlpha > 0.9f
            && list.ScrollIndicatorAlpha > 0
            && list.ScrollIndicatorProgress < 0.01f);
        AddStep("jump to last row", () =>
            list.ScrollEntryIntoView(entries[199], false));
        AddUntilStep("only upper continuation is visible", () =>
            list.TopScrollHintAlpha > 0.9f
            && list.BottomScrollHintAlpha < 0.01f
            && list.ScrollIndicatorProgress > 0.99f);
    }

    [Test]
    public void TestSelectionTransfersFocusShadowWithoutRebuild()
    {
        AddStep("index two rows", () =>
        {
            list.UpdateSelection(null);
            list.SetItems(entries.Take(2).Select(entry =>
                new SongSelectVirtualItem
                {
                    Entry = entry,
                    VisualHeight = 58,
                }));
        });
        AddUntilStep("both rows actualised", () =>
            list.MaterialisedRows.Count() == 2);
        AddStep("select first row", () =>
            list.UpdateSelection(entries[0]));
        AddUntilStep("first row gains focus depth", () =>
            list.MaterialisedRows.Single(row =>
                    ReferenceEquals(row.Entry, entries[0]))
                .FocusShadowAlpha > 0.99f
            && Math.Abs(list.MaterialisedRows.Single(row =>
                    ReferenceEquals(row.Entry, entries[0])).X - 11) < 0.05f
            && list.MaterialisedRows.Single(row =>
                    ReferenceEquals(row.Entry, entries[1]))
                .FocusShadowAlpha < 0.01f
            && Math.Abs(list.MaterialisedRows.Single(row =>
                    ReferenceEquals(row.Entry, entries[1])).X - 14) < 0.05f);
        AddStep("select second row", () =>
            list.UpdateSelection(entries[1]));
        AddUntilStep("focus depth transfers in place", () =>
            list.MaterialisedRows.Single(row =>
                    ReferenceEquals(row.Entry, entries[0]))
                .FocusShadowAlpha < 0.01f
            && list.MaterialisedRows.Single(row =>
                    ReferenceEquals(row.Entry, entries[1]))
                .FocusShadowAlpha > 0.99f
            && Math.Abs(list.MaterialisedRows.Single(row =>
                    ReferenceEquals(row.Entry, entries[0])).X - 14) < 0.05f
            && Math.Abs(list.MaterialisedRows.Single(row =>
                    ReferenceEquals(row.Entry, entries[1])).X - 11) < 0.05f
            && list.ItemCount == 2);
    }

    [Test]
    public void TestSelectionBuildsBoundedPackageProximityCurve()
    {
        AddStep("index five package rows", () =>
        {
            list.UpdateSelection(null);
            list.SetItems(entries.Take(5).Select(entry =>
                new SongSelectVirtualItem
                {
                    Entry = entry,
                    VisualHeight = 58,
                }));
        });
        AddUntilStep("all curve rows actualised", () =>
            list.MaterialisedRows.Count() == 5);
        AddStep("select middle package row", () =>
            list.UpdateSelection(entries[2]));
        AddUntilStep("neighbours form a symmetric curve", () =>
        {
            SongSelectSongRow[] rows = entries.Take(5)
                                               .Select(entry =>
                                                   list.MaterialisedRows.Single(
                                                       row => ReferenceEquals(
                                                           row.Entry,
                                                           entry)))
                                               .ToArray();
            return Math.Abs(rows[0].X - 18) < 0.05f
                   && Math.Abs(rows[1].X - 14) < 0.05f
                   && Math.Abs(rows[2].X - 11) < 0.05f
                   && Math.Abs(rows[3].X - 14) < 0.05f
                   && Math.Abs(rows[4].X - 18) < 0.05f
                   && rows[0].SelectionIndent == 4
                   && rows[2].SelectionIndent == 0
                   && Math.Abs(rows[0].X + rows[0].Width - 850) < 0.05f;
        });
        AddStep("select final package row", () =>
            list.UpdateSelection(entries[4]));
        AddUntilStep("curve transfers without rebuilding", () =>
        {
            SongSelectSongRow[] rows = entries.Take(5)
                                               .Select(entry =>
                                                   list.MaterialisedRows.Single(
                                                       row => ReferenceEquals(
                                                           row.Entry,
                                                           entry)))
                                               .ToArray();
            return Math.Abs(rows[0].X - 26) < 0.05f
                   && Math.Abs(rows[1].X - 22) < 0.05f
                   && Math.Abs(rows[2].X - 18) < 0.05f
                   && Math.Abs(rows[3].X - 14) < 0.05f
                   && Math.Abs(rows[4].X - 11) < 0.05f
                   && rows[0].SelectionIndent == 12
                   && list.ItemCount == 5;
        });
    }

    [Test]
    public void TestPackageSectionsReceiveExtraBreathingRoom()
    {
        AddStep("index two package sections", () => list.SetItems(
        [
            new SongSelectVirtualItem
            {
                HeaderEntry = entries[0],
                PackageId = "section-one",
                PackageName = "Section One",
                SongCount = 1,
                ChartCount = 1,
                VisualHeight = SongSelectPackageHeader.ExpandedHeight,
            },
            new SongSelectVirtualItem
            {
                Entry = entries[0],
                VisualHeight = SongSelectSongRow.CompactHeight,
            },
            new SongSelectVirtualItem
            {
                HeaderEntry = entries[1],
                PackageId = "section-two",
                PackageName = "Section Two",
                SongCount = 1,
                ChartCount = 1,
                VisualHeight = SongSelectPackageHeader.ExpandedHeight,
                SectionSpacingBefore = 8,
            },
        ]));
        AddUntilStep("all section rows actualised", () =>
            list.MaterialisedHeaders.Count() == 2
            && list.MaterialisedRows.Count() == 1);
        AddAssert("section gap exceeds in-package gap", () =>
        {
            SongSelectPackageHeader[] headers = list.MaterialisedHeaders
                .OrderBy(header => header.Y)
                .ToArray();
            SongSelectSongRow row = list.MaterialisedRows.Single();
            float inPackageGap = row.Y
                                 - (headers[0].Y + headers[0].Height);
            float sectionGap = headers[1].Y
                               - (row.Y + row.Height);
            return Math.Abs(inPackageGap - 5) < 0.05f
                   && Math.Abs(sectionGap - 13) < 0.05f;
        });
    }

    [Test]
    public void TestCompactRatingLivesInTrailingMetadata()
    {
        AddStep("index one package row", () => list.SetItems(
        [
            new SongSelectVirtualItem
            {
                Entry = entries[0],
                VisualHeight = 58,
            },
        ]));
        AddUntilStep("compact row actualised", () =>
            list.MaterialisedRows.Count() == 1);
        AddAssert("rating is a transparent trailing readout", () =>
        {
            SongSelectInlineDifficultyRating rating = list
                .MaterialisedRows.Single()
                .ChildrenOfType<SongSelectInlineDifficultyRating>()
                .Single();
            return Math.Abs(rating.X - 628) < 0.05f
                   && Math.Abs(rating.Y - 12) < 0.05f
                   && Math.Abs(rating.Width - 64) < 0.05f
                   && Math.Abs(rating.Height - 20) < 0.05f
                   && Math.Abs(list.MaterialisedRows.Single().Height
                               - SongSelectSongRow.CompactHeight) < 0.05f
                   && Math.Abs(rating.BorderThickness) < 0.01f
                   && rating.UnitText.Text.ToString()
                   == ManiaDifficultyPresentation.Unit(
                       ManiaDifficultyRatingMode.EtternaMsd);
        });
        AddStep("switch rating mode in place", () =>
        {
            SongSelectSongRow row = list.MaterialisedRows.Single();
            row.SetDifficulty(
                row.DisplayedDifficultyRatings,
                ManiaDifficultyRatingMode.RebirthStars);
        });
        AddUntilStep("inline readout updates without rebuilding", () =>
        {
            SongSelectInlineDifficultyRating rating = list
                .MaterialisedRows.Single()
                .ChildrenOfType<SongSelectInlineDifficultyRating>()
                .Single();
            return rating.UnitText.Text.ToString()
                       == ManiaDifficultyPresentation.Unit(
                           ManiaDifficultyRatingMode.RebirthStars)
                   && rating.ValueText.Text.ToString()
                       == ManiaDifficultyPresentation.FormatValue(
                           list.MaterialisedRows.Single()
                               .DisplayedDifficultyRatings,
                           ManiaDifficultyRatingMode.RebirthStars)
                   && list.ItemCount == 1;
        });
    }

    [Test]
    public void TestCompactRowsUseQuietRoundedHierarchy()
    {
        AddStep("index one compact package row", () => list.SetItems(
        [
            new SongSelectVirtualItem
            {
                Entry = entries[0],
                VisualHeight = SongSelectSongRow.CompactHeight,
            },
        ]));
        AddUntilStep("compact row actualised", () =>
            list.MaterialisedRows.Count() == 1);
        AddAssert("difficulty accent is a quiet semantic marker", () =>
        {
            SongSelectSongRow row = list.MaterialisedRows.Single();
            return Math.Abs(row.LeadingAccentWidth
                            - SongSelectSongRow
                                .CompactLeadingAccentWidth) < 0.05f
                   && Math.Abs(SongSelectSongRow
                                   .CompactLeadingAccentOpacity
                               - 0.48f) < 0.001f;
        });
        AddStep("select compact row", () =>
            list.UpdateSelection(entries[0]));
        AddUntilStep("selected outline stays refined", () =>
            Math.Abs(list.MaterialisedRows.Single()
                         .SelectionOutlineThickness
                     - SongSelectSongRow
                         .CompactSelectionOutlineThickness) < 0.05f
            && Math.Abs(SongSelectSongRow
                            .CompactSelectedFillOpacity
                        - 0.18f) < 0.001f);
    }

    [Test]
    public void TestCompactRowLeadsWithDifficultyInsteadOfRepeatedSongTitle()
    {
        AddStep("index one package row", () => list.SetItems(
        [
            new SongSelectVirtualItem
            {
                Entry = entries[0],
                VisualHeight = 58,
            },
        ]));
        AddUntilStep("compact row actualised", () =>
            list.MaterialisedRows.Count() == 1);
        AddAssert("difficulty is primary and mapper is secondary", () =>
        {
            SongSelectSongRow row = list.MaterialisedRows.Single();
            return row.CompactPrimaryText
                       == entries[0].Beatmap.DifficultyName
                   && row.CompactPrimaryText
                       != entries[0].Beatmap.Title
                   && row.CompactSecondaryText
                       == $"mapped by {entries[0].Beatmap.Creator}";
        });
    }

    [Test]
    public void TestCompactModePillProgressivelyDisclosesSelection()
    {
        AddStep("index two package rows", () =>
        {
            list.UpdateSelection(null);
            list.SetItems(entries.Take(2).Select(entry =>
                new SongSelectVirtualItem
                {
                    Entry = entry,
                    VisualHeight = 58,
                }));
        });
        AddUntilStep("both compact rows actualised", () =>
            list.MaterialisedRows.Count() == 2);
        AddAssert("resting rows show quiet key-mode chips", () =>
            list.MaterialisedRows.All(row =>
                Math.Abs(row.ModePillWidth - 54) < 0.05f
                && Math.Abs(row.ModePillX - 780) < 0.05f
                && row.CompactModeTextAlpha > 0.99f
                && row.ExpandedModeTextAlpha < 0.01f));
        AddStep("select first package row", () =>
            list.UpdateSelection(entries[0]));
        AddUntilStep("selected row expands full difficulty chip", () =>
        {
            SongSelectSongRow selected = list.MaterialisedRows.Single(row =>
                ReferenceEquals(row.Entry, entries[0]));
            SongSelectSongRow resting = list.MaterialisedRows.Single(row =>
                ReferenceEquals(row.Entry, entries[1]));
            return Math.Abs(selected.ModePillWidth - 116) < 0.05f
                   && Math.Abs(selected.ModePillX - 718) < 0.05f
                   && selected.CompactModeTextAlpha < 0.01f
                   && selected.ExpandedModeTextAlpha > 0.99f
                   && Math.Abs(resting.ModePillWidth - 54) < 0.05f
                   && resting.CompactModeTextAlpha > 0.99f;
        });
        AddStep("transfer selection to second row", () =>
            list.UpdateSelection(entries[1]));
        AddUntilStep("disclosure transfers without rebuilding", () =>
        {
            SongSelectSongRow previous = list.MaterialisedRows.Single(row =>
                ReferenceEquals(row.Entry, entries[0]));
            SongSelectSongRow selected = list.MaterialisedRows.Single(row =>
                ReferenceEquals(row.Entry, entries[1]));
            return Math.Abs(previous.ModePillWidth - 54) < 0.05f
                   && Math.Abs(previous.ModePillX - 780) < 0.05f
                   && previous.CompactModeTextAlpha > 0.99f
                   && Math.Abs(selected.ModePillWidth - 116) < 0.05f
                   && selected.ExpandedModeTextAlpha > 0.99f
                   && list.ItemCount == 2;
        });
    }

    [Test]
    public void TestArtworkCoverSizePreservesSourceAspectRatio()
    {
        AddAssert("landscape artwork fills square without stretching", () =>
        {
            Vector2 source = new(1672, 941);
            Vector2 result = SongSelectArtworkCrop.CalculateCoverSize(
                source,
                new Vector2(210));
            return Math.Abs(result.Y - 210) < 0.05f
                   && result.X > 372
                   && Math.Abs(result.X / result.Y
                               - source.X / source.Y) < 0.0001f;
        });
        AddAssert("landscape artwork fills square package cover", () =>
        {
            Vector2 source = new(1280, 720);
            Vector2 result = SongSelectArtworkCrop.CalculateCoverSize(
                source,
                new Vector2(84));
            return Math.Abs(result.Y - 84) < 0.05f
                   && result.X > 149
                   && Math.Abs(result.X / result.Y
                               - source.X / source.Y) < 0.0001f;
        });
        AddAssert("portrait artwork fills square package cover", () =>
        {
            Vector2 source = new(900, 1200);
            Vector2 result = SongSelectArtworkCrop.CalculateCoverSize(
                source,
                new Vector2(84));
            return Math.Abs(result.X - 84) < 0.05f
                   && result.Y > 111
                   && Math.Abs(result.X / result.Y
                               - source.X / source.Y) < 0.0001f;
        });
        AddAssert("invalid source dimensions use frame size", () =>
            SongSelectArtworkCrop.CalculateCoverSize(
                Vector2.Zero,
                new Vector2(76)) == new Vector2(76));
    }

    [Test]
    public void TestStandaloneSongUsesSquareArtworkFrame()
    {
        SongSelectEntry standalone = entries[0] with
        {
            IsPackage = false,
            PackageId = "standalone-song",
        };
        AddStep("show one standalone song", () => list.SetItems(
        [
            new SongSelectVirtualItem
            {
                Entry = standalone,
                VisualHeight = 84,
            },
        ]));
        AddUntilStep("standalone row actualised", () =>
            list.MaterialisedRows.Count() == 1);
        AddAssert("standalone cover frame is square", () =>
            list.MaterialisedRows.Single().StandaloneArtworkFrameSize
            == new Vector2(76));
    }

    [Test]
    public void TestExpandedHeaderFeedsAndAnimatesPackageGuide()
    {
        AddStep("show one expanded package", () =>
        {
            list.UpdateSelection(null);
            list.SetItems(
            [
                new SongSelectVirtualItem
                {
                    HeaderEntry = entries[0],
                    PackageId = "scale-package",
                    PackageName = "Scale package",
                    SongCount = 1,
                    ChartCount = 1,
                    Collapsed = false,
                    VisualHeight = SongSelectPackageHeader.ExpandedHeight,
                },
                new SongSelectVirtualItem
                {
                    Entry = entries[0],
                    VisualHeight = 58,
                },
            ]);
        });
        AddUntilStep("header and child actualised", () =>
            list.MaterialisedHeaders.Count() == 1
            && list.MaterialisedRows.Count() == 1);
        AddAssert("package cover frame is square", () =>
        {
            SongSelectPackageHeader header =
                list.MaterialisedHeaders.Single();
            return header.ArtworkFrameSize == new Vector2(132)
                   && header.ArtworkImageFrameSize == new Vector2(122)
                   && Math.Abs(header.ArtworkImageCornerRadius - 8) < 0.05f;
        });
        AddAssert("expanded package actions share the trailing rail", () =>
        {
            SongSelectPackageHeader header =
                list.MaterialisedHeaders.Single();
            return Math.Abs(header.PackageContentStart - 156) < 0.05f
                   && header.FavouriteIconAnchor == Anchor.TopRight
                   && header.FavouriteIconPosition == new Vector2(-18, 8)
                   && header.ChevronFrameAnchor == Anchor.TopRight
                   && header.ChevronFramePosition == new Vector2(-11, 96);
        });
        AddAssert("expanded header exposes a quiet guide stem", () =>
        {
            SongSelectPackageHeader header =
                list.MaterialisedHeaders.Single();
            return header.ChildGuideStemAlpha > 0.33f
                   && header.ChildGuideStemAlpha < 0.35f
                   && header.ExpandedRailAlpha > 0.31f
                   && header.ExpandedRailAlpha < 0.33f
                   && header.ChevronSurfaceAlpha > 0.77f
                   && header.ChevronSurfaceAlpha < 0.79f
                   && header.SelectedRailHeight < 0.01f
                   && header.SelectedIndicatorAlpha < 0.01f;
        });
        AddStep("select package child", () =>
            list.UpdateSelection(entries[0]));
        AddUntilStep("header focus animates onto package guide", () =>
        {
            SongSelectPackageHeader header =
                list.MaterialisedHeaders.Single();
            return header.ChildGuideStemAlpha > 0.91f
                   && header.SelectedRailHeight > 2.99f
                   && header.SelectedIndicatorAlpha > 0.99f
                   && header.PackageSummaryAlpha < 0.01f
                   && header.SelectedSummaryAlpha > 0.99f
                   && header.SelectedContextTitle
                       == entries[0].Beatmap.Title
                   && header.SelectedContextByline.Contains(
                       entries[0].Beatmap.Creator)
                   && header.SelectedContextMode.StartsWith("4K")
                   && !string.IsNullOrWhiteSpace(
                       header.SelectedContextRating);
        });
        AddStep("collapse package and reuse header", () =>
        {
            list.UpdateSelection(null);
            list.SetItems(
            [
                new SongSelectVirtualItem
                {
                    HeaderEntry = entries[0],
                    PackageId = "scale-package",
                    PackageName = "Scale package",
                    SongCount = 1,
                    ChartCount = 1,
                    Collapsed = true,
                    VisualHeight = SongSelectPackageHeader.CollapsedHeight,
                },
            ]);
        });
        AddUntilStep("collapsed header hides pooled guide state", () =>
        {
            SongSelectPackageHeader header =
                list.MaterialisedHeaders.Single();
            return header.ChildGuideStemAlpha < 0.01f
                   && header.ExpandedRailAlpha < 0.01f
                   && header.ChevronSurfaceAlpha > 0.51f
                   && header.ChevronSurfaceAlpha < 0.53f
                   && header.SelectedRailHeight < 0.01f
                   && header.SelectedIndicatorAlpha < 0.01f
                   && header.PackageSummaryAlpha > 0.99f
                   && header.SelectedSummaryAlpha < 0.01f
                   && list.ItemCount == 1;
        });
    }

    [Test]
    public void TestCollapsedHeaderIsCompactAndKeepsLongTitlesOnOneLine()
    {
        const string longPackageName =
            "Harmonic Bloom - Symphony of the Dreaming Petals Beyond the Infinite Starlight Archive of the Last Celestial Horizon";
        AddStep("show one selected collapsed package", () =>
        {
            list.UpdateSelection(entries[0]);
            list.SetItems(
            [
                new SongSelectVirtualItem
                {
                    HeaderEntry = entries[0],
                    PackageId = "scale-package",
                    PackageName = longPackageName,
                    SongCount = 2,
                    ChartCount = 4,
                    Collapsed = true,
                    VisualHeight = SongSelectPackageHeader.CollapsedHeight,
                },
            ]);
        });
        AddUntilStep("compact header actualised", () =>
            list.MaterialisedHeaders.Count() == 1);
        AddAssert("collapsed header preserves a square compact cover", () =>
        {
            SongSelectPackageHeader header =
                list.MaterialisedHeaders.Single();
            return Math.Abs(header.Height
                            - SongSelectPackageHeader.CollapsedHeight) < 0.05f
                   && header.ArtworkFrameSize == new Vector2(
                       SongSelectPackageHeader.CollapsedHeight)
                   && header.ArtworkImageFrameSize == new Vector2(86)
                   && Math.Abs(header.ArtworkImageCornerRadius - 8) < 0.05f;
        });
        AddAssert("long package title stays on one truncating line", () =>
        {
            SongSelectPackageHeader header =
                list.MaterialisedHeaders.Single();
            return header.PackageTitleLineCount == 1
                   && header.PackageTitleUsesTruncation
                   && Math.Abs(header.PackageContentStart - 120) < 0.05f
                   && header.FavouriteIconAnchor == Anchor.TopRight
                   && header.FavouriteIconPosition == new Vector2(-18, 8)
                   && header.ChevronFrameAnchor == Anchor.TopRight
                   && header.ChevronFramePosition == new Vector2(-11, 60)
                   && header.PackageSummaryAlpha > 0.99f
                   && header.SelectedSummaryAlpha < 0.01f
                   && list.ItemCount == 1;
        });
    }

    [Test]
    public void TestPackageScrollClampsWhenContentFitsViewport()
    {
        AddStep("show one short package", () => list.SetItems(
        [
            new SongSelectVirtualItem
            {
                HeaderEntry = entries[0],
                PackageId = "short-package",
                PackageName = "Short package",
                SongCount = 1,
                ChartCount = 1,
                Collapsed = true,
                VisualHeight = SongSelectPackageHeader.CollapsedHeight,
            },
        ]));
        AddUntilStep("package header actualised", () =>
            list.MaterialisedDrawableCount == 1);
        AddStep("request package at top", () =>
            list.ScrollPackageToTop("short-package", false));
        AddAssert("short content does not overscroll", () =>
            Math.Abs(list.ScrollPosition) < 0.01);
        AddUntilStep("short content has no scroll chrome", () =>
            list.TopScrollHintAlpha < 0.01f
            && list.BottomScrollHintAlpha < 0.01f
            && list.ScrollIndicatorAlpha < 0.01f);
    }
}
