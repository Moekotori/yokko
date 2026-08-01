using System;
using System.Collections.Generic;
using System.Linq;

namespace Yokko.Game.Screens.SongSelect;

internal static class SongSelectTextLayout
{
    internal static string[] BalancedTwoLines(
        string value,
        double unitsPerLine)
    {
        value = normalise(value);
        if (value.Length == 0 || measure(value) <= unitsPerLine)
            return [value];

        int bestBreak = -1;
        double bestDifference = double.MaxValue;
        for (int index = 1; index < value.Length - 1; index++)
        {
            if (!char.IsWhiteSpace(value[index]))
                continue;

            string left = value[..index].TrimEnd();
            string right = value[(index + 1)..].TrimStart();
            double leftWidth = measure(left);
            double rightWidth = measure(right);
            if (leftWidth > unitsPerLine || rightWidth > unitsPerLine)
                continue;

            double difference = Math.Abs(leftWidth - rightWidth);
            if (difference < bestDifference)
            {
                bestBreak = index;
                bestDifference = difference;
            }
        }

        return bestBreak >= 0
            ?
            [
                value[..bestBreak].TrimEnd(),
                value[(bestBreak + 1)..].TrimStart(),
            ]
            : TwoLines(value, unitsPerLine);
    }

    internal static string[] TwoLines(string value, double unitsPerLine)
    {
        value = normalise(value);
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

    private static string normalise(string value) => string.Join(
        " ",
        (value ?? string.Empty)
        .Split((char[])null, StringSplitOptions.RemoveEmptyEntries));

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
