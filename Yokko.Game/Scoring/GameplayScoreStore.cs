using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
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
    string[] Mods = null,
    int ComboBreaks = 0,
    int MaxMissCombo = 0,
    ManiaModConfigurationEnvelope ModConfiguration = null,
    string ReplayPath = null,
    string PlayerName = null,
    string PlayerId = null,
    string Source = null,
    bool? IsCurrentPlayer = null,
    string ExternalScoreId = null,
    JudgementConfiguration? JudgementConfiguration = null)
{
    [JsonIgnore]
    public ManiaModSet ModSet
    {
        get
        {
            if (ModConfiguration != null)
            {
                try
                {
                    return ManiaModConfigurationCodec.Restore(
                        ModConfiguration);
                }
                catch (Exception exception) when (
                    exception is InvalidDataException
                    or NotSupportedException
                    or ArgumentException)
                {
                    // Scores written by a newer build must remain readable.
                }
            }

            ManiaModId[] legacyMods = (Mods ?? [])
                .Select(label => OsuManiaModParityCatalog.All
                    .FirstOrDefault(definition => string.Equals(
                        definition.Acronym,
                        label,
                        StringComparison.OrdinalIgnoreCase)))
                .Where(static definition => definition != null)
                .Select(static definition => definition!.Id)
                .ToArray();
            return legacyMods.Length == 0
                ? ManiaModSet.Empty
                : new ManiaModSet(legacyMods);
        }
    }

    [JsonIgnore]
    public IReadOnlyList<string> ModLabels
    {
        get
        {
            if (ModConfiguration != null)
            {
                try
                {
                    return ModSet.DisplayLabels;
                }
                catch (Exception exception) when (
                    exception is InvalidDataException
                    or NotSupportedException
                    or ArgumentException)
                {
                    // Scores written by a newer build must remain readable.
                }
            }

            return Mods ?? [];
        }
    }
}

internal sealed class GameplayScoreStore
{
    private const string scores_filename = "Scores/scores.json";
    private const string history_filename = "Scores/history.json";
    private const int maximum_history_entries = 100;

    private readonly Dictionary<string, StoredGameplayScore> scores =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<StoredGameplayScore>> history =
        new(StringComparer.Ordinal);
    private string scoresPath;
    private string historyPath;

    public void Initialise(Storage storage)
    {
        ArgumentNullException.ThrowIfNull(storage);
        scoresPath = storage.GetFullPath(scores_filename, true);
        historyPath = storage.GetFullPath(history_filename, true);
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
        ManiaScoreResult result,
        string replayPath = null,
        DateTimeOffset? playedAt = null)
    {
        ensureInitialised();
        mods ??= ManiaModSet.Empty;
        if (mods.IsAutomation && !mods.IsDeveloperAutoplay)
            return false;

        string key = keyFor(
            beatmap,
            mods,
            judgementConfiguration);

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
            playedAt ?? DateTimeOffset.UtcNow,
            mods.Acronyms.ToArray(),
            result.ComboBreaks,
            result.MaxMissCombo,
            ManiaModConfigurationCodec.Capture(mods),
            replayPath,
            JudgementConfiguration: judgementConfiguration);
        string historyKey = historyKeyFor(
            beatmap,
            judgementConfiguration);
        List<StoredGameplayScore> attempts =
            history.GetValueOrDefault(historyKey) ?? [];
        history[historyKey] = attempts;
        attempts.Insert(0, replacement);
        if (attempts.Count > maximum_history_entries)
            attempts.RemoveRange(
                maximum_history_entries,
                attempts.Count - maximum_history_entries);

        bool isBest = !scores.TryGetValue(
                          key,
                          out StoredGameplayScore existing)
                      || existing.Score < result.Score;
        if (isBest)
            scores[key] = replacement;

        if (save())
            return isBest;

        attempts.Remove(replacement);
        if (attempts.Count == 0)
            history.Remove(historyKey);
        if (isBest)
        {
            if (existing != null)
                scores[key] = existing;
            else
                scores.Remove(key);
        }

