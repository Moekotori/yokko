namespace Yokko.Core.Scoring;

public static class ScoreRankExtensions
{
    public static string ToDisplayLabel(this ScoreRank rank) =>
        rank switch
        {
            ScoreRank.X => "SS",
            ScoreRank.XH => "SSH",
            _ => rank.ToString(),
        };
}
