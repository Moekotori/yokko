using System;
using System.Threading;
using osu.Framework.Platform;
using osu.Framework.Threading;

namespace Yokko.Game.Presentation;

internal static class YokkoLatencyThreadPolicy
{
    // The native WASAPI callback uses Pro Audio MMCSS and remains above these
    // threads. Input and update receive latency protection while draw remains
    // normal so high frame rates cannot starve decoding or ordinary OS work.
    internal const ThreadPriority InputPriority =
        ThreadPriority.AboveNormal;
    internal const ThreadPriority UpdatePriority =
        ThreadPriority.AboveNormal;
    internal const ThreadPriority DrawPriority =
        ThreadPriority.Normal;

    internal static void Apply(GameHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        if (!OperatingSystem.IsWindows())
            return;

        schedule(host.InputThread, InputPriority);
        schedule(host.UpdateThread, UpdatePriority);
        schedule(host.DrawThread, DrawPriority);
    }

    private static void schedule(
        GameThread thread,
        ThreadPriority priority)
    {
        thread?.Scheduler.Add(() =>
        {
            try
            {
                Thread.CurrentThread.Priority = priority;
            }
            catch
            {
                // Thread priority is an optional latency optimisation. A host
                // or platform policy must never prevent the game from running.
            }
        });
    }
}
