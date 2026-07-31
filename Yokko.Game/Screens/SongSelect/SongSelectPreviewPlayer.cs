using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Logging;
using Yokko.Audio;
using Yokko.Core.Beatmaps;
using Yokko.Core.Mods;
using Yokko.Game.Audio;

namespace Yokko.Game.Screens.SongSelect;

/// <summary>
/// Owns song-select preview playback independently from gameplay audio.
/// </summary>
internal sealed class SongSelectPreviewPlayer : IAsyncDisposable
{
    private const int selection_debounce_milliseconds = 80;

    private sealed record PreviewRequest(
        string AudioPath,
        double PreviewTimeMilliseconds,
        double PlaybackRate,
        AudioPitchMode PitchMode,
        double? FixedFrequencyScale)
    {
        public bool MatchesPlayback(PreviewRequest other) =>
            string.Equals(
                AudioPath,
                other.AudioPath,
                StringComparison.OrdinalIgnoreCase)
            && Math.Abs(PlaybackRate - other.PlaybackRate) < 0.000001
            && PitchMode == other.PitchMode
            && Nullable.Equals(
                FixedFrequencyScale,
                other.FixedFrequencyScale);

        public bool MatchesAudioPolicy(PreviewRequest other) =>
            string.Equals(
                AudioPath,
                other.AudioPath,
                StringComparison.OrdinalIgnoreCase)
            && PitchMode == other.PitchMode
            && Nullable.Equals(
                FixedFrequencyScale,
                other.FixedFrequencyScale);
    }

    private readonly object operationLock = new();
    private readonly IAudioEngine audioEngine;
    private readonly YokkoAudioSettings audioSettings;
    private readonly bool ownsAudioEngine;
    private Task operationQueue = Task.CompletedTask;
    private CancellationTokenSource operationCancellation = new();
    private PreviewRequest currentRequest;
    private int generation;
    private bool disposed;
    private bool hasStartedCurrentRequest;

    public SongSelectPreviewPlayer(
        IAudioEngine audioEngine,
        YokkoAudioSettings audioSettings,
        bool ownsAudioEngine = true)
    {
        this.audioEngine = audioEngine
                           ?? throw new ArgumentNullException(
                               nameof(audioEngine));
        this.audioSettings = audioSettings
                             ?? throw new ArgumentNullException(
                                 nameof(audioSettings));
        this.ownsAudioEngine = ownsAudioEngine;
        this.audioSettings.MixChanged += onMixChanged;
    }

    internal string CurrentAudioPath => currentRequest?.AudioPath;

    internal bool IsPlaying => audioEngine.Status.IsRunning;

    internal void Play(YokkoBeatmap beatmap, ManiaModSet mods)
    {
        PreviewRequest request = createRequest(beatmap, mods);

        lock (operationLock)
        {
            if (disposed
                || request != null
                && currentRequest?.MatchesPlayback(request) == true)
            {
                return;
            }

            currentRequest = request;
            hasStartedCurrentRequest = false;
            queuePlaybackChange(request);
        }
    }

    internal void EnsurePlaying()
    {
        lock (operationLock)
        {
            if (disposed
                || currentRequest == null
                || !hasStartedCurrentRequest
                || !operationQueue.IsCompleted)
            {
                return;
            }

            AudioEngineSnapshot snapshot = audioEngine.Snapshot;
            double duration = audioEngine.DurationMilliseconds;
            bool reachedEnd = duration > 0
                              && snapshot.PlaybackTimeMilliseconds >= duration;
            if (snapshot.Status.IsRunning && !reachedEnd)
                return;

            queuePlaybackChange(currentRequest);
        }
    }

    internal bool TryUpdatePlaybackRate(
        YokkoBeatmap beatmap,
        ManiaModSet mods)
    {
        PreviewRequest request = createRequest(beatmap, mods);

        lock (operationLock)
        {
            if (disposed
                || request == null
                || currentRequest == null
                || !currentRequest.MatchesAudioPolicy(request)
                || !hasStartedCurrentRequest
                || !operationQueue.IsCompleted
                || audioEngine is not IAudioRateControl rateControl)
            {
                return false;
            }

            try
            {
                rateControl.SetPlaybackRate(request.PlaybackRate);
                currentRequest = request;
                return true;
            }
            catch (Exception exception)
            {
                Logger.Error(
                    exception,
                    "Could not change song-select preview rate in place.",
                    LoggingTarget.Runtime);
                return false;
            }
        }
    }

    internal void Stop()
    {
        lock (operationLock)
        {
            if (disposed || currentRequest == null)
                return;

            currentRequest = null;
            hasStartedCurrentRequest = false;
            queuePlaybackChange(null);
        }
    }

    internal void Detach()
    {
        lock (operationLock)
        {
            if (disposed)
                return;

            currentRequest = null;
            hasStartedCurrentRequest = false;
        }
    }

    internal Task WaitForIdleAsync()
    {
        lock (operationLock)
            return operationQueue;
    }

