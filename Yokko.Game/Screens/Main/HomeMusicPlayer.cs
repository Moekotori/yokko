using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
using Yokko.Game.Audio;
using Yokko.Game.Importing;
using Yokko.Game.Localisation;

namespace Yokko.Game.Screens.Main;

/// <summary>
/// 主页角落的紧凑音乐播放器，播放导入谱面所引用的真实歌曲。
/// </summary>
public partial class HomeMusicPlayer : CompositeDrawable
{
    private sealed record ImportedHomeTrack(
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
    private Task audioQueue = Task.CompletedTask;
    private IAudioEngine audioEngine;
    private int trackIndex = -1;
    private int playbackGeneration;
    private bool desiredPlaying = true;
    private bool screenActive;
    private bool disposed;
    private string loadedAudioPath;
    private double pausedProgress;
    private double currentLength;

    private SpriteText titleText;
    private SpriteText artistText;
    private SpriteText bpmText;
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

    [BackgroundDependencyLoader]
    private void load()
    {
        audioEngine = AudioEngineFactory.CreateDefault();
        importedChartLibrary.LibraryChanged += onChartLibraryChanged;
        refreshPlaylist();
    }

    protected override void LoadComplete()
    {
        base.LoadComplete();
        Scheduler.AddDelayed(spawnNote, 1100, true);
    }

    internal void Activate()
    {
        screenActive = true;
        if (desiredPlaying)
            startOrResumeCurrentTrack();
    }

    internal void Deactivate()
    {
        screenActive = false;
        pausePlayback();
    }

    private double currentProgress =>
        audioEngine?.Status.IsRunning == true
            ? audioEngine.PlaybackTimeMilliseconds
            : pausedProgress;

    private void togglePlayPause()
    {
        if (tracks.Count == 0 || !screenActive)
            return;

        desiredPlaying = !desiredPlaying;
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
        bool canSeek = pausedProgress > 0
                       && string.Equals(
                           loadedAudioPath,
                           track.AudioPath,
                           StringComparison.OrdinalIgnoreCase);
        double resumeAt = canSeek ? pausedProgress : 0;

        playPauseButton.Icon.Icon = FontAwesome.Solid.Pause;
        enqueueAudioOperation(async () =>
        {
            if (generation != playbackGeneration)
                return;

            if (canSeek)
                await audioEngine.SeekAsync(resumeAt).ConfigureAwait(false);
            else
                await audioEngine.StartAsync(
                    audioSettings.CreateStartRequest(track.AudioPath))
                                 .ConfigureAwait(false);

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

        string previousAudioPath = trackIndex >= 0 && trackIndex < tracks.Count
            ? tracks[trackIndex].AudioPath
            : null;
        ImportedHomeTrack[] refreshed = importedChartLibrary
                                       .GetCharts()
                                       .Select(chart => chart.Result.Beatmap)
                                       .Where(beatmap =>
                                           isPlayableAudioPath(beatmap.AudioPath))
                                       .GroupBy(
                                           beatmap => Path.GetFullPath(beatmap.AudioPath),
                                           StringComparer.OrdinalIgnoreCase)
                                       .Select(group =>
                                       {
                                           var beatmap = group.First();
                                           double bpm = beatmap.TimingPoints
                                                               .Where(point =>
                                                                   point.Uninherited
                                                                   && point.BeatsPerMinute > 0)
                                                               .Select(point =>
                                                                   point.BeatsPerMinute)
                                                               .FirstOrDefault();
                                           double length = beatmap.HitObjects.Count == 0
                                               ? 0
                                               : beatmap.HitObjects.Max(hitObject =>
                                                   hitObject.EndTimeMilliseconds
                                                   ?? hitObject.StartTimeMilliseconds);

                                           return new ImportedHomeTrack(
                                               group.Key,
                                               beatmap.Title,
                                               beatmap.Artist,
                                               bpm,
                                               Math.Max(0, length));
                                       })
                                       .ToArray();

        tracks = refreshed;
        if (tracks.Count == 0)
        {
            trackIndex = -1;
            pausedProgress = 0;
            currentLength = 0;
            loadedAudioPath = null;
            playbackGeneration++;
            showEmptyState();
            enqueueAudioOperation(
                () => audioEngine.StopAsync().AsTask(),
                playbackGeneration);
            return;
        }

        int preservedIndex = previousAudioPath == null
            ? -1
            : Array.FindIndex(
                refreshed,
                track => string.Equals(
                    track.AudioPath,
                    previousAudioPath,
                    StringComparison.OrdinalIgnoreCase));
        bool trackChanged = preservedIndex < 0;
        trackIndex = trackChanged ? tracks.Count - 1 : preservedIndex;

        ImportedHomeTrack current = tracks[trackIndex];
        updateTrackDisplay(current, trackChanged);

        if (trackChanged)
        {
            pausedProgress = 0;
            currentLength = current.FallbackLength;
            playbackGeneration++;
        }

        if (screenActive
            && desiredPlaying
            && (trackChanged || !audioEngine.Status.IsRunning))
            startOrResumeCurrentTrack();
    }

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

    private void onChartLibraryChanged() =>
        Scheduler.Add(refreshPlaylist);

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

        if (screenActive
            && desiredPlaying
            && currentLength > 0
            && currentProgress >= currentLength)
            nextTrack();

        progressFill.Width = currentLength <= 0
            ? 0
            : (float)Math.Clamp(currentProgress / currentLength, 0, 1);

        // BPM 圆点平缓呼吸，暂停时定格。
        pulseDot.Alpha = screenActive
                         && audioEngine?.Status.IsRunning == true
            ? 0.55f + 0.45f * MathF.Abs(MathF.Sin((float)(Time.Current / 1200 * Math.PI)))
            : 0.35f;
    }

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing)
        {
            disposed = true;
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
