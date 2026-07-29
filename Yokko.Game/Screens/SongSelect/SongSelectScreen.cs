using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Framework.Screens;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;
using Yokko.Core.Beatmaps;
using Yokko.Core.Difficulty;
using Yokko.Core.Gameplay;
using Yokko.Core.Scoring;
using Yokko.Game.Importing;
using Yokko.Game.Localisation;
using Yokko.Game.Screens.Gameplay;
using Yokko.Game.Screens.Main;
using Yokko.Game.Scoring;
using Yokko.Game.Skinning.OsuMania;

namespace Yokko.Game.Screens.SongSelect;

public partial class SongSelectScreen : Screen
{
    private const double list_refresh_stagger = 28;
    private const int max_staggered_rows = 7;

    private readonly List<SongSelectEntry> entries = createEntries();
    private readonly Dictionary<string, SongSelectEntry> importedEntries =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly List<SongSelectSongRow> rows = new();
    private readonly HashSet<string> collapsedPackages =
        new(StringComparer.OrdinalIgnoreCase);

    private TextureStore textures;
    private TextureStore chartArtworkTextures;
    private Container stage;
    private Sprite backgroundA;
    private Sprite backgroundB;
    private Sprite activeBackground;
    private Container detailsHost;
    private Container songBrowser;
    private FillFlowContainer songList;
    private BasicScrollContainer songScroll;
    private SpriteText noResults;
    private SongSelectFilterButton allFilter;
    private SongSelectFilterButton fourKeyFilter;
    private SongSelectFilterButton sevenKeyFilter;
    private SongSelectSearchBox searchBox;

    private List<SongSelectEntry> visibleEntries;
    private List<SongSelectEntry> navigableEntries = [];
    private SongSelectEntry selectedEntry;
    private KeyMode? keyModeFilter;
    private string searchQuery = string.Empty;
    private SongSelectScoreView scoreView = SongSelectScoreView.GlobalRanking;
    [Resolved]
    private GameplayScoreStore scoreStore { get; set; }
    [Resolved]
    private ImportedChartLibrary importedChartLibrary { get; set; }
    [Resolved]
    private IRenderer renderer { get; set; }

    internal SongSelectEntry SelectedEntry => selectedEntry;
    internal int VisibleEntryCount => visibleEntries?.Count ?? 0;
    internal int VisibleRowCount => navigableEntries.Count;
    internal KeyMode? KeyModeFilter => keyModeFilter;
    internal string SearchQuery => searchQuery;
    internal SongSelectScoreView ScoreView => scoreView;

    internal bool IsPackageCollapsed(string packageId) =>
        collapsedPackages.Contains(packageId);

    internal void TogglePackage(string packageId)
    {
        if (string.IsNullOrWhiteSpace(packageId))
            return;

        if (!collapsedPackages.Add(packageId))
            collapsedPackages.Remove(packageId);

        applyFilters();
    }

