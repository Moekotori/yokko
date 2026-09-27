using System;
using osu.Framework;
using osu.Framework.Platform;

namespace Yokko.Game.Tests
{
    public static class Program
    {
        public static void Main()
        {
            using (GameHost host = Host.GetSuitableDesktopHost("visual-tests", new HostOptions
            {
                PortableInstallation = Environment.GetEnvironmentVariable("YOKKO_PORTABLE_TESTS") == "1",
            }))
                host.Run(new YokkoTestBrowser());
        }
    }
}
