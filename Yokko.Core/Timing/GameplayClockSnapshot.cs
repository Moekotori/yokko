namespace Yokko.Core.Timing;

public readonly record struct GameplayClockSnapshot(
    double AudioTimeMilliseconds,
    double DeviceLatencyMilliseconds,
    double UserOffsetMilliseconds,
    bool IsRunning)
{
    /// <summary>
    /// Gameplay time at the audio position already presented by the device.
    /// Device latency remains diagnostic metadata and must not be subtracted
    /// again from a presented-position clock.
    /// </summary>
    public double JudgementTimeMilliseconds =>
        AudioTimeMilliseconds + UserOffsetMilliseconds;
}
