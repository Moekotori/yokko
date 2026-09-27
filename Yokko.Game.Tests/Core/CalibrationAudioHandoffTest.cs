using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Yokko.Audio;
using Yokko.Game.Screens.Main;
using Yokko.Game.Screens.SongSelect;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public class CalibrationAudioHandoffTest
{
    [Test]
    public async Task CalibrationReleasesOutputAfterPendingHomeSongStart()
    {
        using var home = new HomeMusicPlayer();
        var pendingStart = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var engine = new RecordingEngine();
        typeof(HomeMusicPlayer).GetField("audioQueue", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(home, pendingStart.Task);
        typeof(HomeMusicPlayer).GetField("audioEngine", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(home, engine);
        Task released = ((ISongSelectPreviewHost)home).SuspendOutputAsync();
        Assert.That(released.IsCompleted, Is.False);
        Assert.That(engine.Paused, Is.False);
        pendingStart.SetResult();
        await released.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.That(engine.Paused, Is.True);
    }

    private sealed class RecordingEngine : IAudioEngine
    {
        internal bool Paused;
        public AudioEngineStatus Status => default;
        public double PlaybackTimeMilliseconds => 0;
        public double DurationMilliseconds => 0;
        public IReadOnlyList<AudioBackendCapabilities> Backends => [];
        public ValueTask<IReadOnlyList<AudioDeviceInfo>> GetOutputDevicesAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult<IReadOnlyList<AudioDeviceInfo>>([]);
        public ValueTask StartAsync(AudioEngineStartRequest request, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask PauseAsync(CancellationToken cancellationToken = default) { Paused = true; return ValueTask.CompletedTask; }
        public ValueTask SeekAsync(double timeMilliseconds, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask StopAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
