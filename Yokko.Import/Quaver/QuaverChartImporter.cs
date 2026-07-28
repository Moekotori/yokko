using Yokko.Core.Beatmaps;
using Yokko.Core.Gameplay;
using Yokko.Core.Timing;

namespace Yokko.Import.Quaver;

public sealed class QuaverChartImporter : IChartImporter
{
    public ChartImportCapability Capability { get; } =
        new(ChartSourceFormat.Quaver, "Quaver", [".qua"], true, false);

    public bool CanImport(string path)
        => string.Equals(Path.GetExtension(path), ".qua", StringComparison.OrdinalIgnoreCase);

    public ValueTask<ChartImportResult> ImportAsync(ChartImportRequest request)
    {
        request.CancellationToken.ThrowIfCancellationRequested();
        ParsedQua parsed = parse(File.ReadAllLines(request.Path));
        var warnings = new List<string>();

        KeyMode keyMode = parsed.Mode.ToUpperInvariant() switch
        {
            "KEYS4" => KeyMode.FourKey,
            "KEYS7" => KeyMode.SevenKey,
            _ => throw new InvalidDataException($"Unsupported Quaver mode: {parsed.Mode}."),
        };

        if (parsed.HasSliderVelocities)
            warnings.Add("Quaver slider velocities are not represented by Yokko yet and were ignored.");

        if (parsed.HitObjects.Any(static note => !string.IsNullOrWhiteSpace(note.HitSound)))
            warnings.Add("Quaver hitsound references were preserved on notes, but runtime keysound playback is not available yet.");

        IReadOnlyList<YokkoTimingPoint> timingPoints = parsed.TimingPoints.Count == 0
            ? [YokkoTimingPoint.Default]
            : parsed.TimingPoints.Select(static point => new YokkoTimingPoint(
                                           point.StartTime,
                                           point.Bpm > 0 ? 60000 / point.Bpm : 500,
                                           point.Meter))
                                 .ToArray();

        YokkoHitObject[] hitObjects = parsed.HitObjects.Select(note =>
        {
            int lane = note.Lane - 1;
            if (lane < 0 || lane >= (int)keyMode)
                throw new InvalidDataException($"Quaver lane {note.Lane} is outside {(int)keyMode}K.");

            bool isHold = note.EndTime > note.StartTime;
            return new YokkoHitObject(
                lane,
                note.StartTime,
                isHold ? note.EndTime : null,
                isHold ? HitObjectKind.Hold : HitObjectKind.Tap,
                string.IsNullOrWhiteSpace(note.HitSound) ? null : note.HitSound);
        }).OrderBy(static note => note.StartTimeMilliseconds)
          .ThenBy(static note => note.Lane)
          .ToArray();

        var beatmap = new YokkoBeatmap(
            parsed.Values.GetValueOrDefault("Title", "Untitled"),
            parsed.Values.GetValueOrDefault("Artist", "Unknown Artist"),
            parsed.Values.GetValueOrDefault("Creator", "Unknown Creator"),
            parsed.Values.GetValueOrDefault("DifficultyName", $"{(int)keyMode}K"),
            keyMode,
            ChartSourceFormat.Quaver,
            timingPoints,
            ImportParsing.ResolveAdjacentAsset(request.Path, parsed.Values.GetValueOrDefault("AudioFile")),
            hitObjects);

        return ValueTask.FromResult(new ChartImportResult(beatmap, warnings));
    }

