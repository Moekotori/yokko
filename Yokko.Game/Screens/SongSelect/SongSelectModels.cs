using System;
using System.Collections.Generic;
using Yokko.Core.Beatmaps;
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
    IReadOnlyList<string> Mods,
    bool IsCurrentPlayer = false);

internal sealed record SongSelectEntry(
    YokkoBeatmap Beatmap,
    string WallpaperTexture,
    double StarRating,
    TimeSpan Length,
    double Bpm,
    int BestScore,
    double BestAccuracy,
    IReadOnlyList<SongSelectScore> Ranking);
