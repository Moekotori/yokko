using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Yokko.Audio;
using Yokko.Game.Audio;

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
        public async Task SingleFileResourceStoreExposesOnlyRequestedFile()
        {
            string directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "audio-store", TestContext.CurrentContext.Test.ID);
            Directory.CreateDirectory(directory);
            string audioPath = Path.Combine(directory, "tone.wav");
            await File.WriteAllBytesAsync(audioPath, [1, 2, 3, 4]);

            using var store = new SingleFileResourceStore(audioPath);

            Assert.That(store.GetAvailableResources().Single(), Is.EqualTo("tone.wav"));
            Assert.That(store.Get("tone.wav"), Is.EqualTo(new byte[] { 1, 2, 3, 4 }));
            Assert.That(store.Get(Path.GetFullPath(audioPath)), Is.EqualTo(new byte[] { 1, 2, 3, 4 }));
            Assert.That(store.Get("other.wav"), Is.Null);
        }
    }
}
