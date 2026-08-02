using System;
using System.IO;
using System.Text.Json;
using NUnit.Framework;
using Yokko.Game.Presentation;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public class YokkoUiThemeFileTest
{
    [Test]
    public void PartialDocumentOverlaysDefaultTheme()
    {
        YokkoUiThemeFileResult result = YokkoUiThemeFile.Parse(
            """
            {
              "schemaVersion": 1,
              "name": "Test overlay",
              "colours": {
                "dark": {
                  "surface": "#224466CC",
                  "cyan": "#FF44AA"
                }
              },
              "metrics": {
                "cardCornerRadius": 18
              }
            }
            """);

        Assert.That(result.Name, Is.EqualTo("Test overlay"));
        Assert.That(
            result.Theme.Colours.Dark.Surface.R,
            Is.EqualTo(0x22 / 255f).Within(0.0001f));
        Assert.That(
            result.Theme.Colours.Dark.Surface.A,
            Is.EqualTo(0xCC / 255f).Within(0.0001f));
        Assert.That(result.Theme.Metrics.CardCornerRadius, Is.EqualTo(18));
        Assert.That(
            result.Theme.Colours.Dark.Background,
            Is.EqualTo(YokkoUiTheme.Default.Colours.Dark.Background));
    }

    [Test]
    public void InvalidColourIsRejected()
    {
        Assert.Throws<InvalidDataException>(() => YokkoUiThemeFile.Parse(
            """
            {
              "schemaVersion": 1,
              "colours": { "dark": { "surface": "blue" } }
            }
            """));
    }

    [Test]
    public void UnknownTokenIsRejected()
    {
        Assert.Throws<JsonException>(() => YokkoUiThemeFile.Parse(
            """
            {
              "schemaVersion": 1,
              "metrics": { "mysteryRadius": 42 }
            }
            """));
    }

    [Test]
    public void UndefinedNumericEasingIsRejected()
    {
        Assert.Throws<InvalidDataException>(() => YokkoUiThemeFile.Parse(
            """
            {
              "schemaVersion": 1,
              "motion": { "hoverEasing": "999" }
            }
            """));
    }

    [Test]
    public void InvalidThemeDoesNotReplaceCurrentTheme()
    {
        var store = new YokkoUiThemeStore();
        YokkoUiTheme invalid = YokkoUiTheme.Default with
        {
            Colours = null,
        };

        Assert.Throws<ArgumentNullException>(() => store.Apply(invalid));
        Assert.That(store.Current.Value, Is.SameAs(YokkoUiTheme.Default));
    }

    [Test]
    public void IncompleteThemeFontIsRejected()
    {
        Assert.Throws<ArgumentException>(() => YokkoUiThemeFile.Parse(
            """
            {
              "schemaVersion": 1,
              "typography": { "primaryFont": "Roboto" }
            }
            """));
    }
}
