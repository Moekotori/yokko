using System.Globalization;
using System.Text;
using Yokko.Core.Beatmaps;
using Yokko.Core.Editing;
using Yokko.Core.Gameplay;
using Yokko.Core.Timing;

namespace Yokko.Import.Osu;

public static class OsuManiaBeatmapIO
{
    private const int hitCircleType = 1;
    private const int holdType = 128;

    public static EditableBeatmap ReadEditableFromFile(string path)
    {
        return EditableBeatmap.FromBeatmap(ReadBeatmapFromFile(path), path);
    }

    public static YokkoBeatmap ReadBeatmapFromFile(string path)
    {
        YokkoBeatmap beatmap = ReadBeatmap(File.ReadAllText(path, Encoding.UTF8));
        return beatmap with { AudioPath = resolveAudioPath(path, beatmap.AudioPath) };
    }

    public static string? ReadBackgroundPathFromFile(string path)
    {
        var sections = parseSections(File.ReadAllText(path, Encoding.UTF8));

        foreach (string rawLine in sections.GetValueOrDefault("Events") ?? [])
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith("//", StringComparison.Ordinal))
                continue;

            string[] parts = line.Split(',', 4);
            if (parts.Length < 3
                || !parts[0].Trim().Equals("0", StringComparison.Ordinal)
                || !parts[1].Trim().Equals("0", StringComparison.Ordinal))
                continue;

