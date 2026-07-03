using osu.Framework.Platform;
using osu.Framework;
using Yokko.Game;

namespace Yokko.Desktop
{
    public static class Program
    {
        public static void Main()
        {
            using (GameHost host = Host.GetSuitableDesktopHost(@"Yokko"))
            using (osu.Framework.Game game = new YokkoGame())
                host.Run(game);
        }
    }
}
