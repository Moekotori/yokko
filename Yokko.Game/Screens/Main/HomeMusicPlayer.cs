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
using osu.Framework.Logging;
using osuTK;
using osuTK.Graphics;
using Yokko.Audio;
using Yokko.Core.Beatmaps;
using Yokko.Game.Audio;
using Yokko.Game.Importing;
using Yokko.Game.Localisation;
using Yokko.Game.Screens.SongSelect;

namespace Yokko.Game.Screens.Main;

/// <summary>
/// 主页角落的紧凑音乐播放器，播放导入谱面所引用的真实歌曲。
/// </summary>
public partial class HomeMusicPlayer : CompositeDrawable, ISongSelectPreviewHost
{
    internal sealed record ImportedHomeTrack(
        string AudioPath,
        string Title,
        string Artist,
        double Bpm,
        double FallbackLength);

    private static readonly string[] supportedAudioExtensions =
    [
        ".mp3",
        ".ogg",
        ".wav",
    ];

    [Resolved]
    private ImportedChartLibrary importedChartLibrary { get; set; }

    [Resolved]
    private YokkoAudioSettings audioSettings { get; set; }

    private readonly object audioQueueLock = new();
    private IReadOnlyList<ImportedHomeTrack> tracks = [];
    private readonly Dictionary<string, int> trackIndices =
        new(StringComparer.OrdinalIgnoreCase);
    private Task audioQueue = Task.CompletedTask;
    private IAudioEngine audioEngine;
    private int trackIndex = -1;
    private int playbackGeneration;
    private bool desiredPlaying;
    private bool screenActive;
    private volatile bool playlistDirty;
    private CancellationTokenSource playlistRefreshCancellation;
    private int playlistRefreshGeneration;
    private bool disposed;
    private string loadedAudioPath;
    private double pausedProgress;
    private double currentLength;
    private Task previewHandoff = Task.CompletedTask;

    private HomeWaveformVisualiser waveformVisualiser;
    private readonly Dictionary<string, AudioWaveformAnalysis> waveformCache =
        new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource waveformCancellation;
    private int waveformGeneration;
    private string waveformRequestedPath;

    private SpriteText titleText;
    private SpriteText artistText;
    private SpriteText bpmText;
    private SpriteText timeText;
    private Circle pulseDot;
    private Box progressFill;
    private PlayerButton playPauseButton;