    [BackgroundDependencyLoader]
    private void load(TextureStore textureStore)
    {
        textures = textureStore;
        chartArtworkTextures = new TextureStore(
            renderer,
            new TextureLoaderStore(
                new ConstrainedTextureResourceStore(
                    new ChartArtworkResourceStore(),
                    renderer.MaxTextureSize)),
            scaleAdjust: 1);
        synchroniseImportedCharts();
        importedChartLibrary.LibraryChanged += onChartLibraryChanged;
        refreshSavedScores();
        selectedEntry = entries.LastOrDefault();
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
                Colour = new Color4(0.01f, 0.03f, 0.16f, 0.22f),
            },
            // 左深右浅的阶梯渐变，保证左侧文字可读、右侧背景可见。
            createLeftGradient(),
            stage = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    createHeader(logo),
                    detailsHost = new Container
                    {
                        Position = new Vector2(40, 165),
                        Size = new Vector2(440, 470),
                    },
                    createSongBrowser(),
                    createFooter(),
                    new Sprite
                    {
                        Anchor = Anchor.BottomLeft,
                        Origin = Anchor.BottomLeft,
                        Size = new Vector2(144, 163),
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
        selectedEntry = entries.Count == 0
            ? null
            : entries[Math.Min(selectedIndex, entries.Count - 1)];
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
        if (isDisposing)
        {
            if (importedChartLibrary != null)
                importedChartLibrary.LibraryChanged -= onChartLibraryChanged;

            chartArtworkTextures?.Dispose();
        }

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
                Size = new Vector2(235, 79),
                Texture = logo,
            },
            searchBox = new SongSelectSearchBox(SetSearchQuery)
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                Position = new Vector2(-96, 23),
            },
            new FillFlowContainer
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                Position = new Vector2(-340, 72),
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(8, 0),
                Children = new Drawable[]
                {
                    allFilter = new SongSelectFilterButton("ALL SONGS", 126, () => SetKeyModeFilter(null)),
                    fourKeyFilter = new SongSelectFilterButton("4K", 58, () => SetKeyModeFilter(KeyMode.FourKey)),
                    sevenKeyFilter = new SongSelectFilterButton("7K", 58, () => SetKeyModeFilter(KeyMode.SevenKey)),
                },
            },
            new SpriteIcon
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                Position = new Vector2(-39, 31),
                Size = new Vector2(26),
                Icon = FontAwesome.Solid.SlidersH,
                Colour = SongSelectTheme.Cyan,
            },
        },
    };

    private Drawable createSongBrowser() => songBrowser = new Container
    {
        Anchor = Anchor.TopRight,
        Origin = Anchor.TopRight,
        Position = new Vector2(0, 112),
        Size = new Vector2(600, 520),
        Children = new Drawable[]
        {
            songScroll = new BasicScrollContainer
            {
                RelativeSizeAxes = Axes.Both,
                ScrollbarVisible = false,
                Child = songList = new FillFlowContainer
                {
                    X = 5,
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = Vector2.Zero,
                },
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
            Position = new Vector2(620, 21),
            AutoSizeAxes = Axes.Both,
            Direction = FillDirection.Horizontal,
            Spacing = new Vector2(12, 0),
            Children = new Drawable[]
            {
                new SpriteText
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Text = "MODS",
                    Font = HomeTypography.Display(17),
                    Colour = SongSelectTheme.Ivory,
                },
                createUnavailableMod("HD", SongSelectTheme.Cyan),
                createUnavailableMod("DT", SongSelectTheme.Pink),
                new SpriteText
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Text = "2 MODS",
                    Font = HomeTypography.Display(11),
                    Colour = SongSelectTheme.Ivory,
                },
            },
        };

        return new Container
        {
            Anchor = Anchor.BottomLeft,
            Origin = Anchor.BottomLeft,
            RelativeSizeAxes = Axes.X,
            Height = 76,
            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = SongSelectTheme.DeepNavy,
                },
                new ClickableContainer
                {
                    Position = new Vector2(154, 18),
                    Size = new Vector2(120, 40),
                    Action = this.Exit,
                    Children = new Drawable[]
                    {
                        new Container
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Size = new Vector2(35, 29),
                            Masking = true,
                            CornerRadius = 4,
                            BorderThickness = 1,
                            BorderColour = new Color4(1f, 1f, 1f, 0.42f),
                            Children = new Drawable[]
                            {
                                new Box
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Colour = SongSelectTheme.Navy,
                                },
                                new SpriteText
                                {
                                    Anchor = Anchor.Centre,
                                    Origin = Anchor.Centre,
                                    Text = "ESC",
                                    Font = HomeTypography.Display(10),
                                    Colour = SongSelectTheme.Ivory,
                                },
                            },
                        },
                        new SpriteText
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            X = 45,
                            Text = "BACK",
                            Font = HomeTypography.Display(15),
                            Colour = SongSelectTheme.Ivory,
                        },
                    },
                },
                new Box
                {
                    Position = new Vector2(260, 38),
                    Size = new Vector2(34, 1),
                    Colour = new Color4(1f, 1f, 1f, 0.62f),
                },
                new SpriteIcon
                {
                    Position = new Vector2(300, 29),
                    Size = new Vector2(19),
                    Icon = FontAwesome.Solid.Heartbeat,
                    Colour = SongSelectTheme.Ivory,
                },
                new Box
                {
                    Position = new Vector2(324, 38),
                    Size = new Vector2(42, 1),
                    Colour = new Color4(1f, 1f, 1f, 0.62f),
                },
                new FillFlowContainer
                {
                    Position = new Vector2(372, 36),
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(10, 0),
                    Children = Enumerable.Range(0, 8)
                                         .Select(_ => (Drawable)new SpriteIcon
                                         {
                                             Size = new Vector2(3),
                                             Icon = FontAwesome.Solid.Circle,
                                             Colour = SongSelectTheme.Cyan,
                                         })
                                         .ToArray(),
                },
                new SpriteIcon
                {
                    Position = new Vector2(500, 28),
                    Size = new Vector2(14),
                    Icon = FontAwesome.Solid.Plus,
                    Colour = SongSelectTheme.Pink,
                },
                new Box
                {
                    Position = new Vector2(600, 7),
                    Size = new Vector2(1, 62),
                    Rotation = 14,
                    Colour = new Color4(
                        SongSelectTheme.Pink.R,
                        SongSelectTheme.Pink.G,
                        SongSelectTheme.Pink.B,
                        0.64f),
                },
                mods,
                new ClickableContainer
                {
                    Anchor = Anchor.BottomRight,
                    Origin = Anchor.BottomRight,
                    Size = new Vector2(320, 76),
                    Masking = true,
                    Action = PlaySelected,
                    Children = new Drawable[]
                    {
                        // 整体斜切的平行四边形黄底（底边向左伸出，与参考图一致），右缘由 Masking 裁齐。
                        new Box
                        {
                            Position = new Vector2(58, 0),
                            Size = new Vector2(340, 76),
                            Shear = new Vector2(-0.5f, 0),
                            Colour = SongSelectTheme.Yellow,
                        },
                        new SpriteIcon
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            X = 66,
                            Size = new Vector2(30),
                            Icon = FontAwesome.Solid.Play,
                            Colour = SongSelectTheme.Navy,
                        },
                        new SpriteText
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            X = 12,
                            Text = "PLAY",
                            Font = HomeTypography.Display(36),
                            Colour = SongSelectTheme.Navy,
                        },
                        new FillFlowContainer
                        {
                            Anchor = Anchor.CentreRight,
                            Origin = Anchor.CentreRight,
                            X = -13,
                            AutoSizeAxes = Axes.Both,
                            Direction = FillDirection.Vertical,
                            Spacing = new Vector2(0, 4),
                            Children = Enumerable.Range(0, 7)
                                                 .Select(_ => (Drawable)new SpriteIcon
                                                 {
                                                     Size = new Vector2(2),
                                                     Icon = FontAwesome.Solid.Circle,
                                                     Colour = new Color4(
                                                         SongSelectTheme.Navy.R,
                                                         SongSelectTheme.Navy.G,
                                                         SongSelectTheme.Navy.B,
                                                         0.78f),
                                                 })
                                                 .ToArray(),
                        },
                    },
                },
            },
        };
    }

    protected override void Update()
    {
        base.Update();

        if (songBrowser != null)
            songBrowser.Height = MathF.Max(360, DrawHeight - 188);
    }

    private static Drawable createUnavailableMod(string label, Color4 accent) => new Container
    {
        Size = new Vector2(48, 34),
        Masking = true,
        CornerRadius = 4,
        BorderThickness = 2,
        BorderColour = accent,
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
                Font = HomeTypography.Display(18),
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
            new Box
            {
                Position = new Vector2(16, 18),
                Size = new Vector2(1, 54),
                Colour = new Color4(1f, 1f, 1f, 0.72f),
            },
            new SpriteIcon
            {
                Position = new Vector2(12, 78),
                Size = new Vector2(9),
                Icon = FontAwesome.Solid.Plus,
                Colour = SongSelectTheme.Cyan,
            },
            new Box
            {
                Position = new Vector2(16, 96),
                Size = new Vector2(1, 252),
                Colour = new Color4(1f, 1f, 1f, 0.72f),
            },
            new FillFlowContainer
            {
                Position = new Vector2(14, 360),
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 7),
                Children = Enumerable.Range(0, 7)
                                     .Select(_ => (Drawable)new SpriteIcon
                                     {
                                         Size = new Vector2(4),
                                         Icon = FontAwesome.Solid.Circle,
                                         Colour = SongSelectTheme.Cyan,
                                     })
                                     .ToArray(),
            },
            new SpriteIcon
            {
                Position = new Vector2(10, 538),
                Size = new Vector2(11),
                Icon = FontAwesome.Regular.Heart,
                Colour = SongSelectTheme.Pink,
            },
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
            Position = new Vector2(0, 250),
        };
        ranking.SetView(scoreView, textures);

        detailsHost.AddRange(new Drawable[]
        {
            // 黄色标记笔高亮紧贴文字宽度。
            new Container
            {
                AutoSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    new Box
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        RelativeSizeAxes = Axes.X,
                        Width = 1.07f,
                        Height = 40,
                        Y = 6,
                        Rotation = -1.2f,
                        Colour = SongSelectTheme.Yellow,
                    },
                    new SpriteText
                    {
                        MaxWidth = 400,
                        Text = selectedEntry.Beatmap.Title,
                        Font = HomeTypography.Display(42),
                        Colour = SongSelectTheme.Navy,
                    },
                },
            },
            new SpriteText
            {
                Position = new Vector2(10, 62),
                Text = selectedEntry.Beatmap.Artist,
                Font = HomeTypography.Display(19),
                Colour = SongSelectTheme.Ivory,
            },
            new SpriteText
            {
                Position = new Vector2(10, 84),
                Text = $"mapped by {selectedEntry.Beatmap.Creator}",
                Font = HomeTypography.Body(15),
                Colour = SongSelectTheme.PaleCyan,
            },
            new Container
            {
                Position = new Vector2(10, 110),
                AutoSizeAxes = Axes.Both,
                Masking = true,
                CornerRadius = 4,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = SongSelectTheme.Pink,
                    },
                    new SpriteText
                    {
                        Position = new Vector2(9, 3),
                        Text = $"{(int)selectedEntry.Beatmap.KeyMode}K · {selectedEntry.Beatmap.DifficultyName}",
                        Font = HomeTypography.Display(14),
                        Colour = Color4.White,
                    },
                },
            },
            createStarRating(selectedEntry.StarRating),
            createSongStat(10, 192, FontAwesome.Regular.Clock, "LENGTH", selectedEntry.Length.ToString(@"mm\:ss")),
            createSongStat(118, 192, FontAwesome.Solid.WaveSquare, "BPM", selectedEntry.Bpm.ToString("0")),
            createBestScoreStat(226, 192),
            ranking,
        });
    }

    private Drawable createBestScoreStat(float x, float y) => new Container
    {
        Position = new Vector2(x, y),
        Size = new Vector2(200, 66),
        Children = new Drawable[]
        {
            new SpriteIcon
            {
                Position = new Vector2(0, 4),
                Size = new Vector2(15),
                Icon = FontAwesome.Solid.Trophy,
                Colour = SongSelectTheme.Cyan,
            },
            new SpriteText
            {
                Position = new Vector2(20, 0),
                Text = "BEST SCORE",
                Font = HomeTypography.Display(9),
                Colour = SongSelectTheme.PaleCyan,
            },
            new SpriteText
            {
                Position = new Vector2(20, 17),
                Text = selectedEntry.BestScore > 0 ? $"{selectedEntry.BestScore:N0}" : "NO SCORE YET",
                Font = HomeTypography.Display(selectedEntry.BestScore > 0 ? 20 : 14),
                Colour = selectedEntry.BestScore > 0 ? SongSelectTheme.Ivory : SongSelectTheme.PaleCyan,
            },
            new SpriteText
            {
                Position = new Vector2(20, 43),
                Text = selectedEntry.BestAccuracy > 0 ? $"ACC  {selectedEntry.BestAccuracy:P2}" : string.Empty,
                Font = HomeTypography.Display(14),
                Colour = SongSelectTheme.Pink,
            },
        },
    };

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

    private static Drawable createStarRating(
        ManiaStarRatingResult rating)
    {
        double value = rating.Value ?? 0;
        int filled = rating.IsSuccess ? (int)Math.Min(7, Math.Floor(value)) : 0;

        var flow = new FillFlowContainer
        {
            Position = new Vector2(10, 144),
            AutoSizeAxes = Axes.Both,
            Direction = FillDirection.Horizontal,
            Spacing = new Vector2(4, 0),
        };

        for (int i = 0; i < 7; i++)
        {
            flow.Add(new SpriteIcon
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                Size = new Vector2(22),
                Icon = i < filled ? FontAwesome.Solid.Star : FontAwesome.Regular.Star,
                Colour = rating.IsSuccess ? SongSelectTheme.Yellow : SongSelectTheme.PaleCyan,
            });
        }

        flow.Add(new SpriteText
        {
            Anchor = Anchor.CentreLeft,
            Origin = Anchor.CentreLeft,
            X = 6,
            Text = rating.Value?.ToString("0.00") ?? "--",
            Font = HomeTypography.Display(22),
            Colour = rating.IsSuccess ? SongSelectTheme.Ivory : SongSelectTheme.PaleCyan,
        });

        return flow;
    }

    private void rebuildSongList()
    {
        if (songList == null)
            return;

        songList.Clear();
        rows.Clear();
        navigableEntries = [];
        int drawableIndex = 0;

        foreach (IGrouping<string, SongSelectEntry> group in visibleEntries.GroupBy(
                     entry => entry.PackageId,
                     StringComparer.OrdinalIgnoreCase))
        {
            SongSelectEntry first = group.First();
            SongSelectEntry[] groupEntries = group.ToArray();
            bool collapsed = first.IsPackage
                             && collapsedPackages.Contains(first.PackageId)
                             && string.IsNullOrWhiteSpace(searchQuery);

            if (first.IsPackage)
            {
                int songCount = groupEntries
                                .Select(entry =>
                                    $"{entry.Beatmap.Artist}\u001f{entry.Beatmap.Title}")
                                .Distinct(StringComparer.OrdinalIgnoreCase)
                                .Count();
                var header = new SongSelectPackageHeader(
                    first.PackageName,
                    songCount,
                    groupEntries.Length,
                    collapsed,
                    () => TogglePackage(first.PackageId))
                {
                    Alpha = 0,
                    X = 12,
                };
                songList.Add(header);
                header.Delay(Math.Min(drawableIndex++, max_staggered_rows) * list_refresh_stagger)
                      .FadeIn(150, Easing.OutQuint)
                      .MoveToX(0, 210, Easing.OutQuint);
            }

            if (collapsed)
                continue;

            foreach (SongSelectEntry entry in groupEntries)
            {
                SongSelectSongRow row = new(
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
                navigableEntries.Add(entry);
                songList.Add(row);

                double delay = Math.Min(drawableIndex++, max_staggered_rows)
                               * list_refresh_stagger;
                row.Delay(delay)
                   .FadeIn(170, Easing.OutQuint)
                   .MoveToX(0, 240, Easing.OutQuint);
            }
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

        rebuildSongList();

        if (navigableEntries.Count > 0
            && !navigableEntries.Contains(selectedEntry))
            select(navigableEntries[0]);
    }

    private void selectOffset(int direction)
    {
        if (navigableEntries.Count == 0)
            return;

        int index = navigableEntries.IndexOf(selectedEntry);
        if (index < 0)
            index = 0;
        else
            index = (index + direction + navigableEntries.Count) % navigableEntries.Count;

        select(navigableEntries[index]);
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

            SongSelectSongRow selectedRow = rows.FirstOrDefault(row => row.Entry == entry);
            if (selectedRow != null)
                songScroll?.ScrollIntoView(selectedRow, true);
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

    private Texture textureFor(SongSelectEntry entry)
    {
        if (entry != null
            && Path.IsPathRooted(entry.WallpaperTexture)
            && File.Exists(entry.WallpaperTexture))
        {
            try
            {
                Texture artwork = chartArtworkTextures.Get(entry.WallpaperTexture);
                if (artwork != null)
                    return artwork;
            }
            catch
            {
                // Invalid chart artwork falls back to Yokko's bundled image.
            }
        }

        return textures.Get(entry?.WallpaperTexture ?? "SongSelect/blue-signal")
               ?? textures.Get("SongSelect/blue-signal");
    }

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

    private void onChartLibraryChanged() =>
        Scheduler.Add(() => synchroniseImportedCharts(true));

    private void synchroniseImportedCharts(bool selectNewest = false)
    {
        string selectedImportedId = importedEntries
                                    .Where(pair => ReferenceEquals(
                                        pair.Value.Beatmap,
                                        selectedEntry?.Beatmap))
                                    .Select(pair => pair.Key)
                                    .FirstOrDefault();

        foreach (SongSelectEntry existing in importedEntries.Values)
        {
            int existingIndex = entries.FindIndex(entry =>
                ReferenceEquals(entry.Beatmap, existing.Beatmap));
            if (existingIndex >= 0)
                entries.RemoveAt(existingIndex);
        }

        importedEntries.Clear();

        foreach (ImportedChart chart in importedChartLibrary.GetCharts())
        {
            SongSelectEntry entry = createImportedEntry(chart);
            importedEntries[chart.Id] = entry;
            entries.Add(entry);
        }

        if (!selectNewest
            && selectedImportedId != null
            && importedEntries.TryGetValue(
                selectedImportedId,
                out SongSelectEntry preservedSelection))
        {
            selectedEntry = preservedSelection;
        }

        if (songList == null)
            return;

        if (selectNewest)
        {
            keyModeFilter = null;
            searchQuery = string.Empty;
            if (searchBox?.Current.Value.Length > 0)
                searchBox.Current.Value = string.Empty;
            updateFilters();
        }

        applyFilters();

        if (selectNewest && importedEntries.Count > 0)
            select(entries[^1]);
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

    private static Drawable createLeftGradient()
    {
        float[] alphas = { 0.62f, 0.5f, 0.38f, 0.26f, 0.15f, 0.06f };
        return new Container
        {
            RelativeSizeAxes = Axes.Both,
            ChildrenEnumerable = alphas.Select((alpha, index) => (Drawable)new Box
            {
                RelativeSizeAxes = Axes.Both,
                RelativePositionAxes = Axes.X,
                X = index / 6f,
                Width = 1 / 6f + 0.01f,
                Colour = new Color4(
                    SongSelectTheme.DeepNavy.R,
                    SongSelectTheme.DeepNavy.G,
                    SongSelectTheme.DeepNavy.B,
                    alpha),
            }),
        };
    }

    private static List<SongSelectEntry> createEntries() => [];

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
            imported.ArtworkPath ?? "SongSelect/blue-signal",
            imported.StarRating,
            TimeSpan.FromMilliseconds(Math.Max(0, lengthMilliseconds)),
            bpm,
            0,
            0,
            [],
            imported.PackageId,
            imported.PackageName,
            imported.IsPackage);
    }

}
