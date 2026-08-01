using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Yokko.Core.Beatmaps;
using Yokko.Core.Mods;
using Yokko.Core.Scoring;

namespace Yokko.Game.Gameplay;

internal sealed record YokkoReplayLoadResult(
    string BeatmapFingerprint,
    string SourceHash,
    int KeyCount,
    DateTimeOffset RecordedAt,
    GameplayReplay Replay);

internal static class YokkoReplayIO
{
    public const string FileExtension = ".ykr";
    private const int schema_version = 3;
    private const long maximum_file_bytes = 128L * 1024 * 1024;

    private static readonly JsonSerializerOptions json_options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static void Write(
        Stream stream,
        YokkoBeatmap originalBeatmap,
        YokkoBeatmap appliedBeatmap,
        GameplayReplay replay,
        string sourceHash = null,
        DateTimeOffset? recordedAt = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(originalBeatmap);
        ArgumentNullException.ThrowIfNull(appliedBeatmap);
        ArgumentNullException.ThrowIfNull(replay);
        if (!stream.CanWrite)
            throw new ArgumentException("The replay stream is not writable.", nameof(stream));

        int keyCount = (int)appliedBeatmap.KeyMode;
        validateInputs(replay.Inputs, keyCount);
        validateFrames(replay.Frames, keyCount);
        string beatmapFingerprint =
            YokkoBeatmapFingerprint.Compute(originalBeatmap);
        string normalizedSourceHash = string.IsNullOrWhiteSpace(sourceHash)
            ? null
            : sourceHash.Trim();
        DateTimeOffset timestamp = recordedAt ?? DateTimeOffset.UtcNow;
        JudgementConfiguration activeJudgement =
            replay.JudgementConfiguration
            ?? JudgementConfiguration.YokkoDefault;
        ManiaModConfigurationEnvelope modConfiguration =
            ManiaModConfigurationCodec.Capture(replay.Mods);
        YokkoReplayFrameDocument[] frames = replay.Frames
            .Select(static frame => new YokkoReplayFrameDocument(
                frame.TimeMilliseconds,
                frame.PressedLanes))
            .ToArray();
        var document = new YokkoReplayDocument(
            schema_version,
            "mania",
            beatmapFingerprint,
            normalizedSourceHash,
            keyCount,
            timestamp,
            activeJudgement.Mode.ToString(),
            activeJudgement.EtternaJustice,
            modConfiguration,
            Inputs: null,
            Frames: frames,
            ClientVersion:
                typeof(YokkoReplayIO).Assembly.GetName().Version?.ToString(),
            ReplayChecksum: computeReplayChecksum(
                beatmapFingerprint,
                normalizedSourceHash,
                keyCount,
                timestamp,
                activeJudgement.Mode.ToString(),
                activeJudgement.EtternaJustice,
                modConfiguration,
                frames));

        JsonSerializer.Serialize(stream, document, json_options);
    }

