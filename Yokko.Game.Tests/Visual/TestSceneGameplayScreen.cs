using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Screens;
using osu.Framework.Testing;
using Yokko.Core.Beatmaps;
using Yokko.Game.Screens.Gameplay;

namespace Yokko.Game.Tests.Visual
{
    [TestFixture]
    public partial class TestSceneGameplayScreen : YokkoTestScene
    {
        private readonly ScreenStack screenStack;

        public TestSceneGameplayScreen()
        {
            Add(screenStack = new ScreenStack(new GameplayScreen(DemoBeatmaps.CreateFourKeyDemo()))
            {
                RelativeSizeAxes = Axes.Both,
            });
        }

        [Test]
        public void TestLoadsOsuManiaSkinTextures()
        {
            string skinPath = createTestSkin();

            AddStep("open skinned gameplay", () =>
                screenStack.Push(new GameplayScreen(DemoBeatmaps.CreateFourKeyDemo(), skinPath: skinPath)));
            AddUntilStep("custom playfield width applied", () =>
                (screenStack.CurrentScreen as Drawable)?.ChildrenOfType<GameplayPlayfield>().SingleOrDefault()?.Width == 160);
            AddUntilStep("skin sprites loaded", () =>
                (screenStack.CurrentScreen as Drawable)?.ChildrenOfType<Sprite>().Any(sprite => sprite.Texture != null) == true);
        }

        private static string createTestSkin()
        {
            string directory = Path.Combine(
                TestContext.CurrentContext.WorkDirectory,
                "osu-skin-visual",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "skin.ini"), """
[General]
Name: Yokko Visual Fixture
Version: 2.5

[Mania]
Keys: 4
ColumnWidth: 40,40,40,40
KeyImage0: key
KeyImage0D: key-down
NoteImage0: note
NoteImage0H: hold-head
NoteImage0L: hold-body
NoteImage0T: hold-tail
StageHint: stage-hint
""");

            byte[] png = Convert.FromBase64String(
                "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAAEElEQVR42mP8z8AARAwMjIACAA0DAAG8D8nWAAAAAElFTkSuQmCC");

            foreach (string name in new[]
                     {
                         "key.png",
                         "key-down.png",
                         "note.png",
                         "hold-head.png",
                         "hold-body.png",
                         "hold-tail.png",
                         "stage-hint.png",
                     })
                File.WriteAllBytes(Path.Combine(directory, name), png);

            return directory;
        }
    }
}
