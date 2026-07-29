using System;
using osu.Framework.Bindables;
using osu.Framework.Platform;

namespace Yokko.Game.Presentation;

internal readonly record struct YokkoFrameRates(
    double MaximumDrawHz,
    double MaximumUpdateHz);

internal static class YokkoFrameRateLimits
{
    // osu!framework harvests input at 1000 Hz and applies the same ceiling
    // to draw and update work. Going beyond it adds CPU/GPU pressure without
    // improving the timestamp-based gameplay clock.
    internal const double MaximumSaneRate = 1000;

    public static YokkoFrameRates Calculate(
        YokkoFrameLimit limit,
        float refreshRate)
    {
        double safeRefreshRate = Math.Max(1, MathF.Round(refreshRate));
        double maximumDrawHz = safeRefreshRate;
        double maximumUpdateHz = safeRefreshRate * 2;
        int multiplier = limit switch
        {
            YokkoFrameLimit.Limit2x => 2,
            YokkoFrameLimit.Limit4x => 4,
            YokkoFrameLimit.Limit8x => 8,
            _ => 1,
        };

        if (limit == YokkoFrameLimit.Unlimited)
        {
            maximumDrawHz = MaximumSaneRate;
            maximumUpdateHz = MaximumSaneRate;
        }
        else
        {
            maximumDrawHz *= multiplier;
            maximumUpdateHz *= multiplier;
        }

        return new YokkoFrameRates(
            Math.Min(maximumDrawHz, MaximumSaneRate),
            Math.Min(maximumUpdateHz, MaximumSaneRate));
    }
}

internal sealed class YokkoFrameRateController : IDisposable
{
    private readonly GameHost host;
    private readonly Bindable<YokkoFrameLimit> frameLimit;
    private readonly IBindable<DisplayMode> displayMode;

    public YokkoFrameRateController(
        GameHost host,
        Bindable<YokkoFrameLimit> frameLimit,
        IBindable<DisplayMode> displayMode)
    {
        this.host = host;
        this.frameLimit = frameLimit;
        this.displayMode = displayMode;

        frameLimit.BindValueChanged(onFrameLimitChanged, true);
        displayMode.BindValueChanged(onDisplayModeChanged);

        // GameHost triggers its framework FrameSync setting once more after
        // loading the game. Reapply on the first update so the explicit Yokko
        // limit remains the final runtime value on startup as well.
        host.UpdateThread.Scheduler.Add(apply);
    }

    private void onFrameLimitChanged(ValueChangedEvent<YokkoFrameLimit> _) =>
        apply();

    private void onDisplayModeChanged(ValueChangedEvent<DisplayMode> _) =>
        apply();

    private void apply()
    {
        float refreshRate = displayMode.Value.RefreshRate > 0
            ? displayMode.Value.RefreshRate
            : 60;
        YokkoFrameRates rates = YokkoFrameRateLimits.Calculate(
            frameLimit.Value,
            refreshRate);

        host.MaximumDrawHz = rates.MaximumDrawHz;
        host.MaximumUpdateHz = rates.MaximumUpdateHz;
    }

    public void Dispose()
    {
        frameLimit.ValueChanged -= onFrameLimitChanged;
        displayMode.ValueChanged -= onDisplayModeChanged;
    }
}
