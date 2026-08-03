using System;
using System.Collections.Generic;
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
    private ManiaDifficultyRatings currentRatings;

    public TestSceneSongSelectVirtualisedList()
    {
        var beatmap = DemoBeatmaps.CreateFourKeyDemo();
        var ratings = currentRatings = new ManiaDifficultyRatings(
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
            _ => currentRatings,
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
        AddStep("animate far jump to last row", () =>
            list.ScrollEntryIntoView(entries[^1], true));
        AddAssert("far jump lands without traversing intermediate rows", () =>
            list.ScrollPosition > 100_000);
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
    public void TestReleasedRowIgnoresStaleSelectionCallback()
    {
        SongSelectSongRow releasedRow = null;

        AddStep("show one row", () => list.SetItems(
        [
            new SongSelectVirtualItem
            {
                Entry = entries[0],
                VisualHeight = 58,
            },
        ]));
        AddUntilStep("row is materialised", () =>
            list.MaterialisedRows.Count() == 1);
        AddStep("capture row", () =>
            releasedRow = list.MaterialisedRows.Single());
        AddStep("release row", () =>
            list.SetItems(Array.Empty<SongSelectVirtualItem>()));
        AddUntilStep("released row is returned to pool", () =>
            releasedRow.Entry == null);
        AddStep("ignore stale selection callback", () =>
            releasedRow.SetSelected(false));
        AddAssert("list remains empty", () =>
            list.MaterialisedDrawableCount == 0);
    }

    [Test]
    public void TestPackageLayoutRebuildKeepsTransitionHeaderAnchored()
    {
        SongSelectEntry[] firstPackage = entries.Take(12)
                                                .Select((entry, index) =>
                                                    entry with
                                                    {
                                                        PackageId = "anchor-first",
                                                        ChartId = $"anchor-first-{index}",
                                                    })
                                                .ToArray();
        SongSelectEntry[] secondPackage = entries.Skip(12)
                                                 .Take(12)
                                                 .Select((entry, index) =>
                                                     entry with
                                                     {
                                                         PackageId = "anchor-second",
                                                         ChartId = $"anchor-second-{index}",
                                                     })
                                                 .ToArray();
        SongSelectEntry[] trailingPackage = entries.Skip(24)
                                                   .Take(24)
                                                   .Select((entry, index) =>
                                                       entry with
                                                       {
                                                           PackageId = "anchor-trailing",
                                                           ChartId = $"anchor-trailing-{index}",
                                                       })
                                                   .ToArray();

        AddStep("show first package expanded", () => list.SetItems(
            packageLayout(
                firstPackage,
                secondPackage,
                trailingPackage,
                firstExpanded: true,
                secondExpanded: false)));
        AddUntilStep("initial package headers actualised", () =>
            list.MaterialisedHeaders.Any());
        AddStep("place second package at viewport top", () =>
            list.ScrollPackageToTop("anchor-second", false));
        AddAssert("second package starts away from list origin", () =>
            list.ScrollPosition > SongSelectPackageHeader.CollapsedHeight + 100);
        AddStep("transfer expansion to second package", () => list.SetItems(
            packageLayout(
                firstPackage,
                secondPackage,
                trailingPackage,
                firstExpanded: false,
                secondExpanded: true),
            animateLayout: true,
            transitionPackageId: "anchor-second"));
        AddAssert("transition package remains at viewport top", () =>
            Math.Abs(list.ScrollPosition
                     - (SongSelectPackageHeader.CollapsedHeight + 15)) < 0.05);
    }

    private static IEnumerable<SongSelectVirtualItem> packageLayout(
        SongSelectEntry[] firstPackage,
        SongSelectEntry[] secondPackage,
        SongSelectEntry[] trailingPackage,
        bool firstExpanded,
        bool secondExpanded)
    {
        var result = new List<SongSelectVirtualItem>();
        addPackage(firstPackage, firstExpanded, 0);
        addPackage(secondPackage, secondExpanded, 8);
        addPackage(trailingPackage, true, 8);
        return result;

        void addPackage(
            SongSelectEntry[] packageEntries,
            bool expanded,
            float spacing)
        {
            SongSelectEntry first = packageEntries[0];
            result.Add(new SongSelectVirtualItem
            {
                HeaderEntry = first,
                PackageId = first.PackageId,
                PackageName = first.PackageName,
                SongCount = packageEntries.Length,
                ChartCount = packageEntries.Length,
                Collapsed = !expanded,
                VisualHeight = expanded
                    ? SongSelectPackageHeader.ExpandedHeight
                    : SongSelectPackageHeader.CollapsedHeight,
                SectionSpacingBefore = spacing,
            });

            if (!expanded)
                return;

            result.AddRange(packageEntries.Select(entry =>
                new SongSelectVirtualItem
                {
                    Entry = entry,
                    VisualHeight = SongSelectSongRow.CompactHeight,
                }));
        }
    }

    [Test]
    public void TestArtworkPreloadCoversPreparedRememberedViewport()
    {
        SongSelectEntry[] standalone = entries.Take(200)
                                              .Select((entry, index) => entry with
                                              {
                                                  IsPackage = false,
                                                  WallpaperTexture = $"art-{index}",
                                              })
                                              .ToArray();

        AddStep("index standalone artwork rows", () => list.SetItems(
            standalone.Select(entry => new SongSelectVirtualItem
            {
                Entry = entry,
                VisualHeight = SongSelectSongRow.StandaloneHeight,
            })));
        AddStep("prepare remembered viewport", () =>
            list.PrepareViewportFor(standalone[^1], 714));
        AddAssert("remembered viewport is prepared before first update", () =>
            list.ScrollPosition > 0);
        AddAssert("preload covers remembered visible region", () =>
        {
            IReadOnlyList<SongSelectEntry> candidates =
                list.GetArtworkPreloadCandidates(
                    standalone[^1],
                    714,
                    16);
            return candidates.Contains(standalone[^1])
                   && candidates.Any(entry =>
                       !ReferenceEquals(entry, standalone[^1]))
                   && candidates.Count <= 16;
        });
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
            && Math.Abs(list.ScrollIndicatorRightOffset) < 0.01f
            && Math.Abs(list.ScrollIndicatorWidth - 5) < 0.01f
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
                   && Math.Abs(rows[0].X + rows[0].Width
                               - SongSelectSongRow.RowWidth) < 0.05f;
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
            return Math.Abs(inPackageGap - 7) < 0.05f
                   && Math.Abs(sectionGap - 15) < 0.05f;
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
        AddAssert("rating uses separate trailing metadata columns", () =>
        {
            SongSelectInlineDifficultyRating rating = list
                .MaterialisedRows.Single()
                .ChildrenOfType<SongSelectInlineDifficultyRating>()
                .Single();
            return Math.Abs(rating.X - 650) < 0.05f
                   && Math.Abs(rating.Y - 17) < 0.05f
                   && Math.Abs(rating.Width - 112) < 0.05f
                   && Math.Abs(rating.Height - 22) < 0.05f
                   && Math.Abs(rating.UnitText.Width - 42) < 0.05f
                   && Math.Abs(rating.ValueText.Width - 66) < 0.05f
                   && rating.UnitText.Width + rating.ValueText.Width
                   <= rating.Width - 4
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
        AddStep("replace calculated ratings and refresh active row", () =>
        {
            YokkoBeatmap alternate = DemoBeatmaps.CreateSevenKeyDemo();
            currentRatings = new ManiaDifficultyRatings(
                ManiaMsdCalculator.CalculateResult(alternate),
                ManiaStarRatingCalculator.CalculateResult(alternate));
            list.UpdateDifficulties();
        });
        AddAssert("active row adopts refreshed rating without rebind", () =>
            ReferenceEquals(
                list.MaterialisedRows.Single().DisplayedDifficultyRatings,
                currentRatings));
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
                Math.Abs(row.ModePillWidth - 58) < 0.05f
                && Math.Abs(row.ModePillX - 872) < 0.05f
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
            return Math.Abs(selected.ModePillWidth - 126) < 0.05f
                   && Math.Abs(selected.ModePillX - 804) < 0.05f
                   && selected.CompactModeTextAlpha < 0.01f
                   && selected.ExpandedModeTextAlpha > 0.99f
                   && selected.SelectedStickerAlpha > 0.89f
                   && Vector2.Distance(
                       selected.SelectedStickerScale,
                       Vector2.One) < 0.01f
                   && Math.Abs(resting.ModePillWidth - 58) < 0.05f
                   && resting.SelectedStickerAlpha < 0.01f
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
            return Math.Abs(previous.ModePillWidth - 58) < 0.05f
                   && Math.Abs(previous.ModePillX - 872) < 0.05f
                   && previous.CompactModeTextAlpha > 0.99f
                   && previous.SelectedStickerAlpha < 0.01f
                   && Math.Abs(selected.ModePillWidth - 126) < 0.05f
                   && selected.ExpandedModeTextAlpha > 0.99f
                   && selected.SelectedStickerAlpha > 0.89f
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
        AddAssert("landscape artwork fits wide frame without cropping", () =>
        {
            Vector2 source = new(1920, 1080);
            Vector2 result = SongSelectArtworkCrop.CalculateFitSize(
                source,
                SongSelectSongRow.StandaloneArtworkSize);
            return result.X <= SongSelectSongRow.StandaloneArtworkSize.X
                   && result.Y <= SongSelectSongRow.StandaloneArtworkSize.Y
                   && Math.Abs(result.X / result.Y
                               - source.X / source.Y) < 0.0001f;
        });
    }

    [Test]
    public void TestStandaloneSongUsesWideArtworkFrame()
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
                VisualHeight = SongSelectSongRow.StandaloneHeight,
            },
        ]));
        AddUntilStep("standalone row actualised", () =>
            list.MaterialisedRows.Count() == 1);
        AddAssert("standalone cover frame is wide", () =>
            list.MaterialisedRows.Single().StandaloneArtworkFrameSize
            == SongSelectSongRow.StandaloneArtworkSize);
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
        AddAssert("package cover frame preserves wide artwork", () =>
        {
            SongSelectPackageHeader header =
                list.MaterialisedHeaders.Single();
            return header.ArtworkFrameSize == new Vector2(228, 132)
                   && header.ArtworkImageFrameSize
                      == SongSelectSongRow.StandaloneArtworkSize
                   && Math.Abs(header.ArtworkImageCornerRadius - 8) < 0.05f;
        });
        AddAssert("expanded package actions share the trailing rail", () =>
        {
            SongSelectPackageHeader header =
                list.MaterialisedHeaders.Single();
            return Math.Abs(header.PackageContentStart - 244) < 0.05f
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
                   && header.SelectedModePillPosition
                      == new Vector2(244, 98)
                   && header.SelectedModePillSize
                      == new Vector2(276, 30)
                   && header.SelectedRatingPosition
                      == new Vector2(766, 100)
                   && !string.IsNullOrWhiteSpace(
                       header.SelectedContextRating);
        });
        AddStep("refresh selected package rating in place", () =>
        {
            YokkoBeatmap alternate = DemoBeatmaps.CreateSevenKeyDemo();
            currentRatings = new ManiaDifficultyRatings(
                ManiaMsdCalculator.CalculateResult(alternate),
                ManiaStarRatingCalculator.CalculateResult(alternate));
            list.UpdateDifficulties();
        });
        AddAssert("package header and child share refreshed MSD", () =>
        {
            SongSelectPackageHeader header =
                list.MaterialisedHeaders.Single();
            SongSelectSongRow row = list.MaterialisedRows.Single();
            string expected = ManiaDifficultyPresentation.FormatValue(
                currentRatings,
                ManiaDifficultyRatingMode.EtternaMsd);
            return header.SelectedContextRating == expected
                   && ReferenceEquals(
                       row.DisplayedDifficultyRatings,
                       currentRatings);
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
        AddAssert("collapsed header preserves the complete wide artwork", () =>
        {
            SongSelectPackageHeader header =
                list.MaterialisedHeaders.Single();
            return Math.Abs(header.Height
                            - SongSelectPackageHeader.CollapsedHeight) < 0.05f
                   && header.ArtworkFrameSize == new Vector2(193, 112)
                   && header.ArtworkImageFrameSize == new Vector2(185, 104)
                   && Math.Abs(header.ArtworkImageCornerRadius - 8) < 0.05f;
        });
        AddAssert("long package title stays on one truncating line", () =>
        {
            SongSelectPackageHeader header =
                list.MaterialisedHeaders.Single();
            return header.PackageTitleLineCount == 1
                   && header.PackageTitleUsesTruncation
                   && Math.Abs(header.PackageContentStart - 209) < 0.05f
                   && header.FavouriteIconAnchor == Anchor.TopRight
                   && header.FavouriteIconPosition == new Vector2(-18, 8)
                   && header.ChevronFrameAnchor == Anchor.TopRight
                   && header.ChevronFramePosition == new Vector2(-11, 76)
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
