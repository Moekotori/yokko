using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SixLabors.ImageSharp;
using Yokko.Game.Skinning.OsuMania;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class OsuManiaRealSkinCorpusTest
{
    [Test]
    [Category("Integration")]
    public void ParsesConfiguredRealSkinCorpus()
    {
        string root = Environment.GetEnvironmentVariable("YOKKO_OSU_MANIA_SKIN_CORPUS");

        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            Assert.Ignore("Set YOKKO_OSU_MANIA_SKIN_CORPUS to a directory containing real osu! skins.");

        string[] packages = Directory.EnumerateFiles(root, "*", SearchOption.TopDirectoryOnly)
                                     .Where(path =>
                                         Path.GetExtension(path).Equals(".osk", StringComparison.OrdinalIgnoreCase) ||
                                         Path.GetExtension(path).Equals(".zip", StringComparison.OrdinalIgnoreCase))
                                     .Concat(Directory.EnumerateDirectories(root))
                                     .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                                     .ToArray();

        Assert.That(packages, Is.Not.Empty);

        foreach (string package in packages)
        {
            using var source = new OsuManiaSkinSource(package);
            OsuManiaSkinInfo info = OsuManiaSkinIniDecoder.Decode(source.ReadSkinIni());

            Assert.That(info.ManiaConfigurations, Is.Not.Empty, Path.GetFileName(package));

            int resolved = 0;
            int missing = 0;
            var missingAssets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (OsuManiaSkinConfiguration configuration in info.ManiaConfigurations.Values)
            {
                Assert.That(configuration.ColumnWidths, Has.Length.EqualTo(configuration.Keys));
                Assert.That(configuration.ColumnSpacings, Has.Length.EqualTo(configuration.Keys));
                Assert.That(configuration.ColumnLineWidths, Has.Length.EqualTo(configuration.Keys));
                Assert.That(configuration.LaneColours, Has.Length.EqualTo(configuration.Keys));
                Assert.That(configuration.NoteBodyStyles, Has.Length.EqualTo(configuration.Keys));
                Assert.That(configuration.KeyFlipWhenUpsideDown, Has.Length.EqualTo(configuration.Keys));
                Assert.That(configuration.PressedKeyFlipWhenUpsideDown, Has.Length.EqualTo(configuration.Keys));
                Assert.That(configuration.NoteFlipWhenUpsideDown, Has.Length.EqualTo(configuration.Keys));
                Assert.That(configuration.HoldHeadFlipWhenUpsideDown, Has.Length.EqualTo(configuration.Keys));
                Assert.That(configuration.HoldBodyFlipWhenUpsideDown, Has.Length.EqualTo(configuration.Keys));
                Assert.That(configuration.HoldTailFlipWhenUpsideDown, Has.Length.EqualTo(configuration.Keys));

                if (configuration.Keys is not (4 or 7))
                    continue;

                foreach (string asset in configuredAssets(configuration))
                {
                    (string name, _) = source.ResolveTextureName(asset);

                    if (name == null)
                    {
                        missing++;
                        missingAssets.Add(asset);
                        continue;
                    }

                    byte[] bytes = source.Get(name);
                    Assert.That(bytes, Is.Not.Null.And.Not.Empty, $"{Path.GetFileName(package)}: {name}");
                    Assert.DoesNotThrow(() => Image.Load(bytes).Dispose(), $"{Path.GetFileName(package)}: {name}");
                    resolved++;
                }
            }

            Assert.That(resolved, Is.GreaterThan(0), Path.GetFileName(package));
            TestContext.Progress.WriteLine(
                $"{Path.GetFileName(package)} | {info.Name} | keys={string.Join(',', info.ManiaConfigurations.Keys.Order())} | resolved={resolved} missing={missing}");

            if (missingAssets.Count > 0)
                TestContext.Progress.WriteLine($"  inherited/fallback: {string.Join(", ", missingAssets.Order())}");
        }
    }

    private static IEnumerable<string> configuredAssets(OsuManiaSkinConfiguration configuration) =>
        configuration.KeyImages
                     .Concat(configuration.PressedKeyImages)
                     .Concat(configuration.NoteImages)
                     .Concat(configuration.HoldHeadImages)
                     .Concat(configuration.HoldBodyImages)
                     .Concat(configuration.HoldTailImages)
                     .Append(configuration.StageHint)
                     .Where(asset => !string.IsNullOrWhiteSpace(asset))
                     .Distinct(StringComparer.OrdinalIgnoreCase);
}
