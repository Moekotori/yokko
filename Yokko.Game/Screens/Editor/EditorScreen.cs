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
using Yokko.Game.Screens.Gameplay;
using Yokko.Import;
using Yokko.Import.Osu;
using Yokko.Import.Quaver;

namespace Yokko.Game.Screens.Editor;

public partial class EditorScreen : Screen
{
    // The editor is anchored on Yokko's shared 1920x1080 layout space, the
    // same convention as SongSelectScreen. YokkoUiScalingContainer owns the
    // physical scaling, so no screen-local stage scaling is required.
    internal const float CanvasWidth = 1920;
    internal const float CanvasHeight = 1080;
    internal const float HeaderHeight = 72;
    internal const float ToolbarTop = HeaderHeight;
    internal const float ToolbarHeight = 56;
    internal const float WorkspaceLeft = 24;
    internal const float WorkspaceTop = 144;
    internal const float WorkspaceWidth = 1392;
    internal const float SignalHeight = 96;
    internal const float GridTop = 256;
    internal const float GridHeight = 700;
    internal const float TransportTop = 972;
    internal const float TransportHeight = 44;
    internal const float InspectorLeft = 1440;
    internal const float InspectorTop = WorkspaceTop;
    internal const float InspectorWidth = 456;
    internal const float InspectorHeight = 872;
    internal const float StatusBarTop = 1032;
    internal const float StatusBarHeight = 48;

    private const int defaultVisibleRows = 24;
    private const int minVisibleRows = 12;
    private const int maxVisibleRows = 64;
    private const int rowStep = 4;
    private const int jumpStep = 16;
    private const int appendStep = 32;

    private Container workspace;
    private Container inspectorHost;
    private EditableBeatmap editableBeatmap;
    private TimelineViewport viewport;
    private readonly EditorPreviewClock previewClock = new();
    private EditorAudioWaveform audioWaveform = EditorAudioWaveform.Missing;
    private EditorSignalStrip signalStrip;
    private EditorGrid grid;
    private EditorTimelineControls timelineControls;
    private EditorInspector inspector;
    private EditorStatusBar statusBar;
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
                Colour = EditorTheme.Ivory,
            },
            new EditorHeader(),
            new EditorToolbar(
                () => loadChart(KeyMode.FourKey),
                () => loadChart(KeyMode.SevenKey),
                importChart,
                exportOsu,
                playtest)
            {
                Y = ToolbarTop,
            },
            workspace = new Container
            {
                Position = new Vector2(WorkspaceLeft, WorkspaceTop),
                Size = new Vector2(WorkspaceWidth, InspectorHeight),
            },
            inspectorHost = new Container
            {
                Position = new Vector2(InspectorLeft, InspectorTop),
                Size = new Vector2(InspectorWidth, InspectorHeight),
            },
            statusBar = new EditorStatusBar
            {
                Y = StatusBarTop,
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
            Y = GridTop - WorkspaceTop,
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
            appendRows)
        {
            Y = TransportTop - WorkspaceTop,
        };

        inspector = new EditorInspector(editableBeatmap, viewport);

        workspace.Children = new Drawable[]
        {
            signalStrip,
            grid,
            timelineControls,
        };
        inspectorHost.Child = inspector;

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
        statusBar.SetStatus(message);
    }

    private static string formatSeconds(double milliseconds) => $"{milliseconds / 1000:0.00}s";

    protected override void Update()
    {
        base.Update();

        if (!previewClock.Update(Time.Elapsed, getPreviewDurationMilliseconds()))
            return;

        ensurePreviewVisible();
        refreshPreviewVisuals();
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

    #region Test hooks

    internal EditableBeatmap BeatmapForTesting => editableBeatmap;
    internal TimelineViewport ViewportForTesting => viewport;
    internal string StatusTextForTesting => statusBar.StatusText;
    internal bool IsPreviewPlayingForTesting => previewClock.IsPlaying;
    internal double PreviewTimeForTesting => previewClock.CurrentTimeMilliseconds;

    internal void CreateChartForTesting(KeyMode keyMode) => loadChart(keyMode);
    internal void ScrollRowsForTesting(int rowDelta) => scrollRows(rowDelta);
    internal void ZoomTimelineForTesting(int visibleRowDelta) => zoomTimeline(visibleRowDelta);
    internal void AppendRowsForTesting() => appendRows();
    internal void TogglePreviewForTesting() => togglePreviewPlayback();
    internal void StopPreviewForTesting() => stopPreviewPlayback();
    internal void SeekPreviewForTesting(double timeMilliseconds) => seekPreview(timeMilliseconds);

    #endregion
}
