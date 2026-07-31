using System.Text.RegularExpressions;
using Yokko.Core.Beatmaps;
using Yokko.Core.Gameplay;

namespace Yokko.Import.Etterna;

public sealed partial class EtternaChartImporter : IChartImporter
{
    public ChartImportCapability Capability { get; } =
        new(ChartSourceFormat.Etterna, "Etterna / StepMania", [".sm", ".ssc", ".zip", ".smzip"], false, false);

    public bool CanImport(string path)
    {
        string extension = Path.GetExtension(path);
        return extension.Equals(".sm", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".ssc", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".zip", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".smzip", StringComparison.OrdinalIgnoreCase);
    }

    public ValueTask<ChartImportResult> ImportAsync(ChartImportRequest request)
    {
        request.CancellationToken.ThrowIfCancellationRequested();
        string extension = Path.GetExtension(request.Path);

        if (extension.Equals(".sm", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".ssc", StringComparison.OrdinalIgnoreCase))
        {
            return ValueTask.FromResult(importChartFile(request.Path));
        }

        IReadOnlyList<string> charts = ChartArchive.ExtractCharts(request.Path, ".sm", ".ssc");
        string[] orderedCharts = charts.OrderBy(static chart => Path.GetDirectoryName(chart), StringComparer.OrdinalIgnoreCase)
                                       .ThenBy(static chart => Path.GetFileNameWithoutExtension(chart), StringComparer.OrdinalIgnoreCase)
                                       .ThenBy(chart => Path.GetExtension(chart).Equals(".ssc", StringComparison.OrdinalIgnoreCase)
                                           == request.PreferSscSimfiles
                                               ? 0
                                               : 1)
                                       .ToArray();
        var failures = new List<Exception>();

        foreach (string chart in orderedCharts)
        {
            request.CancellationToken.ThrowIfCancellationRequested();

            try
            {
                ChartImportResult result = importChartFile(chart);
                var warnings = new List<string>
                {
                    $"This package contains {charts.Count} simfiles; imported {Path.GetFileName(chart)}.",
                };
                warnings.AddRange(result.Warnings);
                return ValueTask.FromResult(result with { Warnings = warnings });
            }
            catch (InvalidDataException ex)
            {
                failures.Add(ex);
            }
        }

        throw new InvalidDataException(
            "The Etterna/StepMania package does not contain a supported 4K/7K chart.",
            failures.FirstOrDefault());
    }

    private static ChartImportResult importChartFile(string path)
    {
        string text = File.ReadAllText(path);
        List<SimfileTag> tags = parseTags(text);
        bool isSsc = Path.GetExtension(path).Equals(".ssc", StringComparison.OrdinalIgnoreCase);

        Dictionary<string, string> global = collectGlobalTags(tags, isSsc);
        List<ChartBlock> candidates = isSsc ? collectSscCharts(tags) : collectSmCharts(tags);
        List<ChartBlock> supported = candidates.Where(static chart => chart.LaneCount is 4 or 7).ToList();

        if (supported.Count == 0)
            throw new InvalidDataException("No compatible 4K or 7K chart was found in this simfile.");

        ChartBlock selected = supported[0];
        var warnings = new List<string>();

        if (supported.Count > 1)
            warnings.Add($"This simfile contains {supported.Count} compatible charts; imported the first one ({selected.DisplayName}).");

        Dictionary<string, string> timing = new(global, StringComparer.OrdinalIgnoreCase);
        foreach ((string key, string value) in selected.Values)
        {
            if (isTimingKey(key))
                timing[key] = value;
        }

        List<TempoChange> tempoChanges = parseBeatValues(timing.GetValueOrDefault("BPMS"))
                                         .Where(static value => value.Value > 0)
                                         .Select(static value => new TempoChange(value.Beat, value.Value))
                                         .ToList();
        if (tempoChanges.Count == 0)
            tempoChanges.Add(new TempoChange(0, 120));

        var pauses = new List<PauseEvent>();
        pauses.AddRange(parseBeatValues(timing.GetValueOrDefault("STOPS"))
                        .Where(static value => value.Value > 0)
                        .Select(static value => new PauseEvent(value.Beat, value.Value * 1000)));
        pauses.AddRange(parseBeatValues(timing.GetValueOrDefault("DELAYS"))
                        .Where(static value => value.Value > 0)
                        .Select(static value => new PauseEvent(value.Beat, value.Value * 1000)));

        double offsetMilliseconds = -ImportParsing.Double(timing.GetValueOrDefault("OFFSET")) * 1000;
        var converter = new BeatTimeConverter(tempoChanges, pauses, offsetMilliseconds);
        List<BeatNote> beatNotes = parseNotes(selected.Notes, selected.LaneCount, warnings);
        YokkoHitObject[] hitObjects = beatNotes.Select(note => new YokkoHitObject(
                                                       note.Lane,
                                                       converter.ToMilliseconds(note.StartBeat),
                                                       note.EndBeat.HasValue
                                                           ? converter.ToMilliseconds(note.EndBeat.Value)
                                                           : null,
                                                       note.Kind,
                                                       HoldType: note.HoldType))
                                                .OrderBy(static note => note.StartTimeMilliseconds)
                                                .ThenBy(static note => note.Lane)
                                                .ToArray();

        if (!string.IsNullOrWhiteSpace(timing.GetValueOrDefault("WARPS")))
            warnings.Add("SSC warps are not represented by Yokko yet; affected timing may not be faithful.");
        if (!string.IsNullOrWhiteSpace(timing.GetValueOrDefault("FAKES")))
            warnings.Add("SSC fake regions are not represented by Yokko yet and were ignored.");
        if (!string.IsNullOrWhiteSpace(timing.GetValueOrDefault("SCROLLS"))
            || !string.IsNullOrWhiteSpace(timing.GetValueOrDefault("SPEEDS")))
            warnings.Add("SSC scroll and speed effects are not represented by Yokko yet and were ignored.");

        KeyMode keyMode = selected.LaneCount == 4 ? KeyMode.FourKey : KeyMode.SevenKey;
        string audio = selected.Values.GetValueOrDefault("MUSIC", global.GetValueOrDefault("MUSIC", string.Empty));
        string creator = selected.Values.GetValueOrDefault(
            "CREDIT",
            global.GetValueOrDefault("CREDIT", selected.Description));

        var beatmap = new YokkoBeatmap(
            global.GetValueOrDefault("TITLE", "Untitled"),
            global.GetValueOrDefault("ARTIST", "Unknown Artist"),
            string.IsNullOrWhiteSpace(creator) ? "Unknown Creator" : creator,
            selected.DisplayName,
            keyMode,
            ChartSourceFormat.Etterna,
            converter.ToTimingPoints(),
            ImportParsing.ResolveAdjacentAsset(path, audio),
            hitObjects);

        return new ChartImportResult(beatmap, warnings.Distinct().ToArray());
    }

