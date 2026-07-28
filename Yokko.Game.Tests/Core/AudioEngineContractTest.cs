using System.Threading.Tasks;
using NUnit.Framework;
using Yokko.Audio;

namespace Yokko.Game.Tests.Core
{
    [TestFixture]
    public sealed class AudioEngineContractTest
    {
        [Test]
        public async Task NullAudioEngineSupportsPlaybackControlsAsNoOps()
        {
            await using var engine = new NullAudioEngine();

            await engine.StartAsync(new AudioEngineStartRequest("missing.wav", AudioBackendKind.SharedWasapi, null, 0, 0, 0));
            await engine.PauseAsync();
            await engine.SeekAsync(250);
            await engine.StopAsync();

            Assert.That(engine.PlaybackTimeMilliseconds, Is.EqualTo(0));
            Assert.That(engine.Status.IsRunning, Is.False);
        }

        [Test]
        public void AvailableAudioBackendsAreOwnedByYokko()
        {
            Assert.That(
                AudioEngineFactory.AvailableBackends,
                Has.None.Matches<AudioBackendCapabilities>(
                    backend => backend.Description.Contains(
                        "osu!framework",
                        System.StringComparison.OrdinalIgnoreCase)));
        }
    }
}
