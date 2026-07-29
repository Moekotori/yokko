using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using osu.Framework.Logging;
using Yokko.Core.Beatmaps;
using Yokko.Core.Difficulty;

namespace Yokko.Game.Importing;

internal sealed record StoredStarRating(
    double Value,
    double PlaybackRate,
    string AlgorithmIdentifier);

internal sealed class StarRatingCache
{
    private readonly Dictionary<string, StoredStarRating> entries =
        new(StringComparer.Ordinal);
    private readonly object syncRoot = new();
    private string cachePath;
    private bool changed;
    private long changeVersion;

    internal int HitCount { get; private set; }

    internal int Count
    {
        get
        {
            lock (syncRoot)
                return entries.Count;
        }
    }

    public void Initialise(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        lock (syncRoot)
        {
            cachePath = Path.GetFullPath(path);
            entries.Clear();
            changed = false;
            changeVersion = 0;
            HitCount = 0;
            load();
        }
    }

    public ManiaStarRatingResult GetOrCalculate(
        YokkoBeatmap beatmap,
        double playbackRate = 1)
    {
        string key;

        try
        {
            key = ManiaStarRatingCalculator.CreateCacheKey(
                beatmap,
                playbackRate);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return ManiaStarRatingCalculator.CalculateResult(
                beatmap,
                playbackRate);
        }

        lock (syncRoot)
        {
            if (entries.TryGetValue(key, out StoredStarRating cached)
                && cached.AlgorithmIdentifier
                    == ManiaStarRatingCalculator.AlgorithmIdentifier
                && cached.PlaybackRate.Equals(playbackRate)
                && double.IsFinite(cached.Value)
                && cached.Value >= 0)
            {
                HitCount++;
                return new ManiaStarRatingResult(
                    ManiaStarRatingStatus.Success,
                    cached.Value,
                    playbackRate,
                    cached.AlgorithmIdentifier);
            }
        }

        ManiaStarRatingResult calculated =
            ManiaStarRatingCalculator.CalculateResult(
                beatmap,
                playbackRate);

        if (!calculated.IsSuccess)
            return calculated;

        lock (syncRoot)
        {
            entries[key] = new StoredStarRating(
                calculated.Value!.Value,
                playbackRate,
                calculated.AlgorithmIdentifier);
            changed = true;
            changeVersion++;
        }

        return calculated;
    }

    public void SaveIfChanged()
    {
        string path;
        Dictionary<string, StoredStarRating> snapshot;
        long snapshotVersion;

        lock (syncRoot)
        {
            if (!changed || cachePath == null)
                return;

            path = cachePath;
            snapshot = new Dictionary<string, StoredStarRating>(
                entries,
                StringComparer.Ordinal);
            snapshotVersion = changeVersion;
        }

        try
        {
            string directory = Path.GetDirectoryName(path)!;
            Directory.CreateDirectory(directory);
            string temporaryPath = path + ".tmp";
            File.WriteAllText(
                temporaryPath,
                JsonSerializer.Serialize(
                    snapshot,
                    new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporaryPath, path, true);

            lock (syncRoot)
            {
                if (changeVersion == snapshotVersion)
                    changed = false;
            }
        }
        catch (Exception exception)
        {
            Logger.Error(
                exception,
                "Could not save the star rating cache.");
        }
    }

    private void load()
    {
        if (!File.Exists(cachePath))
            return;

        try
        {
            Dictionary<string, StoredStarRating> loaded =
                JsonSerializer.Deserialize<
                    Dictionary<string, StoredStarRating>>(
                    File.ReadAllText(cachePath));

            if (loaded == null)
                return;

            foreach ((string key, StoredStarRating rating) in loaded)
            {
                if (rating.AlgorithmIdentifier
                        != ManiaStarRatingCalculator.AlgorithmIdentifier
                    || !double.IsFinite(rating.Value)
                    || rating.Value < 0
                    || !double.IsFinite(rating.PlaybackRate)
                    || rating.PlaybackRate <= 0)
                {
                    continue;
                }

                entries[key] = rating;
            }
        }
        catch (Exception exception)
        {
            Logger.Error(
                exception,
                "Could not load the star rating cache; ratings will be recalculated.");
        }
    }
}
