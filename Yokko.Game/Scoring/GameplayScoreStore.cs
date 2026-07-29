using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using osu.Framework.Logging;
using osu.Framework.Platform;
using Yokko.Core.Beatmaps;
using Yokko.Core.Mods;
using Yokko.Core.Scoring;

namespace Yokko.Game.Scoring;

internal sealed record StoredGameplayScore(
    long Score,
    double Accuracy,
    int MaxCombo,
    ScoreRank Rank,
    int Perfect,
    int Great,
    int Good,
    int Ok,
    int Meh,
    int Miss,
    DateTimeOffset PlayedAt,
    string[] Mods = null);

internal sealed class GameplayScoreStore
{
    private const string scores_filename = "Scores/scores.json";

    private readonly Dictionary<string, StoredGameplayScore> scores =
        new(StringComparer.Ordinal);
    private string scoresPath;

    public void Initialise(Storage storage)
    {
        ArgumentNullException.ThrowIfNull(storage);
        scoresPath = storage.GetFullPath(scores_filename, true);
        load();
    }

    public StoredGameplayScore GetBest(YokkoBeatmap beatmap)
        => GetBest(beatmap, ManiaModSet.Empty);

    public StoredGameplayScore GetBest(
        YokkoBeatmap beatmap,
        ManiaModSet mods)
        => GetBest(
            beatmap,
            mods,
            JudgementConfiguration.YokkoDefault);

    public StoredGameplayScore GetBest(
        YokkoBeatmap beatmap,
        ManiaModSet mods,
        JudgementConfiguration judgementConfiguration)
    {
        ensureInitialised();
        return scores.GetValueOrDefault(
            keyFor(beatmap, mods, judgementConfiguration));
    }

    public bool SaveBest(YokkoBeatmap beatmap, ManiaScoreResult result)
        => SaveBest(beatmap, ManiaModSet.Empty, result);

    public bool SaveBest(
        YokkoBeatmap beatmap,
        ManiaModSet mods,
        ManiaScoreResult result)
        => SaveBest(
            beatmap,
            mods,
            JudgementConfiguration.YokkoDefault,
            result);

    public bool SaveBest(
        YokkoBeatmap beatmap,
        ManiaModSet mods,
        JudgementConfiguration judgementConfiguration,
        ManiaScoreResult result)
    {
        ensureInitialised();
        mods ??= ManiaModSet.Empty;
        string key = keyFor(
            beatmap,
            mods,
            judgementConfiguration);

        if (scores.TryGetValue(key, out StoredGameplayScore existing)
            && existing.Score >= result.Score)
        {
            return false;
        }

        var replacement = new StoredGameplayScore(
            result.Score,
            result.Accuracy,
            result.MaxCombo,
            result.Rank,
            result.Perfect,
            result.Great,
            result.Good,
            result.Ok,
            result.Meh,
            result.Miss,
            DateTimeOffset.UtcNow,
            mods.Acronyms.ToArray());
        scores[key] = replacement;

        if (save())
            return true;

        if (existing != null)
            scores[key] = existing;
        else
            scores.Remove(key);

        return false;
    }

    private void load()
    {
        scores.Clear();

        if (!File.Exists(scoresPath))
            return;

        try
        {
            Dictionary<string, StoredGameplayScore> loaded =
                JsonSerializer.Deserialize<Dictionary<string, StoredGameplayScore>>(
                    File.ReadAllText(scoresPath));

            if (loaded == null)
                return;

            foreach ((string key, StoredGameplayScore score) in loaded)
                scores[key] = score;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Could not load saved gameplay scores.");
        }
    }

    private bool save()
    {
        try
        {
            string directory = Path.GetDirectoryName(scoresPath);
            if (directory != null)
                Directory.CreateDirectory(directory);

            string temporaryPath = scoresPath + ".tmp";
            File.WriteAllText(
                temporaryPath,
                JsonSerializer.Serialize(
                    scores,
                    new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporaryPath, scoresPath, true);
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Could not save gameplay score.");
            return false;
        }
    }

    private void ensureInitialised()
    {
        if (scoresPath == null)
            throw new InvalidOperationException("The score store is not initialised.");
    }

    private static string keyFor(
        YokkoBeatmap beatmap,
        ManiaModSet mods,
        JudgementConfiguration judgementConfiguration)
    {
        mods ??= ManiaModSet.Empty;
        var source = new StringBuilder()
                     .Append(beatmap.Title).Append('\u001f')
                     .Append(beatmap.Artist).Append('\u001f')
                     .Append(beatmap.Creator).Append('\u001f')
                     .Append(beatmap.DifficultyName).Append('\u001f')
                     .Append((int)beatmap.KeyMode).Append('\u001f')
                     .Append(beatmap.OverallDifficulty.ToString(
                         "R",
                         CultureInfo.InvariantCulture));

        // Keep the pre-Mod NM key byte-for-byte compatible so existing local
        // scores remain visible after the Mod system is introduced.
        if (!mods.IsEmpty)
        {
            source.Append('\u001f')
                  .Append(mods.Fingerprint);
        }

        // Preserve the existing Yokko key so local bests remain visible.
        // Etterna modes are isolated because different Judge values are not
        // comparable timing conditions.
        if (judgementConfiguration.Mode != JudgementMode.Yokko)
        {
            source.Append('\u001f')
                  .Append("judge:")
                  .Append(judgementConfiguration.Mode)
                  .Append(':')
                  .Append(judgementConfiguration.EtternaJustice);
        }

        foreach (YokkoHitObject hitObject in beatmap.HitObjects)
        {
            source.Append('\u001e')
                  .Append(hitObject.Lane).Append(',')
                  .Append(hitObject.StartTimeMilliseconds.ToString(
                      "R",
                      CultureInfo.InvariantCulture)).Append(',')
                  .Append(hitObject.EndTimeMilliseconds?.ToString(
                      "R",
                      CultureInfo.InvariantCulture)).Append(',')
                  .Append((int)hitObject.Kind);
        }

        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(source.ToString())));
    }
}