        return false;
    }

    public IReadOnlyList<StoredGameplayScore> GetHistory(
        YokkoBeatmap beatmap,
        JudgementConfiguration judgementConfiguration,
        int limit = 50)
    {
        ensureInitialised();
        if (limit <= 0)
            return [];

        return history.GetValueOrDefault(
                   historyKeyFor(beatmap, judgementConfiguration))?
               .Where(static score => score.IsCurrentPlayer != false)
               .OrderByDescending(score => score.Score)
               .ThenByDescending(score => score.PlayedAt)
               .Take(limit)
               .ToArray()
               ?? [];
    }

    public IReadOnlyList<StoredGameplayScore> GetRanking(
        YokkoBeatmap beatmap,
        JudgementConfiguration judgementConfiguration,
        int limit = 50)
    {
        ensureInitialised();
        if (limit <= 0)
            return [];

        IEnumerable<StoredGameplayScore> ranking =
            history.GetValueOrDefault(
                historyKeyFor(beatmap, judgementConfiguration))
            ?? [];
        if (judgementConfiguration.Mode != JudgementMode.Yokko)
        {
            ranking = ranking.Concat(
                (history.GetValueOrDefault(historyKeyFor(
                     beatmap,
                     JudgementConfiguration.YokkoDefault))
                 ?? []).Where(static score => score.IsCurrentPlayer == false));
        }

        return ranking.OrderByDescending(score => score.Score)
                      .ThenByDescending(score => score.PlayedAt)
                      .Take(limit)
                      .ToArray();
    }

    public bool ImportExternalScore(
        YokkoBeatmap beatmap,
        ManiaModSet mods,
        JudgementConfiguration judgementConfiguration,
        ManiaScoreResult result,
        string playerName,
        string playerId,
        string source,
        string externalScoreId,
        string replayPath,
        DateTimeOffset? playedAt = null)
    {
        ensureInitialised();
        ArgumentNullException.ThrowIfNull(beatmap);
        ArgumentNullException.ThrowIfNull(result);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(externalScoreId);
        mods ??= ManiaModSet.Empty;

        string historyKey = historyKeyFor(beatmap, judgementConfiguration);
        List<StoredGameplayScore> attempts =
            history.GetValueOrDefault(historyKey) ?? [];
        history[historyKey] = attempts;
        if (attempts.Any(score =>
                string.Equals(
                    score.ExternalScoreId,
                    externalScoreId,
                    StringComparison.Ordinal)))
        {
            return false;
        }

        var imported = new StoredGameplayScore(
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
            playedAt ?? DateTimeOffset.UtcNow,
            mods.Acronyms.ToArray(),
            result.ComboBreaks,
            result.MaxMissCombo,
            ManiaModConfigurationCodec.Capture(mods),
            replayPath,
            string.IsNullOrWhiteSpace(playerName)
                ? source.ToUpperInvariant() + " PLAYER"
                : playerName.Trim(),
            string.IsNullOrWhiteSpace(playerId) ? null : playerId.Trim(),
            source.Trim().ToLowerInvariant(),
            false,
            externalScoreId,
            judgementConfiguration);
        attempts.Insert(0, imported);
        if (attempts.Count > maximum_history_entries)
        {
            attempts.RemoveRange(
                maximum_history_entries,
                attempts.Count - maximum_history_entries);
        }

        if (save())
            return true;

        attempts.Remove(imported);
        if (attempts.Count == 0)
            history.Remove(historyKey);
        return false;
    }

    private void load()
    {
        scores.Clear();
        history.Clear();

        if (File.Exists(scoresPath))
        {
            try
            {
                Dictionary<string, StoredGameplayScore> loaded =
                    JsonSerializer.Deserialize<Dictionary<string, StoredGameplayScore>>(
                        File.ReadAllText(scoresPath));

                if (loaded != null)
                {
                    foreach ((string key, StoredGameplayScore score) in loaded)
                    {
                        if (!isExcludedAutomationScore(score))
                            scores[key] = score;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Could not load saved gameplay scores.");
            }
        }

        if (!File.Exists(historyPath))
            return;

        try
        {
            Dictionary<string, List<StoredGameplayScore>> loaded =
                JsonSerializer.Deserialize<Dictionary<string, List<StoredGameplayScore>>>(
                    File.ReadAllText(historyPath));
            if (loaded == null)
                return;

            foreach ((string key, List<StoredGameplayScore> attempts) in loaded)
            {
                history[key] = attempts?
                               .Where(static score =>
                                   !isExcludedAutomationScore(score))
                               .ToList()
                               ?? [];
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Could not load gameplay score history.");
        }
    }

    private static bool isExcludedAutomationScore(
        StoredGameplayScore score)
    {
        if (score is null)
            return false;

        if (score.ModConfiguration != null)
        {
            try
            {
                if (ManiaModConfigurationCodec
                    .Restore(score.ModConfiguration)
                    .IsCinema)
                {
                    return true;
                }
            }
            catch (Exception exception) when (
                exception is InvalidDataException
                or NotSupportedException
                or ArgumentException)
            {
                // Fall back to legacy acronyms below.
            }
        }

        return score.Mods?.Any(static acronym =>
            string.Equals(
                acronym,
                "AT",
                StringComparison.OrdinalIgnoreCase)
            || string.Equals(
                acronym,
                "CN",
                StringComparison.OrdinalIgnoreCase)) == true;
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

            string temporaryHistoryPath = historyPath + ".tmp";
            File.WriteAllText(
                temporaryHistoryPath,
                JsonSerializer.Serialize(
                    history,
                    new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporaryHistoryPath, historyPath, true);
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
        if (scoresPath == null || historyPath == null)
            throw new InvalidOperationException("The score store is not initialised.");
    }

    private static string historyKeyFor(
        YokkoBeatmap beatmap,
        JudgementConfiguration judgementConfiguration)
    {
        // History intentionally ignores Mods so the per-chart view behaves
        // like osu!'s local score list while retaining each attempt's Mods.
        return keyFor(
            beatmap,
            ManiaModSet.Empty,
            judgementConfiguration);
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
            if (hitObject.HoldType != HoldNoteType.Standard)
            {
                source.Append(',')
                      .Append("hold:")
                      .Append((int)hitObject.HoldType);
            }
        }

        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(source.ToString())));
    }
}
