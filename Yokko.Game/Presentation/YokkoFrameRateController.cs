using System;
using osu.Framework.Bindables;
using osu.Framework.Configuration;
using osu.Framework.Logging;
using osu.Framework.Platform;

namespace Yokko.Game.Presentation;

internal readonly record struct YokkoFrameRates(
    double MaximumDrawHz,
    double MaximumUpdateHz);

internal static class YokkoFrameRateLimits
{
    // AUTO keeps update delivery near the framework's 1000 Hz input ceiling
    // without forcing every display and GPU to render at 1000 FPS.
    internal const YokkoFrameLimit LowLatencyDefault =
        YokkoFrameLimit.Auto;

    // osu!framework harvests input at 1000 Hz and applies the same ceiling
    // to draw and update work. Going beyond it adds CPU/GPU pressure without
    // improving the timestamp-based gameplay clock.
    internal const double MaximumSaneRate = 1000;

    public static YokkoFrameLimit Resolve(
        YokkoFrameLimit limit,
        float refreshRate)
    {
        if (limit != YokkoFrameLimit.Auto)
            return limit;

        double safeRefreshRate = Math.Max(1, MathF.Round(refreshRate));

        // 8x gives 60-120 Hz displays 960-1000 Hz update delivery. Above
        // 120 Hz, 4x already reaches the update ceiling with less draw load.
        return safeRefreshRate <= 120
            ? YokkoFrameLimit.Limit8x
            : YokkoFrameLimit.Limit4x;
    }

    public static YokkoFrameRates Calculate(
        YokkoFrameLimit limit,
        float refreshRate)
    {
        double safeRefreshRate = Math.Max(1, MathF.Round(refreshRate));
        limit = Resolve(limit, refreshRate);

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
            // MAX is deliberately non-adaptive: keep both loops at the
            // framework's full 1000 Hz ceiling regardless of display health.
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
        YokkoFrameLimit limit,
        float refreshRate = 60) => Resolve(limit, refreshRate) switch
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
    private readonly IBindable<DisplayMode> currentDisplayMode;
    private readonly YokkoFrameRateAdaptation adaptation;
    private bool applyingFrameLimit;

    public YokkoFrameRateController(
        FrameworkConfigManager frameworkConfig,
        Bindable<YokkoFrameLimit> frameLimit,
        IBindable<DisplayMode> currentDisplayMode,
        YokkoFrameRateAdaptation adaptation)
    {
        this.frameLimit = frameLimit;
        this.currentDisplayMode = currentDisplayMode;
        this.adaptation = adaptation;
        frameworkFrameSync = frameworkConfig.GetBindable<FrameSync>(
            FrameworkSetting.FrameSync);

        adaptation.EffectiveProfileChanged +=
            onAdaptedProfileChanged;
        frameLimit.BindValueChanged(onFrameLimitChanged, true);
        frameworkFrameSync.BindValueChanged(onFrameworkFrameSyncChanged);
        currentDisplayMode.BindValueChanged(onCurrentDisplayModeChanged);
    }

    private void onFrameLimitChanged(
        ValueChangedEvent<YokkoFrameLimit> change)
    {
        if (change.NewValue == YokkoFrameLimit.Auto)
        {
            configureAuto(
                reset: true,
                deferResetWhileSession: false);
            applyAdaptedLimit();
            return;
        }

        adaptation.Disable();
        applyFrameLimit(change.NewValue);
    }

    private void onCurrentDisplayModeChanged(
        ValueChangedEvent<DisplayMode> change)
    {
        if (frameLimit.Value == YokkoFrameLimit.Auto)
        {
            configureAuto(
                reset: true,
                deferResetWhileSession: true);
            applyAdaptedLimit();
        }
    }

    private void applyFrameLimit(YokkoFrameLimit limit)
    {
        float refreshRate = currentDisplayMode.Value.RefreshRate;
        FrameSync requested =
            YokkoFrameRateLimits.ToFrameworkFrameSync(limit, refreshRate);
        if (frameworkFrameSync.Value == requested)
            return;

        applyingFrameLimit = true;
        try
        {
            frameworkFrameSync.Value = requested;
        }
        finally
        {
            applyingFrameLimit = false;
        }
    }

    private void configureAuto(
        bool reset,
        bool deferResetWhileSession)
    {
        adaptation.Enable(
            YokkoFrameRateLimits.Resolve(
                YokkoFrameLimit.Auto,
                currentDisplayMode.Value.RefreshRate),
            reset,
            deferResetWhileSession);
    }

    private void onAdaptedProfileChanged()
    {
        if (frameLimit.Value != YokkoFrameLimit.Auto)
            return;

        applyAdaptedLimit();
        if (adaptation.HasAdapted)
        {
            Logger.Log(
                $"AUTO frame pacing reduced {adaptation.BaseLimit} "
                + $"to {adaptation.EffectiveLimit} after sustained "
                + "critical frame timing.",
                LoggingTarget.Performance,
                LogLevel.Important);
        }
    }

    private void applyAdaptedLimit() =>
        applyFrameLimit(adaptation.EffectiveLimit);

    private void onFrameworkFrameSyncChanged(
        ValueChangedEvent<FrameSync> change)
    {
        if (applyingFrameLimit)
            return;

        YokkoFrameLimit requested =
            YokkoFrameRateLimits.FromFrameworkFrameSync(change.NewValue);
        if (frameLimit.Value != requested)
            frameLimit.Value = requested;
    }

    public void Dispose()
    {
        frameLimit.ValueChanged -= onFrameLimitChanged;
        frameworkFrameSync.ValueChanged -= onFrameworkFrameSyncChanged;
        currentDisplayMode.ValueChanged -= onCurrentDisplayModeChanged;
        adaptation.EffectiveProfileChanged -=
            onAdaptedProfileChanged;
    }
}
