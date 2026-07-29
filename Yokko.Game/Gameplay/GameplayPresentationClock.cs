namespace Yokko.Game.Gameplay;

internal static class GameplayPresentationClock
{
    /// <summary>
    /// Keeps note presentation on the same authoritative gameplay clock used
    /// by judgement. High-frequency draw scheduling reduces frame age without
    /// introducing a refresh-rate-dependent timing offset.
    /// </summary>
    internal static double EstimateVisualTime(
        double audioPresentationTimeMilliseconds,
        double refreshRate) =>
        audioPresentationTimeMilliseconds;
}
