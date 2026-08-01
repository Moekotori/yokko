using System.Text.Json;
using Yokko.Core.Beatmaps;
using Yokko.Core.Gameplay;
using Yokko.Core.Timing;

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

        if (!isPackage(request.Path))
            return ValueTask.FromResult(importChart(request));

        (IReadOnlyList<ChartImportResult> results, int chartCount, _) =
            importPackage(request);
        ChartImportResult result = results[0];
        if (chartCount > 1)
        {
            result = result with
            {
                Warnings =
                [
                    $"This .mcz contains {chartCount} charts; imported {result.Beatmap.DifficultyName}.",
                    .. result.Warnings,
                ],
            };
        }

        return ValueTask.FromResult(result);
    }

    public ValueTask<IReadOnlyList<ChartImportResult>> ImportAllAsync(
        ChartImportRequest request)
    {
        request.CancellationToken.ThrowIfCancellationRequested();

        if (!isPackage(request.Path))
        {
            return ValueTask.FromResult<IReadOnlyList<ChartImportResult>>(
                [importChart(request)]);
        }

        (IReadOnlyList<ChartImportResult> results, _, int failureCount) =
            importPackage(request);
        if (failureCount == 0)
            return ValueTask.FromResult(results);

        string warning =
            $"Skipped {failureCount} unsupported or invalid chart{(failureCount == 1 ? string.Empty : "s")} in this .mcz package.";
        return ValueTask.FromResult<IReadOnlyList<ChartImportResult>>(
            results.Select(result => result with
                   {
                       Warnings = [warning, .. result.Warnings],
                   })
                   .ToArray());
    }

    private static ChartImportResult importChart(ChartImportRequest request)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(
                File.ReadAllText(request.Path));
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("meta", out JsonElement meta)
                || meta.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException(
                    "Malody chart is missing its meta object.");
            }

            int mode = getInt(meta, "mode", 0);
            if (mode != 0)
            {
                throw new InvalidDataException(
                    $"Only Malody Key mode (mode 0) is supported, found mode {mode}.");
            }

            JsonElement modeExtension =
                meta.TryGetProperty("mode_ext", out JsonElement extension)
                    ? extension
                    : default;
            int laneCount = modeExtension.ValueKind == JsonValueKind.Object
                ? getInt(modeExtension, "column", 4)
                : 4;
            if (laneCount is < 4 or > 9)
            {
                throw new InvalidDataException(
                    $"Unsupported Malody key count: {laneCount}. Yokko supports Malody Key charts from 4K through 9K.");
            }

            KeyMode keyMode = (KeyMode)laneCount;
            var warnings = new List<string>();
            TempoChange[] tempoChanges = readTempoChanges(root, warnings);
            JsonElement[] sourceNotes = readArray(root, "note");
            int audioEventIndex = findBackgroundAudioEvent(sourceNotes);
            JsonElement? audioEvent = audioEventIndex >= 0
                ? sourceNotes[audioEventIndex]
                : null;

            double audioStartBeat = audioEvent is { } eventValue
                                    && eventValue.TryGetProperty(
                                        "beat",
                                        out JsonElement audioBeat)
                ? readBeat(audioBeat)
                : 0;
            double audioOffset = audioEvent is { } audioValue
                ? getDouble(audioValue, "offset", 0)
                : 0;
            var baseConverter = new BeatTimeConverter(tempoChanges);
            double audioStartMilliseconds =
                baseConverter.ToMilliseconds(audioStartBeat) + audioOffset;
            var converter = new BeatTimeConverter(
                tempoChanges,
                offsetMilliseconds: -audioStartMilliseconds);

            string? audioFile = audioEvent is { } audio
                ? getString(audio, "sound", null)
                : null;
            string? audioPath = ImportParsing.ResolveAdjacentAsset(
                request.Path,
                audioFile);
            if (!string.IsNullOrWhiteSpace(audioFile) && audioPath == null)
                warnings.Add($"Malody background audio asset was missing: {audioFile}.");
            else if (audioEvent == null)
                warnings.Add("Malody chart has no background audio event; keysound-only playback may be incomplete.");

            YokkoHitObject[] hitObjects = readHitObjects(
                request,
                sourceNotes,
                laneCount,
                converter,
                warnings);
            YokkoScheduledSample[] scheduledSamples = readScheduledSamples(
                request.Path,
                sourceNotes,
                audioEventIndex,
                converter,
                warnings);
            ScrollVelocityProfile scrollProfile = readScrollProfile(
                root,
                converter,
                hitObjects,
                warnings);

            JsonElement song = meta.TryGetProperty(
                "song",
                out JsonElement songValue)
                ? songValue
                : default;
            double previewTime = getDouble(meta, "preview", -1);
            if (!double.IsFinite(previewTime) || previewTime < 0)
                previewTime = -1;

            var beatmap = new YokkoBeatmap(
                getString(song, "title", "Untitled") ?? "Untitled",
                getString(song, "artist", "Unknown Artist")
                ?? "Unknown Artist",
                getString(meta, "creator", "Unknown Creator")
                ?? "Unknown Creator",
                getString(meta, "version", $"{laneCount}K")
                ?? $"{laneCount}K",
                keyMode,
                ChartSourceFormat.Malody,
                converter.ToTimingPoints(),
                audioPath,
                hitObjects,
                ScrollVelocities: scrollProfile.Changes,
                InitialScrollVelocity: scrollProfile.InitialMultiplier,
                PreviewTimeMilliseconds: previewTime,
                ScheduledSamples: scheduledSamples);
            string? artworkPath = ImportParsing.ResolveAdjacentAsset(
                request.Path,
                getString(meta, "background", null));

            return new ChartImportResult(
                beatmap,
                warnings.Distinct().ToArray(),
                artworkPath);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException(
                $"Invalid Malody JSON in {Path.GetFileName(request.Path)}.",
                ex);
        }
    }

    private static TempoChange[] readTempoChanges(
        JsonElement root,
        ICollection<string> warnings)
    {
        var tempoChanges = new List<TempoChange>();
        foreach (JsonElement point in readArray(root, "time"))
        {
            if (point.ValueKind != JsonValueKind.Object)
                continue;
            if (!point.TryGetProperty("beat", out JsonElement beat))
                continue;

            double bpm = getDouble(point, "bpm", double.NaN);
            if (double.IsFinite(bpm) && bpm > 0)
                tempoChanges.Add(new TempoChange(readBeat(beat), bpm));
            else
                warnings.Add("Ignored a Malody timing point with an invalid BPM.");
        }

        if (tempoChanges.Count == 0)
        {
            warnings.Add("Malody chart has no valid BPM points; assumed 120 BPM.");
            tempoChanges.Add(new TempoChange(0, 120));
        }

        return tempoChanges.ToArray();
    }

    private static YokkoHitObject[] readHitObjects(
        ChartImportRequest request,
        IReadOnlyList<JsonElement> sourceNotes,
        int laneCount,
        BeatTimeConverter converter,
        ICollection<string> warnings)
    {
        var hitObjects = new List<YokkoHitObject>();
        foreach (JsonElement note in sourceNotes)
        {
            if (note.ValueKind != JsonValueKind.Object)
                continue;
            if (getInt(note, "type", 0) == 1)
                continue;
            if (!note.TryGetProperty("column", out JsonElement columnElement)
                || !columnElement.TryGetInt32(out int lane)
                || !note.TryGetProperty("beat", out JsonElement beatElement))
            {
                continue;
            }

            if (lane < 0 || lane >= laneCount)
            {
                warnings.Add($"Ignored Malody note in unsupported lane {lane}.");
                continue;
            }

            double startTime = converter.ToMilliseconds(readBeat(beatElement));
            double? endTime = note.TryGetProperty(
                "endbeat",
                out JsonElement endBeat)
                ? converter.ToMilliseconds(readBeat(endBeat))
                : null;
            if (endTime < startTime)
            {
                warnings.Add(
                    $"Ignored a Malody hold in lane {lane + 1} whose end precedes its start.");
                continue;
            }

            string? sample = request.PreferKeysounds
                ? ImportParsing.ResolveAdjacentAsset(
                    request.Path,
                    getString(note, "sound", null))
                : null;
            int volume = Math.Clamp(getInt(note, "vol", 100), 0, 100);
            YokkoHitSamplePayload? samplePayload = sample == null
                ? null
                : new YokkoHitSamplePayload(
                [
                    new YokkoHitSample(
                        YokkoHitSample.HitNormal,
                        Volume: volume,
                        Filename: sample),
                ]);

            hitObjects.Add(new YokkoHitObject(
                lane,
                startTime,
                endTime,
                endTime.HasValue ? HitObjectKind.Hold : HitObjectKind.Tap,
                sample,
                SamplePayload: samplePayload));
        }

        return hitObjects.OrderBy(static note => note.StartTimeMilliseconds)
                         .ThenBy(static note => note.Lane)
                         .ToArray();
    }

    private static YokkoScheduledSample[] readScheduledSamples(
        string chartPath,
        IReadOnlyList<JsonElement> sourceNotes,
        int audioEventIndex,
        BeatTimeConverter converter,
        ICollection<string> warnings)
    {
        var samples = new List<YokkoScheduledSample>();
        for (int index = 0; index < sourceNotes.Count; index++)
        {
            JsonElement note = sourceNotes[index];
            if (note.ValueKind != JsonValueKind.Object)
                continue;
            if (index == audioEventIndex
                || getInt(note, "type", 0) != 1
                || !note.TryGetProperty("beat", out JsonElement beat))
            {
                continue;
            }

            string? sound = getString(note, "sound", null);
            string? path = ImportParsing.ResolveAdjacentAsset(chartPath, sound);
            if (path == null)
            {
                if (!string.IsNullOrWhiteSpace(sound))
                    warnings.Add($"Malody autoplay sample was missing: {sound}.");
                continue;
            }

            samples.Add(new YokkoScheduledSample(
                converter.ToMilliseconds(readBeat(beat))
                + getDouble(note, "offset", 0),
                path,
                Math.Clamp(getInt(note, "vol", 100), 0, 100)));
        }

        return samples.OrderBy(static sample => sample.TimeMilliseconds)
                      .ToArray();
    }

    private static ScrollVelocityProfile readScrollProfile(
        JsonElement root,
        BeatTimeConverter converter,
        IReadOnlyList<YokkoHitObject> hitObjects,
        ICollection<string> warnings)
    {
        var effects = new List<YokkoScrollVelocity>();
        bool hasUnsupportedJump = false;
        foreach (JsonElement effect in readArray(root, "effect"))
        {
            if (effect.ValueKind != JsonValueKind.Object)
                continue;
            if (!effect.TryGetProperty("beat", out JsonElement beat))
                continue;

            double? multiplier = tryGetDouble(effect, "scroll")
                                 ?? tryGetDouble(effect, "sv");
            if (multiplier is double value && double.IsFinite(value))
            {
                effects.Add(new YokkoScrollVelocity(
                    converter.ToMilliseconds(readBeat(beat)),
                    value));
            }

            hasUnsupportedJump |= effect.TryGetProperty("jump", out _);
        }

        if (hasUnsupportedJump)
        {
            warnings.Add(
                "Malody jump effects are not represented by Yokko yet and were ignored.");
        }

        return ScrollVelocityConversion.FromMalody(
            converter.ToTimingPoints(),
            hitObjects,
            effects);
    }

    private static int findBackgroundAudioEvent(
        IReadOnlyList<JsonElement> sourceNotes)
    {
        // Malody writes the full-song autoplay event after chart notes. Earlier
        // type-1 events are timeline samples and must not be mistaken for BGM.
        for (int index = sourceNotes.Count - 1; index >= 0; index--)
        {
            JsonElement note = sourceNotes[index];
            if (note.ValueKind != JsonValueKind.Object)
                continue;
            if (getInt(note, "type", 0) == 1
                && !note.TryGetProperty("column", out _)
                && !string.IsNullOrWhiteSpace(getString(note, "sound", null)))
            {
                return index;
            }
        }

        return -1;
    }

    private static (
        IReadOnlyList<ChartImportResult> Results,
        int ChartCount,
        int FailureCount) importPackage(ChartImportRequest request)
    {
        IReadOnlyList<string> charts = ChartArchive.ExtractCharts(
            request.Path,
            ".mc");
        var results = new List<ChartImportResult>();
        var failures = new List<Exception>();

        foreach (string chart in charts)
        {
            request.CancellationToken.ThrowIfCancellationRequested();
            try
            {
                results.Add(importChart(request with { Path = chart }));
            }
            catch (InvalidDataException ex)
            {
                failures.Add(ex);
            }
        }

        if (results.Count == 0)
        {
            throw new InvalidDataException(
                "The .mcz package does not contain a supported 4K-9K Malody Key chart.",
                failures.FirstOrDefault());
        }

        return (results, charts.Count, failures.Count);
    }

    private static bool isPackage(string path) =>
        Path.GetExtension(path).Equals(
            ".mcz",
            StringComparison.OrdinalIgnoreCase);

    private static JsonElement[] readArray(JsonElement element, string property)
        => element.TryGetProperty(property, out JsonElement value)
           && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().ToArray()
            : [];

    private static double readBeat(JsonElement beat)
    {
        if (beat.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Invalid Malody beat position.");

        JsonElement[] values = beat.EnumerateArray().ToArray();
        if (values.Length < 3
            || !values[0].TryGetInt32(out int whole)
            || !values[1].TryGetInt32(out int numerator)
            || !values[2].TryGetInt32(out int denominator)
            || denominator == 0)
        {
            throw new InvalidDataException("Invalid Malody beat position.");
        }

        return whole + numerator / (double)denominator;
    }

    private static string? getString(
        JsonElement element,
        string property,
        string? fallback)
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

    private static double getDouble(
        JsonElement element,
        string property,
        double fallback)
        => tryGetDouble(element, property) ?? fallback;

    private static double? tryGetDouble(JsonElement element, string property)
        => element.ValueKind == JsonValueKind.Object
           && element.TryGetProperty(property, out JsonElement value)
           && value.TryGetDouble(out double parsed)
            ? parsed
            : null;
}
