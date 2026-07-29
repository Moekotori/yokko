using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    private const int schema_version = 2;
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
        var document = new YokkoReplayDocument(
            schema_version,
            "mania",
            YokkoBeatmapFingerprint.Compute(originalBeatmap),
            string.IsNullOrWhiteSpace(sourceHash)
                ? null
                : sourceHash.Trim(),
            keyCount,
            recordedAt ?? DateTimeOffset.UtcNow,
            (replay.JudgementConfiguration
             ?? JudgementConfiguration.YokkoDefault).Mode
                .ToString(),
            (replay.JudgementConfiguration
             ?? JudgementConfiguration.YokkoDefault)
                .EtternaJustice,
            ManiaModConfigurationCodec.Capture(replay.Mods),
            replay.Inputs
                  .Select(static input => new YokkoReplayInputDocument(
                      input.Lane,
                      input.IsPressed,
                      input.TimeMilliseconds))
                  .ToArray());

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

        if (document.SchemaVersion is not 1 and not schema_version)
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
        if (document.Inputs is null)
            throw new InvalidDataException("The replay input list is missing.");
        if (document.ModConfiguration is null)
            throw new InvalidDataException("The replay Mod configuration is missing.");

        GameplayReplayInput[] inputs = document.Inputs
            .Select(static input => new GameplayReplayInput(
                input.Lane,
                input.IsPressed,
                input.TimeMilliseconds))
            .ToArray();
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
        IReadOnlyList<YokkoReplayInputDocument> Inputs);

    private sealed record YokkoReplayInputDocument(
        int Lane,
        bool IsPressed,
        double TimeMilliseconds);
}
