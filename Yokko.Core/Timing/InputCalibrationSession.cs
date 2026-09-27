namespace Yokko.Core.Timing;

/// <summary>Tap times are un-offset audio presentation times, never UI elapsed time.</summary>
public sealed class InputCalibrationSession
{
    public const double DurationMilliseconds = 21_000;
    public const double LeadInMilliseconds = 1_000;
    public const double BeatIntervalMilliseconds = 500;
    public const int WarmupBeats = 4;
    public const int MeasurementBeats = 36;
    public const int MinimumUsefulSamples = 24;
    private readonly SortedDictionary<int, double> taps = new();

    public IReadOnlyList<double> Offsets => taps.Values.ToArray();
    public int SampleCount => taps.Count;
    public double LatestTapOffsetMilliseconds { get; private set; }

    public bool TryRecordTap(double audioTime)
    {
        if (!double.IsFinite(audioTime) || audioTime < 0 || audioTime >= DurationMilliseconds)
            return false;
        int beat = (int)Math.Round((audioTime - LeadInMilliseconds) / BeatIntervalMilliseconds);
        double error = audioTime - (LeadInMilliseconds + beat * BeatIntervalMilliseconds);
        if (beat < WarmupBeats || beat >= WarmupBeats + MeasurementBeats
            || Math.Abs(error) > 220 || taps.ContainsKey(beat)
            || (taps.Count > 0 && beat <= taps.Keys.Last()))
            return false;
        taps.Add(beat, error);
        LatestTapOffsetMilliseconds = error;
        return true;
    }

    public CalibrationResult Analyse()
    {
        double[] values = taps.Values.ToArray();
        if (values.Length == 0)
            return new(0, 0, 0, 0, 0, CalibrationQuality.TooFewSamples);
        double centre = Median(values);
        double mad = Median(values.Select(v => Math.Abs(v - centre)).ToArray());
        double cutoff = Math.Max(15, 3 * 1.4826 * mad);
        double[] retained = values.Where(v => Math.Abs(v - centre) <= cutoff).ToArray();
        double median = Median(retained);
        double spread = 1.4826 * Median(retained.Select(v => Math.Abs(v - median)).ToArray());
        // Compare chronological halves, including outliers, so a drift cannot be hidden by trimming.
        int half = values.Length / 2;
        double drift = half == 0 ? 0 : Math.Abs(Median(values[..half]) - Median(values[half..]));
        CalibrationQuality quality = retained.Length < MinimumUsefulSamples
            ? CalibrationQuality.TooFewSamples
            : Math.Abs(median) > 200 ? CalibrationQuality.OutOfRange
            : retained.Length < values.Length * 0.8 || spread > 18 || drift > 12
                ? CalibrationQuality.Unstable : CalibrationQuality.Stable;
        return new(median, spread, drift, retained.Length, values.Length - retained.Length, quality);
    }

    private static double Median(double[] values)
    {
        double[] sorted = values.Order().ToArray();
        int middle = sorted.Length / 2;
        return sorted.Length % 2 == 0 ? (sorted[middle - 1] + sorted[middle]) / 2 : sorted[middle];
    }
}

public enum CalibrationQuality { Stable, TooFewSamples, Unstable, OutOfRange }

public readonly record struct CalibrationResult(
    double MedianMilliseconds, double SpreadMilliseconds, double DriftMilliseconds,
    int AcceptedSamples, int RejectedSamples, CalibrationQuality Quality)
{
    public bool CanApply => Quality == CalibrationQuality.Stable;
    // GameplayClockSnapshot adds the user offset to audio time.
    public double SuggestedOffsetMilliseconds => Math.Round(-MedianMilliseconds);
}
