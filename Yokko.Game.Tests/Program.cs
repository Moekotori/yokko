using osu.Framework;
using osu.Framework.Platform;

namespace Yokko.Game.Tests
{
    public static class Program
    {
        public static void Main()
        {
            using (GameHost host = Host.GetSuitableDesktopHost("visual-tests"))
            using (var game = new YokkoTestBrowser())
                host.Run(game);
        }
    }
}
