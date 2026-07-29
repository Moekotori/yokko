using Yokko.Core.Beatmaps;
using Yokko.Core.Timing;

namespace Yokko.Core.Mods;

/// <summary>
/// Legacy osu!standard-to-Mania circle pattern generation.
/// Ported from ManiaBeatmapConverter and HitCirclePatternGenerator at
/// ppy/osu 9f227ed28b6c8ba46dfea1f000f778d8b2827ad0 (MIT).
/// </summary>
internal sealed class OsuStandardPatternGenerator
{
    [Flags]
    private enum PatternType
    {
        None = 0,
        ForceStack = 1,
        ForceNotStack = 1 << 1,
        KeepSingle = 1 << 2,
        LowProbability = 1 << 3,
        Gathered = 1 << 7,
        Mirror = 1 << 8,
        Reverse = 1 << 9,
        Cycle = 1 << 10,
        Stair = 1 << 11,
        ReverseStair = 1 << 12,
    }

    private sealed class Pattern
    {
        internal List<YokkoHitObject> Objects { get; } = [];
        internal HashSet<int> Columns { get; } = [];

        internal void Add(
            int column,
            double start,
            double? end = null)
        {
            Objects.Add(new YokkoHitObject(
                column,
                start,
                end,
                end is null ? HitObjectKind.Tap : HitObjectKind.Hold));
            Columns.Add(column);
        }
    }

    private readonly ManiaConversionSource source;
    private readonly int totalColumns;
    private readonly int randomStart;
    private readonly IReadOnlyList<YokkoTimingPoint> timingPoints;
    private readonly LegacyRandom random;
    private readonly Queue<double> previousTimes = new(7);
    private Pattern previous = new();
    private PatternType lastStair = PatternType.Stair;
    private double density = int.MaxValue;
    private double lastTime;
    private double lastX;
    private double lastY;

    internal OsuStandardPatternGenerator(
        ManiaConversionSource source,
        int totalColumns,
        IReadOnlyList<YokkoTimingPoint>? timingPoints)
    {
        this.source = source;
        this.totalColumns = totalColumns;
        this.timingPoints = timingPoints ?? [YokkoTimingPoint.Default];
        randomStart = totalColumns == 8 ? 1 : 0;
        int seed = (int)MathF.Round(
                       (float)(source.DrainRate + source.CircleSize))
                   * 20
                   + (int)(source.OverallDifficulty * 41.2)
                   + (int)MathF.Round((float)source.ApproachRate);
        random = new LegacyRandom(seed);
    }

    internal IReadOnlyList<YokkoHitObject> Generate()
    {
        var output = new List<YokkoHitObject>();
        foreach (ManiaConversionHitObject hitObject in
                 source.HitObjects.OrderBy(static item =>
                     item.StartTimeMilliseconds))
        {
            Pattern pattern;
            Pattern? nextPrevious = null;
            switch (hitObject.Kind)
            {
                case ManiaConversionObjectKind.Circle:
                    pattern = generateCircle(hitObject);
                    nextPrevious = pattern;
                    break;

                case ManiaConversionObjectKind.Spinner:
                    pattern = generateSpinner(hitObject);
                    record(
                        hitObject.EndTimeMilliseconds,
                        256,
                        192);
                    computeDensity(hitObject.EndTimeMilliseconds);
                    break;

                default:
                    pattern = generateSlider(hitObject);
                    nextPrevious = sliderEndPattern(
                        pattern,
                        Math.Floor(
                            hitObject.EndTimeMilliseconds));
                    double segmentDuration =
                        (hitObject.EndTimeMilliseconds
                         - hitObject.StartTimeMilliseconds)
                        / Math.Max(1, hitObject.SpanCount);
                    for (int i = 0; i <= hitObject.SpanCount; i++)
                    {
                        double time =
                            hitObject.StartTimeMilliseconds
                            + segmentDuration * i;
                        record(time, hitObject.X, hitObject.Y);
                        computeDensity(time);
                    }
                    break;
            }
            output.AddRange(pattern.Objects);
            if (nextPrevious is not null)
                previous = nextPrevious;
        }

        return output
               .OrderBy(static item => item.StartTimeMilliseconds)
               .ThenBy(static item => item.Lane)
               .ToArray();
    }

