using System.Text;
using System.Text.RegularExpressions;
using Yokko.Core.Beatmaps;
using Yokko.Core.Gameplay;

namespace Yokko.Import.Bms;

public sealed partial class BmsChartImporter : IChartImporter
{
    private static readonly string[] fourKeyCandidates = ["11", "12", "13", "14", "15", "18", "19"];
    private static readonly string[] sevenKeyChannels = ["11", "12", "13", "14", "15", "18", "19"];

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
            warnings.Add("BMS STOP events were baked into absolute note times; editor beat rows cannot display the stopped span exactly yet.");

        LaneMapping laneMapping = createLaneMap(
            events,
            warnings,
            request.EnableBmsScratch);
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

            beatNotes.Add(new MutableBeatNote(lane, rawEvent.Beat, null, rawEvent.Value));
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
                    longEvents[index].Value));
            }

            if (longEvents.Length % 2 != 0)
                warnings.Add($"Ignored an unterminated BMS long note in lane {lane + 1}.");
        }

        if (laneMapping.ScratchIgnored)
        {
            warnings.Add(
                "BMS scratch objects are disabled in Import settings and were ignored.");
        }
        if (events.Any(static value => value.Channel is "04" or "06" or "07"))
            warnings.Add("BMS BGA events are not represented by Yokko yet and were ignored.");
        if (parsed.Headers.GetValueOrDefault("LNTYPE") == "2")
            warnings.Add("BMS LNTYPE 2 continuation semantics are not fully supported; long notes may differ from the source.");

        var unshiftedConverter = new BeatTimeConverter(tempoChanges, pauses);
        string? audioPath = resolveBackgroundAudio(request.Path, parsed, events, warnings, out double audioStartBeat);
        double audioOffset = audioPath == null ? 0 : -unshiftedConverter.ToMilliseconds(audioStartBeat);
        var converter = new BeatTimeConverter(tempoChanges, pauses, audioOffset);
        YokkoHitObject[] hitObjects = beatNotes.Select(note =>
        {
            string? samplePath = request.PreferKeysounds
                                 && parsed.WavFiles.TryGetValue(note.SampleId, out string? sample)
                ? ImportParsing.ResolveAdjacentAsset(request.Path, sample)
                : null;
            return new YokkoHitObject(
                note.Lane,
                converter.ToMilliseconds(note.StartBeat),
                note.EndBeat.HasValue ? converter.ToMilliseconds(note.EndBeat.Value) : null,
                note.EndBeat.HasValue ? HitObjectKind.Hold : HitObjectKind.Tap,
                samplePath);
        }).OrderBy(static note => note.StartTimeMilliseconds)
          .ThenBy(static note => note.Lane)
          .ToArray();

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
                int range = Math.Max(1, ImportParsing.Int(line[8..], 1));
                randomContexts.Push(new RandomContext(active, 1 % range == 0 ? range : 1));
                parsed.Warnings.Add("BMS RANDOM branches were resolved deterministically with choice 1.");
                continue;
            }

            if (upper.StartsWith("#IF ", StringComparison.Ordinal) && randomContexts.TryPeek(out RandomContext? context))
            {
                int branch = ImportParsing.Int(line[4..], 0);
                context.BranchMatched |= branch == context.Selection;
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

        foreach (ChannelLine line in channelLines.Where(static line => line.Channel != "02"))
        {
            if (line.Data.Length < 2 || line.Data.Length % 2 != 0)
                continue;

            int count = line.Data.Length / 2;
            double measureBeats = 4 * measureLengths.GetValueOrDefault(line.Measure, 1);

            for (int index = 0; index < count; index++)
            {
                string value = line.Data.Substring(index * 2, 2).ToUpperInvariant();
                if (value == "00")
                    continue;

                events.Add(new RawEvent(
                    line.Channel,
                    measureStarts[line.Measure] + measureBeats * index / count,
                    value,
                    line.Order));
            }
        }

        return events;
    }

    private static LaneMapping createLaneMap(
        IReadOnlyList<RawEvent> events,
        ICollection<string> warnings,
        bool enableScratch)
    {
        string[] used = fourKeyCandidates.Where(channel =>
                                           events.Any(value => value.Channel == channel
                                                               || value.Channel == toLongNoteChannel(channel)))
                                         .ToArray();

        Dictionary<string, int> keyChannels;
        KeyMode keyMode;

        if (used.Contains("18") || used.Contains("19"))
        {
            keyChannels = sevenKeyChannels
                          .Select((channel, lane) => (channel, lane))
                          .ToDictionary(
                              static pair => pair.channel,
                              static pair => pair.lane);
            keyMode = KeyMode.SevenKey;
        }
        else if (used.Length <= 4)
        {
            keyChannels = used
                          .Select((channel, lane) => (channel, lane))
                          .ToDictionary(
                              static pair => pair.channel,
                              static pair => pair.lane);
            keyMode = KeyMode.FourKey;
        }
        else
        {
            warnings.Add(
                $"BMS uses {used.Length} key lanes; mapped them into Yokko 7K.");
            keyChannels = sevenKeyChannels
                          .Select((channel, lane) => (channel, lane))
                          .ToDictionary(
                              static pair => pair.channel,
                              static pair => pair.lane);
            keyMode = KeyMode.SevenKey;
        }

        bool hasScratch = events.Any(
            static value => value.Channel is "16" or "56");
        if (!hasScratch || !enableScratch)
        {
            return new LaneMapping(
                keyChannels,
                keyMode,
                hasScratch,
                ScratchLane: null);
        }

        var channelsWithScratch = new Dictionary<string, int>
        {
            ["16"] = 0,
        };
        foreach ((string channel, int lane) in keyChannels)
            channelsWithScratch[channel] = lane + 1;

        return new LaneMapping(
            channelsWithScratch,
            (KeyMode)((int)keyMode + 1),
            ScratchIgnored: false,
            ScratchLane: 0);
    }

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
            string? path = ImportParsing.ResolveAdjacentAsset(chartPath, file);
            if (path != null)
            {
                startBeat = backgroundEvents[0].Beat;
                return path;
            }
        }

        if (backgroundEvents.Length > 1)
            warnings.Add($"BMS uses {backgroundEvents.Length} background audio events; runtime BMS sample mixing is not available yet.");
        else
            warnings.Add("No directly playable background audio file was found for this BMS chart.");

        startBeat = 0;
        return null;
    }

    private static string toLongNoteChannel(string channel)
        => $"{(char)(channel[0] + 4)}{channel[1]}";

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

    private sealed record RawEvent(string Channel, double Beat, string Value, int Order);

    private sealed record LaneMapping(
        IReadOnlyDictionary<string, int> Channels,
        KeyMode KeyMode,
        bool ScratchIgnored,
        int? ScratchLane);

    private sealed class MutableBeatNote(int lane, double startBeat, double? endBeat, string sampleId)
    {
        public int Lane { get; } = lane;
        public double StartBeat { get; } = startBeat;
        public double? EndBeat { get; set; } = endBeat;
        public string SampleId { get; } = sampleId;
    }
}
