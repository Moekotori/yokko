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
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Framework.Platform;
using osu.Framework.Screens;
using osuTK;
using osuTK.Input;
using Yokko.Core.Editing;
using Yokko.Core.Beatmaps;
using Yokko.Core.Gameplay;
using Yokko.Game.Importing;
using Yokko.Game.Localisation;
using Yokko.Game.Presentation;
using Yokko.Game.Screens.Gameplay;
using Yokko.Import;
using Yokko.Import.Osu;
using Yokko.Import.Quaver;

namespace Yokko.Game.Screens.Editor;

public partial class EditorScreen : Screen
{
    // Legacy internal editor coordinates. The centred stage is scaled against
    // the shared 1920x1080 viewport and shrinks further for smaller windows.
    private const float designedWidth = 1122;
    private const float designedHeight = 620;
    private const float referenceLayoutScale = 1.45f;
    private const int defaultVisibleRows = 24;
    private const int minVisibleRows = 12;
    private const int maxVisibleRows = 64;
    private const int rowStep = 4;
    private const int jumpStep = 16;
    private const int appendStep = 32;

    private FillFlowContainer workspace;
    private Container editorStage;
    private EditableBeatmap editableBeatmap;
    private TimelineViewport viewport;
    private readonly EditorPreviewClock previewClock = new();
    private EditorAudioWaveform audioWaveform = EditorAudioWaveform.Missing;
    private EditorSignalStrip signalStrip;
    private EditorGrid grid;
    private EditorTimelineControls timelineControls;
    private EditorInspector inspector;
    private SpriteText statusText;
    private CancellationTokenSource waveformLoadCancellation;
    private readonly Dictionary<string, EditorAudioWaveform> waveformCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly bool presentImportOnLoad;

    public EditorScreen(bool presentImportOnLoad = false)
    {
        this.presentImportOnLoad = presentImportOnLoad;
    }

    [Resolved]
    private GameHost host { get; set; }

    [Resolved]
    private YokkoImportSettings importSettings { get; set; }

    [Resolved]
    private ImportedChartLibrary importedChartLibrary { get; set; }

