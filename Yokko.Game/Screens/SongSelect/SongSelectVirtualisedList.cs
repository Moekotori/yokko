using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Pooling;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Textures;
using osuTK;
using Yokko.Core.Difficulty;

namespace Yokko.Game.Screens.SongSelect;

/// <summary>
/// Variable-height, pooled song list.
///
/// The actualisation model follows ppy/osu's Carousel.cs at commit
/// 83b8a64bec19e1463353645c2d6d10c75e275b43 and osu-framework's
/// VirtualisedListContainer from package 2026.728.1 (both MIT): keep cheap
/// position records for the full collection and only allocate drawables for
/// the visible/preloaded range.
/// </summary>
internal partial class SongSelectVirtualisedList : CompositeDrawable
{
    private const float item_spacing = 5;
    private const int initial_row_pool_size = 16;
    private const int maximum_row_pool_size = 40;
    private const int initial_header_pool_size = 4;
    private const int maximum_header_pool_size = 16;
    private const float edge_fade_height = 28;
    private const float scroll_indicator_inset = 12;

    private readonly Func<SongSelectEntry, ManiaDifficultyRatings> ratingsFor;
    private readonly Func<SongSelectEntry, Texture> textureFor;
    private readonly Func<ManiaDifficultyRatingMode> ratingMode;
    private readonly Action<SongSelectEntry> select;
    private readonly Action<SongSelectEntry> play;
    private readonly Action<string> togglePackage;
    private readonly Texture selectedSticker;
    private readonly BasicScrollContainer scroll;
    private readonly Container itemLayer;
    private readonly Box topScrollFade;
    private readonly Box bottomScrollFade;
    private readonly Container scrollIndicator;
    private readonly Container scrollIndicatorThumb;
    private readonly DrawablePool<SongSelectSongRow> rowPool =
        new(initial_row_pool_size, maximum_row_pool_size);
    private readonly DrawablePool<SongSelectPackageHeader> headerPool =
        new(initial_header_pool_size, maximum_header_pool_size);
    private readonly List<SongSelectVirtualItem> items = [];
    private readonly Dictionary<int, PoolableDrawable> active = [];
    private readonly Dictionary<SongSelectEntry, int> entryIndices =
        new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<string, int> packageIndices =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<SongSelectEntry, float> previousEntryTops =
        new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<string, float> previousPackageTops =
        new(StringComparer.OrdinalIgnoreCase);
    private double lastScrollPosition = double.NaN;
    private float lastViewportHeight = float.NaN;
    private bool rangeInvalidated = true;
    private bool layoutAnimationPending;
    private string transitionPackageId;
    private SongSelectEntry selectedEntry;

    internal int ItemCount => items.Count;
    internal int MaterialisedDrawableCount => active.Count;
    internal double ScrollPosition => scroll.Current;
    internal float TopScrollHintAlpha => topScrollFade.Alpha;
    internal float BottomScrollHintAlpha => bottomScrollFade.Alpha;
    internal float ScrollIndicatorAlpha => scrollIndicator.Alpha;
    internal float ScrollIndicatorProgress { get; private set; }
    internal IEnumerable<SongSelectSongRow> MaterialisedRows =>
        active.Values.OfType<SongSelectSongRow>();
    internal IEnumerable<SongSelectPackageHeader> MaterialisedHeaders =>
        active.Values.OfType<SongSelectPackageHeader>();

