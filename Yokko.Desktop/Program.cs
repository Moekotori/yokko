using osu.Framework.Platform;
using System;
using System.Reflection;
using osu.Framework;
using Yokko.Desktop.Diagnostics;
using Yokko.Desktop.Input;
using Yokko.Game;

namespace Yokko.Desktop
{
    public static class Program
    {
        public static void Main()
        {
            using var crashReports = new CrashReportHandler(
                Assembly.GetExecutingAssembly());

            try
            {
                using (GameHost host = Host.GetSuitableDesktopHost(@"Yokko"))
                using (osu.Framework.Game game = new YokkoGame(
                           new WindowsRawKeyboardTimestampBackend(),
                           gameStorage => crashReports.SetStoragePaths(
                               gameStorage.GetFullPath("crashes", true),
                               gameStorage.GetFullPath("logs", true))))
                    host.Run(game);
            }
            catch (Exception exception)
            {
                crashReports.TryWrite(exception, "Desktop main loop");
                throw;
            }
        }
    }
}
