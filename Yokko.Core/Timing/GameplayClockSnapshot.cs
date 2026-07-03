namespace Yokko.Core.Timing;

public readonly record struct GameplayClockSnapshot(
    double AudioTimeMilliseconds,
    double DeviceLatencyMilliseconds,
    double UserOffsetMilliseconds,
    bool IsRunning)
{
    public double JudgementTimeMilliseconds => AudioTimeMilliseconds - DeviceLatencyMilliseconds - UserOffsetMilliseconds;
}