    public SongSelectVirtualisedList(
        Func<SongSelectEntry, ManiaDifficultyRatings> ratingsFor,
        Func<SongSelectEntry, Texture> textureFor,
        Func<ManiaDifficultyRatingMode> ratingMode,
        Texture selectedSticker,
        Action<SongSelectEntry> select,
        Action<SongSelectEntry> play,
        Action<string> togglePackage)
    {
        this.ratingsFor = ratingsFor;
        this.textureFor = textureFor;
        this.ratingMode = ratingMode;
        this.selectedSticker = selectedSticker;
        this.select = select;
        this.play = play;
        this.togglePackage = togglePackage;

        RelativeSizeAxes = Axes.Both;
        InternalChildren = new Drawable[]
        {
            rowPool,
            headerPool,
            scroll = new BasicScrollContainer
            {
                RelativeSizeAxes = Axes.Both,
                ScrollbarVisible = false,
                Child = itemLayer = new Container
                {
                    RelativeSizeAxes = Axes.X,
                },
            },
            topScrollFade = new Box
            {
                RelativeSizeAxes = Axes.X,
                Height = edge_fade_height,
                Colour = ColourInfo.GradientVertical(
                    SongSelectSurface.Ivory(0.84f),
                    SongSelectSurface.Ivory(0)),
                Alpha = 0,
            },
            bottomScrollFade = new Box
            {
                Anchor = Anchor.BottomLeft,
                Origin = Anchor.BottomLeft,
                RelativeSizeAxes = Axes.X,
                Height = edge_fade_height,
                Colour = ColourInfo.GradientVertical(
                    SongSelectSurface.Ivory(0),
                    SongSelectSurface.Ivory(0.84f)),
                Alpha = 0,
            },
            scrollIndicator = new Container
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                Position = new Vector2(-3, scroll_indicator_inset),
                Width = 4,
                Alpha = 0,
                Children =
                [
                    new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Masking = true,
                        CornerRadius = 2,
                        Child = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = SongSelectSurface.Border(0.13f),
                        },
                    },
                    scrollIndicatorThumb = new Container
                    {
                        Width = 4,
                        Masking = true,
                        CornerRadius = 2,
                        Child = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = SongSelectTheme.Cyan,
                        },
                    },
                ],
            },
        };
    }

    internal void SetItems(
        IEnumerable<SongSelectVirtualItem> source,
        bool animateLayout = false,
        string transitionPackageId = null)
    {
        previousEntryTops.Clear();
        previousPackageTops.Clear();
        if (animateLayout)
        {
            foreach (SongSelectVirtualItem oldItem in items)
            {
                if (oldItem.Entry != null)
                    previousEntryTops[oldItem.Entry] = oldItem.Top;
                else if (!string.IsNullOrWhiteSpace(oldItem.PackageId))
                    previousPackageTops[oldItem.PackageId] = oldItem.Top;
            }
        }

        releaseAll();
        items.Clear();
        entryIndices.Clear();
        packageIndices.Clear();

        float top = 0;
        foreach (SongSelectVirtualItem item in source)
        {
            top += item.SectionSpacingBefore;
            item.Top = top;
            int index = items.Count;
            items.Add(item);
            if (item.Entry != null)
                entryIndices[item.Entry] = index;
            else if (!string.IsNullOrEmpty(item.PackageId))
                packageIndices[item.PackageId] = index;
            top += item.VisualHeight + item_spacing;
        }

        itemLayer.Height = Math.Max(0, top - item_spacing);
        if (scroll.Current > itemLayer.Height)
            scroll.ScrollTo(itemLayer.Height, false);
        layoutAnimationPending = animateLayout;
        this.transitionPackageId = transitionPackageId;
        rangeInvalidated = true;
    }

    internal void ScrollEntryIntoView(SongSelectEntry entry, bool animated)
    {
        if (entry != null && entryIndices.TryGetValue(entry, out int index))
            scrollItemIntoView(items[index], animated);
    }

    internal void ScrollPackageToTop(string packageId, bool animated)
    {
        if (packageId != null
            && packageIndices.TryGetValue(packageId, out int index))
        {
            double maximumScroll = Math.Max(
                0,
                itemLayer.Height - scroll.DrawHeight);
            scroll.ScrollTo(
                Math.Clamp(items[index].Top, 0, maximumScroll),
                animated);
        }
    }

    internal void UpdateSelection(SongSelectEntry selectedEntry)
    {
        this.selectedEntry = selectedEntry;
        foreach (KeyValuePair<int, PoolableDrawable> pair in active)
        {
            if (pair.Value is SongSelectSongRow row)
                updateRowSelection(pair.Key, row, true);
            else if (pair.Value is SongSelectPackageHeader header)
            {
                bool selected = string.Equals(
                    items[pair.Key].PackageId,
                    selectedEntry?.PackageId,
                    StringComparison.OrdinalIgnoreCase);
                header.SetSelected(
                    selected,
                    selected ? selectedEntry : null,
                    selected ? ratingsFor(selectedEntry) : null,
                    ratingMode());
            }
        }
    }

    private void updateRowSelection(
        int itemIndex,
        SongSelectSongRow row,
        bool animated)
    {
        bool isSelected = ReferenceEquals(row.Entry, selectedEntry);
        float neighbourIndent = 0;
        if (!isSelected
            && row.Entry?.IsPackage == true
            && selectedEntry?.IsPackage == true
            && string.Equals(
                row.Entry.PackageId,
                selectedEntry.PackageId,
                StringComparison.OrdinalIgnoreCase)
            && entryIndices.TryGetValue(selectedEntry, out int selectedIndex))
        {
            int distance = Math.Abs(itemIndex - selectedIndex);
            neighbourIndent = Math.Min(
                12,
                Math.Max(0, distance - 1) * 4);
        }

        row.SetSelectionState(
            isSelected,
            neighbourIndent,
            animated);
    }

    internal void UpdateDifficulties()
    {
        ManiaDifficultyRatingMode mode = ratingMode();
        foreach (KeyValuePair<int, PoolableDrawable> pair in active)
        {
            if (pair.Value is SongSelectSongRow row)
            {
                row.SetDifficulty(ratingsFor(row.Entry), mode);
                continue;
            }

            if (pair.Value is SongSelectPackageHeader header)
            {
                bool selected = string.Equals(
                    items[pair.Key].PackageId,
                    selectedEntry?.PackageId,
                    StringComparison.OrdinalIgnoreCase);
                header.SetSelected(
                    selected,
                    selected ? selectedEntry : null,
                    selected ? ratingsFor(selectedEntry) : null,
                    mode,
                    false);
            }
        }
    }

    protected override void Update()
    {
        base.Update();
        if (!rangeInvalidated
            && Math.Abs(lastScrollPosition - scroll.Current) < 0.01
            && Math.Abs(lastViewportHeight - scroll.DrawHeight) < 0.01)
        {
            return;
        }

        lastScrollPosition = scroll.Current;
        lastViewportHeight = scroll.DrawHeight;
        rangeInvalidated = false;
        actualiseVisibleRange();
        updateScrollAffordances();
    }

    private void updateScrollAffordances()
    {
        float viewportHeight = scroll.DrawHeight;
        double maximumScroll = Math.Max(0, itemLayer.Height - viewportHeight);
        if (viewportHeight <= 0 || maximumScroll <= 0.5)
        {
            topScrollFade.Alpha = 0;
            bottomScrollFade.Alpha = 0;
            scrollIndicator.Alpha = 0;
            ScrollIndicatorProgress = 0;
            return;
        }

        const float reveal_distance = 24;
        topScrollFade.Alpha = (float)Math.Clamp(
            scroll.Current / reveal_distance,
            0,
            1);
        bottomScrollFade.Alpha = (float)Math.Clamp(
            (maximumScroll - scroll.Current) / reveal_distance,
            0,
            1);

        float indicatorHeight = Math.Max(
            0,
            viewportHeight - scroll_indicator_inset * 2);
        scrollIndicator.Height = indicatorHeight;
        scrollIndicator.Alpha = indicatorHeight > 0 ? 0.72f : 0;

        float proportionalHeight = indicatorHeight
                                   * viewportHeight
                                   / itemLayer.Height;
        scrollIndicatorThumb.Height = Math.Clamp(
            proportionalHeight,
            Math.Min(32, indicatorHeight),
            indicatorHeight);
        ScrollIndicatorProgress = (float)Math.Clamp(
            scroll.Current / maximumScroll,
            0,
            1);
        scrollIndicatorThumb.Y = (indicatorHeight
                                  - scrollIndicatorThumb.Height)
                                 * ScrollIndicatorProgress;
    }

    private void actualiseVisibleRange()
    {
        if (items.Count == 0 || scroll.DrawHeight <= 0)
        {
            releaseAll();
            return;
        }

        double preload = scroll.DrawHeight * 0.75;
        double minimum = Math.Max(0, scroll.Current - preload);
        double maximum = scroll.Current + scroll.DrawHeight + preload;
        int first = firstItemEndingAfter(minimum);
        int lastExclusive = firstItemStartingAfter(maximum);

        foreach (int index in active.Keys.ToArray())
        {
            if (index < first || index >= lastExclusive)
                release(index);
        }

        bool animateLayout = layoutAnimationPending;
        for (int index = first; index < lastExclusive; index++)
        {
            if (!active.ContainsKey(index))
                actualise(index, animateLayout);
        }

        if (animateLayout)
        {
            layoutAnimationPending = false;
            transitionPackageId = null;
            previousEntryTops.Clear();
            previousPackageTops.Clear();
        }
    }

    private int firstItemEndingAfter(double position)
    {
        int low = 0;
        int high = items.Count;
        while (low < high)
        {
            int middle = low + (high - low) / 2;
            SongSelectVirtualItem item = items[middle];
            if (item.Top + item.VisualHeight < position)
                low = middle + 1;
            else
                high = middle;
        }
        return low;
    }

    private int firstItemStartingAfter(double position)
    {
        int low = 0;
        int high = items.Count;
        while (low < high)
        {
            int middle = low + (high - low) / 2;
            if (items[middle].Top <= position)
                low = middle + 1;
            else
                high = middle;
        }
        return low;
    }

    private void actualise(int index, bool animateLayout)
    {
        SongSelectVirtualItem item = items[index];
        if (item.Entry != null)
        {
            SongSelectEntry entry = item.Entry;
            rowPool.Get(row =>
            {
                row.Bind(
                    entry,
                    ratingsFor(entry),
                    ratingMode(),
                    entry.IsPackage ? null : textureFor(entry),
                    selectedSticker,
                    () => select(entry),
                    () => play(entry));
                applyLayoutTransition(
                    row,
                    item.Top,
                    animateLayout,
                    previousEntryTops.TryGetValue(entry, out float oldTop)
                        ? oldTop
                        : null,
                    transitionPackageId == null
                    || string.Equals(
                        transitionPackageId,
                        entry.PackageId,
                        StringComparison.OrdinalIgnoreCase));
                updateRowSelection(index, row, false);
                itemLayer.Add(row);
                active[index] = row;
            });
            return;
        }

        headerPool.Get(header =>
        {
            header.Bind(
                item.PackageName,
                item.SongCount,
                item.ChartCount,
                item.Collapsed,
                textureFor(item.HeaderEntry),
                string.Equals(
                    item.PackageId,
                    selectedEntry?.PackageId,
                    StringComparison.OrdinalIgnoreCase),
                () => togglePackage(item.PackageId));
            bool selected = string.Equals(
                item.PackageId,
                selectedEntry?.PackageId,
                StringComparison.OrdinalIgnoreCase);
            header.SetSelected(
                selected,
                selected ? selectedEntry : null,
                selected ? ratingsFor(selectedEntry) : null,
                ratingMode(),
                false);
            applyLayoutTransition(
                header,
                item.Top,
                animateLayout,
                previousPackageTops.TryGetValue(
                    item.PackageId,
                    out float oldTop)
                    ? oldTop
                    : null,
                false);
            itemLayer.Add(header);
            active[index] = header;
        });
    }

    private static void applyLayoutTransition(
        Drawable drawable,
        float targetY,
        bool animateLayout,
        float? previousTop,
        bool fadeNewItem)
    {
        drawable.Alpha = 1;
        drawable.Y = targetY;
        if (!animateLayout)
            return;

        if (previousTop.HasValue)
        {
            drawable.Y = Math.Clamp(
                previousTop.Value,
                targetY - 64,
                targetY + 64);
        }
        else if (fadeNewItem)
        {
            drawable.Y = targetY - 14;
            drawable.Alpha = 0;
            drawable.FadeIn(170, Easing.OutQuint);
        }

        drawable.MoveToY(targetY, 230, Easing.OutQuint);
    }

    private void scrollItemIntoView(
        SongSelectVirtualItem item,
        bool animated)
    {
        double top = item.Top;
        double bottom = top + item.VisualHeight;
        if (top < scroll.Current)
            scroll.ScrollTo(top, animated);
        else if (bottom > scroll.Current + scroll.DrawHeight)
            scroll.ScrollTo(bottom - scroll.DrawHeight, animated);
    }

    private void releaseAll()
    {
        foreach (int index in active.Keys.ToArray())
            release(index);
    }

    private void release(int index)
    {
        if (!active.Remove(index, out PoolableDrawable drawable))
            return;
        drawable.ClearTransforms();
        drawable.Expire();
    }
}

internal sealed class SongSelectVirtualItem
{
    internal SongSelectEntry Entry { get; init; }
    internal SongSelectEntry HeaderEntry { get; init; }
    internal string PackageId { get; init; }
    internal string PackageName { get; init; }
    internal int SongCount { get; init; }
    internal int ChartCount { get; init; }
    internal bool Collapsed { get; init; }
    internal bool Selected { get; init; }
    internal float VisualHeight { get; init; }
    internal float SectionSpacingBefore { get; init; }
    internal float Top { get; set; }
}
