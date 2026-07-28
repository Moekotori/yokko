using osu.Framework.Bindables;

namespace Yokko.Game.Skinning.OsuMania;

/// <summary>
/// User-facing skin preferences. The selected value is a managed skin id,
/// never an arbitrary filesystem path.
/// </summary>
internal sealed class YokkoSkinSettings
{
    public readonly Bindable<string> SelectedSkinId = new(string.Empty);
}
