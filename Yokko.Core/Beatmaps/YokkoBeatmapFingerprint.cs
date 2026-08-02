using System.Security.Cryptography;
using System.Text;

namespace Yokko.Core.Beatmaps;

/// <summary>
/// Stable identity for the complete imported gameplay model. Local resource
/// paths are intentionally excluded so moving the library does not orphan
/// replays.
/// </summary>
public static class YokkoBeatmapFingerprint
{
    private const int schema_version = 1;

    public static string Compute(YokkoBeatmap beatmap)
    {
        ArgumentNullException.ThrowIfNull(beatmap);

        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(
                   stream,
                   Encoding.UTF8,
                   leaveOpen: true))
        {
            writer.Write(schema_version);
            writer.Write(beatmap.Title ?? string.Empty);
            writer.Write(beatmap.Artist ?? string.Empty);
            writer.Write(beatmap.Creator ?? string.Empty);
            writer.Write(beatmap.DifficultyName ?? string.Empty);
            writer.Write((int)beatmap.KeyMode);
            writer.Write((int)beatmap.SourceFormat);
            writer.Write(beatmap.OverallDifficulty);
            writer.Write(beatmap.DrainRate);
            writer.Write(beatmap.StageCount);
            writer.Write(beatmap.InitialScrollVelocity);
            writer.Write(beatmap.LegacyLongNoteRendering);

            writer.Write(beatmap.TimingPoints.Count);
            foreach (var point in beatmap.TimingPoints)
            {
                writer.Write(point.TimeMilliseconds);
                writer.Write(point.BeatLengthMilliseconds);
                writer.Write(point.Meter);
                writer.Write(point.SampleSet);
                writer.Write(point.SampleIndex);
                writer.Write(point.Volume);
                writer.Write(point.Uninherited);
                writer.Write(point.Effects);
            }

            writer.Write(beatmap.BreakPeriods.Count);
            foreach (YokkoBreakPeriod period in beatmap.BreakPeriods)
            {
                writer.Write(period.StartTimeMilliseconds);
                writer.Write(period.EndTimeMilliseconds);
            }

            writer.Write(beatmap.ScrollVelocities.Count);
            foreach (var velocity in beatmap.ScrollVelocities)
            {
                writer.Write(velocity.TimeMilliseconds);
                writer.Write(velocity.Multiplier);
            }

            writer.Write(beatmap.ScrollSpeedFactors.Count);
            foreach (var factor in beatmap.ScrollSpeedFactors)
            {
                writer.Write(factor.TimeMilliseconds);
                writer.Write(factor.Multiplier);
            }

            writer.Write(beatmap.ScrollProfiles.Count);
            foreach ((string key, var profile) in
                     beatmap.ScrollProfiles.OrderBy(
                         static item => item.Key,
                         StringComparer.Ordinal))
            {
                writer.Write(key);
                writer.Write(profile.InitialScrollVelocity);
                writer.Write(profile.ScrollVelocities.Count);
                foreach (var velocity in profile.ScrollVelocities)
                {
                    writer.Write(velocity.TimeMilliseconds);
                    writer.Write(velocity.Multiplier);
                }

                writer.Write(profile.ScrollSpeedFactors.Count);
                foreach (var factor in profile.ScrollSpeedFactors)
                {
                    writer.Write(factor.TimeMilliseconds);
                    writer.Write(factor.Multiplier);
                }
            }

            writer.Write(beatmap.HitObjects.Count);
            foreach (YokkoHitObject hitObject in beatmap.HitObjects)
            {
                writer.Write(hitObject.Lane);
                writer.Write(hitObject.StartTimeMilliseconds);
                writeOptional(writer, hitObject.EndTimeMilliseconds);
                writer.Write((int)hitObject.Kind);
                writeOptional(writer, hitObject.SampleKey);
                writeOptional(writer, hitObject.ScrollProfileId);
                writeSamplePayload(writer, hitObject.SamplePayload);
            }

            writeConversionSource(writer, beatmap.ConversionSource);

            // Ordinary chart fingerprints remain byte-for-byte compatible.
            if (beatmap.ScratchLane is int scratchLane)
            {
                writer.Write("bms-scratch-v1");
                writer.Write(scratchLane);
            }
            else if (beatmap.ScratchLanes.Count > 0)
            {
                writer.Write("bms-scratch-v2");
                writer.Write(beatmap.ScratchLanes.Count);
                foreach (int lane in beatmap.ScratchLanes)
                    writer.Write(lane);
            }

            if (beatmap.BmsJudgement is { } bmsJudgement)
            {
                writer.Write("bms-judgement-v1");
                writer.Write(bmsJudgement.WindowMultiplier);
                writer.Write(bmsJudgement.RegularKeysPerStage ?? 0);
            }

            // Keep fingerprints for charts without timeline samples stable.
            // The marker makes the appended optional block unambiguous.
            if (beatmap.ScheduledSamples.Count > 0)
            {
                writer.Write("scheduled-samples-v1");
                writer.Write(beatmap.ScheduledSamples.Count);
                foreach (YokkoScheduledSample sample in
                         beatmap.ScheduledSamples)
                {
                    writer.Write(sample.TimeMilliseconds);
                    writer.Write(Path.GetFileName(sample.Path));
                    writer.Write(sample.Volume);
                    writer.Write(sample.UnaffectedByRate);
                }
            }

            if (beatmap.ScheduledSamples.Any(static sample => sample.UseMusicBus))
            {
                writer.Write("scheduled-sample-buses-v1");
                writer.Write(beatmap.ScheduledSamples.Count);
                foreach (YokkoScheduledSample sample in beatmap.ScheduledSamples)
                    writer.Write(sample.UseMusicBus);
            }
        }

        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static void writeConversionSource(
        BinaryWriter writer,
        ManiaConversionSource? source)
    {
        writer.Write(source is not null);
        if (source is null)
            return;

        writer.Write(source.CircleSize);
        writer.Write(source.OverallDifficulty);
        writer.Write(source.ApproachRate);
        writer.Write(source.DrainRate);
        writer.Write(source.TotalBreakTimeMilliseconds);
        writer.Write(source.HitObjects.Count);
        foreach (ManiaConversionHitObject hitObject in source.HitObjects)
        {
            writer.Write(hitObject.X);
            writer.Write(hitObject.Y);
            writer.Write(hitObject.StartTimeMilliseconds);
            writer.Write(hitObject.EndTimeMilliseconds);
            writer.Write((int)hitObject.Kind);
            writer.Write(hitObject.HitSound);
            writer.Write(hitObject.SpanCount);
            writeIntList(writer, hitObject.NodeHitSounds);
            writeSamples(writer, hitObject.Samples);
            writeNodeSamples(writer, hitObject.NodeSamples);
        }
    }

