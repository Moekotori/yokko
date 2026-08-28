using System;
using System.Collections.Generic;
using System.Linq;

namespace Yokko.Game.Gameplay;

internal sealed class GameplayCalibrationSession
{
    internal const double DurationMilliseconds = 30_000;
    internal const double LeadInMilliseconds = 1_000;
    internal const double BeatIntervalMilliseconds = 500;
    internal const int MinimumUsefulSamples = 8;

    private readonly List<double> tapOffsets = new();
    private int lastRecordedBeat = -1;
    private double? startPlaybackTimeMilliseconds;

    public int SampleCount => tapOffsets.Count;

    public double LatestTapOffsetMilliseconds =>
        tapOffsets.Count == 0 ? 0 : tapOffsets[^1];

    public bool HasRecommendation => SampleCount >= MinimumUsefulSamples;

    public double SuggestedOffsetMilliseconds =>
        HasRecommendation
            ? Math.Clamp(
                Math.Round(-median(tapOffsets)),
                -200,
                200)
            : 0;

    public bool IsAudioSessionStarted =>
        startPlaybackTimeMilliseconds.HasValue;

    public double StartPlaybackTimeMilliseconds =>
        startPlaybackTimeMilliseconds
        ?? throw new InvalidOperationException(
            "The calibration audio session has not started yet.");

    public void BeginAudioSession(double startPlaybackTimeMilliseconds)
    {
        if (!double.IsFinite(startPlaybackTimeMilliseconds)
            || startPlaybackTimeMilliseconds < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(startPlaybackTimeMilliseconds));
        }

        this.startPlaybackTimeMilliseconds = startPlaybackTimeMilliseconds;
    }

    public bool IsComplete(double playbackTimeMilliseconds) =>
        startPlaybackTimeMilliseconds is double start
        && playbackTimeMilliseconds - start >= DurationMilliseconds;

    public double RemainingMilliseconds(double playbackTimeMilliseconds) =>
        startPlaybackTimeMilliseconds is double start
            ? Math.Max(0, DurationMilliseconds - (playbackTimeMilliseconds - start))
            : DurationMilliseconds;

    public bool TryRecordTapAtPlaybackTime(double playbackTimeMilliseconds)
    {
        if (startPlaybackTimeMilliseconds is not double start)
            return false;

        double elapsed = playbackTimeMilliseconds - start;
        if (elapsed < LeadInMilliseconds
            || elapsed >= DurationMilliseconds)
        {
            return false;
        }

        int beat = (int)Math.Round(
            (elapsed - LeadInMilliseconds) / BeatIntervalMilliseconds);
        double beatTime =
            LeadInMilliseconds + beat * BeatIntervalMilliseconds;

        if (beat == lastRecordedBeat
            || beatTime >= DurationMilliseconds)
        {
            return false;
        }

        double offset = elapsed - beatTime;
        if (Math.Abs(offset) > BeatIntervalMilliseconds / 2)
            return false;

        lastRecordedBeat = beat;
        tapOffsets.Add(offset);
        return true;
    }

    private static double median(IReadOnlyList<double> values)
    {
        double[] ordered = values.OrderBy(value => value).ToArray();
        int middle = ordered.Length / 2;
        return ordered.Length % 2 == 0
            ? (ordered[middle - 1] + ordered[middle]) / 2
            : ordered[middle];
    }
}
