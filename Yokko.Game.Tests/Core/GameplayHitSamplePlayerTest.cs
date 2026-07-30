using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Yokko.Audio;
using Yokko.Game.Gameplay;

namespace Yokko.Game.Tests.Core
{
    [TestFixture]
    public sealed class GameplayHitSamplePlayerTest
    {
        [Test]
        public void PreparedBindingsBypassStringPlayback()
        {
            var playback = new PreparedTrackingPlayback();
            var handle = new PreparedAudioSampleHandle(new object(), 1, 0);
            var sample = new GameplayHitSamplePlaybackBinding(
                "sample.wav",
                0.4,
                handle,
                true);

            GameplayHitSamplePlayer.TriggerSamples(playback, [sample]);
            uint loopId = GameplayHitSamplePlayer.StartLoopingSample(
                playback,
                sample);

            Assert.Multiple(() =>
            {
                Assert.That(playback.PreparedTriggerCount, Is.EqualTo(1));
                Assert.That(playback.PreparedLoopCount, Is.EqualTo(1));
                Assert.That(playback.StringTriggerCount, Is.Zero);
                Assert.That(playback.StringLoopCount, Is.Zero);
                Assert.That(playback.LastGain, Is.EqualTo(0.4));
                Assert.That(loopId, Is.EqualTo(42));
            });
        }

        [Test]
        public void LegacyPlaybackUsesStringFallback()
        {
            var playback = new LegacyTrackingPlayback();
            var sample = new GameplayHitSamplePlaybackBinding(
                "sample.wav",
                0.7,
                new PreparedAudioSampleHandle(new object(), 1, 0),
                true);

            GameplayHitSamplePlayer.TriggerSamples(playback, [sample]);
            uint loopId = GameplayHitSamplePlayer.StartLoopingSample(
                playback,
                sample);

            Assert.Multiple(() =>
            {
                Assert.That(playback.StringTriggerCount, Is.EqualTo(1));
                Assert.That(playback.StringLoopCount, Is.EqualTo(1));
                Assert.That(playback.LastPath, Is.EqualTo("sample.wav"));
                Assert.That(playback.LastGain, Is.EqualTo(0.7));
                Assert.That(loopId, Is.EqualTo(24));
            });
        }

        [Test]
        public void TimestampedPlaybackSkipsSamplesAlreadyTriggeredByRawInput()
        {
            var playback = new TimestampedTrackingPlayback();
            var first = new GameplayHitSamplePlaybackBinding(
                "first.wav",
                0.4,
                new PreparedAudioSampleHandle(new object(), 1, 0),
                true);
            var second = new GameplayHitSamplePlaybackBinding(
                "second.wav",
                0.7,
                new PreparedAudioSampleHandle(new object(), 1, 1),
                true);

            GameplayHitSamplePlayer.TriggerResult result =
                GameplayHitSamplePlayer.TriggerSamples(
                    playback,
                    [first, second],
                    alreadyTriggeredMask: 1,
                    captureTimestamp: 100,
                    timestampFrequency: 1000);

            Assert.Multiple(() =>
            {
                Assert.That(playback.TimestampedTriggerCount, Is.EqualTo(1));
                Assert.That(playback.RegularPreparedTriggerCount, Is.Zero);
                Assert.That(playback.LastGain, Is.EqualTo(0.7));
                Assert.That(result.AnyTriggered, Is.True);
                Assert.That(result.TriggeredSampleMask, Is.EqualTo(3));
                Assert.That(result.LastAudioEnqueueTimestamp, Is.GreaterThan(0));
            });
        }

        private class LegacyTrackingPlayback : IAudioLoopingSamplePlayback
        {
            public int StringTriggerCount { get; private set; }

            public int StringLoopCount { get; private set; }

            public string LastPath { get; private set; }

            public double LastGain { get; protected set; }

            public ValueTask PrepareSamplesAsync(
                IReadOnlyCollection<string> samplePaths,
                CancellationToken cancellationToken = default) =>
                ValueTask.CompletedTask;

            public bool TriggerSample(string samplePath) =>
                TriggerSample(samplePath, 1);

            public bool TriggerSample(string samplePath, double gain)
            {
                StringTriggerCount++;
                LastPath = samplePath;
                LastGain = gain;
                return true;
            }

            public uint StartLoopingSample(string samplePath, double gain)
            {
                StringLoopCount++;
                LastPath = samplePath;
                LastGain = gain;
                return 24;
            }

            public bool StopLoopingSample(uint loopId) => true;
        }

        private sealed class PreparedTrackingPlayback :
            LegacyTrackingPlayback,
            IPreparedAudioSamplePlayback
        {
            public int PreparedTriggerCount { get; private set; }

            public int PreparedLoopCount { get; private set; }

            public bool TryGetPreparedSampleHandle(
                string samplePath,
                out PreparedAudioSampleHandle handle)
            {
                handle = new PreparedAudioSampleHandle(new object(), 1, 0);
                return true;
            }

            public bool TriggerPreparedSample(
                PreparedAudioSampleHandle handle,
                double gain)
            {
                PreparedTriggerCount++;
                LastGain = gain;
                return true;
            }

            public uint StartLoopingPreparedSample(
                PreparedAudioSampleHandle handle,
                double gain)
            {
                PreparedLoopCount++;
                LastGain = gain;
                return 42;
            }
        }

        private sealed class TimestampedTrackingPlayback :
            LegacyTrackingPlayback,
            ITimestampedPreparedAudioSamplePlayback
        {
            public int TimestampedTriggerCount { get; private set; }

            public int RegularPreparedTriggerCount { get; private set; }

            public bool SupportsSampleTriggerTelemetry => true;

            public AudioSampleTriggerTelemetryStatus
                SampleTriggerTelemetryStatus => default;

            public bool TryGetPreparedSampleHandle(
                string samplePath,
                out PreparedAudioSampleHandle handle)
            {
                handle = new PreparedAudioSampleHandle(new object(), 1, 0);
                return true;
            }

            public bool TriggerPreparedSample(
                PreparedAudioSampleHandle handle,
                double gain)
            {
                RegularPreparedTriggerCount++;
                LastGain = gain;
                return true;
            }

            public bool TriggerPreparedSample(
                PreparedAudioSampleHandle handle,
                double gain,
                long captureTimestamp,
                long timestampFrequency,
                out ulong traceId)
            {
                TimestampedTriggerCount++;
                LastGain = gain;
                traceId = (ulong)TimestampedTriggerCount;
                return true;
            }

            public uint StartLoopingPreparedSample(
                PreparedAudioSampleHandle handle,
                double gain) => 1;

            public bool TryDequeueSampleTriggerTelemetry(
                out AudioSampleTriggerTelemetry telemetry)
            {
                telemetry = default;
                return false;
            }
        }
    }
}
