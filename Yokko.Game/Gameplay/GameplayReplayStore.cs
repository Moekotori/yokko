using System;
using System.IO;
using osu.Framework.Platform;
using Yokko.Core.Beatmaps;

namespace Yokko.Game.Gameplay;

/// <summary>
/// Owns Yokko's native replay directory and atomically persists completed live
/// sessions so their full Mod configuration survives process restart.
/// </summary>
internal sealed class GameplayReplayStore
{
    private readonly object syncRoot = new();
    private string replayDirectory;

    public string ReplayDirectory => replayDirectory;

    public void Initialise(Storage storage)
    {
        ArgumentNullException.ThrowIfNull(storage);
        Initialise(storage.GetFullPath("Replays", true));
    }

    internal void Initialise(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        replayDirectory = Path.GetFullPath(path);
        Directory.CreateDirectory(replayDirectory);
    }

    public string Save(
        YokkoBeatmap originalBeatmap,
        YokkoBeatmap appliedBeatmap,
        GameplayReplay replay,
        string sourceHash = null,
        DateTimeOffset? recordedAt = null)
    {
        ensureInitialised();
        ArgumentNullException.ThrowIfNull(originalBeatmap);
        ArgumentNullException.ThrowIfNull(appliedBeatmap);
        ArgumentNullException.ThrowIfNull(replay);

        DateTimeOffset timestamp = recordedAt ?? DateTimeOffset.UtcNow;
        string fingerprint =
            YokkoBeatmapFingerprint.Compute(originalBeatmap);
        string stem =
            $"{timestamp.UtcDateTime:yyyyMMdd-HHmmssfff}-"
            + fingerprint[..12];

        lock (syncRoot)
        {
            string path = Path.Combine(
                replayDirectory,
                stem + YokkoReplayIO.FileExtension);
            int suffix = 2;
            while (File.Exists(path))
            {
                path = Path.Combine(
                    replayDirectory,
                    $"{stem}-{suffix++}{YokkoReplayIO.FileExtension}");
            }

            YokkoReplayIO.WriteToFile(
                path,
                originalBeatmap,
                appliedBeatmap,
                replay,
                sourceHash,
                timestamp);
            return path;
        }
    }

    private void ensureInitialised()
    {
        if (replayDirectory is null)
        {
            throw new InvalidOperationException(
                "The replay store is not initialised.");
        }
    }
}
