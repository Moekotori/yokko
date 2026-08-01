using System.Text;
using System.Text.RegularExpressions;
using Yokko.Core.Beatmaps;
using Yokko.Core.Gameplay;
using Yokko.Core.Timing;

namespace Yokko.Import.Bms;

public sealed partial class BmsChartImporter : IChartImporter
{
    private static readonly string[] fiveKeyChannels1P = ["11", "12", "13", "14", "15"];
    private static readonly string[] sevenKeyChannels1P = [.. fiveKeyChannels1P, "18", "19"];
    private static readonly string[] fiveKeyChannels2P = ["21", "22", "23", "24", "25"];
    private static readonly string[] sevenKeyChannels2P = [.. fiveKeyChannels2P, "28", "29"];

    public ChartImportCapability Capability { get; } =
        new(ChartSourceFormat.Bms, "BMS / BME / BML", [".bms", ".bme", ".bml"], true, false);

    public bool CanImport(string path)
    {
        string extension = Path.GetExtension(path);
        return extension.Equals(".bms", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".bme", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".bml", StringComparison.OrdinalIgnoreCase);
    }

    public ValueTask<ChartImportResult> ImportAsync(ChartImportRequest request)
    {
        request.CancellationToken.ThrowIfCancellationRequested();
        ParsedBms parsed = parse(request.Path);
        var warnings = new List<string>(parsed.Warnings);

        Dictionary<int, double> measureLengths = parsed.ChannelLines
                                                        .Where(static line => line.Channel == "02")
                                                        .GroupBy(static line => line.Measure)
                                                        .ToDictionary(
                                                            static group => group.Key,
                                                            static group => Math.Max(0.0001, ImportParsing.Double(group.Last().Data, 1)));
        int maximumMeasure = parsed.ChannelLines.Count == 0
            ? 0
            : parsed.ChannelLines.Max(static line => line.Measure);
        var measureStarts = new double[maximumMeasure + 2];

        for (int measure = 0; measure <= maximumMeasure; measure++)
            measureStarts[measure + 1] = measureStarts[measure] + 4 * measureLengths.GetValueOrDefault(measure, 1);

        List<RawEvent> events = expandEvents(parsed.ChannelLines, measureStarts, measureLengths);
        double initialBpm = Math.Max(0.0001, ImportParsing.Double(parsed.Headers.GetValueOrDefault("BPM"), 130));
        var tempoChanges = new List<TempoChange> { new(0, initialBpm) };

        foreach (RawEvent rawEvent in events.Where(static value => value.Channel is "03" or "08"))
        {
            double bpm = rawEvent.Channel == "03"
                ? parseHex(rawEvent.Value)
                : parsed.ExtendedBpms.GetValueOrDefault(rawEvent.Value, 0);

            if (bpm > 0)
                tempoChanges.Add(new TempoChange(rawEvent.Beat, bpm));
            else
                warnings.Add($"Ignored invalid BMS BPM event {rawEvent.Value}.");
        }

        var tempoConverter = new BeatTimeConverter(tempoChanges);
        var pauses = new List<PauseEvent>();

        foreach (RawEvent rawEvent in events.Where(static value => value.Channel == "09"))
        {
            double stopUnits = parsed.Stops.GetValueOrDefault(rawEvent.Value, 0);
            if (stopUnits <= 0)
                continue;

            double duration = stopUnits / 48d * 60000 / tempoConverter.TempoAt(rawEvent.Beat);
            pauses.Add(new PauseEvent(rawEvent.Beat, duration));
        }

        if (pauses.Count > 0)
        {
            warnings.Add(
                "BMS STOP events were baked into absolute note times and visual scroll pauses; constant-scroll gameplay and editor beat rows cannot display the stopped span exactly.");
        }

        LaneMapping laneMapping = createLaneMap(
            events,
            warnings,
            request.EnableBmsScratch,
            Path.GetExtension(request.Path),
            parsed.Headers.GetValueOrDefault("PLAYER"));
        IReadOnlyDictionary<string, int> laneMap = laneMapping.Channels;
        KeyMode keyMode = laneMapping.KeyMode;
        var beatNotes = new List<MutableBeatNote>();

        foreach (RawEvent rawEvent in events.Where(value => laneMap.ContainsKey(value.Channel))
                                            .OrderBy(static value => value.Beat)
                                            .ThenBy(static value => value.Order))
        {
            int lane = laneMap[rawEvent.Channel];

            if (parsed.LongNoteObject != null
                && rawEvent.Value.Equals(parsed.LongNoteObject, StringComparison.OrdinalIgnoreCase))
            {
                MutableBeatNote? start = beatNotes.LastOrDefault(note => note.Lane == lane && note.EndBeat == null);
                if (start == null)
                    warnings.Add($"Ignored BMS LNOBJ end without a matching note in lane {lane + 1}.");
                else
                    start.EndBeat = rawEvent.Beat;

                continue;
            }

            beatNotes.Add(new MutableBeatNote(
                lane,
                rawEvent.Beat,
                null,
                rawEvent.Value,
                HitObjectKind.Tap));
        }

        foreach ((string visibleChannel, int lane) in laneMap)
        {
            string longChannel = toLongNoteChannel(visibleChannel);
            RawEvent[] longEvents = events.Where(value => value.Channel == longChannel)
                                          .OrderBy(static value => value.Beat)
                                          .ThenBy(static value => value.Order)
                                          .ToArray();

            for (int index = 0; index + 1 < longEvents.Length; index += 2)
            {
                beatNotes.Add(new MutableBeatNote(
                    lane,
                    longEvents[index].Beat,
                    longEvents[index + 1].Beat,
                    longEvents[index].Value,
                    HitObjectKind.Hold));
            }

            if (longEvents.Length % 2 != 0)
                warnings.Add($"Ignored an unterminated BMS long note in lane {lane + 1}.");
        }

        bool hasMines = false;
        foreach ((string visibleChannel, int lane) in laneMap)
        {
            string mineChannel = toMineChannel(visibleChannel);
            foreach (RawEvent mineEvent in events.Where(value => value.Channel == mineChannel))
            {
                hasMines = true;
                beatNotes.Add(new MutableBeatNote(
                    lane,
                    mineEvent.Beat,
                    null,
                    "00",
                    HitObjectKind.Mine));
            }
        }

        if (hasMines)
        {
            warnings.Add(
                "BMS landmine damage values were mapped to Yokko's standard mine behaviour.");
        }

        if (laneMapping.ScratchIgnored)
        {
            warnings.Add(
                "BMS scratch objects are disabled in Import settings and were ignored.");
        }
        if (laneMapping.DualScratchApproximation)
        {
            warnings.Add(
                "BMS double-play scratches were preserved as ordinary playable lanes because Yokko cannot mark two scratch lanes yet.");
        }
        if (events.Any(static value => value.Channel is "04" or "06" or "07" or "0A"))
            warnings.Add("BMS BGA events are not represented by Yokko yet and were ignored.");
        if (events.Any(static value => value.Channel.Length == 2
                                      && value.Channel[0] is '3' or '4'))
        {
            warnings.Add("BMS invisible key objects are not represented by Yokko yet and were ignored.");
        }
        if (events.Any(static value => value.Channel is "17" or "27" or "57" or "67" or "D7" or "E7"))
        {
            warnings.Add("BMS free-zone or pedal objects are not represented by Yokko yet and were ignored.");
        }
        if (parsed.Headers.GetValueOrDefault("LNTYPE") == "2")
            warnings.Add("BMS LNTYPE 2 continuation semantics are not fully supported; long notes may differ from the source.");

        var unshiftedConverter = new BeatTimeConverter(tempoChanges, pauses);
        string? audioPath = resolveBackgroundAudio(request.Path, parsed, events, warnings, out double audioStartBeat);
        double audioOffset = audioPath == null ? 0 : -unshiftedConverter.ToMilliseconds(audioStartBeat);
        var converter = new BeatTimeConverter(tempoChanges, pauses, audioOffset);
        YokkoScheduledSample[] scheduledSamples = audioPath == null
            ? resolveBackgroundSamples(request.Path, parsed, events, converter, warnings)
            : [];
        YokkoHitObject[] hitObjects = beatNotes.Select(note =>
        {
            string? samplePath = request.PreferKeysounds
                                 && parsed.WavFiles.TryGetValue(note.SampleId, out string? sample)
                ? resolveAudioAsset(request.Path, sample, warnings)
                : null;
            return new YokkoHitObject(
                note.Lane,
                converter.ToMilliseconds(note.StartBeat),
                note.EndBeat.HasValue ? converter.ToMilliseconds(note.EndBeat.Value) : null,
                note.EndBeat.HasValue ? HitObjectKind.Hold : note.Kind,
                samplePath);
        }).OrderBy(static note => note.StartTimeMilliseconds)
          .ThenBy(static note => note.Lane)
          .ToArray();
        ScrollVelocityProfile scrollVelocity = createScrollVelocityProfile(
            tempoChanges,
            pauses,
            converter,
            tempoConverter.TempoAt(0));

        string difficulty = parsed.Headers.GetValueOrDefault(
            "SUBTITLE",
            parsed.Headers.TryGetValue("PLAYLEVEL", out string? level) ? $"Level {level}" : $"{(int)keyMode}K");
        string creator = parsed.Headers.GetValueOrDefault("SUBARTIST", "Unknown Creator");

        var beatmap = new YokkoBeatmap(
            parsed.Headers.GetValueOrDefault("TITLE", "Untitled"),
            parsed.Headers.GetValueOrDefault("ARTIST", "Unknown Artist"),
            creator,
            difficulty.Trim(),
            keyMode,
            ChartSourceFormat.Bms,
            converter.ToTimingPoints(),
            audioPath,
            hitObjects,
            ScrollVelocities: scrollVelocity.Changes,
            InitialScrollVelocity: scrollVelocity.InitialMultiplier,
            StageCount: laneMapping.StageCount,
            ScheduledSamples: scheduledSamples,
            ScratchLane: laneMapping.ScratchLane);

        return ValueTask.FromResult(new ChartImportResult(beatmap, warnings.Distinct().ToArray()));
    }

