using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Framework.Screens;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;
using Yokko.Core.Beatmaps;
using Yokko.Core.Gameplay;
using Yokko.Core.Scoring;
using Yokko.Game.Importing;
using Yokko.Game.Localisation;
using Yokko.Game.Screens.Gameplay;
using Yokko.Game.Screens.Main;
using Yokko.Game.Scoring;

namespace Yokko.Game.Screens.SongSelect;

public partial class SongSelectScreen : Screen
{
    private const float designed_width = 1280;
    private const float designed_height = 720;
    private const double list_refresh_stagger = 28;
    private const int max_staggered_rows = 7;

    private readonly List<SongSelectEntry> entries = createEntries();
    private readonly Dictionary<string, SongSelectEntry> importedEntries =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly List<SongSelectSongRow> rows = new();

    private TextureStore textures;
    private Container stage;
    private Sprite backgroundA;
    private Sprite backgroundB;
    private Sprite activeBackground;
    private Container detailsHost;
    private FillFlowContainer songList;
    private SpriteText noResults;
    private SongSelectFilterButton allFilter;
    private SongSelectFilterButton fourKeyFilter;
    private SongSelectFilterButton sevenKeyFilter;
    private SongSelectSearchBox searchBox;

    private List<SongSelectEntry> visibleEntries;
    private SongSelectEntry selectedEntry;
    private KeyMode? keyModeFilter;
    private string searchQuery = string.Empty;
    private SongSelectScoreView scoreView = SongSelectScoreView.GlobalRanking;
    [Resolved]
    private GameplayScoreStore scoreStore { get; set; }
    [Resolved]
    private ImportedChartLibrary importedChartLibrary { get; set; }

    internal SongSelectEntry SelectedEntry => selectedEntry;
    internal int VisibleEntryCount => visibleEntries?.Count ?? 0;
    internal KeyMode? KeyModeFilter => keyModeFilter;
    internal string SearchQuery => searchQuery;
    internal SongSelectScoreView ScoreView => scoreView;