    private static List<SimfileTag> parseTags(string text)
        => tagRegex().Matches(text)
                     .Select(match => new SimfileTag(
                         match.Groups["key"].Value.Trim(),
                         match.Groups["value"].Value.Trim()))
                     .ToList();

    private static Dictionary<string, string> collectGlobalTags(IReadOnlyList<SimfileTag> tags, bool isSsc)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (SimfileTag tag in tags)
        {
            if (isSsc && tag.Key.Equals("NOTEDATA", StringComparison.OrdinalIgnoreCase))
                break;

            if (!tag.Key.Equals("NOTES", StringComparison.OrdinalIgnoreCase))
                values[tag.Key] = tag.Value;
        }

        return values;
    }

    private static List<ChartBlock> collectSmCharts(IEnumerable<SimfileTag> tags)
    {
        var charts = new List<ChartBlock>();

        foreach (SimfileTag tag in tags.Where(static tag => tag.Key.Equals("NOTES", StringComparison.OrdinalIgnoreCase)))
        {
            string[] parts = tag.Value.Split([':'], 6);
            if (parts.Length < 6)
                continue;

            string description = parts[1].Trim();
            string difficulty = parts[2].Trim();
            string meter = parts[3].Trim();
            string notes = parts[5];
            int laneCount = detectLaneCount(notes);
            string displayName = string.IsNullOrWhiteSpace(description)
                ? $"{difficulty} {meter}".Trim()
                : description;

            charts.Add(new ChartBlock(
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                notes,
                laneCount,
                description,
                displayName));
        }

        return charts;
    }

    private static List<ChartBlock> collectSscCharts(IReadOnlyList<SimfileTag> tags)
    {
        var charts = new List<ChartBlock>();
        int index = 0;

        while (index < tags.Count)
        {
            if (!tags[index].Key.Equals("NOTEDATA", StringComparison.OrdinalIgnoreCase))
            {
                index++;
                continue;
            }

            index++;
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            while (index < tags.Count && !tags[index].Key.Equals("NOTEDATA", StringComparison.OrdinalIgnoreCase))
            {
                values[tags[index].Key] = tags[index].Value;
                index++;
            }

            string notes = values.GetValueOrDefault("NOTES", values.GetValueOrDefault("NOTES2", string.Empty));
            int laneCount = detectLaneCount(notes);
            string description = values.GetValueOrDefault("DESCRIPTION", string.Empty);
            string chartName = values.GetValueOrDefault("CHARTNAME", string.Empty);
            string difficultyAndMeter =
                $"{values.GetValueOrDefault("DIFFICULTY", "Chart")} {values.GetValueOrDefault("METER", string.Empty)}".Trim();
            string displayName = !string.IsNullOrWhiteSpace(chartName)
                ? chartName
                : !string.IsNullOrWhiteSpace(description)
                    ? description
                    : difficultyAndMeter;

            charts.Add(new ChartBlock(values, notes, laneCount, description, displayName));
        }

        return charts;
    }

    private static int detectLaneCount(string notes)
    {
        foreach (string measure in notes.Split(','))
        {
            foreach (string rawLine in measure.Replace("\r", string.Empty).Split('\n'))
            {
                string line = stripComment(rawLine).Trim();
                if (line.Length > 0)
                    return line.Length;
            }
        }

        return 0;
    }

    private static List<BeatNote> parseNotes(string notes, int laneCount, ICollection<string> warnings)
    {
        var result = new List<BeatNote>();
        var openHolds =
            new Dictionary<int, (double StartBeat, HoldNoteType Type)>();
        string[] measures = notes.Split(',');

        for (int measureIndex = 0; measureIndex < measures.Length; measureIndex++)
        {
            string[] rows = measures[measureIndex].Replace("\r", string.Empty)
                                                    .Split('\n')
                                                    .Select(stripComment)
                                                    .Select(static line => line.Trim())
                                                    .Where(static line => line.Length > 0)
                                                    .ToArray();
            if (rows.Length == 0)
                continue;

            for (int rowIndex = 0; rowIndex < rows.Length; rowIndex++)
            {
                string row = rows[rowIndex];
                if (row.Length != laneCount)
                    throw new InvalidDataException("Inconsistent lane count in StepMania note data.");

                double beat = measureIndex * 4 + rowIndex * 4d / rows.Length;

                for (int lane = 0; lane < laneCount; lane++)
                {
                    switch (char.ToUpperInvariant(row[lane]))
                    {
                        case '1':
                            result.Add(new BeatNote(
                                lane,
                                beat,
                                null,
                                HitObjectKind.Tap));
                            break;

                        case '2':
                            openHolds[lane] =
                                (beat, HoldNoteType.Standard);
                            break;

                        case '4':
                            openHolds[lane] =
                                (beat, HoldNoteType.Roll);
                            break;

                        case '3':
                            if (openHolds.Remove(
                                    lane,
                                    out (double StartBeat, HoldNoteType Type)
                                    openHold))
                            {
                                result.Add(new BeatNote(
                                    lane,
                                    openHold.StartBeat,
                                    beat,
                                    HitObjectKind.Hold,
                                    openHold.Type));
                            }
                            else
                                warnings.Add("Ignored a StepMania hold end without a matching start.");
                            break;

                        case 'M':
                            result.Add(new BeatNote(
                                lane,
                                beat,
                                null,
                                HitObjectKind.Mine));
                            break;

                        case 'F':
                            warnings.Add("StepMania fake notes are not represented by Yokko yet and were ignored.");
                            break;

                        case 'L':
                            warnings.Add("StepMania lift notes are not represented by Yokko yet and were ignored.");
                            break;
                    }
                }
            }
        }

        if (openHolds.Count > 0)
            warnings.Add($"Ignored {openHolds.Count} unterminated StepMania hold note(s).");

        return result;
    }

    private static List<BeatValue> parseBeatValues(string? text)
    {
        var values = new List<BeatValue>();

        foreach (string entry in (text ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] parts = entry.Split('=', 2);
            if (parts.Length != 2)
                continue;

            double beat = ImportParsing.Double(parts[0], double.NaN);
            double value = ImportParsing.Double(parts[1], double.NaN);
            if (!double.IsNaN(beat) && !double.IsNaN(value))
                values.Add(new BeatValue(beat, value));
        }

        return values;
    }

    private static string stripComment(string line)
    {
        int comment = line.IndexOf("//", StringComparison.Ordinal);
        return comment >= 0 ? line[..comment] : line;
    }

    private static bool isTimingKey(string key)
        => key.Equals("BPMS", StringComparison.OrdinalIgnoreCase)
           || key.Equals("STOPS", StringComparison.OrdinalIgnoreCase)
           || key.Equals("DELAYS", StringComparison.OrdinalIgnoreCase)
           || key.Equals("WARPS", StringComparison.OrdinalIgnoreCase)
           || key.Equals("FAKES", StringComparison.OrdinalIgnoreCase)
           || key.Equals("SCROLLS", StringComparison.OrdinalIgnoreCase)
           || key.Equals("SPEEDS", StringComparison.OrdinalIgnoreCase)
           || key.Equals("OFFSET", StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex(@"#(?<key>[^:;\r\n]+):(?<value>.*?);", RegexOptions.Singleline)]
    private static partial Regex tagRegex();

    private sealed record SimfileTag(string Key, string Value);

    private sealed record ChartBlock(
        Dictionary<string, string> Values,
        string Notes,
        int LaneCount,
        string Description,
        string DisplayName);

    private readonly record struct BeatValue(double Beat, double Value);

    private readonly record struct BeatNote(
        int Lane,
        double StartBeat,
        double? EndBeat,
        HitObjectKind Kind,
        HoldNoteType HoldType = HoldNoteType.Standard);
}