    private static ParsedBms parse(string path)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        byte[] bytes = File.ReadAllBytes(path);
        string text = decodeText(bytes);
        var parsed = new ParsedBms();
        bool active = true;
        var randomContexts = new Stack<RandomContext>();
        int order = 0;

        foreach (string rawLine in text.Replace("\r", string.Empty).Split('\n'))
        {
            string line = rawLine.Trim();
            if (!line.StartsWith('#') || line.Length < 2)
                continue;

            string upper = line.ToUpperInvariant();

            if (upper.StartsWith("#RANDOM ", StringComparison.Ordinal))
            {
                randomContexts.Push(new RandomContext(active, 1));
                parsed.Warnings.Add("BMS RANDOM branches were resolved deterministically with choice 1.");
                continue;
            }

            if (upper.StartsWith("#IF ", StringComparison.Ordinal) && randomContexts.TryPeek(out RandomContext? context))
            {
                int branch = ImportParsing.Int(line[4..], 0);
                context.BranchMatched = branch == context.Selection;
                active = context.ParentActive && branch == context.Selection;
                continue;
            }

            if (upper.StartsWith("#ELSEIF ", StringComparison.Ordinal) && randomContexts.TryPeek(out context))
            {
                int branch = ImportParsing.Int(line[8..], 0);
                bool selected = !context.BranchMatched && branch == context.Selection;
                context.BranchMatched |= branch == context.Selection;
                active = context.ParentActive && selected;
                continue;
            }

            if (upper == "#ELSE" && randomContexts.TryPeek(out context))
            {
                active = context.ParentActive && !context.BranchMatched;
                context.BranchMatched = true;
                continue;
            }

            if (upper == "#ENDIF" && randomContexts.TryPeek(out context))
            {
                active = context.ParentActive;
                context.BranchMatched = false;
                continue;
            }

            if (upper == "#ENDRANDOM" && randomContexts.Count > 0)
            {
                RandomContext completed = randomContexts.Pop();
                active = completed.ParentActive;
                continue;
            }

            if (!active)
                continue;

            Match channelMatch = channelRegex().Match(line);
            if (channelMatch.Success)
            {
                parsed.ChannelLines.Add(new ChannelLine(
                    ImportParsing.Int(channelMatch.Groups["measure"].Value),
                    channelMatch.Groups["channel"].Value.ToUpperInvariant(),
                    channelMatch.Groups["data"].Value.Trim(),
                    order++));
                continue;
            }

            int separator = line.IndexOfAny([' ', '\t']);
            string key = (separator < 0 ? line[1..] : line[1..separator]).Trim().ToUpperInvariant();
            string value = separator < 0 ? string.Empty : line[(separator + 1)..].Trim();

            if (key.StartsWith("WAV", StringComparison.Ordinal) && key.Length == 5)
                parsed.WavFiles[key[3..]] = value;
            else if (key.StartsWith("BPM", StringComparison.Ordinal) && key.Length == 5)
                parsed.ExtendedBpms[key[3..]] = ImportParsing.Double(value);
            else if (key.StartsWith("STOP", StringComparison.Ordinal) && key.Length == 6)
                parsed.Stops[key[4..]] = ImportParsing.Double(value);
            else if (key == "LNOBJ")
                parsed.LongNoteObject = value.ToUpperInvariant();
            else
                parsed.Headers[key] = value;
        }

