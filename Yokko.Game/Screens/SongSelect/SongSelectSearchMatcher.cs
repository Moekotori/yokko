using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Yokko.Core.Beatmaps;

namespace Yokko.Game.Screens.SongSelect;

internal static class SongSelectSearchMatcher
{
    internal static string CreateDocument(YokkoBeatmap beatmap)
    {
        ArgumentNullException.ThrowIfNull(beatmap);
        return normalize(string.Join(
            '\u001f',
            beatmap.Title,
            beatmap.RomanisedTitle,
            beatmap.Artist,
            beatmap.RomanisedArtist,
            beatmap.Creator,
            beatmap.DifficultyName,
            beatmap.Source,
            beatmap.Tags));
    }

    internal static string[] TokenizeQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];

        string normalized = normalize(query);
        var tokens = new List<string>();
        var current = new StringBuilder();
        foreach (char character in normalized)
        {
            if (!char.IsWhiteSpace(character))
            {
                current.Append(character);
                continue;
            }

            flushToken();
        }

        flushToken();
        return tokens.Distinct(StringComparer.Ordinal).ToArray();

        void flushToken()
        {
            if (current.Length == 0)
                return;

            tokens.Add(current.ToString());
            current.Clear();
        }
    }

    internal static bool Matches(
        string document,
        IReadOnlyList<string> tokens)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(tokens);
        for (int index = 0; index < tokens.Count; index++)
        {
            if (!document.Contains(tokens[index], StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    private static string normalize(string value) =>
        (value ?? string.Empty)
        .Normalize(NormalizationForm.FormKC)
        .ToUpperInvariant();
}
