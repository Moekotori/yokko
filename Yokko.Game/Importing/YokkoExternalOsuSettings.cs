using osu.Framework.Bindables;

namespace Yokko.Game.Importing;

internal sealed class YokkoExternalOsuSettings
{
    /// <summary>
    /// Absolute path to an osu!stable Songs directory. Empty disables the
    /// external read-only library without deleting its Yokko-owned cache.
    /// </summary>
    public readonly Bindable<string> SongsPath = new(string.Empty);
}
