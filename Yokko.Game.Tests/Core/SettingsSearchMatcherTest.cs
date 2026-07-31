using NUnit.Framework;
using Yokko.Game.Screens.Settings;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public class SettingsSearchMatcherTest
{
    [TestCase("音频 设备", "音频", "音频 输出设备 扬声器", true)]
    [TestCase("FPS", "显示", "显示 帧率 FPS", true)]
    [TestCase("window-resolution", "Display", "window mode resolution", true)]
    [TestCase("音频 显示", "音频", "音频 输出设备", false)]
    public void SearchSupportsChineseAliasesAndMultipleTokens(
        string query,
        string title,
        string terms,
        bool expectedMatch)
    {
        int score = SettingsSearchMatcher.Score(query, title, terms);

        Assert.That(
            score != SettingsSearchMatcher.NoMatch,
            Is.EqualTo(expectedMatch));
    }

    [Test]
    public void TitleMatchOutranksOptionMatchOnAnotherPage()
    {
        int shortcutPage = SettingsSearchMatcher.Score(
            "快捷键",
            "Keyboard shortcuts 快捷键",
            "Keyboard shortcuts 快捷键 暂停 重试");
        int gameplayPage = SettingsSearchMatcher.Score(
            "快捷键",
            "Gameplay 游玩",
            "Gameplay 游玩 快捷键 暂停 重试");

        Assert.That(shortcutPage, Is.GreaterThan(gameplayPage));
    }

    [Test]
    public void EveryPageIndexesItsConcreteSettings()
    {
        Assert.That(
            SettingsSearchMatcher.Score(
                "帧率",
                SettingsPages.Get(SettingsPageKind.Display).TitleSearchTerms,
                SettingsPages.Get(SettingsPageKind.Display).SearchTerms),
            Is.GreaterThanOrEqualTo(0));
        Assert.That(
            SettingsSearchMatcher.Score(
                "失焦 暂停",
                SettingsPages.Get(SettingsPageKind.Gameplay).TitleSearchTerms,
                SettingsPages.Get(SettingsPageKind.Gameplay).SearchTerms),
            Is.GreaterThanOrEqualTo(0));
        Assert.That(
            SettingsSearchMatcher.Score(
                "迁移 文件夹",
                SettingsPages.Get(SettingsPageKind.Import).TitleSearchTerms,
                SettingsPages.Get(SettingsPageKind.Import).SearchTerms),
            Is.GreaterThanOrEqualTo(0));
    }
}