    [BackgroundDependencyLoader]
    private void load(TextureStore textureStore)
    {
        textures = textureStore;
        synchroniseImportedCharts();
        importedChartLibrary.ChartImported += onChartImported;
        refreshSavedScores();
        selectedEntry = importedEntries.Count > 0 ? entries[^1] : entries[0];
        visibleEntries = entries.ToList();

        Texture firstWallpaper = textureFor(selectedEntry);
        Texture logo = textures.Get("home-logo-light");
        Texture mascot = textures.Get("yokko").Crop(new RectangleF(80, 1840, 1200, 1360));

        InternalChildren = new Drawable[]
        {
            backgroundA = createBackground(firstWallpaper),
            backgroundB = createBackground(firstWallpaper),
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = new Color4(0.01f, 0.03f, 0.16f, 0.12f),
            },
            stage = new Container
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Size = new Vector2(designed_width, designed_height),
                Children = new Drawable[]
                {
                    createHeader(logo),
                    detailsHost = new Container
                    {
                        Position = new Vector2(45, 138),
                        Size = new Vector2(460, 500),
                    },
                    createSongBrowser(),
                    createFooter(),
                    new Sprite
                    {
                        Position = new Vector2(7, 604),
                        Size = new Vector2(102, 116),
                        Texture = mascot,
                    },
                    createDecorations(),
                },
            },
        };

        backgroundB.Alpha = 0;
        activeBackground = backgroundA;
        rebuildDetails();
        rebuildSongList();
        updateFilters();

        stage.Alpha = 0;
        stage.Y = 14;
    }

    public override void OnEntering(ScreenTransitionEvent e)
    {
        base.OnEntering(e);
        stage.FadeIn(260, Easing.OutQuint).MoveToY(0, 420, Easing.OutQuint);
    }

    public override void OnResuming(ScreenTransitionEvent e)
    {
        base.OnResuming(e);
        synchroniseImportedCharts();
        int selectedIndex = Math.Max(0, entries.IndexOf(selectedEntry));
        refreshSavedScores();
        selectedEntry = entries[Math.Min(selectedIndex, entries.Count - 1)];
        applyFilters();
        rebuildDetails();
        this.FadeIn(180, Easing.OutQuint);
    }

    public override void OnSuspending(ScreenTransitionEvent e)
    {
        base.OnSuspending(e);
        this.FadeTo(0.35f, 180, Easing.OutQuint);
    }

    public override bool OnExiting(ScreenExitEvent e)
    {
        this.FadeOut(180, Easing.OutQuint);
        return base.OnExiting(e);
    }

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing && importedChartLibrary != null)
            importedChartLibrary.ChartImported -= onChartImported;

        base.Dispose(isDisposing);
    }

    protected override bool OnKeyDown(KeyDownEvent e)
    {
        switch (e.Key)
        {
            case Key.Up:
                SelectPrevious();
                return true;

            case Key.Down:
                SelectNext();
                return true;

            case Key.Enter:
                PlaySelected();
                return true;

            case Key.Escape:
                this.Exit();
                return true;

            default:
                return base.OnKeyDown(e);
        }
    }

    internal void SelectNext() => selectOffset(1);

    internal void SelectPrevious() => selectOffset(-1);

    internal void SetKeyModeFilter(KeyMode? mode)
    {
        keyModeFilter = mode;
        updateFilters();
        applyFilters();
    }

    internal void SetSearchQuery(string query)
    {
        searchQuery = query ?? string.Empty;
        applyFilters();
    }

    internal void ToggleScoreView()
    {
        scoreView = scoreView == SongSelectScoreView.GlobalRanking
            ? SongSelectScoreView.Personal
            : SongSelectScoreView.GlobalRanking;
        rebuildDetails();
    }

    internal void PlaySelected()
    {
        if (selectedEntry != null)
            this.Push(new GameplayScreen(selectedEntry.Beatmap));
    }

    private Drawable createHeader(Texture logo) => new Container
    {
        RelativeSizeAxes = Axes.Both,
        Children = new Drawable[]
        {
            new Sprite
            {
                Position = new Vector2(42, 24),
                Size = new Vector2(270, 91),
                Texture = logo,
            },
            searchBox = new SongSelectSearchBox(SetSearchQuery)
            {
                Position = new Vector2(784, 23),
            },
            new FillFlowContainer
            {
                Position = new Vector2(690, 72),
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(8, 0),
                Children = new Drawable[]
                {
                    allFilter = new SongSelectFilterButton(YokkoStrings.Get("song_select.all_songs"), 126, () => SetKeyModeFilter(null)),
                    fourKeyFilter = new SongSelectFilterButton("4K", 58, () => SetKeyModeFilter(KeyMode.FourKey)),
                    sevenKeyFilter = new SongSelectFilterButton("7K", 58, () => SetKeyModeFilter(KeyMode.SevenKey)),
                },
            },
            new SpriteIcon
            {
                Position = new Vector2(1215, 31),
                Size = new Vector2(26),
                Icon = FontAwesome.Solid.SlidersH,
                Colour = SongSelectTheme.Cyan,
            },
        },
    };

    private Drawable createSongBrowser() => new Container
    {
        Position = new Vector2(680, 112),
        Size = new Vector2(600, 520),
        Children = new Drawable[]
        {
            songList = new FillFlowContainer
            {
                X = 5,
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 5),
            },
            noResults = new SpriteText
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Text = YokkoStrings.Get("song_select.no_results"),
                Font = HomeTypography.Display(24),
                Colour = SongSelectTheme.PaleCyan,
                Alpha = 0,
            },
        },
    };

    private Drawable createFooter()
    {
        var mods = new FillFlowContainer
        {
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            AutoSizeAxes = Axes.Both,
            Direction = FillDirection.Horizontal,
            Spacing = new Vector2(12, 0),
            Children = new Drawable[]
            {
                new SpriteText
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Text = YokkoStrings.Get("song_select.mods"),
                    Font = HomeTypography.Display(18),
                    Colour = SongSelectTheme.Ivory,
                },
                createUnavailableMod("HD", SongSelectTheme.Cyan),
                createUnavailableMod("DT", SongSelectTheme.Pink),
                new SpriteText
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Text = YokkoStrings.Get("song_select.mods_unavailable"),
                    Font = HomeTypography.Body(11),
                    Colour = SongSelectTheme.Muted,
                },
            },
        };

        return new Container
        {
            Anchor = Anchor.BottomLeft,
            Origin = Anchor.BottomLeft,
            Size = new Vector2(designed_width, 60),
            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = SongSelectTheme.DeepNavy,
                },
                new ClickableContainer
                {
                    Position = new Vector2(178, 8),
                    Size = new Vector2(145, 44),
                    Action = this.Exit,
                    Children = new Drawable[]
                    {
                        new SpriteText
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Text = YokkoStrings.Get("song_select.back"),
                            Font = HomeTypography.Display(17),
                            Colour = SongSelectTheme.Ivory,
                        },
                    },
                },
                mods,
                new ClickableContainer
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    Size = new Vector2(278, 60),
                    Action = PlaySelected,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = SongSelectTheme.Yellow,
                        },
                        new SpriteIcon
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            X = 34,
                            Size = new Vector2(27),
                            Icon = FontAwesome.Solid.Play,
                            Colour = SongSelectTheme.Navy,
                        },
                        new SpriteText
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            X = 20,
                            Text = YokkoStrings.Get("song_select.play"),
                            Font = HomeTypography.Display(31),
                            Colour = SongSelectTheme.Navy,
                        },
                    },
                },
            },
        };
    }

    private static Drawable createUnavailableMod(string label, Color4 accent) => new Container
    {
        Size = new Vector2(46, 32),
        Masking = true,
        CornerRadius = 5,
        BorderThickness = 1.5f,
        BorderColour = new Color4(accent.R, accent.G, accent.B, 0.5f),
        Alpha = 0.55f,
        Children = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = new Color4(SongSelectTheme.Navy.R, SongSelectTheme.Navy.G, SongSelectTheme.Navy.B, 0.62f),
            },
            new SpriteText
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Text = label,
                Font = HomeTypography.Display(16),
                Colour = accent,
            },
        },
    };

    private static Drawable createDecorations() => new Container
    {
        RelativeSizeAxes = Axes.Both,
        Depth = 5,
        Children = new Drawable[]
        {
            new SpriteIcon
            {
                Position = new Vector2(33, 127),
                Size = new Vector2(13),
                Icon = FontAwesome.Solid.Plus,
                Colour = SongSelectTheme.Yellow,
            },
            new SpriteIcon
            {
                Position = new Vector2(358, 158),
                Size = new Vector2(12),
                Icon = FontAwesome.Solid.Plus,
                Colour = SongSelectTheme.Pink,
            },
            new SpriteIcon
            {
                Position = new Vector2(493, 414),
                Size = new Vector2(11),
                Icon = FontAwesome.Solid.Plus,
                Colour = SongSelectTheme.Cyan,
            },
        },
    };

    private void rebuildDetails()
    {
        if (detailsHost == null || selectedEntry == null)
            return;

        detailsHost.Clear();

        var ranking = new SongSelectRankingPanel(selectedEntry, textures, newView => scoreView = newView)
        {
            Position = new Vector2(8, 184),
        };
        ranking.SetView(scoreView, textures);

        detailsHost.AddRange(new Drawable[]
        {
            new Container
            {
                Position = new Vector2(7, 0),
                Size = new Vector2(410, 54),
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = SongSelectTheme.Yellow,
                        Rotation = -1.2f,
                    },
                    new SpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        X = 18,
                        Text = selectedEntry.Beatmap.Title,
                        Font = HomeTypography.Display(42),
                        Colour = SongSelectTheme.Navy,
                    },
                },
            },
            new SpriteText
            {
                Position = new Vector2(18, 57),
                Text = selectedEntry.Beatmap.Artist,
                Font = HomeTypography.Display(19),
                Colour = SongSelectTheme.Ivory,
            },
            new SpriteText
            {
                Position = new Vector2(18, 82),
                Text = YokkoStrings.Get("song_select.mapped_by", selectedEntry.Beatmap.Creator),
                Font = HomeTypography.Body(15),
                Colour = SongSelectTheme.PaleCyan,
            },
            new SpriteText
            {
                Position = new Vector2(18, 111),
                Text = $"{(int)selectedEntry.Beatmap.KeyMode}K  ·  {selectedEntry.Beatmap.DifficultyName}",
                Font = HomeTypography.Display(17),
                Colour = SongSelectTheme.Pink,
            },
            createStarRating(selectedEntry.StarRating),
            createSongStat(258, 136, FontAwesome.Regular.Clock, YokkoStrings.Get("song_select.length"), selectedEntry.Length.ToString(@"mm\:ss")),
            createSongStat(356, 136, FontAwesome.Solid.WaveSquare, "BPM", selectedEntry.Bpm.ToString("0")),
            ranking,
        });
    }

    private static Drawable createSongStat(float x, float y, IconUsage icon, LocalisableString label, string value) => new Container
    {
        Position = new Vector2(x, y),
        Size = new Vector2(96, 44),
        Children = new Drawable[]
        {
            new SpriteIcon
            {
                Position = new Vector2(0, 4),
                Size = new Vector2(15),
                Icon = icon,
                Colour = SongSelectTheme.Cyan,
            },
            new SpriteText
            {
                Position = new Vector2(20, 0),
                Text = label,
                Font = HomeTypography.Display(9),
                Colour = SongSelectTheme.PaleCyan,
            },
            new SpriteText
            {
                Position = new Vector2(20, 17),
                Text = value,
                Font = HomeTypography.Display(20),
                Colour = SongSelectTheme.Ivory,
            },
        },
    };

    private static Drawable createStarRating(double rating)
    {
        var flow = new FillFlowContainer
        {
            Position = new Vector2(18, 140),
            AutoSizeAxes = Axes.Both,
            Direction = FillDirection.Horizontal,
            Spacing = new Vector2(7, 0),
        };

        for (int i = 0; i < 5; i++)
        {
            flow.Add(new SpriteIcon
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                Size = new Vector2(22),
                Icon = FontAwesome.Solid.Star,
                Colour = SongSelectTheme.Yellow,
            });
        }

        flow.Add(new SpriteIcon
        {
            Anchor = Anchor.CentreLeft,
            Origin = Anchor.CentreLeft,
            Size = new Vector2(22),
            Icon = FontAwesome.Regular.Star,
            Colour = SongSelectTheme.Ivory,
        });

        flow.Add(new SpriteText
        {
            Anchor = Anchor.CentreLeft,
            Origin = Anchor.CentreLeft,
            Text = rating.ToString("0.00"),
            Font = HomeTypography.Display(21),
            Colour = SongSelectTheme.Ivory,
        });

        return flow;
    }

    private void rebuildSongList()
    {
        if (songList == null)
            return;

        songList.Clear();
        rows.Clear();

        for (int index = 0; index < visibleEntries.Count; index++)
        {
            SongSelectEntry entry = visibleEntries[index];
            SongSelectSongRow row = null;
            row = new SongSelectSongRow(
                entry,
                textureFor(entry),
                () => select(entry),
                () =>
                {
                    select(entry);
                    PlaySelected();
                });
            row.SetSelected(entry == selectedEntry);
            row.Alpha = 0;
            row.X = 24;
            rows.Add(row);
            songList.Add(row);

            double delay = Math.Min(index, max_staggered_rows) * list_refresh_stagger;
            row.Delay(delay)
               .FadeIn(170, Easing.OutQuint)
               .MoveToX(0, 240, Easing.OutQuint);
        }

        noResults.FadeTo(visibleEntries.Count == 0 ? 1 : 0, 140, Easing.OutQuint);
    }

    private void applyFilters()
    {
        visibleEntries = entries.Where(entry =>
            (!keyModeFilter.HasValue || entry.Beatmap.KeyMode == keyModeFilter) &&
            (string.IsNullOrWhiteSpace(searchQuery) ||
             entry.Beatmap.Title.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) ||
             entry.Beatmap.Artist.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) ||
             entry.Beatmap.Creator.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) ||
             entry.Beatmap.DifficultyName.Contains(searchQuery, StringComparison.OrdinalIgnoreCase)))
                                .ToList();

        if (visibleEntries.Count > 0 && !visibleEntries.Contains(selectedEntry))
            select(visibleEntries[0], rebuildList: false);

        rebuildSongList();
    }

    private void selectOffset(int direction)
    {
        if (visibleEntries.Count == 0)
            return;

        int index = visibleEntries.IndexOf(selectedEntry);
        if (index < 0)
            index = 0;
        else
            index = (index + direction + visibleEntries.Count) % visibleEntries.Count;

        select(visibleEntries[index]);
    }

    private void select(SongSelectEntry entry, bool rebuildList = true)
    {
        if (entry == null)
            return;

        bool changed = selectedEntry != entry;
        selectedEntry = entry;

        if (changed)
        {
            crossFadeBackground(textureFor(entry));
            rebuildDetails();
        }

        if (rebuildList)
        {
            foreach (SongSelectSongRow row in rows)
                row.SetSelected(row.Entry == entry);
        }
    }

    private void crossFadeBackground(Texture texture)
    {
        Sprite incoming = activeBackground == backgroundA ? backgroundB : backgroundA;
        incoming.Texture = texture;
        incoming.Alpha = 0;
        incoming.FadeIn(220, Easing.OutQuint);
        activeBackground.FadeOut(220, Easing.OutQuint);
        activeBackground = incoming;
    }

    private Texture textureFor(SongSelectEntry entry) => textures.Get(entry.WallpaperTexture);

    private void refreshSavedScores()
    {
        for (int i = 0; i < entries.Count; i++)
        {
            SongSelectEntry entry = entries[i];
            StoredGameplayScore saved = scoreStore.GetBest(entry.Beatmap);
            if (saved == null)
                continue;

            var ranking = entry.Ranking
                               .Where(score => !score.IsCurrentPlayer)
                               .Append(new SongSelectScore(
                                   0,
                                   "YOKKO",
                                   "yokko",
                                   saved.Rank,
                                   (int)Math.Min(int.MaxValue, saved.Score),
                                   saved.Accuracy,
                                   [],
                                   true))
                               .OrderByDescending(score => score.Score)
                               .Select((score, rank) => score with
                               {
                                   Rank = rank + 1,
                               })
                               .ToArray();

            entries[i] = entry with
            {
                BestScore = (int)Math.Min(int.MaxValue, saved.Score),
                BestAccuracy = saved.Accuracy,
                Ranking = ranking,
            };
        }
    }

    private void onChartImported(ImportedChart chart) =>
        Scheduler.Add(() => upsertImportedChart(chart, true));

    private void synchroniseImportedCharts()
    {
        foreach (ImportedChart chart in importedChartLibrary.GetCharts())
            upsertImportedChart(chart, false);
    }

    private void upsertImportedChart(ImportedChart chart, bool selectImported)
    {
        if (importedEntries.TryGetValue(chart.SourcePath, out SongSelectEntry existing))
        {
            int existingIndex = entries.FindIndex(entry =>
                ReferenceEquals(entry.Beatmap, existing.Beatmap));
            if (existingIndex >= 0)
                entries.RemoveAt(existingIndex);
        }

        SongSelectEntry entry = createImportedEntry(chart);
        importedEntries[chart.SourcePath] = entry;
        entries.Add(entry);

        if (songList == null)
            return;

        if (selectImported)
        {
            keyModeFilter = null;
            searchQuery = string.Empty;
            if (searchBox?.Current.Value.Length > 0)
                searchBox.Current.Value = string.Empty;
            updateFilters();
        }

        applyFilters();

        if (selectImported)
            select(entry);
    }

    private void updateFilters()
    {
        allFilter?.SetSelected(!keyModeFilter.HasValue);
        fourKeyFilter?.SetSelected(keyModeFilter == KeyMode.FourKey);
        sevenKeyFilter?.SetSelected(keyModeFilter == KeyMode.SevenKey);
    }

    private static Sprite createBackground(Texture texture) => new()
    {
        RelativeSizeAxes = Axes.Both,
        Texture = texture,
        FillMode = FillMode.Fill,
    };

    private static List<SongSelectEntry> createEntries()
    {
        IReadOnlyList<SongSelectScore> ranking =
        [
            new(1, "MIKA", "SongSelect/Avatars/mika", ScoreRank.X, 998420, 0.9982, ["HD"]),
            new(2, "RIN", "SongSelect/Avatars/rin", ScoreRank.S, 992115, 0.9921, ["DT"]),
            new(3, "AOI", "SongSelect/Avatars/aoi", ScoreRank.S, 990004, 0.9894, []),
            new(4, "LUNA", "SongSelect/Avatars/luna", ScoreRank.A, 988020, 0.9866, ["HD"]),
            new(5, "YOKKO", "yokko", ScoreRank.A, 987432, 0.9841, ["HD", "DT"], true),
        ];

        return
        [
            createEntry("Blue Signal", "Asteria", "Yokko Team", "Hyper", KeyMode.FourKey, "SongSelect/blue-signal", 6.42, 138, 178, ranking),
            // Demo entries use Yokko's fallback art. A real library provider maps
            // each imported beatmap's own background into WallpaperTexture.
            createEntry("Neon Pulse", "Synthion", "EchoRay", "Hyper", KeyMode.FourKey, "SongSelect/blue-signal", 6.21, 126, 186, ranking),
            createEntry("Afterimage", "Nixara", "Zero", "Insane", KeyMode.FourKey, "SongSelect/blue-signal", 6.78, 154, 174, ranking),
            createEntry("Circuit Bloom", "Lunetia", "Mura", "Hyper", KeyMode.SevenKey, "SongSelect/blue-signal", 6.05, 142, 192, ranking),
            createEntry("Parallel Hearts", "Koharu", "Rinstar", "Insane", KeyMode.SevenKey, "SongSelect/blue-signal", 6.66, 149, 180, ranking),
        ];
    }

    private static SongSelectEntry createImportedEntry(ImportedChart imported)
    {
        YokkoBeatmap beatmap = imported.Result.Beatmap;
        double lengthMilliseconds = beatmap.HitObjects.Count == 0
            ? 0
            : beatmap.HitObjects.Max(hitObject =>
                hitObject.EndTimeMilliseconds ?? hitObject.StartTimeMilliseconds);
        double bpm = beatmap.TimingPoints
                            .Where(point => point.Uninherited && point.BeatsPerMinute > 0)
                            .Select(point => point.BeatsPerMinute)
                            .FirstOrDefault();

        return new SongSelectEntry(
            beatmap,
            "SongSelect/blue-signal",
            beatmap.OverallDifficulty,
            TimeSpan.FromMilliseconds(Math.Max(0, lengthMilliseconds)),
            bpm,
            0,
            0,
            []);
    }

    private static SongSelectEntry createEntry(
        string title,
        string artist,
        string creator,
        string difficulty,
        KeyMode keyMode,
        string wallpaper,
        double stars,
        int seconds,
        double bpm,
        IReadOnlyList<SongSelectScore> ranking)
    {
        YokkoBeatmap source = keyMode == KeyMode.FourKey
            ? DemoBeatmaps.CreateFourKeyDemo()
            : DemoBeatmaps.CreateSevenKeyDemo();
        YokkoBeatmap beatmap = source with
        {
            Title = title,
            Artist = artist,
            Creator = creator,
            DifficultyName = difficulty,
        };

        return new SongSelectEntry(
            beatmap,
            wallpaper,
            stars,
            TimeSpan.FromSeconds(seconds),
            bpm,
            987432,
            0.9841,
            ranking);
    }
}
