using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Screens;
using osu.Framework.Testing;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Yokko.Core.Beatmaps;
using Yokko.Core.Gameplay;
using Yokko.Core.Timing;
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

        [Test]
        [Category("Integration")]
        public void TestLoadsRealOsuManiaSkinSample()
        {
            string skinPath = Environment.GetEnvironmentVariable("YOKKO_OSU_MANIA_SKIN_SAMPLE");

            if (string.IsNullOrWhiteSpace(skinPath) || (!File.Exists(skinPath) && !Directory.Exists(skinPath)))
                Assert.Ignore("Set YOKKO_OSU_MANIA_SKIN_SAMPLE to a real osu! skin package.");

            bool sevenKey = Environment.GetEnvironmentVariable("YOKKO_OSU_MANIA_SKIN_SAMPLE_KEYS") == "7";

            AddStep($"open real {(sevenKey ? 7 : 4)}K skin", () =>
                screenStack.Push(new GameplayScreen(
                    sevenKey ? DemoBeatmaps.CreateSevenKeyDemo() : DemoBeatmaps.CreateFourKeyDemo(),
                    skinPath: skinPath)));
            AddUntilStep("real skin playfield loaded", () =>
                (screenStack.CurrentScreen as Drawable)?.ChildrenOfType<GameplayPlayfield>().SingleOrDefault() != null);
            AddUntilStep("real skin textures decoded", () =>
                (screenStack.CurrentScreen as Drawable)?.ChildrenOfType<Sprite>().Any(sprite => sprite.Texture != null) == true);
        }

        [Test]
        [Category("Integration")]
        public void TestLoadsRealArrowSkin()
        {
            string skinPath = Environment.GetEnvironmentVariable("YOKKO_OSU_MANIA_ARROW_SKIN_SAMPLE");

            if (string.IsNullOrWhiteSpace(skinPath) || !File.Exists(skinPath))
                Assert.Ignore("Set YOKKO_OSU_MANIA_ARROW_SKIN_SAMPLE to a real arrow skin package.");

            AddStep("open real arrow skin", () =>
                screenStack.Push(new GameplayScreen(createHoldDemo(KeyMode.FourKey), skinPath: skinPath)));
            AddUntilStep("all arrow lanes use textures", () =>
            {
                DrawableNote[] notes = (screenStack.CurrentScreen as Drawable)?
                                       .ChildrenOfType<DrawableNote>()
                                       .Take(4)
                                       .ToArray();
                Sprite tiledBody = notes?
                                   .FirstOrDefault()?
                                   .ChildrenOfType<Sprite>()
                                   .FirstOrDefault(sprite => sprite.Texture?.WrapModeT == WrapMode.Repeat);
                return notes?.Length == 4 &&
                       notes.All(note => note.ChildrenOfType<Sprite>().Any(sprite => sprite.Texture != null)) &&
                       notes.SelectMany(note => note.ChildrenOfType<Sprite>())
                            .Select(sprite => sprite.Texture)
                            .Where(texture => texture != null)
                            .Distinct()
                            .Count() >= 4 &&
                       tiledBody?.TextureRelativeSizeAxes == Axes.None;
            });
        }

        [Test]
        [Category("Integration")]
        public void TestLoadsRealThrowStyleSkinBodyWithoutStretching()
        {
            string skinPath = Environment.GetEnvironmentVariable("YOKKO_OSU_MANIA_THROW_SKIN_SAMPLE");

            if (string.IsNullOrWhiteSpace(skinPath) || !File.Exists(skinPath))
                Assert.Ignore("Set YOKKO_OSU_MANIA_THROW_SKIN_SAMPLE to a real throw-style skin package.");

            AddStep("open real throw-style skin", () =>
                screenStack.Push(new GameplayScreen(createHoldDemo(KeyMode.SevenKey), skinPath: skinPath)));
            AddUntilStep("long body texture is cropped at natural scale", () =>
            {
                DrawableNote hold = (screenStack.CurrentScreen as Drawable)?
                                    .ChildrenOfType<DrawableNote>()
                                    .FirstOrDefault();
                Sprite longBody = hold?
                                  .ChildrenOfType<Sprite>()
                                  .FirstOrDefault(sprite => sprite.Texture?.DisplayHeight >= 30000);
                return longBody != null &&
                       longBody.Texture.Available &&
                       Math.Abs(longBody.Height - 40000f * 92 / 138) < 1 &&
                       longBody.Height > hold.Height * 10 &&
                       longBody.Parent is Container { Masking: true };
            });
        }

        [Test]
        public void TestUpsideDownSkinFlipsArrowElements()
        {
            string skinPath = createTestSkin("""
UpsideDown: 1
HitPosition: 400
""");

            AddStep("open upside-down skin", () =>
                screenStack.Push(new GameplayScreen(DemoBeatmaps.CreateFourKeyDemo(), skinPath: skinPath)));
            AddUntilStep("upscroll geometry applied", () =>
            {
                GameplayPlayfield playfield = (screenStack.CurrentScreen as Drawable)?
                                              .ChildrenOfType<GameplayPlayfield>()
                                              .SingleOrDefault();
                return playfield?.ScrollOrigin == 480 && playfield.JudgementPosition == 80;
            });
            AddUntilStep("notes and receptors flipped", () =>
            {
                Drawable current = screenStack.CurrentScreen as Drawable;
                bool noteFlipped = current?
                                   .ChildrenOfType<DrawableNote>()
                                   .SelectMany(note => note.ChildrenOfType<Sprite>())
                                   .Any(sprite => sprite.Scale.Y < 0) == true;
                bool keyFlipped = current?
                                  .ChildrenOfType<LaneColumn>()
                                  .SelectMany(lane => lane.ReceptorLayer.ChildrenOfType<Sprite>())
                                  .Any(sprite => sprite.Scale.Y < 0) == true;
                return noteFlipped && keyFlipped;
            });
        }

        private static string createTestSkin()
            => createTestSkin(string.Empty);

        private static string createTestSkin(string extraManiaConfiguration)
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
""" + Environment.NewLine + extraManiaConfiguration);

            using var image = new Image<Rgba32>(8, 8, new Rgba32(72, 208, 240, 255));
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
                image.SaveAsPng(Path.Combine(directory, name));

            return directory;
        }

        private static YokkoBeatmap createHoldDemo(KeyMode keyMode)
        {
            int keys = (int)keyMode;
            var hitObjects = Enumerable.Range(0, keys)
                                       .Select(lane => lane == 0
                                           ? new YokkoHitObject(lane, 1600, 2800, HitObjectKind.Hold)
                                           : new YokkoHitObject(lane, 1600 + lane * 180, null, HitObjectKind.Tap))
                                       .ToArray();

            return new YokkoBeatmap(
                "Skin Compatibility",
                "Yokko",
                "Codex",
                $"{keys}K Special Skin",
                keyMode,
                ChartSourceFormat.Yokko,
                [YokkoTimingPoint.Default],
                null,
                hitObjects);
        }
    }
}
