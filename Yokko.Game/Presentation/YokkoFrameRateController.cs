using System;
using osu.Framework.Bindables;
using osu.Framework.Configuration;

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

        if (limit == YokkoFrameLimit.VSync)
        {
            return new YokkoFrameRates(
                Math.Min(safeRefreshRate, MaximumSaneRate),
                Math.Min(safeRefreshRate * 4, MaximumSaneRate));
        }

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

    public static FrameSync ToFrameworkFrameSync(
        YokkoFrameLimit limit) => limit switch
        {
            YokkoFrameLimit.VSync => FrameSync.VSync,
            YokkoFrameLimit.Limit4x => FrameSync.Limit4x,
            YokkoFrameLimit.Limit8x => FrameSync.Limit8x,
            YokkoFrameLimit.Unlimited => FrameSync.Unlimited,
            _ => FrameSync.Limit2x,
        };

    public static YokkoFrameLimit FromFrameworkFrameSync(
        FrameSync mode) => mode switch
        {
            FrameSync.VSync => YokkoFrameLimit.VSync,
            FrameSync.Limit4x => YokkoFrameLimit.Limit4x,
            FrameSync.Limit8x => YokkoFrameLimit.Limit8x,
            FrameSync.Unlimited => YokkoFrameLimit.Unlimited,
            _ => YokkoFrameLimit.Limit2x,
        };
}

internal sealed class YokkoFrameRateController : IDisposable
{
    private readonly Bindable<YokkoFrameLimit> frameLimit;
    private readonly Bindable<FrameSync> frameworkFrameSync;

    public YokkoFrameRateController(
        FrameworkConfigManager frameworkConfig,
        Bindable<YokkoFrameLimit> frameLimit)
    {
        this.frameLimit = frameLimit;
        frameworkFrameSync = frameworkConfig.GetBindable<FrameSync>(
            FrameworkSetting.FrameSync);

        frameLimit.BindValueChanged(onFrameLimitChanged, true);
        frameworkFrameSync.BindValueChanged(onFrameworkFrameSyncChanged);
    }

    private void onFrameLimitChanged(
        ValueChangedEvent<YokkoFrameLimit> change)
    {
        FrameSync requested =
            YokkoFrameRateLimits.ToFrameworkFrameSync(change.NewValue);
        if (frameworkFrameSync.Value != requested)
            frameworkFrameSync.Value = requested;
    }

    private void onFrameworkFrameSyncChanged(
        ValueChangedEvent<FrameSync> change)
    {
        YokkoFrameLimit requested =
            YokkoFrameRateLimits.FromFrameworkFrameSync(change.NewValue);
        if (frameLimit.Value != requested)
            frameLimit.Value = requested;
    }

    public void Dispose()
    {
        frameLimit.ValueChanged -= onFrameLimitChanged;
        frameworkFrameSync.ValueChanged -= onFrameworkFrameSyncChanged;
    }
}