    [BackgroundDependencyLoader]
    private void load()
    {
        editableBeatmap = EditableBeatmap.Create(KeyMode.FourKey);
        viewport = new TimelineViewport(0, defaultVisibleRows);

        InternalChildren = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = YokkoPalette.Background,
            },
            editorStage = new Container
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Size = new Vector2(designedWidth, designedHeight),
                Child = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 16),
                    Children = new Drawable[]
                    {
                        new EditorHeader(
                            () => loadChart(KeyMode.FourKey),
                            () => loadChart(KeyMode.SevenKey),
                            importChart,
                            exportOsu,
                            playtest),
                        workspace = new FillFlowContainer
                        {
                            AutoSizeAxes = Axes.X,
                            Height = 466,
                            Direction = FillDirection.Horizontal,
                            Spacing = new Vector2(32, 0),
                        },
                        statusText = new SpriteText
                        {
                            Text = YokkoStrings.Get("editor.ready"),
                            Font = FontUsage.Default.With(size: 16),
                            Colour = YokkoPalette.TextDim,
                        },
                    }
                }
            },
        };

        rebuildWorkspace();
    }

    protected override void LoadComplete()
    {
        base.LoadComplete();

        if (presentImportOnLoad)
            Schedule(importChart);
    }

    private void loadChart(KeyMode keyMode)
    {
        cancelWaveformLoad();
        editableBeatmap = EditableBeatmap.Create(keyMode);
        viewport = new TimelineViewport(0, defaultVisibleRows);
        previewClock.Stop();
        audioWaveform = EditorAudioWaveform.Missing;
        rebuildWorkspace();
        setStatus(YokkoStrings.Get("editor.status.new_draft", (int)keyMode));
    }

    private void rebuildWorkspace()
    {
        viewport.MoveToRow(viewport.StartRow, editableBeatmap.Rows);

        signalStrip = new EditorSignalStrip(editableBeatmap, viewport, () => audioWaveform, seekPreview);

        grid = new EditorGrid(editableBeatmap, viewport, scrollRows)
        {
            Anchor = Anchor.TopLeft,
            Origin = Anchor.TopLeft,
        };
        grid.NotesChanged += refreshEditorState;

        timelineControls = new EditorTimelineControls(
            editableBeatmap,
            viewport,
            togglePreviewPlayback,
            stopPreviewPlayback,
            () => scrollRows(-jumpStep),
            () => scrollRows(-rowStep),
            () => scrollRows(rowStep),
            () => scrollRows(jumpStep),
            () => zoomTimeline(-rowStep),
            () => zoomTimeline(rowStep),
            appendRows);

        inspector = new EditorInspector(editableBeatmap, viewport)
        {
        };

        workspace.Children = new Drawable[]
        {
            new FillFlowContainer
            {
                AutoSizeAxes = Axes.X,
                Height = 466,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 8),
                Children = new Drawable[]
                {
                    signalStrip,
                    grid,
                    timelineControls,
                },
            },
            inspector,
        };

        refreshPreviewVisuals();
    }

    private void refreshEditorState()
    {
        grid.Refresh();
        signalStrip.Refresh();
        timelineControls.Refresh();
        inspector.Refresh();
        refreshPreviewVisuals();
    }

    private void refreshPreviewVisuals()
    {
        double durationMilliseconds = getPreviewDurationMilliseconds();
        double timeMilliseconds = previewClock.CurrentTimeMilliseconds;

        grid.SetPlayheadTime(timeMilliseconds);
        signalStrip.SetPlayheadTime(timeMilliseconds);
        timelineControls.RefreshPlayback(timeMilliseconds, durationMilliseconds, previewClock.IsPlaying);
    }

    private void scrollRows(int rowDelta)
    {
        int previousStart = viewport.StartRow;
        viewport.MoveByRows(rowDelta, editableBeatmap.Rows);

        if (viewport.StartRow == previousStart)
            return;

        refreshEditorState();
        setStatus(YokkoStrings.Get(
            "editor.status.timeline",
            formatSeconds(editableBeatmap.TimeAtRow(viewport.StartRow)),
            formatSeconds(editableBeatmap.TimeAtRow(viewport.EndRowExclusive))));
    }

    private void zoomTimeline(int visibleRowDelta)
    {
        int previousVisibleRows = viewport.VisibleRows;
        int nextVisibleRows = Math.Clamp(viewport.VisibleRows + visibleRowDelta, minVisibleRows, maxVisibleRows);

        if (nextVisibleRows == previousVisibleRows)
            return;

        viewport.SetVisibleRows(nextVisibleRows, editableBeatmap.Rows);
        rebuildWorkspace();
        setStatus(YokkoStrings.Get("editor.status.zoom", viewport.VisibleRows));
    }

    private void appendRows()
    {
        editableBeatmap.AppendRows(appendStep);
        viewport.MoveByRows(appendStep, editableBeatmap.Rows);
        refreshEditorState();
        setStatus(YokkoStrings.Get("editor.status.extended", editableBeatmap.Rows));
    }

    private void playtest()
    {
        previewClock.Pause();
        this.Push(new GameplaySessionScreen(
            new GameplayScreen(editableBeatmap.ToBeatmap())));
    }

    private void importChart()
    {
        ISystemFileSelector selector = host.CreateSystemFileSelector(KnownChartImporters.FileExtensions);
        selector.Selected += file => Schedule(() => importChart(file.FullName));
        selector.Present();
    }

    private void importChart(string path)
    {
        try
        {
            cancelWaveformLoad();
            ChartImportResult result = importedChartLibrary.ImportAsync(
                                                                new ChartImportRequest(
                                                                    path,
                                                                    importSettings.PreferKeysounds.Value,
                                                                    importSettings.PreferSscSimfiles.Value,
                                                                    importSettings.EnableBmsScratch.Value))
                                                            .GetAwaiter()
                                                            .GetResult()[0];
            editableBeatmap = EditableBeatmap.FromBeatmap(result.Beatmap, path);
            viewport = new TimelineViewport(0, defaultVisibleRows);
            previewClock.Stop();
            audioWaveform = EditorAudioWaveform.Missing;
            rebuildWorkspace();
            beginWaveformLoad();
            LocalisableString warning = importSettings.ShowCompatibilityWarnings.Value
                             && result.Warnings.Count > 0
                ? YokkoStrings.Get(
                    "editor.status.warning",
                    result.Warnings[0],
                    result.Warnings.Count > 1
                        ? YokkoStrings.Get(
                            "editor.status.more_warnings",
                            result.Warnings.Count - 1)
                        : string.Empty)
                : string.Empty;
            setStatus(YokkoStrings.Get(
                "editor.status.imported",
                Path.GetFileName(path),
                warning));
        }
        catch (Exception ex)
        {
            setStatus(YokkoStrings.Get("editor.status.import_failed", ex.Message));
        }
    }

    private void exportOsu()
    {
        try
        {
            string outputPath = getExportPath();
            if (editableBeatmap.SourceFormat == ChartSourceFormat.Quaver)
                QuaverBeatmapIO.WriteEditableToFile(editableBeatmap, outputPath);
            else
                OsuManiaBeatmapIO.WriteEditableToFile(editableBeatmap, outputPath);
            editableBeatmap.SourcePath = outputPath;
            inspector.Refresh();
            setStatus(YokkoStrings.Get("editor.status.exported", outputPath));
        }
        catch (Exception ex)
        {
            setStatus(YokkoStrings.Get("editor.status.export_failed", ex.Message));
        }
    }

    private string getExportPath()
    {
        string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string exportDirectory = Path.Combine(documents, "Yokko Exports");
        string fileName = string.Join("_", editableBeatmap.Title.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));

        if (string.IsNullOrWhiteSpace(fileName))
            fileName = "yokko-chart";

        string extension =
            editableBeatmap.SourceFormat == ChartSourceFormat.Quaver
                ? ".qua"
                : ".osu";
        return Path.Combine(
            exportDirectory,
            $"{fileName}-{(int)editableBeatmap.KeyMode}K{extension}");
    }

    private void setStatus(LocalisableString message)
    {
        statusText.Text = message;
    }

    private static string formatSeconds(double milliseconds) => $"{milliseconds / 1000:0.00}s";

    protected override void Update()
    {
        base.Update();

        float stageScale = CalculateResponsiveStageScale(DrawSize);
        editorStage.Scale = new Vector2(stageScale);

        if (!previewClock.Update(Time.Elapsed, getPreviewDurationMilliseconds()))
            return;

        ensurePreviewVisible();
        refreshPreviewVisuals();
    }

    internal static float CalculateResponsiveStageScale(Vector2 viewportSize)
    {
        if (viewportSize.X <= 0 || viewportSize.Y <= 0)
            return 1;

        return MathF.Min(
            referenceLayoutScale,
            MathF.Min(
                viewportSize.X / designedWidth,
                viewportSize.Y / designedHeight));
    }

    private void togglePreviewPlayback()
    {
        previewClock.Toggle(getPreviewDurationMilliseconds());
        refreshPreviewVisuals();
        setStatus(YokkoStrings.Get(
            previewClock.IsPlaying
                ? "editor.status.preview_playing"
                : "editor.status.preview_paused"));
    }

    private void stopPreviewPlayback()
    {
        previewClock.Stop();
        refreshPreviewVisuals();
        setStatus(YokkoStrings.Get("editor.status.preview_stopped"));
    }

    private void seekPreview(double timeMilliseconds)
    {
        previewClock.Seek(timeMilliseconds, getPreviewDurationMilliseconds());
        ensurePreviewVisible();
        refreshPreviewVisuals();
        setStatus(YokkoStrings.Get(
            "editor.status.preview_at",
            formatSeconds(previewClock.CurrentTimeMilliseconds)));
    }

    private void ensurePreviewVisible()
    {
        int row = editableBeatmap.ClosestRowAt(previewClock.CurrentTimeMilliseconds);

        if (row >= viewport.StartRow + 2 && row < viewport.EndRowExclusive - 2)
            return;

        int previousStart = viewport.StartRow;
        viewport.MoveToRow(row - viewport.VisibleRows / 4, editableBeatmap.Rows);

        if (viewport.StartRow != previousStart)
            refreshEditorState();
    }

    private double getPreviewDurationMilliseconds()
    {
        double lastNoteEnd = getLastNoteEndMilliseconds();
        double chartDuration = Math.Max(
            editableBeatmap.TimeAtRow(editableBeatmap.Rows),
            lastNoteEnd + editableBeatmap.TimingMap.StepAtTime(lastNoteEnd) * 4);
        return Math.Max(chartDuration, audioWaveform.DurationMilliseconds);
    }

    private double getLastNoteEndMilliseconds()
        => editableBeatmap.Notes.Count == 0
            ? 0
            : editableBeatmap.Notes.Max(note => note.EndTimeMilliseconds ?? note.StartTimeMilliseconds);

    private void beginWaveformLoad()
    {
        cancelWaveformLoad();

        string audioPath = getExistingAudioPath();
        if (audioPath == null)
        {
            audioWaveform = EditorAudioWaveform.Missing;
            refreshEditorState();
            return;
        }

        string cacheKey = getWaveformCacheKey(audioPath);
        if (waveformCache.TryGetValue(cacheKey, out EditorAudioWaveform cachedWaveform))
        {
            audioWaveform = cachedWaveform;
            refreshEditorState();
            setStatus(YokkoStrings.Get(
                "editor.status.waveform_ready",
                Path.GetFileName(audioPath)));
            return;
        }

        var cancellation = new CancellationTokenSource();
        waveformLoadCancellation = cancellation;
        audioWaveform = EditorAudioWaveform.Loading(audioPath);
        refreshEditorState();

        Task.Run(async () =>
        {
            try
            {
                EditorAudioWaveform loadedWaveform = await EditorAudioWaveformLoader.LoadAsync(audioPath, cancellation.Token).ConfigureAwait(false);

                Schedule(() =>
                {
                    if (waveformLoadCancellation != cancellation || cancellation.IsCancellationRequested)
                        return;

                    waveformCache[cacheKey] = loadedWaveform;
                    audioWaveform = loadedWaveform;
                    refreshEditorState();
                    setStatus(loadedWaveform.HasAudio
                        ? YokkoStrings.Get(
                            "editor.status.waveform_ready",
                            Path.GetFileName(audioPath))
                        : YokkoStrings.Get(
                            "editor.status.waveform_unavailable",
                            loadedWaveform.Label));
                });
            }
            catch (OperationCanceledException)
            {
            }
        }, cancellation.Token);
    }

    private void cancelWaveformLoad()
    {
        waveformLoadCancellation?.Cancel();
        waveformLoadCancellation?.Dispose();
        waveformLoadCancellation = null;
    }

    protected override bool OnKeyDown(KeyDownEvent e)
    {
        if (e.Key != Key.Escape)
            return base.OnKeyDown(e);

        cancelWaveformLoad();
        previewClock.Stop();
        this.Exit();
        return true;
    }

    private string getExistingAudioPath()
    {
        string audioPath = editableBeatmap.AudioPath;

        if (string.IsNullOrWhiteSpace(audioPath))
            return null;

        if (File.Exists(audioPath))
            return Path.GetFullPath(audioPath);

        if (!string.IsNullOrWhiteSpace(editableBeatmap.SourcePath))
        {
            string sourceDirectory = Path.GetDirectoryName(editableBeatmap.SourcePath);
            if (!string.IsNullOrWhiteSpace(sourceDirectory))
            {
                string relativeAudioPath = Path.GetFullPath(Path.Combine(sourceDirectory, audioPath));
                if (File.Exists(relativeAudioPath))
                    return relativeAudioPath;
            }
        }

        return null;
    }

    private static string getWaveformCacheKey(string audioPath)
    {
        var fileInfo = new FileInfo(audioPath);
        return $"{fileInfo.FullName}|{fileInfo.Length}|{fileInfo.LastWriteTimeUtc.Ticks}";
    }
}