    private static ParsedQua parse(IEnumerable<string> lines)
    {
        var parsed = new ParsedQua();
        string section = string.Empty;
        QuaTimingPoint? timingPoint = null;
        QuaHitObject? hitObject = null;

        foreach (string rawLine in lines)
        {
            string trimmed = rawLine.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
                continue;

            bool isTopLevel = !char.IsWhiteSpace(rawLine, 0) && !trimmed.StartsWith('-');
            if (isTopLevel)
            {
                int topLevelSeparator = trimmed.IndexOf(':');
                if (topLevelSeparator < 0)
                    continue;

                string topLevelKey = trimmed[..topLevelSeparator].Trim();
                string topLevelValue = ImportParsing.Scalar(trimmed[(topLevelSeparator + 1)..]);
                bool isCollection = topLevelKey is "TimingPoints" or "SliderVelocities" or "HitObjects"
                    or "SoundEffects" or "EditorLayers" or "Bookmarks" or "CustomAudioSamples";

                if (!isCollection)
                {
                    section = string.Empty;
                    parsed.Values[topLevelKey] = topLevelValue;
                    continue;
                }

                section = topLevelKey;
                timingPoint = null;
                hitObject = null;

                if (section.Equals("SliderVelocities", StringComparison.OrdinalIgnoreCase)
                    && topLevelValue != "[]")
                    parsed.HasSliderVelocities = true;

                continue;
            }

            int separator = trimmed.IndexOf(':');
            if (separator < 0)
                continue;

            string key = trimmed.TrimStart('-').Trim()[..trimmed.TrimStart('-').Trim().IndexOf(':')].Trim();
            string value = ImportParsing.Scalar(trimmed[(separator + 1)..]);
            bool startsItem = rawLine.Length == trimmed.Length && trimmed.StartsWith('-');

            if (section.Equals("SliderVelocities", StringComparison.OrdinalIgnoreCase))
            {
                parsed.HasSliderVelocities = true;
                continue;
            }

            if (section.Equals("TimingPoints", StringComparison.OrdinalIgnoreCase))
            {
                if (startsItem)
                {
                    timingPoint = new QuaTimingPoint();
                    parsed.TimingPoints.Add(timingPoint);
                }

                timingPoint ??= addTimingPoint(parsed);
                assignTimingPoint(timingPoint, key, value);
                continue;
            }

            if (section.Equals("HitObjects", StringComparison.OrdinalIgnoreCase))
            {
                if (startsItem)
                {
                    hitObject = new QuaHitObject();
                    parsed.HitObjects.Add(hitObject);
                }

                hitObject ??= addHitObject(parsed);
                assignHitObject(hitObject, key, value);
                continue;
            }

        }

        parsed.Mode = parsed.Values.GetValueOrDefault("Mode", string.Empty);
        return parsed;
    }

    private static QuaTimingPoint addTimingPoint(ParsedQua parsed)
    {
        var point = new QuaTimingPoint();
        parsed.TimingPoints.Add(point);
        return point;
    }

    private static QuaHitObject addHitObject(ParsedQua parsed)
    {
        var note = new QuaHitObject();
        parsed.HitObjects.Add(note);
        return note;
    }

    private static void assignTimingPoint(QuaTimingPoint point, string key, string value)
    {
        if (key.Equals("StartTime", StringComparison.OrdinalIgnoreCase))
            point.StartTime = ImportParsing.Double(value);
        else if (key.Equals("Bpm", StringComparison.OrdinalIgnoreCase))
            point.Bpm = ImportParsing.Double(value, 120);
        else if (key.Equals("TimeSignature", StringComparison.OrdinalIgnoreCase))
        {
            string numerator = value.Split(['/', '|'])[0];
            point.Meter = Math.Max(1, ImportParsing.Int(numerator, 4));
        }
    }

    private static void assignHitObject(QuaHitObject note, string key, string value)
    {
        if (key.Equals("StartTime", StringComparison.OrdinalIgnoreCase))
            note.StartTime = ImportParsing.Double(value);
        else if (key.Equals("EndTime", StringComparison.OrdinalIgnoreCase))
            note.EndTime = ImportParsing.Double(value);
        else if (key.Equals("Lane", StringComparison.OrdinalIgnoreCase))
            note.Lane = ImportParsing.Int(value);
        else if (key.Equals("HitSound", StringComparison.OrdinalIgnoreCase))
            note.HitSound = value;
    }

    private sealed class ParsedQua
    {
        public Dictionary<string, string> Values { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<QuaTimingPoint> TimingPoints { get; } = [];
        public List<QuaHitObject> HitObjects { get; } = [];
        public string Mode { get; set; } = string.Empty;
        public bool HasSliderVelocities { get; set; }
    }

    private sealed class QuaTimingPoint
    {
        public double StartTime { get; set; }
        public double Bpm { get; set; } = 120;
        public int Meter { get; set; } = 4;
    }

    private sealed class QuaHitObject
    {
        public double StartTime { get; set; }
        public double EndTime { get; set; }
        public int Lane { get; set; }
        public string? HitSound { get; set; }
    }
}
