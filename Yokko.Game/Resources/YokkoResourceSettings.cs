using osu.Framework.Bindables;

namespace Yokko.Game.Resources;

internal sealed class YokkoResourceSettings
{
    /// <summary>
    /// Empty uses Yokko's default storage location. A custom value is always
    /// stored as an absolute path.
    /// </summary>
    public readonly Bindable<string> RootPath = new(string.Empty);
}
