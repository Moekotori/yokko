namespace Yokko.Core.Scoring;

public enum JudgementMode
{
    Yokko,
    Etterna,
    Quaver,
}

/// <summary>
/// Selects the gameplay judgement rules without coupling player preferences to
/// the judgement state itself.
/// </summary>
public readonly record struct JudgementConfiguration
{
    public const int MinimumEtternaJustice = 4;
    public const int MaximumEtternaJustice = 9;
    public const int DefaultEtternaJustice = 4;

    public static JudgementConfiguration YokkoDefault { get; } =
        new(JudgementMode.Yokko);

    public static JudgementConfiguration EtternaDefault { get; } =
        new(JudgementMode.Etterna);

    public static JudgementConfiguration QuaverDefault { get; } =
        new(JudgementMode.Quaver);

    public JudgementConfiguration(
        JudgementMode mode,
        int etternaJustice = DefaultEtternaJustice)
    {
        if (!Enum.IsDefined(mode))
            throw new ArgumentOutOfRangeException(nameof(mode));
        if (etternaJustice is < MinimumEtternaJustice
            or > MaximumEtternaJustice)
        {
            throw new ArgumentOutOfRangeException(
                nameof(etternaJustice));
        }

        Mode = mode;
        EtternaJustice = etternaJustice;
    }

    public JudgementMode Mode { get; }

    public int EtternaJustice { get; }

    /// <summary>
    /// Etterna's J4-J8 and Justice timing scales.
    /// Source: etternagame/etterna GameState.h
    /// commit 939a26ae042d3a689999a0dae630721c7701f187 (MIT).
    /// </summary>
    public double EtternaTimingScale => EtternaJustice switch
    {
        4 => 1.00,
        5 => 0.84,
        6 => 0.66,
        7 => 0.50,
        8 => 0.33,
        9 => 0.20,
        _ => throw new InvalidOperationException(
            "The Etterna Justice value is outside the supported range."),
    };

    public string EtternaJusticeLabel =>
        EtternaJustice == MaximumEtternaJustice
            ? "Justice"
            : $"J{EtternaJustice}";

    public string RatingLabel(JudgementRating rating)
    {
        if (Mode == JudgementMode.Quaver)
        {
            return rating switch
            {
                JudgementRating.Perfect => "MARVELOUS",
                JudgementRating.Great => "PERFECT",
                JudgementRating.Good => "GREAT",
                JudgementRating.Ok => "GOOD",
                JudgementRating.Meh => "OKAY",
                JudgementRating.Miss
                    or JudgementRating.ComboBreak => "MISS",
                _ => rating.ToString().ToUpperInvariant(),
            };
        }

        if (Mode != JudgementMode.Etterna)
            return rating == JudgementRating.ComboBreak
                ? "MISS"
                : rating.ToString().ToUpperInvariant();

        return rating switch
        {
            JudgementRating.Perfect => "MARVELOUS",
            JudgementRating.Great => "PERFECT",
            JudgementRating.Good => "GREAT",
            JudgementRating.Ok => "GOOD",
            JudgementRating.Meh => "BAD",
            JudgementRating.Miss
                or JudgementRating.ComboBreak => "MISS",
            _ => rating.ToString().ToUpperInvariant(),
        };
    }
}
