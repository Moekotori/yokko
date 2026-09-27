using System;
using NUnit.Framework;
using Yokko.Core.Timing;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public class InputCalibrationSessionTest
{
    private static double beatTime(int beat) => InputCalibrationSession.LeadInMilliseconds
        + (InputCalibrationSession.WarmupBeats + beat) * InputCalibrationSession.BeatIntervalMilliseconds;

    [TestCase(42)]
    [TestCase(-37)]
    [TestCase(0)]
    public void StableTapsProduceAbsoluteOffsetWithCorrectGameplaySign(double bias)
    {
        var session = new InputCalibrationSession();
        for (int i = 0; i < 36; i++) session.TryRecordTap(beatTime(i) + bias + i % 3 - 1);
        CalibrationResult result = session.Analyse();
        Assert.That(result.CanApply, Is.True);
        Assert.That(result.SuggestedOffsetMilliseconds, Is.EqualTo(-bias));
        Assert.That(result.MedianMilliseconds + result.SuggestedOffsetMilliseconds, Is.EqualTo(0).Within(0.01));
    }

    [Test]
    public void WarmupDuplicateOutOfOrderAndNonFiniteInputsAreIgnored()
    {
        var session = new InputCalibrationSession();
        Assert.That(session.TryRecordTap(1500), Is.False);
        Assert.That(session.TryRecordTap(double.NaN), Is.False);
        Assert.That(session.TryRecordTap(double.PositiveInfinity), Is.False);
        Assert.That(session.TryRecordTap(beatTime(1) + 20), Is.True);
        Assert.That(session.TryRecordTap(beatTime(1) + 25), Is.False);
        Assert.That(session.TryRecordTap(beatTime(0) + 20), Is.False);
        Assert.That(session.TryRecordTap(22_000), Is.False);
        Assert.That(session.Analyse().CanApply, Is.False);
    }

    [Test]
    public void OccasionalOutliersDoNotShiftTheRecommendation()
    {
        var session = new InputCalibrationSession();
        for (int i = 0; i < 36; i++) session.TryRecordTap(beatTime(i) + (i % 9 == 0 ? 130 : 24));
        CalibrationResult result = session.Analyse();
        Assert.That(result.CanApply, Is.True);
        Assert.That(result.RejectedSamples, Is.EqualTo(4));
        Assert.That(result.SuggestedOffsetMilliseconds, Is.EqualTo(-24));
    }

    [Test]
    public void DriftingOrErraticTapsCannotBeApplied()
    {
        var drifting = new InputCalibrationSession();
        var erratic = new InputCalibrationSession();
        for (int i = 0; i < 36; i++)
        {
            drifting.TryRecordTap(beatTime(i) + i * 2);
            erratic.TryRecordTap(beatTime(i) + (i % 2 == 0 ? -60 : 60));
        }
        Assert.That(drifting.Analyse().Quality, Is.EqualTo(CalibrationQuality.Unstable));
        Assert.That(erratic.Analyse().Quality, Is.EqualTo(CalibrationQuality.Unstable));
    }

    [Test]
    public void TooFewSamplesAndOutOfRangeAreRejectedRatherThanClamped()
    {
        var sparse = new InputCalibrationSession();
        var excessive = new InputCalibrationSession();
        for (int i = 0; i < 36; i++)
        {
            if (i < 23) sparse.TryRecordTap(beatTime(i) + 20);
            excessive.TryRecordTap(beatTime(i) + 210);
        }
        Assert.That(sparse.Analyse().Quality, Is.EqualTo(CalibrationQuality.TooFewSamples));
        Assert.That(excessive.Analyse().Quality, Is.EqualTo(CalibrationQuality.OutOfRange));
    }
}
