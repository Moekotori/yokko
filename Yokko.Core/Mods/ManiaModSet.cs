namespace Yokko.Core.Mods;

/// <summary>
/// Immutable, canonically ordered set of Mania Mods selected for one gameplay
/// session. Configurable Mod values will be added as versioned settings rather
/// than changing these stable identifiers.
/// </summary>
public sealed class ManiaModSet : IEquatable<ManiaModSet>
{
    private static readonly ManiaModId[] rateMods =
    [
        ManiaModId.HalfTime,
        ManiaModId.Daycore,
        ManiaModId.DoubleTime,
        ManiaModId.Nightcore,
    ];

    private static readonly ManiaModId[] holdRuleMods =
    [
        ManiaModId.NoRelease,
        ManiaModId.HoldOff,
    ];

    private readonly ManiaModId[] mods;

    public static ManiaModSet Empty { get; } = new([]);

    public ManiaModSet(
        IEnumerable<ManiaModId> mods,
        int? randomSeed = null)
    {
        ArgumentNullException.ThrowIfNull(mods);
        this.mods = mods.Distinct()
                        .OrderBy(static mod => mod)
                        .ToArray();
        RandomSeed = Contains(ManiaModId.Random)
            ? randomSeed ?? 0
            : null;

        int selectedRateMods = this.mods.Count(rateMods.Contains);
        if (selectedRateMods > 1)
        {
            throw new ArgumentException(
                "Only one fixed-rate Mod may be selected.",
                nameof(mods));
        }

        int selectedHoldRuleMods = this.mods.Count(holdRuleMods.Contains);
        if (selectedHoldRuleMods > 1)
        {
            throw new ArgumentException(
                "No Release and Hold Off are mutually exclusive.",
                nameof(mods));
        }
    }

    public IReadOnlyList<ManiaModId> Mods => mods;

    public int? RandomSeed { get; }

    public bool IsEmpty => mods.Length == 0;

    public double PlaybackRate =>
        Contains(ManiaModId.DoubleTime)
        || Contains(ManiaModId.Nightcore)
            ? 1.5
            : Contains(ManiaModId.HalfTime)
              || Contains(ManiaModId.Daycore)
                ? 0.75
            : 1;

    public bool ChangesAudioPitch =>
        Contains(ManiaModId.Nightcore)
        || Contains(ManiaModId.Daycore);

    public bool IsAutomation => Contains(ManiaModId.Autoplay);

    public string Fingerprint => IsEmpty
        ? "nm"
        : string.Join(
            '+',
            mods.Select(mod =>
            {
                string key = OsuManiaModParityCatalog.Get(mod).Key;
                return mod == ManiaModId.Random
                    ? $"{key}:{RandomSeed}"
                    : key;
            }));

    public IReadOnlyList<string> Acronyms =>
        mods.Select(static mod =>
                OsuManiaModParityCatalog.Get(mod).Acronym)
            .ToArray();

    public bool Contains(ManiaModId mod) => Array.IndexOf(mods, mod) >= 0;

    public ManiaModSet With(ManiaModId mod, bool enabled)
    {
        var next = mods.ToList();
        next.Remove(mod);

        if (enabled)
        {
            if (rateMods.Contains(mod))
                next.RemoveAll(rateMods.Contains);
            if (holdRuleMods.Contains(mod))
                next.RemoveAll(holdRuleMods.Contains);

            next.Add(mod);
        }

        return next.Count == 0
            ? Empty
            : new ManiaModSet(
                next,
                next.Contains(ManiaModId.Random)
                    ? RandomSeed
                    : null);
    }

    public ManiaModSet WithRandomSeed(int seed)
    {
        var next = mods.ToList();
        if (!next.Contains(ManiaModId.Random))
            next.Add(ManiaModId.Random);

        return new ManiaModSet(next, seed);
    }

    public bool Equals(ManiaModSet? other) =>
        ReferenceEquals(this, other)
        || other != null
        && mods.SequenceEqual(other.mods)
        && RandomSeed == other.RandomSeed;

    public override bool Equals(object? obj) =>
        obj is ManiaModSet other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (ManiaModId mod in mods)
            hash.Add(mod);
        hash.Add(RandomSeed);

        return hash.ToHashCode();
    }
}
