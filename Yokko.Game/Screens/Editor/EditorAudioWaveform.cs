using System;
using System.Collections.Generic;

namespace Yokko.Game.Screens.Editor;

public sealed class EditorAudioWaveform
{
    public static EditorAudioWaveform Missing { get; } = new(null, WaveformLoadState.Missing, 0, [], [], [], [], "No audio");

    private EditorAudioWaveform(
        string audioPath,
        WaveformLoadState state,
        double durationMilliseconds,
        IReadOnlyList<float> peaks,
        IReadOnlyList<float> lows,
        IReadOnlyList<float> mids,
        IReadOnlyList<float> highs,
        string label)
    {
        AudioPath = audioPath;
        State = state;
        DurationMilliseconds = durationMilliseconds;
        Peaks = peaks;
        Lows = lows;
        Mids = mids;
        Highs = highs;
        Label = label;
    }

    public string AudioPath { get; }

    public WaveformLoadState State { get; }

    public double DurationMilliseconds { get; }

    public IReadOnlyList<float> Peaks { get; }

    public IReadOnlyList<float> Lows { get; }

    public IReadOnlyList<float> Mids { get; }

    public IReadOnlyList<float> Highs { get; }

    public string Label { get; }

    public bool HasAudio => State == WaveformLoadState.Ready && Peaks.Count > 0;

    public static EditorAudioWaveform Loading(string audioPath)
        => new(audioPath, WaveformLoadState.Loading, 0, [], [], [], [], "Loading audio");

    public static EditorAudioWaveform Failed(string audioPath, string message)
        => new(audioPath, WaveformLoadState.Failed, 0, [], [], [], [], message);

    public static EditorAudioWaveform Ready(
        string audioPath,
        double durationMilliseconds,
        IReadOnlyList<float> peaks,
        IReadOnlyList<float> lows,
        IReadOnlyList<float> mids,
        IReadOnlyList<float> highs)
        => new(audioPath, WaveformLoadState.Ready, durationMilliseconds, peaks, lows, mids, highs, "Audio waveform");

    public EditorWaveformSample Sample(double startMilliseconds, double endMilliseconds, double fallbackDurationMilliseconds)
    {
        if (!HasAudio)
            return EditorWaveformSample.Empty;

        double duration = DurationMilliseconds > 0 ? DurationMilliseconds : fallbackDurationMilliseconds;
        if (duration <= 0)
            return EditorWaveformSample.Empty;

        int startIndex = timeToStartIndex(startMilliseconds, duration);
        int endIndex = Math.Max(startIndex + 1, timeToEndIndex(endMilliseconds, duration));
        endIndex = Math.Min(endIndex, Peaks.Count);

        float peak = 0;
        float low = 0;
        float mid = 0;
        float high = 0;

        for (int i = startIndex; i < endIndex; i++)
        {
            peak = Math.Max(peak, Peaks[i]);
            low = Math.Max(low, Lows[i]);
            mid = Math.Max(mid, Mids[i]);
            high = Math.Max(high, Highs[i]);
        }

        return new EditorWaveformSample(peak, low, mid, high);
    }

    private int timeToStartIndex(double milliseconds, double durationMilliseconds)
    {
        double progress = Math.Clamp(milliseconds / durationMilliseconds, 0, 1);
        return Math.Clamp((int)Math.Floor(progress * Peaks.Count), 0, Math.Max(0, Peaks.Count - 1));
    }

    private int timeToEndIndex(double milliseconds, double durationMilliseconds)
    {
        double progress = Math.Clamp(milliseconds / durationMilliseconds, 0, 1);
        return Math.Clamp((int)Math.Ceiling(progress * Peaks.Count), 1, Peaks.Count);
    }
}

public enum WaveformLoadState
{
    Missing,
    Loading,
    Ready,
    Failed,
}

public readonly record struct EditorWaveformSample(float Peak, float Low, float Mid, float High)
{
    public static EditorWaveformSample Empty { get; } = new(0, 0, 0, 0);
}
