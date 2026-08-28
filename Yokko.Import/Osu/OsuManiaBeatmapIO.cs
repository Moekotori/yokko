using System.Globalization;
using System.Text;
using Yokko.Core.Beatmaps;
using Yokko.Core.Editing;
using Yokko.Core.Gameplay;
using Yokko.Core.Mods;
using Yokko.Core.Timing;

namespace Yokko.Import.Osu;

public static class OsuManiaBeatmapIO
{
    public const long MaximumFileBytes = 16L * 1024 * 1024;
    public const int MaximumLineCount = 500_000;
    public const int MaximumHitObjectLineCount = 250_000;
    private const int hitCircleType = 1;
    private const int sliderType = 2;
    private const int spinnerType = 8;
    private const int holdType = 128;
    private const double controlPointLeniency = 5;

    public static EditableBeatmap ReadEditableFromFile(string path)
    {
        return EditableBeatmap.FromBeatmap(ReadBeatmapFromFile(path), path);
    }

    public static YokkoBeatmap ReadBeatmapFromFile(
        string path,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        YokkoBeatmap beatmap = ReadBeatmap(
            readTextFromFileWithinBudget(path),
            cancellationToken);
        return beatmap with { AudioPath = resolveAudioPath(path, beatmap.AudioPath) };
    }

