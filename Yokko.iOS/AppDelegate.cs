using osu.Framework.iOS;
using Yokko.Game;

namespace Yokko.iOS
{
    /// <inheritdoc />
    public class AppDelegate : GameApplicationDelegate
    {
        /// <inheritdoc />
        protected override osu.Framework.Game CreateGame() => new YokkoGame();
    }
}
