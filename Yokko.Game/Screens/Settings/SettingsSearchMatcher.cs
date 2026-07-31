using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Yokko.Game.Screens.Settings;

internal static class SettingsSearchMatcher
{
    public const int NoMatch = -1;

    public static int Score(string query, string titleTerms, string searchTerms)
    {
        string[] tokens = tokenize(query);
        if (tokens.Length == 0)
            return 0;

        string normalizedTitle = normalize(titleTerms);
        string normalizedSearchTerms = normalize(searchTerms);

        if (tokens.Any(token => !normalizedSearchTerms.Contains(token, StringComparison.Ordinal)))
            return NoMatch;

        int score = tokens.Length * 20;
        string completeQuery = string.Concat(tokens);

        // A page title match must outrank a coincidental option match on an
        // earlier page (for example "快捷键" under the gameplay page).
        if (tokens.All(token => normalizedTitle.Contains(token, StringComparison.Ordinal)))
            score += 600;

        if (normalizedTitle.Contains(completeQuery, StringComparison.Ordinal))
            score += 250;
        else if (normalizedSearchTerms.Contains(completeQuery, StringComparison.Ordinal))
            score += 120;

        return score;
    }

    private static string[] tokenize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Array.Empty<string>();

        var tokens = new List<string>();
        var current = new StringBuilder();

        foreach (char character in value)
        {
            if (char.IsLetterOrDigit(character))
            {
                current.Append(char.ToLowerInvariant(character));
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

    private static string normalize(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var result = new StringBuilder(value.Length);
        foreach (char character in value)
        {
            if (char.IsLetterOrDigit(character))
                result.Append(char.ToLowerInvariant(character));
        }

        return result.ToString();
    }
}
