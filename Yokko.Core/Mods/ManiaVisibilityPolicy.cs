namespace Yokko.Core.Mods;

public readonly record struct ManiaVisibilityPolicy(
    ManiaVisibilityMode Mode,
    double Coverage,
    ManiaCoverDirection CoverDirection,
    double FlashlightSize);

/// <summary>
/// Resolves osu!mania visibility Mod defaults into UI-independent values.
/// Constants follow ppy/osu's ManiaModHidden, ManiaModCover and
/// ManiaModFlashlight at commit
/// 9f227ed28b6c8ba46dfea1f000f778d8b2827ad0 (MIT).
/// </summary>
public static class ManiaVisibilityPolicyResolver
{
    private const double referencePlayfieldHeight = 768;
    private const double hiddenMinimumCoverage = 160;
    private const double hiddenMaximumCoverage = 400;
    private const double hiddenCoveragePerCombo = 0.5;
    private const double defaultFlashlightSize = 50;

    public static ManiaVisibilityPolicy Resolve(
        ManiaModSet? mods,
        int combo)
    {
        mods ??= ManiaModSet.Empty;
        combo = Math.Max(0, combo);

        if (mods.Contains(ManiaModId.FadeIn))
        {
            return coverPolicy(
                ManiaVisibilityMode.FadeIn,
                dynamicHiddenCoverage(combo),
                ManiaCoverDirection.AlongScroll);
        }

        if (mods.Contains(ManiaModId.Hidden))
        {
            return coverPolicy(
                ManiaVisibilityMode.Hidden,
                dynamicHiddenCoverage(combo),
                ManiaCoverDirection.AgainstScroll);
        }

        if (mods.Contains(ManiaModId.Cover))
        {
            return coverPolicy(
                ManiaVisibilityMode.Cover,
                mods.CoverCoverage,
                mods.CoverDirection);
        }

        if (mods.Contains(ManiaModId.Flashlight))
        {
            double comboScale = !mods.FlashlightComboBasedSize
                ? 1
                : combo >= 200
                    ? 0.625
                    : combo >= 100
                        ? 0.8125
                        : 1;
            return new ManiaVisibilityPolicy(
                ManiaVisibilityMode.Flashlight,
                0,
                ManiaCoverDirection.AlongScroll,
                defaultFlashlightSize
                * mods.FlashlightSizeMultiplier
                * comboScale);
        }

        return new ManiaVisibilityPolicy(
            ManiaVisibilityMode.None,
            0,
            ManiaCoverDirection.AlongScroll,
            0);
    }

    private static ManiaVisibilityPolicy coverPolicy(
        ManiaVisibilityMode mode,
        double coverage,
        ManiaCoverDirection direction) =>
        new(mode, coverage, direction, 0);

    private static double dynamicHiddenCoverage(int combo) =>
        Math.Min(
            hiddenMaximumCoverage,
            hiddenMinimumCoverage + combo * hiddenCoveragePerCombo)
        / referencePlayfieldHeight;
}
