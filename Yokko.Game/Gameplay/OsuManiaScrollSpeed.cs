using System;

namespace Yokko.Game.Gameplay;

/// <summary>
/// osu!mania-compatible scroll-speed semantics shared by settings and gameplay.
/// </summary>
internal static class OsuManiaScrollSpeed
{
    public const double Minimum = 1;
    public const double Maximum = 40;
    public const double Default = 8;
    public const double SpeedStep = 0.1;
    public const double SettingsPrecision = 0.000001;
    public const double ShortcutStep = 1;
    public const double ScrollTimeStepMilliseconds = 1;

    private const double maximumTimeRangeMilliseconds = 11485;
    private const double defaultLegacyHitPosition = 402;
    private const double minimumLegacyHitPosition = 240;
    private const double maximumLegacyHitPosition = 480;

    /// <remarks>
    /// Matches ppy/osu's ManiaRulesetConfigManager and DrawableManiaRuleset at
    /// commit 82056109080329928c572db8851f4bcf8e04362b (MIT).
    /// </remarks>
    public static double ComputeScrollTime(double scrollSpeed) =>
        maximumTimeRangeMilliseconds / ClampPrecise(scrollSpeed);

    public static double ComputeScrollSpeed(double scrollTimeMilliseconds) =>
        ClampPrecise(
            maximumTimeRangeMilliseconds
            / Math.Clamp(
                scrollTimeMilliseconds,
                ComputeScrollTime(Maximum),
                ComputeScrollTime(Minimum)));

    /// <summary>
    /// Computes the effective time range for a legacy osu!mania skin.
    /// Scaling with HitPosition keeps the visible scroll velocity stable when
    /// a skin moves the receptor away from its default position.
    /// </summary>
    public static double ComputeScrollTime(
        double scrollSpeed,
        double legacyHitPosition) =>
        ComputeScrollTime(scrollSpeed)
        * Math.Clamp(
            legacyHitPosition,
            minimumLegacyHitPosition,
            maximumLegacyHitPosition)
        / defaultLegacyHitPosition;

    public static double Clamp(double scrollSpeed) =>
        Math.Clamp(
            Math.Round(scrollSpeed / SpeedStep) * SpeedStep,
            Minimum,
            Maximum);

    public static double ClampPrecise(double scrollSpeed) =>
        Math.Clamp(
            Math.Round(scrollSpeed / SettingsPrecision) * SettingsPrecision,
            Minimum,
            Maximum);

    public static double Adjust(double scrollSpeed, double amount) =>
        Clamp(scrollSpeed + amount);

    public static double SnapToWholeStep(double scrollSpeed) =>
        Clamp(Math.Round(scrollSpeed, MidpointRounding.AwayFromZero));

    public static double AdjustWholeStep(double scrollSpeed, double amount) =>
        Clamp(SnapToWholeStep(scrollSpeed) + amount);

    public static double AdjustScrollTime(
        double scrollSpeed,
        double deltaMilliseconds)
    {
        double currentMilliseconds = Math.Round(
            ComputeScrollTime(scrollSpeed));
        return ComputeScrollSpeed(
            currentMilliseconds + deltaMilliseconds);
    }
}