    internal static double CalculatePreviewStart(
        double preferredMilliseconds,
        double durationMilliseconds)
    {
        if (!double.IsFinite(durationMilliseconds)
            || durationMilliseconds <= 0)
        {
            return 0;
        }

        // Mirrors osu!lazer's fallback for charts without a valid PreviewTime.
        // Reference: ppy/osu, osu.Game/Beatmaps/WorkingBeatmap.cs,
        // commit 9f227ed28b6c8ba46dfea1f000f778d8b2827ad0.
        double start = double.IsFinite(preferredMilliseconds)
                       && preferredMilliseconds >= 0
                       && preferredMilliseconds <= durationMilliseconds
            ? preferredMilliseconds
            : durationMilliseconds * 0.4;

        return Math.Clamp(start, 0, durationMilliseconds);
    }

    public async ValueTask DisposeAsync()
    {
        Task pending;
        CancellationTokenSource cancellation;

        lock (operationLock)
        {
            if (disposed)
                return;

            disposed = true;
            audioSettings.MixChanged -= onMixChanged;
            currentRequest = null;
            hasStartedCurrentRequest = false;
            generation++;
            cancellation = operationCancellation;
            if (ownsAudioEngine)
                cancellation.Cancel();
            pending = operationQueue;
        }

        try
        {
            await pending.ConfigureAwait(false);
        }
        catch
        {
        }
        finally
        {
            cancellation.Dispose();
            if (ownsAudioEngine)
                await audioEngine.DisposeAsync().ConfigureAwait(false);
        }
    }

    private void onMixChanged()
    {
        lock (operationLock)
        {
            if (!disposed && audioEngine is IAudioMixControl mixControl)
                audioSettings.ApplyMixSettings(mixControl);
        }
    }

    private void queuePlaybackChange(PreviewRequest request)
    {
        int requestGeneration = ++generation;
        operationCancellation.Cancel();
        operationCancellation.Dispose();
        operationCancellation = new CancellationTokenSource();
        CancellationToken cancellationToken =
            operationCancellation.Token;

        operationQueue = operationQueue.ContinueWith(
            _ => applyPlaybackChangeAsync(
                request,
                requestGeneration,
                cancellationToken),
            CancellationToken.None,
            TaskContinuationOptions.None,
            TaskScheduler.Default).Unwrap();
    }

    private async Task applyPlaybackChangeAsync(
        PreviewRequest request,
        int requestGeneration,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (request == null)
            {
                await audioEngine.StopAsync(cancellationToken)
                                 .ConfigureAwait(false);
                return;
            }

            await Task.Delay(
                          selection_debounce_milliseconds,
                          cancellationToken)
                      .ConfigureAwait(false);

            if (audioEngine is IAudioMixControl mixControl)
                audioSettings.ApplyMixSettings(mixControl);

            AudioEngineStartRequest startRequest =
                audioSettings.CreateStartRequest(
                    request.AudioPath,
                    request.PlaybackRate,
                    request.PitchMode,
                    request.FixedFrequencyScale) with
                {
                    DynamicPlaybackRate = true,
                };
            await audioEngine.StartAsync(
                                 startRequest,
                                 cancellationToken)
                             .ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();
            double previewStart = CalculatePreviewStart(
                request.PreviewTimeMilliseconds,
                audioEngine.DurationMilliseconds);
            if (previewStart > 0)
            {
                await audioEngine.SeekAsync(
                                     previewStart,
                                     cancellationToken)
                                 .ConfigureAwait(false);
            }

            lock (operationLock)
            {
                if (!disposed && requestGeneration == generation)
                {
                    hasStartedCurrentRequest =
                        audioEngine.Status.IsRunning;
                }
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            Logger.Error(
                exception,
                "Could not update song-select preview playback.",
                LoggingTarget.Runtime);

            lock (operationLock)
            {
                if (!disposed && requestGeneration == generation)
                {
                    currentRequest = null;
                    hasStartedCurrentRequest = false;
                }
            }
        }
    }

    private static PreviewRequest createRequest(
        YokkoBeatmap beatmap,
        ManiaModSet mods)
    {
        string audioPath = beatmap?.AudioPath;
        if (string.IsNullOrWhiteSpace(audioPath)
            || !File.Exists(audioPath))
        {
            return null;
        }

        string extension = Path.GetExtension(audioPath);
        if (!extension.Equals(".wav", StringComparison.OrdinalIgnoreCase)
            && !extension.Equals(".mp3", StringComparison.OrdinalIgnoreCase)
            && !extension.Equals(".ogg", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return new PreviewRequest(
            Path.GetFullPath(audioPath),
            beatmap.PreviewTimeMilliseconds,
            mods.PlaybackRate,
            mods.ChangesAudioPitch
                ? AudioPitchMode.ScaleWithRate
                : AudioPitchMode.Preserve,
            mods.FixedAudioFrequencyScale);
    }
}
