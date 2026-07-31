using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using osu.Framework.Logging;
using Yokko.Core.Beatmaps;
using Yokko.Core.Difficulty;

namespace Yokko.Game.Importing;

internal sealed record StoredMsdRating(
    EtternaMsdValues Skillsets,
    double PlaybackRate,
    string AlgorithmIdentifier);

internal sealed class MsdRatingCache
{
    private readonly Dictionary<string, StoredMsdRating> entries =
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

    public ManiaMsdResult GetOrCalculate(
        YokkoBeatmap beatmap,
        double playbackRate = 1)
    {
        string key;

        try
        {
            key = ManiaMsdCalculator.CreateCacheKey(
                beatmap,
                playbackRate);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return ManiaMsdCalculator.CalculateResult(
                beatmap,
                playbackRate);
        }

        lock (syncRoot)
        {
            if (entries.TryGetValue(key, out StoredMsdRating cached)
                && cached.AlgorithmIdentifier
                    == ManiaMsdCalculator.AlgorithmIdentifier
                && cached.PlaybackRate.Equals(playbackRate)
                && valuesAreValid(cached.Skillsets))
            {
                HitCount++;
                return new ManiaMsdResult(
                    ManiaMsdStatus.Success,
                    cached.Skillsets,
                    playbackRate,
                    cached.AlgorithmIdentifier);
            }
        }

        ManiaMsdResult calculated =
            ManiaMsdCalculator.CalculateResult(
                beatmap,
                playbackRate);

        if (!calculated.IsSuccess)
            return calculated;

        lock (syncRoot)
        {
            entries[key] = new StoredMsdRating(
                calculated.Skillsets!,
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
        Dictionary<string, StoredMsdRating> snapshot;
        long snapshotVersion;

        lock (syncRoot)
        {
            if (!changed || cachePath == null)
                return;

            path = cachePath;
            snapshot = new Dictionary<string, StoredMsdRating>(
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
                "Could not save the Etterna MSD cache.");
        }
    }

    private void load()
    {
        if (!File.Exists(cachePath))
            return;

        try
        {
            Dictionary<string, StoredMsdRating> loaded =
                JsonSerializer.Deserialize<
                    Dictionary<string, StoredMsdRating>>(
                    File.ReadAllText(cachePath));

            if (loaded == null)
                return;

            foreach ((string key, StoredMsdRating rating) in loaded)
            {
                if (rating.AlgorithmIdentifier
                        != ManiaMsdCalculator.AlgorithmIdentifier
                    || !double.IsFinite(rating.PlaybackRate)
                    || rating.PlaybackRate <= 0
                    || !valuesAreValid(rating.Skillsets))
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
                "Could not load the Etterna MSD cache; ratings will be recalculated.");
        }
    }

    private static bool valuesAreValid(EtternaMsdValues values) =>
        values != null
        && Enum.GetValues<EtternaMsdSkillset>()
               .All(skillset =>
                   double.IsFinite(values[skillset])
                   && values[skillset] >= 0);
}