    private Pattern generateCircle(
        ManiaConversionHitObject hitObject)
    {
        computeDensity(hitObject.StartTimeMilliseconds);
        double separation = Math.Sqrt(
            Math.Pow(hitObject.X - lastX, 2)
            + Math.Pow(hitObject.Y - lastY, 2));
        double timeSeparation =
            hitObject.StartTimeMilliseconds - lastTime;
        PatternType type = PatternType.None;
        if (timeSeparation <= 80)
            type |= PatternType.ForceNotStack | PatternType.KeepSingle;
        else if (timeSeparation <= 95)
            type |= PatternType.ForceNotStack
                    | PatternType.KeepSingle
                    | lastStair;
        else if (timeSeparation <= 105)
            type |= PatternType.ForceNotStack | PatternType.LowProbability;
        else if (timeSeparation <= 125)
            type |= PatternType.ForceNotStack;
        else if (timeSeparation <= 135 && separation < 20)
            type |= PatternType.Cycle | PatternType.KeepSingle;
        else if (timeSeparation <= 150 && separation < 20)
            type |= PatternType.ForceStack | PatternType.LowProbability;
        else if (separation < 20
                 && density >= beatLengthAt(
                     hitObject.StartTimeMilliseconds) / 2.5)
        {
            type |= PatternType.Reverse | PatternType.LowProbability;
        }
        else if (density >= beatLengthAt(
                     hitObject.StartTimeMilliseconds) / 2.5
                 && !kiaiAt(hitObject.StartTimeMilliseconds))
        {
            type |= PatternType.LowProbability;
        }

        if (!type.HasFlag(PatternType.KeepSingle))
        {
            if (hasFinish(hitObject) && totalColumns != 8)
                type |= PatternType.Mirror;
            else if (hasClap(hitObject))
                type |= PatternType.Gathered;
        }

        Pattern pattern = generateCircleCore(hitObject, type);
        foreach (YokkoHitObject generated in pattern.Objects)
        {
            if (type.HasFlag(PatternType.Stair)
                && generated.Lane == totalColumns - 1)
            {
                lastStair = PatternType.ReverseStair;
            }
            if (type.HasFlag(PatternType.ReverseStair)
                && generated.Lane == randomStart)
            {
                lastStair = PatternType.Stair;
            }
        }
        record(
            hitObject.StartTimeMilliseconds,
            hitObject.X,
            hitObject.Y);
        return pattern;
    }

