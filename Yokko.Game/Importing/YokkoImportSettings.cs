using osu.Framework.Bindables;

namespace Yokko.Game.Importing;

/// <summary>
/// Shared import preferences. Importers remain stateless and receive the
/// effective values through <see cref="Yokko.Import.ChartImportRequest"/>.
/// </summary>
public sealed class YokkoImportSettings
{
    public readonly BindableBool PreferKeysounds = new(true);
    public readonly BindableBool PreferSscSimfiles = new(true);
    public readonly BindableBool EnableBmsScratch = new(false);
    public readonly BindableBool ShowCompatibilityWarnings = new(true);
    public readonly BindableBool WatchFolderEnabled = new(false);
    public readonly Bindable<string> WatchFolderPath = new(string.Empty);
}
