using System.Globalization;
using System.Text;
using Yokko.Core.Beatmaps;
using Yokko.Core.Editing;
using Yokko.Core.Gameplay;
using Yokko.Core.Timing;

namespace Yokko.Import.Quaver;

public static class QuaverBeatmapIO
{
    public static void WriteEditableToFile(
        EditableBeatmap editable,
        string path)
    {
        ArgumentNullException.ThrowIfNull(editable);
        WriteToFile(editable.ToBeatmap(), path);
    }

    public static void WriteToFile(YokkoBeatmap beatmap, string path)
    {
        ArgumentNullException.ThrowIfNull(beatmap);
        if (beatmap.KeyMode is not (
                KeyMode.FourKey or KeyMode.SevenKey))
        {
            throw new InvalidDataException(
                "Quaver export supports pure 4K and 7K charts only.");
        }

        string? directory = Path.GetDirectoryName(
            Path.GetFullPath(path));
        if (directory != null)
            Directory.CreateDirectory(directory);

        var customPaths = beatmap.HitObjects
            .SelectMany(static hitObject => hitObject.Samples)
            .Select(static sample => sample.Filename)
            .Concat(beatmap.ScheduledSamples.Select(
                static sample => sample.Path))
            .Where(static samplePath =>
                !string.IsNullOrWhiteSpace(samplePath))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Cast<string>()
            .ToArray();
        var customIndices = customPaths
            .Select((samplePath, index) => (samplePath, index: index + 1))
            .ToDictionary(
                static item => item.samplePath,
                static item => item.index,
                StringComparer.OrdinalIgnoreCase);

        var output = new StringBuilder();
        append(output, "AudioFile", fileName(beatmap.AudioPath));
        append(output, "SongPreviewTime", number(
            beatmap.PreviewTimeMilliseconds));
        append(output, "Mode", $"Keys{(int)beatmap.KeyMode}");
        append(output, "HasScratchKey", "false");
        append(output, "Title", scalar(beatmap.Title));
        append(output, "Artist", scalar(beatmap.Artist));
        append(output, "Creator", scalar(beatmap.Creator));
        append(output, "DifficultyName", scalar(beatmap.DifficultyName));
        append(
            output,
            "BPMDoesNotAffectScrollVelocity",
            "true");
        append(
            output,
            "InitialScrollVelocity",
            number(beatmap.InitialScrollVelocity));
        append(
            output,
            "LegacyLNRendering",
            beatmap.LegacyLongNoteRendering ? "true" : "false");

        output.AppendLine("TimingPoints:");
        foreach (YokkoTimingPoint point in beatmap.TimingPoints)
        {
            output.AppendLine($"- StartTime: {number(point.TimeMilliseconds)}");
            output.AppendLine(
                $"  Bpm: {number(60000 / point.BeatLengthMilliseconds)}");
            output.AppendLine($"  TimeSignature: {point.Meter}|4");
        }

        writeVelocities(
            output,
            "SliderVelocities",
            beatmap.ScrollVelocities);
        writeFactors(
            output,
            "ScrollSpeedFactors",
            beatmap.ScrollSpeedFactors);

        if (beatmap.ScrollProfiles.Count > 0)
        {
            output.AppendLine("TimingGroups:");
            foreach ((string id, YokkoScrollProfile profile) in
                     beatmap.ScrollProfiles.OrderBy(
                         static item => item.Key,
                         StringComparer.Ordinal))
            {
                output.AppendLine($"  {scalar(id)}: !ScrollGroup");
                output.AppendLine(
                    $"    InitialScrollVelocity: {number(profile.InitialScrollVelocity)}");
                writeVelocities(
                    output,
                    "ScrollVelocities",
                    profile.ScrollVelocities,
                    "    ");
                writeFactors(
                    output,
                    "ScrollSpeedFactors",
                    profile.ScrollSpeedFactors,
                    "    ");
            }
        }

        if (customPaths.Length > 0)
        {
            output.AppendLine("CustomAudioSamples:");
            foreach (string samplePath in customPaths)
            {
                bool unaffected = beatmap.ScheduledSamples.Any(
                    sample => string.Equals(
                        sample.Path,
                        samplePath,
                        StringComparison.OrdinalIgnoreCase)
                              && sample.UnaffectedByRate);
                output.AppendLine($"- Path: {scalar(fileName(samplePath))}");
                if (unaffected)
                    output.AppendLine("  UnaffectedByRate: true");
            }
        }

        if (beatmap.ScheduledSamples.Count > 0)
        {
            output.AppendLine("SoundEffects:");
            foreach (YokkoScheduledSample sample in
                     beatmap.ScheduledSamples.OrderBy(
                         static sample => sample.TimeMilliseconds))
            {
                output.AppendLine(
                    $"- StartTime: {number(sample.TimeMilliseconds)}");
                output.AppendLine(
                    $"  Sample: {customIndices[sample.Path]}");
                output.AppendLine($"  Volume: {sample.Volume}");
            }
        }

        output.AppendLine("HitObjects:");
        foreach (YokkoHitObject hitObject in beatmap.HitObjects)
        {
            output.AppendLine(
                $"- StartTime: {number(hitObject.StartTimeMilliseconds)}");
            output.AppendLine($"  Lane: {hitObject.Lane + 1}");
            if (hitObject.EndTimeMilliseconds is double endTime)
                output.AppendLine($"  EndTime: {number(endTime)}");
            if (hitObject.Kind == HitObjectKind.Mine)
                output.AppendLine("  Type: Mine");
            if (!string.IsNullOrWhiteSpace(hitObject.ScrollProfileId))
            {
                output.AppendLine(
                    $"  TimingGroup: {scalar(hitObject.ScrollProfileId)}");
            }

            int flags = hitSoundFlags(hitObject.Samples);
            if (flags != 0)
                output.AppendLine($"  HitSound: {flags}");

            YokkoHitSample[] custom = hitObject.Samples
                .Where(static sample =>
                    !string.IsNullOrWhiteSpace(sample.Filename))
                .ToArray();
            if (custom.Length > 0)
            {
                output.AppendLine("  KeySounds:");
                foreach (YokkoHitSample sample in custom)
                {
                    output.AppendLine(
                        $"  - Sample: {customIndices[sample.Filename!]}");
                    output.AppendLine($"    Volume: {sample.Volume}");
                }
            }
        }

        File.WriteAllText(
            path,
            output.ToString(),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static void writeVelocities(
        StringBuilder output,
        string name,
        IReadOnlyList<YokkoScrollVelocity> velocities,
        string indentation = "")
    {
        output.AppendLine($"{indentation}{name}:");
        string itemIndentation = indentation.Length == 0
            ? string.Empty
            : indentation + "  ";
        foreach (YokkoScrollVelocity velocity in velocities)
        {
            output.AppendLine(
                $"{itemIndentation}- StartTime: {number(velocity.TimeMilliseconds)}");
            output.AppendLine(
                $"{itemIndentation}  Multiplier: {number(velocity.Multiplier)}");
        }
    }

    private static void writeFactors(
        StringBuilder output,
        string name,
        IReadOnlyList<YokkoScrollSpeedFactor> factors,
        string indentation = "")
    {
        output.AppendLine($"{indentation}{name}:");
        string itemIndentation = indentation.Length == 0
            ? string.Empty
            : indentation + "  ";
        foreach (YokkoScrollSpeedFactor factor in factors)
        {
            output.AppendLine(
                $"{itemIndentation}- StartTime: {number(factor.TimeMilliseconds)}");
            output.AppendLine(
                $"{itemIndentation}  Multiplier: {number(factor.Multiplier)}");
        }
    }

    private static int hitSoundFlags(
        IReadOnlyList<YokkoHitSample> samples)
    {
        int flags = 0;
        foreach (YokkoHitSample sample in samples.Where(
                     static sample => sample.Filename == null))
        {
            flags |= sample.Name switch
            {
                YokkoHitSample.HitNormal => 1,
                YokkoHitSample.HitWhistle => 2,
                YokkoHitSample.HitFinish => 4,
                YokkoHitSample.HitClap => 8,
                _ => 0,
            };
        }

        return flags is 0 or 1 ? 0 : flags;
    }

    private static void append(
        StringBuilder output,
        string key,
        string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            output.AppendLine($"{key}: {value}");
    }

    private static string scalar(string? value) =>
        $"'{(value ?? string.Empty).Replace("'", "''")}'";

    private static string? fileName(string? path) =>
        string.IsNullOrWhiteSpace(path)
            ? null
            : Path.GetFileName(path);

    private static string number(double value) =>
        value.ToString("R", CultureInfo.InvariantCulture);
}
