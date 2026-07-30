using osu.Framework.Bindables;

namespace Yokko.Game.Skinning.OsuMania;

/// <summary>
/// User-facing skin preferences. The selected value is a managed skin id,
/// never an arbitrary filesystem path.
/// </summary>
internal sealed class YokkoSkinSettings
{
    public readonly Bindable<string> SelectedSkinId = new(string.Empty);

    /// <summary>
    /// osu!stable exposes the same choice in its options; when disabled the
    /// skin's comboburst-mania character is never shown during gameplay.
    /// </summary>
    public readonly BindableBool ShowComboBursts = new(true);
}
