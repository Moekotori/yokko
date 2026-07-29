namespace Yokko.Core.Beatmaps;

/// <summary>
/// Ruleset-neutral gameplay hit sample metadata.
/// The fields mirror osu!lazer's <c>HitSampleInfo</c> lookup identity while
/// keeping resource resolution outside Core.
/// </summary>
public sealed record YokkoHitSample
{
    public const string HitNormal = "hitnormal";
    public const string HitWhistle = "hitwhistle";
    public const string HitFinish = "hitfinish";
    public const string HitClap = "hitclap";
    public const string SliderSlide = "sliderslide";
    public const string SliderWhistle = "sliderwhistle";

    public const string BankNormal = "normal";
    public const string BankSoft = "soft";
    public const string BankDrum = "drum";

    public YokkoHitSample(
        string Name,
        string Bank = BankNormal,
        int Volume = 100,
        int CustomSampleBank = 0,
        string? Filename = null,
        bool IsLayered = false)
    {
        if (string.IsNullOrWhiteSpace(Name))
            throw new ArgumentException("A sample name is required.", nameof(Name));
        if (string.IsNullOrWhiteSpace(Bank))
            throw new ArgumentException("A sample bank is required.", nameof(Bank));
        if (Volume is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(Volume));
        if (CustomSampleBank < 0)
            throw new ArgumentOutOfRangeException(nameof(CustomSampleBank));

        this.Name = Name;
        this.Bank = Bank;
        this.Volume = Volume;
        this.CustomSampleBank = CustomSampleBank;
        this.Filename = string.IsNullOrWhiteSpace(Filename)
            ? null
            : Filename;
        this.IsLayered = IsLayered;
    }

    public string Name { get; }

    public string Bank { get; }

    public int Volume { get; }

    public int CustomSampleBank { get; }

    public string? Filename { get; }

    /// <summary>
    /// A normal sample which is present only to layer additions on converted
    /// maps. Native osu!mania maps intentionally silence this layer.
    /// </summary>
    public bool IsLayered { get; }

    /// <summary>
    /// Lookup stems in the same priority order as osu!lazer's
    /// <c>HitSampleInfo.LookupNames</c>. Extensions are selected by the
    /// platform resource resolver.
    /// </summary>
    public IEnumerable<string> LookupNames()
    {
        if (!string.IsNullOrWhiteSpace(Filename))
        {
            yield return Filename;
            string withoutExtension = Path.ChangeExtension(Filename, null);
            if (!Filename.Equals(withoutExtension, StringComparison.Ordinal))
                yield return withoutExtension;
        }

        if (CustomSampleBank >= 2)
            yield return $"{Bank}-{Name}{CustomSampleBank}";

        yield return $"{Bank}-{Name}";
        yield return Name;
    }
}

public sealed record YokkoHitSamplePayload
{
    public YokkoHitSamplePayload(
        IReadOnlyList<YokkoHitSample>? Samples = null,
        IReadOnlyList<IReadOnlyList<YokkoHitSample>>? NodeSamples = null,
        bool PlaySlidingSamples = false)
    {
        this.Samples = Samples?.ToArray() ?? [];
        this.NodeSamples = NodeSamples?
            .Select(static node => (IReadOnlyList<YokkoHitSample>)node.ToArray())
            .ToArray()
            ?? [];
        this.PlaySlidingSamples = PlaySlidingSamples;
    }

    public IReadOnlyList<YokkoHitSample> Samples { get; }

    public IReadOnlyList<IReadOnlyList<YokkoHitSample>> NodeSamples { get; }

    public bool PlaySlidingSamples { get; }
}
