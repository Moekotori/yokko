using osu.Framework.Bindables;

namespace Yokko.Game.Skinning.OsuMania;

/// <summary>
/// User-facing skin preferences. The selected value is a managed skin id,
/// never an arbitrary filesystem path.
/// </summary>
internal sealed class YokkoSkinSettings
{
    public const double MinimumLongNoteCutAmount = 0;

    public const double MaximumLongNoteCutAmount = 2;

    public const double LongNoteCutAmountStep = 0.1;

    public const double DefaultLongNoteCutAmount = 0;

    public readonly Bindable<string> SelectedSkinId = new(string.Empty);

    /// <summary>
    /// osu!stable exposes the same choice in its options; when disabled the
    /// skin's comboburst-mania character is never shown during gameplay.
    /// </summary>
    public readonly BindableBool ShowComboBursts = new(true);

    /// <summary>
    /// Additional visual distance removed from the release end on top of the
    /// skin's own baked Percy/LN cut, measured in current note widths.
    /// Judgement and hold duration are never changed.
    /// </summary>
    public readonly Bindable<double> LongNoteCutAmount =
        new(DefaultLongNoteCutAmount);
}
