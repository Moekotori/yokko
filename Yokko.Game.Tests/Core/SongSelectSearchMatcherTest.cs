using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using Yokko.Core.Beatmaps;
using Yokko.Game.Screens.SongSelect;

namespace Yokko.Game.Tests.Core;

[TestFixture]
public sealed class SongSelectSearchMatcherTest
{
    [Test]
    public void MultipleTokensCanMatchDifferentMetadataFields()
    {
        string document = SongSelectSearchMatcher.CreateDocument(
            DemoBeatmaps.CreateFourKeyDemo() with
            {
                Title = "Paper Moon",
                Artist = "Mochi Unit",
                Creator = "Mapper Cat",
                DifficultyName = "Midnight Insane",
            });

        Assert.Multiple(() =>
        {
            Assert.That(matches(document, "mochi insane"), Is.True);
            Assert.That(matches(document, "paper mapper midnight"), Is.True);
            Assert.That(matches(document, "mochi missing"), Is.False);
        });
    }

    [Test]
    public void UnicodeCompatibilityAndCanonicalFormsMatch()
    {
        string document = SongSelectSearchMatcher.CreateDocument(
            DemoBeatmaps.CreateFourKeyDemo() with
            {
                Title = "Café ＭＯＯＮ",
                RomanisedTitle = "Cafe Moon",
                Artist = "夜空",
            });

        Assert.Multiple(() =>
        {
            Assert.That(matches(document, "Cafe\u0301 moon"), Is.True);
            Assert.That(matches(document, "ＣＡＦＥ́ ＭＯＯＮ"), Is.True);
            Assert.That(matches(document, "夜空 cafe"), Is.True);
        });
    }

    [Test]
    public void QueryWhitespaceAndDuplicateTokensAreIgnored()
    {
        string document = SongSelectSearchMatcher.CreateDocument(
            DemoBeatmaps.CreateFourKeyDemo() with
            {
                Title = "Signal",
                Tags = "electronic rhythm",
            });
        string[] tokens = SongSelectSearchMatcher.TokenizeQuery(
            "  signal\tRHYTHM  signal ");

        Assert.Multiple(() =>
        {
            Assert.That(tokens, Is.EqualTo(new[] { "SIGNAL", "RHYTHM" }));
            Assert.That(SongSelectSearchMatcher.Matches(document, tokens), Is.True);
            Assert.That(
                SongSelectSearchMatcher.Matches(
                    document,
                    SongSelectSearchMatcher.TokenizeQuery(string.Empty)),
                Is.True);
        });
    }

    [Test]
    public void TenThousandCachedDocumentsCanBeSearched()
    {
        string[] documents = Enumerable.Range(0, 10_000)
            .Select(index => SongSelectSearchMatcher.CreateDocument(
                DemoBeatmaps.CreateFourKeyDemo() with
                {
                    Title = $"Library Song {index:D5}",
                    Artist = index == 9_999 ? "Target Artist" : "Other Artist",
                    Creator = "Large Library Mapper",
                }))
            .ToArray();
        string[] tokens = SongSelectSearchMatcher.TokenizeQuery(
            "target 09999 mapper");

        Stopwatch stopwatch = Stopwatch.StartNew();
        int matches = documents.Count(document =>
            SongSelectSearchMatcher.Matches(document, tokens));
        stopwatch.Stop();
        TestContext.Progress.WriteLine(
            $"Searched 10,000 cached song documents in {stopwatch.ElapsedMilliseconds} ms.");

        Assert.That(matches, Is.EqualTo(1));
    }

    private static bool matches(string document, string query) =>
        SongSelectSearchMatcher.Matches(
            document,
            SongSelectSearchMatcher.TokenizeQuery(query));
}