            return ImportParsing.ResolveAdjacentAsset(path, parts[2]);
        }

        return null;
    }

    public static YokkoBeatmap ReadBeatmap(string text)
    {
        var sections = parseSections(text);
        Dictionary<string, string> general = parseKeyValueSection(sections, "General");
        Dictionary<string, string> metadata = parseKeyValueSection(sections, "Metadata");
        Dictionary<string, string> difficulty = parseKeyValueSection(sections, "Difficulty");

        if (general.TryGetValue("Mode", out string? mode) && mode.Trim() != "3")
            throw new InvalidDataException("Only osu!mania beatmaps (Mode: 3) are supported.");

        int keyCount = parseInt(difficulty.GetValueOrDefault("CircleSize"), 4);
        KeyMode keyMode = keyCount switch
        {
            4 => KeyMode.FourKey,
            7 => KeyMode.SevenKey,
            _ => throw new InvalidDataException($"Unsupported osu!mania key count: {keyCount}."),
        };

        List<YokkoHitObject> hitObjects = parseHitObjects(sections.GetValueOrDefault("HitObjects") ?? [], keyCount);
        List<YokkoTimingPoint> timingPoints = parseTimingPoints(sections.GetValueOrDefault("TimingPoints") ?? []);
        ScrollVelocityProfile scrollVelocity =
            ScrollVelocityConversion.FromOsu(timingPoints, hitObjects);
        double overallDifficulty =
            parseDouble(
                difficulty.GetValueOrDefault("OverallDifficulty"),
                5);

        return new YokkoBeatmap(
            preferredMetadataValue(metadata, "TitleUnicode", "Title", "Untitled"),
            preferredMetadataValue(metadata, "ArtistUnicode", "Artist", "Unknown Artist"),
            metadata.GetValueOrDefault("Creator", "Unknown Creator"),
            metadata.GetValueOrDefault("Version", $"{keyCount}K"),
            keyMode,
            ChartSourceFormat.OsuMania,
            timingPoints.Count == 0 ? [YokkoTimingPoint.Default] : timingPoints,
            general.GetValueOrDefault("AudioFilename"),
            hitObjects,
            overallDifficulty,
            scrollVelocity.Changes,
            scrollVelocity.InitialMultiplier);
    }

    private static string preferredMetadataValue(
        IReadOnlyDictionary<string, string> metadata,
        string preferredKey,
        string fallbackKey,
        string fallback)
    {
        string? preferred = metadata.GetValueOrDefault(preferredKey);
        if (!string.IsNullOrWhiteSpace(preferred))
            return preferred;

        string? value = metadata.GetValueOrDefault(fallbackKey);
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    public static void WriteEditableToFile(EditableBeatmap beatmap, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, WriteEditableToString(beatmap), new UTF8Encoding(false));
    }

    public static string WriteEditableToString(EditableBeatmap beatmap)
        => WriteBeatmap(beatmap.ToBeatmap());

    public static string WriteBeatmap(YokkoBeatmap beatmap)
    {
        int keyCount = (int)beatmap.KeyMode;
        var builder = new StringBuilder();

        builder.AppendLine("osu file format v14");
        builder.AppendLine();
        builder.AppendLine("[General]");
        builder.AppendLine($"AudioFilename: {formatAudioFilename(beatmap.AudioPath)}");
        builder.AppendLine("AudioLeadIn: 0");
        builder.AppendLine("PreviewTime: -1");
        builder.AppendLine("Countdown: 0");
        builder.AppendLine("SampleSet: Normal");
        builder.AppendLine("StackLeniency: 0.7");
        builder.AppendLine("Mode: 3");
        builder.AppendLine("LetterboxInBreaks: 0");
        builder.AppendLine("SpecialStyle: 0");
        builder.AppendLine("WidescreenStoryboard: 1");
        builder.AppendLine();
        builder.AppendLine("[Editor]");
        builder.AppendLine("DistanceSpacing: 1");
        builder.AppendLine("BeatDivisor: 4");
        builder.AppendLine("GridSize: 4");
        builder.AppendLine("TimelineZoom: 1");
        builder.AppendLine();
        builder.AppendLine("[Metadata]");
        builder.AppendLine($"Title:{escapeValue(beatmap.Title)}");
        builder.AppendLine($"Artist:{escapeValue(beatmap.Artist)}");
        builder.AppendLine($"Creator:{escapeValue(beatmap.Creator)}");
        builder.AppendLine($"Version:{escapeValue(beatmap.DifficultyName)}");
        builder.AppendLine("Source:Yokko");
        builder.AppendLine("Tags:yokko");
        builder.AppendLine("BeatmapID:0");
        builder.AppendLine("BeatmapSetID:-1");
        builder.AppendLine();
        builder.AppendLine("[Difficulty]");
        builder.AppendLine($"HPDrainRate:{keyCount}");
        builder.AppendLine($"CircleSize:{keyCount}");
        builder.AppendLine($"OverallDifficulty:{formatDouble(beatmap.OverallDifficulty)}");
        builder.AppendLine("ApproachRate:5");
        builder.AppendLine("SliderMultiplier:1.4");
        builder.AppendLine("SliderTickRate:1");
        builder.AppendLine();
        builder.AppendLine("[Events]");
        builder.AppendLine("//Background and Video events");
        builder.AppendLine("//Break Periods");
        builder.AppendLine("//Storyboard Layer 0 (Background)");
        builder.AppendLine("//Storyboard Layer 1 (Fail)");
        builder.AppendLine("//Storyboard Layer 2 (Pass)");
        builder.AppendLine("//Storyboard Layer 3 (Foreground)");
        builder.AppendLine("//Storyboard Layer 4 (Overlay)");
        builder.AppendLine("//Storyboard Sound Samples");
        builder.AppendLine();
        builder.AppendLine("[TimingPoints]");

        IReadOnlyList<YokkoTimingPoint> timingPoints =
            createTimingPointsForExport(beatmap);

        foreach (YokkoTimingPoint timingPoint in timingPoints.OrderBy(static point => point.TimeMilliseconds))
            builder.AppendLine(formatTimingPoint(timingPoint));

        builder.AppendLine();
        builder.AppendLine("[HitObjects]");

        foreach (YokkoHitObject hitObject in beatmap.HitObjects.OrderBy(static hitObject => hitObject.StartTimeMilliseconds).ThenBy(static hitObject => hitObject.Lane))
            builder.AppendLine(formatHitObject(hitObject, keyCount));

        return builder.ToString();
    }

    private static Dictionary<string, List<string>> parseSections(string text)
    {
        var sections = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        string? currentSection = null;

        using var reader = new StringReader(text);

        while (reader.ReadLine() is { } rawLine)
        {
            string line = rawLine.Trim();

            if (line.Length == 0)
                continue;

            if (line.StartsWith("[", StringComparison.Ordinal) && line.EndsWith("]", StringComparison.Ordinal))
            {
                currentSection = line[1..^1];
                sections[currentSection] = [];
                continue;
            }

            if (currentSection == null)
                continue;

            sections[currentSection].Add(line);
        }

        return sections;
    }

    private static Dictionary<string, string> parseKeyValueSection(Dictionary<string, List<string>> sections, string section)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (!sections.TryGetValue(section, out List<string>? lines))
            return values;

        foreach (string line in lines)
        {
            if (line.StartsWith("//", StringComparison.Ordinal))
                continue;

            int separator = line.IndexOf(':');

            if (separator < 0)
                continue;

            values[line[..separator].Trim()] = line[(separator + 1)..].Trim();
        }

        return values;
    }

    private static List<YokkoHitObject> parseHitObjects(IReadOnlyList<string> lines, int keyCount)
    {
        var hitObjects = new List<YokkoHitObject>();

        foreach (string line in lines)
        {
            if (line.StartsWith("//", StringComparison.Ordinal))
                continue;

            string[] parts = line.Split(',');

            if (parts.Length < 5)
                continue;

            int x = parseInt(parts[0], 0);
            int lane = Math.Clamp((int)Math.Floor(x * keyCount / 512d), 0, keyCount - 1);
            double startTime = parseDouble(parts[2], 0);
            int type = parseInt(parts[3], 0);

            if ((type & holdType) != 0)
            {
                double endTime = startTime;

                if (parts.Length >= 6)
                {
                    string endTimePart = parts[5].Split(':')[0];
                    endTime = parseDouble(endTimePart, startTime);
                }

                hitObjects.Add(new YokkoHitObject(lane, startTime, endTime, HitObjectKind.Hold));
                continue;
            }

            if ((type & hitCircleType) != 0)
                hitObjects.Add(new YokkoHitObject(lane, startTime, null, HitObjectKind.Tap));
        }

        hitObjects.Sort(static (left, right) =>
        {
            int timeComparison = left.StartTimeMilliseconds.CompareTo(right.StartTimeMilliseconds);
            return timeComparison != 0 ? timeComparison : left.Lane.CompareTo(right.Lane);
        });

        return hitObjects;
    }

    private static List<YokkoTimingPoint> parseTimingPoints(IReadOnlyList<string> lines)
    {
        var timingPoints = new List<YokkoTimingPoint>();

        foreach (string line in lines)
        {
            if (line.StartsWith("//", StringComparison.Ordinal))
                continue;

            string[] parts = line.Split(',');
            if (parts.Length < 2)
                continue;

            if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double time)
                || !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double beatLength)
                || Math.Abs(beatLength) < double.Epsilon)
                continue;

            timingPoints.Add(new YokkoTimingPoint(
                time,
                beatLength,
                Math.Max(1, parseInt(parts.ElementAtOrDefault(2), 4)),
                parseInt(parts.ElementAtOrDefault(3), 2),
                parseInt(parts.ElementAtOrDefault(4), 0),
                Math.Clamp(parseInt(parts.ElementAtOrDefault(5), 100), 0, 100),
                parseInt(parts.ElementAtOrDefault(6), 1) != 0,
                parseInt(parts.ElementAtOrDefault(7), 0)));
        }

        return timingPoints;
    }

    private static IReadOnlyList<YokkoTimingPoint> createTimingPointsForExport(
        YokkoBeatmap beatmap)
    {
        if (beatmap.ScrollProfiles.Count > 0
            || beatmap.HitObjects.Any(
                static hitObject => hitObject.ScrollProfileId != null))
        {
            throw new InvalidDataException(
                "osu!mania export cannot represent Quaver per-note timing groups.");
        }

        if (beatmap.ScrollSpeedFactors.Any(
                static factor => Math.Abs(factor.Multiplier - 1) > 0.0000001))
        {
            throw new InvalidDataException(
                "osu!mania export cannot represent Quaver scroll speed factors.");
        }

        IReadOnlyList<YokkoTimingPoint> sourceTimingPoints =
            beatmap.TimingPoints.Count == 0
                ? [YokkoTimingPoint.Default]
                : beatmap.TimingPoints;
        ScrollVelocityProfile sourceProfile =
            ScrollVelocityConversion.FromOsu(
                sourceTimingPoints,
                beatmap.HitObjects);
        var targetMap = new ScrollVelocityMap(
            beatmap.ScrollVelocities,
            beatmap.InitialScrollVelocity);

        if (profilesMatch(sourceProfile, beatmap))
            return sourceTimingPoints;

        if (targetMap.InitialMultiplier <= 0
            || beatmap.ScrollVelocities.Any(
                static velocity => velocity.Multiplier <= 0))
        {
            throw new InvalidDataException(
                "osu!mania export cannot represent zero or negative scroll velocities.");
        }

        YokkoTimingPoint[] uninherited = sourceTimingPoints
                                         .Where(static point => point.Uninherited)
                                         .OrderBy(static point => point.TimeMilliseconds)
                                         .ToArray();

        if (uninherited.Length == 0)
            uninherited = [YokkoTimingPoint.Default];

        double baseBeatLength =
            ScrollVelocityConversion.MostCommonBeatLength(
                uninherited,
                beatmap.HitObjects);
        YokkoTimingPoint[] inherited = sourceTimingPoints
                                       .Where(static point => !point.Uninherited)
                                       .OrderBy(static point => point.TimeMilliseconds)
                                       .ToArray();
        double firstTime = Math.Min(
            uninherited[0].TimeMilliseconds,
            beatmap.ScrollVelocities.FirstOrDefault()?.TimeMilliseconds
            ?? uninherited[0].TimeMilliseconds);
        double[] eventTimes = uninherited
                              .Select(static point => point.TimeMilliseconds)
                              .Concat(inherited.Select(static point => point.TimeMilliseconds))
                              .Concat(beatmap.ScrollVelocities.Select(
                                  static velocity => velocity.TimeMilliseconds))
                              .Append(firstTime)
                              .Distinct()
                              .Order()
                              .ToArray();
        double currentBeatLength =
            uninherited[0].BeatLengthMilliseconds > 0
                ? uninherited[0].BeatLengthMilliseconds
                : YokkoTimingPoint.Default.BeatLengthMilliseconds;
        double effectiveInheritedMultiplier = 1;
        var exported = new List<YokkoTimingPoint>();

        foreach (double eventTime in eventTimes)
        {
            YokkoTimingPoint[] redPoints = uninherited
                                           .Where(point =>
                                               point.TimeMilliseconds
                                               == eventTime)
                                           .ToArray();

            if (redPoints.Length > 0)
            {
                exported.AddRange(redPoints);
                YokkoTimingPoint activeRed = redPoints[^1];

                if (activeRed.BeatLengthMilliseconds > 0)
                {
                    currentBeatLength =
                        activeRed.BeatLengthMilliseconds;
                }

                effectiveInheritedMultiplier = 1;
            }

            double targetMultiplier = targetMap.MultiplierAt(eventTime);
            double requiredInheritedMultiplier =
                targetMultiplier * currentBeatLength / baseBeatLength;

            if (!double.IsFinite(requiredInheritedMultiplier)
                || requiredInheritedMultiplier is < 0.01 or > 10)
            {
                throw new InvalidDataException(
                    $"osu!mania export cannot represent scroll velocity {targetMultiplier:0.###}x at {eventTime:0.###} ms.");
            }

            YokkoTimingPoint[] greenPoints = inherited
                                             .Where(point =>
                                                 point.TimeMilliseconds
                                                 == eventTime)
                                             .ToArray();

            if (greenPoints.Length > 0)
            {
                foreach (YokkoTimingPoint greenPoint in greenPoints)
                {
                    exported.Add(greenPoint with
                    {
                        BeatLengthMilliseconds =
                            -100 / requiredInheritedMultiplier,
                    });
                }

                effectiveInheritedMultiplier =
                    requiredInheritedMultiplier;
                continue;
            }

            if (Math.Abs(
                    requiredInheritedMultiplier
                    - effectiveInheritedMultiplier)
                <= 0.0000001)
            {
                continue;
            }

            YokkoTimingPoint metadata = sourceTimingPoints
                                        .Where(point =>
                                            point.TimeMilliseconds
                                            <= eventTime)
                                        .LastOrDefault()
                                        ?? YokkoTimingPoint.Default;
            exported.Add(new YokkoTimingPoint(
                eventTime,
                -100 / requiredInheritedMultiplier,
                metadata.Meter,
                metadata.SampleSet,
                metadata.SampleIndex,
                metadata.Volume,
                Uninherited: false,
                metadata.Effects));
            effectiveInheritedMultiplier =
                requiredInheritedMultiplier;
        }

        return exported.OrderBy(static point => point.TimeMilliseconds)
                       .ThenByDescending(static point => point.Uninherited)
                       .ToArray();
    }

    private static bool profilesMatch(
        ScrollVelocityProfile sourceProfile,
        YokkoBeatmap beatmap)
    {
        if (Math.Abs(
                sourceProfile.InitialMultiplier
                - beatmap.InitialScrollVelocity)
            > 0.0000001)
        {
            return false;
        }

        YokkoScrollVelocity[] targetChanges = beatmap.ScrollVelocities
                                                     .OrderBy(
                                                         static velocity =>
                                                             velocity.TimeMilliseconds)
                                                     .GroupBy(
                                                         static velocity =>
                                                             velocity.TimeMilliseconds)
                                                     .Select(
                                                         static group =>
                                                             group.Last())
                                                     .ToArray();

        if (sourceProfile.Changes.Count != targetChanges.Length)
            return false;

        for (int i = 0; i < targetChanges.Length; i++)
        {
            YokkoScrollVelocity source = sourceProfile.Changes[i];
            YokkoScrollVelocity target = targetChanges[i];

            if (Math.Abs(
                    source.TimeMilliseconds
                    - target.TimeMilliseconds)
                > 0.0000001
                || Math.Abs(source.Multiplier - target.Multiplier)
                > 0.0000001)
            {
                return false;
            }
        }

        return true;
    }

    private static string formatTimingPoint(YokkoTimingPoint timingPoint)
        => string.Join(",",
            formatDouble(timingPoint.TimeMilliseconds),
            formatDouble(timingPoint.BeatLengthMilliseconds),
            timingPoint.Meter.ToString(CultureInfo.InvariantCulture),
            timingPoint.SampleSet.ToString(CultureInfo.InvariantCulture),
            timingPoint.SampleIndex.ToString(CultureInfo.InvariantCulture),
            timingPoint.Volume.ToString(CultureInfo.InvariantCulture),
            timingPoint.Uninherited ? "1" : "0",
            timingPoint.Effects.ToString(CultureInfo.InvariantCulture));

    private static string formatHitObject(YokkoHitObject hitObject, int keyCount)
    {
        int x = laneToX(hitObject.Lane, keyCount);
        int time = roundMilliseconds(hitObject.StartTimeMilliseconds);

        if (hitObject.Kind == HitObjectKind.Hold && hitObject.EndTimeMilliseconds != null)
            return $"{x},192,{time},{holdType},0,{roundMilliseconds(hitObject.EndTimeMilliseconds.Value)}:0:0:0:0:";

        return $"{x},192,{time},{hitCircleType},0,0:0:0:0:";
    }

    private static int laneToX(int lane, int keyCount)
    {
        lane = Math.Clamp(lane, 0, keyCount - 1);
        return (int)Math.Floor((lane + 0.5) * 512 / keyCount);
    }

    private static int roundMilliseconds(double value)
        => (int)Math.Round(value, MidpointRounding.AwayFromZero);

    private static string formatDouble(double value)
        => value.ToString("0.###############", CultureInfo.InvariantCulture);

    private static int parseInt(string? value, int fallback)
        => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) ? parsed : fallback;

    private static double parseDouble(string? value, double fallback)
        => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed) ? parsed : fallback;

    private static string escapeValue(string value)
        => value.ReplaceLineEndings(" ").Trim();

    private static string formatAudioFilename(string? audioPath)
        => string.IsNullOrWhiteSpace(audioPath)
            ? "audio.mp3"
            : escapeValue(Path.GetFileName(audioPath));

    private static string? resolveAudioPath(string beatmapPath, string? audioPath)
    {
        if (string.IsNullOrWhiteSpace(audioPath))
            return audioPath;

        if (Path.IsPathRooted(audioPath))
            return audioPath;

        string? beatmapDirectory = Path.GetDirectoryName(Path.GetFullPath(beatmapPath));
        if (beatmapDirectory == null)
            return audioPath;

        string resolvedAudioPath = Path.GetFullPath(Path.Combine(beatmapDirectory, audioPath));
        return File.Exists(resolvedAudioPath) ? resolvedAudioPath : audioPath;
    }
}
