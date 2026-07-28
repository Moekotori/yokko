namespace Yokko.Core.Scoring;

public sealed class JudgementCounter
{
    private readonly Dictionary<JudgementRating, int> counts = [];

    public int Perfect => this[JudgementRating.Perfect];
    public int Great => this[JudgementRating.Great];
    public int Good => this[JudgementRating.Good];
    public int Ok => this[JudgementRating.Ok];
    public int Meh => this[JudgementRating.Meh];
    public int Miss => this[JudgementRating.Miss];
    public int ComboBreak => this[JudgementRating.ComboBreak];

    public int TotalBasic => Perfect + Great + Good + Ok + Meh + Miss;

    public int this[JudgementRating rating] => counts.GetValueOrDefault(rating);

    public IReadOnlyDictionary<JudgementRating, int> All => counts;

    public void Add(JudgementRating rating)
        => counts[rating] = this[rating] + 1;
}