    public static void WriteToFile(
        string path,
        YokkoBeatmap originalBeatmap,
        YokkoBeatmap appliedBeatmap,
        GameplayReplay replay,
        string sourceHash = null,
        DateTimeOffset? recordedAt = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string fullPath = Path.GetFullPath(path);
        string directory = Path.GetDirectoryName(fullPath)
                           ?? throw new InvalidOperationException(
                               "The replay path has no directory.");
        Directory.CreateDirectory(directory);

        string temporaryPath = fullPath + ".tmp";
        try
        {
            using (FileStream stream = File.Create(temporaryPath))
            {
                Write(
                    stream,
                    originalBeatmap,
                    appliedBeatmap,
                    replay,
                    sourceHash,
                    recordedAt);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    public static YokkoReplayLoadResult Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead)
            throw new ArgumentException("The replay stream is not readable.", nameof(stream));
        if (stream.CanSeek && stream.Length > maximum_file_bytes)
            throw new InvalidDataException("The Yokko replay is too large.");

        YokkoReplayDocument document;
        try
        {
            document =
                JsonSerializer.Deserialize<YokkoReplayDocument>(
                    stream,
                    json_options)
                ?? throw new InvalidDataException(
                    "The Yokko replay is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                "The Yokko replay JSON is invalid.",
                exception);
        }

        if (document.SchemaVersion is not 1 and not 2 and not schema_version)
        {
            throw new NotSupportedException(
                $"Unsupported Yokko replay schema "
                + $"{document.SchemaVersion}.");
        }

        if (!string.Equals(
                document.Ruleset,
                "mania",
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Unsupported replay ruleset '{document.Ruleset}'.");
        }

        if (!isSha256(document.BeatmapFingerprint))
        {
            throw new InvalidDataException(
                "The replay beatmap fingerprint is invalid.");
        }

        if (document.KeyCount is < 1 or > 20)
            throw new InvalidDataException("The replay key count is invalid.");
        if (document.ModConfiguration is null)
            throw new InvalidDataException("The replay Mod configuration is missing.");

        GameplayReplayInput[] inputs;
        if (document.SchemaVersion >= 3)
        {
            if (document.Frames is null)
                throw new InvalidDataException("The replay frame list is missing.");
            validateFrameDocuments(document.Frames, document.KeyCount);
            if (!isSha256(document.ReplayChecksum))
                throw new InvalidDataException("The replay checksum is invalid.");
            string expectedChecksum = computeReplayChecksum(
                document.BeatmapFingerprint,
                document.SourceHash,
                document.KeyCount,
                document.RecordedAt,
                document.JudgementMode,
                document.EtternaJustice,
                document.ModConfiguration,
                document.Frames);
            if (!CryptographicOperations.FixedTimeEquals(
                    Convert.FromHexString(expectedChecksum),
                    Convert.FromHexString(document.ReplayChecksum)))
            {
                throw new InvalidDataException(
                    "The replay checksum does not match its contents.");
            }

            inputs = inputsFromFrames(
                document.Frames,
                document.KeyCount);
        }
        else
        {
            if (document.Inputs is null)
                throw new InvalidDataException("The replay input list is missing.");
            inputs = document.Inputs
                .Select(static input => new GameplayReplayInput(
                    input.Lane,
                    input.IsPressed,
                    input.TimeMilliseconds))
                .ToArray();
        }
        validateInputs(inputs, document.KeyCount);

        ManiaModSet mods;
        try
        {
            mods = ManiaModConfigurationCodec.Restore(
                document.ModConfiguration);
        }
        catch (Exception exception)
            when (exception is ArgumentException
                or InvalidDataException
                or NotSupportedException)
        {
            throw new InvalidDataException(
                "The replay Mod configuration cannot be restored.",
                exception);
        }

        JudgementConfiguration judgementConfiguration;
        if (document.SchemaVersion == 1)
        {
            judgementConfiguration =
                JudgementConfiguration.YokkoDefault;
        }
        else
        {
            if (!Enum.TryParse(
                    document.JudgementMode,
                    ignoreCase: true,
                    out JudgementMode mode)
                || !Enum.IsDefined(mode)
                || document.EtternaJustice is not int justice)
            {
                throw new InvalidDataException(
                    "The Yokko replay judgement configuration is invalid.");
            }

            try
            {
                judgementConfiguration =
                    new JudgementConfiguration(mode, justice);
            }
            catch (ArgumentOutOfRangeException exception)
            {
                throw new InvalidDataException(
                    "The Yokko replay judgement configuration is invalid.",
                    exception);
            }
        }

        return new YokkoReplayLoadResult(
            document.BeatmapFingerprint,
            document.SourceHash,
            document.KeyCount,
            document.RecordedAt,
            new GameplayReplay(
                inputs,
                mods,
                judgementConfiguration));
    }

    public static YokkoReplayLoadResult ReadFromFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var info = new FileInfo(Path.GetFullPath(path));
        if (!info.Exists)
            throw new FileNotFoundException("The Yokko replay was not found.", info.FullName);
        if (info.Length > maximum_file_bytes)
            throw new InvalidDataException("The Yokko replay is too large.");

        using FileStream stream = File.OpenRead(info.FullName);
        return Read(stream);
    }

    private static void validateInputs(
        IReadOnlyList<GameplayReplayInput> inputs,
        int keyCount)
    {
        for (int index = 0; index < inputs.Count; index++)
        {
            GameplayReplayInput input = inputs[index];
            if ((uint)input.Lane >= keyCount)
            {
                throw new InvalidDataException(
                    $"Replay input {index} uses lane {input.Lane} "
                    + $"outside the {keyCount}K session.");
            }

            if (!double.IsFinite(input.TimeMilliseconds))
                throw new InvalidDataException(
                    $"Replay input {index} has an invalid timestamp.");
            if (index > 0
                && input.TimeMilliseconds
                < inputs[index - 1].TimeMilliseconds)
            {
                throw new InvalidDataException(
                    "Replay inputs are not ordered by gameplay time.");
            }
        }
    }

    private static void validateFrames(
        IReadOnlyList<GameplayReplayFrame> frames,
        int keyCount) => validateFrameDocuments(
        frames.Select(static frame => new YokkoReplayFrameDocument(
            frame.TimeMilliseconds,
            frame.PressedLanes)).ToArray(),
        keyCount);

    private static void validateFrameDocuments(
        IReadOnlyList<YokkoReplayFrameDocument> frames,
        int keyCount)
    {
        ulong supportedLanes = (1UL << keyCount) - 1;
        for (int index = 0; index < frames.Count; index++)
        {
            YokkoReplayFrameDocument frame = frames[index];
            if (!double.IsFinite(frame.TimeMilliseconds))
            {
                throw new InvalidDataException(
                    $"Replay frame {index} has an invalid timestamp.");
            }
            if ((frame.PressedLanes & ~supportedLanes) != 0)
            {
                throw new InvalidDataException(
                    $"Replay frame {index} uses keys outside the "
                    + $"{keyCount}K session.");
            }
            if (index > 0
                && frame.TimeMilliseconds
                < frames[index - 1].TimeMilliseconds)
            {
                throw new InvalidDataException(
                    "Replay frames are not ordered by gameplay time.");
            }
        }
    }

    private static GameplayReplayInput[] inputsFromFrames(
        IReadOnlyList<YokkoReplayFrameDocument> frames,
        int keyCount)
    {
        var inputs = new List<GameplayReplayInput>();
        ulong previousLanes = 0;
        foreach (YokkoReplayFrameDocument frame in frames)
        {
            ulong changedLanes = previousLanes ^ frame.PressedLanes;
            for (int lane = 0; lane < keyCount; lane++)
            {
                ulong laneMask = 1UL << lane;
                if ((changedLanes & laneMask) == 0)
                    continue;

                inputs.Add(new GameplayReplayInput(
                    lane,
                    (frame.PressedLanes & laneMask) != 0,
                    frame.TimeMilliseconds));
            }

            previousLanes = frame.PressedLanes;
        }

        return inputs.ToArray();
    }

    private static string computeReplayChecksum(
        string beatmapFingerprint,
        string sourceHash,
        int keyCount,
        DateTimeOffset recordedAt,
        string judgementMode,
        int? etternaJustice,
        ManiaModConfigurationEnvelope modConfiguration,
        IReadOnlyList<YokkoReplayFrameDocument> frames)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(
                   stream,
                   Encoding.UTF8,
                   leaveOpen: true))
        {
            writer.Write("yokko-replay-v3");
            writer.Write(beatmapFingerprint ?? string.Empty);
            writer.Write(sourceHash ?? string.Empty);
            writer.Write(keyCount);
            writer.Write(recordedAt.UtcTicks);
            writer.Write(judgementMode ?? string.Empty);
            writer.Write(etternaJustice ?? int.MinValue);
            writer.Write(JsonSerializer.Serialize(
                modConfiguration,
                json_options));
            writer.Write(frames.Count);
            foreach (YokkoReplayFrameDocument frame in frames)
            {
                writer.Write(BitConverter.DoubleToInt64Bits(
                    frame.TimeMilliseconds));
                writer.Write(frame.PressedLanes);
            }
        }

        return Convert.ToHexString(SHA256.HashData(stream.ToArray()))
                      .ToLowerInvariant();
    }

    private static bool isSha256(string value) =>
        value is { Length: 64 }
        && value.All(static character =>
            character is >= '0' and <= '9'
                or >= 'A' and <= 'F'
                or >= 'a' and <= 'f');

    private sealed record YokkoReplayDocument(
        int SchemaVersion,
        string Ruleset,
        string BeatmapFingerprint,
        string SourceHash,
        int KeyCount,
        DateTimeOffset RecordedAt,
        string JudgementMode,
        int? EtternaJustice,
        ManiaModConfigurationEnvelope ModConfiguration,
        IReadOnlyList<YokkoReplayInputDocument> Inputs,
        IReadOnlyList<YokkoReplayFrameDocument> Frames,
        string ClientVersion,
        string ReplayChecksum);

    private sealed record YokkoReplayInputDocument(
        int Lane,
        bool IsPressed,
        double TimeMilliseconds);

    private sealed record YokkoReplayFrameDocument(
        double TimeMilliseconds,
        ulong PressedLanes);
}