    private Pattern generateCircleCore(
        ManiaConversionHitObject hitObject,
        PatternType type)
    {
        var pattern = new Pattern();
        if (totalColumns == 1)
        {
            pattern.Add(0, hitObject.StartTimeMilliseconds);
            return pattern;
        }

        int lastColumn = previous.Objects.FirstOrDefault()?.Lane ?? 0;
        if (type.HasFlag(PatternType.Reverse)
            && previous.Objects.Count > 0)
        {
            for (int column = randomStart;
                 column < totalColumns;
                 column++)
            {
                if (previous.Columns.Contains(column))
                {
                    pattern.Add(
                        randomStart + totalColumns - column - 1,
                        hitObject.StartTimeMilliseconds);
                }
            }
            return pattern;
        }
        if (type.HasFlag(PatternType.Cycle)
            && previous.Objects.Count == 1
            && (totalColumns != 8 || lastColumn != 0)
            && (totalColumns % 2 == 0
                || lastColumn != totalColumns / 2))
        {
            pattern.Add(
                randomStart + totalColumns - lastColumn - 1,
                hitObject.StartTimeMilliseconds);
            return pattern;
        }
        if (type.HasFlag(PatternType.ForceStack)
            && previous.Objects.Count > 0)
        {
            foreach (int column in previous.Columns.Order())
                pattern.Add(column, hitObject.StartTimeMilliseconds);
            return pattern;
        }
        if (previous.Objects.Count == 1)
        {
            if (type.HasFlag(PatternType.Stair))
            {
                int column = lastColumn + 1;
                if (column == totalColumns)
                    column = randomStart;
                pattern.Add(column, hitObject.StartTimeMilliseconds);
                return pattern;
            }
            if (type.HasFlag(PatternType.ReverseStair))
            {
                int column = lastColumn - 1;
                if (column == randomStart - 1)
                    column = totalColumns - 1;
                pattern.Add(column, hitObject.StartTimeMilliseconds);
                return pattern;
            }
        }
        if (type.HasFlag(PatternType.KeepSingle))
            return generateRandomNotes(hitObject, type, 1);
        if (type.HasFlag(PatternType.Mirror))
        {
            if (conversionDifficulty > 6.5)
                return mirrored(hitObject, type, 0.12, 0.38, 0.12);
            if (conversionDifficulty > 4)
                return mirrored(hitObject, type, 0.12, 0.17, 0);
            return mirrored(hitObject, type, 0.12, 0, 0);
        }

        (double p2, double p3) = conversionDifficulty switch
        {
            > 6.5 when type.HasFlag(PatternType.LowProbability) =>
                (0.78, 0.42),
            > 6.5 => (1, 0.62),
            > 4 when type.HasFlag(PatternType.LowProbability) =>
                (0.35, 0.08),
            > 4 => (0.52, 0.15),
            > 2 when type.HasFlag(PatternType.LowProbability) =>
                (0.18, 0),
            > 2 => (0.45, 0),
            _ => (0, 0),
        };
        return randomPattern(hitObject, type, p2, p3);
    }

    private Pattern randomPattern(
        ManiaConversionHitObject hitObject,
        PatternType type,
        double p2,
        double p3,
        double p4 = 0,
        double p5 = 0)
    {
        clampCircleProbabilities(ref p2, ref p3, ref p4, ref p5);
        if (hasClap(hitObject))
            p2 = 1;
        Pattern pattern = generateRandomNotes(
            hitObject,
            type,
            randomNoteCount(p2, p3, p4, p5));
        if (randomStart > 0 && hasClap(hitObject) && hasFinish(hitObject))
            pattern.Add(0, hitObject.StartTimeMilliseconds);
        return pattern;
    }

    private Pattern generateRandomNotes(
        ManiaConversionHitObject hitObject,
        PatternType type,
        int count)
    {
        var pattern = new Pattern();
        bool allowStacking =
            !type.HasFlag(PatternType.ForceNotStack);
        if (!allowStacking)
        {
            count = Math.Min(
                count,
                totalColumns
                - randomStart
                - previous.Columns.Count);
        }

        int column = columnAt(hitObject.X, allowSpecial: true);
        for (int i = 0; i < count; i++)
        {
            column = findAvailable(
                column,
                candidate => type.HasFlag(PatternType.Gathered)
                    ? candidate + 1 == totalColumns
                        ? randomStart
                        : candidate + 1
                    : random.Next(randomStart, totalColumns),
                allowStacking
                    ? [pattern]
                    : [pattern, previous]);
            pattern.Add(column, hitObject.StartTimeMilliseconds);
        }
        return pattern;
    }