    public static string? ReadBackgroundPathFromFile(
        string path,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var sections = parseSections(
            readTextFromFileWithinBudget(path),
            cancellationToken);

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

    public static YokkoBeatmap ReadBeatmap(
        string text,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        int formatVersion = parseFormatVersion(text);
        double legacyTimingOffset = formatVersion < 5 ? 24 : 0;
        var sections = parseSections(text, cancellationToken);
        Dictionary<string, string> general = parseKeyValueSection(sections, "General");
        Dictionary<string, string> metadata = parseKeyValueSection(sections, "Metadata");
        Dictionary<string, string> difficulty = parseKeyValueSection(sections, "Difficulty");

        string mode = general.GetValueOrDefault("Mode", "0").Trim();
        if (mode is not "0" and not "3")
        {
            throw new InvalidDataException(
                $"Only osu!standard (Mode: 0) and osu!mania (Mode: 3) beatmaps are supported; received Mode: {mode}.");
        }

        int defaultSampleSet = parseSampleSet(
            general.GetValueOrDefault("SampleSet"));
        int defaultSampleVolume = Math.Clamp(
            parseInt(general.GetValueOrDefault("SampleVolume"), 100),
            0,
            100);
        List<YokkoTimingPoint> timingPoints = parseTimingPoints(
            sections.GetValueOrDefault("TimingPoints") ?? [],
            defaultSampleSet,
            defaultSampleVolume,
            legacyTimingOffset);
        if (timingPoints.Count == 0)
        {
            timingPoints.Add(
                new YokkoTimingPoint(
                    0,
                    500,
                    SampleSet: normaliseSampleSet(defaultSampleSet),
                    Volume: defaultSampleVolume));
        }
        double overallDifficulty =
            parseDouble(
                difficulty.GetValueOrDefault("OverallDifficulty"),
                5);
        if (!double.IsFinite(overallDifficulty)
            || overallDifficulty is < 0 or > 10)
        {
            throw new ArgumentOutOfRangeException(
                nameof(overallDifficulty),
                "osu!mania source OD must be between 0 and 10.");
        }
        double drainRate =
            parseDouble(
                difficulty.GetValueOrDefault("HPDrainRate"),
                5);
        double circleSize =
            parseDouble(difficulty.GetValueOrDefault("CircleSize"), 4);
        double approachRate =
            parseDouble(
                difficulty.GetValueOrDefault("ApproachRate"),
                overallDifficulty);
        List<YokkoBreakPeriod> breakPeriods = parseBreakPeriods(
            sections.GetValueOrDefault("Events") ?? [],
            legacyTimingOffset);
        ManiaConversionSource? conversionSource = null;
        int keyCount;
        int stageCount = 1;
        List<YokkoHitObject> hitObjects;
        ChartSourceFormat sourceFormat;
        if (mode == "3")
        {
            keyCount = (int)Math.Round(circleSize);
            if (keyCount is < 1 or > 20
                || keyCount > 10 && keyCount % 2 != 0)
            {
                throw new InvalidDataException(
                    $"Unsupported osu!mania key count: {keyCount}. Expected 1-10K or an even dual-stage 12-20K.");
            }
            stageCount = keyCount > 10 ? 2 : 1;

            hitObjects = parseHitObjects(
                sections.GetValueOrDefault("HitObjects") ?? [],
                keyCount,
                timingPoints,
                parseDouble(
                    difficulty.GetValueOrDefault("SliderMultiplier"),
                    1.4),
                circleSize,
                overallDifficulty,
                approachRate,
                drainRate,
                legacyTimingOffset,
                cancellationToken);
            sourceFormat = ChartSourceFormat.OsuMania;
        }
        else
        {
            validateStandardDifficulty(circleSize, nameof(circleSize));
            validateStandardDifficulty(approachRate, nameof(approachRate));
            conversionSource = new ManiaConversionSource(
                circleSize,
                overallDifficulty,
                approachRate,
                drainRate,
                parseStandardHitObjects(
                    sections.GetValueOrDefault("HitObjects") ?? [],
                    timingPoints,
                    parseDouble(
                        difficulty.GetValueOrDefault("SliderMultiplier"),
                        1.4),
                    legacyTimingOffset),
                breakPeriods.Sum(static period =>
                    period.DurationMilliseconds));
            keyCount =
                OsuStandardManiaConverter.DetermineDefaultColumnCount(
                    conversionSource);
            hitObjects = OsuStandardManiaConverter
                         .Convert(
                             conversionSource,
                             keyCount,
                             timingPoints)
                         .ToList();
            sourceFormat = ChartSourceFormat.OsuStandard;
        }
        KeyMode keyMode = (KeyMode)keyCount;
        ScrollVelocityProfile scrollVelocity =
            ScrollVelocityConversion.FromOsu(
                timingPoints,
                hitObjects,
                // lazer resets effect control points when converting a
                // non-mania beatmap: BPM changes remain, inherited SV does not.
                applyInheritedScrollSpeed:
                    sourceFormat == ChartSourceFormat.OsuMania);
        double previewTime = parseDouble(
            general.GetValueOrDefault("PreviewTime"),
            -1);
        if (previewTime >= 0)
            previewTime += legacyTimingOffset;

        double localOffset = parseDouble(
            general.GetValueOrDefault("Offset"),
            0);

        string romanisedTitle = metadataValue(metadata, "Title", "Untitled");
        string romanisedArtist = metadataValue(
            metadata,
            "Artist",
            "Unknown Artist");
        return new YokkoBeatmap(
            preferredMetadataValue(metadata, "TitleUnicode", romanisedTitle),
            preferredMetadataValue(metadata, "ArtistUnicode", romanisedArtist),
            metadata.GetValueOrDefault("Creator", "Unknown Creator"),
            metadata.GetValueOrDefault("Version", $"{keyCount}K"),
            keyMode,
            sourceFormat,
            timingPoints.Count == 0 ? [YokkoTimingPoint.Default] : timingPoints,
            general.GetValueOrDefault("AudioFilename"),
            hitObjects,
            overallDifficulty,
            scrollVelocity.Changes,
            scrollVelocity.InitialMultiplier,
            DrainRate: drainRate,
            ConversionSource: conversionSource,
            StageCount: stageCount,
            PreviewTimeMilliseconds: previewTime,
            LocalOffsetMilliseconds: localOffset,
            BreakPeriods: breakPeriods,
            RomanisedTitle: romanisedTitle,
            RomanisedArtist: romanisedArtist,
            Source: metadata.GetValueOrDefault("Source", string.Empty),
            Tags: metadata.GetValueOrDefault("Tags", string.Empty),
            OnlineBeatmapId: parseInt(
                metadata.GetValueOrDefault("BeatmapID"),
                -1),
            OnlineBeatmapSetId: parseInt(
                metadata.GetValueOrDefault("BeatmapSetID"),
                -1));
    }

    private static int parseFormatVersion(string text)
    {
        using var reader = new StringReader(text);

        while (reader.ReadLine() is { } rawLine)
        {
            string line = rawLine.Trim().TrimStart('\uFEFF');
            if (line.Length == 0)
                continue;

            const string prefix = "osu file format v";
            return line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                   && int.TryParse(
                       line[prefix.Length..],
                       NumberStyles.Integer,
                       CultureInfo.InvariantCulture,
                       out int version)
                ? version
                : 14;
        }

        return 14;
    }

    private static string preferredMetadataValue(
        IReadOnlyDictionary<string, string> metadata,
        string preferredKey,
        string fallback)
    {
        string? preferred = metadata.GetValueOrDefault(preferredKey);
        if (!string.IsNullOrWhiteSpace(preferred))
            return preferred;

        return fallback;
    }

    private static string metadataValue(
        IReadOnlyDictionary<string, string> metadata,
        string key,
        string fallback)
    {
        string? value = metadata.GetValueOrDefault(key);
        return metadataValue(value, fallback);
    }

    private static string metadataValue(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value;

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
        builder.AppendLine($"Offset: {formatDouble(beatmap.LocalOffsetMilliseconds)}");
        builder.AppendLine(
            $"PreviewTime: {formatDouble(beatmap.PreviewTimeMilliseconds)}");
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
        builder.AppendLine($"Title:{escapeValue(metadataValue(beatmap.RomanisedTitle, beatmap.Title))}");
        builder.AppendLine($"TitleUnicode:{escapeValue(beatmap.Title)}");
        builder.AppendLine($"Artist:{escapeValue(metadataValue(beatmap.RomanisedArtist, beatmap.Artist))}");
        builder.AppendLine($"ArtistUnicode:{escapeValue(beatmap.Artist)}");
        builder.AppendLine($"Creator:{escapeValue(beatmap.Creator)}");
        builder.AppendLine($"Version:{escapeValue(beatmap.DifficultyName)}");
        builder.AppendLine($"Source:{escapeValue(metadataValue(beatmap.Source, "Yokko"))}");
        builder.AppendLine($"Tags:{escapeValue(metadataValue(beatmap.Tags, "yokko"))}");
        builder.AppendLine($"BeatmapID:{beatmap.OnlineBeatmapId}");
        builder.AppendLine($"BeatmapSetID:{beatmap.OnlineBeatmapSetId}");
        builder.AppendLine();
        builder.AppendLine("[Difficulty]");
        builder.AppendLine($"HPDrainRate:{formatDouble(beatmap.DrainRate)}");
        builder.AppendLine($"CircleSize:{keyCount}");
        builder.AppendLine($"OverallDifficulty:{formatDouble(beatmap.OverallDifficulty)}");
        builder.AppendLine("ApproachRate:5");
        builder.AppendLine("SliderMultiplier:1.4");
        builder.AppendLine("SliderTickRate:1");
        builder.AppendLine();
        builder.AppendLine("[Events]");
        builder.AppendLine("//Background and Video events");
        builder.AppendLine("//Break Periods");
        foreach (YokkoBreakPeriod breakPeriod in
                 beatmap.BreakPeriods.OrderBy(static period =>
                     period.StartTimeMilliseconds))
        {
            builder.AppendLine(
                $"2,{formatDouble(breakPeriod.StartTimeMilliseconds)},"
                + formatDouble(breakPeriod.EndTimeMilliseconds));
        }
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

    private static Dictionary<string, List<string>> parseSections(
        string text,
        CancellationToken cancellationToken = default)
    {
        var sections = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        string? currentSection = null;
        int lineCount = 0;
        int hitObjectLineCount = 0;

        using var reader = new StringReader(text);

        while (reader.ReadLine() is { } rawLine)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (++lineCount > MaximumLineCount)
            {
                throw new InvalidDataException(
                    $"osu! beatmap exceeds the {MaximumLineCount:N0}-line safety limit.");
            }

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

            if (currentSection.Equals(
                    "HitObjects",
                    StringComparison.OrdinalIgnoreCase)
                && !line.StartsWith("//", StringComparison.Ordinal)
                && ++hitObjectLineCount > MaximumHitObjectLineCount)
            {
                throw new InvalidDataException(
                    $"osu! beatmap exceeds the {MaximumHitObjectLineCount:N0}-object safety limit.");
            }

            sections[currentSection].Add(line);
        }

        return sections;
    }

    private static string readTextFromFileWithinBudget(string path)
    {
        var info = new FileInfo(path);
        if (info.Length > MaximumFileBytes)
        {
            throw new InvalidDataException(
                $"osu! beatmap '{path}' is {info.Length:N0} bytes; "
                + $"the safety limit is {MaximumFileBytes:N0} bytes.");
        }

        string text = File.ReadAllText(path, Encoding.UTF8);
        // Protect against a file growing between the metadata check and read.
        if (text.Length > MaximumFileBytes)
        {
            throw new InvalidDataException(
                $"osu! beatmap '{path}' grew beyond the "
                + $"{MaximumFileBytes:N0}-byte safety limit while reading.");
        }

        return text;
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

    private static List<YokkoHitObject> parseHitObjects(
        IReadOnlyList<string> lines,
        int keyCount,
        IReadOnlyList<YokkoTimingPoint> timingPoints,
        double sliderMultiplier,
        double circleSize,
        double overallDifficulty,
        double approachRate,
        double drainRate,
        double timingOffset,
        CancellationToken cancellationToken)
    {
        if (!double.IsFinite(sliderMultiplier)
            || sliderMultiplier <= 0)
        {
            throw new InvalidDataException(
                "osu!mania SliderMultiplier must be positive.");
        }

        var hitObjects = new List<YokkoHitObject>();
        var legacySpinners = new List<ManiaConversionHitObject>();

        foreach (string line in lines)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (line.StartsWith("//", StringComparison.Ordinal))
                continue;

            string[] parts = line.Split(',');

            if (parts.Length < 5)
                continue;

            int x = parseInt(parts[0], 0);
            int lane = Math.Clamp((int)Math.Floor(x * keyCount / 512d), 0, keyCount - 1);
            double startTime = parseDouble(parts[2], 0) + timingOffset;
            int type = parseInt(parts[3], 0);
            int hitSound = parseInt(parts[4], 0);

            if ((type & holdType) != 0)
            {
                double endTime = startTime;
                LegacySampleBankInfo sampleBank = new();

                if (parts.Length >= 6)
                {
                    string[] holdParts = parts[5].Split(':');
                    string endTimePart = holdParts[0];
                    endTime = parseDouble(
                        endTimePart,
                        startTime - timingOffset) + timingOffset;
                    sampleBank = parseSampleBankInfo(
                        string.Join(':', holdParts.Skip(1)));
                }

                IReadOnlyList<YokkoHitSample> samples = createSamples(
                    hitSound,
                    sampleBank,
                    samplePointAt(
                        timingPoints,
                        endTime + controlPointLeniency));
                hitObjects.Add(
                    new YokkoHitObject(
                        lane,
                        startTime,
                        endTime,
                        HitObjectKind.Hold,
                        sampleBank.Filename,
                        SamplePayload: defaultHoldPayload(samples)));
                continue;
            }

            if ((type & sliderType) != 0 && parts.Length >= 8)
            {
                int spanCount = Math.Max(1, parseInt(parts[6], 1));
                double pixelLength = Math.Max(0, parseDouble(parts[7], 0));
                double endTime = startTime
                                 + sliderDuration(
                                     startTime,
                                     pixelLength,
                                     spanCount,
                                     sliderMultiplier,
                                     timingPoints);
                LegacySampleBankInfo sampleBank =
                    parseSampleBankInfo(
                        parts.ElementAtOrDefault(10),
                        banksOnly: true);
                IReadOnlyList<YokkoHitSample> samples = createSamples(
                    hitSound,
                    sampleBank,
                    samplePointAt(
                        timingPoints,
                        startTime + controlPointLeniency + 1));
                IReadOnlyList<IReadOnlyList<YokkoHitSample>> nodeSamples =
                    createSliderNodeSamples(
                        parts,
                        hitSound,
                        sampleBank,
                        startTime,
                        endTime,
                        spanCount,
                        timingPoints);
                hitObjects.Add(
                    new YokkoHitObject(
                        lane,
                        startTime,
                        endTime,
                        HitObjectKind.Hold,
                        SamplePayload: new YokkoHitSamplePayload(
                            samples,
                            nodeSamples,
                            PlaySlidingSamples: true)));
                continue;
            }

            if ((type & spinnerType) != 0 && parts.Length >= 6)
            {
                double endTime = Math.Max(
                    startTime,
                    parseDouble(
                        parts[5],
                        startTime - timingOffset) + timingOffset);
                LegacySampleBankInfo sampleBank =
                    parseSampleBankInfo(parts.ElementAtOrDefault(6));
                IReadOnlyList<YokkoHitSample> samples = createSamples(
                    hitSound,
                    sampleBank,
                    samplePointAt(
                        timingPoints,
                        endTime + controlPointLeniency));
                legacySpinners.Add(
                    new ManiaConversionHitObject(
                        256,
                        startTime,
                        endTime,
                        ManiaConversionObjectKind.Spinner,
                        hitSound,
                        Y: 192,
                        Samples: samples));
                continue;
            }

            if ((type & hitCircleType) != 0)
            {
                LegacySampleBankInfo sampleBank =
                    parseSampleBankInfo(parts.ElementAtOrDefault(5));
                IReadOnlyList<YokkoHitSample> samples = createSamples(
                    hitSound,
                    sampleBank,
                    samplePointAt(
                        timingPoints,
                        startTime + controlPointLeniency));
                hitObjects.Add(
                    new YokkoHitObject(
                        lane,
                        startTime,
                        null,
                        HitObjectKind.Tap,
                        sampleBank.Filename,
                        SamplePayload: new YokkoHitSamplePayload(samples)));
            }
        }

        if (legacySpinners.Count > 0)
        {
            // Old mania-specific maps can contain legacy spinner objects.
            // lazer routes these through SpinnerPatternGenerator rather than
            // passing them through. Reusing the pinned legacy generator keeps
            // its seed and column sequence identical.
            hitObjects.AddRange(
                OsuStandardManiaConverter.Convert(
                    new ManiaConversionSource(
                        circleSize,
                        overallDifficulty,
                        approachRate,
                        drainRate,
                        legacySpinners),
                    keyCount,
                    timingPoints));
        }

        hitObjects.Sort(static (left, right) =>
        {
            int timeComparison = left.StartTimeMilliseconds.CompareTo(right.StartTimeMilliseconds);
            return timeComparison != 0 ? timeComparison : left.Lane.CompareTo(right.Lane);
        });

        return hitObjects;
    }

    private static IReadOnlyList<ManiaConversionHitObject>
        parseStandardHitObjects(
            IReadOnlyList<string> lines,
            IReadOnlyList<YokkoTimingPoint> timingPoints,
            double sliderMultiplier,
            double timingOffset)
    {
        if (!double.IsFinite(sliderMultiplier)
            || sliderMultiplier <= 0)
        {
            throw new InvalidDataException(
                "osu!standard SliderMultiplier must be positive.");
        }

        var hitObjects = new List<ManiaConversionHitObject>();
        foreach (string line in lines)
        {
            if (line.StartsWith("//", StringComparison.Ordinal))
                continue;

            string[] parts = line.Split(',');
            if (parts.Length < 5)
                continue;

            double x = Math.Clamp(parseDouble(parts[0], 0), 0, 512);
            double y = Math.Clamp(parseDouble(parts[1], 192), 0, 384);
            double startTime = parseDouble(parts[2], 0) + timingOffset;
            int type = parseInt(parts[3], 0);
            int hitSound = parseInt(parts[4], 0);
            if ((type & hitCircleType) != 0)
            {
                LegacySampleBankInfo sampleBank =
                    parseSampleBankInfo(parts.ElementAtOrDefault(5));
                IReadOnlyList<YokkoHitSample> samples = createSamples(
                    hitSound,
                    sampleBank,
                    samplePointAt(
                        timingPoints,
                        startTime + controlPointLeniency));
                hitObjects.Add(new ManiaConversionHitObject(
                    x,
                    startTime,
                    startTime,
                    ManiaConversionObjectKind.Circle,
                    hitSound,
                    Y: y,
                    Samples: samples));
                continue;
            }

            if ((type & sliderType) != 0 && parts.Length >= 8)
            {
                int spanCount = Math.Max(1, parseInt(parts[6], 1));
                double pixelLength = Math.Max(0, parseDouble(parts[7], 0));
                IReadOnlyList<int>? nodeHitSounds =
                    parts.Length >= 9
                        ? parts[8]
                          .Split('|')
                          .Select(value => parseInt(value, hitSound))
                          .ToArray()
                        : null;
                double endTime = startTime
                                 + sliderDuration(
                                     startTime,
                                     pixelLength,
                                     spanCount,
                                     sliderMultiplier,
                                     timingPoints);
                LegacySampleBankInfo sampleBank =
                    parseSampleBankInfo(
                        parts.ElementAtOrDefault(10),
                        banksOnly: true);
                IReadOnlyList<YokkoHitSample> samples = createSamples(
                    hitSound,
                    sampleBank,
                    samplePointAt(
                        timingPoints,
                        startTime + controlPointLeniency + 1));
                IReadOnlyList<IReadOnlyList<YokkoHitSample>> nodeSamples =
                    createSliderNodeSamples(
                        parts,
                        hitSound,
                        sampleBank,
                        startTime,
                        endTime,
                        spanCount,
                        timingPoints);
                hitObjects.Add(new ManiaConversionHitObject(
                    x,
                    startTime,
                    endTime,
                    ManiaConversionObjectKind.Slider,
                    hitSound,
                    spanCount,
                    y,
                    nodeHitSounds,
                    samples,
                    nodeSamples));
                continue;
            }

            if ((type & spinnerType) != 0 && parts.Length >= 6)
            {
                double endTime = Math.Max(
                    startTime,
                    parseDouble(
                        parts[5],
                        startTime - timingOffset) + timingOffset);
                LegacySampleBankInfo sampleBank =
                    parseSampleBankInfo(parts.ElementAtOrDefault(6));
                IReadOnlyList<YokkoHitSample> samples = createSamples(
                    hitSound,
                    sampleBank,
                    samplePointAt(
                        timingPoints,
                        endTime + controlPointLeniency));
                hitObjects.Add(new ManiaConversionHitObject(
                    256,
                    startTime,
                    endTime,
                    ManiaConversionObjectKind.Spinner,
                    hitSound,
                    Y: 192,
                    Samples: samples));
            }
        }

        return hitObjects
               .OrderBy(static hitObject =>
                   hitObject.StartTimeMilliseconds)
               .ThenBy(static hitObject => hitObject.X)
               .ToArray();
    }

    private static YokkoHitSamplePayload defaultHoldPayload(
        IReadOnlyList<YokkoHitSample> samples) =>
        new(
            samples,
            [
                samples,
                [],
            ]);

    private static IReadOnlyList<IReadOnlyList<YokkoHitSample>>
        createSliderNodeSamples(
            IReadOnlyList<string> parts,
            int defaultHitSound,
            LegacySampleBankInfo defaultBank,
            double startTime,
            double endTime,
            int spanCount,
            IReadOnlyList<YokkoTimingPoint> timingPoints)
    {
        int nodeCount = spanCount + 1;
        var banks = Enumerable
                    .Range(0, nodeCount)
                    .Select(_ => defaultBank)
                    .ToArray();
        string? edgeSets = parts.ElementAtOrDefault(9);
        if (!string.IsNullOrWhiteSpace(edgeSets))
        {
            string[] values = edgeSets.Split('|');
            for (int index = 0;
                 index < Math.Min(values.Length, banks.Length);
                 index++)
            {
                banks[index] = parseSampleBankInfo(values[index]);
            }
        }

        int[] hitSounds = Enumerable
                          .Repeat(defaultHitSound, nodeCount)
                          .ToArray();
        string? edgeSounds = parts.ElementAtOrDefault(8);
        if (!string.IsNullOrWhiteSpace(edgeSounds))
        {
            string[] values = edgeSounds.Split('|');
            for (int index = 0;
                 index < Math.Min(values.Length, hitSounds.Length);
                 index++)
            {
                hitSounds[index] = parseInt(values[index], defaultHitSound);
            }
        }

        double segmentDuration =
            (endTime - startTime) / Math.Max(1, spanCount);
        return Enumerable.Range(0, nodeCount)
                         .Select(index =>
                             (IReadOnlyList<YokkoHitSample>)createSamples(
                                 hitSounds[index],
                                 banks[index],
                                 samplePointAt(
                                     timingPoints,
                                     startTime
                                     + segmentDuration * index
                                     + controlPointLeniency)))
                         .ToArray();
    }

    private static IReadOnlyList<YokkoHitSample> createSamples(
        int hitSound,
        LegacySampleBankInfo bankInfo,
        YokkoTimingPoint samplePoint)
    {
        string controlBank = sampleBankName(samplePoint.SampleSet);
        string normalBank = bankInfo.NormalBank ?? controlBank;
        string additionBank = bankInfo.AdditionBank ?? normalBank;
        int volume = bankInfo.Volume > 0
            ? bankInfo.Volume
            : Math.Clamp(samplePoint.Volume, 0, 100);
        int customSampleBank = bankInfo.CustomSampleBank > 0
            ? bankInfo.CustomSampleBank
            : Math.Max(0, samplePoint.SampleIndex);
        var samples = new List<YokkoHitSample>
        {
            new(
                YokkoHitSample.HitNormal,
                normalBank,
                volume,
                customSampleBank,
                bankInfo.Filename,
                IsLayered: hitSound != 0 && (hitSound & 1) == 0),
        };

        if ((hitSound & 4) != 0)
        {
            samples.Add(
                new YokkoHitSample(
                    YokkoHitSample.HitFinish,
                    additionBank,
                    volume,
                    customSampleBank));
        }
        if ((hitSound & 2) != 0)
        {
            samples.Add(
                new YokkoHitSample(
                    YokkoHitSample.HitWhistle,
                    additionBank,
                    volume,
                    customSampleBank));
        }
        if ((hitSound & 8) != 0)
        {
            samples.Add(
                new YokkoHitSample(
                    YokkoHitSample.HitClap,
                    additionBank,
                    volume,
                    customSampleBank));
        }

        return samples;
    }

    private static LegacySampleBankInfo parseSampleBankInfo(
        string? value,
        bool banksOnly = false)
    {
        if (string.IsNullOrEmpty(value))
            return new LegacySampleBankInfo();

        string[] parts = value.Split(':');
        string? normalBank = explicitSampleBank(
            parseInt(parts.ElementAtOrDefault(0), 0));
        string? additionBank = explicitSampleBank(
            parseInt(parts.ElementAtOrDefault(1), 0))
            ?? normalBank;
        if (banksOnly)
        {
            return new LegacySampleBankInfo(
                normalBank,
                additionBank);
        }

        return new LegacySampleBankInfo(
            normalBank,
            additionBank,
            Math.Max(0, parseInt(parts.ElementAtOrDefault(2), 0)),
            Math.Max(0, parseInt(parts.ElementAtOrDefault(3), 0)),
            parts.ElementAtOrDefault(4));
    }

    private static YokkoTimingPoint samplePointAt(
        IReadOnlyList<YokkoTimingPoint> timingPoints,
        double time)
    {
        return timingPoints
                   .Select((point, index) => (point, index))
                   .Where(item =>
                       item.point.TimeMilliseconds <= time)
                   .OrderBy(item =>
                       item.point.TimeMilliseconds)
                   .ThenBy(item => item.index)
                   .Select(item => item.point)
                   .LastOrDefault()
               ?? YokkoTimingPoint.Default;
    }

    private static string? explicitSampleBank(int sampleSet) =>
        sampleSet switch
        {
            1 => YokkoHitSample.BankNormal,
            2 => YokkoHitSample.BankSoft,
            3 => YokkoHitSample.BankDrum,
            _ => null,
        };

    private static string sampleBankName(int sampleSet) =>
        explicitSampleBank(sampleSet) ?? YokkoHitSample.BankNormal;

    private sealed record LegacySampleBankInfo(
        string? NormalBank = null,
        string? AdditionBank = null,
        int CustomSampleBank = 0,
        int Volume = 0,
        string? Filename = null);

    private static List<YokkoBreakPeriod> parseBreakPeriods(
        IReadOnlyList<string> lines,
        double timingOffset)
    {
        var periods = new List<YokkoBreakPeriod>();
        foreach (string line in lines)
        {
            string[] parts = line.Split(',');
            if (parts.Length < 3
                || !parts[0].Trim().Equals(
                    "2",
                    StringComparison.Ordinal))
            {
                continue;
            }

            double start = parseDouble(parts[1], 0) + timingOffset;
            double end = parseDouble(
                parts[2],
                start - timingOffset) + timingOffset;
            if (!double.IsFinite(start)
                || !double.IsFinite(end)
                || end < start)
            {
                continue;
            }

            periods.Add(new YokkoBreakPeriod(start, end));
        }

        return periods
               .OrderBy(static period =>
                   period.StartTimeMilliseconds)
               .ThenBy(static period =>
                   period.EndTimeMilliseconds)
               .ToList();
    }

    private static double sliderDuration(
        double startTime,
        double pixelLength,
        int spanCount,
        double sliderMultiplier,
        IReadOnlyList<YokkoTimingPoint> timingPoints)
    {
        YokkoTimingPoint timing = timingPoints
                                  .Where(point =>
                                      point.Uninherited
                                      && point.TimeMilliseconds
                                      <= startTime)
                                  .LastOrDefault()
                                  ?? timingPoints
                                     .FirstOrDefault(static point =>
                                         point.Uninherited)
                                  ?? YokkoTimingPoint.Default;
        YokkoTimingPoint? inherited = timingPoints
                                      .Where(point =>
                                          !point.Uninherited
                                          && point.TimeMilliseconds
                                          <= startTime)
                                      .LastOrDefault();
        double beatLengthMultiplier = inherited == null
            ? 1
            : Math.Clamp(
                (float)-inherited.BeatLengthMilliseconds,
                10,
                10000) / 100d;
        return pixelLength
               / (sliderMultiplier * 100)
               * timing.BeatLengthMilliseconds
               * beatLengthMultiplier
               * spanCount;
    }

    private static void validateStandardDifficulty(
        double value,
        string name)
    {
        if (!double.IsFinite(value) || value is < 0 or > 10)
        {
            throw new ArgumentOutOfRangeException(
                name,
                "osu!standard difficulty values must be between 0 and 10.");
        }
    }

    private static List<YokkoTimingPoint> parseTimingPoints(
        IReadOnlyList<string> lines,
        int defaultSampleSet = 1,
        int defaultSampleVolume = 100,
        double timingOffset = 0)
    {
        var parsedPoints = new List<ParsedOsuTimingPoint>();

        foreach (string line in lines)
        {
            if (line.StartsWith("//", StringComparison.Ordinal))
                continue;

            string[] parts = line.Split(',');
            if (parts.Length < 2)
                continue;

            if (!double.TryParse(
                    parts[0],
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out double time)
                || !tryParseOsuDouble(parts[1], out double beatLength))
                continue;

            bool uninherited =
                parseInt(parts.ElementAtOrDefault(6), 1) != 0;

            // osu!'s legacy decoder rejects only this line and continues
            // decoding the rest of the file.
            if (uninherited && double.IsNaN(beatLength))
                continue;

            parsedPoints.Add(new ParsedOsuTimingPoint(
                time + timingOffset,
                beatLength,
                Math.Max(1, parseInt(parts.ElementAtOrDefault(2), 4)),
                normaliseSampleSet(
                    parseInt(
                        parts.ElementAtOrDefault(3),
                        defaultSampleSet)),
                parseInt(parts.ElementAtOrDefault(4), 0),
                Math.Clamp(
                    parseInt(
                        parts.ElementAtOrDefault(5),
                        defaultSampleVolume),
                    0,
                    100),
                uninherited,
                parseInt(parts.ElementAtOrDefault(7), 0)));
        }

        var timingPoints = new List<YokkoTimingPoint>();

        foreach (IGrouping<double, ParsedOsuTimingPoint> group in
                 parsedPoints.OrderBy(static point => point.TimeMilliseconds)
                             .GroupBy(static point => point.TimeMilliseconds))
        {
            // Legacy osu! resolves control-point types independently. At one
            // timestamp the first timing (red) point supplies BPM, while the
            // last non-timing (green) point overrides effect/sample data.
            // See ppy/osu LegacyBeatmapDecoder.addControlPoint().
            ParsedOsuTimingPoint? timingSource =
                group.FirstOrDefault(static point => point.Uninherited);
            ParsedOsuTimingPoint? inheritedSource =
                group.LastOrDefault(static point => !point.Uninherited);
            ParsedOsuTimingPoint effectSource =
                inheritedSource ?? timingSource
                ?? throw new InvalidDataException(
                    "An osu! timing-point group contained no control point.");

            if (timingSource != null)
            {
                timingPoints.Add(timingSource.ToTimingPoint(
                    Math.Clamp(
                        timingSource.BeatLengthMilliseconds,
                        osuMinimumBeatLength,
                        osuMaximumBeatLength),
                    uninherited: true));
            }

            double scrollSpeed = osuScrollSpeed(effectSource.BeatLengthMilliseconds);

            // A positive red point already resets effect speed to 1. Keep an
            // inherited point when a green point exists (including explicit
            // 1x resets), or when an abnormal red point also carries a
            // non-default effect speed.
            if (inheritedSource != null || scrollSpeed != 1)
            {
                timingPoints.Add(effectSource.ToTimingPoint(
                    -100 / scrollSpeed,
                    uninherited: false));
            }
        }

        return timingPoints;
    }

    private const double osuMinimumBeatLength = 6;
    private const double osuMaximumBeatLength = 60_000;
    private const double osuMinimumScrollSpeed = 0.01;
    private const double osuMaximumScrollSpeed = 10;

    private static double osuScrollSpeed(double rawBeatLength)
    {
        double speed = rawBeatLength < 0
            ? 100 / -rawBeatLength
            : 1;

        return Math.Clamp(
            speed,
            osuMinimumScrollSpeed,
            osuMaximumScrollSpeed);
    }

    private static bool tryParseOsuDouble(
        string value,
        out double result)
    {
        value = value.Trim();

        if (value.Equals("NaN", StringComparison.OrdinalIgnoreCase))
        {
            result = double.NaN;
            return true;
        }

        if (value.Equals("Infinity", StringComparison.OrdinalIgnoreCase)
            || value.Equals("+Infinity", StringComparison.OrdinalIgnoreCase))
        {
            result = double.PositiveInfinity;
            return true;
        }

        if (value.Equals("-Infinity", StringComparison.OrdinalIgnoreCase))
        {
            result = double.NegativeInfinity;
            return true;
        }

        return double.TryParse(
            value,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out result);
    }

    private sealed record ParsedOsuTimingPoint(
        double TimeMilliseconds,
        double BeatLengthMilliseconds,
        int Meter,
        int SampleSet,
        int SampleIndex,
        int Volume,
        bool Uninherited,
        int Effects)
    {
        public YokkoTimingPoint ToTimingPoint(
            double beatLengthMilliseconds,
            bool uninherited) =>
            new(
                TimeMilliseconds,
                beatLengthMilliseconds,
                Meter,
                SampleSet,
                SampleIndex,
                Volume,
                uninherited,
                Effects);
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
        int hitSound = 0;
        foreach (YokkoHitSample sample in hitObject.Samples)
        {
            hitSound |= sample.Name switch
            {
                YokkoHitSample.HitWhistle => 2,
                YokkoHitSample.HitFinish => 4,
                YokkoHitSample.HitClap => 8,
                _ => 0,
            };
        }
        string sampleBanks = formatSampleBanks(hitObject);

        if (hitObject.Kind == HitObjectKind.Hold && hitObject.EndTimeMilliseconds != null)
        {
            return $"{x},192,{time},{holdType},{hitSound},"
                   + $"{roundMilliseconds(hitObject.EndTimeMilliseconds.Value)}:"
                   + sampleBanks;
        }

        return $"{x},192,{time},{hitCircleType},{hitSound},{sampleBanks}";
    }

    private static string formatSampleBanks(YokkoHitObject hitObject)
    {
        YokkoHitSample? normal = hitObject.Samples.FirstOrDefault(
            static sample =>
                sample.Name == YokkoHitSample.HitNormal)
            ?? hitObject.Samples.FirstOrDefault();
        YokkoHitSample? addition = hitObject.Samples.FirstOrDefault(
            static sample =>
                sample.Name != YokkoHitSample.HitNormal);
        int normalBank = sampleBankNumber(normal?.Bank);
        int additionBank = sampleBankNumber(
            addition?.Bank ?? normal?.Bank);
        int customSampleBank =
            normal?.CustomSampleBank
            ?? addition?.CustomSampleBank
            ?? 0;
        int volume = normal?.Volume
                     ?? addition?.Volume
                     ?? 0;
        string filename = normal?.Filename
                          ?? hitObject.SampleKey
                          ?? string.Empty;
        return string.Join(
            ":",
            normalBank.ToString(CultureInfo.InvariantCulture),
            additionBank.ToString(CultureInfo.InvariantCulture),
            customSampleBank.ToString(CultureInfo.InvariantCulture),
            volume.ToString(CultureInfo.InvariantCulture),
            filename);
    }

    private static int sampleBankNumber(string? bank) =>
        bank switch
        {
            YokkoHitSample.BankNormal => 1,
            YokkoHitSample.BankSoft => 2,
            YokkoHitSample.BankDrum => 3,
            _ => 0,
        };

    private static int laneToX(int lane, int keyCount)
    {
        lane = Math.Clamp(lane, 0, keyCount - 1);
        return (int)Math.Floor((lane + 0.5) * 512 / keyCount);
    }

    private static int roundMilliseconds(double value)
        => (int)Math.Round(value, MidpointRounding.AwayFromZero);

    private static string formatDouble(double value)
        => value.ToString("0.###############", CultureInfo.InvariantCulture);

    private static int parseSampleSet(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return 1;
        if (int.TryParse(
                value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int numeric))
        {
            return normaliseSampleSet(numeric);
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "soft" => 2,
            "drum" => 3,
            _ => 1,
        };
    }

    private static int normaliseSampleSet(int value) =>
        value is >= 1 and <= 3 ? value : 1;

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
