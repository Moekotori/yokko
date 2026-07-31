using NUnit.Framework;
using Yokko.Game.Screens.Editor;
using Yokko.Game.Screens.Main;
using Yokko.Game.Screens.Settings;
using Yokko.Game.Screens.SongSelect;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class MainScreenPlaybackLifecycleTest
{
    [Test]
    public void SettingsKeepsHomeMusicPlaying()
    {
        Assert.That(
            MainScreen.KeepsMusicPlaying(new SettingsScreen()),
            Is.True);
    }

    [Test]
    public void EditorStillSuspendsHomeMusic()
    {
        Assert.That(
            MainScreen.KeepsMusicPlaying(new EditorScreen()),
            Is.False);
    }

    [Test]
    public void SongSelectKeepsSharedHomeMusicEngineActive()
    {
        Assert.That(
            MainScreen.KeepsMusicPlaying(new SongSelectScreen()),
            Is.True);
    }
}