    public HomeMusicPlayer()
    {
        Size = new Vector2(452, 72);

        InternalChildren = new Drawable[]
        {
            new Container
            {
                Position = new Vector2(-2, -2),
                Size = new Vector2(456, 76),
                Masking = true,
                CornerRadius = 16,
                Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new Color4(HomeControlColours.Navy.R, HomeControlColours.Navy.G, HomeControlColours.Navy.B, 0.28f),
                },
            },
            new Container
            {
                RelativeSizeAxes = Axes.Both,
                Masking = true,
                CornerRadius = 14,
                BorderThickness = 2,
                BorderColour = HomeControlColours.Navy,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = new Color4(1f, 1f, 1f, 0.97f),
                    },
                    new Container
                    {
                        Anchor = Anchor.BottomLeft,
                        Origin = Anchor.BottomLeft,
                        Position = new Vector2(12, -5),
                        Size = new Vector2(428, 3),
                        Child = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = new Color4(HomeControlColours.Navy.R, HomeControlColours.Navy.G, HomeControlColours.Navy.B, 0.14f),
                        },
                    },
                    new Container
                    {
                        Anchor = Anchor.BottomLeft,
                        Origin = Anchor.BottomLeft,
                        Position = new Vector2(12, -5),
                        Size = new Vector2(428, 3),
                        Child = progressFill = new Box
                        {
                            RelativeSizeAxes = Axes.X,
                            Height = 3,
                            Width = 0,
                            Colour = HomeControlColours.Pink,
                        },
                    },
                    new SeekBar(seekTo)
                    {
                        Anchor = Anchor.BottomLeft,
                        Origin = Anchor.BottomLeft,
                        Position = new Vector2(12, -7),
                        Size = new Vector2(428, 12),
                    },
                    timeText = new SpriteText
                    {
                        Anchor = Anchor.BottomRight,
                        Origin = Anchor.BottomRight,
                        Position = new Vector2(-14, -11),
                        Font = HomeTypography.Body(10),
                        Colour = new Color4(0.18f, 0.28f, 0.58f, 0.55f),
                    },
                },
            },
            new Container
            {
                Position = new Vector2(12, 12),
                Size = new Vector2(48),
                Masking = true,
                CornerRadius = 10,
                BorderThickness = 2,
                BorderColour = HomeControlColours.Navy,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = HomeControlColours.Navy,
                    },
                    new SpriteIcon
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Size = new Vector2(20),
                        Icon = FontAwesome.Solid.Music,
                        Colour = HomeControlColours.Cyan,
                    },
                },
            },
            titleText = new SpriteText
            {
                Position = new Vector2(74, 10),
                Width = 230,
                Truncate = true,
                Text = YokkoStrings.Get("main.music_no_songs"),
                Font = HomeTypography.Display(19),
                Colour = HomeControlColours.Navy,
            },
            new FillFlowContainer
            {
                Position = new Vector2(74, 37),
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(7, 0),
                Children = new Drawable[]
                {
                    pulseDot = new Circle
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Size = new Vector2(5),
                        Colour = HomeControlColours.Pink,
                    },
                    artistText = new SpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Width = 135,
                        Truncate = true,
                        Text = YokkoStrings.Get("main.music_import_hint"),
                        Font = HomeTypography.Body(13),
                        Colour = new Color4(0.18f, 0.28f, 0.58f, 1f),
                    },
                    bpmText = new SpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Text = string.Empty,
                        Font = HomeTypography.Body(13),
                        Colour = new Color4(0.18f, 0.28f, 0.58f, 0.75f),
                    },
                },
            },
            new PlayerButton(FontAwesome.Solid.StepBackward, previousTrack)
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.Centre,
                X = -114,
            },
            new PlayerButton(FontAwesome.Solid.StepForward, nextTrack)
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.Centre,
                X = -30,
            },
            playPauseButton = new PlayerButton(FontAwesome.Solid.Play, togglePlayPause, isPrimary: true)
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.Centre,
                X = -72,
            },
        };
    }

    IAudioEngine ISongSelectPreviewHost.AudioEngine =>
        audioEngine ??= AudioEngineFactory.CreateDefault();

    void ISongSelectPreviewHost.AdoptPreview(YokkoBeatmap beatmap) =>
        adoptPreview(beatmap);

    void ISongSelectPreviewHost.CompletePreviewHandoff(
        Task playbackSettled) => completePreviewHandoff(playbackSettled);

    [BackgroundDependencyLoader]
    private void load()
    {
        audioEngine ??= AudioEngineFactory.CreateDefault();
        audioSettings.MixChanged += onMixChanged;
        desiredPlaying = audioSettings.HomeMusicEnabled.Value;
        playPauseButton.Icon.Icon = desiredPlaying
            ? FontAwesome.Solid.Pause
            : FontAwesome.Solid.Play;
        importedChartLibrary.LibraryChanged += onChartLibraryChanged;
        refreshPlaylist();
    }

    private void onMixChanged()
    {
        if (audioEngine is IAudioMixControl mixControl)
            audioSettings.ApplyMixSettings(mixControl);
    }

    protected override void LoadComplete()
    {
        base.LoadComplete();
        Scheduler.AddDelayed(spawnNote, 1100, true);
    }

    internal void Activate()
    {
        screenActive = true;
        refreshPlaylistIfDirty();
        if (desiredPlaying && audioEngine?.Status.IsRunning != true)
            startOrResumeCurrentTrack();
    }

    internal void Deactivate(bool pause = true)
    {
        screenActive = false;
        if (pause)
            pausePlayback();
    }

    private double currentProgress =>
        audioEngine?.Status.IsRunning == true
            ? audioEngine.PlaybackTimeMilliseconds
            : pausedProgress;

    internal void TogglePlayPause() => togglePlayPause();

    internal void NextTrack() => nextTrack();

    internal void PreviousTrack() => previousTrack();

    private void adoptPreview(YokkoBeatmap beatmap)
    {
        if (!isPlayableAudioPath(beatmap?.AudioPath))
            return;

        string audioPath = Path.GetFullPath(beatmap.AudioPath);
        if (!trackIndices.TryGetValue(audioPath, out int index))
            return;

        bool changed = trackIndex != index;
        trackIndex = index;
        loadedAudioPath = audioPath;
        pausedProgress = audioEngine?.PlaybackTimeMilliseconds ?? 0;
        currentLength = audioEngine?.DurationMilliseconds > 0
            ? audioEngine.DurationMilliseconds
            : tracks[index].FallbackLength;
        playbackGeneration++;
        desiredPlaying = true;
        playPauseButton.Icon.Icon = FontAwesome.Solid.Pause;
        updateTrackDisplay(tracks[index], changed);
        ensureWaveformForCurrentTrack();
    }

    private void completePreviewHandoff(Task playbackSettled)
    {
        pausedProgress = audioEngine?.PlaybackTimeMilliseconds
                         ?? pausedProgress;
        if (audioEngine?.DurationMilliseconds > 0)
            currentLength = audioEngine.DurationMilliseconds;

        previewHandoff = playbackSettled ?? Task.CompletedTask;
        Logger.Log(
            $"Song-select preview handed to home at {pausedProgress:0} ms "
            + $"(running={audioEngine?.Status.IsRunning == true}).",
            LoggingTarget.Runtime,
            LogLevel.Important);
    }

    /// <summary>
    /// 挂上底部波形带；挂接时立即为当前曲目补齐波形数据。
    /// </summary>
    internal void AttachWaveform(HomeWaveformVisualiser visualiser)
    {
        waveformVisualiser = visualiser;
        waveformRequestedPath = null;
        ensureWaveformForCurrentTrack();
    }

    /// <summary>
    /// 确保波形带展示当前曲目的波形：命中缓存直接给，否则后台分析后回填。
    /// 分析期间先把波形带收平，避免短暂显示上一首的形状。
    /// </summary>
    private void ensureWaveformForCurrentTrack()
    {
        if (waveformVisualiser == null)
            return;

        if (trackIndex < 0 || trackIndex >= tracks.Count)
        {
            waveformRequestedPath = null;
            waveformGeneration++;
            waveformVisualiser.SetWaveform(null);
            return;
        }

        string path = tracks[trackIndex].AudioPath;
        if (waveformRequestedPath == path)
            return;

        waveformRequestedPath = path;

        if (waveformCache.TryGetValue(path, out AudioWaveformAnalysis cached))
        {
            waveformVisualiser.SetWaveform(cached);
            return;
        }

        waveformVisualiser.SetWaveform(null);

        int generation = ++waveformGeneration;
        waveformCancellation?.Cancel();
        waveformCancellation = new CancellationTokenSource();
        CancellationToken token = waveformCancellation.Token;

        Task.Run(async () =>
        {
            try
            {
                AudioWaveformAnalysis analysis = await AudioWaveformAnalyzer
                                                       .AnalyzeAsync(
                                                           path,
                                                           HomeWaveformVisualiser
                                                               .AnalysisPointCount,
                                                           token)
                                                       .ConfigureAwait(false);
                waveformCache[path] = analysis;
                Scheduler.Add(() =>
                {
                    if (disposed || generation != waveformGeneration)
                        return;

                    waveformVisualiser?.SetWaveform(analysis);
                });
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                Logger.Error(
                    exception,
                    "Could not analyse the home music waveform.",
                    LoggingTarget.Runtime);
                Scheduler.Add(() =>
                {
                    if (disposed || generation != waveformGeneration)
                        return;

                    waveformVisualiser?.SetWaveform(null);
                });
            }
        }, token);
    }

    /// <summary>
    /// 点击进度条跳转。暂停中只更新位置，播放中立即 Seek。
    /// </summary>
    private void seekTo(double ratio)
    {
        if (tracks.Count == 0 || currentLength <= 0)
            return;

        double target = Math.Clamp(ratio, 0, 0.999) * currentLength;
        pausedProgress = target;
        progressFill.Width = (float)(target / currentLength);

        if (audioEngine?.Status.IsRunning != true)
            return;

        int generation = playbackGeneration;
        enqueueAudioOperation(async () =>
        {
            if (generation != playbackGeneration)
                return;

            await audioEngine.SeekAsync(target).ConfigureAwait(false);
        }, generation);
    }

    private void togglePlayPause()
    {
        if (tracks.Count == 0 || !screenActive)
            return;

        desiredPlaying = !desiredPlaying;
        audioSettings.HomeMusicEnabled.Value = desiredPlaying;
        playPauseButton.Icon.Icon = desiredPlaying
            ? FontAwesome.Solid.Pause
            : FontAwesome.Solid.Play;

        if (desiredPlaying)
            startOrResumeCurrentTrack();
        else
            pausePlayback();
    }

    private void nextTrack()
    {
        if (tracks.Count > 0)
            switchTrack((trackIndex + 1) % tracks.Count);
    }

    private void previousTrack()
    {
        if (tracks.Count > 0)
            switchTrack((trackIndex + tracks.Count - 1) % tracks.Count);
    }

    private void switchTrack(int index)
    {
        trackIndex = index;
        pausedProgress = 0;
        currentLength = tracks[index].FallbackLength;
        playbackGeneration++;
        updateTrackDisplay(tracks[index], true);
        ensureWaveformForCurrentTrack();

        if (screenActive && desiredPlaying)
            startOrResumeCurrentTrack();
        else
            playPauseButton.Icon.Icon = FontAwesome.Solid.Play;
    }

    private void startOrResumeCurrentTrack()
    {
        if (disposed
            || !screenActive
            || !desiredPlaying
            || trackIndex < 0
            || trackIndex >= tracks.Count)
            return;

        ImportedHomeTrack track = tracks[trackIndex];
        int generation = playbackGeneration;
        Task handoff = previewHandoff;
        previewHandoff = Task.CompletedTask;

        playPauseButton.Icon.Icon = FontAwesome.Solid.Pause;
        enqueueAudioOperation(async () =>
        {
            await handoff.ConfigureAwait(false);
            if (generation != playbackGeneration)
                return;

            if (audioEngine is IAudioMixControl mixControl)
                audioSettings.ApplyMixSettings(mixControl);

            double resumeAt = Math.Max(
                pausedProgress,
                audioEngine.PlaybackTimeMilliseconds);
            bool canResume = resumeAt > 0
                             && string.Equals(
                                 loadedAudioPath,
                                 track.AudioPath,
                                 StringComparison.OrdinalIgnoreCase);

            if (!audioEngine.Status.IsRunning && canResume)
                await audioEngine.SeekAsync(resumeAt).ConfigureAwait(false);

            if (!audioEngine.Status.IsRunning)
            {
                await audioEngine.StartAsync(
                    audioSettings.CreateStartRequest(track.AudioPath))
                                 .ConfigureAwait(false);
                if (canResume)
                {
                    await audioEngine.SeekAsync(resumeAt)
                                     .ConfigureAwait(false);
                }
            }

            if (!audioEngine.Status.IsRunning)
                throw new InvalidOperationException(
                    "The home music audio engine returned without starting playback.");

            double duration = audioEngine.DurationMilliseconds;
            Scheduler.Add(() =>
            {
                if (disposed || generation != playbackGeneration)
                    return;

                loadedAudioPath = track.AudioPath;
                pausedProgress = resumeAt;
                if (duration > 0)
                    currentLength = duration;
            });
        }, generation);
    }

    private void pausePlayback()
    {
        if (audioEngine == null)
            return;

        pausedProgress = currentProgress;
        enqueueAudioOperation(
            () => audioEngine.PauseAsync().AsTask(),
            playbackGeneration);
    }

    private void refreshPlaylist()
    {
        if (disposed)
            return;

        applyPlaylist(BuildPlaylistProjection(
            importedChartLibrary.GetCharts(),
            CancellationToken.None));
    }

    internal static ImportedHomeTrack[] BuildPlaylistProjection(
        IReadOnlyList<ImportedChart> charts,
        CancellationToken cancellationToken)
    {
        var seenAudioPaths = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        var projected = new List<ImportedHomeTrack>();
        foreach (ImportedChart chart in charts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var beatmap = chart.Result.Beatmap;
            string audioPath = beatmap.AudioPath;
            if (string.IsNullOrWhiteSpace(audioPath)
                || !supportedAudioExtensions.Contains(
                    Path.GetExtension(audioPath),
                    StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                audioPath = Path.GetFullPath(audioPath);
            }
            catch (Exception exception) when (exception is ArgumentException
                                              or NotSupportedException
                                              or PathTooLongException)
            {
                continue;
            }

            // One set commonly has several difficulties pointing at the same
            // audio file. Probe the filesystem only once per unique song.
            if (!seenAudioPaths.Add(audioPath) || !File.Exists(audioPath))
                continue;

            double bpm = chart.Bpm
                         ?? beatmap.TimingPoints
                                   .Where(point =>
                                       point.Uninherited
                                       && point.BeatsPerMinute > 0)
                                   .Select(point => point.BeatsPerMinute)
                                   .FirstOrDefault();
            double length = chart.LengthMilliseconds
                            ?? (beatmap.HitObjects.Count == 0
                                ? 0
                                : beatmap.HitObjects.Max(hitObject =>
                                    hitObject.EndTimeMilliseconds
                                    ?? hitObject.StartTimeMilliseconds));
            projected.Add(new ImportedHomeTrack(
                audioPath,
                beatmap.Title,
                beatmap.Artist,
                bpm,
                Math.Max(0, length)));
        }

        return projected.ToArray();
    }

    private void applyPlaylist(ImportedHomeTrack[] refreshed)
    {
        if (disposed)
            return;

        string previousAudioPath = trackIndex >= 0 && trackIndex < tracks.Count
            ? tracks[trackIndex].AudioPath
            : null;
        tracks = refreshed;
        trackIndices.Clear();
        for (int index = 0; index < tracks.Count; index++)
            trackIndices[tracks[index].AudioPath] = index;
        if (tracks.Count == 0)
        {
            trackIndex = -1;
            pausedProgress = 0;
            currentLength = 0;
            loadedAudioPath = null;
            playbackGeneration++;
            showEmptyState();
            ensureWaveformForCurrentTrack();
            enqueueAudioOperation(
                () => audioEngine.StopAsync().AsTask(),
                playbackGeneration);
            return;
        }

        int preservedIndex = previousAudioPath == null
            ? -1
            : trackIndices.TryGetValue(previousAudioPath, out int previousIndex)
                ? previousIndex
                : -1;
        bool trackChanged = preservedIndex < 0;
        trackIndex = preservedIndex >= 0
            ? preservedIndex
            : previousAudioPath == null
                ? ChooseInitialTrackIndex(tracks.Count, Random.Shared)
                : tracks.Count - 1;

        ImportedHomeTrack current = tracks[trackIndex];
        updateTrackDisplay(current, trackChanged);

        if (trackChanged)
        {
            pausedProgress = 0;
            currentLength = current.FallbackLength;
            playbackGeneration++;
        }

        ensureWaveformForCurrentTrack();

        if (screenActive
            && desiredPlaying
            && (trackChanged || !audioEngine.Status.IsRunning))
            startOrResumeCurrentTrack();
    }

    internal static int ChooseInitialTrackIndex(
        int trackCount,
        Random random) =>
        random.Next(trackCount);

    private void updateTrackDisplay(
        ImportedHomeTrack track,
        bool animate)
    {
        titleText.Text = string.IsNullOrWhiteSpace(track.Title)
            ? Path.GetFileNameWithoutExtension(track.AudioPath)
            : track.Title;
        artistText.Text = string.IsNullOrWhiteSpace(track.Artist)
            ? string.Empty
            : track.Artist;
        bpmText.Text = track.Bpm > 0
            ? $"· {Math.Round(track.Bpm):0} BPM"
            : string.Empty;

        if (!animate)
            return;

        titleText.FadeInFromZero(260);
        artistText.FadeInFromZero(340);
        bpmText.FadeInFromZero(340);
    }

    private void showEmptyState()
    {
        titleText.Text = YokkoStrings.Get("main.music_no_songs");
        artistText.Text = YokkoStrings.Get("main.music_import_hint");
        bpmText.Text = string.Empty;
        progressFill.Width = 0;
        playPauseButton.Icon.Icon = FontAwesome.Solid.Play;
    }

    private static bool isPlayableAudioPath(string audioPath)
    {
        if (string.IsNullOrWhiteSpace(audioPath)
            || !File.Exists(audioPath))
            return false;

        return supportedAudioExtensions.Contains(
            Path.GetExtension(audioPath),
            StringComparer.OrdinalIgnoreCase);
    }

    private void onChartLibraryChanged(ImportedChartLibraryChange change)
    {
        // Difficulty-only completion cannot change audio paths or track
        // metadata, so rebuilding the whole home playlist would be pure work.
        if ((change.Kind & ImportedChartLibraryChangeKind.Structure) == 0)
            return;

        playlistDirty = true;
        Scheduler.AddOnce(refreshPlaylistIfDirty);
    }

    private void refreshPlaylistIfDirty()
    {
        if (!screenActive || !playlistDirty)
            return;

        playlistDirty = false;
        requestPlaylistRefresh();
    }

    private void requestPlaylistRefresh()
    {
        int generation = Interlocked.Increment(
            ref playlistRefreshGeneration);
        var cancellation = new CancellationTokenSource();
        CancellationTokenSource previous = Interlocked.Exchange(
            ref playlistRefreshCancellation,
            cancellation);
        previous?.Cancel();

        _ = Task.Run(
                () => BuildPlaylistProjection(
                    importedChartLibrary.GetCharts(),
                    cancellation.Token),
                cancellation.Token)
            .ContinueWith(task =>
            {
                Interlocked.CompareExchange(
                    ref playlistRefreshCancellation,
                    null,
                    cancellation);
                cancellation.Dispose();

                if (task.IsCanceled)
                    return;
                if (task.IsFaulted)
                {
                    Logger.Error(
                        task.Exception!.GetBaseException(),
                        "Could not refresh the home music playlist.",
                        LoggingTarget.Runtime);
                    return;
                }

                ImportedHomeTrack[] refreshed = task.Result;
                Scheduler.Add(() =>
                {
                    if (!disposed
                        && generation == Volatile.Read(
                            ref playlistRefreshGeneration))
                    {
                        applyPlaylist(refreshed);
                    }
                });
            }, TaskScheduler.Default);
    }

    private void enqueueAudioOperation(
        Func<Task> operation,
        int generation)
    {
        lock (audioQueueLock)
        {
            audioQueue = audioQueue.ContinueWith(
                async _ =>
                {
                    if (disposed)
                        return;

                    try
                    {
                        await operation().ConfigureAwait(false);
                    }
                    catch (Exception exception)
                    {
                        Logger.Error(
                            exception,
                            "Could not update home music playback.",
                            LoggingTarget.Runtime);
                        Scheduler.Add(() =>
                        {
                            if (disposed || generation != playbackGeneration)
                                return;

                            desiredPlaying = false;
                            playPauseButton.Icon.Icon = FontAwesome.Solid.Play;
                        });
                    }
                },
                TaskScheduler.Default).Unwrap();
        }
    }

    private void spawnNote()
    {
        if (!screenActive || audioEngine?.Status.IsRunning != true)
            return;

        var note = new SpriteIcon
        {
            Position = new Vector2(30, 4),
            Size = new Vector2(13),
            Icon = FontAwesome.Solid.Music,
            Colour = Color4.White,
            Alpha = 0.85f,
        };

        AddInternal(note);
        note.MoveToOffset(new Vector2(26, -52), 1600, Easing.OutQuad);
        note.RotateTo(22, 1600, Easing.OutQuad);
        note.FadeOut(1600, Easing.InQuad).Expire();
    }

    protected override void Update()
    {
        base.Update();

        if (screenActive && audioEngine?.DurationMilliseconds > 0)
            currentLength = audioEngine.DurationMilliseconds;

        if (screenActive
            && desiredPlaying
            && currentLength > 0
            && currentProgress >= currentLength)
            nextTrack();

        progressFill.Width = currentLength <= 0
            ? 0
            : (float)Math.Clamp(currentProgress / currentLength, 0, 1);

        waveformVisualiser?.UpdatePlayback(currentProgress);

        timeText.Text = tracks.Count == 0 || currentLength <= 0
            ? string.Empty
            : $"{formatTime(currentProgress)} / {formatTime(currentLength)}";

        // BPM 圆点平缓呼吸，暂停时定格。
        pulseDot.Alpha = screenActive
                         && audioEngine?.Status.IsRunning == true
            ? 0.55f + 0.45f * MathF.Abs(MathF.Sin((float)(Time.Current / 1200 * Math.PI)))
            : 0.35f;
    }

    private static string formatTime(double milliseconds)
    {
        var time = TimeSpan.FromMilliseconds(milliseconds);
        return $"{(int)time.TotalMinutes}:{time.Seconds:00}";
    }

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
        {
            disposed = true;
            Interlocked.Increment(ref playlistRefreshGeneration);
            Interlocked.Exchange(
                    ref playlistRefreshCancellation,
                    null)
                ?.Cancel();
            if (audioSettings != null)
                audioSettings.MixChanged -= onMixChanged;
            waveformCancellation?.Cancel();
            if (importedChartLibrary != null)
                importedChartLibrary.LibraryChanged -= onChartLibraryChanged;

            Task pending;
            lock (audioQueueLock)
                pending = audioQueue;

            _ = disposeAudioAfterAsync(pending);
        }

        base.Dispose(isDisposing);
    }

    private async Task disposeAudioAfterAsync(Task pending)
    {
        try
        {
            await pending.ConfigureAwait(false);
        }
        catch
        {
        }

        if (audioEngine != null)
            await audioEngine.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// 覆盖在进度条上的隐形点击区，把点击位置换算成跳转比例。
    /// </summary>
    private partial class SeekBar : ClickableContainer
    {
        private readonly Action<double> seek;

        public SeekBar(Action<double> seek)
        {
            this.seek = seek;
        }

        protected override bool OnMouseDown(MouseDownEvent e)
        {
            seek(ToLocalSpace(e.ScreenSpaceMousePosition).X / DrawWidth);
            return true;
        }
    }

    /// <summary>
    /// 播放器专用圆形按钮，主按钮为实心藏青底。
    /// </summary>
    private partial class PlayerButton : ClickableContainer
    {
        public readonly SpriteIcon Icon;
        public readonly bool IsPrimary;

        private readonly Box background;

        public PlayerButton(IconUsage icon, Action action, bool isPrimary = false)
        {
            Action = action;
            IsPrimary = isPrimary;
            Size = new Vector2(isPrimary ? 36 : 28);

            InternalChildren = new Drawable[]
            {
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Masking = true,
                    CornerRadius = Size.X / 2,
                    BorderThickness = isPrimary ? 0 : 1.5f,
                    BorderColour = new Color4(HomeControlColours.Navy.R, HomeControlColours.Navy.G, HomeControlColours.Navy.B, 0.45f),
                    Children = new Drawable[]
                    {
                        background = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = isPrimary ? HomeControlColours.Navy : Color4.White,
                        },
                        Icon = new SpriteIcon
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Size = new Vector2(isPrimary ? 14 : 12),
                            Icon = icon,
                            Colour = isPrimary ? Color4.White : HomeControlColours.Navy,
                        },
                    },
                },
            };
        }

        protected override bool OnHover(HoverEvent e)
        {
            background.FadeColour(IsPrimary ? new Color4(0.055f, 0.15f, 0.7f, 1f) : HomeControlColours.PaleCyan, 120, Easing.OutQuint);
            this.ScaleTo(1.12f, 130, Easing.OutQuint);
            return true;
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            background.FadeColour(IsPrimary ? HomeControlColours.Navy : Color4.White, 150, Easing.OutQuint);
            this.ScaleTo(1f, 150, Easing.OutQuint);
        }

        protected override bool OnMouseDown(MouseDownEvent e)
        {
            this.ScaleTo(0.9f, 400, Easing.OutQuint);
            return base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseUpEvent e)
        {
            this.ScaleTo(IsHovered ? 1.12f : 1f, 220, Easing.OutQuint);
            base.OnMouseUp(e);
        }
    }
}
