using Yokko.Import;

if (args.Length != 1)
    throw new ArgumentException("Pass a .qp package path.");

IReadOnlyList<ChartImportResult> results =
    await KnownChartImporters.ImportAllAsync(
        new ChartImportRequest(args[0], true));

Console.WriteLine($"charts={results.Count}");

foreach (ChartImportResult result in results)
{
    int zeroVelocityCount =
        result.Beatmap.ScrollVelocities.Count(
            velocity => velocity.Multiplier == 0);
    int negativeVelocityCount =
        result.Beatmap.ScrollVelocities.Count(
            velocity => velocity.Multiplier < 0);

    Console.WriteLine(
        $"{result.Beatmap.Title} [{result.Beatmap.DifficultyName}] "
        + $"notes={result.Beatmap.HitObjects.Count} "
        + $"initialSV={result.Beatmap.InitialScrollVelocity} "
        + $"svs={result.Beatmap.ScrollVelocities.Count} "
        + $"zero={zeroVelocityCount} "
        + $"negative={negativeVelocityCount} "
        + $"audio={File.Exists(result.Beatmap.AudioPath)} "
        + $"art={File.Exists(result.ArtworkPath)}");
}
