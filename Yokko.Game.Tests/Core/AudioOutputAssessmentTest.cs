using NUnit.Framework;
using Yokko.Audio;
using Yokko.Game.Audio;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class AudioOutputAssessmentTest
{
    [TestCase(
        AudioBackendKind.SharedWasapi,
        AudioBackendKind.SharedWasapi,
        23.49,
        (int)AudioOutputAssessmentKind.HighSharedLatency)]
    [TestCase(
        AudioBackendKind.WasapiExclusive,
        AudioBackendKind.SharedWasapi,
        23.49,
        (int)AudioOutputAssessmentKind.ExclusiveFallback)]
    [TestCase(
        AudioBackendKind.WasapiExclusive,
        AudioBackendKind.WasapiExclusive,
        5,
        (int)AudioOutputAssessmentKind.Verified)]
    [TestCase(
        AudioBackendKind.Asio,
        AudioBackendKind.Asio,
        20,
        (int)AudioOutputAssessmentKind.HighLatency)]
    public void ClassifiesMeasuredOutput(
        AudioBackendKind requested,
        AudioBackendKind active,
        double latencyMilliseconds,
        int expected)
    {
        Assert.That(
            AudioOutputAssessment.Assess(
                requested,
                createStatus(active, latencyMilliseconds)),
            Is.EqualTo((AudioOutputAssessmentKind)expected));
    }

    private static AudioEngineStatus createStatus(
        AudioBackendKind active,
        double latencyMilliseconds) => new(
        active,
        "Test device",
        48000,
        256,
        latencyMilliseconds,
        active != AudioBackendKind.SharedWasapi,
        true,
        false,
        false,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0);
}
