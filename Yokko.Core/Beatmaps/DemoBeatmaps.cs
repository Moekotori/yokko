using Yokko.Core.Gameplay;

namespace Yokko.Core.Beatmaps;

public static class DemoBeatmaps
{
    public static YokkoBeatmap CreateFourKeyDemo()
    {
        int[] lanes =
        [
            0, 1, 2, 3,
            0, 2, 1, 3,
            0, 1, 2, 3,
            0, 3, 1, 2,
            0, 2, 3, 1,
            0, 1, 2, 3,
        ];

        return create("Yokko Signal", "Yokko", "Codex", "4K Framework Check", KeyMode.FourKey, lanes, 520);
    }

    public static YokkoBeatmap CreateSevenKeyDemo()
    {
        int[] lanes =
        [
            0, 1, 2, 3, 4, 5, 6,
            0, 2, 4, 6, 1, 3, 5,
            6, 5, 4, 3, 2, 1, 0,
            0, 3, 6, 2, 4, 1, 5,
        ];

        return create("Yokko Signal", "Yokko", "Codex", "7K Framework Check", KeyMode.SevenKey, lanes, 430);
    }

    private static YokkoBeatmap create(
        string title,
        string artist,
        string creator,
        string difficultyName,
        KeyMode keyMode,
        IReadOnlyList<int> lanes,
        double spacingMilliseconds)
    {
        const double firstNoteTime = 1600;
        var hitObjects = new List<YokkoHitObject>(lanes.Count);

        for (int i = 0; i < lanes.Count; i++)
        {
            hitObjects.Add(new YokkoHitObject(
                lanes[i],
                firstNoteTime + i * spacingMilliseconds,
                null,
                HitObjectKind.Tap));
        }

        return new YokkoBeatmap(
            title,
            artist,
            creator,
            difficultyName,
            keyMode,
            ChartSourceFormat.Yokko,
            null,
            hitObjects);
    }
}
