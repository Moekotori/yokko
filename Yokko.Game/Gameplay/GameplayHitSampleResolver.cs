using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Yokko.Core.Beatmaps;

namespace Yokko.Game.Gameplay;

internal readonly record struct ResolvedGameplayHitSample(
    string Path,
    double Gain);

/// <summary>
/// Resolves lazer-style hit sample lookup identities to beatmap assets, then
/// falls back to Yokko's default skin samples. Resource choice is a skin concern;
/// bank, suffix, layering and volume remain beatmap-owned truth.
/// </summary>
internal sealed class GameplayHitSampleResolver
{
    private static readonly string[] extensions =
    [
        ".wav",
        ".ogg",
        ".mp3",
    ];

    private readonly YokkoBeatmap beatmap;
    private readonly string beatmapDirectory;

    internal GameplayHitSampleResolver(YokkoBeatmap beatmap)
    {
        this.beatmap = beatmap;
        beatmapDirectory = resolveBeatmapDirectory(beatmap.AudioPath);
    }

    internal IReadOnlyList<ResolvedGameplayHitSample> ResolveHead(
        YokkoHitObject hitObject)
    {
        IReadOnlyList<YokkoHitSample> samples =
            hitObject.NodeSamples.Count > 0
                ? hitObject.NodeSamples[0]
                : hitObject.Samples;
        return resolve(samples, hitObject.SampleKey);
    }

    internal IReadOnlyList<ResolvedGameplayHitSample> ResolveTail(
        YokkoHitObject hitObject)
    {
        IReadOnlyList<YokkoHitSample> samples =
            hitObject.NodeSamples.Count > 0
                ? hitObject.NodeSamples[^1]
                : [];
        return resolve(samples, null);
    }

    internal IReadOnlyList<ResolvedGameplayHitSample> ResolveSliding(
        YokkoHitObject hitObject)
    {
        if (!hitObject.PlaySlidingSamples)
            return [];

        YokkoHitSample[] samples = hitObject.Samples
            .Select(static sample => sample.Name switch
            {
                YokkoHitSample.HitNormal => slidingSample(
                    sample,
                    YokkoHitSample.SliderSlide),
                YokkoHitSample.HitWhistle => slidingSample(
                    sample,
                    YokkoHitSample.SliderWhistle),
                _ => null,
            })
            .Where(static sample => sample is not null)
            .ToArray();
        return resolve(samples, null);
    }

    internal IReadOnlyCollection<string> AllPaths()
    {
        return beatmap.HitObjects
                      .SelectMany(hitObject =>
                          ResolveHead(hitObject)
                              .Concat(ResolveTail(hitObject))
                              .Concat(ResolveSliding(hitObject)))
                      .Select(static sample => sample.Path)
                      .Distinct(StringComparer.OrdinalIgnoreCase)
                      .ToArray();
    }

    private static YokkoHitSample slidingSample(
        YokkoHitSample sample,
        string name) =>
        new(
            name,
            sample.Bank,
            sample.Volume,
            sample.CustomSampleBank,
            sample.Filename,
            sample.IsLayered);

    private IReadOnlyList<ResolvedGameplayHitSample> resolve(
        IReadOnlyList<YokkoHitSample> samples,
        string legacySampleKey)
    {
        var resolved = new List<ResolvedGameplayHitSample>();
        foreach (YokkoHitSample sample in samples)
        {
            // lazer's ManiaLegacySkinTransformer suppresses layered normal
            // samples on native Mania maps while retaining them on converts.
            if (sample.IsLayered
                && beatmap.SourceFormat == ChartSourceFormat.OsuMania)
            {
                continue;
            }

            string path = resolve(sample);
            if (path is not null)
            {
                resolved.Add(
                    new ResolvedGameplayHitSample(
                        path,
                        Math.Clamp(sample.Volume / 100d, 0, 1)));
            }
        }

        if (resolved.Count == 0
            && !string.IsNullOrWhiteSpace(legacySampleKey))
        {
            string path = resolvePath(legacySampleKey);
            if (path is not null)
                resolved.Add(new ResolvedGameplayHitSample(path, 1));
        }

        return resolved;
    }

    private string resolve(YokkoHitSample sample)
    {
        foreach (string lookup in sample.LookupNames())
        {
            string path = resolvePath(lookup);
            if (path is not null)
                return path;
        }

        return GameplayDefaultHitSampleStore.Resolve(sample);
    }

    private string resolvePath(string lookup)
    {
        if (string.IsNullOrWhiteSpace(lookup))
            return null;

        try
        {
            string candidate = Path.IsPathRooted(lookup)
                ? Path.GetFullPath(lookup)
                : beatmapDirectory is null
                    ? Path.GetFullPath(lookup)
                    : Path.GetFullPath(
                        Path.Combine(beatmapDirectory, lookup));
            if (File.Exists(candidate))
                return candidate;

            if (!string.IsNullOrEmpty(Path.GetExtension(candidate)))
                return null;

            foreach (string extension in extensions)
            {
                string withExtension = candidate + extension;
                if (File.Exists(withExtension))
                    return withExtension;
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    private static string resolveBeatmapDirectory(string audioPath)
    {
        if (string.IsNullOrWhiteSpace(audioPath))
            return null;

        try
        {
            return Path.GetDirectoryName(Path.GetFullPath(audioPath));
        }
        catch
        {
            return null;
        }
    }
}
