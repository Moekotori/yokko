using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;
using Yokko.Game.Presentation;

namespace Yokko.Game.Screens.Gameplay;

/// <summary>
/// Compact replay-only playback rail. Its pause and user-rate controls follow
/// ppy/osu's ReplayPlayer/PlaybackSettings at commit
/// 83b8a64bec19e1463353645c2d6d10c75e275b43 (MIT).
/// </summary>
internal partial class GameplayReplayControlsOverlay : CompositeDrawable
{
    private readonly Action togglePause;
    private readonly Action seekBackward;
    private readonly Action seekForward;
    private readonly Action decreaseRate;
    private readonly Action increaseRate;
    private readonly SpriteIcon pauseIcon;
    private readonly SpriteText timeText;
    private readonly SpriteText rateText;
    private readonly GameplayReplayProgressBar progressBar;

    internal string TimeText => timeText.Text.ToString();
    internal string RateText => rateText.Text.ToString();
    internal bool ShowsPausedState { get; private set; }
    internal double DisplayedProgressMilliseconds =>
        progressBar.DisplayedMilliseconds;
    internal void PreviewProgressForTest(double progress) =>
        progressBar.BeginPreview(progress);
    internal void CommitProgressForTest() => progressBar.CommitPreview();
    internal void ActivateSeekBackward() => seekBackward();
    internal void ActivateSeekForward() => seekForward();
    internal void ActivateDecreaseRate() => decreaseRate();
    internal void ActivateIncreaseRate() => increaseRate();
    internal void ActivateTogglePause() => togglePause();

    public GameplayReplayControlsOverlay(
        Action togglePause,
        Action seekBackward,
        Action seekForward,
        Action<double> seekTo,
        Action decreaseRate,
        Action increaseRate)
    {
        this.togglePause = togglePause
                           ?? throw new ArgumentNullException(
                               nameof(togglePause));
        this.seekBackward = seekBackward
                            ?? throw new ArgumentNullException(
                                nameof(seekBackward));
        this.seekForward = seekForward
                           ?? throw new ArgumentNullException(
                               nameof(seekForward));
        ArgumentNullException.ThrowIfNull(seekTo);
        this.decreaseRate = decreaseRate
                            ?? throw new ArgumentNullException(
                                nameof(decreaseRate));
        this.increaseRate = increaseRate
                            ?? throw new ArgumentNullException(
                                nameof(increaseRate));

        Anchor = Anchor.TopCentre;
        Origin = Anchor.TopCentre;
        Position = new Vector2(0, 24);
        Size = new Vector2(540, 78);
        Depth = -260;

        InternalChild = new CircularContainer
        {
            RelativeSizeAxes = Axes.Both,
            CornerRadius = 14,
            Masking = true,
            BorderThickness = 1.5f,
            BorderColour = YokkoPalette.Cyan,
            Children =
            [
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new Color4(0.025f, 0.05f, 0.18f, 0.92f),
                },
                new SpriteText
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    X = 18,
                    Text = "REPLAY",
                    Font = FontUsage.Default.With(size: 15, weight: "Bold"),
                    Colour = YokkoPalette.Rose,
                },
                timeText = new SpriteText
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    X = 92,
                    Text = "00:00 / 00:00",
                    Font = FontUsage.Default.With(size: 14, weight: "SemiBold"),
                    Colour = Color4.White,
                },
                createButton(
                    Anchor.CentreRight,
                    new Vector2(-286, 0),
                    FontAwesome.Solid.StepBackward,
                    this.seekBackward),
                createButton(
                    Anchor.CentreRight,
                    new Vector2(-237, 0),
                    FontAwesome.Solid.StepForward,
                    this.seekForward),
                createButton(
                    Anchor.CentreRight,
                    new Vector2(-188, 0),
                    FontAwesome.Solid.Minus,
                    this.decreaseRate),
                new Container
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.Centre,
                    Position = new Vector2(-139, 0),
                    Size = new Vector2(58, 38),
                    Children =
                    [
                        rateText = new SpriteText
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Text = "1.00x",
                            Font = FontUsage.Default.With(
                                size: 14,
                                weight: "Bold"),
                            Colour = YokkoPalette.Cyan,
                        },
                    ],
                },
                createButton(
                    Anchor.CentreRight,
                    new Vector2(-90, 0),
                    FontAwesome.Solid.Plus,
                    this.increaseRate),
                createButton(
                    Anchor.CentreRight,
                    new Vector2(-38, 0),
                    FontAwesome.Solid.Pause,
                    this.togglePause,
                    out pauseIcon),
                progressBar = new GameplayReplayProgressBar(seekTo)
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    Position = new Vector2(18, -6),
                    Size = new Vector2(504, 18),
                },
            ],
        };
    }

    internal void UpdateState(
        double currentMilliseconds,
        double durationMilliseconds,
        double playbackRate,
        bool paused)
    {
        currentMilliseconds = Math.Clamp(
            currentMilliseconds,
            0,
            Math.Max(0, durationMilliseconds));
        durationMilliseconds = Math.Max(0, durationMilliseconds);
        double displayedMilliseconds = progressBar.UpdateState(
            currentMilliseconds,
            durationMilliseconds);
        timeText.Text = $"{formatTime(displayedMilliseconds)} / "
                        + formatTime(durationMilliseconds);
        rateText.Text = $"{playbackRate:0.00}x";
        ShowsPausedState = paused;
        pauseIcon.Icon = paused
            ? FontAwesome.Solid.Play
            : FontAwesome.Solid.Pause;
    }

    internal void CompleteSeekPreview(double currentMilliseconds) =>
        progressBar.CompleteSeek(currentMilliseconds);

    private static GameplayReplayControlButton createButton(
        Anchor anchor,
        Vector2 position,
        IconUsage icon,
        Action action) => createButton(
        anchor,
        position,
        icon,
        action,
        out _);

    private static GameplayReplayControlButton createButton(
        Anchor anchor,
        Vector2 position,
        IconUsage icon,
        Action action,
        out SpriteIcon spriteIcon)
    {
        var button = new GameplayReplayControlButton(
            icon,
            action)
        {
            Anchor = anchor,
            Origin = Anchor.Centre,
            Position = position,
        };
        spriteIcon = button.Icon;
        return button;
    }

    private static string formatTime(double milliseconds)
    {
        var duration = TimeSpan.FromMilliseconds(milliseconds);
        return duration.TotalHours >= 1
            ? $"{(int)duration.TotalHours:00}:{duration.Minutes:00}:{duration.Seconds:00}"
            : $"{duration.Minutes:00}:{duration.Seconds:00}";
    }
}

