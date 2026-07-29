namespace Yokko.Core.Beatmaps;

public sealed record YokkoHitObject
{
    public YokkoHitObject(
        int Lane,
        double StartTimeMilliseconds,
        double? EndTimeMilliseconds,
        HitObjectKind Kind,
        string? SampleKey = null,
        string? ScrollProfileId = null,
        YokkoHitSamplePayload? SamplePayload = null)
    {
        if (!double.IsFinite(StartTimeMilliseconds))
            throw new ArgumentOutOfRangeException(nameof(StartTimeMilliseconds));

        if (EndTimeMilliseconds is double endTime
            && !double.IsFinite(endTime))
        {
            throw new ArgumentOutOfRangeException(nameof(EndTimeMilliseconds));
        }

        if (Kind == HitObjectKind.Hold
            && (EndTimeMilliseconds is not double holdEnd
                || holdEnd < StartTimeMilliseconds))
        {
            throw new ArgumentException(
                "Hold notes require an end time at or after their start time.",
                nameof(EndTimeMilliseconds));
        }

        this.Lane = Lane;
        this.StartTimeMilliseconds = StartTimeMilliseconds;
        this.EndTimeMilliseconds = EndTimeMilliseconds;
        this.Kind = Kind;
        this.SampleKey = SampleKey;
        this.ScrollProfileId = ScrollProfileId;
        this.SamplePayload = SamplePayload;
    }

    public int Lane { get; }

    public double StartTimeMilliseconds { get; }

    public double? EndTimeMilliseconds { get; }

    public HitObjectKind Kind { get; }

    public string? SampleKey { get; }

    public string? ScrollProfileId { get; }

    public YokkoHitSamplePayload? SamplePayload { get; }

    public IReadOnlyList<YokkoHitSample> Samples =>
        SamplePayload?.Samples ?? [];

    public IReadOnlyList<IReadOnlyList<YokkoHitSample>> NodeSamples =>
        SamplePayload?.NodeSamples ?? [];

    public bool PlaySlidingSamples =>
        SamplePayload?.PlaySlidingSamples == true;
}