    private Pattern mirrored(
        ManiaConversionHitObject hitObject,
        PatternType type,
        double centreProbability,
        double p2,
        double p3)
    {
        if (type.HasFlag(PatternType.ForceNotStack))
        {
            return randomPattern(
                hitObject,
                type,
                0.5 + p2 / 2,
                p2,
                (p2 + p3) / 2,
                p3);
        }

        switch (totalColumns)
        {
            case 2:
                centreProbability = p2 = p3 = 0;
                break;
            case 3:
                centreProbability = Math.Min(centreProbability, 0.03);
                p2 = p3 = 0;
                break;
            case 4:
                centreProbability = 0;
                p2 = 1 - Math.Max((1 - p2) * 2, 0.8);
                p3 = 0;
                break;
            case 5:
                centreProbability = Math.Min(centreProbability, 0.03);
                p3 = 0;
                break;
            case 6:
                centreProbability = 0;
                p2 = 1 - Math.Max((1 - p2) * 2, 0.5);
                p3 = 1 - Math.Max((1 - p3) * 2, 0.85);
                break;
        }
        p2 = Math.Clamp(p2, 0, 1);
        p3 = Math.Clamp(p3, 0, 1);
        double centreValue = random.NextDouble();
        int count = randomNoteCount(p2, p3);
        bool addCentre = totalColumns % 2 != 0
                         && count != 3
                         && centreValue > 1 - centreProbability;
        var pattern = new Pattern();
        int limit =
            (totalColumns % 2 == 0
                ? totalColumns
                : totalColumns - 1) / 2;
        int column = random.Next(randomStart, limit);
        for (int i = 0; i < count; i++)
        {
            column = findAvailable(
                column,
                _ => random.Next(randomStart, limit),
                [pattern],
                randomStart,
                limit);
            pattern.Add(column, hitObject.StartTimeMilliseconds);
            pattern.Add(
                randomStart + totalColumns - column - 1,
                hitObject.StartTimeMilliseconds);
        }
        if (addCentre)
            pattern.Add(totalColumns / 2, hitObject.StartTimeMilliseconds);
        if (randomStart > 0 && hasClap(hitObject) && hasFinish(hitObject))
            pattern.Add(0, hitObject.StartTimeMilliseconds);
        return pattern;
    }

    private Pattern generateSlider(
        ManiaConversionHitObject hitObject)
    {
        int start = (int)Math.Round(
            hitObject.StartTimeMilliseconds);
        int end = (int)Math.Floor(
            hitObject.EndTimeMilliseconds);
        int spans = Math.Max(1, hitObject.SpanCount);
        int segment = (end - start) / spans;
        bool lowProbability =
            !kiaiAt(hitObject.StartTimeMilliseconds);
        if (totalColumns == 1)
        {
            var one = new Pattern();
            one.Add(0, start, end);
            return one;
        }

        if (spans > 1)
        {
            if (segment <= 90)
                return randomHolds(hitObject, start, end, 1);
            if (segment <= 120)
            {
                return sliderNotes(
                    hitObject,
                    start,
                    segment,
                    spans + 1,
                    true);
            }
            if (segment <= 160)
                return sliderStair(hitObject, start, segment, spans);
            if (segment <= 200 && conversionDifficulty > 3)
            {
                return sliderMultiple(
                    hitObject,
                    start,
                    segment,
                    spans);
            }
            if (end - start >= 4000)
            {
                return randomHolds(
                    hitObject,
                    start,
                    end,
                    sliderHoldCount(
                        hitObject,
                        lowProbability,
                        0.23,
                        0,
                        0));
            }
            if (segment > 400
                && spans < totalColumns - 1 - randomStart)
            {
                return tiledHolds(
                    hitObject,
                    start,
                    segment,
                    spans);
            }
            return holdAndNormal(
                hitObject,
                start,
                end,
                segment,
                spans);
        }

        if (segment <= 110)
        {
            return sliderNotes(
                hitObject,
                start,
                segment,
                segment < 80 ? 1 : 2,
                previous.Columns.Count < totalColumns);
        }

        (double p2, double p3, double p4) = conversionDifficulty switch
        {
            > 6.5 when lowProbability => (0.78, 0.3, 0),
            > 6.5 => (0.85, 0.36, 0.03),
            > 4 when lowProbability => (0.43, 0.08, 0),
            > 4 => (0.56, 0.18, 0),
            > 2.5 when lowProbability => (0.3, 0, 0),
            > 2.5 => (0.37, 0.08, 0),
            _ when lowProbability => (0.17, 0, 0),
            _ => (0.27, 0, 0),
        };
        return randomHolds(
            hitObject,
            start,
            end,
            sliderHoldCount(
                hitObject,
                lowProbability,
                p2,
                p3,
                p4));
    }

