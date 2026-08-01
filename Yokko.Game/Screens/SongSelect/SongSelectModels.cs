using System;
using System.Collections.Generic;
using Yokko.Core.Beatmaps;
using Yokko.Core.Difficulty;
using Yokko.Core.Scoring;

namespace Yokko.Game.Screens.SongSelect;

internal sealed class SongSelectSelectionMemory
{
    internal string ChartId { get; set; }
}

internal enum SongSelectScoreView
{
    Personal,
    GlobalRanking,
}

internal sealed record SongSelectScore(
    int Rank,
    string PlayerName,
    string AvatarTexture,
    ScoreRank Grade,
    int Score,
    double Accuracy,
    int MaxCombo,
    IReadOnlyList<string> Mods,
    bool IsCurrentPlayer = false,
    DateTimeOffset? PlayedAt = null,
    int Perfect = 0,
    int Great = 0,
    int Good = 0,
    int Ok = 0,
    int Meh = 0,
    int Miss = 0,
    int ComboBreaks = 0,
    int MaxMissCombo = 0,
    string ReplayPath = null);

internal sealed record SongSelectEntry
{
    public SongSelectEntry(
        YokkoBeatmap beatmap,
        string wallpaperTexture,
        ManiaMsdResult difficultyRating,
        ManiaStarRatingResult starRating,
        TimeSpan length,
        double bpm,
        int bestScore,
        double bestAccuracy,
        IReadOnlyList<SongSelectScore> ranking,
        IReadOnlyList<SongSelectScore> history,
        string packageId,
        string packageName,
        bool isPackage,
        string chartId = null,
        bool isReadOnly = false)
    {
        Beatmap = beatmap;
        WallpaperTexture = wallpaperTexture;
        DifficultyRating = difficultyRating;
        StarRating = starRating;
        Length = length;
        Bpm = bpm;
        BestScore = bestScore;
        BestAccuracy = bestAccuracy;
        Ranking = ranking;
        History = history;
        PackageId = packageId;
        PackageName = packageName;
        IsPackage = isPackage;
        ChartId = chartId;
        IsReadOnly = isReadOnly;
    }

    public YokkoBeatmap Beatmap { get; set; }
    public string WallpaperTexture { get; init; }
    public ManiaMsdResult DifficultyRating { get; init; }
    public ManiaStarRatingResult StarRating { get; init; }
    public TimeSpan Length { get; init; }
    public double Bpm { get; init; }
    public int BestScore { get; init; }
    public double BestAccuracy { get; init; }
    public IReadOnlyList<SongSelectScore> Ranking { get; init; }
    public IReadOnlyList<SongSelectScore> History { get; init; }
    public string PackageId { get; init; }
    public string PackageName { get; init; }
    public bool IsPackage { get; init; }
    public string ChartId { get; init; }
    public bool IsReadOnly { get; init; }
}
