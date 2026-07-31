using System;
using System.Collections.Generic;
using System.Linq;

namespace Yokko.Game.Screens.SongSelect;

internal static class SongSelectTextLayout
{
    internal static string[] TwoLines(string value, double unitsPerLine)
    {
        value = string.Join(
            " ",
            (value ?? string.Empty)
            .Split((char[])null, StringSplitOptions.RemoveEmptyEntries));
        if (value.Length == 0)
            return [string.Empty];
        if (measure(value) <= unitsPerLine)
            return [value];

        var lines = new List<string>(2);
        int start = 0;
        while (start < value.Length && lines.Count < 2)
        {
            int end = start;
            int lastSpace = -1;
            double width = 0;
            while (end < value.Length)
            {
                double next = widthOf(value[end]);
                if (width + next > unitsPerLine)
                    break;
                width += next;
                if (char.IsWhiteSpace(value[end]))
                    lastSpace = end;
                end++;
            }

            if (end < value.Length && lastSpace >= start)
                end = lastSpace;
            if (end <= start)
                end = Math.Min(start + 1, value.Length);

            string line = value[start..end].Trim();
            start = end;
            while (start < value.Length && char.IsWhiteSpace(value[start]))
                start++;

            if (lines.Count == 1 && start < value.Length)
            {
                while (line.Length > 1
                       && measure(line + "…") > unitsPerLine)
                {
                    line = line[..^1];
                }
                line = line.TrimEnd() + "…";
                start = value.Length;
            }

            lines.Add(line);
        }

        return lines.ToArray();
    }

    private static double measure(string value) =>
        value.Sum(widthOf);

    private static double widthOf(char character)
    {
        if (char.IsWhiteSpace(character))
            return 0.45;
        if (character >= 0x2E80)
            return 1;
        if ("MW@#%&".Contains(character))
            return 0.9;
        if ("ilI.,'!:;|".Contains(character))
            return 0.35;
        return 0.62;
    }
}