    private Pattern randomHolds(
        ManiaConversionHitObject hitObject,
        int start,
        int end,
        int count)
    {
        var pattern = new Pattern();
        int usable = totalColumns
                     - randomStart
                     - previous.Columns.Count;
        int column = random.Next(randomStart, totalColumns);
        for (int i = 0; i < Math.Min(usable, count); i++)
        {
            column = findAvailable(
                column,
                _ => random.Next(randomStart, totalColumns),
                [pattern, previous]);
            pattern.Add(column, start, end);
        }
        for (int i = 0; i < count - usable; i++)
        {
            column = findAvailable(
                column,
                _ => random.Next(randomStart, totalColumns),
                [pattern]);
            pattern.Add(column, start, end);
        }
        return pattern;
    }

    private Pattern sliderNotes(
        ManiaConversionHitObject hitObject,
        int start,
        int segment,
        int count,
        bool forceNotStack)
    {
        var pattern = new Pattern();
        int column = columnAt(hitObject.X, allowSpecial: true);
        if (forceNotStack
            && previous.Columns.Count < totalColumns)
        {
            column = findAvailable(
                column,
                _ => random.Next(randomStart, totalColumns),
                [previous]);
        }
        int lastColumn = column;
        for (int i = 0; i < count; i++)
        {
            pattern.Add(column, start);
            column = findAvailable(
                column,
                _ => random.Next(randomStart, totalColumns),
                [new Pattern { Columns = { lastColumn } }]);
            lastColumn = column;
            start += segment;
        }
        return pattern;
    }

    private Pattern sliderStair(
        ManiaConversionHitObject hitObject,
        int start,
        int segment,
        int spans)
    {
        var pattern = new Pattern();
        int column = columnAt(hitObject.X, allowSpecial: true);
        bool increasing = random.NextDouble() > 0.5;
        for (int i = 0; i <= spans; i++)
        {
            pattern.Add(column, start);
            start += segment;
            if (increasing)
            {
                if (column >= totalColumns - 1)
                {
                    increasing = false;
                    column--;
                }
                else
                    column++;
            }
            else if (column <= randomStart)
            {
                increasing = true;
                column++;
            }
            else
                column--;
        }
        return pattern;
    }

    private Pattern sliderMultiple(
        ManiaConversionHitObject hitObject,
        int start,
        int segment,
        int spans)
    {
        var pattern = new Pattern();
        bool legacy = totalColumns is >= 4 and <= 8;
        int interval = random.Next(
            1,
            totalColumns - (legacy ? 1 : 0));
        int column = columnAt(hitObject.X, allowSpecial: true);
        for (int i = 0; i <= spans; i++)
        {
            pattern.Add(column, start);
            column += interval;
            if (column >= totalColumns - randomStart)
            {
                column = column
                         - totalColumns
                         - randomStart
                         + (legacy ? 1 : 0);
            }
            column += randomStart;
            if (totalColumns > 2)
                pattern.Add(column, start);
            column = random.Next(randomStart, totalColumns);
            start += segment;
        }
        return pattern;
    }

    private Pattern tiledHolds(
        ManiaConversionHitObject hitObject,
        int start,
        int segment,
        int spans)
    {
        var pattern = new Pattern();
        int repeat = Math.Min(spans, totalColumns);
        int end = start + segment * spans;
        int column = columnAt(hitObject.X, allowSpecial: true);
        if (previous.Columns.Count < totalColumns)
        {
            column = findAvailable(
                column,
                _ => random.Next(randomStart, totalColumns),
                [previous]);
        }
        for (int i = 0; i < repeat; i++)
        {
            column = findAvailable(
                column,
                _ => random.Next(randomStart, totalColumns),
                [pattern]);
            pattern.Add(column, start, end);
            start += segment;
        }
        return pattern;
    }

