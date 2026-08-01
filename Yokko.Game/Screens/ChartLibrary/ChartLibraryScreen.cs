using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Framework.Platform;
using osu.Framework.Screens;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;
using Yokko.Game.Configuration;
using Yokko.Game.Importing;
using Yokko.Game.Localisation;
using Yokko.Game.Resources;
using Yokko.Game.Screens.Main;
using Yokko.Game.Screens.Settings;
using Yokko.Import;

namespace Yokko.Game.Screens.ChartLibrary;

public partial class ChartLibraryScreen : Screen
{
    private const float designedWidth = 1280;
    private const float designedHeight = 720;
    private const int pageSize = 120;

    [Resolved]
    private GameHost host { get; set; }

    [Resolved]
    private ImportedChartLibrary importedChartLibrary { get; set; }

    [Resolved]
    private YokkoImportSettings importSettings { get; set; }

    [Resolved]
    private YokkoExternalOsuSettings externalOsuSettings { get; set; }

    [Resolved]
    private IResourceDirectoryPicker resourceDirectoryPicker { get; set; }

    [Resolved]
    private YokkoConfigManager yokkoConfig { get; set; }

    private readonly CancellationTokenSource workCancellation = new();
    private Container stage;
    private Container decorationLayer;
    private FillFlowContainer chartRows;
    private ChartLibraryScrollContainer chartScroll;
    private ChartLibrarySearchBox searchBox;
    private ChartLibraryFilterChip allFilter;
    private ChartLibraryFilterChip managedFilter;
    private ChartLibraryFilterChip externalFilter;
    private ChartLibraryStatCard totalStat;
    private ChartLibraryStatCard managedStat;
    private ChartLibraryStatCard externalStat;
    private SpriteText resultCountText;
    private SpriteText statusText;
    private SpriteText managedPathText;
    private SpriteText externalPathText;
    private ChartLibraryActionButton importButton;
    private ChartLibraryActionButton importFolderButton;
    private ChartLibraryActionButton selectOsuButton;
    private ChartLibraryActionButton refreshButton;
    private ChartLibraryActionButton autoFindButton;
    private ChartLibraryActionButton disableExternalButton;
    private IReadOnlyList<ImportedChart> snapshot = Array.Empty<ImportedChart>();
    private string query = string.Empty;
    private int displayLimit = pageSize;
    private bool workInProgress;
    private bool resourcesDisposed;
    private Vector2 lastResponsiveStageSize;
    private Vector2 parallaxCurrent;

    internal int FilteredChartCount { get; private set; }
    internal int ManagedChartCount { get; private set; }
    internal int ExternalChartCount { get; private set; }
    internal ChartLibrarySourceFilter CurrentSourceFilter { get; private set; }

    internal void SetSourceFilter(ChartLibrarySourceFilter filter) =>
        setFilter(filter);

    internal void SetSearchQuery(string value)
    {
        if (searchBox != null)
            searchBox.Current.Value = value ?? string.Empty;
    }

