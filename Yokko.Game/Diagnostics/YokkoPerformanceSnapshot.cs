using System;
using System.Globalization;

namespace Yokko.Game.Diagnostics;

internal enum YokkoPerformanceHealth
{
    Stable,
    Warning,
    Critical,
}

internal readonly record struct YokkoPerformanceSnapshot(
    DateTimeOffset Timestamp,
    double DrawFramesPerSecond,
    double DrawFrameTimeMilliseconds,
    double DrawP95Milliseconds,
    double DrawP99Milliseconds,
    double DrawMaximumMilliseconds,
    int DrawBudgetMissCount,
    double DrawBudgetMissRatio,
    double UpdateFramesPerSecond,
    double UpdateFrameTimeMilliseconds,
    double UpdateP95Milliseconds,
    double UpdateP99Milliseconds,
    double UpdateMaximumMilliseconds,
    int UpdateBudgetMissCount,
    double UpdateBudgetMissRatio,
    double InputFramesPerSecond,
    double ProcessCpuPercent,
    long WorkingSetBytes,
    long ManagedMemoryBytes,
    int Generation0Collections,
    int Generation1Collections,
    int Generation2Collections,
    YokkoPerformanceHealth Health)
{
    public string ToDisplayText() => string.Create(
        CultureInfo.InvariantCulture,
        $"DRAW {DrawFramesPerSecond:0} fps  P99 {DrawP99Milliseconds:0.0} ms"
        + $"  |  UPDATE {UpdateFrameTimeMilliseconds:0.0} ms  P99 {UpdateP99Milliseconds:0.0} ms"
        + $"  |  CPU {ProcessCpuPercent:0.0}%  RAM {bytesToMegabytes(WorkingSetBytes):0} MB"
        + $"  GC {Generation0Collections}/{Generation1Collections}/{Generation2Collections}"
        + $"  |  {Health.ToString().ToUpperInvariant()}");

    public string ToLogDetails() => string.Create(
        CultureInfo.InvariantCulture,
        $"health={Health.ToString().ToLowerInvariant()}"
        + $" | draw-fps={DrawFramesPerSecond:0.###}"
        + $" | draw-frame={DrawFrameTimeMilliseconds:0.###}ms"
        + $" | draw-p95={DrawP95Milliseconds:0.###}ms"
        + $" | draw-p99={DrawP99Milliseconds:0.###}ms"
        + $" | draw-max={DrawMaximumMilliseconds:0.###}ms"
        + $" | draw-misses={DrawBudgetMissCount}"
        + $" | draw-miss-ratio={DrawBudgetMissRatio:P3}"
        + $" | update-fps={UpdateFramesPerSecond:0.###}"
        + $" | update-frame={UpdateFrameTimeMilliseconds:0.###}ms"
        + $" | update-p95={UpdateP95Milliseconds:0.###}ms"
        + $" | update-p99={UpdateP99Milliseconds:0.###}ms"
        + $" | update-max={UpdateMaximumMilliseconds:0.###}ms"
        + $" | update-misses={UpdateBudgetMissCount}"
        + $" | update-miss-ratio={UpdateBudgetMissRatio:P3}"
        + $" | input-hz={InputFramesPerSecond:0.###}"
        + $" | cpu={ProcessCpuPercent:0.###}%"
        + $" | working-set={bytesToMegabytes(WorkingSetBytes):0.###}MB"
        + $" | managed={bytesToMegabytes(ManagedMemoryBytes):0.###}MB"
        + $" | gc={Generation0Collections}/{Generation1Collections}/{Generation2Collections}");

    private static double bytesToMegabytes(long bytes) =>
        bytes / (1024d * 1024d);
}
