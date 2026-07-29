using System;

namespace Yokko.Game.Presentation;

internal enum FramePacingHealth
{
    Stable,
    Warning,
    Critical,
}

/// <summary>
/// Holds the runtime-only AUTO frame-rate decision. A downgrade is staged
/// while gameplay is active and applied after the whole gameplay session,
/// including retries, exits.
/// </summary>
internal sealed class YokkoFrameRateAdaptation
{
    internal const int RequiredCriticalObservations = 4;

    private int criticalObservationCount;
    private int activeSessionCount;
    private YokkoFrameLimit? pendingBaseLimit;
    private YokkoFrameLimit? pendingLimit;

    internal event Action EffectiveProfileChanged;

    internal bool IsEnabled { get; private set; }
    internal bool IsSessionActive => activeSessionCount > 0;
    internal int Revision { get; private set; }
    internal bool HasAdapted =>
        IsEnabled
        && EffectiveLimit != BaseLimit;
    internal YokkoFrameLimit BaseLimit { get; private set; } =
        YokkoFrameLimit.Limit4x;
    internal YokkoFrameLimit EffectiveLimit { get; private set; } =
        YokkoFrameLimit.Limit4x;
    internal YokkoFrameLimit? PendingLimit => pendingLimit;

    internal void Enable(
        YokkoFrameLimit baseLimit,
        bool reset,
        bool deferResetWhileSession = false)
    {
        if (baseLimit is not (
                YokkoFrameLimit.Limit4x
                or YokkoFrameLimit.Limit8x))
        {
            throw new ArgumentOutOfRangeException(
                nameof(baseLimit),
                "AUTO must resolve to a bounded high-rate mode.");
        }

        bool wasEnabled = IsEnabled;
        IsEnabled = true;
        if (!reset)
        {
            BaseLimit = baseLimit;
            return;
        }

        Revision++;
        if (deferResetWhileSession && IsSessionActive)
        {
            pendingBaseLimit = baseLimit;
            pendingLimit = null;
            criticalObservationCount = 0;
            return;
        }

        bool changed = !wasEnabled
                       || EffectiveLimit != baseLimit;
        BaseLimit = baseLimit;
        EffectiveLimit = baseLimit;
        pendingBaseLimit = null;
        pendingLimit = null;
        criticalObservationCount = 0;
        if (changed)
            EffectiveProfileChanged?.Invoke();
    }

    internal void Disable()
    {
        Revision++;
        IsEnabled = false;
        pendingBaseLimit = null;
        pendingLimit = null;
        criticalObservationCount = 0;
    }

    internal void Observe(FramePacingHealth health)
    {
        if (!IsEnabled
            || pendingLimit.HasValue
            || EffectiveLimit != BaseLimit)
            return;

        if (health != FramePacingHealth.Critical)
        {
            criticalObservationCount = 0;
            return;
        }

        criticalObservationCount++;
        if (criticalObservationCount
            < RequiredCriticalObservations)
        {
            return;
        }

        criticalObservationCount = 0;
        YokkoFrameLimit requested =
            downgrade(EffectiveLimit);
        if (requested == EffectiveLimit)
            return;

        if (IsSessionActive)
        {
            pendingLimit = requested;
            return;
        }

        EffectiveLimit = requested;
        Revision++;
        EffectiveProfileChanged?.Invoke();
    }

    internal void BeginSession() => activeSessionCount++;

    internal void EndSession()
    {
        if (activeSessionCount == 0)
            return;

        activeSessionCount--;
        if (activeSessionCount > 0 || !IsEnabled)
        {
            return;
        }

        if (pendingBaseLimit.HasValue)
        {
            bool changed =
                BaseLimit != pendingBaseLimit.Value
                || EffectiveLimit != pendingBaseLimit.Value;
            BaseLimit = pendingBaseLimit.Value;
            EffectiveLimit = pendingBaseLimit.Value;
            pendingBaseLimit = null;
            pendingLimit = null;
            criticalObservationCount = 0;
            Revision++;
            if (changed)
                EffectiveProfileChanged?.Invoke();
            return;
        }

        if (!pendingLimit.HasValue)
            return;

        EffectiveLimit = pendingLimit.Value;
        pendingLimit = null;
        Revision++;
        EffectiveProfileChanged?.Invoke();
    }

    private static YokkoFrameLimit downgrade(
        YokkoFrameLimit limit) => limit switch
        {
            YokkoFrameLimit.Limit8x =>
                YokkoFrameLimit.Limit4x,
            YokkoFrameLimit.Limit4x =>
                YokkoFrameLimit.Limit2x,
            _ => limit,
        };
}

internal static class FramePacingHealthEvaluator
{
    internal static FramePacingHealth Evaluate(
        FrameTimingSnapshot draw,
        double drawTargetFrameTime,
        FrameTimingSnapshot update,
        double updateTargetFrameTime)
    {
        double worstP99Ratio = Math.Max(
            safeRatio(
                draw.P99FrameTimeMilliseconds,
                drawTargetFrameTime),
            safeRatio(
                update.P99FrameTimeMilliseconds,
                updateTargetFrameTime));
        double worstMissRatio = Math.Max(
            draw.BudgetMissRatio,
            update.BudgetMissRatio);

        if (worstP99Ratio >= 2
            || worstMissRatio >= 0.02)
            return FramePacingHealth.Critical;

        if (worstP99Ratio >= 1.25
            || draw.BudgetMissCount > 0
            || update.BudgetMissCount > 0)
            return FramePacingHealth.Warning;

        return FramePacingHealth.Stable;
    }

    private static double safeRatio(
        double value,
        double target) =>
        double.IsFinite(target) && target > 0
            ? value / target
            : 0;
}
