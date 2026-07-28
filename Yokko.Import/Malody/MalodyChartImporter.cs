using System.Text.Json;
using Yokko.Core.Beatmaps;
using Yokko.Core.Gameplay;

namespace Yokko.Import.Malody;

public sealed class MalodyChartImporter : IChartImporter
{
    public ChartImportCapability Capability { get; } =
        new(ChartSourceFormat.Malody, "Malody Key", [".mc", ".mcz"], true, false);

    public bool CanImport(string path)
    {
        string extension = Path.GetExtension(path);
        return extension.Equals(".mc", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".mcz", StringComparison.OrdinalIgnoreCase);
    }

    public ValueTask<ChartImportResult> ImportAsync(ChartImportRequest request)
    {
        request.CancellationToken.ThrowIfCancellationRequested();

        if (Path.GetExtension(request.Path).Equals(".mcz", StringComparison.OrdinalIgnoreCase))
        {
            IReadOnlyList<string> charts = ChartArchive.ExtractCharts(request.Path, ".mc");
            var failures = new List<Exception>();

            foreach (string chart in charts)
            {
                try
                {
                    ChartImportResult result = ImportAsync(request with { Path = chart }).AsTask().GetAwaiter().GetResult();
                    IReadOnlyList<string> packageWarnings = charts.Count > 1
                        ? [$"This .mcz contains {charts.Count} charts; imported {Path.GetFileName(chart)}.", .. result.Warnings]
                        : result.Warnings;
                    return ValueTask.FromResult(result with { Warnings = packageWarnings });
                }
                catch (InvalidDataException ex)
                {
                    failures.Add(ex);
                }
            }

            throw new InvalidDataException("The .mcz package does not contain a supported 4K/7K Malody Key chart.", failures.FirstOrDefault());
        }

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(request.Path));
        JsonElement root = document.RootElement;
        JsonElement meta = root.GetProperty("meta");

        int mode = getInt(meta, "mode", 0);
        if (mode != 0)
            throw new InvalidDataException($"Only Malody Key mode (mode 0) is supported, found mode {mode}.");

        JsonElement modeExtension = meta.TryGetProperty("mode_ext", out JsonElement extension)
            ? extension
            : default;
        int laneCount = modeExtension.ValueKind == JsonValueKind.Object
            ? getInt(modeExtension, "column", 4)
            : 4;
        KeyMode keyMode = laneCount switch
        {
            4 => KeyMode.FourKey,
            7 => KeyMode.SevenKey,
            _ => throw new InvalidDataException($"Unsupported Malody key count: {laneCount}."),
        };

        var tempoChanges = new List<TempoChange>();
        if (root.TryGetProperty("time", out JsonElement timing) && timing.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement point in timing.EnumerateArray())
            {
                if (!point.TryGetProperty("beat", out JsonElement beat))
                    continue;

                double bpm = getDouble(point, "bpm", 120);
                if (bpm > 0)
                    tempoChanges.Add(new TempoChange(readBeat(beat), bpm));
            }
        }

        if (tempoChanges.Count == 0)
            tempoChanges.Add(new TempoChange(0, 120));

        JsonElement[] sourceNotes = root.TryGetProperty("note", out JsonElement notes)
            && notes.ValueKind == JsonValueKind.Array
            ? notes.EnumerateArray().ToArray()
            : [];

        JsonElement? audioEvent = null;
        foreach (JsonElement note in sourceNotes)
        {
            if (note.TryGetProperty("sound", out _)
                && getInt(note, "type", 0) == 1
                && !note.TryGetProperty("column", out _))
            {
                audioEvent = note;
                break;
            }
        }

        double audioStartBeat = audioEvent is { } eventValue
                                && eventValue.TryGetProperty("beat", out JsonElement audioBeat)
            ? readBeat(audioBeat)
            : 0;
        double audioOffset = audioEvent is { } audioValue ? getDouble(audioValue, "offset", 0) : 0;
        var baseConverter = new BeatTimeConverter(tempoChanges);
        double audioStartMilliseconds = baseConverter.ToMilliseconds(audioStartBeat) + audioOffset;
        var converter = new BeatTimeConverter(tempoChanges, offsetMilliseconds: -audioStartMilliseconds);
        var warnings = new List<string>();
        var hitObjects = new List<YokkoHitObject>();

        foreach (JsonElement note in sourceNotes)
        {
            if (!note.TryGetProperty("column", out JsonElement columnElement)
                || !note.TryGetProperty("beat", out JsonElement beatElement))
                continue;

            int lane = columnElement.GetInt32();
            if (lane < 0 || lane >= laneCount)
            {
                warnings.Add($"Ignored Malody note in unsupported lane {lane}.");
                continue;
            }

            double startTime = converter.ToMilliseconds(readBeat(beatElement));
            double? endTime = note.TryGetProperty("endbeat", out JsonElement endBeat)
                ? converter.ToMilliseconds(readBeat(endBeat))
                : null;
            string? sample = note.TryGetProperty("sound", out JsonElement sound)
                ? ImportParsing.ResolveAdjacentAsset(request.Path, sound.GetString())
                : null;

            hitObjects.Add(new YokkoHitObject(
                lane,
                startTime,
                endTime,
                endTime.HasValue ? HitObjectKind.Hold : HitObjectKind.Tap,
                sample));
        }

        if (hitObjects.Any(static note => note.SampleKey != null))
            warnings.Add("Malody keysound references were preserved on notes, but runtime keysound playback is not available yet.");

        string? audioFile = audioEvent is { } audio
            && audio.TryGetProperty("sound", out JsonElement audioSound)
            ? audioSound.GetString()
            : null;
        JsonElement song = meta.TryGetProperty("song", out JsonElement songValue) ? songValue : default;

        var beatmap = new YokkoBeatmap(
            getString(song, "title", "Untitled"),
            getString(song, "artist", "Unknown Artist"),
            getString(meta, "creator", "Unknown Creator"),
            getString(meta, "version", $"{laneCount}K"),
            keyMode,
            ChartSourceFormat.Malody,
            converter.ToTimingPoints(),
            ImportParsing.ResolveAdjacentAsset(request.Path, audioFile),
            hitObjects.OrderBy(static note => note.StartTimeMilliseconds)
                      .ThenBy(static note => note.Lane)
                      .ToArray());

        return ValueTask.FromResult(new ChartImportResult(beatmap, warnings));
    }

    private static double readBeat(JsonElement beat)
    {
        int[] values = beat.EnumerateArray().Select(static value => value.GetInt32()).ToArray();
        if (values.Length < 3 || values[2] == 0)
            throw new InvalidDataException("Invalid Malody beat position.");

        return values[0] + values[1] / (double)values[2];
    }

    private static string getString(JsonElement element, string property, string fallback)
        => element.ValueKind == JsonValueKind.Object
           && element.TryGetProperty(property, out JsonElement value)
           && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? fallback
            : fallback;

    private static int getInt(JsonElement element, string property, int fallback)
        => element.ValueKind == JsonValueKind.Object
           && element.TryGetProperty(property, out JsonElement value)
           && value.TryGetInt32(out int parsed)
            ? parsed
            : fallback;

    private static double getDouble(JsonElement element, string property, double fallback)
        => element.ValueKind == JsonValueKind.Object
           && element.TryGetProperty(property, out JsonElement value)
           && value.TryGetDouble(out double parsed)
            ? parsed
            : fallback;
}
