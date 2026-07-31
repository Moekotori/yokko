using System;
using System.Collections.Generic;
using Yokko.Core.Beatmaps;
using Yokko.Core.Difficulty;
using Yokko.Core.Scoring;

namespace Yokko.Game.Screens.SongSelect;

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
    DateTimeOffset? PlayedAt = null);

internal sealed record SongSelectEntry(
    YokkoBeatmap Beatmap,
    string WallpaperTexture,
    ManiaMsdResult DifficultyRating,
    TimeSpan Length,
    double Bpm,
    int BestScore,
    double BestAccuracy,
    IReadOnlyList<SongSelectScore> Ranking,
    IReadOnlyList<SongSelectScore> History,
    string PackageId,
    string PackageName,
    bool IsPackage);