    private Pattern holdAndNormal(
        ManiaConversionHitObject hitObject,
        int start,
        int end,
        int segment,
        int spans)
    {
        var pattern = new Pattern();
        int holdColumn = columnAt(
            hitObject.X,
            allowSpecial: true);
        if (previous.Columns.Count < totalColumns)
        {
            holdColumn = findAvailable(
                holdColumn,
                _ => random.Next(randomStart, totalColumns),
                [previous]);
        }
        pattern.Add(holdColumn, start, end);
        int noteCount = conversionDifficulty switch
        {
            > 6.5 => randomNoteCount(0.63, 0),
            > 4 => randomNoteCount(
                totalColumns < 6 ? 0.12 : 0.45,
                0),
            > 2.5 => randomNoteCount(
                totalColumns < 6 ? 0 : 0.24,
                0),
            _ => 0,
        };
        noteCount = Math.Min(totalColumns - 1, noteCount);
        bool ignoreHead =
            (hitSoundAtNode(hitObject, 0) & (2 | 4 | 8)) == 0;
        int column = random.Next(randomStart, totalColumns);
        for (int row = 0; row <= spans; row++)
        {
            var rowPattern = new Pattern();
            if (!(ignoreHead && row == 0))
            {
                for (int i = 0; i < noteCount; i++)
                {
                    column = findAvailable(
                        column,
                        _ => random.Next(randomStart, totalColumns),
                        [rowPattern]);
                    if (column == holdColumn)
                    {
                        column = findAvailable(
                            column,
                            _ => random.Next(randomStart, totalColumns),
                            [rowPattern, new Pattern
                            {
                                Columns = { holdColumn },
                            }]);
                    }
                    rowPattern.Add(column, start);
                }
            }
            pattern.Objects.AddRange(rowPattern.Objects);
            pattern.Columns.UnionWith(rowPattern.Columns);
            start += segment;
        }
        return pattern;
    }

    private int sliderHoldCount(
        ManiaConversionHitObject hitObject,
        bool lowProbability,
        double p2,
        double p3,
        double p4)
    {
        switch (totalColumns)
        {
            case 2:
                p2 = p3 = p4 = 0;
                break;
            case 3:
                p2 = Math.Min(p2, 0.1);
                p3 = p4 = 0;
                break;
            case 4:
                p2 = Math.Min(p2, 0.3);
                p3 = Math.Min(p3, 0.04);
                p4 = 0;
                break;
            case 5:
                p2 = Math.Min(p2, 0.34);
                p3 = Math.Min(p3, 0.1);
                p4 = Math.Min(p4, 0.03);
                break;
        }
        if (!lowProbability
            && (hasClap(hitObject)
                || hasFinish(hitObject)
                || (hitSoundAtNode(hitObject, 0) & (4 | 8)) != 0))
        {
            p2 = 1;
        }
        return randomNoteCount(p2, p3, p4);
    }

    private static Pattern sliderEndPattern(
        Pattern pattern,
        double endTime)
    {
        if (pattern.Objects.Count == 1)
            return pattern;
        var ending = new Pattern();
        foreach (YokkoHitObject hitObject in pattern.Objects)
        {
            double objectEnd = hitObject.EndTimeMilliseconds
                               ?? hitObject.StartTimeMilliseconds;
            if (Math.Round(objectEnd) == endTime)
            {
                ending.Objects.Add(hitObject);
                ending.Columns.Add(hitObject.Lane);
            }
        }
        return ending;
    }

    private Pattern generateSpinner(
        ManiaConversionHitObject hitObject)
    {
        bool shortFinish = totalColumns == 8
                           && hasFinish(hitObject)
                           && hitObject.EndTimeMilliseconds
                           - hitObject.StartTimeMilliseconds < 1000;
        int lower = totalColumns == 8 ? randomStart : 0;
        int column = shortFinish
            ? 0
            : random.Next(lower, totalColumns);
        if (!shortFinish
            && previous.Columns.Count < totalColumns)
        {
            column = findAvailable(
                column,
                _ => random.Next(lower, totalColumns),
                [previous],
                lower,
                totalColumns);
        }
        var pattern = new Pattern();
        bool hold = hitObject.EndTimeMilliseconds
                    - hitObject.StartTimeMilliseconds >= 100;
        pattern.Add(
            column,
            hitObject.StartTimeMilliseconds,
            hold ? hitObject.EndTimeMilliseconds : null);
        return pattern;
    }

