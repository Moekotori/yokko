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
using osu.Framework.Platform;
using osu.Framework.Screens;
using osuTK;
using Yokko.Core.Editing;
using Yokko.Core.Gameplay;
using Yokko.Game.Presentation;
using Yokko.Game.Screens.Gameplay;
using Yokko.Import.Osu;

namespace Yokko.Game.Screens.Editor;

public partial class EditorScreen : Screen
{
    private const float designedWidth = 1122;
    private const float designedHeight = 620;
    private const int defaultVisibleRows = 24;
    private const int minVisibleRows = 12;
    private const int maxVisibleRows = 64;
    private const int rowStep = 4;
    private const int jumpStep = 16;
    private const int appendStep = 32;

    private FillFlowContainer workspace;
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

    [Resolved]
    private GameHost host { get; set; }

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
            new Container
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
                            importOsu,
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
                            Text = "Ready. New 4K/7K, Import .osu, click grid cells, then Playtest.",
                            Font = FontUsage.Default.With(size: 16),
                            Colour = YokkoPalette.TextDim,
                        },
                    }
                }
            },
        };

        rebuildWorkspace();
    }

    private void loadChart(KeyMode keyMode)
    {
        cancelWaveformLoad();
        editableBeatmap = EditableBeatmap.Create(keyMode);
        viewport = new TimelineViewport(0, defaultVisibleRows);
        previewClock.Stop();
        audioWaveform = EditorAudioWaveform.Missing;
        rebuildWorkspace();
        setStatus($"New {(int)keyMode}K draft created.");
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
        setStatus($"Timeline {formatSeconds(viewport.StartMilliseconds(editableBeatmap.StepMilliseconds))}-{formatSeconds(viewport.EndMilliseconds(editableBeatmap.StepMilliseconds))}");
    }

    private void zoomTimeline(int visibleRowDelta)
    {
        int previousVisibleRows = viewport.VisibleRows;
        int nextVisibleRows = Math.Clamp(viewport.VisibleRows + visibleRowDelta, minVisibleRows, maxVisibleRows);

        if (nextVisibleRows == previousVisibleRows)
            return;

        viewport.SetVisibleRows(nextVisibleRows, editableBeatmap.Rows);
        rebuildWorkspace();
        setStatus($"Timeline zoom {viewport.VisibleRows} rows.");
    }

    private void appendRows()
    {
        editableBeatmap.AppendRows(appendStep);
        viewport.MoveByRows(appendStep, editableBeatmap.Rows);
        refreshEditorState();
        setStatus($"Extended chart to {editableBeatmap.Rows} rows.");
    }

    private void playtest()
    {
        previewClock.Pause();
        this.Push(new GameplayScreen(editableBeatmap.ToBeatmap()));
    }

    private void importOsu()
    {
        ISystemFileSelector selector = host.CreateSystemFileSelector([".osu"]);
        selector.Selected += file => Schedule(() => importOsu(file.FullName));
        selector.Present();
    }

    private void importOsu(string path)
    {
        try
        {
            cancelWaveformLoad();
            editableBeatmap = OsuManiaBeatmapIO.ReadEditableFromFile(path);
            viewport = new TimelineViewport(0, defaultVisibleRows);
            previewClock.Stop();
            audioWaveform = EditorAudioWaveform.Missing;
            rebuildWorkspace();
            beginWaveformLoad();
            setStatus($"Imported {Path.GetFileName(path)}.");
        }
        catch (Exception ex)
        {
            setStatus($"Import failed: {ex.Message}");
        }
    }

    private void exportOsu()
    {
        try
        {
            string outputPath = getExportPath();
            OsuManiaBeatmapIO.WriteEditableToFile(editableBeatmap, outputPath);
            editableBeatmap.SourcePath = outputPath;
            inspector.Refresh();
            setStatus($"Exported {outputPath}");
        }
        catch (Exception ex)
        {
            setStatus($"Export failed: {ex.Message}");
        }
    }

    private string getExportPath()
    {
        string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string exportDirectory = Path.Combine(documents, "Yokko Exports");
        string fileName = string.Join("_", editableBeatmap.Title.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));

        if (string.IsNullOrWhiteSpace(fileName))
            fileName = "yokko-chart";

        return Path.Combine(exportDirectory, $"{fileName}-{(int)editableBeatmap.KeyMode}K.osu");
    }

    private void setStatus(string message)
    {
        statusText.Text = message;
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
        setStatus(previewClock.IsPlaying ? "Preview playing." : "Preview paused.");
    }

    private void stopPreviewPlayback()
    {
        previewClock.Stop();
        refreshPreviewVisuals();
        setStatus("Preview stopped.");
    }

    private void seekPreview(double timeMilliseconds)
    {
        previewClock.Seek(timeMilliseconds, getPreviewDurationMilliseconds());
        ensurePreviewVisible();
        refreshPreviewVisuals();
        setStatus($"Preview {formatSeconds(previewClock.CurrentTimeMilliseconds)}.");
    }

    private void ensurePreviewVisible()
    {
        int row = Math.Max(0, (int)Math.Floor(previewClock.CurrentTimeMilliseconds / editableBeatmap.StepMilliseconds));

        if (row >= viewport.StartRow + 2 && row < viewport.EndRowExclusive - 2)
            return;

        int previousStart = viewport.StartRow;
        viewport.MoveToRow(row - viewport.VisibleRows / 4, editableBeatmap.Rows);

        if (viewport.StartRow != previousStart)
            refreshEditorState();
    }

    private double getPreviewDurationMilliseconds()
    {
        double chartDuration = Math.Max(editableBeatmap.Rows * editableBeatmap.StepMilliseconds, getLastNoteEndMilliseconds() + editableBeatmap.StepMilliseconds * 4);
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
            setStatus($"Waveform ready: {Path.GetFileName(audioPath)}.");
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
                        ? $"Waveform ready: {Path.GetFileName(audioPath)}."
                        : $"Waveform unavailable: {loadedWaveform.Label}");
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