    [BackgroundDependencyLoader]
    private void load(TextureStore textures)
    {
        InternalChildren = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = HomeControlColours.Cyan,
            },
            stage = new Container
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Size = new Vector2(designedWidth, designedHeight),
                Scale = new Vector2(SettingsScreen.ReferenceLayoutScale),
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Y,
                        Width = 334,
                        Colour = HomeControlColours.Ivory,
                    },
                    new HomeMarqueeTicker(),
                    new SpriteText
                    {
                        Position = new Vector2(850, 560),
                        Rotation = -4,
                        Text = "CHART LAB",
                        Font = HomeTypography.Brand(96),
                        Colour = new Color4(
                            Color4.White.R,
                            Color4.White.G,
                            Color4.White.B,
                            0.075f),
                    },
                    decorationLayer = createDecorationLayer(),
                    new HomeTapRippleLayer(),
                    createHeader(textures.Get("home-logo-light")),
                    createSidebar(),
                    createLibraryPanel(),
                    new Container
                    {
                        Anchor = Anchor.BottomLeft,
                        Origin = Anchor.BottomLeft,
                        RelativeSizeAxes = Axes.X,
                        Height = 8,
                        Child = new HomeHazardStripes(
                            4000,
                            new Color4(
                                HomeControlColours.Navy.R,
                                HomeControlColours.Navy.G,
                                HomeControlColours.Navy.B,
                                0.34f)),
                    },
                },
            },
        };

        setFilter(ChartLibrarySourceFilter.All);
        refreshSnapshot();
    }

    protected override void LoadComplete()
    {
        base.LoadComplete();
        importedChartLibrary.LibraryChanged += onLibraryChanged;
    }

    public override void OnEntering(ScreenTransitionEvent e)
    {
        base.OnEntering(e);
        stage.MoveToY(10).MoveToY(0, 420, Easing.OutQuint);
        this.FadeInFromZero(260, Easing.OutQuint);
    }

    public override bool OnExiting(ScreenExitEvent e)
    {
        this.FadeOut(180, Easing.OutQuint);
        return base.OnExiting(e);
    }

    protected override bool OnKeyDown(KeyDownEvent e)
    {
        if (e.Key != Key.Escape)
            return base.OnKeyDown(e);

        if (!string.IsNullOrEmpty(searchBox.Current.Value))
        {
            searchBox.Current.Value = string.Empty;
            return true;
        }

        this.Exit();
        return true;
    }

    protected override void Update()
    {
        base.Update();

        if (stage == null || DrawWidth <= 0 || DrawHeight <= 0)
            return;

        var viewport = new Vector2(DrawWidth, DrawHeight);
        Vector2 stageSize = SettingsScreen.CalculateResponsiveStageSize(viewport);
        float stageScale = SettingsScreen.CalculateStageScale(viewport, stageSize);

        if ((stageSize - lastResponsiveStageSize).LengthSquared >= 0.01f
            || MathF.Abs(stage.Scale.X - stageScale) >= 0.001f)
        {
            lastResponsiveStageSize = stageSize;
            stage.Size = stageSize;
            stage.Scale = new Vector2(stageScale);
        }

        Vector2 pointer = GetContainingInputManager()?.CurrentState.Mouse.Position
                          ?? viewport / 2;
        Vector2 target = new(
            Math.Clamp((pointer.X / MathF.Max(1, viewport.X) - 0.5f) * 2, -1, 1),
            Math.Clamp((pointer.Y / MathF.Max(1, viewport.Y) - 0.5f) * 2, -1, 1));
        float blend = 1f - MathF.Exp((float)(-Clock.ElapsedFrameTime / 120));
        parallaxCurrent = Vector2.Lerp(parallaxCurrent, target, blend);
        decorationLayer.Position = parallaxCurrent * new Vector2(12, 8);
    }

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing && !resourcesDisposed)
        {
            resourcesDisposed = true;
            importedChartLibrary.LibraryChanged -= onLibraryChanged;
            workCancellation.Cancel();
            workCancellation.Dispose();
        }

        base.Dispose(isDisposing);
    }

    private Drawable createHeader(Texture logoTexture) => new Container
    {
        Position = new Vector2(40, 36),
        Size = new Vector2(1200, 74),
        Children = new Drawable[]
        {
            new ChartLibraryActionButton(
                string.Empty,
                FontAwesome.Solid.ArrowLeft,
                this.Exit,
                50),
            new HomeBrandLockup(
                logoTexture,
                HomeControlColours.Navy,
                HomeControlColours.Yellow)
            {
                Position = new Vector2(72, -1),
                Scale = new Vector2(0.36f),
            },
            new Box
            {
                Position = new Vector2(298, 3),
                Size = new Vector2(2, 56),
                Colour = new Color4(
                    HomeControlColours.Navy.R,
                    HomeControlColours.Navy.G,
                    HomeControlColours.Navy.B,
                    0.26f),
            },
            new Box
            {
                Position = new Vector2(322, 35),
                Size = new Vector2(122, 8),
                Colour = new Color4(
                    HomeControlColours.Yellow.R,
                    HomeControlColours.Yellow.G,
                    HomeControlColours.Yellow.B,
                    0.82f),
            },
            new SpriteText
            {
                Position = new Vector2(322, 0),
                Text = YokkoStrings.Get("chart_library.title"),
                Font = HomeTypography.Display(31),
                Colour = HomeControlColours.Navy,
            },
            new SpriteText
            {
                Position = new Vector2(324, 42),
                Text = YokkoStrings.Get("chart_library.subtitle"),
                Font = HomeTypography.Body(13),
                Colour = new Color4(
                    HomeControlColours.Navy.R,
                    HomeControlColours.Navy.G,
                    HomeControlColours.Navy.B,
                    0.68f),
            },
            new FillFlowContainer
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(10, 0),
                Children = new Drawable[]
                {
                    refreshButton = new ChartLibraryActionButton(
                        YokkoStrings.Get("chart_library.refresh"),
                        FontAwesome.Solid.SyncAlt,
                        refreshAll,
                        112),
                    selectOsuButton = new ChartLibraryActionButton(
                        YokkoStrings.Get("chart_library.select_osu"),
                        FontAwesome.Solid.FolderOpen,
                        chooseExternalOsuFolder,
                        174),
                    importFolderButton = new ChartLibraryActionButton(
                        YokkoStrings.Get("chart_library.import_folder"),
                        FontAwesome.Solid.FolderPlus,
                        openImportFolderSelector,
                        148),
                    importButton = new ChartLibraryActionButton(
                        YokkoStrings.Get("chart_library.import"),
                        FontAwesome.Solid.BookOpen,
                        openImportSelector,
                        146,
                        true),
                },
            },
        },
    };

    private Drawable createSidebar() => new Container
    {
        Position = new Vector2(40, 124),
        Size = new Vector2(286, 548),
        Masking = true,
        CornerRadius = 12,
        BorderThickness = 1.8f,
        BorderColour = HomeControlColours.Navy,
        Children = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = Color4.White,
            },
            new HomeDotField
            {
                Position = new Vector2(202, 18),
                Size = new Vector2(68, 42),
                Colour = new Color4(
                    HomeControlColours.Navy.R,
                    HomeControlColours.Navy.G,
                    HomeControlColours.Navy.B,
                    0.12f),
            },
            new SpriteText
            {
                Position = new Vector2(20, 18),
                Text = "LIBRARY // 01",
                Font = HomeTypography.Display(11),
                Colour = HomeControlColours.Pink,
            },
            new SpriteText
            {
                Position = new Vector2(20, 42),
                Text = YokkoStrings.Get("chart_library.overview"),
                Font = HomeTypography.Display(22),
                Colour = HomeControlColours.Navy,
            },
            totalStat = new ChartLibraryStatCard(
                YokkoStrings.Get("chart_library.total"),
                FontAwesome.Solid.Music,
                HomeControlColours.Yellow,
                246)
            {
                Position = new Vector2(20, 82),
            },
            managedStat = new ChartLibraryStatCard(
                YokkoStrings.Get("chart_library.managed"),
                FontAwesome.Solid.BookOpen,
                HomeControlColours.Cyan,
                119)
            {
                Position = new Vector2(20, 170),
            },
            externalStat = new ChartLibraryStatCard(
                YokkoStrings.Get("chart_library.external"),
                FontAwesome.Solid.FolderOpen,
                HomeControlColours.Pink,
                119)
            {
                Position = new Vector2(147, 170),
            },
            createPathCard(
                YokkoStrings.Get("chart_library.managed_path"),
                importedChartLibrary.LibraryPath,
                HomeControlColours.Cyan,
                new Vector2(20, 260),
                out managedPathText),
            createPathCard(
                YokkoStrings.Get("chart_library.osu_path"),
                externalOsuSettings.SongsPath.Value,
                HomeControlColours.Pink,
                new Vector2(20, 344),
                out externalPathText),
            autoFindButton = new ChartLibraryActionButton(
                YokkoStrings.Get("chart_library.auto_find"),
                FontAwesome.Solid.Search,
                autoFindExternalOsu,
                118)
            {
                Position = new Vector2(20, 432),
            },
            disableExternalButton = new ChartLibraryActionButton(
                YokkoStrings.Get("chart_library.disable_osu"),
                FontAwesome.Solid.Times,
                disableExternalOsu,
                118,
                accent: HomeControlColours.Pink)
            {
                Position = new Vector2(148, 432),
            },
            new SpriteText
            {
                Position = new Vector2(20, 499),
                Text = YokkoStrings.Get("chart_library.read_only_hint"),
                Font = HomeTypography.Body(11),
                Colour = new Color4(
                    HomeControlColours.Navy.R,
                    HomeControlColours.Navy.G,
                    HomeControlColours.Navy.B,
                    0.62f),
                MaxWidth = 246,
                Truncate = true,
            },
        },
    };

    private Drawable createLibraryPanel() => new Container
    {
        Position = new Vector2(344, 124),
        Size = new Vector2(896, 548),
        Masking = true,
        CornerRadius = 12,
        BorderThickness = 1.8f,
        BorderColour = HomeControlColours.Navy,
        Children = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = Color4.White,
            },
            new Box
            {
                RelativeSizeAxes = Axes.X,
                Height = 62,
                Colour = new Color4(
                    HomeControlColours.PaleCyan.R,
                    HomeControlColours.PaleCyan.G,
                    HomeControlColours.PaleCyan.B,
                    0.32f),
            },
            searchBox = new ChartLibrarySearchBox(onQueryChanged)
            {
                Position = new Vector2(18, 11),
            },
            new FillFlowContainer
            {
                Position = new Vector2(350, 14),
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(8, 0),
                Children = new Drawable[]
                {
                    allFilter = new ChartLibraryFilterChip(
                        YokkoStrings.Get("chart_library.filter_all"),
                        () => setFilter(ChartLibrarySourceFilter.All),
                        92),
                    managedFilter = new ChartLibraryFilterChip(
                        YokkoStrings.Get("chart_library.filter_managed"),
                        () => setFilter(ChartLibrarySourceFilter.Managed),
                        116),
                    externalFilter = new ChartLibraryFilterChip(
                        YokkoStrings.Get("chart_library.filter_external"),
                        () => setFilter(ChartLibrarySourceFilter.ExternalOsu),
                        126),
                },
            },
            resultCountText = new SpriteText
            {
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                Position = new Vector2(-18, 21),
                Font = HomeTypography.Display(12),
                Colour = HomeControlColours.Navy,
            },
            chartScroll = new ChartLibraryScrollContainer
            {
                Position = new Vector2(18, 74),
                Size = new Vector2(860, 414),
                ScrollbarVisible = true,
                Child = chartRows = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 7),
                },
            },
            new Box
            {
                Position = new Vector2(18, 501),
                Width = 860,
                Height = 1,
                Colour = new Color4(
                    HomeControlColours.Navy.R,
                    HomeControlColours.Navy.G,
                    HomeControlColours.Navy.B,
                    0.18f),
            },
            statusText = new SpriteText
            {
                Position = new Vector2(18, 516),
                Text = YokkoStrings.Get("chart_library.ready"),
                Font = HomeTypography.Body(12),
                Colour = new Color4(
                    HomeControlColours.Navy.R,
                    HomeControlColours.Navy.G,
                    HomeControlColours.Navy.B,
                    0.68f),
                MaxWidth = 820,
                Truncate = true,
            },
        },
    };

    private static Container createPathCard(
        LocalisableString label,
        string path,
        Color4 accent,
        Vector2 position,
        out SpriteText pathText)
    {
        pathText = new SpriteText
        {
            Position = new Vector2(12, 39),
            Text = formatPath(path),
            Font = HomeTypography.Body(11),
            Colour = new Color4(
                HomeControlColours.Navy.R,
                HomeControlColours.Navy.G,
                HomeControlColours.Navy.B,
                0.68f),
            MaxWidth = 220,
            Truncate = true,
        };

        return new Container
        {
            Position = position,
            Size = new Vector2(246, 72),
            Masking = true,
            CornerRadius = 8,
            BorderThickness = 1.2f,
            BorderColour = new Color4(
                HomeControlColours.Navy.R,
                HomeControlColours.Navy.G,
                HomeControlColours.Navy.B,
                0.3f),
            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.White,
                },
                new Box
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 4,
                    Colour = accent,
                },
                new SpriteText
                {
                    Position = new Vector2(12, 13),
                    Text = label,
                    Font = HomeTypography.Display(12),
                    Colour = HomeControlColours.Navy,
                },
                pathText,
            },
        };
    }

    private static Container createDecorationLayer() => new()
    {
        RelativeSizeAxes = Axes.Both,
        Children = new Drawable[]
        {
            new HomeCornerBracket
            {
                Position = new Vector2(18, 102),
            },
            new HomeDotField
            {
                Position = new Vector2(1180, 86),
                Size = new Vector2(80, 52),
                Colour = new Color4(
                    HomeControlColours.Cyan.R,
                    HomeControlColours.Cyan.G,
                    HomeControlColours.Cyan.B,
                    0.28f),
            },
            new SpriteIcon
            {
                Position = new Vector2(18, 650),
                Size = new Vector2(12),
                Icon = FontAwesome.Solid.Plus,
                Colour = HomeControlColours.Pink,
            },
            new SpriteIcon
            {
                Position = new Vector2(1250, 108),
                Size = new Vector2(10),
                Icon = FontAwesome.Solid.Plus,
                Colour = HomeControlColours.Yellow,
            },
        },
    };

    private void onLibraryChanged() => Scheduler.Add(refreshSnapshot);

    private void refreshSnapshot()
    {
        snapshot = importedChartLibrary.GetCharts();
        ManagedChartCount = snapshot.Count(chart =>
            chart.SourceKind == ImportedChartSourceKind.Managed);
        ExternalChartCount = snapshot.Count(chart =>
            chart.SourceKind == ImportedChartSourceKind.ExternalOsu);
        totalStat?.SetValue(snapshot.Count);
        managedStat?.SetValue(ManagedChartCount);
        externalStat?.SetValue(ExternalChartCount);
        if (managedPathText != null)
            managedPathText.Text = formatPath(importedChartLibrary.LibraryPath);

        if (externalPathText != null)
            externalPathText.Text = formatPath(externalOsuSettings.SongsPath.Value);
        disableExternalButton?.SetEnabled(!string.IsNullOrWhiteSpace(
            externalOsuSettings.SongsPath.Value));
        refreshRows();
    }

    private void onQueryChanged(string value)
    {
        query = value?.Trim() ?? string.Empty;
        displayLimit = pageSize;
        refreshRows();
    }

    private void setFilter(ChartLibrarySourceFilter filter)
    {
        CurrentSourceFilter = filter;
        displayLimit = pageSize;
        allFilter?.SetSelected(filter == ChartLibrarySourceFilter.All);
        managedFilter?.SetSelected(filter == ChartLibrarySourceFilter.Managed);
        externalFilter?.SetSelected(filter == ChartLibrarySourceFilter.ExternalOsu);
        refreshRows();
    }

    private void refreshRows()
    {
        if (chartRows == null)
            return;

        IEnumerable<ImportedChart> filtered = snapshot;
        filtered = CurrentSourceFilter switch
        {
            ChartLibrarySourceFilter.Managed => filtered.Where(chart =>
                chart.SourceKind == ImportedChartSourceKind.Managed),
            ChartLibrarySourceFilter.ExternalOsu => filtered.Where(chart =>
                chart.SourceKind == ImportedChartSourceKind.ExternalOsu),
            _ => filtered,
        };

        if (!string.IsNullOrWhiteSpace(query))
        {
            filtered = filtered.Where(chart =>
                contains(chart.Result.Beatmap.Title, query)
                || contains(chart.Result.Beatmap.Artist, query)
                || contains(chart.Result.Beatmap.Creator, query)
                || contains(chart.Result.Beatmap.DifficultyName, query)
                || contains(chart.PackageName, query));
        }

        ImportedChart[] ordered = filtered
                                  .OrderBy(chart => chart.Result.Beatmap.Title,
                                      StringComparer.CurrentCultureIgnoreCase)
                                  .ThenBy(chart => chart.Result.Beatmap.DifficultyName,
                                      StringComparer.CurrentCultureIgnoreCase)
                                  .ToArray();
        FilteredChartCount = ordered.Length;
        resultCountText.Text = YokkoStrings.Get(
            "chart_library.result_count",
            FilteredChartCount);

        chartRows.Clear();
        if (ordered.Length == 0)
        {
            chartRows.Add(new Container
            {
                RelativeSizeAxes = Axes.X,
                Height = 190,
                Children = new Drawable[]
                {
                    new SpriteIcon
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Y = -24,
                        Size = new Vector2(36),
                        Icon = FontAwesome.Solid.Search,
                        Colour = HomeControlColours.Cyan,
                    },
                    new SpriteText
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Y = 20,
                        Text = YokkoStrings.Get("chart_library.empty"),
                        Font = HomeTypography.Display(18),
                        Colour = HomeControlColours.Navy,
                    },
                    new SpriteText
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Y = 48,
                        Text = YokkoStrings.Get("chart_library.empty_hint"),
                        Font = HomeTypography.Body(12),
                        Colour = new Color4(
                            HomeControlColours.Navy.R,
                            HomeControlColours.Navy.G,
                            HomeControlColours.Navy.B,
                            0.62f),
                    },
                },
            });
            return;
        }

        foreach (ImportedChart chart in ordered.Take(displayLimit))
        {
            chartRows.Add(new ChartLibraryChartRow(
                chart,
                () => removeChart(chart.Id)));
        }

        if (ordered.Length > displayLimit)
        {
            chartRows.Add(new ChartLibraryLoadMoreButton(
                YokkoStrings.Get(
                    "chart_library.load_more",
                    Math.Min(pageSize, ordered.Length - displayLimit)),
                () =>
                {
                    displayLimit += pageSize;
                    refreshRows();
                }));
        }
    }

    private void openImportSelector()
    {
        if (workInProgress)
            return;

        ISystemFileSelector selector = host.CreateSystemFileSelector(
            KnownChartImporters.FileExtensions);
        selector.Selected += file => Schedule(() => importChart(file.FullName));
        selector.Present();
    }

    private void importChart(string path)
    {
        if (!beginWork(YokkoStrings.Get("chart_library.importing", Path.GetFileName(path))))
            return;

        var request = new ChartImportRequest(
            path,
            importSettings.PreferKeysounds.Value,
            importSettings.PreferSscSimfiles.Value,
            importSettings.EnableBmsScratch.Value,
            workCancellation.Token);
        completeWork(
            Task.Run(() => importedChartLibrary.ImportAsync(request), workCancellation.Token),
            results => YokkoStrings.Get("chart_library.imported", results.Count));
    }

    private async void openImportFolderSelector()
    {
        if (workInProgress)
            return;

        if (!resourceDirectoryPicker.IsAvailable)
        {
            setStatus(YokkoStrings.Get("chart_library.folder_picker_unavailable"), true);
            return;
        }

        string selectedPath;
        try
        {
            selectedPath = await resourceDirectoryPicker.PickAsync(
                importedChartLibrary.LibraryPath);
        }
        catch (Exception exception)
        {
            setStatus(exception.Message, true);
            return;
        }

        if (!string.IsNullOrWhiteSpace(selectedPath))
            importFolder(selectedPath);
    }

    private void importFolder(string path)
    {
        if (!beginWork(YokkoStrings.Get(
                "chart_library.importing_folder",
                Path.GetFileName(Path.TrimEndingDirectorySeparator(path)))))
        {
            return;
        }

        completeWork(
            Task.Run(
                () => importFolderAsync(path, workCancellation.Token),
                workCancellation.Token),
            summary => summary.FailedFileCount == 0
                ? YokkoStrings.Get(
                    "chart_library.imported_folder",
                    summary.ImportedChartCount,
                    summary.SourceFileCount)
                : YokkoStrings.Get(
                    "chart_library.imported_folder_with_failures",
                    summary.ImportedChartCount,
                    summary.SourceFileCount,
                    summary.FailedFileCount));
    }

    private async Task<FolderChartImportResult> importFolderAsync(
        string path,
        CancellationToken cancellationToken)
    {
        FolderChartImportResult summary =
            await importedChartLibrary.ImportFolderAsync(
                path,
                importSettings.PreferKeysounds.Value,
                importSettings.PreferSscSimfiles.Value,
                importSettings.EnableBmsScratch.Value,
                cancellationToken).ConfigureAwait(false);
        if (summary.SourceFileCount == 0)
        {
            throw new InvalidOperationException(
                YokkoStrings.Get("chart_library.no_importable_files").ToString());
        }

        if (summary.ImportedChartCount == 0 && summary.FailedFileCount > 0)
        {
            throw new InvalidOperationException(
                YokkoStrings.Get(
                    "chart_library.folder_import_failed",
                    summary.FailedFileCount).ToString());
        }

        return summary;
    }

    private async void chooseExternalOsuFolder()
    {
        if (workInProgress)
            return;

        if (!resourceDirectoryPicker.IsAvailable)
        {
            setStatus(YokkoStrings.Get("chart_library.folder_picker_unavailable"), true);
            return;
        }

        string initialPath = externalOsuSettings.SongsPath.Value;
        if (string.IsNullOrWhiteSpace(initialPath))
            initialPath = ExternalOsuSongsLocator.Find() ?? importedChartLibrary.LibraryPath;

        string selectedPath;
        try
        {
            selectedPath = await resourceDirectoryPicker.PickAsync(initialPath);
        }
        catch (Exception exception)
        {
            setStatus(exception.Message, true);
            return;
        }

        if (!string.IsNullOrWhiteSpace(selectedPath))
            scanExternalOsu(selectedPath);
    }

    private void autoFindExternalOsu()
    {
        if (workInProgress)
            return;

        string detected = ExternalOsuSongsLocator.Find(
            externalOsuSettings.SongsPath.Value);
        if (string.IsNullOrWhiteSpace(detected))
        {
            setStatus(YokkoStrings.Get("chart_library.osu_not_found"), true);
            return;
        }

        scanExternalOsu(detected);
    }

    private void scanExternalOsu(string path)
    {
        if (!beginWork(YokkoStrings.Get("chart_library.scanning_osu")))
            return;

        completeWork(
            importedChartLibrary.SetExternalOsuSongsPathAsync(
                path,
                workCancellation.Token),
            result =>
            {
                if (!result.Success)
                    throw new InvalidOperationException(result.Message);

                yokkoConfig.Save();
                return YokkoStrings.Get(
                    "chart_library.osu_ready",
                    result.ChartCount);
            });
    }

    private void refreshAll()
    {
        if (!beginWork(YokkoStrings.Get("chart_library.refreshing")))
            return;

        completeWork(
            refreshAllAsync(workCancellation.Token),
            count => YokkoStrings.Get("chart_library.refreshed", count));
    }

    private async Task<int> refreshAllAsync(CancellationToken cancellationToken)
    {
        int count = await importedChartLibrary.LoadFromDiskAsync(
            importSettings.PreferKeysounds.Value,
            importSettings.PreferSscSimfiles.Value,
            importSettings.EnableBmsScratch.Value,
            cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(externalOsuSettings.SongsPath.Value))
        {
            ExternalOsuLibraryResult external =
                await importedChartLibrary.RefreshExternalOsuAsync(cancellationToken)
                                          .ConfigureAwait(false);
            if (!external.Success)
                throw new InvalidOperationException(external.Message);

            count += external.ChartCount;
        }

        return count;
    }

    private void disableExternalOsu()
    {
        if (workInProgress
            || string.IsNullOrWhiteSpace(externalOsuSettings.SongsPath.Value))
        {
            return;
        }

        importedChartLibrary.DisableExternalOsu();
        yokkoConfig.Save();
        setStatus(YokkoStrings.Get("chart_library.osu_disabled"));
        refreshSnapshot();
    }

    private void removeChart(string chartId)
    {
        if (!beginWork(YokkoStrings.Get("chart_library.removing")))
            return;

        completeWork(
            importedChartLibrary.RemoveManagedChartAsync(
                chartId,
                workCancellation.Token),
            result => YokkoStrings.Get(
                "chart_library.removed",
                result.RemovedChartCount));
    }

    private bool beginWork(LocalisableString status)
    {
        if (workInProgress)
            return false;

        workInProgress = true;
        setActionsEnabled(false);
        setStatus(status);
        return true;
    }

    private void completeWork<T>(Task<T> task, Func<T, LocalisableString> success)
    {
        _ = task.ContinueWith(completed => Scheduler.Add(() =>
        {
            workInProgress = false;
            setActionsEnabled(true);

            if (!completed.IsCompletedSuccessfully)
            {
                if (completed.IsCanceled)
                    setStatus(YokkoStrings.Get("chart_library.cancelled"), true);
                else if (completed.Exception?.GetBaseException().Message
                         is { Length: > 0 } errorMessage)
                    setStatus(errorMessage, true);
                else
                    setStatus(YokkoStrings.Get("chart_library.failed"), true);
                return;
            }

            refreshSnapshot();
            setStatus(success(completed.Result));
        }), TaskScheduler.Default);
    }

    private void setActionsEnabled(bool enabled)
    {
        importButton?.SetEnabled(enabled);
        importFolderButton?.SetEnabled(enabled);
        selectOsuButton?.SetEnabled(enabled);
        refreshButton?.SetEnabled(enabled);
        autoFindButton?.SetEnabled(enabled);
        disableExternalButton?.SetEnabled(enabled
            && !string.IsNullOrWhiteSpace(externalOsuSettings.SongsPath.Value));
    }

    private void setStatus(LocalisableString text, bool error = false)
    {
        if (statusText == null)
            return;

        statusText.Text = text;
        statusText.Colour = error
            ? HomeControlColours.Pink
            : new Color4(
                HomeControlColours.Navy.R,
                HomeControlColours.Navy.G,
                HomeControlColours.Navy.B,
                0.68f);
    }

    private static bool contains(string value, string search) =>
        value?.Contains(search, StringComparison.CurrentCultureIgnoreCase) == true;

    private static LocalisableString formatPath(string path) =>
        string.IsNullOrWhiteSpace(path)
            ? YokkoStrings.Get("chart_library.not_configured")
            : path;

}
