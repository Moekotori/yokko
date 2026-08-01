using System;
using System.IO;
using System.Threading;
using NUnit.Framework;
using Yokko.Game.Presentation;
using Yokko.Game.Tests.Development;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public class YokkoThemeFileHotReloadTest
{
    [Test]
    public void ReloadsValidFileAndKeepsLastThemeOnError()
    {
        string directory = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            $"theme-reload-{Guid.NewGuid():N}");
        string path = Path.Combine(directory, "theme.json");
        Directory.CreateDirectory(directory);

        try
        {
            File.WriteAllText(path, theme("First", "#224466"));
            var store = new YokkoUiThemeStore();
            using var watcher = new YokkoThemeFileHotReload(
                path,
                store,
                action => action());

            Assert.That(
                SpinWait.SpinUntil(
                    () => store.ActiveName.Value == "First",
                    TimeSpan.FromSeconds(5)),
                Is.True,
                store.LastError.Value);

            File.WriteAllText(path, theme("Second", "#884466"));
            Assert.That(
                SpinWait.SpinUntil(
                    () => store.ActiveName.Value == "Second",
                    TimeSpan.FromSeconds(5)),
                Is.True,
                store.LastError.Value);

            File.WriteAllText(path, theme("Broken", "not-a-colour"));
            Assert.That(
                SpinWait.SpinUntil(
                    () => !string.IsNullOrWhiteSpace(store.LastError.Value),
                    TimeSpan.FromSeconds(5)),
                Is.True);
            Assert.That(store.ActiveName.Value, Is.EqualTo("Second"));

            string replacementPath = path + ".tmp";
            File.WriteAllText(
                replacementPath,
                theme("Third", "#3388AA"));
            File.Move(replacementPath, path, true);
            Assert.That(
                SpinWait.SpinUntil(
                    () => store.ActiveName.Value == "Third",
                    TimeSpan.FromSeconds(5)),
                Is.True,
                store.LastError.Value);
            Assert.That(store.LastError.Value, Is.Empty);
            Assert.That(
                store.Current.Value.Colours.Dark.Surface,
                Is.Not.EqualTo(YokkoUiTheme.Default.Colours.Dark.Surface));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static string theme(string name, string surface) =>
        $$"""
        {
          "schemaVersion": 1,
          "name": "{{name}}",
          "colours": {
            "dark": { "surface": "{{surface}}" }
          }
        }
        """;
}
