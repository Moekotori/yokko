using NUnit.Framework;
using Yokko.Game.Screens.SongSelect;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public class SongSelectTextLayoutTest
{
    [TestCase("Waterfall")]
    [TestCase("THE EXTRAORDINARILY LONG TITLE OF A SONG PACK THAT MUST NEVER BREAK THE CARD")]
    [TestCase("这是一个特别特别长而且完全没有空格的中文歌曲标题用于验证布局不会溢出")]
    public void DetailsTitleNeverUsesMoreThanTwoLines(string title)
    {
        string[] lines = SongSelectTextLayout.TwoLines(title, 22);

        Assert.Multiple(() =>
        {
            Assert.That(lines, Has.Length.InRange(1, 2));
            Assert.That(lines, Has.All.Not.Empty);
            if (title.Length > 40)
                Assert.That(lines[^1], Does.EndWith("…"));
        });
    }

    [Test]
    public void SelectedDetailsUsesSecondLineBeforeTruncatingModerateTitle()
    {
        const string title =
            "Harmonic Bloom: Symphony of the Dreaming Petals";

        string[] lines = SongSelectScreen.LayoutDetailsTitle(title);

        Assert.Multiple(() =>
        {
            Assert.That(lines, Has.Length.EqualTo(2));
            Assert.That(string.Join(" ", lines), Is.EqualTo(title));
            Assert.That(lines[0], Is.EqualTo(
                "Harmonic Bloom: Symphony"));
            Assert.That(lines[1], Is.EqualTo(
                "of the Dreaming Petals"));
        });
    }
}
