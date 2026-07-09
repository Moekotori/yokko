using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Audio;
using osu.Framework.Audio.Track;
using Yokko.Audio;

namespace Yokko.Game.Audio;

public sealed class OsuFrameworkAudioEngine : IAudioEngine
{
    private readonly AudioManager audioManager;
    private Track currentTrack;
    private SingleFileResourceStore currentStore;

    public OsuFrameworkAudioEngine(AudioManager audioManager)
    {
        this.audioManager = audioManager;
    }

    public AudioEngineStatus Status { get; private set; } = stoppedStatus;

    public double PlaybackTimeMilliseconds => currentTrack?.CurrentTime ?? 0;

    public IReadOnlyList<AudioBackendCapabilities> Backends { get; } =
    [
        new(AudioBackendKind.SharedWasapi, false, false, false, "osu!framework shared output path backed by BASS/WASAPI on Windows."),
        new(AudioBackendKind.WasapiExclusive, true, true, false, "Planned exclusive Windows endpoint access for lower latency."),
        new(AudioBackendKind.Asio, true, true, true, "Planned pro-audio driver path for devices with native ASIO support."),
    ];

    public ValueTask<IReadOnlyList<AudioDeviceInfo>> GetOutputDevicesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var devices = new List<AudioDeviceInfo>
        {
            new(
                string.Empty,
                "Default",
                AudioBackendKind.SharedWasapi,
                [],
                [] ,
                string.IsNullOrEmpty(audioManager.AudioDevice.Value)),
        };

        devices.AddRange(audioManager.AudioDeviceNames.Select(deviceName => new AudioDeviceInfo(
            deviceName,
            deviceName,
            AudioBackendKind.SharedWasapi,
            [],
            [],
            string.Equals(audioManager.AudioDevice.Value, deviceName, StringComparison.Ordinal))));

        return ValueTask.FromResult<IReadOnlyList<AudioDeviceInfo>>(devices);
    }

    public async ValueTask StartAsync(AudioEngineStartRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (request.PreferredBackend != AudioBackendKind.SharedWasapi)
            throw new NotSupportedException("The first osu!framework audio backend only supports shared output.");

        if (string.IsNullOrWhiteSpace(request.AudioPath))
            throw new ArgumentException("An audio file path is required.", nameof(request));

        string audioPath = Path.GetFullPath(request.AudioPath);
        if (!File.Exists(audioPath))
            throw new FileNotFoundException("Audio file was not found.", audioPath);

        await StopAsync(cancellationToken).ConfigureAwait(false);

        audioManager.UseExperimentalWasapi.Value = true;
        audioManager.AudioDevice.Value = request.DeviceId ?? string.Empty;

        currentStore = new SingleFileResourceStore(audioPath);
        ITrackStore trackStore = audioManager.GetTrackStore(currentStore);
        currentTrack = await trackStore.GetAsync(Path.GetFileName(audioPath), cancellationToken).ConfigureAwait(false);

        if (currentTrack == null)
            throw new InvalidOperationException($"osu!framework could not load audio file '{audioPath}'.");

        await currentTrack.SeekAsync(0).ConfigureAwait(false);
        await currentTrack.StartAsync().ConfigureAwait(false);

        Status = new AudioEngineStatus(
            AudioBackendKind.SharedWasapi,
            string.IsNullOrWhiteSpace(request.DeviceId) ? "Default" : request.DeviceId,
            request.PreferredSampleRate,
            request.PreferredBufferSize,
            0,
            false,
            true,
            false);
    }

    public async ValueTask PauseAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (currentTrack == null)
            return;

        await currentTrack.StopAsync().ConfigureAwait(false);
        setRunning(false);
    }

    public async ValueTask SeekAsync(double timeMilliseconds, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (currentTrack == null)
            return;

        await currentTrack.SeekAsync(Math.Max(0, timeMilliseconds)).ConfigureAwait(false);
    }

    public async ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (currentTrack != null)
        {
            await currentTrack.StopAsync().ConfigureAwait(false);
            await currentTrack.SeekAsync(0).ConfigureAwait(false);
            currentTrack.Dispose();
            currentTrack = null;
        }

        currentStore?.Dispose();
        currentStore = null;
        Status = stoppedStatus;
    }

    public async ValueTask DisposeAsync()
        => await StopAsync().ConfigureAwait(false);

    private void setRunning(bool isRunning)
        => Status = Status with { IsRunning = isRunning };

    private static readonly AudioEngineStatus stoppedStatus = new(
        AudioBackendKind.SharedWasapi,
        null,
        0,
        0,
        0,
        false,
        false,
        false);
}
