namespace Yokko.Core.Scoring;

public sealed record ManiaScoreResult(
    long Score,
    double Accuracy,
    int MaxCombo,
    ScoreRank Rank,
    int Perfect,
    int Great,
    int Good,
    int Ok,
    int Meh,
    int Miss);