    private int findAvailable(
        int initial,
        Func<int, int> next,
        IReadOnlyList<Pattern> patterns,
        int? lower = null,
        int? upper = null)
    {
        int minimum = lower ?? randomStart;
        int maximum = upper ?? totalColumns;
        bool valid(int column) =>
            column >= minimum
            && column < maximum
            && patterns.All(pattern =>
                !pattern.Columns.Contains(column));
        if (valid(initial))
            return initial;
        if (!Enumerable.Range(minimum, maximum - minimum).Any(valid))
            return initial;
        do
            initial = next(initial);
        while (!valid(initial));
        return initial;
    }

    private int columnAt(double x, bool allowSpecial)
    {
        if (allowSpecial && totalColumns == 8)
        {
            return Math.Clamp(
                (int)Math.Floor(x / (512d / 7)),
                0,
                6) + 1;
        }
        return Math.Clamp(
            (int)Math.Floor(x / (512d / totalColumns)),
            0,
            totalColumns - 1);
    }

    private int randomNoteCount(params double[] probabilities)
    {
        double value = random.NextDouble();
        for (int i = probabilities.Length - 1; i >= 0; i--)
        {
            if (value >= 1 - probabilities[i])
                return i + 2;
        }
        return 1;
    }

    private void clampCircleProbabilities(
        ref double p2,
        ref double p3,
        ref double p4,
        ref double p5)
    {
        switch (totalColumns)
        {
            case 2:
                p2 = p3 = p4 = p5 = 0;
                break;
            case 3:
                p2 = Math.Min(p2, 0.1);
                p3 = p4 = p5 = 0;
                break;
            case 4:
                p2 = Math.Min(p2, 0.23);
                p3 = Math.Min(p3, 0.04);
                p4 = p5 = 0;
                break;
            case 5:
                p3 = Math.Min(p3, 0.15);
                p4 = Math.Min(p4, 0.03);
                p5 = 0;
                break;
        }
    }

    private double conversionDifficulty
    {
        get
        {
            double first = source.HitObjects
                                 .MinBy(static hitObject =>
                                     hitObject.StartTimeMilliseconds)?
                                 .StartTimeMilliseconds ?? 0;
            double last = source.HitObjects
                                .MaxBy(static hitObject =>
                                    hitObject.StartTimeMilliseconds)?
                                .StartTimeMilliseconds ?? 0;
            int drainTime = (int)(
                (last - first - source.TotalBreakTimeMilliseconds)
                / 1000);
            if (drainTime == 0)
                drainTime = 10000;
            double value =
                ((source.DrainRate
                  + Math.Clamp(source.ApproachRate, 4, 7)) / 1.5
                 + (double)source.HitObjects.Count / drainTime * 9)
                / 38 * 5 / 1.15;
            return Math.Min(value, 12);
        }
    }

    private void computeDensity(double time)
    {
        if (previousTimes.Count == 7)
            previousTimes.Dequeue();
        previousTimes.Enqueue(time);
        if (previousTimes.Count >= 2)
            density = (previousTimes.Last() - previousTimes.First())
                      / previousTimes.Count;
    }

    private double beatLengthAt(double time) =>
        timingPoints
            .Where(point =>
                point.Uninherited
                && point.TimeMilliseconds <= time)
            .LastOrDefault()?
            .BeatLengthMilliseconds
        ?? timingPoints.FirstOrDefault(static point => point.Uninherited)?
                       .BeatLengthMilliseconds
        ?? 500;

    private bool kiaiAt(double time) =>
        timingPoints
            .Where(point => point.TimeMilliseconds <= time)
            .LastOrDefault() is { Effects: var effects }
        && (effects & 1) != 0;

    private void record(double time, double x, double y)
    {
        lastTime = time;
        lastX = x;
        lastY = y;
    }

    private static bool hasClap(
        ManiaConversionHitObject hitObject) =>
        (hitObject.HitSound & 8) != 0;

    private static bool hasFinish(
        ManiaConversionHitObject hitObject) =>
        (hitObject.HitSound & 4) != 0;

    private static int hitSoundAtNode(
        ManiaConversionHitObject hitObject,
        int index) =>
        hitObject.NodeHitSounds is { Count: > 0 } nodeSounds
            ? nodeSounds[Math.Min(index, nodeSounds.Count - 1)]
            : hitObject.HitSound;
}
