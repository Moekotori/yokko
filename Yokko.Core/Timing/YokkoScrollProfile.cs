namespace Yokko.Core.Timing;

/// <summary>
/// A named visual timing profile used by Quaver timing groups.
/// </summary>
public sealed record YokkoScrollProfile(
    double InitialScrollVelocity,
    IReadOnlyList<YokkoScrollVelocity> ScrollVelocities,
    IReadOnlyList<YokkoScrollSpeedFactor> ScrollSpeedFactors);
