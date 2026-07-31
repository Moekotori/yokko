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
        public static void Main(string[] args)
        {
            using var crashReports = new CrashReportHandler(
                Assembly.GetExecutingAssembly());

            try
            {
                if (OperatingSystem.IsWindows())
                {
                    WindowsFileAssociationRegistrar.TryRegister(
                        Environment.ProcessPath);
                }

                using (GameHost host = Host.GetSuitableDesktopHost(@"Yokko"))
                {
                    host.Run(new YokkoGame(
                        new WindowsRawKeyboardTimestampBackend(),
                        gameStorage => crashReports.SetStoragePaths(
                            gameStorage.GetFullPath("crashes", true),
                            gameStorage.GetFullPath("logs", true)),
                        StartupFileArguments.Resolve(args)));
                }
            }
            catch (Exception exception)
            {
                crashReports.TryWrite(exception, "Desktop main loop");
                throw;
            }
        }
    }
}
