using System;
using System.Collections.Generic;
using System.Linq;

namespace Yokko.Game.Screens.SongSelect;

internal enum SongSelectSortMode
{
    Title,
    Artist,
    Creator,
    Difficulty,
    Bpm,
    Length,
    LastPlayed,
    BestScore,
}

internal enum SongSelectSortDirection
{
    Ascending,
    Descending,
}

internal static class SongSelectSorting
{
    internal static SongSelectSortDirection DefaultDirection(
        SongSelectSortMode mode) =>
        mode is SongSelectSortMode.Title
            or SongSelectSortMode.Artist
            or SongSelectSortMode.Creator
            ? SongSelectSortDirection.Ascending
            : SongSelectSortDirection.Descending;

    internal static string Label(SongSelectSortMode mode) => mode switch
    {
        SongSelectSortMode.Title => "TITLE",
        SongSelectSortMode.Artist => "ARTIST",
        SongSelectSortMode.Creator => "MAPPER",
        SongSelectSortMode.Difficulty => "DIFFICULTY",
        SongSelectSortMode.Bpm => "BPM",
        SongSelectSortMode.Length => "LENGTH",
        SongSelectSortMode.LastPlayed => "LAST PLAYED",
        SongSelectSortMode.BestScore => "BEST SCORE",
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
    };

    /// <summary>
    /// Sorts packages as units so package headers and charts remain contiguous.
    /// The leading chart in each sorted package determines the package's global
    /// position, while original indices provide a final stable tie-break.
    /// </summary>
    internal static List<SongSelectEntry> Sort(
        IReadOnlyList<SongSelectEntry> entries,
        SongSelectSortMode mode,
        SongSelectSortDirection direction,
        Func<SongSelectEntry, double?> difficultyValue)
    {
        if (entries.Count <= 1)
            return entries.ToList();

        var indexed = entries.Select((entry, index) => new IndexedEntry(entry, index))
                             .ToArray();
        var comparer = new EntryComparer(mode, direction, difficultyValue);
        var indexedComparer = Comparer<IndexedEntry>.Create((left, right) =>
        {
            int result = comparer.Compare(left.Entry, right.Entry);
            return result != 0 ? result : left.Index.CompareTo(right.Index);
        });

        return indexed.GroupBy(
                          item => item.Entry.PackageId ?? item.Entry.ChartId ?? string.Empty,
                          StringComparer.OrdinalIgnoreCase)
                      .Select(group =>
                      {
                          IndexedEntry[] sorted = group.OrderBy(item => item, indexedComparer)
                                                       .ToArray();
                          return new SortedPackage(sorted, group.Min(item => item.Index));
                      })
                      .OrderBy(
                          package => package.Entries[0],
                          indexedComparer)
                      .ThenBy(package => package.FirstIndex)
                      .SelectMany(package => package.Entries)
                      .Select(item => item.Entry)
                      .ToList();
    }

    private sealed class EntryComparer(
        SongSelectSortMode mode,
        SongSelectSortDirection direction,
        Func<SongSelectEntry, double?> difficultyValue)
        : IComparer<SongSelectEntry>
    {
        public int Compare(SongSelectEntry left, SongSelectEntry right)
        {
            int primary = mode switch
            {
                SongSelectSortMode.Title => compareText(
                    left.Beatmap.Title,
                    right.Beatmap.Title),
                SongSelectSortMode.Artist => compareText(
                    left.Beatmap.Artist,
                    right.Beatmap.Artist),
                SongSelectSortMode.Creator => compareText(
                    left.Beatmap.Creator,
                    right.Beatmap.Creator),
                SongSelectSortMode.Difficulty => compareOptional(
                    difficultyValue(left),
                    difficultyValue(right)),
                SongSelectSortMode.Bpm => compareOptional(
                    positiveOrNull(left.Bpm),
                    positiveOrNull(right.Bpm)),
                SongSelectSortMode.Length => compareOptional(
                    positiveOrNull(left.Length.TotalMilliseconds),
                    positiveOrNull(right.Length.TotalMilliseconds)),
                SongSelectSortMode.LastPlayed => compareOptional(
                    lastPlayedTicks(left),
                    lastPlayedTicks(right)),
                SongSelectSortMode.BestScore => compareOptional(
                    positiveOrNull(left.BestScore),
                    positiveOrNull(right.BestScore)),
                _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
            };
            if (primary != 0)
                return primary;

            // Direction only applies to the chosen field. Alphabetical
            // tie-breaks remain predictable instead of reversing at random.
            int result = compareTextAscending(left.Beatmap.Title, right.Beatmap.Title);
            if (result != 0)
                return result;
            result = compareTextAscending(left.Beatmap.Artist, right.Beatmap.Artist);
            if (result != 0)
                return result;
            result = compareTextAscending(left.Beatmap.Creator, right.Beatmap.Creator);
            if (result != 0)
                return result;
            result = compareTextAscending(left.Beatmap.DifficultyName, right.Beatmap.DifficultyName);
            if (result != 0)
                return result;
            return compareTextAscending(left.ChartId, right.ChartId);
        }

        private int compareText(string left, string right)
        {
            bool leftMissing = string.IsNullOrWhiteSpace(left);
            bool rightMissing = string.IsNullOrWhiteSpace(right);
            if (leftMissing || rightMissing)
                return leftMissing == rightMissing ? 0 : leftMissing ? 1 : -1;

            int result = StringComparer.OrdinalIgnoreCase.Compare(left, right);
            return direction == SongSelectSortDirection.Descending ? -result : result;
        }

        private int compareOptional(double? left, double? right)
        {
            if (!left.HasValue || !right.HasValue)
                return left.HasValue == right.HasValue ? 0 : left.HasValue ? -1 : 1;

            int result = left.Value.CompareTo(right.Value);
            return direction == SongSelectSortDirection.Descending ? -result : result;
        }

        private static int compareTextAscending(string left, string right) =>
            StringComparer.OrdinalIgnoreCase.Compare(left ?? string.Empty, right ?? string.Empty);

        private static double? positiveOrNull(double value) => value > 0 ? value : null;

        private static double? lastPlayedTicks(SongSelectEntry entry)
        {
            DateTimeOffset? lastPlayed = entry.History
                                                .Where(score => score.PlayedAt.HasValue)
                                                .Select(score => score.PlayedAt)
                                                .Max();
            return lastPlayed?.UtcTicks;
        }
    }

    private sealed record IndexedEntry(SongSelectEntry Entry, int Index);

    private sealed record SortedPackage(IndexedEntry[] Entries, int FirstIndex);
}
