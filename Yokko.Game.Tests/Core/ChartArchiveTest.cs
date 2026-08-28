using System;
using System.IO;
using NUnit.Framework;
using Yokko.Import;

namespace Yokko.Game.Tests.Core
{
    [TestFixture]
    public sealed class ChartArchiveTest
    {
        private static string extractionsRoot => Path.Combine(
            Path.GetTempPath(),
            "Yokko",
            "ChartImports");

        [Test]
        public void CleanUpStaleExtractionsRemovesOnlyOldDirectories()
        {
            string staleDirectory = Path.Combine(
                extractionsRoot,
                Guid.NewGuid().ToString("N"));
            string freshDirectory = Path.Combine(
                extractionsRoot,
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(staleDirectory);
            Directory.CreateDirectory(freshDirectory);
            File.WriteAllText(Path.Combine(staleDirectory, "chart.osu"), "stale");
            Directory.SetLastWriteTimeUtc(
                staleDirectory,
                DateTime.UtcNow - TimeSpan.FromDays(3));

            try
            {
                ChartArchive.CleanUpStaleExtractions(TimeSpan.FromHours(24));

                Assert.Multiple(() =>
                {
                    Assert.That(Directory.Exists(staleDirectory), Is.False,
                        "Stale extraction directories must be reclaimed.");
                    Assert.That(Directory.Exists(freshDirectory), Is.True,
                        "Recent extraction directories may belong to a live session and must survive.");
                });
            }
            finally
            {
                if (Directory.Exists(staleDirectory))
                    Directory.Delete(staleDirectory, true);
                if (Directory.Exists(freshDirectory))
                    Directory.Delete(freshDirectory, true);
            }
        }

        [Test]
        public void TryDeleteExtractionIgnoresPathsOutsideExtractionsRoot()
        {
            string unrelatedDirectory = Path.Combine(
                TestContext.CurrentContext.WorkDirectory,
                $"not-an-extraction-{Guid.NewGuid():N}");
            Directory.CreateDirectory(unrelatedDirectory);

            try
            {
                ChartArchive.TryDeleteExtraction(unrelatedDirectory);

                Assert.That(Directory.Exists(unrelatedDirectory), Is.True,
                    "Only directories under the shared extraction root may be deleted.");
            }
            finally
            {
                if (Directory.Exists(unrelatedDirectory))
                    Directory.Delete(unrelatedDirectory, true);
            }
        }

        [Test]
        public void TryDeleteExtractionRemovesDirectoryUnderExtractionsRoot()
        {
            string extractionDirectory = Path.Combine(
                extractionsRoot,
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(extractionDirectory);
            File.WriteAllText(
                Path.Combine(extractionDirectory, "chart.osu"),
                "content");

            try
            {
                ChartArchive.TryDeleteExtraction(extractionDirectory);

                Assert.That(Directory.Exists(extractionDirectory), Is.False);
            }
            finally
            {
                if (Directory.Exists(extractionDirectory))
                    Directory.Delete(extractionDirectory, true);
            }
        }
    }
}
