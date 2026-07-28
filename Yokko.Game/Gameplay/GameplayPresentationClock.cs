using System;

namespace Yokko.Game.Gameplay;

internal static class GameplayPresentationClock
{
    private const double fallback_refresh_rate = 60;
    private const double maximum_visual_lead_milliseconds = 1000.0 / 30;

    /// <summary>
    /// Estimates where the audio presentation clock will be when the frame
    /// reaches the display. Half a refresh interval is the average wait from
    /// an uncoupled update to the next scanout; judgement remains on the
    /// unmodified audio clock.
    /// </summary>
    internal static double EstimateVisualTime(
        double audioPresentationTimeMilliseconds,
        double refreshRate)
    {
        double safeRefreshRate =
            double.IsFinite(refreshRate) && refreshRate > 0
                ? refreshRate
                : fallback_refresh_rate;
        double visualLead = Math.Min(
            500.0 / safeRefreshRate,
            maximum_visual_lead_milliseconds);
        return audioPresentationTimeMilliseconds + visualLead;
    }
}