internal partial class GameplayReplayProgressBar : CompositeDrawable
{
    private const float track_padding = 7;

    private readonly Action<double> seekCommitted;
    private readonly Box track;
    private readonly Box fill;
    private readonly CircularContainer marker;
    private double durationMilliseconds;
    private double currentMilliseconds;
    private double previewMilliseconds;
    private double committedPreviewMilliseconds = double.NaN;
    private bool pressed;

    internal double DisplayedMilliseconds => pressed
        ? previewMilliseconds
        : double.IsFinite(committedPreviewMilliseconds)
            ? committedPreviewMilliseconds
            : currentMilliseconds;

    public GameplayReplayProgressBar(Action<double> seekCommitted)
    {
        this.seekCommitted = seekCommitted
                             ?? throw new ArgumentNullException(
                                 nameof(seekCommitted));
        InternalChildren =
        [
            track = new Box
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = track_padding,
                Height = 4,
                Colour = new Color4(1, 1, 1, 0.2f),
            },
            fill = new Box
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                X = track_padding,
                Height = 4,
                Colour = YokkoPalette.Cyan,
            },
            marker = new CircularContainer
            {
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.Centre,
                Size = new Vector2(12),
                Masking = true,
                Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = YokkoPalette.Rose,
                },
            },
        ];
    }

    internal double UpdateState(
        double currentMilliseconds,
        double durationMilliseconds)
    {
        this.durationMilliseconds = Math.Max(0, durationMilliseconds);
        this.currentMilliseconds = Math.Clamp(
            currentMilliseconds,
            0,
            this.durationMilliseconds);
        updateVisual();
        return DisplayedMilliseconds;
    }

    internal void BeginPreview(double progress)
    {
        pressed = true;
        committedPreviewMilliseconds = double.NaN;
        previewMilliseconds = Math.Clamp(progress, 0, 1)
                              * durationMilliseconds;
        updateVisual();
    }

    internal void CommitPreview()
    {
        if (!pressed)
            return;

        pressed = false;
        committedPreviewMilliseconds = previewMilliseconds;
        updateVisual();
        seekCommitted(previewMilliseconds);
    }

    internal void CompleteSeek(double currentMilliseconds)
    {
        committedPreviewMilliseconds = double.NaN;
        this.currentMilliseconds = Math.Clamp(
            currentMilliseconds,
            0,
            durationMilliseconds);
        updateVisual();
    }

    protected override bool OnMouseDown(MouseDownEvent e)
    {
        if (e.Button != MouseButton.Left)
            return false;

        BeginPreview(progressAt(e.ScreenSpaceMousePosition));
        marker.ScaleTo(1.25f, 80, Easing.OutQuint);
        return true;
    }

    protected override bool OnDragStart(DragStartEvent e) => pressed;

    protected override void OnDrag(DragEvent e) =>
        BeginPreview(progressAt(e.ScreenSpaceMousePosition));

    protected override void OnMouseUp(MouseUpEvent e)
    {
        if (e.Button == MouseButton.Left)
            CommitPreview();

        marker.ScaleTo(IsHovered ? 1.12f : 1, 100, Easing.OutQuint);
        base.OnMouseUp(e);
    }

    protected override bool OnHover(HoverEvent e)
    {
        if (!pressed)
            marker.ScaleTo(1.12f, 90, Easing.OutQuint);
        return true;
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        if (!pressed)
            marker.ScaleTo(1, 100, Easing.OutQuint);
    }

    private double progressAt(Vector2 screenPosition)
    {
        float trackWidth = Math.Max(1, DrawWidth - track_padding * 2);
        return Math.Clamp(
            (ToLocalSpace(screenPosition).X - track_padding) / trackWidth,
            0,
            1);
    }

    private void updateVisual()
    {
        double progress = durationMilliseconds <= 0
            ? 0
            : Math.Clamp(
                DisplayedMilliseconds / durationMilliseconds,
                0,
                1);
        float trackWidth = Math.Max(1, DrawWidth - track_padding * 2);
        float x = track_padding + (float)progress * trackWidth;
        track.Width = trackWidth;
        fill.Width = Math.Max(0, x - track_padding);
        marker.X = x;
    }
}

internal partial class GameplayReplayControlButton : ClickableContainer
{
    internal SpriteIcon Icon { get; }

    public GameplayReplayControlButton(IconUsage icon, Action action)
    {
        Action = action;
        Size = new Vector2(40, 40);
        CornerRadius = 10;
        Masking = true;
        Children =
        [
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = new Color4(1, 1, 1, 0.09f),
            },
            Icon = new SpriteIcon
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Size = new Vector2(16),
                Icon = icon,
                Colour = Color4.White,
            },
        ];
    }
}
