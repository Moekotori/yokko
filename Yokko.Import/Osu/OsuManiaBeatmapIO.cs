using System.Globalization;
using System.Text;
using Yokko.Core.Beatmaps;
using Yokko.Core.Editing;
using Yokko.Core.Gameplay;

namespace Yokko.Import.Osu;

public static class OsuManiaBeatmapIO
{
    private const int hitCircleType = 1;
    private const int holdType = 128;

    public static EditableBeatmap ReadEditableFromFile(string path)
    {
        EditableBeatmap editable = EditableBeatmap.FromBeatmap(ReadBeatmap(File.ReadAllText(path, Encoding.UTF8)), path);
        editable.AudioPath = resolveAudioPath(path, editable.AudioPath);
        return editable;
    }

    public static YokkoBeatmap ReadBeatmapFromFile(string path)
        => ReadBeatmap(File.ReadAllText(path, Encoding.UTF8));

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

        return new YokkoBeatmap(
            metadata.GetValueOrDefault("Title", "Untitled"),
            metadata.GetValueOrDefault("Artist", "Unknown Artist"),
            metadata.GetValueOrDefault("Creator", "Unknown Creator"),
            metadata.GetValueOrDefault("Version", $"{keyCount}K"),
            keyMode,
            ChartSourceFormat.OsuMania,
            general.GetValueOrDefault("AudioFilename"),
            hitObjects);
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
        builder.AppendLine("OverallDifficulty:8");
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
        builder.AppendLine("0,500,4,2,0,100,1,0");
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