        return parsed;
    }

    private static List<RawEvent> expandEvents(
        IEnumerable<ChannelLine> channelLines,
        IReadOnlyList<double> measureStarts,
        IReadOnlyDictionary<int, double> measureLengths)
    {
        var events = new List<RawEvent>();

        foreach (IGrouping<(int Measure, string Channel), ChannelLine> group in
                 channelLines.Where(static line => line.Channel != "02")
                             .GroupBy(static line => (line.Measure, line.Channel)))
        {
            IEnumerable<ExpandedObject> objects = group.Key.Channel == "01"
                ? group.SelectMany(expandLine)
                : mergeChannelLines(group);
            double measureBeats = 4 * measureLengths.GetValueOrDefault(
                group.Key.Measure,
                1);

            foreach (ExpandedObject value in objects)
            {
                events.Add(new RawEvent(
                    group.Key.Channel,
                    measureStarts[group.Key.Measure]
                    + measureBeats * value.Position.Numerator
                    / value.Position.Denominator,
                    value.Value,
                    value.Order));
            }
        }

        return events;
    }

    private static IEnumerable<ExpandedObject> mergeChannelLines(
        IEnumerable<ChannelLine> lines)
    {
        var merged = new Dictionary<FractionPosition, ExpandedObject>();
        foreach (ChannelLine line in lines.OrderBy(static line => line.Order))
        {
            foreach (ExpandedObject value in expandLine(line))
                merged[value.Position] = value;
        }

        return merged.Values.OrderBy(static value => value.Position);
    }

    private static IEnumerable<ExpandedObject> expandLine(ChannelLine line)
    {
        if (line.Data.Length < 2 || line.Data.Length % 2 != 0)
            yield break;

        int count = line.Data.Length / 2;
        for (int index = 0; index < count; index++)
        {
            string value = line.Data.Substring(index * 2, 2).ToUpperInvariant();
            if (value == "00")
                continue;

            yield return new ExpandedObject(
                FractionPosition.Create(index, count),
                value,
                line.Order);
        }
    }

    private static LaneMapping createLaneMap(
        IReadOnlyList<RawEvent> events,
        ICollection<string> warnings,
        bool enableScratch,
        string extension,
        string? playerHeader)
    {
        string[] used1P = sevenKeyChannels1P.Where(channel => laneHasObjects(events, channel)).ToArray();
        string[] used2P = sevenKeyChannels2P.Where(channel => laneHasObjects(events, channel)).ToArray();
        int player = ImportParsing.Int(playerHeader, 1);
        bool doublePlay = used2P.Length > 0
                          || laneHasObjects(events, "26")
                          || player == 3;
        bool sevenKey = extension.Equals(".bme", StringComparison.OrdinalIgnoreCase)
                        || used1P.Any(static channel => channel is "18" or "19")
                        || used2P.Any(static channel => channel is "28" or "29");

        if (player is 2 or 4)
        {
            warnings.Add(
                $"BMS PLAYER {player} multi-gauge semantics are not supported; playable channels were imported into one score state.");
        }

        string[] keyChannels1P;
        string[] keyChannels2P;
        if (doublePlay)
        {
            keyChannels1P = sevenKey ? sevenKeyChannels1P : fiveKeyChannels1P;
            keyChannels2P = sevenKey ? sevenKeyChannels2P : fiveKeyChannels2P;
        }
        else if (sevenKey)
        {
            keyChannels1P = sevenKeyChannels1P;
            keyChannels2P = [];
        }
        else if (used1P.Contains("15"))
        {
            keyChannels1P = fiveKeyChannels1P;
            keyChannels2P = [];
        }
        else
        {
            keyChannels1P = used1P.Length == 0 ? ["11", "12", "13", "14"] : used1P;
            keyChannels2P = [];
        }

        bool hasScratch = laneHasObjects(events, "16")
                          || doublePlay && laneHasObjects(events, "26");
        bool includeScratch = hasScratch && enableScratch;
        var channels = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        if (!doublePlay)
        {
            int offset = includeScratch ? 1 : 0;
            if (includeScratch)
                channels["16"] = 0;
            for (int lane = 0; lane < keyChannels1P.Length; lane++)
                channels[keyChannels1P[lane]] = lane + offset;

            return new LaneMapping(
                channels,
                (KeyMode)(keyChannels1P.Length + offset),
                StageCount: 1,
                ScratchIgnored: hasScratch && !enableScratch,
                ScratchLane: includeScratch ? 0 : null,
                DualScratchApproximation: false);
        }

        int stageWidth = keyChannels1P.Length + (includeScratch ? 1 : 0);
        int laneCursor = 0;
        if (includeScratch)
            channels["16"] = laneCursor++;
        foreach (string channel in keyChannels1P)
            channels[channel] = laneCursor++;
        if (includeScratch)
            channels["26"] = laneCursor++;
        foreach (string channel in keyChannels2P)
            channels[channel] = laneCursor++;

        return new LaneMapping(
            channels,
            (KeyMode)(stageWidth * 2),
            StageCount: 2,
            ScratchIgnored: hasScratch && !enableScratch,
            ScratchLane: null,
            DualScratchApproximation: includeScratch);
    }

    private static bool laneHasObjects(
        IEnumerable<RawEvent> events,
        string visibleChannel) =>
        events.Any(value => value.Channel == visibleChannel
                            || value.Channel == toLongNoteChannel(visibleChannel)
                            || value.Channel == toMineChannel(visibleChannel));

    private static string? resolveBackgroundAudio(
        string chartPath,
        ParsedBms parsed,
        IReadOnlyList<RawEvent> events,
        ICollection<string> warnings,
        out double startBeat)
    {
        RawEvent[] backgroundEvents = events.Where(static value => value.Channel == "01").ToArray();
        string[] backgroundSamples = backgroundEvents.Select(static value => value.Value)
                                                      .Distinct(StringComparer.OrdinalIgnoreCase)
                                                      .ToArray();

        if (backgroundEvents.Length == 1
            && backgroundSamples.Length == 1
            && parsed.WavFiles.TryGetValue(backgroundSamples[0], out string? file))
        {
            string? path = resolveAudioAsset(chartPath, file, warnings);
            if (path != null)
            {
                startBeat = backgroundEvents[0].Beat;
                return path;
            }
        }

        if (backgroundEvents.Length <= 1)
            warnings.Add("No directly playable background audio file was found for this BMS chart.");

        startBeat = 0;
        return null;
    }

    private static YokkoScheduledSample[] resolveBackgroundSamples(
        string chartPath,
        ParsedBms parsed,
        IEnumerable<RawEvent> events,
        BeatTimeConverter converter,
        ICollection<string> warnings)
    {
        var samples = new List<(YokkoScheduledSample Sample, int Order)>();

        foreach (RawEvent rawEvent in events.Where(static value => value.Channel == "01"))
        {
            string? path = parsed.WavFiles.TryGetValue(rawEvent.Value, out string? file)
                ? resolveAudioAsset(chartPath, file, warnings)
                : null;
            if (path == null)
            {
                warnings.Add($"BMS background sample {rawEvent.Value} could not be resolved.");
                continue;
            }

            samples.Add((
                new YokkoScheduledSample(
                    converter.ToMilliseconds(rawEvent.Beat),
                    path,
                    UseMusicBus: true),
                rawEvent.Order));
        }

        return samples.OrderBy(static item => item.Sample.TimeMilliseconds)
                      .ThenBy(static item => item.Order)
                      .Select(static item => item.Sample)
                      .ToArray();
    }

    private static ScrollVelocityProfile createScrollVelocityProfile(
        IEnumerable<TempoChange> tempoChanges,
        IEnumerable<PauseEvent> pauses,
        BeatTimeConverter converter,
        double baseBpm)
    {
        baseBpm = Math.Max(0.0001, baseBpm);
        var changes = new List<ScrollChange>();

        foreach (TempoChange tempo in tempoChanges)
        {
            if (tempo.BeatsPerMinute <= 0)
                continue;

            changes.Add(new ScrollChange(
                converter.ToMilliseconds(tempo.Beat),
                tempo.BeatsPerMinute / baseBpm,
                Priority: 0));
        }

        foreach (IGrouping<double, PauseEvent> group in pauses.GroupBy(static pause => pause.Beat))
        {
            double start = converter.ToMilliseconds(group.Key);
            double duration = group.Sum(static pause => pause.DurationMilliseconds);
            changes.Add(new ScrollChange(start, 0, Priority: 2));
            changes.Add(new ScrollChange(
                start + duration,
                converter.TempoAt(group.Key) / baseBpm,
                Priority: 1));
        }

        var result = new List<YokkoScrollVelocity>();
        double previous = 1;
        foreach (ScrollChange change in changes.OrderBy(static change => change.TimeMilliseconds)
                                               .ThenBy(static change => change.Priority)
                                               .GroupBy(static change => change.TimeMilliseconds)
                                               .Select(static group => group.Last()))
        {
            if (change.Multiplier.Equals(previous))
                continue;

            result.Add(new YokkoScrollVelocity(
                change.TimeMilliseconds,
                change.Multiplier));
            previous = change.Multiplier;
        }

        return new ScrollVelocityProfile(1, result);
    }

    private static string? resolveAudioAsset(
        string chartPath,
        string? assetPath,
        ICollection<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(assetPath))
            return null;

        assetPath = assetPath.Trim().Trim('"');
        if (Path.IsPathRooted(assetPath))
            return File.Exists(assetPath) ? Path.GetFullPath(assetPath) : null;

        string chartDirectory = Path.GetFullPath(
            Path.GetDirectoryName(Path.GetFullPath(chartPath))!);
        string candidate = Path.GetFullPath(Path.Combine(
            chartDirectory,
            assetPath.Replace('/', Path.DirectorySeparatorChar)));
        string rootPrefix = chartDirectory + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            return null;

        string? directory = Path.GetDirectoryName(candidate);
        if (directory == null || !Directory.Exists(directory))
            return null;

        string fileName = Path.GetFileName(candidate);
        string[] exactMatches = Directory.EnumerateFiles(directory)
                                         .Where(path => string.Equals(
                                             Path.GetFileName(path),
                                             fileName,
                                             StringComparison.OrdinalIgnoreCase))
                                         .ToArray();
        if (exactMatches.Length == 1)
            return exactMatches[0];

        string stem = Path.GetFileNameWithoutExtension(candidate);
        string[] supportedExtensions = [".wav", ".ogg", ".mp3"];
        string[] matches = Directory.EnumerateFiles(directory)
                                    .Where(path => string.Equals(
                                        Path.GetFileNameWithoutExtension(path),
                                        stem,
                                        StringComparison.OrdinalIgnoreCase)
                                                   && supportedExtensions.Contains(
                                                       Path.GetExtension(path),
                                                       StringComparer.OrdinalIgnoreCase))
                                    .ToArray();
        if (matches.Length == 1)
            return matches[0];

        if (matches.Length > 1)
        {
            warnings.Add(
                $"BMS audio asset {assetPath} matched multiple alternate file extensions and was ignored.");
        }

        return null;
    }

    private static string toLongNoteChannel(string channel)
        => $"{(char)(channel[0] + 4)}{channel[1]}";

    private static string toMineChannel(string channel)
        => $"{(channel[0] == '1' ? 'D' : 'E')}{channel[1]}";

    private static int parseHex(string value)
        => int.TryParse(value, System.Globalization.NumberStyles.HexNumber, null, out int parsed) ? parsed : 0;

    private static bool hasUtf8Bom(IReadOnlyList<byte> bytes)
        => bytes.Count >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;

    private static string decodeText(byte[] bytes)
    {
        if (hasUtf8Bom(bytes))
            return Encoding.UTF8.GetString(bytes);

        try
        {
            return new UTF8Encoding(false, true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return Encoding.GetEncoding(932).GetString(bytes);
        }
    }

    [GeneratedRegex(@"^#(?<measure>\d{3})(?<channel>[0-9A-Za-z]{2}):(?<data>.+)$")]
    private static partial Regex channelRegex();

    private sealed class ParsedBms
    {
        public Dictionary<string, string> Headers { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> WavFiles { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, double> ExtendedBpms { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, double> Stops { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<ChannelLine> ChannelLines { get; } = [];
        public List<string> Warnings { get; } = [];
        public string? LongNoteObject { get; set; }
    }

    private sealed class RandomContext(bool parentActive, int selection)
    {
        public bool ParentActive { get; } = parentActive;
        public int Selection { get; } = selection;
        public bool BranchMatched { get; set; }
    }

    private sealed record ChannelLine(int Measure, string Channel, string Data, int Order);

    private readonly record struct FractionPosition(
        int Numerator,
        int Denominator) : IComparable<FractionPosition>
    {
        public static FractionPosition Create(int numerator, int denominator)
        {
            int divisor = greatestCommonDivisor(numerator, denominator);
            return new FractionPosition(numerator / divisor, denominator / divisor);
        }

        public int CompareTo(FractionPosition other) =>
            ((long)Numerator * other.Denominator).CompareTo(
                (long)other.Numerator * Denominator);

        private static int greatestCommonDivisor(int left, int right)
        {
            while (right != 0)
                (left, right) = (right, left % right);
            return Math.Max(1, Math.Abs(left));
        }
    }

    private sealed record ExpandedObject(
        FractionPosition Position,
        string Value,
        int Order);

    private sealed record RawEvent(string Channel, double Beat, string Value, int Order);

    private sealed record ScrollChange(
        double TimeMilliseconds,
        double Multiplier,
        int Priority);

    private sealed record LaneMapping(
        IReadOnlyDictionary<string, int> Channels,
        KeyMode KeyMode,
        int StageCount,
        bool ScratchIgnored,
        int? ScratchLane,
        bool DualScratchApproximation);

    private sealed class MutableBeatNote(
        int lane,
        double startBeat,
        double? endBeat,
        string sampleId,
        HitObjectKind kind)
    {
        public int Lane { get; } = lane;
        public double StartBeat { get; } = startBeat;
        public double? EndBeat { get; set; } = endBeat;
        public string SampleId { get; } = sampleId;
        public HitObjectKind Kind { get; } = kind;
    }
}
