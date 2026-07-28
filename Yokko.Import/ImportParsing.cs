using System.Globalization;
using Yokko.Core.Timing;

namespace Yokko.Import;

internal static class ImportParsing
{
    public static double Double(string? value, double fallback = 0)
        => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)
            ? parsed
            : fallback;

    public static int Int(string? value, int fallback = 0)
        => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
            ? parsed
            : fallback;

    public static string Scalar(string value)
    {
        value = value.Trim();

        if (value.Length >= 2
            && ((value[0] == '\'' && value[^1] == '\'')
                || (value[0] == '"' && value[^1] == '"')))
            return value[1..^1];

        return value;
    }

    public static string? ResolveAdjacentAsset(string chartPath, string? assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath))
            return null;

        assetPath = assetPath.Trim().Trim('"');

        if (Path.IsPathRooted(assetPath))
            return File.Exists(assetPath) ? assetPath : null;

        string directory = Path.GetFullPath(Path.GetDirectoryName(Path.GetFullPath(chartPath))!);
        string candidate = Path.GetFullPath(Path.Combine(directory, assetPath.Replace('/', Path.DirectorySeparatorChar)));
        string directoryPrefix = directory.EndsWith(Path.DirectorySeparatorChar)
            ? directory
            : directory + Path.DirectorySeparatorChar;

        if (!candidate.StartsWith(directoryPrefix, StringComparison.OrdinalIgnoreCase))
            return null;

        if (File.Exists(candidate))
            return candidate;

        string? fileName = Path.GetFileName(candidate);
        return Directory.EnumerateFiles(directory)
                        .FirstOrDefault(path => string.Equals(Path.GetFileName(path), fileName, StringComparison.OrdinalIgnoreCase));
    }
}

internal sealed class BeatTimeConverter
{
    private const double epsilon = 0.0000001;
    private readonly TempoChange[] tempoChanges;
    private readonly PauseEvent[] pauses;

    public BeatTimeConverter(
        IEnumerable<TempoChange> tempoChanges,
        IEnumerable<PauseEvent>? pauses = null,
        double offsetMilliseconds = 0)
    {
        TempoChange[] ordered = tempoChanges.Where(static change => change.BeatsPerMinute > 0)
                                             .OrderBy(static change => change.Beat)
                                             .GroupBy(static change => change.Beat)
                                             .Select(static group => group.Last())
                                             .ToArray();

        if (ordered.Length == 0)
            ordered = [new TempoChange(0, 120)];
        else if (ordered[0].Beat > 0)
            ordered = [new TempoChange(0, ordered[0].BeatsPerMinute), .. ordered];

        this.tempoChanges = ordered;
        this.pauses = pauses?.Where(static pause => pause.DurationMilliseconds > 0)
                             .OrderBy(static pause => pause.Beat)
                             .ToArray() ?? [];
        OffsetMilliseconds = offsetMilliseconds;
    }

    public double OffsetMilliseconds { get; }

    public double ToMilliseconds(double beat)
    {
        double time = OffsetMilliseconds;
        double cursorBeat = 0;
        double currentBpm = tempoAt(0);

        foreach (TempoChange change in tempoChanges)
        {
            if (change.Beat <= 0 || change.Beat >= beat)
                continue;

            time += (change.Beat - cursorBeat) * 60000 / currentBpm;
            cursorBeat = change.Beat;
            currentBpm = change.BeatsPerMinute;
        }

        time += (beat - cursorBeat) * 60000 / currentBpm;
        time += pauses.Where(pause => pause.Beat < beat - epsilon)
                      .Sum(static pause => pause.DurationMilliseconds);
        return time;
    }

    public IReadOnlyList<YokkoTimingPoint> ToTimingPoints(int meter = 4)
        => tempoChanges.Select(change => new YokkoTimingPoint(
                             ToMilliseconds(change.Beat),
                             60000 / change.BeatsPerMinute,
                             meter))
                       .ToArray();

    public double TempoAt(double beat) => tempoAt(beat);

    private double tempoAt(double beat)
    {
        double bpm = tempoChanges[0].BeatsPerMinute;

        foreach (TempoChange change in tempoChanges)
        {
            if (change.Beat > beat)
                break;

            bpm = change.BeatsPerMinute;
        }

        return bpm;
    }
}

internal readonly record struct TempoChange(double Beat, double BeatsPerMinute);

internal readonly record struct PauseEvent(double Beat, double DurationMilliseconds);
