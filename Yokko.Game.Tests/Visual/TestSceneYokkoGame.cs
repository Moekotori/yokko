using osu.Framework.Allocation;
using NUnit.Framework;
using osu.Framework.Logging;

namespace Yokko.Game.Tests.Visual
{
    [TestFixture]
    public partial class TestSceneYokkoGame : YokkoTestScene
    {
        private YokkoGame game;
        // Add visual tests to ensure correct behaviour of your game: https://github.com/ppy/osu-framework/wiki/Development-and-Testing
        // You can make changes to classes associated with the tests and they will recompile and update immediately.

        [BackgroundDependencyLoader]
        private void load()
        {
            AddGame(game = new YokkoGame());
        }

        [Test]
        public void TestDebugConsoleReceivesLiveLoggerEntries()
        {
            bool originalVisibility = false;
            string marker = null;

            AddStep("remember console setting", () =>
                originalVisibility = game.DebugConsoleVisible);
            AddStep("enable console", () =>
                game.SetDebugConsoleVisible(true));
            AddStep("write live marker", () =>
            {
                marker = $"visual-live-{System.Guid.NewGuid():N}";
                Logger.Log(
                    marker,
                    LoggingTarget.Runtime,
                    LogLevel.Important);
            });
            AddUntilStep(
                "marker appears without reading files",
                () => marker != null && game.DebugConsoleContains(marker));
            AddStep("restore console setting", () =>
                game.SetDebugConsoleVisible(originalVisibility));
        }
    }
}
