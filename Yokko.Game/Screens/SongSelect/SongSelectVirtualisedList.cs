using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Pooling;
using osu.Framework.Graphics.Textures;
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

    private readonly Func<SongSelectEntry, ManiaDifficultyRatings> ratingsFor;
    private readonly Func<SongSelectEntry, Texture> textureFor;
    private readonly Func<ManiaDifficultyRatingMode> ratingMode;
    private readonly Action<SongSelectEntry> select;
    private readonly Action<SongSelectEntry> play;
    private readonly Action<string> togglePackage;
    private readonly Texture selectedSticker;
    private readonly BasicScrollContainer scroll;
    private readonly Container itemLayer;
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
    private double lastScrollPosition = double.NaN;
    private float lastViewportHeight = float.NaN;
    private bool rangeInvalidated = true;
    private SongSelectEntry selectedEntry;

    internal int ItemCount => items.Count;
    internal int MaterialisedDrawableCount => active.Count;
    internal IEnumerable<SongSelectSongRow> MaterialisedRows =>
        active.Values.OfType<SongSelectSongRow>();

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
        };
    }

    internal void SetItems(IEnumerable<SongSelectVirtualItem> source)
    {
        releaseAll();
        items.Clear();
        entryIndices.Clear();
        packageIndices.Clear();

        float top = 0;
        foreach (SongSelectVirtualItem item in source)
        {
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
            scroll.ScrollTo(items[index].Top, animated);
        }
    }

    internal void UpdateSelection(SongSelectEntry selectedEntry)
    {
        this.selectedEntry = selectedEntry;
        foreach (KeyValuePair<int, PoolableDrawable> pair in active)
        {
            if (pair.Value is SongSelectSongRow row)
                row.SetSelected(ReferenceEquals(row.Entry, selectedEntry));
            else if (pair.Value is SongSelectPackageHeader header)
                header.SetSelected(string.Equals(
                    items[pair.Key].PackageId,
                    selectedEntry?.PackageId,
                    StringComparison.OrdinalIgnoreCase));
        }
    }

    internal void UpdateDifficulties()
    {
        ManiaDifficultyRatingMode mode = ratingMode();
        foreach (SongSelectSongRow row in MaterialisedRows)
            row.SetDifficulty(ratingsFor(row.Entry), mode);
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

        for (int index = first; index < lastExclusive; index++)
        {
            if (!active.ContainsKey(index))
                actualise(index);
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

    private void actualise(int index)
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
                row.Y = item.Top;
                row.SetSelected(ReferenceEquals(entry, selectedEntry));
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
            header.Y = item.Top;
            itemLayer.Add(header);
            active[index] = header;
        });
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
    internal float Top { get; set; }
}
