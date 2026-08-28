using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Sprites;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Yokko.Game.Screens.Gameplay;
using Yokko.Game.Skinning.OsuMania;

namespace Yokko.Game.Tests.Visual;

[TestFixture]
public partial class TestSceneOsuManiaSkinOverlay : YokkoTestScene
{
    [Resolved]
    private IRenderer renderer { get; set; }

    private OsuManiaSkin skin;
    private OsuManiaSkinOverlay overlay;

    [Test]
    public void TestComboDigitsReusePooledSprites()
    {
        Sprite[] pooledSprites = null;

        AddStep("load skinned overlay", () =>
        {
            loadOverlay(withDigits: true);
            pooledSprites = overlay.ComboDigitSpritesForTest.ToArray();
        });
        AddAssert("pool covers the maximum combo digit count", () =>
            pooledSprites.Length == 10);
        AddStep("show combo 12", () => overlay.SetCombo(12));
        AddAssert("two digits visible with correct textures", () =>
            visibleDigitCount(overlay.ComboDigitSpritesForTest) == 2
            && overlay.ComboDigitSpritesForTest[0].Texture
            == skin.GetTexture("score-1")
            && overlay.ComboDigitSpritesForTest[1].Texture
            == skin.GetTexture("score-2"));
        AddStep("show combo 345", () => overlay.SetCombo(345));
        AddAssert("combo change reuses the pooled sprites", () =>
            overlay.ComboDigitSpritesForTest.SequenceEqual(pooledSprites));
        AddAssert("three digits visible with updated textures", () =>
            visibleDigitCount(overlay.ComboDigitSpritesForTest) == 3
            && overlay.ComboDigitSpritesForTest[0].Texture
            == skin.GetTexture("score-3")
            && overlay.ComboDigitSpritesForTest[1].Texture
            == skin.GetTexture("score-4")
            && overlay.ComboDigitSpritesForTest[2].Texture
            == skin.GetTexture("score-5"));
        AddAssert("digit positions advance by texture width", () =>
        {
            // 数字纹理宽度为 8 + digit，ComboOverlap 为 0。
            IReadOnlyList<Sprite> sprites =
                overlay.ComboDigitSpritesForTest;
            return sprites[0].X == 0
                   && Math.Abs(sprites[1].X - 11) < 0.001f
                   && Math.Abs(sprites[2].X - 23) < 0.001f;
        });
        AddStep("shrink combo to 7", () => overlay.SetCombo(7));
        AddAssert("stale digits are hidden", () =>
            visibleDigitCount(overlay.ComboDigitSpritesForTest) == 1
            && overlay.ComboDigitSpritesForTest[0].Texture
            == skin.GetTexture("score-7"));
    }

    [Test]
    public void TestComboBreakReusesPooledSprites()
    {
        Sprite[] pooledBreakSprites = null;

        AddStep("load skinned overlay", () =>
        {
            loadOverlay(withDigits: true);
            pooledBreakSprites =
                overlay.ComboBreakDigitSpritesForTest.ToArray();
        });
        AddStep("break a 25 combo", () =>
        {
            overlay.SetCombo(25);
            overlay.SetCombo(0);
        });
        AddAssert("combo break reuses the pooled sprites", () =>
            overlay.ComboBreakDigitSpritesForTest.SequenceEqual(
                pooledBreakSprites)
            && visibleDigitCount(overlay.ComboBreakDigitSpritesForTest) == 2
            && overlay.ComboBreakDigitSpritesForTest[0].Texture
            == skin.GetTexture("score-2")
            && overlay.ComboBreakDigitSpritesForTest[1].Texture
            == skin.GetTexture("score-5"));
    }

    [Test]
    public void TestMissingDigitTexturesFallBackToText()
    {
        AddStep("load overlay without digit textures", () =>
        {
            loadOverlay(withDigits: false);
            overlay.SetCombo(128);
        });
        AddAssert("fallback text is shown instead of digit sprites", () =>
            overlay.ComboLayoutDrawable is SpriteText text
            && text.Text.ToString() == "128"
            && visibleDigitCount(overlay.ComboDigitSpritesForTest) == 0);
    }

    private void loadOverlay(bool withDigits)
    {
        // 先移除上一轮的 overlay 再释放其皮肤，避免场景中的 sprite
        // 引用已释放的纹理。最后一份皮肤随测试进程结束回收。
        Clear();
        skin?.Dispose();
        skin = OsuManiaSkin.Load(createDigitSkin(withDigits), 4, renderer);
        Add(overlay = new OsuManiaSkinOverlay(skin));
    }

    private static int visibleDigitCount(IReadOnlyList<Sprite> sprites)
        => sprites.Count(sprite => sprite.Alpha > 0);

    private static string createDigitSkin(bool withDigits)
    {
        string directory = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            "osu-skin-combo-pool",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "skin.ini"), """
[General]
Name: Combo Pool Fixture
Version: 2.5

[Mania]
Keys: 4
""");

        if (!withDigits)
            return directory;

        for (int digit = 0; digit < 10; digit++)
        {
            using var image = new Image<Rgba32>(
                8 + digit,
                8,
                new Rgba32(240, 200, 80, 255));
            image.SaveAsPng(Path.Combine(directory, $"score-{digit}.png"));
        }

        return directory;
    }
}
