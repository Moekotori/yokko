namespace Yokko.Audio;

public sealed record AudioWaveformAnalysis(
    double DurationMilliseconds,
    float[] Peaks,
    float[] LowIntensity,
    float[] MidIntensity,
    float[] HighIntensity);