    private static void writeSamplePayload(
        BinaryWriter writer,
        YokkoHitSamplePayload? payload)
    {
        writer.Write(payload is not null);
        if (payload is null)
            return;

        writer.Write(payload.PlaySlidingSamples);
        writeSamples(writer, payload.Samples);
        writeNodeSamples(writer, payload.NodeSamples);
    }

    private static void writeNodeSamples(
        BinaryWriter writer,
        IReadOnlyList<IReadOnlyList<YokkoHitSample>>? nodes)
    {
        writer.Write(nodes?.Count ?? -1);
        if (nodes is null)
            return;

        foreach (IReadOnlyList<YokkoHitSample> node in nodes)
            writeSamples(writer, node);
    }

    private static void writeSamples(
        BinaryWriter writer,
        IReadOnlyList<YokkoHitSample>? samples)
    {
        writer.Write(samples?.Count ?? -1);
        if (samples is null)
            return;

        foreach (YokkoHitSample sample in samples)
        {
            writer.Write(sample.Name);
            writer.Write(sample.Bank);
            writer.Write(sample.Volume);
            writer.Write(sample.CustomSampleBank);
            writeOptional(writer, sample.Filename);
            writer.Write(sample.IsLayered);
        }
    }

    private static void writeIntList(
        BinaryWriter writer,
        IReadOnlyList<int>? values)
    {
        writer.Write(values?.Count ?? -1);
        if (values is null)
            return;

        foreach (int value in values)
            writer.Write(value);
    }

    private static void writeOptional(
        BinaryWriter writer,
        string? value)
    {
        writer.Write(value is not null);
        if (value is not null)
            writer.Write(value);
    }

    private static void writeOptional(
        BinaryWriter writer,
        double? value)
    {
        writer.Write(value.HasValue);
        if (value.HasValue)
            writer.Write(value.Value);
    }
}
